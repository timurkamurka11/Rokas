using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rokas.Core;
using Rokas.Core.ReactiveTurns;

namespace Rokas.Core.Tests
{
    public static class ReactiveTurnsSaveTests
    {
        public static void RunAll()
        {
            V1EveryRunPhaseMigratesWithoutChangingTheRoute();
            V1ActiveCombatMigratesAsLegacyWithoutLosingProfile();
            V1PendingPaymentRemainsClaimable();
            ReactiveCheckpointCannotBelongToAnotherRun();
            ReactiveEntryWaitsForDurableInitialCheckpoint();
            ReactiveEntryPublishesAnAlreadyStartedSession();
            MidActionSaveReplaysTheStablePreActionDecision();
            FailedTargetChangeSaveRetryRequiresTheNewTarget();
            PostReplaceTargetChangeRetryReconcilesTheNewTarget();
            RetreatRetryKeepsEconomicRunAndSeed();
            FailedPaymentWriteCanRetryWithoutDuplicatingReward();
            FailedTemporaryWriteNeverPublishesHalfAPayment();
            PostReplaceFailureReconcilesTheDurablePayment();
            PaymentUsesLatestPersistedMessengerState();
            RepeatablePaymentKeepsFirstClearRewardIdSingular();
        }

        private static void V1EveryRunPhaseMigratesWithoutChangingTheRoute()
        {
            foreach (RunPhase phase in Enum.GetValues(typeof(RunPhase)))
            {
                WithDirectory(delegate(string directory)
                {
                    SaveStore store = new SaveStore(directory, new JsonCodec());
                    SaveData legacy = new SaveData
                    {
                        version = 1,
                        phase = phase,
                        activeContractId = phase == RunPhase.Home ? string.Empty : "contract_subway_001",
                        contractRunSequence = phase == RunPhase.Home ? 0 : 3,
                        yen = 941,
                        preparedFoodId = phase == RunPhase.Home ? string.Empty : FoodService.GreenTeaId
                    };
                    File.WriteAllText(store.PrimaryPath, LegacyJson(legacy));

                    SaveLoadResult loaded = store.Load();
                    True(loaded.Succeeded, "v1 " + phase + " must load");
                    Equal(SaveData.CurrentVersion, loaded.Data.version, "v1 " + phase + " migrates to v2");
                    Equal(CombatMode.Legacy, loaded.Data.combatMode, "v1 " + phase + " stays on Legacy route");
                    Equal(phase, loaded.Data.phase, "v1 " + phase + " preserves the phase");
                    Equal(941, loaded.Data.yen, "v1 " + phase + " preserves currency");
                    True(store.Save(loaded.Data).Succeeded, "migrated " + phase + " profile must save");
                    Equal(phase, store.Load().Data.phase, "v2 reload preserves " + phase);
                });
            }
        }

        private static void V1ActiveCombatMigratesAsLegacyWithoutLosingProfile()
        {
            WithDirectory(delegate(string directory)
            {
                SaveStore store = new SaveStore(directory, new JsonCodec());
                SaveData legacy = new SaveData
                {
                    version = 1,
                    phase = RunPhase.Combat,
                    activeContractId = "contract_subway_001",
                    contractRunSequence = 4,
                    yen = 713,
                    reputation = 9,
                    spiritAsh = 2,
                    preparedFoodId = "food_green_tea",
                    storedFoodId = "food_onigiri",
                    storedFoodCount = 1,
                    enemyHp = 71f,
                    playerHp = 63f
                };
                new MessageService(legacy).DeliverIncoming("migration:kaito", "kaito", "Keep this.");
                File.WriteAllText(store.PrimaryPath, LegacyJson(legacy));

                SaveLoadResult loaded = store.Load();
                True(loaded.Succeeded, "a complete v1 combat profile must load");
                Equal(SaveData.CurrentVersion, loaded.Data.version, "v1 must migrate to the current schema");
                Equal(CombatMode.Legacy, loaded.Data.combatMode, "an unfinished v1 combat stays Legacy");
                Equal(RunPhase.Combat, loaded.Data.phase, "migration preserves the phase");
                Equal(4, loaded.Data.contractRunSequence, "migration preserves run identity");
                Equal(713, loaded.Data.yen, "migration preserves currency");
                Equal(63f, loaded.Data.playerHp, "migration preserves combat HP");
                Equal("food_green_tea", loaded.Data.preparedFoodId, "migration preserves food");
                Equal(1, loaded.Data.storedFoodCount, "migration preserves inventory");
                True(new MessageService(loaded.Data).HasDeliveredEvent("migration:kaito"), "migration preserves delivered messages");
                True(store.Save(loaded.Data).Succeeded, "migrated v2 profile must save");
                Equal(CombatMode.Legacy, store.Load().Data.combatMode, "Legacy route survives reload");
            });
        }

