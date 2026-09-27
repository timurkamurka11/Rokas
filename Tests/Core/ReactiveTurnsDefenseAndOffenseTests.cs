using System;
using System.Collections.Generic;
using Rokas.Core.ReactiveTurns;

namespace Rokas.Core.Tests
{
    public static class ReactiveTurnsDefenseAndOffenseTests
    {
        public static void RunAll()
        {
            StandardDefenseWindowsKeepMicrosecondBoundaries();
            WrongDefenseLocksFirstAttemptUntilHitFinalizes();
            SimultaneousDefensePressesPreferDodge();
            TripleSequenceResolvesInOrderAndOnlyOnce();
            LaterHitRequiresPreviousImpactAndRelease();
            LateDeliveryCannotChangeFinalizedMiss();
            LateMissCannotRetargetAnOverlappingNextHit();
            OutcomeLedgerIsFrameRateIndependent();
            DamageRoundsOnlyAfterAllFactors();
            ActionPointsAndDefenseSealHonorSeparateCaps();
            BrokenSkipVulnerabilityAndRefractoryFollowSeparateLifetimes();
            BreakInterruptsOnlyAnAuthoredSuffix();
            ResumeRequiresFreshEpochAndRelease();
            CorruptDefenseCheckpointEntersSafeError();
            GoldenDuelCompletesWithFourCommandsAndExactLedger();
        }

        private static void StandardDefenseWindowsKeepMicrosecondBoundaries()
        {
            DefenseWindowProfile profile = DefenseWindowProfile.Standard;
            Check(DefenseOutcome.Ignored, DefenseResolver.Classify(DefenseKind.Dodge, -400001, profile), "Dodge before acquisition");
            Check(DefenseOutcome.EarlyFail, DefenseResolver.Classify(DefenseKind.Dodge, -400000, profile), "Dodge at acquisition start");
            Check(DefenseOutcome.EarlyFail, DefenseResolver.Classify(DefenseKind.Dodge, -240001, profile), "Dodge one microsecond early");
            Check(DefenseOutcome.Dodge, DefenseResolver.Classify(DefenseKind.Dodge, -240000, profile), "Dodge early endpoint");
            Check(DefenseOutcome.Dodge, DefenseResolver.Classify(DefenseKind.Dodge, 60000, profile), "Dodge late endpoint");
            Check(DefenseOutcome.Ignored, DefenseResolver.Classify(DefenseKind.Dodge, 60001, profile), "Dodge after acquisition");

            Check(DefenseOutcome.EarlyFail, DefenseResolver.Classify(DefenseKind.Parry, -110001, profile), "Parry one microsecond early");
            Check(DefenseOutcome.Parry, DefenseResolver.Classify(DefenseKind.Parry, -110000, profile), "Parry early endpoint");
            Check(DefenseOutcome.Parry, DefenseResolver.Classify(DefenseKind.Parry, -40001, profile), "Parry before Perfect");
            Check(DefenseOutcome.Perfect, DefenseResolver.Classify(DefenseKind.Parry, -40000, profile), "Perfect early endpoint");
            Check(DefenseOutcome.Perfect, DefenseResolver.Classify(DefenseKind.Parry, 25000, profile), "Perfect late endpoint");
            Check(DefenseOutcome.Parry, DefenseResolver.Classify(DefenseKind.Parry, 25001, profile), "Parry after Perfect");
            Check(DefenseOutcome.Parry, DefenseResolver.Classify(DefenseKind.Parry, 45000, profile), "Parry late endpoint");
            Check(DefenseOutcome.LateFail, DefenseResolver.Classify(DefenseKind.Parry, 45001, profile), "Parry one microsecond late");
            Check(DefenseOutcome.LateFail, DefenseResolver.Classify(DefenseKind.Parry, 60000, profile), "Parry at acquisition end");
            Check(DefenseOutcome.Ignored, DefenseResolver.Classify(DefenseKind.Parry, 60001, profile), "Parry after acquisition");
        }

