using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace Rokas.Core
{
    public interface ISaveCodec
    {
        string Serialize(SaveData data);
        bool TryDeserialize(string text, out SaveData data, out string error);
    }

    public enum SaveLoadStatus
    {
        LoadedPrimary,
        RecoveredBackup,
        NotFound,
        Corrupt,
        FutureVersion
    }

    public enum SaveWriteStatus
    {
        Saved,
        InvalidData,
        FutureVersionPreserved,
        SerializationFailed,
        IoFailure
    }

    // Precise storage boundary for deterministic fault injection and diagnostics.
    public enum SaveCommitStage
    {
        BeforeTemporaryWrite,
        AfterTemporaryFlush,
        AfterReplace
    }

    public sealed class SaveLoadResult
    {
        public readonly SaveLoadStatus Status;
        public readonly SaveData Data;
        public readonly string Message;

        public bool Succeeded
        {
            get { return Status == SaveLoadStatus.LoadedPrimary || Status == SaveLoadStatus.RecoveredBackup; }
        }

        internal SaveLoadResult(SaveLoadStatus status, SaveData data, string message)
        {
            Status = status;
            Data = data;
            Message = message ?? string.Empty;
        }
    }

    public sealed class SaveWriteResult
    {
        public readonly SaveWriteStatus Status;
        public readonly string Message;

        public bool Succeeded
        {
            get { return Status == SaveWriteStatus.Saved; }
        }

        internal SaveWriteResult(SaveWriteStatus status, string message)
        {
            Status = status;
            Message = message ?? string.Empty;
        }
    }

    public sealed class SaveStore
    {
        private enum CandidateStatus
        {
            Missing,
            Valid,
            Future,
            Invalid,
            IoFailure
        }

        private sealed class Candidate
        {
            public CandidateStatus Status;
            public SaveData Data;
            public string Message = string.Empty;
        }

        private readonly string directoryPath;
        private readonly ISaveCodec codec;
        private readonly Action<SaveCommitStage> commitObserver;

        public string PrimaryPath { get; private set; }
        public string BackupPath { get; private set; }

        public SaveStore(string directoryPath, ISaveCodec codec, string fileName = "save.json")
            : this(directoryPath, codec, fileName, null)
        {
        }

        internal SaveStore(string directoryPath, ISaveCodec codec, string fileName,
            Action<SaveCommitStage> commitObserver)
        {
            if (string.IsNullOrEmpty(directoryPath))
            {
                throw new ArgumentException("A save directory is required.", "directoryPath");
            }
            if (codec == null)
            {
                throw new ArgumentNullException("codec");
            }
            if (string.IsNullOrEmpty(fileName) || Path.GetFileName(fileName) != fileName)
            {
                throw new ArgumentException("The save file name must be a file name without directories.", "fileName");
            }

            this.directoryPath = directoryPath;
            this.codec = codec;
            this.commitObserver = commitObserver;
            PrimaryPath = Path.Combine(directoryPath, fileName);
            BackupPath = PrimaryPath + ".bak";
        }

        public SaveLoadResult Load()
        {
            Candidate primary = ReadCandidate(PrimaryPath);
            if (primary.Status == CandidateStatus.Valid)
            {
                return new SaveLoadResult(SaveLoadStatus.LoadedPrimary, primary.Data, string.Empty);
            }
            if (primary.Status == CandidateStatus.Future)
            {
                return new SaveLoadResult(SaveLoadStatus.FutureVersion, primary.Data, primary.Message);
            }

            Candidate backup = ReadCandidate(BackupPath);
            if (backup.Status == CandidateStatus.Valid)
            {
                return new SaveLoadResult(
                    SaveLoadStatus.RecoveredBackup,
                    backup.Data,
                    "Primary save unavailable: " + primary.Message);
            }
            if (backup.Status == CandidateStatus.Future)
            {
                return new SaveLoadResult(SaveLoadStatus.FutureVersion, backup.Data, backup.Message);
            }
            if (primary.Status == CandidateStatus.Missing && backup.Status == CandidateStatus.Missing)
            {
                return new SaveLoadResult(SaveLoadStatus.NotFound, null, "No save or backup exists.");
            }

            return new SaveLoadResult(
                SaveLoadStatus.Corrupt,
                null,
                "Primary save: " + primary.Message + " Backup save: " + backup.Message);
        }

        public SaveWriteResult Save(SaveData data)
        {
            string validationError;
            if (!ValidateCurrent(data, out validationError))
            {
                return new SaveWriteResult(SaveWriteStatus.InvalidData, validationError);
            }

            string serialized;
            try
            {
                serialized = codec.Serialize(data);
            }
            catch (Exception exception)
            {
                return new SaveWriteResult(SaveWriteStatus.SerializationFailed, exception.Message);
            }

            if (string.IsNullOrEmpty(serialized))
            {
                return new SaveWriteResult(SaveWriteStatus.SerializationFailed, "Serializer returned empty text.");
            }

            int serializedVersion;
            string shapeError;
            if (!SaveJsonShape.TryReadVersion(serialized, out serializedVersion, out shapeError))
            {
                return SerializationFailure(shapeError);
            }
            if (serializedVersion != SaveData.CurrentVersion)
            {
                return SerializationFailure("Serializer emitted save version " + serializedVersion + ".");
            }
            if (!SaveJsonShape.HasRequiredShape(serialized, out shapeError))
            {
                return SerializationFailure(shapeError);
            }

            SaveData verification;
            string codecError;
            if (!TryDeserialize(serialized, out verification, out codecError))
            {
                return SerializationFailure(codecError);
            }
            if (!ValidateCurrent(verification, out validationError))
            {
                return SerializationFailure(validationError);
            }

            Candidate primary = ReadCandidate(PrimaryPath);
            Candidate backup = ReadCandidate(BackupPath);
            if (primary.Status == CandidateStatus.Future || backup.Status == CandidateStatus.Future)
            {
                return new SaveWriteResult(
                    SaveWriteStatus.FutureVersionPreserved,
                    "A future-version save was preserved and the write was blocked.");
            }
            if (primary.Status == CandidateStatus.IoFailure || backup.Status == CandidateStatus.IoFailure)
            {
                return new SaveWriteResult(SaveWriteStatus.IoFailure, "Existing save files could not be inspected safely.");
            }

            string temporaryPath = PrimaryPath + ".tmp";
            string corruptPath = PrimaryPath + ".corrupt-" +
                                 DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" +
                                 Guid.NewGuid().ToString("N");
            bool quarantinedCorruption = false;
            try
            {
                Directory.CreateDirectory(directoryPath);
                Observe(SaveCommitStage.BeforeTemporaryWrite);
                WriteUtf8Durably(temporaryPath, serialized);
                Observe(SaveCommitStage.AfterTemporaryFlush);

                if (primary.Status == CandidateStatus.Valid)
                {
                    File.Replace(temporaryPath, PrimaryPath, BackupPath, true);
                }
                else if (File.Exists(PrimaryPath))
                {
                    File.Replace(temporaryPath, PrimaryPath, corruptPath, true);
                    quarantinedCorruption = true;
                }
                else
                {
                    File.Move(temporaryPath, PrimaryPath);
                }

                Observe(SaveCommitStage.AfterReplace);

                return new SaveWriteResult(
                    SaveWriteStatus.Saved,
                    quarantinedCorruption ? "Invalid primary was preserved at " + corruptPath + "." : string.Empty);
            }
            catch (Exception exception)
            {
                return new SaveWriteResult(SaveWriteStatus.IoFailure, exception.Message);
            }
            finally
            {
                TryDelete(temporaryPath);
            }
        }

        private Candidate ReadCandidate(string path)
        {
            if (!File.Exists(path))
            {
                return new Candidate { Status = CandidateStatus.Missing, Message = "File does not exist." };
            }

            string text;
            try
            {
                text = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception exception)
            {
                return new Candidate { Status = CandidateStatus.IoFailure, Message = exception.Message };
            }

            SaveData data;
            string error;
            int serializedVersion;
            if (!SaveJsonShape.TryReadVersion(text, out serializedVersion, out error))
            {
                return new Candidate { Status = CandidateStatus.Invalid, Message = "JSON shape failed: " + error };
            }
            if (serializedVersion > SaveData.CurrentVersion)
            {
                TryDeserialize(text, out data, out error);
                return new Candidate
                {
                    Status = CandidateStatus.Future,
                    Data = data,
                    Message = "Save version " + serializedVersion + " is newer than supported version " + SaveData.CurrentVersion + "."
                };
            }
            if (!SaveJsonShape.HasRequiredShape(text, out error))
            {
                return new Candidate { Status = CandidateStatus.Invalid, Message = "JSON shape failed: " + error };
            }
            if (!TryDeserialize(text, out data, out error) || data == null)
            {
                return new Candidate { Status = CandidateStatus.Invalid, Message = "Decode failed: " + error };
            }
            if (data.version != serializedVersion)
            {
                return new Candidate { Status = CandidateStatus.Invalid, Message = "Decoded save version differs from JSON version." };
            }
            if (serializedVersion == 1)
            {
                data.version = SaveData.CurrentVersion;
                data.combatMode = CombatMode.Legacy;
                data.battleCheckpoint = null;
                data.claimedEconomicRunIds = new List<string>();
                data.firstClearRewardIds = new List<string>();
            }
            string validationError;
            if (!ValidateCurrent(data, out validationError))
            {
                return new Candidate { Status = CandidateStatus.Invalid, Data = data, Message = validationError };
            }

            return new Candidate { Status = CandidateStatus.Valid, Data = data };
        }

        private bool TryDeserialize(string text, out SaveData data, out string error)
        {
            try
            {
                return codec.TryDeserialize(text, out data, out error);
            }
            catch (Exception exception)
            {
                data = null;
                error = exception.Message;
                return false;
            }
        }

        internal bool TryClone(SaveData source, out SaveData copy, out string error)
        {
            copy = null;
            if (source == null)
            {
                error = "Save data is null.";
                return false;
            }
            try
            {
                return TryDeserialize(codec.Serialize(source), out copy, out error) && copy != null;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static bool ValidateCurrent(SaveData data, out string error)
        {
            if (data == null)
            {
                error = "Save data is null.";
                return false;
            }
            if (data.version != SaveData.CurrentVersion)
            {
                error = "Unsupported save version " + data.version + ".";
                return false;
            }
            if (data.yen < 0 || data.reputation < 0 || data.spiritAsh < 0 ||
                data.weaponLevel < 1 || data.weaponLevel > SaveData.MaxWeaponLevel ||
                data.completedRuns < 0 || data.contractRunSequence < 0 || data.mameInteractions < 0)
            {
                error = "Save contains invalid progression values.";
                return false;
            }
            if (!Enum.IsDefined(typeof(RunPhase), data.phase))
            {
                error = "Save contains an unknown run phase.";
                return false;
            }
            if (!Enum.IsDefined(typeof(CombatMode), data.combatMode) ||
                data.claimedEconomicRunIds == null || data.firstClearRewardIds == null ||
                !ValidLedger(data.claimedEconomicRunIds) || !ValidLedger(data.firstClearRewardIds))
            {
                error = "Save contains invalid combat mode or reward ledger.";
                return false;
            }
            if (data.combatMode == CombatMode.ReactiveTurns &&
                (data.phase == RunPhase.Combat || data.phase == RunPhase.Sealed ||
                 data.phase == RunPhase.Failed || data.phase == RunPhase.Payment) &&
                (data.battleCheckpoint == null || data.battleCheckpoint.schemaVersion != 2 ||
                 data.battleCheckpoint.economicRunId != ContractService.GetEconomicRunId(
                     data.activeContractId, data.contractRunSequence) ||
                 data.battleCheckpoint.attemptId < 1 || data.battleCheckpoint.revision < 0 ||
                 data.battleCheckpoint.actors == null || data.battleCheckpoint.queue == null ||
                 (data.phase == RunPhase.Sealed || data.phase == RunPhase.Payment) &&
                 data.battleCheckpoint.terminalResult != "Victory" ||
                 data.phase == RunPhase.Failed && data.battleCheckpoint.terminalResult != "Defeat"))
            {
                error = "Reactive combat is missing a valid stable checkpoint.";
                return false;
            }
            if (data.settings == null ||
                !Finite(data.enemyHp) || !Finite(data.playerHp) ||
                !Finite(data.enemyTimer) || !Finite(data.autoTimer) ||
                !Finite(data.clickTimer) || !Finite(data.combatTime) ||
                !Finite(data.settings.masterVolume) || !Finite(data.settings.musicVolume) ||
                !Finite(data.settings.sfxVolume) || !Finite(data.settings.glitchIntensity))
            {
                error = "Save contains missing settings or invalid combat values.";
                return false;
            }
            if (data.enemyHp < 0f || data.playerHp < 0f || data.enemyTimer < 0f ||
                data.autoTimer < 0f || data.clickTimer < 0f || data.combatTime < 0f)
            {
                error = "Save contains negative combat values.";
                return false;
            }
            if (data.phase != RunPhase.Home && string.IsNullOrEmpty(data.activeContractId))
            {
                error = "An active run is missing its contract ID.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool Finite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool ValidLedger(List<string> ids)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < ids.Count; index++)
            {
                if (string.IsNullOrEmpty(ids[index]) || !seen.Add(ids[index])) return false;
            }
            return true;
        }

        private static SaveWriteResult SerializationFailure(string message)
        {
            return new SaveWriteResult(
                SaveWriteStatus.SerializationFailed,
                "Serialized save failed validation: " + message);
        }

        private static void WriteUtf8Durably(string path, string text)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(text);
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        private void Observe(SaveCommitStage stage)
        {
            if (commitObserver != null) commitObserver(stage);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }
}