        private static void V1PendingPaymentRemainsClaimable()
        {
            WithDirectory(delegate(string directory)
            {
                SaveStore store = new SaveStore(directory, new JsonCodec());
                SaveData legacy = new SaveData
                {
                    version = 1,
                    phase = RunPhase.Payment,
                    activeContractId = "contract_subway_001",
                    contractRunSequence = 1,
                    yen = 520
                };
                File.WriteAllText(store.PrimaryPath, LegacyJson(legacy));
                GameSession session = new GameSession(store.Load().Data, new ContractDefinition(), store);

                True(session.ClaimPayment(), "v1 payment should still be claimable");
                Equal(2320, store.Load().Data.yen, "one legacy payment must persist");
                Equal(false, session.ClaimPayment(), "same payment cannot be claimed twice");
            });
        }

        private static void ReactiveEntryWaitsForDurableInitialCheckpoint()
        {
            WithDirectory(delegate(string directory)
            {
                string blocker = Path.Combine(directory, "blocked");
                File.WriteAllText(blocker, "not a directory");
                SaveStore store = new SaveStore(blocker, new JsonCodec());
                SaveData state = PortalState();
                GameSession session = new GameSession(state, new ContractDefinition(), store);

                Equal(false, session.EnterReactiveTestEncounter(), "entry must fail when checkpoint cannot be written");
                Equal(RunPhase.Portal, state.phase, "failed entry must not publish Combat");
                True(session.SaveBlocked, "failed entry should block further actions");
                bool lampBefore = state.lampOn;
                session.SetLamp(!lampBefore);
                Equal(lampBefore, state.lampOn, "a blocked save prevents unrelated state mutations");

                File.Delete(blocker);
                True(session.RetryBlockedSave(), "entry should retry the same pending write");
                Equal(RunPhase.Combat, state.phase, "successful retry publishes Combat");
                Equal(CombatMode.ReactiveTurns, store.Load().Data.combatMode, "reactive route survives reload");
                True(store.Load().Data.battleCheckpoint != null, "initial stable checkpoint is durable");
            });
        }

        private static void ReactiveCheckpointCannotBelongToAnotherRun()
        {
            WithDirectory(delegate(string directory)
            {
                SaveData state = PortalState();
                ContractDefinition contract = new ContractDefinition();
                state.phase = RunPhase.Combat;
                state.combatMode = CombatMode.ReactiveTurns;
                state.battleCheckpoint = BattleCheckpoint.CreateInitial(
                    ReactiveDuelDefinitions.Create(contract, state), "other-contract:1", 1, 1);
                SaveStore store = new SaveStore(directory, new JsonCodec());

                Equal(SaveWriteStatus.InvalidData, store.Save(state).Status,
                    "a battle from another economic run must not be persisted as this contract");
            });
        }

        private static void ReactiveEntryPublishesAnAlreadyStartedSession()
        {
            WithDirectory(delegate(string directory)
            {
                SaveStore store = new SaveStore(directory, new JsonCodec());
                SaveData state = PortalState();
                True(store.Save(state).Succeeded, "Portal fixture should persist");
                GameSession session = new GameSession(state, new ContractDefinition(), store);
                bool observedEntry = false;
                session.Changed += delegate
                {
                    if (session.State.phase != RunPhase.Combat) return;
                    observedEntry = true;
                    True(session.ReactiveCombat != null, "published combat must already have its session");
                    Equal(ReactivePhase.PlayerCommand, session.ReactiveCombat.Phase,
                        "published combat must already be at the first decision");
                };

                True(session.EnterReactiveTestEncounter(), "entry should succeed");
                True(observedEntry, "entry should notify after session startup");
            });
        }

