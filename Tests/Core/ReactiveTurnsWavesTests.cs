using System;
using System.Collections.Generic;
using System.Text.Json;
using Rokas.Core.ReactiveTurns;

namespace Rokas.Core.Tests
{
    public static class ReactiveTurnsWavesTests
    {
        public static void RunAll()
        {
            EightEnemyContentHasThreeBoundedWaves();
            SweepFreezesOnlyCurrentLivingWaveTargets();
            SingleTargetDeathRemovesFutureTurn();
            SelectionIsAuthoritativeAndPersists();
            FourActiveEnemiesRetainDeterministicQueueOrder();
            AnchorDelaysOneTargetAndHeavyParryHasNoReward();
            HeavyOffenseWaitsForTimingWindowAndAppliesOneDamage();
            CorruptReserveActivationFailsSafely();
            RetryStartsFirstWaveWithNewAttemptAndSameSeed();
            WaveTransitionReloadAndVictoryPreserveIdentity();
            EightEnemyEncounterCompletesWithOneHunter();
            Console.WriteLine("PASS ReactiveTurns: multi-enemy targeting, queue, AoE, waves, and eight-enemy encounter");
        }

        private static void EightEnemyContentHasThreeBoundedWaves()
        {
            CombatDefinitions defs = ReactiveEightEnemyDefinitions.CreateForTests();
            Require(defs.Validate().IsValid, "eight-enemy content validates");
            Require(defs.Actors.Count == 9 && defs.Waves.Count == 3 && defs.MaxActiveEnemies == 3,
                "one Hunter and eight enemies in three capped waves");
            Require(defs.Waves[0].EnemyActorIds.Count == 3 &&
                defs.Waves[1].EnemyActorIds.Count == 3 && defs.Waves[2].EnemyActorIds.Count == 2,
                "3/3/2 composition");
            int totalHp = 0;
            foreach (ActorDefinition actor in defs.Actors) if (!actor.IsHunter) totalHp += actor.MaxHp;
            Require(totalHp == 660, "authored 660 enemy HP");
            var session = new ReactiveCombatSession(defs);
            session.Start();
            Require(session.Phase == ReactivePhase.PlayerCommand, "first Hunter command");
            Require(Joined(session.ActiveEnemyIds) == "E1,E2,E3", "wave one only active");
            Require(!session.Queue.GetEntry("E4").Active && session.Queue.GetEntry("E4").Alive,
                "reserve enemy remains alive but cannot act");
            Require(session.GetActorMaxHp("E6") == 120 && session.GetActorSealMax("E6") == 100,
                "elite maxima provided to HUD");
            CommandResult reserve = session.SubmitCommand(new CommandIntent("reserve", session.Revision,
                CommandKind.Basic, null, new[] { "E4" }));
            Require(!reserve.Accepted && reserve.Reason == "InvalidTarget" && session.HunterAp == 4,
                "reserve target rejected without AP or recovery");
        }

        private static void SweepFreezesOnlyCurrentLivingWaveTargets()
        {
            var session = new ReactiveCombatSession(ReactiveEightEnemyDefinitions.CreateForTests());
            session.Start();
            CommandImpactPreview preview = session.PreviewCommandImpact(CommandKind.Skill, "sweep", "E2");
            Require(preview != null && preview.Damage == 24 && preview.SealDamage == 12 &&
                preview.ApCost == 3 && preview.CanAfford, "Sweep preview per target");
            long revision = session.Revision;
            CommandResult invalid = session.SubmitCommand(new CommandIntent("incomplete", revision,
                CommandKind.Skill, "sweep", new[] { "E1", "E2" }));
            Require(!invalid.Accepted && session.HunterAp == 4,
                "incomplete explicit target set rejected before AP spend");
            CommandResult accepted = session.SubmitCommand(new CommandIntent("sweep", revision,
                CommandKind.Skill, "sweep", new string[0]));
            Require(accepted.Accepted && session.HunterAp == 1, "Sweep charges three AP once");
            Require(session.SubmitCommand(new CommandIntent("sweep", session.Revision,
                CommandKind.Skill, "sweep", new string[0])).ActionId == accepted.ActionId,
                "duplicate command does not charge again");
            CombatStep step = session.Advance(500000, 500000);
            int hitEvents = 0;
            foreach (CombatEvent item in step.Events)
                if (item.Kind == CombatEventKind.HitResolved && item.ActorId == "P") hitEvents++;
            Require(hitEvents == 3, "one deterministic effect event per frozen target");
            foreach (string id in new[] { "E1", "E2", "E3" })
                Require(session.GetActorState(id).Hp == 36 && session.GetActorState(id).Seal == 48,
                    "Sweep 24 HP and 12 Seal on " + id);
            Require(session.GetActorState("E4").Hp == 60 && session.GetActorState("E4").Seal == 60,
                "future wave untouched");
        }

