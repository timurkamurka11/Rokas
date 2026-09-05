using System;
using System.IO;
using System.Text.Json;
using Rokas.Core;

namespace Rokas.Core.Tests
{
    public static class DomainBehaviorTests
    {
        public static void RunAll()
        {
            InvalidTransitionsAreRejected();
            PaymentCanOnlyBeClaimedOnce();
            SessionsRestoreAcceptedCombatAndPaymentPhases();
            FailureReturnsHomeAndAllowsRetry();
            HomeTicksDoNotAdvanceCombat();
            AutomaticCombatIsIndependentOfTickPartitioning();
            WeakPointBonusRequiresAnActiveUnclaimedWindow();
            UpgradeCannotOverspend();
            TeaCannotStackAndExpiresOnReturn();
            CorruptPrimaryRecoversBackup();
            FutureVersionIsPreserved();
            ValidSaveRoundTripsAllState();
        }

        private static void InvalidTransitionsAreRejected()
        {
            GameSession session = NewSession();
            float enemyHp = session.State.enemyHp;

            Equal(false, session.EnterPortal(), "Home must not jump directly into combat");
            Equal(false, session.ClickAttack(false), "Home must reject combat input");
            Equal(false, session.ClaimPayment(), "Home must reject payment claims");
            Equal(RunPhase.Home, session.State.phase, "invalid actions must not mutate the phase");
            Equal(enemyHp, session.State.enemyHp, "invalid combat input must not damage an enemy");
        }

        private static void PaymentCanOnlyBeClaimedOnce()
        {
            ContractDefinition contract = NewContract();
            contract.enemyHealth = 5f;
            contract.reward = 1800;
            contract.reputationReward = 10;
            contract.ashReward = 3;
            GameSession session = NewSession(contract);

            True(session.AcceptContract(), "contract should be accepted at home");
            True(session.LeaveHome(), "accepted contract should reach the portal");
            True(session.EnterPortal(), "portal should start combat");
            True(session.ClickAttack(false), "first click should seal a five-health enemy");
            Equal(RunPhase.Sealed, session.State.phase, "defeat should seal the contract");
            Equal(false, session.ClickAttack(false), "sealed combat must reject further attacks");
            True(session.ReturnHome(), "sealed run should return to payment");
            Equal(false, session.ReturnHome(), "payment state must not be completed twice");
            True(session.ClaimPayment(), "payment should be claimable once");
            Equal(false, session.ClaimPayment(), "payment must not be claimable twice");
            Equal(2400, session.State.yen, "one reward should be added to starting yen");
            Equal(10, session.State.reputation, "reputation should be awarded once");
            Equal(3, session.State.spiritAsh, "spirit ash should be awarded once");
            Equal(1, session.State.completedRuns, "one run should be counted");
        }

        private static void SessionsRestoreAcceptedCombatAndPaymentPhases()
        {
            ContractDefinition contract = NewContract();
            SaveData accepted = new SaveData();
            accepted.phase = RunPhase.Accepted;
            accepted.activeContractId = contract.id;
            GameSession acceptedSession = new GameSession(accepted, contract);
            Equal(false, acceptedSession.AcceptContract(), "restored accepted contract must not be accepted twice");
            True(acceptedSession.LeaveHome(), "restored accepted state should continue to the portal");

            SaveData combat = new SaveData();
            combat.phase = RunPhase.Combat;
            combat.activeContractId = contract.id;
            combat.enemyHp = 91f;
            combat.playerHp = 76f;
            combat.enemyTimer = 1.6f;
            combat.autoTimer = .2f;
            combat.combatTime = 1f;
            GameSession combatSession = new GameSession(combat, contract);
            combatSession.Tick(.2f);
            Equal(83f, combatSession.State.enemyHp, "restored auto timer should continue rather than restart");
            Equal(76f, combatSession.State.playerHp, "enemy timer should also be restored");

            SaveData payment = new SaveData();
            payment.phase = RunPhase.Payment;
            payment.activeContractId = contract.id;
            GameSession paymentSession = new GameSession(payment, contract);
            True(paymentSession.ClaimPayment(), "restored payment should remain claimable");
            Equal(2400, paymentSession.State.yen, "restored payment should award once");
            Equal(false, paymentSession.ClaimPayment(), "restored payment must not duplicate rewards");
        }

        private static void FailureReturnsHomeAndAllowsRetry()
        {
            ContractDefinition contract = NewContract();
            contract.enemyDamage = 200f;
            contract.enemyInterval = .1f;
            contract.autoInterval = 10f;
            GameSession session = NewSession(contract);

            True(session.AcceptContract(), "first attempt should be accepted");
            True(session.LeaveHome(), "first attempt should leave home");
            True(session.EnterPortal(), "first attempt should enter combat");
            session.Tick(.1f);
            Equal(RunPhase.Failed, session.State.phase, "lethal enemy hit should fail the run");
            True(session.ReturnHome(), "failed run should return home");
            Equal(0, session.State.completedRuns, "failure must not count as completion");
            True(session.AcceptContract(), "contract should be retryable after failure");
        }

