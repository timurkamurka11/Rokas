using System;
using Rokas.Core.ReactiveTurns;

namespace Rokas.Core.Tests
{
    internal static class ReactiveTurnsFoundationTests
    {
        public static void RunAll()
        {
            Check("valid definitions", ValidDefinitions);
            Check("invalid definitions identify fields", InvalidDefinitions);
            Check("invalid timing and active cap", InvalidTimingAndActiveCap);
            Check("clock pause and epochs", ClockPause);
            Check("clock speed segments", ClockSpeed);
            Check("queue ordering and budget", QueueOrderingAndBudget);
            Check("forecast uses live scheduler", ForecastMatchesDispatch);
            Check("enemy sequence fixed at turn start", EnemySequenceSelection);
            Check("command idempotency and revision", CommandIdempotency);
            Check("stable player command restores without turn gain", StablePlayerCommandRestores);
            Check("invalid command leaves state untouched", InvalidCommandIsAtomic);
            Check("queue ties death and summon notice", QueueIdentityCases);
            Check("attack and break policy are validated data", AttackAndBreakMetadata);
            Check("player dispatch advances queue time", PlayerDispatchAdvancesQueueTime);
            Check("committed command ID survives stable reload", CommandIdempotencySurvivesReload);
            Check("rejected command ID keeps first response", RejectedCommandIdempotency);
            Check("input epoch only moves forward", InputEpochIsMonotonic);
            Check("content hash changes with authored timing", ContentHashCoversTiming);
            Check("corrupt checkpoint enters safe error", CorruptCheckpointIsRejected);
            Check("defeat checkpoint reloads as terminal", DefeatCheckpointRestores);
        }

        private static CombatDefinitions Duel()
        {
            return new CombatDefinitions("duel", new[] {
                new ActorDefinition("P", true, 0, 100, 0, 100, 0, null),
                new ActorDefinition("E1", false, 1, 100, 60, 180, 60, "single"),
                new ActorDefinition("E2", false, 2, 100, 90, 180, 60, "single"),
                new ActorDefinition("E3", false, 3, 100, 120, 180, 60, "single")
            }, new[] { new AttackSequenceDefinition("single", 2000000, new[] {
                new HitDefinition("h1", 1000000, 8, DefenseResponseMask.Dodge | DefenseResponseMask.Parry)
            }) }, new[] { new SkillDefinition("seal", 3, 115, 2.4, 35) }, DefenseWindowProfile.Standard);
        }

        private static void InvalidTimingAndActiveCap()
        {
            var invalid = new CombatDefinitions("bad", new[] {
                new ActorDefinition("P", true, 0, 100, 0, 100, 0, null, -1),
                new ActorDefinition("E1", false, 1, 100, 60, 20, 60, "fast"),
                new ActorDefinition("E2", false, 2, 100, 90, 20, 60, "fast"),
                new ActorDefinition("E3", false, 3, 100, 120, 20, 60, "fast"),
                new ActorDefinition("E4", false, 4, 100, 150, 20, 60, "fast"),
                new ActorDefinition("E5", false, 5, 100, 180, 20, 60, "fast")
            }, new[] { new AttackSequenceDefinition("fast", 8000001, new[] {
                new HitDefinition("h1", 1000000, 8, DefenseResponseMask.Dodge),
                new HitDefinition("h2", 1349999, 8, DefenseResponseMask.Dodge)
            }) }, new SkillDefinition[0], DefenseWindowProfile.Standard);
            string errors = string.Join("|", invalid.Validate().Errors);
            Require(errors.Contains("actors/P/initialAp"), errors);
            Require(errors.Contains("sequences/fast/hits/h2/gapUs"), errors);
            Require(errors.Contains("sequences/fast/durationUs"), errors);
            Require(errors.Contains("encounter/activeEnemies"), errors);
        }

        private static void ValidDefinitions()
        {
            ValidationResult result = Duel().Validate();
            Require(result.IsValid, "duel should validate");
        }

