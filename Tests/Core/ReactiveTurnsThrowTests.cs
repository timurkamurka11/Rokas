using System;
using System.IO;
using System.Text.Json;
using Rokas.Core.ReactiveTurns;

namespace Rokas.Core.Tests
{
    public static class ReactiveTurnsThrowTests
    {
        public static void RunAll()
        {
            ThrowHasASeparateAuthoredSkillAndReadOnlyPreview();
            ThrowCommitsAndContactsExactlyOnce();
            PreviousEightCatalogHydratesByExactHash();
            PreviousCatalogTargetSaveRetriesWithoutChangingTheCatalog();
            Console.WriteLine("PASS ReactiveTurns: Throw catalog, contact, and previous-save compatibility");
        }

        private static void ThrowHasASeparateAuthoredSkillAndReadOnlyPreview()
        {
            CombatDefinitions definitions = ReactiveEightEnemyDefinitions.CreateForTests();
            SkillDefinition throwing = definitions.FindSkill(ReactiveEightEnemyDefinitions.ThrowId);
            Require(throwing != null && throwing.Id != "seal_strike" &&
                throwing.ApCost == 2 && throwing.DelayTicks == 110 &&
                throwing.DamagePower == 1 && throwing.SealDamage == 0 &&
                throwing.Targeting == TargetingMode.SingleEnemy,
                "Throw is a distinct two-AP, single-target skill");
            Require(definitions.FindSkill("seal_strike").DamagePower == 2.4,
                "adding Throw did not change Seal Strike");
            Require(definitions.ContentHash !=
                "5847af078b0d633903f41af5b90c25d2dbe30157d692506c8ee895d0962e5905",
                "new skill changes the authored content hash");
            CombatDefinitions entry = ReactiveEightEnemyDefinitions.Create(new ContractDefinition(),
                new SaveData { weaponLevel = 1 });
            Require(entry.FindSkill(ReactiveEightEnemyDefinitions.ThrowId) != null &&
                entry.ContentHash !=
                "4282fe5f76298dc35bcccc34790f672d3b50c2dbf41e35cd3af22fc80a22b675",
                "a newly entered production encounter uses the Throw catalog");

            var session = new ReactiveCombatSession(definitions);
            session.Start();
            long revision = session.Revision;
            CommandImpactPreview preview = session.PreviewCommandImpact(CommandKind.Skill,
                ReactiveEightEnemyDefinitions.ThrowId, "E1");
            Require(preview != null && preview.Damage == 20 && preview.ApCost == 2 &&
                preview.CanAfford && preview.ProjectedAp == 2,
                "Throw's read-only impact preview uses the authored power and AP cost");
            Require(session.Phase == ReactivePhase.PlayerCommand && session.Revision == revision &&
                session.HunterAp == 4 && session.GetActorState("E1").Hp == 60,
                "preview does not commit a command or damage");
        }

        private static void ThrowCommitsAndContactsExactlyOnce()
        {
            var session = new ReactiveCombatSession(ReactiveEightEnemyDefinitions.CreateForTests());
            session.Start();
            var intent = new CommandIntent("throw-once", session.Revision, CommandKind.Skill,
                ReactiveEightEnemyDefinitions.ThrowId, new[] { "E1" });
            CommandResult first = session.SubmitCommand(intent);
            Require(first.Accepted && session.HunterAp == 2 &&
                session.GetActorState("E1").Hp == 60,
                "Throw reserves two AP and waits for contact");
            CommandResult repeated = session.SubmitCommand(intent);
            Require(repeated.Accepted && repeated.ActionId == first.ActionId && session.HunterAp == 2,
                "replayed command ID cannot spend AP again");
            CombatStep contact = session.Advance(500000, 500000);
            int hitEvents = 0;
            foreach (CombatEvent combatEvent in contact.Events)
                if (combatEvent.Kind == CombatEventKind.HitResolved &&
                    combatEvent.ActorId == ReactiveEightEnemyDefinitions.HunterId) hitEvents++;
            Require(hitEvents == 1 && session.GetActorState("E1").Hp == 40 &&
                session.GetActorState("E1").Seal == 60,
                "one Throw contact deals 20 HP and no Seal damage");
            session.Advance(700000, 700000);
            Require(session.GetActorState("E1").Hp == 40 && session.HunterAp == 2,
                "repeated advance cannot apply a second contact or AP charge");
        }