        private static void FailedPaymentWriteCanRetryWithoutDuplicatingReward()
        {
            WithDirectory(delegate(string directory)
            {
                string blocker = Path.Combine(directory, "blocked");
                File.WriteAllText(blocker, "not a directory");
                SaveStore store = new SaveStore(blocker, new JsonCodec());
                SaveData state = PaymentState();
                GameSession session = new GameSession(state, new ContractDefinition(), store);

                Equal(false, session.ClaimPayment(), "payment cannot show success before disk commit");
                Equal(600, state.yen, "failed commit cannot publish currency");
                Equal(RunPhase.Payment, state.phase, "failed commit remains claimable");
                True(session.SaveBlocked, "failed commit must block the next claim");
                Equal(false, session.ClaimPayment(), "blocked claim cannot create a second delta");

                File.Delete(blocker);
                True(session.RetryBlockedSave(), "same payment delta should retry");
                Equal(2400, state.yen, "one reward is published after persistence");
                Equal(2400, store.Load().Data.yen, "one reward is durable");
                Equal(1, store.Load().Data.claimedEconomicRunIds.Count, "one entitlement is recorded");
                True(new MessageService(store.Load().Data).HasDeliveredEvent("guild-contract-completed:contract_subway_001:1"),
                    "payment message is in the same durable candidate");
                Equal(false, session.ClaimPayment(), "double click does not pay twice");
                Equal(2400, new GameSession(store.Load().Data, new ContractDefinition(), store).State.yen,
                    "reload does not pay again");
            });
        }

        private static void MidActionSaveReplaysTheStablePreActionDecision()
        {
            WithDirectory(delegate(string directory)
            {
                SaveStore store = new SaveStore(directory, new JsonCodec());
                SaveData state = PortalState();
                True(store.Save(state).Succeeded, "Portal fixture should persist");
                GameSession session = new GameSession(state, new ContractDefinition(), store);
                True(session.EnterReactiveTestEncounter(), "test duel should enter");
                Equal(ReactivePhase.PlayerCommand, session.ReactiveCombat.Phase, "entry starts the first decision");
                long stableRevision = session.ReactiveCombat.Revision;
                CommandResult original = session.ReactiveCombat.SubmitCommand(new CommandIntent(
                    "preaction-basic", stableRevision, CommandKind.Basic, null,
                    new[] { session.ReactiveCombat.ActiveEnemyIds[0] }));
                True(original.Accepted, "command should begin an unresolved action");

                True(session.SaveReactiveCheckpoint(), "mid-action autosave should store the last stable snapshot");
                SaveData persisted = store.Load().Data;
                Equal(stableRevision, persisted.battleCheckpoint.revision, "transient command revision is not saved");
                Equal(ReactivePhase.PlayerCommand.ToString(), persisted.battleCheckpoint.phase,
                    "Continue reopens the pre-action decision");
                Equal(4, persisted.battleCheckpoint.hunterAp, "uncommitted AP cost is not saved");
                GameSession reopened = new GameSession(persisted, new ContractDefinition(), store);
                Equal(ReactivePhase.PlayerCommand, reopened.ReactiveCombat.Phase, "reload does not dispatch an extra turn");
                Equal(4, reopened.ReactiveCombat.HunterAp, "reload retains pre-action AP");
                CommandResult replay = reopened.ReactiveCombat.SubmitCommand(new CommandIntent(
                    "preaction-basic", reopened.ReactiveCombat.Revision, CommandKind.Basic, null,
                    new[] { reopened.ReactiveCombat.ActiveEnemyIds[0] }));
                True(replay.Accepted, "the interrupted action can be replayed");
                Equal(original.ActionId, replay.ActionId, "same run, attempt and seed replay the action identity");
            });
        }