        private static void HomeTicksDoNotAdvanceCombat()
        {
            GameSession session = NewSession();
            session.State.enemyHp = 33f;
            session.State.playerHp = 44f;
            session.State.enemyTimer = .25f;
            session.State.autoTimer = .5f;
            session.State.combatTime = 7f;

            session.Tick(100f);

            Equal(33f, session.State.enemyHp, "home tick must not damage an enemy");
            Equal(44f, session.State.playerHp, "home tick must not damage the player");
            Equal(.25f, session.State.enemyTimer, "home tick must not consume enemy timer");
            Equal(.5f, session.State.autoTimer, "home tick must not consume auto timer");
            Equal(7f, session.State.combatTime, "home tick must not advance combat time");
        }

        private static void AutomaticCombatIsIndependentOfTickPartitioning()
        {
            GameSession oneTick = ActiveCombat(NewContract());
            GameSession manyTicks = ActiveCombat(NewContract());

            oneTick.Tick(5f);
            for (int index = 0; index < 50; index++)
            {
                manyTicks.Tick(.1f);
            }

            Near(oneTick.State.enemyHp, manyTicks.State.enemyHp, .001f, "auto damage must be partition independent");
            Near(oneTick.State.playerHp, manyTicks.State.playerHp, .001f, "enemy damage must be partition independent");
            Near(oneTick.State.autoTimer, manyTicks.State.autoTimer, .001f, "auto timer must be partition independent");
            Near(oneTick.State.enemyTimer, manyTicks.State.enemyTimer, .001f, "enemy timer must be partition independent");
        }

        private static void WeakPointBonusRequiresAnActiveUnclaimedWindow()
        {
            GameSession session = ActiveCombat(NewContract());
            int criticalHits = 0;
            session.Combat.Hit += delegate(CombatHit hit)
            {
                if (hit.targetIsEnemy && hit.critical)
                {
                    criticalHits++;
                }
            };

            True(session.ClickAttack(true), "weak-point input outside its window should remain a regular attack");
            Equal(false, session.State.weakPointClaimed, "inactive weak-point input must not consume the bonus");
            session.Tick(2.05f);
            Equal(true, session.Combat.WeakPointActive, "weak point should open at the timed window");
            True(session.ClickAttack(true), "active weak-point input should attack");
            Equal(true, session.State.weakPointClaimed, "active weak point should be consumed");
            Equal(1, criticalHits, "exactly one critical hit should be emitted");
            session.Tick(.2f);
            True(session.ClickAttack(true), "later input should still make a regular attack");
            Equal(1, criticalHits, "weak point should grant only one bonus per run");
        }

        private static void UpgradeCannotOverspend()
        {
            SaveData state = new SaveData();
            state.yen = 299;
            GameSession session = new GameSession(state, NewContract());

            Equal(false, session.UpgradeWeapon(), "yen below the level-one price should be rejected");
            Equal(299, session.State.yen, "failed upgrade must preserve yen");
            Equal(1, session.State.weaponLevel, "failed upgrade must preserve level");
            session.State.yen = 300;
            True(session.UpgradeWeapon(), "exact upgrade price should be accepted");
            Equal(0, session.State.yen, "successful upgrade should deduct the exact price");
            Equal(2, session.State.weaponLevel, "successful upgrade should increment one level");
            Equal(false, session.UpgradeWeapon(), "a second unaffordable upgrade should fail");
            Equal(0, session.State.yen, "upgrade must never make yen negative");
        }

        private static void TeaCannotStackAndExpiresOnReturn()
        {
            GameSession session = NewSession();
            True(session.AcceptContract(), "contract should be accepted before preparation");
            True(session.PrepareFood("food_green_tea"), "green tea should be purchasable");
            Equal(520, session.State.yen, "tea should cost 80 yen");
            Equal(false, session.PrepareFood("food_green_tea"), "prepared tea must not stack");
            Equal(520, session.State.yen, "rejected tea must not charge again");
            True(session.ReturnHome(), "accepted contract should be cancellable");
            Equal(string.Empty, session.State.preparedFoodId, "tea should expire when a run returns");
        }