        private static void WrongDefenseLocksFirstAttemptUntilHitFinalizes()
        {
            DefenseSequenceLedger ledger = SingleHitLedger(DefenseResponseMask.Dodge);
            IReadOnlyList<DefenseAttempt> wrong = ledger.SubmitPresses(new[] {
                new DefensePress("wrong", 1, DefenseKind.Parry, 960000)
            });
            Check(1, wrong.Count, "one first attempt");
            Check(true, wrong[0].Accepted, "wrong grammar still consumes attempt");
            Check("h1", wrong[0].HitId, "first attempt hit identity");
            Check(DefenseOutcome.WrongDefense, wrong[0].Outcome, "wrong grammar reason");

            ledger.Release(DefenseKind.Parry);
            IReadOnlyList<DefenseAttempt> correction = ledger.SubmitPresses(new[] {
                new DefensePress("correction", 1, DefenseKind.Dodge, 1000000)
            });
            Check(false, correction[0].Accepted, "second press cannot fix same hit");
            Check(0, ledger.Advance(1099999, 1059999).Count, "failed hit waits for late delivery watermark");
            IReadOnlyList<DefenseHitResolution> resolved = ledger.Advance(1100000, 1060000);
            Check(1, resolved.Count, "hit finalizes once");
            Check(DefenseOutcome.WrongDefense, resolved[0].Outcome, "failed outcome persists");
            Check(8, resolved[0].RawDamage, "failed defense preserves authored damage");
            Check(0, ledger.Advance(1200000, 1160000).Count, "resolved hit never repeats");
        }

        private static void SimultaneousDefensePressesPreferDodge()
        {
            DefenseSequenceLedger ledger = SingleHitLedger(DefenseResponseMask.Dodge | DefenseResponseMask.Parry);
            IReadOnlyList<DefenseAttempt> attempts = ledger.SubmitPresses(new[] {
                new DefensePress("parry", 1, DefenseKind.Parry, 1000000),
                new DefensePress("dodge", 1, DefenseKind.Dodge, 1000000)
            });
            Check(2, attempts.Count, "both physical events reported");
            Check(true, attempts[0].Accepted, "Dodge wins same-time tie");
            Check(DefenseOutcome.Dodge, attempts[0].Outcome, "tie resolved as Dodge");
            Check(false, attempts[1].Accepted, "Parry cannot change tie result");
        }

        private static DefenseSequenceLedger SingleHitLedger(DefenseResponseMask allowed)
        {
            var sequence = new AttackSequenceDefinition("single", 1600000,
                new[] { new HitDefinition("h1", 1000000, 8, allowed) });
            return new DefenseSequenceLedger("action-1", sequence, 0, DefenseWindowProfile.Standard, 1);
        }

        private static void TripleSequenceResolvesInOrderAndOnlyOnce()
        {
            var sequence = new AttackSequenceDefinition("triple", 3850000, new[] {
                new HitDefinition("h1", 1000000, 8, DefenseResponseMask.Dodge | DefenseResponseMask.Parry),
                new HitDefinition("h2", 1650000, 8, DefenseResponseMask.Dodge | DefenseResponseMask.Parry),
                new HitDefinition("h3", 2550000, 8, DefenseResponseMask.Dodge | DefenseResponseMask.Parry)
            });
            var ledger = new DefenseSequenceLedger("e60", sequence, 0, DefenseWindowProfile.Standard, 1);
            Check(DefenseOutcome.Parry, ledger.SubmitPresses(new[] { new DefensePress("p1", 1, DefenseKind.Parry, 950000) })[0].Outcome, "first Parry");
            ledger.Release(DefenseKind.Parry);
            Check(1, ledger.Advance(1100000, 1060000).Count, "first hit delivered");
            Check(DefenseOutcome.EarlyFail, ledger.SubmitPresses(new[] { new DefensePress("p2", 1, DefenseKind.Parry, 1300000) })[0].Outcome, "second early failure");
            ledger.Release(DefenseKind.Parry);
            Check(1, ledger.Advance(1750000, 1710000).Count, "second hit delivered");
            Check(DefenseOutcome.Perfect, ledger.SubmitPresses(new[] { new DefensePress("p3", 1, DefenseKind.Parry, 2550000) })[0].Outcome, "final Perfect");
            IReadOnlyList<DefenseHitResolution> final = ledger.Advance(2650000, 2610000);
            Check(1, final.Count, "third hit delivered");
            Check(true, ledger.CounterEligible, "Parry and final Perfect open Counter");
            Check(2650000L, ledger.CounterOpenUs, "Counter open is last impact plus 100 ms");
            Check(3250000L, ledger.CounterCloseUs, "Counter is open for 600 ms");
            Check(0, ledger.Advance(3900000, 3860000).Count, "all hits exactly once");
        }

