using System;
using Rokas.Core.ReactiveTurns;

namespace Rokas.Core
{
    public sealed class GameSession
    {
        private enum PendingSaveKind { None, ReactiveEntry, ReactiveCheckpoint, ReactiveRetry, ReactiveRetreat, Payment, ReturnHome }

        private readonly ContractService contracts;
        private readonly EconomyService economy;
        private readonly FoodService food;
        private readonly SaveStore store;
        private PendingSaveKind pendingSave;
        private BattleCheckpoint pendingCheckpoint;

        public SaveData State { get; private set; }
        public ContractDefinition Contract { get; private set; }
        public CombatService Combat { get; private set; }
        public ReactiveCombatSession ReactiveCombat { get; private set; }
        public CombatMode CombatMode { get { return State.combatMode; } }
        public bool SaveBlocked { get; private set; }
        public string SaveError { get; private set; }
        public MessageService Messages { get; private set; }
        public LiveMessengerService LiveMessages { get; private set; }

        public event Action Changed;

        public GameSession(SaveData state, ContractDefinition contract)
            : this(state, contract, null)
        {
        }

        public GameSession(SaveData state, ContractDefinition contract, SaveStore store)
        {
            if (state == null)
            {
                throw new ArgumentNullException("state");
            }

            if (contract == null)
            {
                throw new ArgumentNullException("contract");
            }

            State = state;
            Contract = contract;
            this.store = store;
            SaveError = string.Empty;
            NormalizeIntegrationState();
            contracts = new ContractService();
            economy = new EconomyService();
            food = new FoodService();
            Combat = new CombatService(state, contract, economy);
            Messages = new MessageService(state, contract, contracts, food);
            Messages.Changed += NotifyChanged;
            LiveMessages = new LiveMessengerService(state, Messages);
            if (State.combatMode == Rokas.Core.CombatMode.ReactiveTurns && State.battleCheckpoint != null &&
                (State.phase == RunPhase.Combat || State.phase == RunPhase.Sealed || State.phase == RunPhase.Failed))
            {
                ReactiveCombat = new ReactiveCombatSession(ResolveReactiveDefinitions(State.battleCheckpoint),
                    State.battleCheckpoint.Clone());
                ReactiveCombat.Start();
            }
        }

        private CombatDefinitions ResolveReactiveDefinitions(BattleCheckpoint checkpoint)
        {
            CombatDefinitions eight = ReactiveEightEnemyDefinitions.Create(Contract, State);
            if (checkpoint == null || checkpoint.contentHash == eight.ContentHash) return eight;
            // Existing Milestone A duel saves remain loadable with their original content.
            CombatDefinitions duel = ReactiveDuelDefinitions.Create(Contract, State);
            return checkpoint.contentHash == duel.ContentHash ? duel : eight;
        }

        public bool AcceptContract()
        {
            if (SaveBlocked) return false;
            if (!contracts.Accept(State, Contract))
            {
                return false;
            }

            if (!Messages.DeliverYumikoContractContext(Contract.id))
            {
                NotifyChanged();
            }
            return true;
        }

        public bool EnsureGuildContractOffer()
        {
            if (SaveBlocked) return false;
            if (Contract == null || string.IsNullOrEmpty(Contract.id))
            {
                return false;
            }

            return Messages.DeliverIncoming(
                "guild-contract-offer:" + Contract.id,
                "guild",
                "Новый контракт доступен для принятия.",
                new MessageAttachment
                {
                    kind = MessageAttachmentKind.Contract,
                    id = "contract_" + Contract.id + "_attachment",
                    title = Contract.title ?? string.Empty,
                    body = Contract.location ?? string.Empty,
                    targetId = Contract.id
                });
        }

        public bool PrepareFood(string foodId)
        {
            if (SaveBlocked) return false;
            int yenBefore = State.yen;
            if (!food.Prepare(State, foodId))
            {
                return false;
            }

            bool delivered = State.yen < yenBefore && Messages.DeliverYumikoPaidFoodPurchase(foodId);
            if (!delivered)
            {
                NotifyChanged();
            }
            return true;
        }