        private static void SingleTargetDeathRemovesFutureTurn()
        {
            CombatDefinitions defs = SmallTargetEncounter();
            var session = new ReactiveCombatSession(defs);
            session.Start();
            Require(session.SubmitCommand(new CommandIntent("kill", session.Revision,
                CommandKind.Basic, null, new[] { "E1" })).Accepted, "single-target Basic commits");
            session.Advance(500000, 500000);
            Require(session.GetActorState("E1").Hp == 0 && !session.Queue.GetEntry("E1").Alive,
                "dead enemy removed from queue");
            Require(Joined(session.ActiveEnemyIds) == "E2,E3", "selection fallback list is stable");
            Require(session.GetActorState("E2").Hp == 40 && session.GetActorState("E3").Hp == 40,
                "single-target damage did not spill");
            Require(session.ActiveActorId == "E2", "E1's due slot was skipped, E2 acts next");
        }

        private static void SelectionIsAuthoritativeAndPersists()
        {
            CombatDefinitions defs = SmallTargetEncounter();
            var session = new ReactiveCombatSession(defs);
            session.Start();
            Require(session.SelectedTargetId == "E1", "first live slot selected by Core");
            long revision = session.Revision;
            int ap = session.HunterAp;
            Require(session.SelectTarget("E3") && session.SelectedTargetId == "E3",
                "explicit active target selected");
            Require(!session.SelectTarget("missing") && session.SelectedTargetId == "E3",
                "invalid selection leaves Core target unchanged");
            Require(session.CycleTarget(1) && session.SelectedTargetId == "E1",
                "selection wraps in slot order");
            Require(session.Revision == revision && session.HunterAp == ap,
                "free target change does not consume turn resources");
            BattleCheckpoint selected = session.GetStableCheckpoint();
            Require(selected.selectedTargetId == "E1", "selection stored in stable checkpoint");
            var restored = new ReactiveCombatSession(defs, selected);
            restored.Start();
            Require(restored.SelectedTargetId == "E1", "selection restored without extra turn");
            Require(restored.SubmitCommand(new CommandIntent("kill", restored.Revision,
                CommandKind.Basic, null, new[] { "E1" })).Accepted, "selected target can be attacked");
            Require(!restored.SelectTarget("E2"), "selection is locked outside PlayerCommand");
            restored.Advance(500000, 500000);
            Require(restored.SelectedTargetId == "E2", "death falls forward to next live slot");
        }

        private static void FourActiveEnemiesRetainDeterministicQueueOrder()
        {
            CombatDefinitions defs = FourEnemyEncounter();
            var session = new ReactiveCombatSession(defs);
            session.Start();
            Require(session.ActiveEnemyIds.Count == 4, "contract permits four active enemies");
            TurnForecast preview = session.Preview(new CommandIntent("p", session.Revision,
                CommandKind.Basic, null, new[] { "E4" }), 5);
            Require(preview.Slots.Count == 5 && preview.Slots[0].ActorId == "P" &&
                preview.Slots[1].ActorId == "E1" && preview.Slots[2].ActorId == "E2" &&
                preview.Slots[3].ActorId == "P", "pressure budget forces Hunter before third enemy");
            QueueState queue = QueueState.CreateInitial(defs);
            queue.MarkDead("E1");
            var scheduler = new TurnScheduler();
            TurnDecision hunter = scheduler.SelectNext(queue, defs);
            scheduler.CommitAction(queue, defs, hunter, 100);
            Require(scheduler.SelectNext(queue, defs).ActorId == "E2", "dead E1 never dispatches");
        }