        private static void LaterHitRequiresPreviousImpactAndRelease()
        {
            var sequence = new AttackSequenceDefinition("pair", 2000000, new[] {
                new HitDefinition("h1", 1000000, 8, DefenseResponseMask.Dodge | DefenseResponseMask.Parry),
                new HitDefinition("h2", 1400000, 8, DefenseResponseMask.Dodge | DefenseResponseMask.Parry)
            });
            var ledger = new DefenseSequenceLedger("pair-action", sequence, 0, DefenseWindowProfile.Standard, 1);
            Check(true, ledger.SubmitPresses(new[] { new DefensePress("a", 1, DefenseKind.Parry, 1000000) })[0].Accepted, "first accepted");
            Check(false, ledger.SubmitPresses(new[] { new DefensePress("b", 1, DefenseKind.Dodge, 1000001) })[0].Accepted, "new kind cannot target second without release");
            ledger.Release(DefenseKind.Parry);
            Check(true, ledger.SubmitPresses(new[] { new DefensePress("c", 1, DefenseKind.Parry, 1200000) })[0].Accepted, "release rearms second hit after first impact");
        }

        private static void LateDeliveryCannotChangeFinalizedMiss()
        {
            DefenseSequenceLedger ledger = SingleHitLedger(DefenseResponseMask.Dodge | DefenseResponseMask.Parry);
            Check(1, ledger.Advance(1100000, 1060000).Count, "miss finalizes at watermark");
            Check(false, ledger.SubmitPresses(new[] { new DefensePress("late", 1, DefenseKind.Parry, 1000000) })[0].Accepted, "late delivered input cannot revise miss");
            Check(true, ledger.LateDeliveryObserved, "late delivery diagnosed");
            Check(0, ledger.Advance(1200000, 1160000).Count, "no duplicate after late event");
        }

        private static void LateMissCannotRetargetAnOverlappingNextHit()
        {
            var mask = DefenseResponseMask.Dodge | DefenseResponseMask.Parry;
            var pair = new AttackSequenceDefinition("pair", 2100000, new[] {
                new HitDefinition("h1", 1000000, 8, mask),
                new HitDefinition("h2", 1350000, 8, mask)
            });
            var ledger = new DefenseSequenceLedger("pair", pair, 0, DefenseWindowProfile.Standard, 1);
            ledger.Advance(1100000, 1060000);
            DefenseAttempt late = ledger.SubmitPresses(new[] {
                new DefensePress("late", 1, DefenseKind.Parry, 1050000)
            })[0];
            Check(false, late.Accepted, "late miss cannot migrate to overlap");
            Check(true, ledger.LateDeliveryObserved, "overlap late delivery diagnosed");
        }

        private static void OutcomeLedgerIsFrameRateIndependent()
        {
            string thirty = ResolveTripleAtFrameStep(33333);
            Check(thirty, ResolveTripleAtFrameStep(16666), "30/60 fps outcome ledger");
            Check(thirty, ResolveTripleAtFrameStep(8333), "30/120 fps outcome ledger");
            Check("h1:Parry:1000000|h2:EarlyFail:1650000|h3:Perfect:2550000", thirty,
                "authored hit times and outcomes stable");
        }