        public FoodConsumeBlockReason GetFoodConsumeBlockReason(string foodId)
        {
            return food.GetConsumeBlockReason(State, foodId);
        }

        public bool ConsumeFood(string foodId)
        {
            if (SaveBlocked) return false;
            return NotifyIf(food.Consume(State, foodId));
        }

        public bool LeaveHome()
        {
            if (SaveBlocked) return false;
            return NotifyIf(contracts.LeaveHome(State));
        }

        public bool EnterPortal()
        {
            if (SaveBlocked) return false;
            if (!contracts.BeginCombat(State, Contract, food.GetAutoInterval(State, Contract))) return false;
            Combat.ResetEncounter();
            NotifyChanged();
            return true;
        }

        // Explicit eight-enemy route. The normal Portal route remains Legacy until rollout.
        public bool EnterReactiveTestEncounter()
        {
            return EnterReactiveEncounter(ReactiveEightEnemyDefinitions.Create(Contract, State));
        }

        // Keeps the accepted Milestone A duel available for regression and old checkpoint review.
        public bool EnterReactiveDuelTestEncounter()
        {
            return EnterReactiveEncounter(ReactiveDuelDefinitions.Create(Contract, State));
        }

        private bool EnterReactiveEncounter(CombatDefinitions definitions)
        {
            if (SaveBlocked || State.phase != RunPhase.Portal || State.activeContractId != Contract.id) return false;
            string runId = ContractService.GetEconomicRunId(Contract.id, State.contractRunSequence);
            if (string.IsNullOrEmpty(runId)) return false;
            BattleCheckpoint initial = BattleCheckpoint.CreateInitial(definitions, runId, 1, StableSeed(runId));
            return CommitReactiveCheckpoint(PendingSaveKind.ReactiveEntry, initial, false);
        }

        public bool SaveReactiveCheckpoint()
        {
            if (SaveBlocked || CombatMode != Rokas.Core.CombatMode.ReactiveTurns || ReactiveCombat == null ||
                State.phase != RunPhase.Combat) return false;
            BattleCheckpoint checkpoint = ReactiveCombat.GetStableCheckpoint();
            return CommitReactiveCheckpoint(PendingSaveKind.ReactiveCheckpoint, checkpoint, false);
        }

        public bool RetryEncounter()
        {
            if (SaveBlocked || CombatMode != Rokas.Core.CombatMode.ReactiveTurns || State.phase != RunPhase.Failed ||
                State.battleCheckpoint == null || State.battleCheckpoint.attemptId == long.MaxValue) return false;
            BattleCheckpoint previous = State.battleCheckpoint;
            CombatDefinitions definitions = ResolveReactiveDefinitions(previous);
            BattleCheckpoint initial = BattleCheckpoint.CreateInitial(definitions, previous.economicRunId,
                previous.attemptId + 1, previous.seed);
            return CommitReactiveCheckpoint(PendingSaveKind.ReactiveRetry, initial, false);
        }

        public bool RetryWave()
        {
            if (SaveBlocked || CombatMode != Rokas.Core.CombatMode.ReactiveTurns ||
                State.phase != RunPhase.Failed || State.battleCheckpoint == null ||
                State.battleCheckpoint.attemptId == long.MaxValue) return false;
            BattleCheckpoint previous = State.battleCheckpoint;
            // Pre-B duel saves have no wave-entry snapshot; their only retry is the encounter entry.
            if (previous.waveEntry == null) return RetryEncounter();
            BattleCheckpoint retry = previous.CreateWaveRetry(previous.attemptId + 1);
            return CommitReactiveCheckpoint(PendingSaveKind.ReactiveRetry, retry, false);
        }

        public bool RetreatReactiveEncounter()
        {
            if (SaveBlocked || CombatMode != Rokas.Core.CombatMode.ReactiveTurns || State.phase != RunPhase.Combat ||
                ReactiveCombat == null) return false;
            BattleCheckpoint checkpoint = ReactiveCombat.GetStableCheckpoint();
            checkpoint.revision++;
            checkpoint.phase = ReactivePhase.Defeat.ToString();
            checkpoint.terminalResult = CombatOutcome.Defeat.ToString();
            return CommitReactiveCheckpoint(PendingSaveKind.ReactiveRetreat, checkpoint, false);
        }