        private static void InvalidDefinitions()
        {
            CombatDefinitions invalid = new CombatDefinitions("invalid", new[] {
                new ActorDefinition("P", true, 0, 100, 0, 100, 0, null),
                new ActorDefinition("P", false, 1, 100, 60, 20, 60, "missing")
            }, new AttackSequenceDefinition[0], new SkillDefinition[0],
                new DefenseWindowProfile(400000, 60000, 240000, 60000, 110000, 45000, 40000, 50000));
            string errors = string.Join("|", invalid.Validate().Errors);
            Require(errors.Contains("actors/P/id"), errors);
            Require(errors.Contains("actors/P/attackSequenceId"), errors);
            Require(errors.Contains("window/perfectLateUs"), errors);
        }

        private static void ClockPause()
        {
            var clock = new CombatClock(0);
            long firstEpoch = clock.CurrentEpoch;
            clock.Pause(1000000);
            Require(clock.MapInput(999999, firstEpoch) == 999999, "earlier input stays valid");
            Require(!clock.MapInput(1000000, firstEpoch).HasValue, "pause boundary rejected");
            clock.Resume(3000000);
            Require(clock.MapInput(3200000, clock.CurrentEpoch) == 1200000, "resume mapping");
            Require(!clock.MapInput(3200000, firstEpoch).HasValue, "stale epoch rejected");
        }

        private static void ClockSpeed()
        {
            var clock = new CombatClock(0);
            clock.SetSpeed(1000000, .75);
            Require(clock.MapInput(1200000, clock.CurrentEpoch) == 1150000, "piecewise speed");
        }

        private static void QueueOrderingAndBudget()
        {
            CombatDefinitions definitions = Duel();
            QueueState basic = QueueState.CreateInitial(definitions);
            var scheduler = new TurnScheduler();
            TurnDecision p = scheduler.SelectNext(basic, definitions);
            Require(p.ActorId == "P" && p.DispatchTick == 0, "P0");
            scheduler.CommitAction(basic, definitions, p, 100);
            TurnDecision e1 = scheduler.SelectNext(basic, definitions);
            Require(e1.ActorId == "E1" && e1.DispatchTick == 60, "E60");
            scheduler.CommitAction(basic, definitions, e1, 100);
            TurnDecision e2 = scheduler.SelectNext(basic, definitions);
            Require(e2.ActorId == "E2" && e2.DispatchTick == 90, "E90");
            scheduler.CommitAction(basic, definitions, e2, 100);
            TurnDecision p2 = scheduler.SelectNext(basic, definitions);
            Require(p2.ActorId == "P" && p2.DispatchTick == 100 && !p2.ForcedResponse, "P100");

            QueueState heavy = QueueState.CreateInitial(definitions);
            TurnDecision hp = scheduler.SelectNext(heavy, definitions);
            scheduler.CommitAction(heavy, definitions, hp, 140);
            TurnDecision he1 = scheduler.SelectNext(heavy, definitions);
            scheduler.CommitAction(heavy, definitions, he1, 100);
            TurnDecision he2 = scheduler.SelectNext(heavy, definitions);
            scheduler.CommitAction(heavy, definitions, he2, 100);
            TurnDecision forced = scheduler.SelectNext(heavy, definitions);
            Require(forced.ActorId == "P" && forced.DispatchTick == 120 && forced.ForcedResponse, "forced P120");
            Require(heavy.GetEntry("E1").NextTick == 160, "enemy due preserved");
        }