        private static string ResolveTripleAtFrameStep(long stepUs)
        {
            var mask = DefenseResponseMask.Dodge | DefenseResponseMask.Parry;
            var sequence = new AttackSequenceDefinition("triple", 3850000, new[] {
                new HitDefinition("h1", 1000000, 8, mask),
                new HitDefinition("h2", 1650000, 8, mask),
                new HitDefinition("h3", 2550000, 8, mask)
            });
            var ledger = new DefenseSequenceLedger("action", sequence, 0, DefenseWindowProfile.Standard, 1);
            ledger.SubmitPresses(new[] { new DefensePress("p1", 1, DefenseKind.Parry, 950000) });
            ledger.Release(DefenseKind.Parry);
            ledger.SubmitPresses(new[] { new DefensePress("p2", 1, DefenseKind.Parry, 1300000) });
            ledger.Release(DefenseKind.Parry);
            ledger.SubmitPresses(new[] { new DefensePress("p3", 1, DefenseKind.Parry, 2550000) });
            var events = new List<string>();
            for (long combatUs = 0; combatUs < 3900000; combatUs += stepUs)
                foreach (DefenseHitResolution hit in ledger.Advance(combatUs, combatUs - 40000))
                    events.Add(hit.HitId + ":" + hit.Outcome + ":" + hit.ImpactUs);
            return string.Join("|", events);
        }

        private static void DamageRoundsOnlyAfterAllFactors()
        {
            Check(16, DamageResolver.Resolve(new DamageRequest(1, 20, 25)), "Basic against Defense 25");
            Check(38, DamageResolver.Resolve(new DamageRequest(2.4, 20, 0) { Difficulty = .8 }), "Story Seal Strike rounds once");
            Check(63, DamageResolver.Resolve(new DamageRequest(3.4, 20, 0) { Difficulty = .8, Timing = 1.15 }), "Heavy timing rounds once");
            Check(0, DamageResolver.Resolve(new DamageRequest(1, 8, 0) { DefenseOutcomeMultiplier = 0 }), "successful defense blocks HP");
            Check(2, DamageResolver.Resolve(new DamageRequest(1, 8, 0) { DefenseOutcomeMultiplier = .5, DefendMultiplier = .5 }), "heavy Parry and Defend multiply before rounding");
        }

        private static void ActionPointsAndDefenseSealHonorSeparateCaps()
        {
            var points = new ActionPointLedger(3);
            points.OnNaturalPlayerTurnStart();
            Check(4, points.Current, "first command starts with four AP");
            Check(true, points.TrySpend(3), "Seal Strike costs three AP");
            Check(1, points.Current, "skill cost charged once");
            points.BeginEnemyAction("e60");
            DefenseReward first = points.GrantDefense("e60/h1", DefenseOutcome.Parry);
            DefenseReward second = points.GrantDefense("e60/h2", DefenseOutcome.EarlyFail);
            DefenseReward final = points.GrantDefense("e60/h3", DefenseOutcome.Perfect);
            Check(1, first.ApGranted, "ordinary Parry AP");
            Check(12, first.SealDamage, "ordinary Parry Seal");
            Check(0, second.ApGranted, "failure AP");
            Check(0, second.SealDamage, "failure Seal");
            Check(1, final.ApGranted, "Perfect clipped by defense interval cap");
            Check(20, final.SealDamage, "Perfect Seal unaffected by AP cap");
            Check(3, points.Current, "triple leaves three AP");
            Check(0, points.GrantDefense("e60/h3", DefenseOutcome.Perfect).SealDamage, "duplicate hit cannot reward twice");
            points.OnNaturalPlayerTurnStart();
            Check(4, points.Current, "next turn grants one and resets defense cap");
            Check(true, points.TrySpend(3), "second Seal Strike cost");
            points.OnNaturalPlayerTurnStart();
            Check(2, points.Current, "third player turn AP");
            points.GrantBasic("p3/impact");
            points.GrantBasic("p3/impact");
            Check(4, points.Current, "Basic grants two AP once per impact");
        }