        public bool RetryBlockedSave()
        {
            if (!SaveBlocked || pendingSave == PendingSaveKind.None) return false;
            switch (pendingSave)
            {
                case PendingSaveKind.ReactiveEntry:
                case PendingSaveKind.ReactiveCheckpoint:
                case PendingSaveKind.ReactiveRetry:
                case PendingSaveKind.ReactiveRetreat:
                    return CommitReactiveCheckpoint(pendingSave, pendingCheckpoint, true);
                case PendingSaveKind.Payment:
                    return ClaimPaymentCore(true);
                case PendingSaveKind.ReturnHome:
                    return ReturnHomeCore(true);
                default:
                    return false;
            }
        }

        public bool ClickAttack(bool weakPoint)
        {
            return !SaveBlocked && CombatMode == Rokas.Core.CombatMode.Legacy && NotifyIf(Combat.ClickAttack(weakPoint));
        }

        public bool BeginAttack() { return !SaveBlocked && CombatMode == Rokas.Core.CombatMode.Legacy && NotifyIf(Combat.BeginAttack()); }
        public bool ReleaseAttack() { return !SaveBlocked && CombatMode == Rokas.Core.CombatMode.Legacy && NotifyIf(Combat.ReleaseAttack()); }
        public bool Dodge() { return !SaveBlocked && CombatMode == Rokas.Core.CombatMode.Legacy && NotifyIf(Combat.Dodge()); }
        public bool Deflect() { return !SaveBlocked && CombatMode == Rokas.Core.CombatMode.Legacy && NotifyIf(Combat.Deflect()); }
        public bool TraceRitualPoint(int index) { return !SaveBlocked && CombatMode == Rokas.Core.CombatMode.Legacy && NotifyIf(Combat.TraceRitualPoint(index)); }
        public bool ActivateResonance() { return !SaveBlocked && CombatMode == Rokas.Core.CombatMode.Legacy && NotifyIf(Combat.ActivateResonance()); }
        public bool CancelCombatInput() { return !SaveBlocked && CombatMode == Rokas.Core.CombatMode.Legacy && NotifyIf(Combat.CancelCombatInput()); }

        public void Tick(float seconds)
        {
            if (SaveBlocked) return;
            bool combatChanged = CombatMode == Rokas.Core.CombatMode.Legacy && Combat.Tick(seconds);
            LiveMessages.Tick(seconds);
            NotifyIf(combatChanged);
        }

        public bool ReturnHome()
        {
            if (SaveBlocked) return false;
            if (store != null && CombatMode == Rokas.Core.CombatMode.ReactiveTurns)
                return ReturnHomeCore(false);
            RunPhase result = State.phase;
            string contractId = State.activeContractId ?? string.Empty;
            int runIdentity = State.contractRunSequence;
            string preparedFoodId = State.preparedFoodId ?? string.Empty;
            if (!contracts.ReturnHome(State))
            {
                return false;
            }

            bool delivered = false;
            if (result == RunPhase.Sealed || result == RunPhase.Failed)
            {
                delivered = Messages.DeliverYumikoReturn(contractId, runIdentity, result, preparedFoodId);
            }
            if (!delivered)
            {
                NotifyChanged();
            }
            return true;
        }

        public bool ClaimPayment()
        {
            if (SaveBlocked) return false;
            return ClaimPaymentCore(false);
        }

        private bool ClaimPaymentCore(bool retry)
        {
            if (store != null) return CommitPayment(retry);
            if (!economy.ClaimPayment(State, Contract))
            {
                return false;
            }

            bool delivered = Messages.DeliverIncoming(
                "guild-contract-completed:" + Contract.id + ":" + State.completedRuns,
                "guild",
                "Контракт закрыт. Награда перечислена. Выполнение №" + State.completedRuns + ".");
            if (!delivered)
            {
                NotifyChanged();
            }
            return true;
        }

        public bool UpgradeWeapon()
        {
            if (SaveBlocked) return false;
            return NotifyIf(economy.UpgradeWeapon(State));
        }