        private static void ForecastMatchesDispatch()
        {
            CombatDefinitions definitions = Duel();
            QueueState state = QueueState.CreateInitial(definitions);
            var scheduler = new TurnScheduler();
            var candidate = new CommandIntent("c1", 0, CommandKind.Basic, null, new[] { "E1" });
            TurnForecast forecast = scheduler.Preview(state, candidate, definitions, 4);
            Require(forecast.Slots.Count == 4, "four slots");
            Require(forecast.Slots[0].ActorId == "P" && forecast.Slots[1].ActorId == "E1" && forecast.Slots[2].ActorId == "E2" && forecast.Slots[3].ActorId == "P", "forecast order");
            Require(state.GetEntry("P").NextTick == 0, "preview did not mutate real state");
        }

        private static void EnemySequenceSelection()
        {
            var enemy = new ActorDefinition("E", false, 1, 100, 60, 180, 60, "single", null,
                new[] { "single", "triple" });
            Require(EnemyPolicy.ChooseAttack(enemy, 0, 17) == "single", "first intent");
            Require(EnemyPolicy.ChooseAttack(enemy, 1, 17) == "triple", "second intent");
            Require(EnemyPolicy.ChooseAttack(enemy, 2, 17) == "single", "repeat deterministically");
        }

        private static void CommandIdempotency()
        {
            CombatDefinitions definitions = Duel();
            var session = new ReactiveCombatSession(definitions, null);
            session.Start();
            Require(session.Phase == ReactivePhase.PlayerCommand, "player command reached");
            long revision = session.Revision;
            var command = new CommandIntent("c1", revision, CommandKind.Basic, null, new[] { "E1" });
            CommandResult first = session.SubmitCommand(command);
            Require(first.Accepted, "first commit");
            Require(session.SubmitCommand(command).ActionId == first.ActionId, "same action id");
            Require(!session.SubmitCommand(new CommandIntent("c2", revision, CommandKind.Basic, null, new[] { "E1" })).Accepted, "stale revision");
        }

        private static void StablePlayerCommandRestores()
        {
            CombatDefinitions definitions = Duel();
            var session = new ReactiveCombatSession(definitions);
            session.Start();
            BattleCheckpoint snapshot = session.GetStableCheckpoint();
            Require(snapshot.phase == ReactivePhase.PlayerCommand.ToString(), "player command persisted");
            Require(snapshot.hunterAp == 4, "first turn gain persisted once");
            var restored = new ReactiveCombatSession(definitions, snapshot);
            restored.Start();
            Require(restored.Phase == ReactivePhase.PlayerCommand, "same phase");
            Require(restored.HunterAp == 4, "no duplicate turn AP");
            Require(restored.Revision == snapshot.revision, "same revision");
            Require(restored.Queue.NowTick == snapshot.nowTick, "same queue tick");
        }

        private static void InvalidCommandIsAtomic()
        {
            var session = new ReactiveCombatSession(Duel());
            session.Start();
            BattleCheckpoint before = session.GetStableCheckpoint();
            CommandResult invalid = session.SubmitCommand(new CommandIntent("invalid", session.Revision,
                CommandKind.Skill, "seal", new[] { "missing" }));
            Require(!invalid.Accepted && invalid.Reason == "InvalidTarget", "target rejected");
            Require(session.HunterAp == before.hunterAp, "AP not spent");
            Require(session.Queue.NowTick == before.nowTick, "queue not advanced");
            Require(session.Revision == before.revision, "revision not advanced");
        }

