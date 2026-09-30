using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Core.ReactiveTurns;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class ReactiveReactionPolishPlayModeTests
    {
        private GameObject root;
        private ReactiveMissionView view;
        private int clickCues;
        private int normalCommands;
        private int heavyCommands;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            root = new GameObject("ReactiveReactionPolishFixture", typeof(RectTransform));
            clickCues = normalCommands = heavyCommands = 0;
            view = new ReactiveMissionView(new UiKit(assets, () => clickCues++), assets,
                () => normalCommands++, () => { }, () => { }, () => { },
                () => heavyCommands++, () => { }, () => { }, () => { },
                () => { }, () => { }, id => { });
            view.Build(root.GetComponent<RectTransform>());
            ReactiveBattleDisplay initial = IdleDisplay("E1", 100, 100);
            view.Refresh(initial);
            for (int step = 0; step < 400 && !view.PresentationReady; step++)
            {
                view.Tick(.05f);
                view.Refresh(initial);
            }
            Assert.That(view.PresentationReady, Is.True, "The fixture finishes the real encounter entrance before reaction assertions.");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            view?.ClearReferences();
            UnityEngine.Object.Destroy(root);
            view = null;
            root = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator IncomingCursorUsesRealPressTimestampThenResolvesAndReopensForNextHit()
        {
            ReactiveCombatSession combat = StartEnemyTriple();
            long startUs = combat.CurrentActionStartUs;
            combat.Advance(startUs + 100000);
            view.Refresh(DisplayFor(combat, 0));
            Assert.That(Find("ReactiveContactTrack").activeInHierarchy, Is.True);
            Assert.That(Find("ReactiveOffenseTiming").activeInHierarchy, Is.False);
            RectTransform cursor = Find("ReactiveTimingBeacon").GetComponent<RectTransform>();
            float firstX = cursor.anchoredPosition.x;
            Assert.That(cursor.sizeDelta.x, Is.GreaterThanOrEqualTo(6f));
            Assert.That(cursor.sizeDelta.y, Is.GreaterThanOrEqualTo(35f));

            combat.Advance(startUs + 600000);
            view.Refresh(DisplayFor(combat, 0));
            Assert.That(cursor.anchoredPosition.x, Is.GreaterThan(firstX + 200f),
                "An incoming attack needs a visibly moving cursor, not just a filling texture.");
            Assert.That(Width("ReactiveDodgeWindow"), Is.GreaterThan(Width("ReactiveBlockWindow")));
            Assert.That(Width("ReactiveBlockWindow"), Is.GreaterThan(Width("ReactivePerfectZone")));

            long pressUs = startUs + 940000;
            combat.Advance(startUs + 950000);
            view.Refresh(DisplayFor(combat, 0));
            DefenseAttempt attempt = combat.SubmitDefense(new DefenseIntent("ui-parry-one",
                combat.InputEpoch, DefenseKind.Parry, pressUs));
            Assert.That(attempt.Accepted, Is.True);
            Assert.That(attempt.Outcome, Is.EqualTo(DefenseOutcome.Parry));
            view.PresentDefenseAttempt(attempt, pressUs);
            float expectedPressX = 690f + 540f * (940000f / 1060000f) - 3f;
            Assert.That(cursor.anchoredPosition.x, Is.EqualTo(expectedPressX).Within(.01f),
                "Feedback must use the submitted timestamp rather than the later rendered frame.");
            Assert.That(Find("ReactiveHitFeedback").GetComponent<Text>().text, Is.EqualTo("БЛОК"));
            Assert.That(Find("ReactiveDefense").activeInHierarchy, Is.False,
                "A consumed press cannot invite another defense for the same hit.");
            combat.ReleaseDefense(DefenseKind.Parry, combat.InputEpoch);
            combat.Advance(startUs + 990000);
            view.Refresh(DisplayFor(combat, 0));
            view.Tick(.20f);
            Assert.That(cursor.anchoredPosition.x, Is.EqualTo(expectedPressX).Within(.01f));

            CombatStep resolved = combat.Advance(startUs + 1100000);
            Present(resolved, combat.CurrentAttack);
            view.Refresh(DisplayFor(combat, 1));
            Assert.That(Find("ReactiveContactTrack").activeInHierarchy, Is.False,
                "Resolved contact hides its reaction bar while the result lingers.");
            Assert.That(Find("ReactiveHitFeedback").GetComponent<Text>().text, Is.EqualTo("БЛОК"));
            view.Tick(.31f);
            Assert.That(Find("ReactiveContactTrack").activeInHierarchy, Is.True,
                "A later hit in the same sequence must open a fresh reaction bar.");
            Assert.That(Find("ReactiveDefense").activeInHierarchy, Is.True);
            Assert.That(cursor.anchoredPosition.x, Is.LessThan(expectedPressX));

            combat.Advance(startUs + 1600000);
            view.Refresh(DisplayFor(combat, 1));
            DefenseAttempt second = combat.SubmitDefense(new DefenseIntent("ui-parry-two",
                combat.InputEpoch, DefenseKind.Parry, startUs + 1600000));
            Assert.That(second.Accepted, Is.True, "The second hit has an independent input lifecycle.");
            view.PresentDefenseAttempt(second, startUs + 1600000);
            Assert.That(Find("ReactiveHitFeedback").GetComponent<Text>().text, Is.EqualTo("БЛОК"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MainCommandsOnlyExposeNormalAndHeavyAndHeavyDoesNotOpenEnemyReactionUi()
        {
            ReactiveBattleDisplay display = IdleDisplay("E1", 100, 100);
            view.Refresh(display);
            Assert.That(Find("ReactiveBasic").activeInHierarchy, Is.True);
            Assert.That(Find("ReactiveHeavy").activeInHierarchy, Is.True);
            foreach (string hidden in new[] { "ReactiveSealStrike", "ReactiveDefend", "ReactiveSweep", "ReactiveAnchor" })
                Assert.That(Find(hidden).activeSelf, Is.False, hidden);
            Assert.That(Find("ReactiveContactTrack").activeInHierarchy, Is.False);
            Assert.That(Find("ReactiveDefense").activeInHierarchy, Is.False);
            Find("ReactiveBasic").GetComponent<Button>().onClick.Invoke();
            Find("ReactiveHeavy").GetComponent<Button>().onClick.Invoke();
            Assert.That(normalCommands, Is.EqualTo(1));
            Assert.That(heavyCommands, Is.EqualTo(1));
            Assert.That(clickCues, Is.EqualTo(0), "Attack intent must not add another UI sound cue.");

            display.Phase = ReactiveDisplayPhase.OffenseTiming;
            display.ContactProgress = .8f;
            display.Telegraph = "ТЯЖЁЛЫЙ УДАР";
            display.TimingStartUs = 500000;
            display.TimingImpactUs = 700000;
            display.TimingEndUs = 750000;
            display.OffenseEarlyUs = 100000;
            display.OffenseLateUs = 50000;
            view.Refresh(display);
            Assert.That(Find("ReactiveOffenseTiming").activeInHierarchy, Is.True);
            Assert.That(Find("OffenseHint").GetComponent<Text>().text, Does.Contain("SPACE"));
            RectTransform heavyZone = Find("OffenseSuccessWindow").GetComponent<RectTransform>();
            Assert.That(heavyZone.anchoredPosition.x, Is.EqualTo(176f).Within(.01f),
                "The authored Heavy window begins at 100 ms of the 250 ms track.");
            Assert.That(heavyZone.sizeDelta.x, Is.EqualTo(264f).Within(.01f),
                "The authored Heavy window includes its full late endpoint at 250 ms.");
            foreach (string reactionOnly in new[] { "ReactiveContactTrack", "ReactiveTimingBeacon",
                "ReactiveDodgeWindow", "ReactiveBlockWindow", "ReactivePerfectZone", "ReactiveDefenseHint", "ReactiveDefense" })
                Assert.That(Find(reactionOnly).activeInHierarchy, Is.False, reactionOnly);
            Assert.That(Find("ReactiveBasic").activeInHierarchy, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HpTextIsImmediateBarsTweenAndReplacementSlotOwnerSnaps()
        {
            ReactiveBattleDisplay display = IdleDisplay("E1", 100, 100);
            view.Refresh(display);
            Assert.That(Width("ReactiveHunterHpFill"), Is.EqualTo(198f).Within(.001f));
            Image enemyFill = Find("ReactiveTarget1").transform.Find("EnemyHpFill").GetComponent<Image>();
            Assert.That(enemyFill.rectTransform.sizeDelta.x, Is.EqualTo(127f).Within(.001f));

            display = IdleDisplay("E1", 60, 50);
            view.Refresh(display);
            Assert.That(Find("ReactiveHunterHp").GetComponent<Text>().text, Is.EqualTo("HP 60 / 100"));
            Assert.That(Find("ReactiveTarget1").transform.Find("EnemyHp").GetComponent<Text>().text,
                Is.EqualTo("HP 50 / 100"));
            Assert.That(Width("ReactiveHunterHpFill"), Is.EqualTo(198f).Within(.001f));
            Assert.That(enemyFill.rectTransform.sizeDelta.x, Is.EqualTo(127f).Within(.001f));
            view.Tick(.15f);
            Assert.That(Width("ReactiveHunterHpFill"), Is.EqualTo(158.4f).Within(.01f));
            Assert.That(enemyFill.rectTransform.sizeDelta.x, Is.EqualTo(95.25f).Within(.01f));
            view.Tick(.15f);
            Assert.That(Width("ReactiveHunterHpFill"), Is.EqualTo(118.8f).Within(.01f));
            Assert.That(enemyFill.rectTransform.sizeDelta.x, Is.EqualTo(63.5f).Within(.01f));

            display = IdleDisplay("E2", 60, 90);
            display.Wave = 2;
            view.Refresh(display);
            Assert.That(enemyFill.rectTransform.sizeDelta.x, Is.EqualTo(114.3f).Within(.01f),
                "A replacement slot starts from its own HP instead of tweening from the previous enemy.");
            Assert.That(Find("ReactiveTarget1").transform.Find("SelectedTargetRule").gameObject.activeSelf, Is.True);
            yield return null;
        }

        private static ReactiveCombatSession StartEnemyTriple()
        {
            var contract = new ContractDefinition { id = "reaction-polish", enemyId = "E1",
                enemyHealth = 200f, enemyDamage = 8f, clickDamage = 20f };
            var combat = new ReactiveCombatSession(ReactiveDuelDefinitions.Create(contract, new SaveData()));
            combat.Start();
            Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.PlayerCommand));
            Assert.That(combat.SubmitCommand(new CommandIntent("start-triple", combat.Revision,
                CommandKind.Defend, null, null)).Accepted, Is.True);
            combat.Advance(440000);
            Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.EnemyExecution));
            Assert.That(combat.CurrentAttack.Hits.Count, Is.EqualTo(3));
            return combat;
        }

        private static ReactiveBattleDisplay DisplayFor(ReactiveCombatSession combat, int hitIndex)
        {
            HitDefinition hit = combat.CurrentAttack.Hits[hitIndex];
            long segmentStart = combat.CurrentActionStartUs + (hitIndex == 0 ? 0 :
                combat.CurrentAttack.Hits[hitIndex - 1].ImpactUs + combat.DefenseWindow.AcquireLateUs);
            long impact = combat.CurrentActionStartUs + hit.ImpactUs;
            long end = impact + combat.DefenseWindow.AcquireLateUs;
            ReactiveBattleDisplay display = IdleDisplay("E1", combat.HunterHp, combat.EnemyHp);
            display.Phase = ReactiveDisplayPhase.Reacting;
            display.ActingId = "E1";
            display.ActionId = combat.CurrentActionId;
            display.HitId = hit.Id;
            display.IncomingHit = true;
            display.TimingStartUs = segmentStart;
            display.TimingImpactUs = impact;
            display.TimingEndUs = end;
            display.DefenseWindow = combat.DefenseWindow;
            display.AllowedResponses = hit.AllowedResponses;
            display.ContactProgress = (float)(combat.CurrentCombatUs - segmentStart) / (end - segmentStart);
            display.Telegraph = "УДАР " + (hitIndex + 1) + " / 3";
            return display;
        }

        private static ReactiveBattleDisplay IdleDisplay(string ownerId, int hunterHp, int enemyHp)
        {
            return new ReactiveBattleDisplay { HunterHp = hunterHp, HunterAp = 6, Wave = 1,
                WaveCount = 3, WaveSlotCount = 1, WaveEnemyIds = new[] { ownerId },
                Enemies = new List<ReactiveEnemyDisplay> { new ReactiveEnemyDisplay { Id = ownerId,
                    Slot = 0, Name = "ЁКАЙ", Hp = enemyHp, MaxHp = 100, Seal = 60, SealMax = 60 } },
                SelectedTargetId = ownerId, Phase = ReactiveDisplayPhase.Command, CanHeavy = true };
        }

        private void Present(CombatStep step, AttackSequenceDefinition sequence)
        {
            foreach (CombatEvent evt in step.Events) view.Present(evt, sequence);
        }

        private GameObject Find(string name)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == name) return candidate.gameObject;
            Assert.Fail("Missing UI object " + name);
            return null;
        }

        private float Width(string name) { return Find(name).GetComponent<RectTransform>().sizeDelta.x; }
    }
}