        public void SetLamp(bool on)
        {
            if (SaveBlocked) return;
            if (State.lampOn == on)
            {
                return;
            }

            State.lampOn = on;
            NotifyChanged();
        }

        public void PetMame()
        {
            if (SaveBlocked) return;
            State.mameInteractions++;
            NotifyChanged();
        }

        private bool CommitReactiveCheckpoint(PendingSaveKind kind, BattleCheckpoint checkpoint, bool retry)
        {
            if (checkpoint == null) return false;
            BattleCheckpoint stable = checkpoint.Clone();
            bool success = CommitCandidate(kind, stable, retry,
                delegate(SaveData candidate)
                {
                    if (candidate.activeContractId != Contract.id ||
                        ContractService.GetEconomicRunId(candidate.activeContractId, candidate.contractRunSequence) != stable.economicRunId)
                        return false;
                    if (kind == PendingSaveKind.ReactiveEntry && candidate.phase != RunPhase.Portal) return false;
                    if (kind == PendingSaveKind.ReactiveRetry &&
                        (candidate.phase != RunPhase.Failed || candidate.combatMode != Rokas.Core.CombatMode.ReactiveTurns ||
                         candidate.battleCheckpoint == null || candidate.battleCheckpoint.attemptId + 1 != stable.attemptId ||
                         candidate.battleCheckpoint.seed != stable.seed)) return false;
                    if ((kind == PendingSaveKind.ReactiveCheckpoint || kind == PendingSaveKind.ReactiveRetreat) &&
                        (candidate.phase != RunPhase.Combat || candidate.combatMode != Rokas.Core.CombatMode.ReactiveTurns ||
                         candidate.battleCheckpoint == null || candidate.battleCheckpoint.attemptId != stable.attemptId ||
                         candidate.battleCheckpoint.revision > stable.revision)) return false;
                    candidate.combatMode = Rokas.Core.CombatMode.ReactiveTurns;
                    candidate.battleCheckpoint = stable.Clone();
                    candidate.playerHp = stable.hunterHp;
                    candidate.enemyHp = stable.enemyHp;
                    candidate.phase = stable.terminalResult == CombatOutcome.Victory.ToString() ? RunPhase.Sealed :
                        stable.terminalResult == CombatOutcome.Defeat.ToString() ? RunPhase.Failed : RunPhase.Combat;
                    return true;
                },
                delegate(SaveData durable)
                {
                    BattleCheckpoint saved = durable.battleCheckpoint;
                    return saved != null && durable.combatMode == Rokas.Core.CombatMode.ReactiveTurns &&
                        saved.economicRunId == stable.economicRunId && saved.attemptId == stable.attemptId &&
                        saved.revision >= stable.revision &&
                        string.Equals(saved.selectedTargetId, stable.selectedTargetId, StringComparison.Ordinal);
                });
            if (success && (kind == PendingSaveKind.ReactiveEntry || kind == PendingSaveKind.ReactiveRetry))
            {
                ReactiveCombat = new ReactiveCombatSession(ResolveReactiveDefinitions(State.battleCheckpoint),
                    State.battleCheckpoint.Clone());
                ReactiveCombat.Start();
            }
            if (success) NotifyChanged();
            return success;
        }

        private bool CommitPayment(bool retry)
        {
            string runId = ContractService.GetEconomicRunId(State.activeContractId, State.contractRunSequence);
            if (string.IsNullOrEmpty(runId)) return false;
            return CommitCandidate(PendingSaveKind.Payment, null, retry,
                delegate(SaveData candidate)
                {
                    if (candidate.phase != RunPhase.Payment || candidate.activeContractId != Contract.id ||
                        ContractService.GetEconomicRunId(candidate.activeContractId, candidate.contractRunSequence) != runId ||
                        !economy.ClaimPayment(candidate, Contract)) return false;
                    new MessageService(candidate, Contract, contracts, food).DeliverIncoming(
                        "guild-contract-completed:" + Contract.id + ":" + candidate.completedRuns,
                        "guild", "Контракт закрыт. Награда перечислена. Выполнение №" + candidate.completedRuns + ".");
                    return true;
                },
                delegate(SaveData durable)
                {
                    return durable.claimedEconomicRunIds != null && durable.claimedEconomicRunIds.Contains(runId) &&
                        durable.phase == RunPhase.Home;
                });
        }