        private static void FailedTargetChangeSaveRetryRequiresTheNewTarget()
        {
            WithDirectory(delegate(string directory)
            {
                SaveStore seedStore = new SaveStore(directory, new JsonCodec());
                SaveData state = PortalState();
                True(seedStore.Save(state).Succeeded, "Portal fixture should persist");

                GameSession seeded = new GameSession(seedStore.Load().Data, new ContractDefinition(), seedStore);
                True(seeded.EnterReactiveTestEncounter(), "reactive encounter should enter");
                True(seeded.ReactiveCombat.SelectTarget("E1"), "target A should be selected explicitly");
                True(seeded.SaveReactiveCheckpoint(), "target A should be durably persisted before the failed B write");
                SaveData durableA = seedStore.Load().Data;
                string targetA = durableA.battleCheckpoint.selectedTargetId;
                long revision = durableA.battleCheckpoint.revision;
                Equal("E1", targetA, "fixture starts with target A");

                bool failTargetWriteOnce = true;
                SaveStore faultedStore = new SaveStore(directory, new JsonCodec(), "save.json",
                    delegate(SaveCommitStage stage)
                    {
                        if (stage == SaveCommitStage.BeforeTemporaryWrite && failTargetWriteOnce)
                        {
                            failTargetWriteOnce = false;
                            throw new IOException("simulated target-change save failure");
                        }
                    });

                GameSession session = new GameSession(faultedStore.Load().Data, new ContractDefinition(), faultedStore);
                True(session.ReactiveCombat.SelectTarget("E2"), "target B should be selected in live combat");
                Equal(revision, session.ReactiveCombat.Revision,
                    "target selection intentionally keeps the battle revision unchanged");
                Equal("E2", session.ReactiveCombat.GetStableCheckpoint().selectedTargetId,
                    "stable checkpoint contains target B before persistence");

                Equal(false, session.SaveReactiveCheckpoint(), "target B save should fail once");
                True(session.SaveBlocked, "failed target persistence must block the session");
                Equal(targetA, seedStore.Load().Data.battleCheckpoint.selectedTargetId,
                    "old durable checkpoint still contains target A");
                Equal(revision, seedStore.Load().Data.battleCheckpoint.revision,
                    "old durable checkpoint has the same revision as pending target B");

                True(session.RetryBlockedSave(),
                    "retry must write pending target B instead of accepting stale target A by revision alone");
                Equal(false, session.SaveBlocked, "successful retry clears the save block");

                SaveData durableB = seedStore.Load().Data;
                Equal("E2", durableB.battleCheckpoint.selectedTargetId,
                    "retry durably persists target B");
                Equal(revision, durableB.battleCheckpoint.revision,
                    "target-only persistence does not invent a new battle revision");
                Equal("E2", session.State.battleCheckpoint.selectedTargetId,
                    "published session state matches the durable target B checkpoint");
                Equal("E2", session.ReactiveCombat.SelectedTargetId,
                    "live combat target remains aligned with the durable checkpoint");
            });
        }

        private static void PostReplaceTargetChangeRetryReconcilesTheNewTarget()
        {
            WithDirectory(delegate(string directory)
            {
                SaveStore seedStore = new SaveStore(directory, new JsonCodec());
                SaveData state = PortalState();
                True(seedStore.Save(state).Succeeded, "Portal fixture should persist");

                GameSession seeded = new GameSession(seedStore.Load().Data, new ContractDefinition(), seedStore);
                True(seeded.EnterReactiveTestEncounter(), "reactive encounter should enter");
                True(seeded.ReactiveCombat.SelectTarget("E1"), "target A should be selected explicitly");
                True(seeded.SaveReactiveCheckpoint(), "target A should be durably persisted before the lost acknowledgement");
                long revision = seedStore.Load().Data.battleCheckpoint.revision;

                int writeAttempts = 0;
                bool failAfterReplaceOnce = true;
                SaveStore faultedStore = new SaveStore(directory, new JsonCodec(), "save.json",
                    delegate(SaveCommitStage stage)
                    {
                        if (stage == SaveCommitStage.BeforeTemporaryWrite) writeAttempts++;
                        if (stage == SaveCommitStage.AfterReplace && failAfterReplaceOnce)
                        {
                            failAfterReplaceOnce = false;
                            throw new IOException("simulated target-change acknowledgement loss after replace");
                        }
                    });

                GameSession session = new GameSession(faultedStore.Load().Data, new ContractDefinition(), faultedStore);
                True(session.ReactiveCombat.SelectTarget("E2"), "target B should be selected in live combat");

                Equal(false, session.SaveReactiveCheckpoint(),
                    "UI success waits for acknowledgement even though target B reached durable storage");
                True(session.SaveBlocked, "unacknowledged target persistence must block the session");
                Equal("E1", session.State.battleCheckpoint.selectedTargetId,
                    "unacknowledged target B write must not publish into the live profile");
                Equal("E2", seedStore.Load().Data.battleCheckpoint.selectedTargetId,
                    "target B is already durable after the replace");
                Equal(revision, seedStore.Load().Data.battleCheckpoint.revision,
                    "target B durable checkpoint keeps the same battle revision");
                Equal(1, writeAttempts, "the original target B persistence used one write attempt");

                True(session.RetryBlockedSave(),
                    "retry should reconcile the already durable target B checkpoint");
                Equal(false, session.SaveBlocked, "reconciliation clears the save block");
                Equal("E2", session.State.battleCheckpoint.selectedTargetId,
                    "reconciliation publishes the durable target B checkpoint");
                Equal("E2", session.ReactiveCombat.SelectedTargetId,
                    "live combat remains aligned with the reconciled target B checkpoint");
                Equal(1, writeAttempts,
                    "reconciliation must not rewrite an already durable target B checkpoint");
            });
        }

