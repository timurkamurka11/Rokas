using System;

namespace Rokas.Core.ReactiveTurns
{
    // Flat, serializable wave-entry state avoids recursive BattleCheckpoint graphs in Unity JsonUtility.
    [Serializable]
    public sealed class BattleWaveEntrySnapshot
    {
        public string phase;
        public int waveIndex;
        public string waveInstanceId;
        public string selectedTargetId;
        public bool restApplied;
        public long nowTick;
        public int enemyActionCount;
        public int defensiveHitCount;
        public long authoredDurationUs;
        public int hunterHp;
        public int hunterAp;
        public int enemyHp;
        public int enemySeal;
        public int defenseApThisInterval;
        public bool defendActive;
        public BattleActorSnapshot[] actors;
        public BattleQueueEntrySnapshot[] queue;
        public BattleSealSnapshot[] sealStates;

        public static BattleWaveEntrySnapshot FromCheckpoint(BattleCheckpoint checkpoint)
        {
            if (checkpoint == null) throw new ArgumentNullException("checkpoint");
            BattleCheckpoint copy = checkpoint.Clone();
            return new BattleWaveEntrySnapshot {
                phase = copy.phase, waveIndex = copy.waveIndex,
                waveInstanceId = copy.waveInstanceId, selectedTargetId = copy.selectedTargetId,
                restApplied = copy.restApplied, nowTick = copy.nowTick,
                enemyActionCount = copy.enemyActionCount,
                defensiveHitCount = copy.defensiveHitCount,
                authoredDurationUs = copy.authoredDurationUs,
                hunterHp = copy.hunterHp, hunterAp = copy.hunterAp,
                enemyHp = copy.enemyHp, enemySeal = copy.enemySeal,
                defenseApThisInterval = copy.defenseApThisInterval,
                defendActive = copy.defendActive, actors = copy.actors,
                queue = copy.queue, sealStates = copy.sealStates
            };
        }

        public BattleWaveEntrySnapshot Clone()
        {
            var clone = (BattleWaveEntrySnapshot)MemberwiseClone();
            if (actors != null)
            {
                clone.actors = new BattleActorSnapshot[actors.Length];
                for (int i = 0; i < actors.Length; i++)
                    if (actors[i] != null)
                        clone.actors[i] = new BattleActorSnapshot {
                            id = actors[i].id, definitionId = actors[i].definitionId,
                            hp = actors[i].hp, seal = actors[i].seal, ap = actors[i].ap,
                            naturalTurns = actors[i].naturalTurns
                        };
            }
            if (queue != null)
            {
                clone.queue = new BattleQueueEntrySnapshot[queue.Length];
                for (int i = 0; i < queue.Length; i++)
                    if (queue[i] != null)
                        clone.queue[i] = new BattleQueueEntrySnapshot {
                            actorId = queue[i].actorId, nextTick = queue[i].nextTick,
                            spawnOrdinal = queue[i].spawnOrdinal, speed = queue[i].speed,
                            alive = queue[i].alive, active = queue[i].active,
                            anchorDelayed = queue[i].anchorDelayed,
                            skipNextTurn = queue[i].skipNextTurn,
                            naturalTurns = queue[i].naturalTurns
                        };
            }
            if (sealStates != null)
            {
                clone.sealStates = new BattleSealSnapshot[sealStates.Length];
                for (int i = 0; i < sealStates.Length; i++)
                    if (sealStates[i] != null) clone.sealStates[i] = sealStates[i].Clone();
            }
            return clone;
        }
    }

    public sealed partial class BattleCheckpoint
    {
        public BattleCheckpoint CreateWaveRetry(long nextAttemptId)
        {
            if (waveEntry == null) throw new InvalidOperationException("Wave entry is unavailable.");
            if (nextAttemptId <= attemptId) throw new ArgumentOutOfRangeException("nextAttemptId");
            BattleWaveEntrySnapshot entry = waveEntry.Clone();
            BattleCheckpoint retry = Clone();
            retry.attemptId = nextAttemptId;
            retry.revision = 0;
            retry.nextActionOrdinal = 1;
            retry.commands = new BattleCommandSnapshot[0];
            retry.terminalResult = null;
            retry.phase = entry.phase;
            retry.waveIndex = entry.waveIndex;
            retry.waveInstanceId = economicRunId + ":" + nextAttemptId + ":wave:" +
                (entry.waveIndex + 1);
            retry.selectedTargetId = entry.selectedTargetId;
            retry.restApplied = entry.restApplied;
            retry.nowTick = entry.nowTick;
            retry.enemyActionCount = entry.enemyActionCount;
            retry.defensiveHitCount = entry.defensiveHitCount;
            retry.authoredDurationUs = entry.authoredDurationUs;
            retry.hunterHp = entry.hunterHp;
            retry.hunterAp = entry.hunterAp;
            retry.enemyHp = entry.enemyHp;
            retry.enemySeal = entry.enemySeal;
            retry.defenseApThisInterval = entry.defenseApThisInterval;
            retry.defendActive = entry.defendActive;
            retry.actors = entry.actors;
            retry.queue = entry.queue;
            retry.sealStates = entry.sealStates;
            retry.waveEntry = entry.Clone();
            retry.waveEntry.waveInstanceId = retry.waveInstanceId;
            return retry;
        }
    }
}