        private bool ReturnHomeCore(bool retry)
        {
            RunPhase result = State.phase;
            string contractId = State.activeContractId ?? string.Empty;
            int sequence = State.contractRunSequence;
            string preparedFoodId = State.preparedFoodId ?? string.Empty;
            return CommitCandidate(PendingSaveKind.ReturnHome, null, retry,
                delegate(SaveData candidate)
                {
                    if (candidate.phase != result || candidate.activeContractId != contractId ||
                        candidate.contractRunSequence != sequence || !contracts.ReturnHome(candidate)) return false;
                    if (result == RunPhase.Sealed || result == RunPhase.Failed)
                        new MessageService(candidate, Contract, contracts, food).DeliverYumikoReturn(
                            contractId, sequence, result, preparedFoodId);
                    return true;
                },
                delegate(SaveData durable)
                {
                    return durable.contractRunSequence == sequence &&
                        (result == RunPhase.Sealed ? durable.phase == RunPhase.Payment : durable.phase == RunPhase.Home);
                });
        }

        private bool CommitCandidate(PendingSaveKind kind, BattleCheckpoint checkpoint, bool retry,
            Func<SaveData, bool> mutation, Func<SaveData, bool> alreadyDurable)
        {
            if (store == null)
            {
                if (!mutation(State)) return false;
                ClearSaveBlock();
                NotifyCommittedState(kind);
                return true;
            }

            SaveLoadResult loaded = store.Load();
            SaveData candidate;
            if (loaded.Succeeded)
            {
                candidate = loaded.Data;
            }
            else if (loaded.Status == SaveLoadStatus.NotFound)
            {
                string cloneError;
                if (!store.TryClone(State, out candidate, out cloneError))
                {
                    BlockSave(kind, checkpoint, cloneError);
                    return false;
                }
            }
            else
            {
                BlockSave(kind, checkpoint, loaded.Message);
                return false;
            }

            if (retry && alreadyDurable != null && alreadyDurable(candidate))
            {
                Publish(candidate);
                ClearSaveBlock();
                NotifyCommittedState(kind);
                return true;
            }
            if (!mutation(candidate))
            {
                SaveError = "The durable profile no longer matches the pending operation.";
                return false;
            }
            SaveWriteResult write = store.Save(candidate);
            if (!write.Succeeded)
            {
                BlockSave(kind, checkpoint, write.Message);
                return false;
            }
            Publish(candidate);
            ClearSaveBlock();
            NotifyCommittedState(kind);
            return true;
        }

        private void NotifyCommittedState(PendingSaveKind kind)
        {
            if (IsReactiveSave(kind)) return;
            if (kind == PendingSaveKind.Payment) Messages.SignalPublishedState();
            else NotifyChanged();
        }

        private static bool IsReactiveSave(PendingSaveKind kind)
        {
            return kind == PendingSaveKind.ReactiveEntry || kind == PendingSaveKind.ReactiveCheckpoint ||
                kind == PendingSaveKind.ReactiveRetry || kind == PendingSaveKind.ReactiveRetreat;
        }

        private void BlockSave(PendingSaveKind kind, BattleCheckpoint checkpoint, string reason)
        {
            SaveBlocked = true;
            SaveError = reason ?? string.Empty;
            pendingSave = kind;
            pendingCheckpoint = checkpoint == null ? null : checkpoint.Clone();
            NotifyChanged();
        }

        private void ClearSaveBlock()
        {
            SaveBlocked = false;
            SaveError = string.Empty;
            pendingSave = PendingSaveKind.None;
            pendingCheckpoint = null;
        }