        private static void RetreatRetryKeepsEconomicRunAndSeed()
        {
            WithDirectory(delegate(string directory)
            {
                SaveStore store = new SaveStore(directory, new JsonCodec());
                SaveData state = PortalState();
                state.preparedFoodId = FoodService.GreenTeaId;
                state.storedFoodId = FoodService.OnigiriId;
                state.storedFoodCount = 2;
                True(store.Save(state).Succeeded, "Portal fixture should persist");
                GameSession session = new GameSession(state, new ContractDefinition(), store);
                True(session.EnterReactiveTestEncounter(), "first attempt should enter");
                string runId = session.State.battleCheckpoint.economicRunId;
                long seed = session.State.battleCheckpoint.seed;
                True(session.RetreatReactiveEncounter(), "retreat should durably fail the attempt");
                Equal(RunPhase.Failed, store.Load().Data.phase, "retreat gives no reward");
                Equal(4, store.Load().Data.battleCheckpoint.hunterAp,
                    "retreat settles the latest stable turn rather than an older entry snapshot");
                True(session.RetryEncounter(), "retry should start a new attempt in the same run");
                SaveData retried = store.Load().Data;
                Equal(runId, retried.battleCheckpoint.economicRunId, "retry keeps the economic run");
                Equal(seed, retried.battleCheckpoint.seed, "retry keeps the content seed");
                Equal(2L, retried.battleCheckpoint.attemptId, "retry increments attempt identity");
                Equal(100, retried.battleCheckpoint.hunterHp, "retry starts at full encounter HP");
                Equal(3, retried.battleCheckpoint.hunterAp, "initial AP is not accumulated across attempts");
                Equal(0, retried.claimedEconomicRunIds.Count, "retry cannot pay the contract");
                Equal(FoodService.GreenTeaId, retried.preparedFoodId, "food effect remains without a second spend");
                Equal(2, retried.storedFoodCount, "retry does not roll back inventory");
            });
        }

        private static void PaymentUsesLatestPersistedMessengerState()
        {
            WithDirectory(delegate(string directory)
            {
                SaveStore store = new SaveStore(directory, new JsonCodec());
                SaveData original = PaymentState();
                True(store.Save(original).Succeeded, "payment fixture should save");
                GameSession staleSession = new GameSession(store.Load().Data, new ContractDefinition(), store);
                SaveData newer = store.Load().Data;
                new MessageService(newer).DeliverIncoming("later:message", "kaito", "Newer message.");
                True(store.Save(newer).Succeeded, "newer Messenger state should save");

                True(staleSession.ClaimPayment(), "stale session can apply payment to latest profile");
                SaveData persisted = store.Load().Data;
                True(new MessageService(persisted).HasDeliveredEvent("later:message"),
                    "claim must not overwrite Messenger updates from a stale clone");
                True(new MessageService(persisted).HasDeliveredEvent("guild-contract-completed:contract_subway_001:1"),
                    "guild message is persisted with the reward");
            });
        }

        private static void PostReplaceFailureReconcilesTheDurablePayment()
        {
            WithDirectory(delegate(string directory)
            {
                SaveStore seedStore = new SaveStore(directory, new JsonCodec());
                True(seedStore.Save(PaymentState()).Succeeded, "payment fixture should save");
                bool failOnce = true;
                SaveStore faultedStore = new SaveStore(directory, new JsonCodec(), "save.json",
                    delegate(SaveCommitStage stage)
                    {
                        if (stage == SaveCommitStage.AfterReplace && failOnce)
                        {
                            failOnce = false;
                            throw new IOException("simulated acknowledgement loss after replace");
                        }
                    });
                SaveData loaded = faultedStore.Load().Data;
                GameSession session = new GameSession(loaded, new ContractDefinition(), faultedStore);

                Equal(false, session.ClaimPayment(), "UI success waits for an acknowledged save");
                Equal(RunPhase.Payment, session.State.phase, "unacknowledged write cannot publish Home");
                Equal(600, session.State.yen, "unacknowledged write cannot publish reward");
                Equal(2400, seedStore.Load().Data.yen, "primary is already durably updated");
                True(session.RetryBlockedSave(), "retry should reconcile the durable ledger");
                Equal(2400, session.State.yen, "reconciliation publishes exactly one reward");
                Equal(1, seedStore.Load().Data.claimedEconomicRunIds.Count, "ledger remains singular");
                Equal(false, session.ClaimPayment(), "a further click cannot pay again");
            });
        }