        private static void QueueIdentityCases()
        {
            CombatDefinitions definitions = Duel();
            QueueState queue = QueueState.CreateInitial(definitions);
            queue.MarkDead("E1");
            var scheduler = new TurnScheduler();
            TurnDecision p = scheduler.SelectNext(queue, definitions);
            scheduler.CommitAction(queue, definitions, p, 100);
            Require(scheduler.SelectNext(queue, definitions).ActorId == "E2", "dead actor removed");
            bool rejected = false;
            try { queue.AddSummon("E4", 4, 100, 59); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Require(rejected, "early summon rejected");
            queue.AddSummon("E4", 4, 100, 60);
            Require(queue.GetEntry("E4").NextTick == 60, "summon due accepted");
        }

        private static void AttackAndBreakMetadata()
        {
            var triple = new AttackSequenceDefinition("triple", 3850000, new[] {
                new HitDefinition("h1", 1000000, 8, DefenseResponseMask.Dodge),
                new HitDefinition("h2", 1650000, 8, DefenseResponseMask.Dodge),
                new HitDefinition("h3", 2550000, 8, DefenseResponseMask.Dodge)
            }, interruptibleOnBreak: true);
            Require(triple.InterruptibleOnBreak, "authored interrupt policy");
            var defs = new CombatDefinitions("a", new[] {
                new ActorDefinition("P", true, 0, 100, 0, 100, 0, null),
                new ActorDefinition("E", false, 1, 100, 60, 100, 60, "triple")
            }, new[] { triple }, new SkillDefinition[0], DefenseWindowProfile.Standard, hunterAttack: double.NaN);
            Require(!defs.Validate().IsValid, "nonfinite attack rejected");
            Require(string.Join("|", defs.Validate().Errors).Contains("encounter/hunterAttack"), "field named");
        }

        private static void PlayerDispatchAdvancesQueueTime()
        {
            var session = new ReactiveCombatSession(Duel());
            session.Start();
            Require(session.SubmitCommand(new CommandIntent("basic", session.Revision, CommandKind.Basic,
                null, new[] { "E1" })).Accepted, "basic accepted");
            session.Advance(500000, 500000);
            Require(session.CurrentActionStartUs == 500000, "enemy authored timeline origin");
            Require(session.EnemySealMax == 60, "HUD max Seal from immutable actor definition");
            session.Advance(3000000, 3000000);
            session.Advance(6000000, 6000000);
            Require(session.Phase == ReactivePhase.PlayerCommand, "returned to command");
            Require(session.Queue.NowTick == 100, "P100 is the current queue time");
        }

        private static void CommandIdempotencySurvivesReload()
        {
            CombatDefinitions definitions = Duel();
            var session = new ReactiveCombatSession(definitions);
            session.Start();
            var original = new CommandIntent("command-once", session.Revision, CommandKind.Basic,
                null, new[] { "E1" });
            CommandResult first = session.SubmitCommand(original);
            session.Advance(500000, 500000);
            session.Advance(3000000, 3000000);
            session.Advance(6000000, 6000000);
            BattleCheckpoint checkpoint = session.GetStableCheckpoint();
            var restored = new ReactiveCombatSession(definitions, checkpoint);
            restored.Start();
            long beforeRevision = restored.Revision;
            int beforeAp = restored.HunterAp;
            CommandResult duplicate = restored.SubmitCommand(new CommandIntent("command-once", restored.Revision,
                CommandKind.Basic, null, new[] { "E1" }));
            Require(duplicate.Accepted && duplicate.ActionId == first.ActionId, "prior response replayed");
            Require(restored.Revision == beforeRevision && restored.HunterAp == beforeAp, "no repeat effect");
        }

        private static void RejectedCommandIdempotency()
        {
            var session = new ReactiveCombatSession(Duel());
            session.Start();
            CommandResult first = session.SubmitCommand(new CommandIntent("same", session.Revision,
                CommandKind.Basic, null, new[] { "missing" }));
            Require(!first.Accepted && first.Reason == "InvalidTarget", "initial rejection");
            CommandResult duplicate = session.SubmitCommand(new CommandIntent("same", session.Revision,
                CommandKind.Basic, null, new[] { "E1" }));
            Require(!duplicate.Accepted && duplicate.Reason == first.Reason, "prior rejection replayed");
            Require(session.Phase == ReactivePhase.PlayerCommand, "no command committed");
            var restored = new ReactiveCombatSession(Duel(), session.GetStableCheckpoint());
            restored.Start();
            CommandResult afterReload = restored.SubmitCommand(new CommandIntent("same", restored.Revision,
                CommandKind.Basic, null, new[] { "E1" }));
            Require(!afterReload.Accepted && afterReload.Reason == first.Reason,
                "rejected ID survives stable reload");
        }

        private static void InputEpochIsMonotonic()
        {
            var session = new ReactiveCombatSession(Duel());
            session.Start();
            session.SetInputEpoch(7);
            Require(session.InputEpoch == 7, "new epoch accepted");
            session.SetInputEpoch(7);
            bool rejected = false;
            try { session.SetInputEpoch(6); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Require(rejected && session.InputEpoch == 7, "older epoch cannot rearm input");
        }

        private static void ContentHashCoversTiming()
        {
            CombatDefinitions original = Duel();
            CombatDefinitions revised = new CombatDefinitions("duel", new[] {
                new ActorDefinition("P", true, 0, 100, 0, 100, 0, null),
                new ActorDefinition("E1", false, 1, 100, 60, 180, 60, "single"),
                new ActorDefinition("E2", false, 2, 100, 90, 180, 60, "single"),
                new ActorDefinition("E3", false, 3, 100, 120, 180, 60, "single")
            }, new[] { new AttackSequenceDefinition("single", 2200000, new[] {
                new HitDefinition("h1", 1000000, 8, DefenseResponseMask.Dodge | DefenseResponseMask.Parry)
            }) }, new[] { new SkillDefinition("seal", 3, 115, 2.4, 35) }, DefenseWindowProfile.Standard);
            Require(original.ContentHash != revised.ContentHash, "duration changes hash");
            BattleCheckpoint checkpoint = BattleCheckpoint.CreateInitial(original, "run", 1, 17);
            Require(checkpoint.contentHash == original.ContentHash, "checkpoint records exact content");
            var changed = new ReactiveCombatSession(revised, checkpoint);
            Require(changed.Start().Phase == ReactivePhase.SafeError, "changed content rejected safely");
        }

        private static void CorruptCheckpointIsRejected()
        {
            CombatDefinitions definitions = Duel();
            BattleCheckpoint missingActor = BattleCheckpoint.CreateInitial(definitions, "run", 1, 17);
            missingActor.actors[1] = null;
            Require(new ReactiveCombatSession(definitions, missingActor).Start().Phase == ReactivePhase.SafeError,
                "missing actor snapshot rejected");
            BattleCheckpoint duplicateQueue = BattleCheckpoint.CreateInitial(definitions, "run", 1, 17);
            duplicateQueue.queue[1].actorId = "P";
            Require(new ReactiveCombatSession(definitions, duplicateQueue).Start().Phase == ReactivePhase.SafeError,
                "duplicate queue actor rejected");
        }

        private static void DefeatCheckpointRestores()
        {
            var definitions = new CombatDefinitions("lethal", new[] {
                new ActorDefinition("P", true, 0, 100, 0, 100, 0, null),
                new ActorDefinition("E", false, 1, 100, 60, 180, 60, "lethal-hit")
            }, new[] { new AttackSequenceDefinition("lethal-hit", 1600000, new[] {
                new HitDefinition("h1", 1000000, 200, DefenseResponseMask.Dodge | DefenseResponseMask.Parry)
            }) }, new SkillDefinition[0], DefenseWindowProfile.Standard);
            var session = new ReactiveCombatSession(definitions);
            session.Start();
            Require(session.SubmitCommand(new CommandIntent("c", session.Revision, CommandKind.Basic,
                null, new[] { "E" })).Accepted, "basic accepted");
            session.Advance(500000, 500000);
            session.Advance(1600000, 1600000);
            Require(session.Phase == ReactivePhase.Defeat, "lethal hit defeats Hunter");
            var restored = new ReactiveCombatSession(definitions, session.GetStableCheckpoint());
            Require(restored.Start().Phase == ReactivePhase.Defeat, "terminal defeat survives reload");
        }

        private static void Check(string name, Action test)
        {
            test();
            Console.WriteLine("PASS ReactiveTurns: " + name);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