        private static void AnchorDelaysOneTargetAndHeavyParryHasNoReward()
        {
            CombatDefinitions anchorDefs = ReactiveEightEnemyDefinitions.CreateForTests();
            var anchor = new ReactiveCombatSession(anchorDefs);
            anchor.Start();
            Require(anchor.SubmitCommand(new CommandIntent("anchor", anchor.Revision,
                CommandKind.Skill, "anchor", new[] { "E1" })).Accepted, "Anchor commits");
            Require(anchor.HunterAp == 2, "Anchor costs two AP");
            anchor.Advance(500000, 500000);
            Require(anchor.GetActorState("E1").Hp == 44 && anchor.GetActorState("E1").Seal == 45,
                "Anchor deals 16 HP and 15 Seal");
            Require(anchor.Queue.GetEntry("E1").NextTick == 95 &&
                anchor.Queue.GetEntry("E1").AnchorDelayed,
                "target due moved once by 35 ticks");
            Require(anchor.ActiveActorId == "E2", "untouched E2 dispatches before delayed E1");

            CombatDefinitions heavyDefs = HeavyGrammarEncounter();
            var heavy = new ReactiveCombatSession(heavyDefs);
            heavy.Start();
            Require(heavy.SubmitCommand(new CommandIntent("basic", heavy.Revision,
                CommandKind.Basic, null, new[] { "E" })).Accepted, "Basic commits");
            heavy.Advance(500000, 500000);
            Require(heavy.GetActorState("E").Hp == 100 && heavy.GetActorState("E").Seal == 48,
                "Basic deals 20 HP, 12 Seal");
            Require(heavy.HunterAp == 3 && heavy.CurrentAttack.Hits[0].IsHeavy,
                "enemy Heavy starts after Basic");
            long impact = heavy.CurrentActionStartUs + heavy.CurrentAttack.Hits[0].ImpactUs;
            DefenseAttempt ordinary = heavy.SubmitDefense(new DefenseIntent("heavy-parry",
                heavy.InputEpoch, DefenseKind.Parry, impact - 80000));
            Require(ordinary.Outcome == DefenseOutcome.Parry, "ordinary Heavy Parry timing");
            heavy.ReleaseDefense(DefenseKind.Parry, heavy.InputEpoch);
            heavy.Advance(impact + 100000, impact + 100000);
            Require(heavy.HunterHp == 94 && heavy.HunterAp == 3 &&
                heavy.GetActorState("E").Seal == 48,
                "Heavy ordinary Parry halves HP damage without AP or Seal reward");
        }

        private static void HeavyOffenseWaitsForTimingWindowAndAppliesOneDamage()
        {
            CombatDefinitions defs = TimedHeavyEncounter();
            var timed = new ReactiveCombatSession(defs);
            timed.Start();
            Require(timed.SubmitCommand(new CommandIntent("heavy", timed.Revision,
                CommandKind.Skill, "heavy", new[] { "E" })).Accepted,
                "Heavy command commits");
            Require(timed.HunterAp == 1, "Heavy charges five AP once");
            Require(timed.SubmitOffenseTiming("timing", timed.InputEpoch, 240000),
                "late-window Heavy timing accepted");
            Require(!timed.SubmitOffenseTiming("timing", timed.InputEpoch, 240000),
                "same input ID cannot apply twice");
            CombatStep presentationStep = timed.Advance(240000, 240000);
            bool heavyEvent = false;
            foreach (CombatEvent combatEvent in presentationStep.Events)
                if (combatEvent.Kind == CombatEventKind.CommandCommitted && combatEvent.Detail == "heavy")
                    heavyEvent = true;
            Require(heavyEvent, "committed Heavy identifies its clip to presentation");
            Require(timed.GetActorState("E").Hp == 200,
                "damage held until timing window and delivery watermark close");
            timed.Advance(300000, 300000);
            Require(timed.GetActorState("E").Hp == 122 && timed.GetActorState("E").Seal == 50,
                "timed Heavy deals 78 HP once and 50 Seal");
            timed.Advance(500000, 500000);
            Require(timed.GetActorState("E").Hp == 122, "no delayed second impact");

            var untimed = new ReactiveCombatSession(defs);
            untimed.Start();
            Require(untimed.SubmitCommand(new CommandIntent("heavy-base", untimed.Revision,
                CommandKind.Skill, "heavy", new[] { "E" })).Accepted, "base Heavy commits");
            Require(!untimed.SubmitOffenseTiming("too-late", untimed.InputEpoch, 250001),
                "timing outside inclusive endpoint is rejected");
            untimed.Advance(300000, 300000);
            Require(untimed.GetActorState("E").Hp == 132,
                "untimed Heavy deals authored base 68 HP exactly once");
        }