        private static void BrokenSkipVulnerabilityAndRefractoryFollowSeparateLifetimes()
        {
            var seal = new SealLedger(60, 1);
            seal.ApplyDamage(35, "p1", "p1/impact");
            Check(25, seal.Remaining, "first Seal Strike leaves 25");
            seal.ApplyDamage(12, "e60", "e60/h1");
            seal.ApplyDamage(20, "e60", "e60/h3");
            Check(true, seal.IsBroken, "defensive Seal damage breaks enemy");
            Check(1.25, seal.DamageMultiplier, "counter sees Broken multiplier");
            Check(true, seal.TryConsumeSkipToken(), "next natural enemy slot skips once");
            Check(false, seal.TryConsumeSkipToken(), "skip token cannot repeat");
            seal.CompleteOffensiveCommand("p2", true);
            Check(false, seal.IsBroken, "next offensive command consumes vulnerability");
            Check(60, seal.Remaining, "Seal restores after vulnerability use");
            Check(1, seal.RefractoryActionsRemaining, "normal enemy refractory lasts one completed action");
            seal.ApplyDamage(12, "p3", "p3/impact");
            Check(60, seal.Remaining, "Seal damage during refractory is zero");
            seal.CompleteNaturalEnemyAction();
            Check(0, seal.RefractoryActionsRemaining, "completed action ends refractory");
            seal.ApplyDamage(12, "p4", "p4/impact");
            Check(48, seal.Remaining, "Seal damage resumes afterward");
        }