        private static void CorruptPrimaryRecoversBackup()
        {
            WithTempDirectory(delegate(string directory)
            {
                SaveStore store = new SaveStore(directory, new JsonCodec());
                SaveData first = new SaveData();
                first.yen = 710;
                SaveData second = new SaveData();
                second.yen = 820;
                Equal(SaveWriteStatus.Saved, store.Save(first).Status, "first save should succeed");
                Equal(SaveWriteStatus.Saved, store.Save(second).Status, "second save should rotate a valid backup");
                File.WriteAllText(store.PrimaryPath, "{ definitely corrupt", System.Text.Encoding.UTF8);

                SaveLoadResult result = store.Load();

                Equal(SaveLoadStatus.RecoveredBackup, result.Status, "corrupt primary should recover the valid backup");
                Equal(710, result.Data.yen, "recovery should return the previous valid state");
                True(result.Message.Length > 0, "recovery should report why the primary was rejected");
            });
        }

        private static void FutureVersionIsPreserved()
        {
            WithTempDirectory(delegate(string directory)
            {
                JsonCodec codec = new JsonCodec();
                SaveStore store = new SaveStore(directory, codec);
                SaveData future = new SaveData();
                future.version = SaveData.CurrentVersion + 1;
                future.yen = 9999;
                string original = codec.Serialize(future);
                Directory.CreateDirectory(directory);
                File.WriteAllText(store.PrimaryPath, original, System.Text.Encoding.UTF8);

                SaveLoadResult loaded = store.Load();
                Equal(SaveLoadStatus.FutureVersion, loaded.Status, "future saves should be identified explicitly");
                Equal(9999, loaded.Data.yen, "future data should be returned for diagnosis");
                SaveWriteResult write = store.Save(new SaveData());
                Equal(SaveWriteStatus.FutureVersionPreserved, write.Status, "future primary must block overwrite");
                Equal(original, File.ReadAllText(store.PrimaryPath), "future primary bytes must remain untouched");
            });
        }

        private static void ValidSaveRoundTripsAllState()
        {
            WithTempDirectory(delegate(string directory)
            {
                SaveStore store = new SaveStore(directory, new JsonCodec());
                SaveData state = new SaveData();
                state.yen = 1234;
                state.reputation = 8;
                state.spiritAsh = 4;
                state.weaponLevel = 3;
                state.completedRuns = 2;
                state.phase = RunPhase.Combat;
                state.activeContractId = "contract_subway_001";
                state.preparedFoodId = "food_green_tea";
                state.enemyHp = 77.5f;
                state.playerHp = 65f;
                state.enemyTimer = .6f;
                state.autoTimer = .7f;
                state.clickTimer = .1f;
                state.combatTime = 2.4f;
                state.weakPointClaimed = true;
                state.lampOn = false;
                state.mameInteractions = 9;
                state.settings.masterVolume = .2f;
                state.settings.fullscreen = false;

                Equal(SaveWriteStatus.Saved, store.Save(state).Status, "valid state should save");
                SaveLoadResult result = store.Load();

                Equal(SaveLoadStatus.LoadedPrimary, result.Status, "valid primary should load directly");
                Equal(1234, result.Data.yen, "yen should round-trip");
                Equal(4, result.Data.spiritAsh, "spirit ash should round-trip");
                Equal(RunPhase.Combat, result.Data.phase, "phase should round-trip");
                Equal(77.5f, result.Data.enemyHp, "combat state should round-trip");
                Equal(true, result.Data.weakPointClaimed, "weak-point state should round-trip");
                Equal(false, result.Data.lampOn, "home state should round-trip");
                Equal(.2f, result.Data.settings.masterVolume, "settings should round-trip");
                Equal(false, result.Data.settings.fullscreen, "boolean settings should round-trip");
            });
        }

        private static GameSession NewSession(ContractDefinition contract = null)
        {
            return new GameSession(new SaveData(), contract ?? NewContract());
        }

        private static GameSession ActiveCombat(ContractDefinition contract)
        {
            GameSession session = NewSession(contract);
            True(session.AcceptContract(), "test setup should accept contract");
            True(session.LeaveHome(), "test setup should leave home");
            True(session.EnterPortal(), "test setup should enter combat");
            return session;
        }

        private static ContractDefinition NewContract()
        {
            return new ContractDefinition();
        }

        private static void WithTempDirectory(Action<string> action)
        {
            string directory = Path.Combine(Path.GetTempPath(), "rokas-core-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                action(directory);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void True(bool actual, string message)
        {
            Equal(true, actual, message);
        }

        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(message + ". Expected " + expected + ", got " + actual + ".");
            }
        }

        private static void Near(float expected, float actual, float tolerance, string message)
        {
            if (Math.Abs(expected - actual) > tolerance)
            {
                throw new InvalidOperationException(message + ". Expected " + expected + ", got " + actual + ".");
            }
        }

        private sealed class JsonCodec : ISaveCodec
        {
            private readonly JsonSerializerOptions options = new JsonSerializerOptions { IncludeFields = true };

            public string Serialize(SaveData data)
            {
                return JsonSerializer.Serialize(data, options);
            }

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
