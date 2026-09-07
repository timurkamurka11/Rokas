using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
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
            FoodScreenConsumeActionUsesPreparedSlot();
            CorruptPrimaryRecoversBackup();
            PartialPrimaryRecoversBackupWithoutDiscardingDamage();
            MissingOrNullSettingsRecoverBackup();
            JsonShapeUsesObjectKeysRatherThanTextMatches();
            FutureVersionIsPreserved();
            LossySerializationIsRejectedBeforeRotation();
            ValidSaveRoundTripsAllState();
            HostileNumericValuesCannotOverflowEconomy();
            DirectServicesRejectNullState();
        }

        private static void InvalidTransitionsAreRejected()
        {
            GameSession session = NewSession();
            session.State.activeContractId = session.Contract.id;
            float enemyHp = session.State.enemyHp;

            Equal(false, session.EnterPortal(), "Home must not jump directly into combat");
            Equal(false, session.ClickAttack(false), "Home must reject combat input");
            Equal(false, session.ClaimPayment(), "Home must reject payment claims");
            Equal(RunPhase.Home, session.State.phase, "invalid actions must not mutate the phase");
            Equal(enemyHp, session.State.enemyHp, "invalid combat input must not damage an enemy");
            Equal(100f, session.State.playerHp, "removing only the phase guard must make this test fail");
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

        private static void FoodScreenConsumeActionUsesPreparedSlot()
        {
            GameSession session = NewSession();

            True(session.ConsumeFood(FoodService.RamenId), "ramen should be consumable from the food screen");
            Equal(FoodService.RamenId, session.State.preparedFoodId, "food screen consume should use the existing prepared-food slot");
            Equal(false, session.ConsumeFood(FoodService.OnigiriId), "a second food effect must not stack over the active one");
            Equal(FoodService.RamenId, session.State.preparedFoodId, "rejected food must preserve the active effect");
            True(session.AcceptContract(), "prepared food should coexist with accepting a contract");
            True(session.ReturnHome(), "accepted contract should still be cancellable");
            Equal(string.Empty, session.State.preparedFoodId, "food screen effect should expire through the existing return-home cleanup");
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

        private static void PartialPrimaryRecoversBackupWithoutDiscardingDamage()
        {
            WithTempDirectory(delegate(string directory)
            {
                SaveStore store = SeedPrimaryAndBackup(directory, 710, 820);
                string partial = "{\"version\":1}";
                File.WriteAllText(store.PrimaryPath, partial, System.Text.Encoding.UTF8);

                SaveLoadResult result = store.Load();

                Equal(SaveLoadStatus.RecoveredBackup, result.Status, "partial primary must not initialize missing fields to defaults");
                Equal(710, result.Data.yen, "partial-primary recovery should return the last backup");
                Equal(partial, File.ReadAllText(store.PrimaryPath), "load must preserve partial input for diagnosis");
            });
        }

        private static void MissingOrNullSettingsRecoverBackup()
        {
            WithTempDirectory(delegate(string directory)
            {
                SaveStore store = SeedPrimaryAndBackup(directory, 710, 820);
                JsonNode root = JsonNode.Parse(File.ReadAllText(store.PrimaryPath));
                root.AsObject().Remove("settings");
                File.WriteAllText(store.PrimaryPath, root.ToJsonString(), System.Text.Encoding.UTF8);
                Equal(SaveLoadStatus.RecoveredBackup, store.Load().Status, "missing settings must reject the primary");
            });

            WithTempDirectory(delegate(string directory)
            {
                SaveStore store = SeedPrimaryAndBackup(directory, 710, 820);
                JsonNode root = JsonNode.Parse(File.ReadAllText(store.PrimaryPath));
                root["settings"] = null;
                File.WriteAllText(store.PrimaryPath, root.ToJsonString(), System.Text.Encoding.UTF8);
                Equal(SaveLoadStatus.RecoveredBackup, store.Load().Status, "null settings must reject the primary");
            });
        }

        private static void JsonShapeUsesObjectKeysRatherThanTextMatches()
        {
            string namesHiddenInText = "{\"version\":1,\"note\":\"yen reputation spiritAsh weaponLevel completedRuns phase activeContractId preparedFoodId enemyHp playerHp enemyTimer autoTimer clickTimer combatTime weakPointClaimed lampOn mameInteractions settings masterVolume musicVolume sfxVolume screenShake glitchIntensity damageNumbers fullscreen\"}";
            string error;
            Equal(false, SaveJsonShape.HasRequiredShape(namesHiddenInText, out error), "required names inside a string must not satisfy structural fields");

            int version;
            Equal(false, SaveJsonShape.TryReadVersion("{\"version\":1,\"version\":2}", out version, out error), "duplicate top-level version must be rejected");

            JsonCodec codec = new JsonCodec();
            string escapedRequiredKey = codec.Serialize(new SaveData()).Replace("\"yen\"", "\"\\u0079en\"");
            Equal(true, SaveJsonShape.HasRequiredShape(escapedRequiredKey, out error), "escaped JSON property keys should decode structurally");
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

            WithTempDirectory(delegate(string directory)
            {
                SaveStore store = new SaveStore(directory, new JsonCodec());
                Directory.CreateDirectory(directory);
                string changedFutureShape = "{\"version\":2,\"futureOnly\":true}";
                File.WriteAllText(store.PrimaryPath, changedFutureShape, System.Text.Encoding.UTF8);

                Equal(SaveLoadStatus.FutureVersion, store.Load().Status, "future schema must be protected before current required-field checks");
                Equal(SaveWriteStatus.FutureVersionPreserved, store.Save(new SaveData()).Status, "changed future schema must block overwrite");
                Equal(changedFutureShape, File.ReadAllText(store.PrimaryPath), "changed future schema bytes must remain untouched");
            });
        }

        private static void LossySerializationIsRejectedBeforeRotation()
        {
            WithTempDirectory(delegate(string directory)
            {
                SaveStore seeded = SeedPrimaryAndBackup(directory, 710, 820);
                string primaryBefore = File.ReadAllText(seeded.PrimaryPath);
                string backupBefore = File.ReadAllText(seeded.BackupPath);
                SaveStore lossy = new SaveStore(directory, new LossyJsonCodec());
                SaveData third = new SaveData();
                third.yen = 930;

                SaveWriteResult result = lossy.Save(third);

                Equal(SaveWriteStatus.SerializationFailed, result.Status, "serializer field loss must reject the write");
                Equal(primaryBefore, File.ReadAllText(seeded.PrimaryPath), "rejected serialization must preserve primary bytes");
                Equal(backupBefore, File.ReadAllText(seeded.BackupPath), "rejected serialization must preserve backup bytes");
                Equal(820, seeded.Load().Data.yen, "last valid primary must remain readable");
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
                state.settings.musicVolume = .31f;
                state.settings.sfxVolume = .41f;
                state.settings.screenShake = false;
                state.settings.glitchIntensity = .52f;
                state.settings.damageNumbers = false;
                state.settings.fullscreen = false;

                Equal(SaveWriteStatus.Saved, store.Save(state).Status, "valid state should save");
                SaveLoadResult result = store.Load();

                Equal(SaveLoadStatus.LoadedPrimary, result.Status, "valid primary should load directly");
                Equal(SaveData.CurrentVersion, result.Data.version, "version should round-trip");
                Equal(1234, result.Data.yen, "yen should round-trip");
                Equal(8, result.Data.reputation, "reputation should round-trip");
                Equal(4, result.Data.spiritAsh, "spirit ash should round-trip");
                Equal(3, result.Data.weaponLevel, "weapon level should round-trip");
                Equal(2, result.Data.completedRuns, "completed runs should round-trip");
                Equal(RunPhase.Combat, result.Data.phase, "phase should round-trip");
                Equal("contract_subway_001", result.Data.activeContractId, "active contract should round-trip");
                Equal("food_green_tea", result.Data.preparedFoodId, "prepared food should round-trip");
                Equal(77.5f, result.Data.enemyHp, "combat state should round-trip");
                Equal(65f, result.Data.playerHp, "player health should round-trip");
                Equal(.6f, result.Data.enemyTimer, "enemy timer should round-trip");
                Equal(.7f, result.Data.autoTimer, "auto timer should round-trip");
                Equal(.1f, result.Data.clickTimer, "click timer should round-trip");
                Equal(2.4f, result.Data.combatTime, "combat time should round-trip");
                Equal(true, result.Data.weakPointClaimed, "weak-point state should round-trip");
                Equal(false, result.Data.lampOn, "home state should round-trip");
                Equal(9, result.Data.mameInteractions, "Mame interactions should round-trip");
                Equal(.2f, result.Data.settings.masterVolume, "settings should round-trip");
                Equal(.31f, result.Data.settings.musicVolume, "music volume should round-trip");
                Equal(.41f, result.Data.settings.sfxVolume, "SFX volume should round-trip");
                Equal(false, result.Data.settings.screenShake, "screen shake should round-trip");
                Equal(.52f, result.Data.settings.glitchIntensity, "glitch intensity should round-trip");
                Equal(false, result.Data.settings.damageNumbers, "damage numbers should round-trip");
                Equal(false, result.Data.settings.fullscreen, "boolean settings should round-trip");
            });
        }

        private static void HostileNumericValuesCannotOverflowEconomy()
        {
            SaveData upgradeState = new SaveData();
            upgradeState.weaponLevel = int.MaxValue;
            upgradeState.yen = int.MaxValue;
            GameSession upgrade = new GameSession(upgradeState, NewContract());
            Equal(false, upgrade.UpgradeWeapon(), "unrepresentable upgrade cost must be rejected");
            Equal(int.MaxValue, upgrade.State.weaponLevel, "rejected high-level upgrade must preserve level");
            Equal(int.MaxValue, upgrade.State.yen, "rejected high-level upgrade must preserve yen");

            ContractDefinition hugeReward = NewContract();
            hugeReward.reward = int.MaxValue;
            SaveData paymentState = new SaveData();
            paymentState.phase = RunPhase.Payment;
            paymentState.activeContractId = hugeReward.id;
            paymentState.yen = 1;
            paymentState.reputation = 5;
            paymentState.spiritAsh = 6;
            GameSession payment = new GameSession(paymentState, hugeReward);
            Equal(false, payment.ClaimPayment(), "overflowing yen reward must reject the whole payment");
            Equal(RunPhase.Payment, payment.State.phase, "rejected reward must stay claimable after data repair");
            Equal(1, payment.State.yen, "rejected reward must preserve yen");
            Equal(5, payment.State.reputation, "rejected reward must preserve reputation");
            Equal(6, payment.State.spiritAsh, "rejected reward must preserve ash");
            Equal(0, payment.State.completedRuns, "rejected reward must preserve completed count");

            ContractDefinition zeroReward = NewContract();
            zeroReward.reward = 0;
            zeroReward.reputationReward = 0;
            zeroReward.ashReward = 0;
            SaveData completedState = new SaveData();
            completedState.phase = RunPhase.Payment;
            completedState.activeContractId = zeroReward.id;
            completedState.completedRuns = int.MaxValue;
            GameSession completed = new GameSession(completedState, zeroReward);
            Equal(false, completed.ClaimPayment(), "completed-run overflow must reject payment before mutation");
            Equal(int.MaxValue, completed.State.completedRuns, "rejected completion must not wrap the counter");

            ContractDefinition reputationReward = NewContract();
            reputationReward.reward = 0;
            reputationReward.reputationReward = 10;
            reputationReward.ashReward = 0;
            SaveData reputationState = new SaveData();
            reputationState.phase = RunPhase.Payment;
            reputationState.activeContractId = reputationReward.id;
            reputationState.reputation = int.MaxValue - 5;
            GameSession reputation = new GameSession(reputationState, reputationReward);
            Equal(false, reputation.ClaimPayment(), "overflowing reputation reward must reject payment before mutation");
            Equal(int.MaxValue - 5, reputation.State.reputation, "rejected reputation must not wrap");

            WithTempDirectory(delegate(string directory)
            {
                SaveData hostile = new SaveData();
                hostile.weaponLevel = int.MaxValue;
                SaveStore store = new SaveStore(directory, new JsonCodec());
                Equal(SaveWriteStatus.InvalidData, store.Save(hostile).Status, "structurally valid hostile progression must not persist");
            });
        }

        private static void DirectServicesRejectNullState()
        {
            ThrowsArgumentNull(delegate { new ContractService().LeaveHome(null); }, "contract service should reject null state clearly");
            ThrowsArgumentNull(delegate { new FoodService().Prepare(null, FoodService.GreenTeaId); }, "food service should reject null state clearly");
            ThrowsArgumentNull(delegate { new EconomyService().UpgradeWeapon(null); }, "economy service should reject null state clearly");
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

        private static SaveStore SeedPrimaryAndBackup(string directory, int backupYen, int primaryYen)
        {
            SaveStore store = new SaveStore(directory, new JsonCodec());
            SaveData first = new SaveData();
            first.yen = backupYen;
            SaveData second = new SaveData();
            second.yen = primaryYen;
            Equal(SaveWriteStatus.Saved, store.Save(first).Status, "test seed backup should save");
            Equal(SaveWriteStatus.Saved, store.Save(second).Status, "test seed primary should save");
            return store;
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

        private static void ThrowsArgumentNull(Action action, string message)
        {
            try
            {
                action();
            }
            catch (ArgumentNullException)
            {
                return;
            }
            throw new InvalidOperationException(message + ". Expected ArgumentNullException.");
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

        private sealed class LossyJsonCodec : ISaveCodec
        {
            private readonly JsonCodec decoder = new JsonCodec();

            public string Serialize(SaveData data)
            {
                return "{\"version\":1}";
            }

            public bool TryDeserialize(string text, out SaveData data, out string error)
            {
                return decoder.TryDeserialize(text, out data, out error);
            }
        }
    }
}