        private static void GoldenDuelCompletesWithFourCommandsAndExactLedger()
        {
            var mask = DefenseResponseMask.Dodge | DefenseResponseMask.Parry;
            var triple = new AttackSequenceDefinition("triple", 3850000, new[] {
                new HitDefinition("h1", 1000000, 8, mask),
                new HitDefinition("h2", 1650000, 8, mask),
                new HitDefinition("h3", 2550000, 8, mask)
            }, interruptibleOnBreak: true);
            var single = new AttackSequenceDefinition("single", 1600000, new[] {
                new HitDefinition("h1", 1000000, 8, mask)
            });
            var definitions = new CombatDefinitions("golden", new[] {
                new ActorDefinition("P", true, 0, 100, 0, 100, 0, "single"),
                new ActorDefinition("E", false, 1, 100, 60, 180, 60, "triple", 0, new[] { "triple", "single" })
            }, new[] { triple, single }, new[] {
                new SkillDefinition("seal_strike", 3, 115, 2.4, 35)
            }, DefenseWindowProfile.Standard, hunterAttack: 20);
            var session = new ReactiveCombatSession(definitions);
            session.Start();
            Check(ReactivePhase.PlayerCommand, session.Phase, "P0 starts");
            Check(4, session.HunterAp, "P0 gains AP");
            CommandImpactPreview firstPreview = session.PreviewCommandImpact(CommandKind.Skill, "seal_strike", "E");
            Check(48, firstPreview.Damage, "P0 preview damage uses combat formula");
            Check(35, firstPreview.SealDamage, "P0 preview Seal");
            Check(3, firstPreview.ApCost, "P0 preview AP cost");
            Check(false, firstPreview.WouldBreak, "P0 preview break status");
            Check(true, session.SubmitCommand(new CommandIntent("p0", session.Revision, CommandKind.Skill,
                "seal_strike", new[] { "E" })).Accepted, "P0 Seal Strike commits");
            session.Advance(440000);
            Check(132, session.EnemyHp, "P0 enemy HP");
            Check(25, session.EnemySeal, "P0 enemy Seal");
            Check(1, session.HunterAp, "P0 AP");
            Check(ReactivePhase.EnemyExecution, session.Phase, "E60 triple starts");
            long attackStart = session.CurrentActionStartUs;
            long epoch = session.InputEpoch;
            Check(DefenseOutcome.Parry, session.SubmitDefense(new DefenseIntent("parry1", epoch,
                DefenseKind.Parry, attackStart + 950000)).Outcome, "golden first Parry");
            session.ReleaseDefense(DefenseKind.Parry, epoch);
            session.Advance(attackStart + 1100000);
            Check(DefenseOutcome.EarlyFail, session.SubmitDefense(new DefenseIntent("fail2", epoch,
                DefenseKind.Parry, attackStart + 1300000)).Outcome, "golden early fail");
            session.ReleaseDefense(DefenseKind.Parry, epoch);
            session.Advance(attackStart + 1750000);
            Check(DefenseOutcome.Perfect, session.SubmitDefense(new DefenseIntent("perfect3", epoch,
                DefenseKind.Parry, attackStart + 2550000)).Outcome, "golden final Perfect");
            Check(false, session.ConfirmCounter("early-counter", epoch, attackStart + 2649999).Accepted,
                "Counter one microsecond before open rejected");
            Check(true, session.ConfirmCounter("counter", epoch, attackStart + 2650000).Accepted, "Counter confirmed");
            Check(false, session.ConfirmCounter("counter", epoch, attackStart + 2650000).Accepted,
                "Counter same input cannot confirm twice");
            Check(92, session.HunterHp, "triple takes exactly 8 HP");
            Check(4, session.HunterAp, "triple grants capped 2 AP then P115 grants one");
            Check(0, session.EnemySeal, "triple breaks Seal");
            Check(true, session.EnemyBroken, "Break state is explicit");
            Check(107, session.EnemyHp, "Counter deals 25 Broken damage");
            Check(ReactivePhase.PlayerCommand, session.Phase, "P115 follows Counter");
            Check(4, session.HunterAp, "P115 AP start");

            BattleCheckpoint stable = session.GetStableCheckpoint();
            session = new ReactiveCombatSession(definitions, stable);
            session.Start();
            Check(ReactivePhase.PlayerCommand, session.Phase, "P115 checkpoint restores directly");
            Check(4, session.HunterAp, "restore does not grant extra AP");
            Check(0, session.EnemySeal, "restore preserves Broken Seal");
            Check(true, session.EnemyBroken, "restore preserves Broken state");
            Check(60, session.PreviewCommandImpact(CommandKind.Skill, "seal_strike", "E").Damage,
                "P115 preview includes Broken multiplier");

            Check(true, session.SubmitCommand(new CommandIntent("p115", session.Revision, CommandKind.Skill,
                "seal_strike", new[] { "E" })).Accepted, "P115 Seal Strike commits");
            session.Advance(session.CurrentActionStartUs + 440000);
            Check(47, session.EnemyHp, "P115 Broken strike deals 60");
            Check(60, session.EnemySeal, "Seal restored after vulnerability");
            Check(false, session.EnemyBroken, "vulnerability consumed");
            Check(ReactivePhase.PlayerCommand, session.Phase, "E160 skipped and P230 starts");
            Check(2, session.HunterAp, "P230 AP start");
            Check(true, session.SubmitCommand(new CommandIntent("p230", session.Revision, CommandKind.Basic,
                null, new[] { "E" })).Accepted, "P230 Basic commits");
            session.Advance(session.CurrentActionStartUs + 440000);
            Check(27, session.EnemyHp, "P230 Basic deals 20");
            Check(60, session.EnemySeal, "refractory rejects Seal damage");
            Check(4, session.HunterAp, "Basic grants 2 AP");
            Check(ReactivePhase.EnemyExecution, session.Phase, "E260 single starts");
            attackStart = session.CurrentActionStartUs;
            Check(DefenseOutcome.Dodge, session.SubmitDefense(new DefenseIntent("dodge", epoch,
                DefenseKind.Dodge, attackStart + 1000000)).Outcome, "single Dodge");
            session.Advance(attackStart + 1640000);
            Check(92, session.HunterHp, "single Dodge blocks damage");
            Check(ReactivePhase.PlayerCommand, session.Phase, "P330 starts");
            Check(5, session.HunterAp, "P330 AP start");
            Check(true, session.SubmitCommand(new CommandIntent("p330", session.Revision, CommandKind.Skill,
                "seal_strike", new[] { "E" })).Accepted, "P330 Seal Strike commits");
            session.Advance(session.CurrentActionStartUs + 440000);
            Check(0, session.EnemyHp, "P330 ends encounter");
            Check(2, session.HunterAp, "P330 final AP");
            Check(CombatOutcome.Victory, session.TerminalResult.Value, "four command victory");
        }