        private static void CorruptReserveActivationFailsSafely()
        {
            CombatDefinitions defs = ReactiveEightEnemyDefinitions.CreateForTests();
            BattleCheckpoint checkpoint = BattleCheckpoint.CreateInitial(defs, "run", 1, 17);
            checkpoint.queue[4].active = true;
            Require(new ReactiveCombatSession(defs, checkpoint).Start().Phase == ReactivePhase.SafeError,
                "reserve actor cannot be smuggled into active queue by a corrupt save");
        }

        private static void RetryStartsFirstWaveWithNewAttemptAndSameSeed()
        {
            CombatDefinitions defs = ReactiveEightEnemyDefinitions.CreateForTests();
            BattleCheckpoint first = BattleCheckpoint.CreateInitial(defs, "contract:9", 1, 921);
            BattleCheckpoint retry = BattleCheckpoint.CreateInitial(defs, first.economicRunId,
                first.attemptId + 1, first.seed);
            Require(retry.economicRunId == first.economicRunId && retry.seed == first.seed &&
                retry.attemptId == 2 && retry.waveInstanceId != first.waveInstanceId,
                "new attempt retains economy/seed and changes wave identity");
            var replay = new ReactiveCombatSession(defs, retry);
            replay.Start();
            Require(replay.CurrentWaveIndex == 0 && Joined(replay.ActiveEnemyIds) == "E1,E2,E3" &&
                replay.HunterHp == 100 && replay.HunterAp == 4,
                "retry restores wave one baseline and applies first natural turn once");
        }