        private static void FailedTemporaryWriteNeverPublishesHalfAPayment()
        {
            foreach (SaveCommitStage failingStage in new[] {
                SaveCommitStage.BeforeTemporaryWrite, SaveCommitStage.AfterTemporaryFlush })
            {
                WithDirectory(delegate(string directory)
                {
                    SaveStore seedStore = new SaveStore(directory, new JsonCodec());
                    True(seedStore.Save(PaymentState()).Succeeded, "payment fixture should save");
                    string oldBytes = File.ReadAllText(seedStore.PrimaryPath);
                    SaveStore faulted = new SaveStore(directory, new JsonCodec(), "save.json",
                        delegate(SaveCommitStage stage)
                        {
                            if (stage == failingStage) throw new IOException("simulated " + stage);
                        });
                    SaveData candidate = faulted.Load().Data;
                    True(new EconomyService().ClaimPayment(candidate, new ContractDefinition()),
                        "candidate should contain one intended reward");

                    Equal(SaveWriteStatus.IoFailure, faulted.Save(candidate).Status,
                        "injected storage fault should report failure");
                    Equal(oldBytes, File.ReadAllText(seedStore.PrimaryPath),
                        "primary stays a complete old profile before replace");
                    Equal(600, seedStore.Load().Data.yen, "no partial reward is loadable");
                });
            }
        }

        private static void RepeatablePaymentKeepsFirstClearRewardIdSingular()
        {
            WithDirectory(delegate(string directory)
            {
                SaveStore store = new SaveStore(directory, new JsonCodec());
                True(store.Save(PaymentState()).Succeeded, "first payment fixture should save");
                ContractDefinition contract = new ContractDefinition { enemyHealth = 5f };
                GameSession session = new GameSession(store.Load().Data, contract, store);
                True(session.ClaimPayment(), "first payment should be durable");
                Equal(1, store.Load().Data.firstClearRewardIds.Count, "first clear marker is recorded");

                True(session.AcceptContract(), "a completed contract can be accepted again");
                True(session.LeaveHome(), "second run reaches Portal");
                True(session.EnterPortal(), "normal second run uses Legacy until rollout");
                True(session.ClickAttack(false), "second run can seal the enemy");
                True(session.ReturnHome(), "second run reaches Payment");
                True(store.Save(session.State).Succeeded, "second run's pending payment should save");
                True(session.ClaimPayment(), "ordinary reward is repeatable for a new economic run");
                SaveData paid = store.Load().Data;
                Equal(4200, paid.yen, "two valid runs receive two ordinary payments");
                Equal(2, paid.claimedEconomicRunIds.Count, "two economic runs have distinct claims");
                Equal(1, paid.firstClearRewardIds.Count, "first clear reward ID remains singular");
            });
        }

        private static SaveData PortalState()
        {
            return new SaveData { phase = RunPhase.Portal, activeContractId = "contract_subway_001", contractRunSequence = 1 };
        }

        private static SaveData PaymentState()
        {
            return new SaveData { phase = RunPhase.Payment, activeContractId = "contract_subway_001", contractRunSequence = 1 };
        }

        private static string LegacyJson(SaveData data)
        {
            JsonObject root = JsonNode.Parse(new JsonCodec().Serialize(data)).AsObject();
            root["version"] = 1;
            root.Remove("combatMode");
            root.Remove("battleCheckpoint");
            root.Remove("claimedEconomicRunIds");
            root.Remove("firstClearRewardIds");
            return root.ToJsonString();
        }

        private static void WithDirectory(Action<string> run)
        {
            string directory = Path.Combine(Path.GetTempPath(), "rokas-reactive-save-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try { run(directory); }
            finally { Directory.Delete(directory, true); }
        }

        private static void True(bool value, string message) { Equal(true, value, message); }

        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
                throw new InvalidOperationException(message + ". Expected " + expected + ", got " + actual + ".");
        }

        private sealed class JsonCodec : ISaveCodec
        {
            private readonly JsonSerializerOptions options = new JsonSerializerOptions { IncludeFields = true };
            public string Serialize(SaveData data) { return JsonSerializer.Serialize(data, options); }
            public bool TryDeserialize(string text, out SaveData data, out string error)
            {
                try
                {
                    data = JsonSerializer.Deserialize<SaveData>(text, options);
                    error = data == null ? "Save contained null." : string.Empty;
                    return data != null;
                }
                catch (Exception exception)
                {
                    data = null;
                    error = exception.Message;
                    return false;
                }
            }
        }
    }
}