        private static void BreakInterruptsOnlyAnAuthoredSuffix()
        {
            var mask = DefenseResponseMask.Dodge | DefenseResponseMask.Parry;
            var triple = new AttackSequenceDefinition("triple", 3000000, new[] {
                new HitDefinition("h1", 1000000, 8, mask),
                new HitDefinition("h2", 1600000, 8, mask),
                new HitDefinition("h3", 2200000, 8, mask)
            }, interruptibleOnBreak: true);
            var definitions = new CombatDefinitions("interrupt", new[] {
                new ActorDefinition("P", true, 0, 100, 0, 100, 0, "triple"),
                new ActorDefinition("E", false, 1, 100, 60, 100, 12, "triple")
            }, new[] { triple }, new SkillDefinition[0], DefenseWindowProfile.Standard);
            var session = new ReactiveCombatSession(definitions);
            session.Start();
            session.SubmitCommand(new CommandIntent("defend", session.Revision, CommandKind.Defend, null, null));
            session.Advance(440000);
            long start = session.CurrentActionStartUs;
            session.SubmitDefense(new DefenseIntent("first-parry", session.InputEpoch,
                DefenseKind.Parry, start + 950000));
            CombatStep step = session.Advance(start + 3040000);
            Check(100, session.HunterHp, "Break cancels later authored hits in same batched Advance");
            int hitEvents = 0;
            foreach (CombatEvent combatEvent in step.Events)
                if (combatEvent.Kind == CombatEventKind.HitResolved && combatEvent.ActorId == "E") hitEvents++;
            Check(1, hitEvents, "only first hit resolves after interruptible Break");
        }

        private static void ResumeRequiresFreshEpochAndRelease()
        {
            var mask = DefenseResponseMask.Dodge | DefenseResponseMask.Parry;
            var single = new AttackSequenceDefinition("single", 1600000, new[] {
                new HitDefinition("h1", 1000000, 8, mask)
            });
            var definitions = new CombatDefinitions("epoch", new[] {
                new ActorDefinition("P", true, 0, 100, 0, 100, 0, "single"),
                new ActorDefinition("E", false, 1, 100, 60, 100, 60, "single")
            }, new[] { single }, new SkillDefinition[0], DefenseWindowProfile.Standard);
            var session = new ReactiveCombatSession(definitions);
            session.Start();
            session.SubmitCommand(new CommandIntent("defend", session.Revision, CommandKind.Defend, null, null));
            session.Advance(440000);
            long start = session.CurrentActionStartUs;
            long stale = session.InputEpoch;
            session.Suspend("RawFrameGap");
            session.Resume(stale + 2);
            Check(false, session.SubmitDefense(new DefenseIntent("stale", stale,
                DefenseKind.Parry, start + 1000000)).Accepted, "prior epoch rejected");
            Check(false, session.SubmitDefense(new DefenseIntent("held", session.InputEpoch,
                DefenseKind.Parry, start + 1000000)).Accepted, "fresh epoch requires release");
            session.ReleaseDefense(DefenseKind.Parry, session.InputEpoch);
            Check(true, session.SubmitDefense(new DefenseIntent("rearmed", session.InputEpoch,
                DefenseKind.Parry, start + 1000000)).Accepted, "release rearms after resume");
        }

        private static void CorruptDefenseCheckpointEntersSafeError()
        {
            var sequence = new AttackSequenceDefinition("single", 1600000, new[] {
                new HitDefinition("h1", 1000000, 8, DefenseResponseMask.Dodge | DefenseResponseMask.Parry)
            });
            var definitions = new CombatDefinitions("checkpoint", new[] {
                new ActorDefinition("P", true, 0, 100, 0, 100, 0, "single"),
                new ActorDefinition("E", false, 1, 100, 60, 100, 60, "single")
            }, new[] { sequence }, new SkillDefinition[0], DefenseWindowProfile.Standard);
            var first = new ReactiveCombatSession(definitions);
            first.Start();
            BattleCheckpoint corrupted = first.GetStableCheckpoint();
            corrupted.defenseApThisInterval = 3;
            var resumed = new ReactiveCombatSession(definitions, corrupted);
            Check(ReactivePhase.SafeError, resumed.Start().Phase, "invalid AP cap checkpoint safely rejected");
            corrupted = first.GetStableCheckpoint();
            corrupted.sealStates[0].remaining = -1;
            resumed = new ReactiveCombatSession(definitions, corrupted);
            Check(ReactivePhase.SafeError, resumed.Start().Phase, "invalid Seal checkpoint safely rejected");
        }

        private static void Check<T>(T expected, T actual, string label)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException(label + ": expected " + expected + ", got " + actual);
        }
    }
}