        private static void WaveTransitionReloadAndVictoryPreserveIdentity()
        {
            CombatDefinitions defs = FastWaves();
            var session = new ReactiveCombatSession(defs,
                BattleCheckpoint.CreateInitial(defs,
                    ContractService.GetEconomicRunId("contract_subway_001", 1), 1, 17));
            session.Start();
            Require(session.SubmitCommand(new CommandIntent("w1", session.Revision,
                CommandKind.Skill, "sweep", new string[0])).Accepted, "wave one Sweep");
            session.Advance(500000, 500000);
            Require(session.Phase == ReactivePhase.WaveTransition && session.CurrentWaveIndex == 0,
                "three simultaneous kills clear wave one");
            Require(session.ActiveEnemyIds.Count == 0 && session.HunterAp == 4,
                "transition does not grant AP or spawn early");
            BattleCheckpoint cleared = session.GetStableCheckpoint();
            Require(cleared.phase == ReactivePhase.WaveTransition.ToString() && cleared.waveIndex == 0,
                "wave clear is durable checkpoint");
            var restored = new ReactiveCombatSession(defs, cleared);
            restored.Start();
            Require(restored.Phase == ReactivePhase.WaveTransition && restored.HunterAp == 4,
                "reload restores transition without AP duplication");
            restored.Advance(1999999, 1999999);
            Require(restored.CurrentWaveIndex == 0, "full two second transition not elapsed");
            restored.Advance(2000000, 2000000);
            Require(restored.CurrentWaveIndex == 1 && Joined(restored.ActiveEnemyIds) == "E4,E5,E6",
                "wave two activates exactly three reserves");
            Require(restored.Queue.NowTick == cleared.nowTick + 60 && restored.ActiveActorId == "E4",
                "first new enemy dispatches at prior queue tick plus authored notice");
            Require(!restored.Queue.GetEntry("E1").Alive && !restored.Queue.GetEntry("E1").Active,
                "dead previous wave cannot contaminate queue");
            string secondInstance = restored.WaveInstanceId;
            BattleCheckpoint entry = restored.GetStableCheckpoint();
            var replay = new ReactiveCombatSession(defs, entry);
            replay.Start();
            Require(replay.CurrentWaveIndex == 1 && replay.WaveInstanceId == secondInstance,
                "wave entry identity survives reload");
            FinishFastWave(replay, "w2");
            Require(replay.Phase == ReactivePhase.WaveTransition && replay.CurrentWaveIndex == 1,
                "wave two clears");
            BattleCheckpoint afterWaveTwo = replay.GetStableCheckpoint();
            Require(afterWaveTwo.hunterHp < entry.hunterHp && afterWaveTwo.hunterAp > entry.hunterAp,
                "wave two changed HP/AP after its entry snapshot");
            var jsonOptions = new JsonSerializerOptions { IncludeFields = true };
            BattleCheckpoint durable = JsonSerializer.Deserialize<BattleCheckpoint>(
                JsonSerializer.Serialize(afterWaveTwo, jsonOptions), jsonOptions);
            Require(durable != null && durable.waveEntry != null,
                "wave-entry snapshot survives durable JSON roundtrip");
            durable.restApplied = true;
            BattleCheckpoint assist = durable.CreateWaveRetry(durable.attemptId + 1);
            Require(assist.economicRunId == afterWaveTwo.economicRunId &&
                assist.seed == afterWaveTwo.seed && assist.attemptId == 2 &&
                assist.waveIndex == 1 && assist.waveInstanceId != entry.waveInstanceId,
                "assist retry preserves economic run and seed with fresh attempt/wave identity");
            Require(assist.hunterHp == entry.hunterHp && assist.hunterAp == entry.hunterAp &&
                assist.nowTick == entry.nowTick && !assist.restApplied &&
                assist.commands.Length == 0 && assist.nextActionOrdinal == 1,
                "assist retry restores exact wave-entry HP/AP/queue/rest and clears old commands");
            for (int i = 0; i < assist.queue.Length; i++)
                Require(assist.queue[i].nextTick == entry.queue[i].nextTick &&
                    assist.queue[i].active == entry.queue[i].active &&
                    assist.queue[i].alive == entry.queue[i].alive,
                    "assist retry queue entry " + i + " matches wave entry");
            var assistedSession = new ReactiveCombatSession(defs, assist);
            assistedSession.Start();
            Require(assistedSession.CurrentWaveIndex == 1 && assistedSession.HunterHp == entry.hunterHp &&
                Joined(assistedSession.ActiveEnemyIds) == "E4,E5,E6",
                "assist retry resumes wave two without recreating wave one");
            SaveData payment = new SaveData {
                phase = RunPhase.Payment, combatMode = CombatMode.ReactiveTurns,
                activeContractId = "contract_subway_001", contractRunSequence = 1,
                battleCheckpoint = assist.Clone()
            };
            payment.battleCheckpoint.terminalResult = CombatOutcome.Victory.ToString();
            var economy = new EconomyService();
            Require(economy.ClaimPayment(payment, new ContractDefinition()) &&
                !economy.ClaimPayment(payment, new ContractDefinition()) &&
                payment.claimedEconomicRunIds.Count == 1,
                "attempt two still has one economic-run payment entitlement");
            long next = replay.CurrentCombatUs + 2000000;
            replay.Advance(next, next);
            Require(replay.CurrentWaveIndex == 2 && Joined(replay.ActiveEnemyIds) == "E7,E8",
                "final wave has only two active enemies");
            FinishFastWave(replay, "w3");
            Require(replay.Phase == ReactivePhase.Victory && replay.TerminalResult == CombatOutcome.Victory,
                "last two kills complete encounter once");
            var terminal = new ReactiveCombatSession(defs, replay.GetStableCheckpoint());
            Require(terminal.Start().Phase == ReactivePhase.Victory,
                "terminal result survives checkpoint reload");
        }