        private static void PreviousEightCatalogHydratesByExactHash()
        {
            ContractDefinition contract = new ContractDefinition();
            SaveData saved = PreviousCatalogState(contract);
            CombatDefinitions previous = ReactiveEightEnemyDefinitions.CreatePreviousCatalog(contract, saved);
            Require(previous.ContentHash ==
                "4282fe5f76298dc35bcccc34790f672d3b50c2dbf41e35cd3af22fc80a22b675",
                "previous eight-enemy catalog retains its verified exact hash");
            Require(previous.FindSkill(ReactiveEightEnemyDefinitions.ThrowId) == null,
                "resumed old encounters cannot offer the new skill");
            saved.battleCheckpoint = BattleCheckpoint.CreateInitial(previous,
                ContractService.GetEconomicRunId(contract.id, saved.contractRunSequence), 1, 17);
            var reopened = new GameSession(saved, contract);
            Require(reopened.ReactiveCombat != null &&
                reopened.ReactiveCombat.Phase == ReactivePhase.PlayerCommand &&
                reopened.ReactiveCombat.GetStableCheckpoint().contentHash == previous.ContentHash,
                "GameSession picks the exact prior catalog for its checkpoint");
            Require(reopened.ReactiveCombat.PreviewCommandImpact(CommandKind.Skill,
                ReactiveEightEnemyDefinitions.ThrowId, "E1") == null,
                "old-catalog session does not preview Throw");
            CommandResult rejected = reopened.ReactiveCombat.SubmitCommand(new CommandIntent(
                "old-catalog-throw", reopened.ReactiveCombat.Revision, CommandKind.Skill,
                ReactiveEightEnemyDefinitions.ThrowId, new[] { "E1" }));
            Require(!rejected.Accepted && rejected.Reason == "UnknownSkill" &&
                reopened.ReactiveCombat.HunterAp == 4,
                "old-catalog session rejects Throw without spending AP");

            saved.battleCheckpoint.contentHash = "unrecognized-content-hash";
            var corrupted = new GameSession(saved, contract);
            Require(corrupted.ReactiveCombat.Phase == ReactivePhase.SafeError,
                "unknown content hash still fails safely");
        }

        private static void PreviousCatalogTargetSaveRetriesWithoutChangingTheCatalog()
        {
            string directory = Path.Combine(Path.GetTempPath(), "rokas-throw-save-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                ContractDefinition contract = new ContractDefinition();
                SaveData saved = PreviousCatalogState(contract);
                CombatDefinitions previous = ReactiveEightEnemyDefinitions.CreatePreviousCatalog(contract, saved);
                saved.battleCheckpoint = BattleCheckpoint.CreateInitial(previous,
                    ContractService.GetEconomicRunId(contract.id, saved.contractRunSequence), 1, 17);
                var durableStore = new SaveStore(directory, new JsonCodec());
                Require(durableStore.Save(saved).Succeeded, "previous checkpoint is durably valid");
                bool failOnce = true;
                var faultedStore = new SaveStore(directory, new JsonCodec(), "save.json",
                    delegate(SaveCommitStage stage)
                    {
                        if (stage == SaveCommitStage.BeforeTemporaryWrite && failOnce)
                        {
                            failOnce = false;
                            throw new IOException("simulated target-save fault");
                        }
                    });
                var session = new GameSession(faultedStore.Load().Data, contract, faultedStore);
                Require(session.ReactiveCombat.SelectTarget("E2"), "old catalog target B selected");
                Require(!session.SaveReactiveCheckpoint() && session.SaveBlocked,
                    "failed target save blocks further publication");
                Require(session.RetryBlockedSave() && !session.SaveBlocked,
                    "pending old-catalog target save retries once");
                SaveData persisted = durableStore.Load().Data;
                Require(persisted.battleCheckpoint.contentHash == previous.ContentHash &&
                    persisted.battleCheckpoint.selectedTargetId == "E2",
                    "retry keeps the previous catalog hash and selected target");
                var resumed = new GameSession(persisted, contract, durableStore);
                Require(resumed.ReactiveCombat.Phase == ReactivePhase.PlayerCommand &&
                    resumed.ReactiveCombat.SelectedTargetId == "E2" &&
                    resumed.ReactiveCombat.PreviewCommandImpact(CommandKind.Skill,
                        ReactiveEightEnemyDefinitions.ThrowId, "E2") == null,
                    "saved previous catalog reloads with target and without Throw");
                Require(resumed.RetreatReactiveEncounter() && resumed.RetryEncounter(),
                    "an old-catalog run can start a new attempt after retreat");
                Require(durableStore.Load().Data.battleCheckpoint.contentHash == previous.ContentHash &&
                    resumed.ReactiveCombat.PreviewCommandImpact(CommandKind.Skill,
                        ReactiveEightEnemyDefinitions.ThrowId, "E1") == null,
                    "retry within the old economic run retains its original skill catalog");
            }
            finally { Directory.Delete(directory, true); }
        }

        private static SaveData PreviousCatalogState(ContractDefinition contract)
        {
            return new SaveData {
                phase = RunPhase.Combat,
                combatMode = CombatMode.ReactiveTurns,
                activeContractId = contract.id,
                contractRunSequence = 1,
                weaponLevel = 1
            };
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class JsonCodec : ISaveCodec
        {
            private readonly JsonSerializerOptions options = new JsonSerializerOptions { IncludeFields = true };
            public string Serialize(SaveData data) { return JsonSerializer.Serialize(data, options); }
            public bool TryDeserialize(string source, out SaveData data, out string error)
            {
                try
                {
                    data = JsonSerializer.Deserialize<SaveData>(source, options);
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