        private void Publish(SaveData source)
        {
            State.version = source.version;
            State.yen = source.yen;
            State.reputation = source.reputation;
            State.spiritAsh = source.spiritAsh;
            State.weaponLevel = source.weaponLevel;
            State.completedRuns = source.completedRuns;
            State.contractRunSequence = source.contractRunSequence;
            State.phase = source.phase;
            State.activeContractId = source.activeContractId;
            State.activeDestinationId = source.activeDestinationId;
            State.preparedFoodId = source.preparedFoodId;
            State.storedFoodId = source.storedFoodId;
            State.storedFoodCount = source.storedFoodCount;
            State.combatMode = source.combatMode;
            State.battleCheckpoint = source.battleCheckpoint == null ? null : source.battleCheckpoint.Clone();
            State.claimedEconomicRunIds = source.claimedEconomicRunIds;
            State.firstClearRewardIds = source.firstClearRewardIds;
            State.enemyHp = source.enemyHp;
            State.playerHp = source.playerHp;
            State.enemyTimer = source.enemyTimer;
            State.autoTimer = source.autoTimer;
            State.clickTimer = source.clickTimer;
            State.combatTime = source.combatTime;
            State.weakPointClaimed = source.weakPointClaimed;
            State.lampOn = source.lampOn;
            State.mameInteractions = source.mameInteractions;
            if (State.messages == null) State.messages = new MessageSaveData();
            MessageSaveData targetMessages = State.messages;
            MessageSaveData sourceMessages = source.messages ?? new MessageSaveData();
            targetMessages.nextSequence = sourceMessages.nextSequence;
            targetMessages.nextLiveSequence = sourceMessages.nextLiveSequence;
            targetMessages.deliveredEventIds = sourceMessages.deliveredEventIds;
            targetMessages.conversations = sourceMessages.conversations;
            targetMessages.livePendingChains = sourceMessages.livePendingChains;
            targetMessages.livePendingReactions = sourceMessages.livePendingReactions;
            if (State.settings == null) State.settings = new SettingsData();
            SettingsData targetSettings = State.settings;
            SettingsData sourceSettings = source.settings ?? new SettingsData();
            targetSettings.masterVolume = sourceSettings.masterVolume;
            targetSettings.musicVolume = sourceSettings.musicVolume;
            targetSettings.sfxVolume = sourceSettings.sfxVolume;
            targetSettings.screenShake = sourceSettings.screenShake;
            targetSettings.glitchIntensity = sourceSettings.glitchIntensity;
            targetSettings.damageNumbers = sourceSettings.damageNumbers;
            targetSettings.fullscreen = sourceSettings.fullscreen;
        }

        private static long StableSeed(string runId)
        {
            unchecked
            {
                long hash = 2166136261;
                for (int index = 0; index < runId.Length; index++) hash = (hash ^ runId[index]) * 16777619;
                return hash & long.MaxValue;
            }
        }

        private void NormalizeIntegrationState()
        {
            if (State.version == 1)
            {
                State.version = SaveData.CurrentVersion;
                State.combatMode = Rokas.Core.CombatMode.Legacy;
                State.battleCheckpoint = null;
            }
            if (State.claimedEconomicRunIds == null) State.claimedEconomicRunIds = new System.Collections.Generic.List<string>();
            if (State.firstClearRewardIds == null) State.firstClearRewardIds = new System.Collections.Generic.List<string>();
            State.storedFoodId = State.storedFoodId ?? string.Empty;
            if (State.storedFoodCount <= 0 || State.storedFoodId.Length == 0)
            {
                State.storedFoodCount = 0;
                State.storedFoodId = string.Empty;
            }
            if (State.contractRunSequence < 0)
            {
                State.contractRunSequence = 0;
            }

            int minimumRunSequence = Math.Max(0, State.completedRuns);
            if (!string.IsNullOrEmpty(State.activeContractId) && State.phase != RunPhase.Home &&
                minimumRunSequence < int.MaxValue)
            {
                minimumRunSequence++;
            }
            if (State.contractRunSequence < minimumRunSequence)
            {
                State.contractRunSequence = minimumRunSequence;
            }
        }

        private bool NotifyIf(bool changed)
        {
            if (changed)
            {
                NotifyChanged();
            }
            return changed;
        }

        private void NotifyChanged()
        {
            Action handler = Changed;
            if (handler != null)
            {
                handler();
            }
        }
    }
}