        private static void EightEnemyEncounterCompletesWithOneHunter()
        {
            CombatDefinitions defs = ReactiveEightEnemyDefinitions.CreateForTests();
            var session = new ReactiveCombatSession(defs);
            session.Start();
            long now = 0;
            int commands = 0;
            int largestActive = 0;
            var participating = new HashSet<string>(StringComparer.Ordinal);
            for (int guard = 0; guard < 400 && !session.TerminalResult.HasValue; guard++)
            {
                IReadOnlyList<string> active = session.ActiveEnemyIds;
                largestActive = Math.Max(largestActive, active.Count);
                foreach (string id in active) participating.Add(id);
                if (session.Phase == ReactivePhase.PlayerCommand)
                {
                    string skill = session.HunterAp >= 3 ?
                        active.Count >= 2 ? "sweep" : "seal_strike" : null;
                    var command = new CommandIntent("auto-" + commands++, session.Revision,
                        skill == null ? CommandKind.Basic : CommandKind.Skill,
                        skill, skill == "sweep" ? new string[0] : new[] { active[0] });
                    Require(session.SubmitCommand(command).Accepted, "autoplay command accepted");
                    now += 500000;
                    session.Advance(now, now);
                }
                else if (session.Phase == ReactivePhase.EnemyExecution)
                {
                    string attackId = session.ActiveActorId;
                    participating.Add(attackId);
                    long start = session.CurrentActionStartUs;
                    AttackSequenceDefinition attack = session.CurrentAttack;
                    for (int i = 0; i < attack.Hits.Count && session.Phase == ReactivePhase.EnemyExecution; i++)
                    {
                        HitDefinition hit = attack.Hits[i];
                        long pressUs = start + hit.ImpactUs - 150000;
                        session.SubmitDefense(new DefenseIntent("d-" + guard + "-" + i,
                            session.InputEpoch, DefenseKind.Dodge, pressUs));
                        session.ReleaseDefense(DefenseKind.Dodge, session.InputEpoch);
                        now = start + hit.ImpactUs + 100000;
                        session.Advance(now, now);
                    }
                    now = Math.Max(now, start + attack.DurationUs + 100000);
                    session.Advance(now, now);
                }
                else if (session.Phase == ReactivePhase.CounterWindow)
                {
                    now += 700000;
                    session.Advance(now, now);
                }
                else if (session.Phase == ReactivePhase.WaveTransition)
                {
                    now += 2000000;
                    session.Advance(now, now);
                }
                else throw new InvalidOperationException("Unexpected phase " + session.Phase);
            }
            Require(session.TerminalResult == CombatOutcome.Victory,
                "eight-enemy authored encounter can be completed");
            Require(participating.Count == 8 && largestActive <= 3 && session.CurrentWaveIndex == 2,
                "all eight participate across 3/3/2 waves; never more than three active");
            Require(session.HunterHp == 100, "successful defensive policy preserves HP");
            Require(commands >= 12 && commands <= 35, "meaningful multi-wave command count");
        }

        private static void FinishFastWave(ReactiveCombatSession session, string commandId)
        {
            for (int i = 0; i < 10 && session.Phase != ReactivePhase.PlayerCommand; i++)
            {
                Require(session.Phase == ReactivePhase.EnemyExecution, "queued enemy executes before Hunter");
                long end = session.CurrentActionStartUs + session.CurrentAttack.DurationUs + 100000;
                session.Advance(end, end);
            }
            Require(session.Phase == ReactivePhase.PlayerCommand, "Hunter eventually receives response");
            Require(session.SubmitCommand(new CommandIntent(commandId, session.Revision,
                CommandKind.Skill, "sweep", new string[0])).Accepted, "fast Sweep commits");
            long impact = session.CurrentCombatUs + 500000;
            session.Advance(impact, impact);
        }

        private static CombatDefinitions FastWaves()
        {
            var actors = new List<ActorDefinition> {
                new ActorDefinition("P", true, 0, 100, 0, 1000, 0, "single")
            };
            for (int i = 1; i <= 8; i++)
                actors.Add(new ActorDefinition("E" + i, false, i, 100,
                    60 + 30 * ((i - 1) % 3), 24, 0, "single"));
            return new CombatDefinitions("fast-waves", actors.ToArray(),
                new[] { new AttackSequenceDefinition("single", 1600000,
                    new[] { new HitDefinition("h1", 1000000, 10,
                        DefenseResponseMask.Dodge | DefenseResponseMask.Parry) }) },
                new[] { new SkillDefinition("sweep", 0, 130, 1.2, 0,
                    TargetingMode.AllActiveEnemies) }, DefenseWindowProfile.Standard,
                3, new[] {
                    new WaveDefinition("one", new[] { "E1", "E2", "E3" }),
                    new WaveDefinition("two", new[] { "E4", "E5", "E6" }),
                    new WaveDefinition("three", new[] { "E7", "E8" })
                }, hunterAttack: 20);
        }

        private static CombatDefinitions SmallTargetEncounter()
        {
            return new CombatDefinitions("targets", new[] {
                new ActorDefinition("P", true, 0, 100, 0, 100, 0, "single"),
                new ActorDefinition("E1", false, 1, 100, 60, 20, 60, "single"),
                new ActorDefinition("E2", false, 2, 100, 90, 40, 60, "single"),
                new ActorDefinition("E3", false, 3, 100, 120, 40, 60, "single")
            }, new[] { new AttackSequenceDefinition("single", 1600000,
                new[] { new HitDefinition("h1", 1000000, 0,
                    DefenseResponseMask.Dodge | DefenseResponseMask.Parry) }) },
                new SkillDefinition[0], DefenseWindowProfile.Standard, 3);
        }

        private static CombatDefinitions FourEnemyEncounter()
        {
            return new CombatDefinitions("four", new[] {
                new ActorDefinition("P", true, 0, 100, 0, 100, 0, "single"),
                new ActorDefinition("E1", false, 1, 100, 60, 40, 0, "single"),
                new ActorDefinition("E2", false, 2, 100, 90, 40, 0, "single"),
                new ActorDefinition("E3", false, 3, 100, 120, 40, 0, "single"),
                new ActorDefinition("E4", false, 4, 100, 150, 40, 0, "single")
            }, new[] { new AttackSequenceDefinition("single", 1600000,
                new[] { new HitDefinition("h1", 1000000, 0,
                    DefenseResponseMask.Dodge | DefenseResponseMask.Parry) }) },
                new SkillDefinition[0], DefenseWindowProfile.Standard, 4);
        }

        private static CombatDefinitions HeavyGrammarEncounter()
        {
            return new CombatDefinitions("heavy-grammar", new[] {
                new ActorDefinition("P", true, 0, 100, 0, 100, 0, "heavy", initialAp: 0),
                new ActorDefinition("E", false, 1, 100, 60, 120, 60, "heavy")
            }, new[] { new AttackSequenceDefinition("heavy", 2200000,
                new[] { new HitDefinition("h1", 1450000, 12,
                    DefenseResponseMask.Dodge | DefenseResponseMask.Parry, isHeavy: true) }) },
                new SkillDefinition[0], DefenseWindowProfile.Standard, 1);
        }

        private static CombatDefinitions TimedHeavyEncounter()
        {
            return new CombatDefinitions("timed-heavy", new[] {
                new ActorDefinition("P", true, 0, 100, 0, 100, 0, "single", initialAp: 5),
                new ActorDefinition("E", false, 1, 100, 60, 200, 100, "single")
            }, new[] { new AttackSequenceDefinition("single", 1600000,
                new[] { new HitDefinition("h1", 1000000, 0,
                    DefenseResponseMask.Dodge | DefenseResponseMask.Parry) }) },
                new[] { new SkillDefinition("heavy", 5, 140, 3.4, 50,
                    offenseTimingBonus: 1.15, offenseTimingEarlyUs: 100000,
                    offenseTimingLateUs: 50000) }, DefenseWindowProfile.Standard, 1);
        }

        private static string Joined(IReadOnlyList<string> ids)
        {
            var items = new string[ids.Count];
            for (int i = 0; i < ids.Count; i++) items[i] = ids[i];
            return string.Join(",", items);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
