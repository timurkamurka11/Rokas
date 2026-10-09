using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Core.ReactiveTurns;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Rokas.Tests
{
    public sealed class ReactiveCinematicHudPlayModeTests
    {
        private GameObject root;
        private ReactiveMissionView view;
        private string saveDirectory;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            root = new GameObject("ReactiveCinematicHudFixture", typeof(RectTransform));
            view = new ReactiveMissionView(new UiKit(assets, null), assets,
                () => { }, () => { }, () => { }, () => { }, () => { }, () => { },
                () => { }, () => { }, () => { }, () => { }, id => { });
            view.Build(root.GetComponent<RectTransform>());
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
            if (saveDirectory != null && Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
            saveDirectory = null;
        }

        [UnityTest]
        public IEnumerator RealEntranceAndFadingTurnAnnouncementGatePlayerControls()
        {
            ReactiveBattleDisplay display = CommandDisplay();
            view.Refresh(display);
            Assert.That(view.PresentationReady, Is.False, "Core's command state must not bypass encounter entrance.");
            Assert.That(Find("ReactiveBasic").activeInHierarchy, Is.False);
            Assert.That(Find("ReactiveHeavy").activeInHierarchy, Is.False);
            Assert.That(Find("ReactiveHunterHp").activeInHierarchy, Is.False);
            Assert.That(Find("ReactiveTarget1").activeInHierarchy, Is.False);
            foreach (string hidden in new[]
            {
                "ReactiveTopShade", "ReactiveLowerShade", "ReactiveEnemyGround",
                "ReactiveForecastPanel", "ReactiveForecastRule", "ReactiveForecastTitle", "ReactiveForecast",
                "ReactiveWavePanel", "ReactiveWaveRule", "ReactiveWave", "ReactiveEnemyFacelessCommuter",
                "ReactiveTargetName", "ReactiveTargetHp", "ReactiveTargetSeal",
                "ReactiveHunterPanel", "ReactiveHunterRule", "ReactiveHunterBottomRule",
                "ReactiveHunterPortraitFrame", "ReactiveHunterKeiko", "ReactiveHunterName",
                "ReactiveHunterHp", "ReactiveHunterHpTrack", "ReactiveHunterHpFill", "ReactiveAp",
                "ReactiveSelectionHint", "ReactiveTelegraph", "ReactiveDetail",
                "ReactiveAttackWarningArt", "ReactiveTimingPromptArt", "ReactiveContactTrack",
                "ReactiveContactFill", "ReactiveDodgeWindow", "ReactiveBlockWindow", "ReactivePerfectZone",
                "ReactivePerfectWindow", "ReactiveNearBars", "ReactiveTimingBeacon", "ReactiveDefenseHint",
                "ReactiveOffenseTiming", "ReactiveHitFeedback", "ReactiveSelectedAction",
                "ReactiveWaveBanner", "ReactiveAnnouncement",
                "ReactiveCommands", "ReactiveDefense", "ReactiveCounter", "ReactiveSaveBlocked"
            })
                Assert.That(Find(hidden).activeInHierarchy, Is.False,
                    hidden + " must stay hidden during the portal/encounter entrance.");
            for (int index = 1; index <= 6; index++)
                Assert.That(Find("ReactiveApPip" + index).activeInHierarchy, Is.False);

            FinishEntrance(display);
            Assert.That(Find("ReactiveBasic").activeInHierarchy, Is.True);
            Assert.That(Find("ReactiveHunterHp").activeInHierarchy, Is.True);
            RectTransform target = Find("ReactiveTarget1").GetComponent<RectTransform>();
            Assert.That(target.sizeDelta.y, Is.LessThanOrEqualTo(43f));
            Assert.That(-target.parent.GetComponent<RectTransform>().anchoredPosition.y, Is.EqualTo(320f));
            Assert.That(target.Find("EnemyPortrait").gameObject.activeInHierarchy, Is.False);
            view.ShowTurnAnnouncement(true);
            Assert.That(view.ArenaSettled, Is.True);
            Assert.That(view.AnnouncementActive, Is.True);
            Assert.That(view.PresentationReady, Is.False);
            Assert.That(Find("ReactiveBasic").activeInHierarchy, Is.False,
                "The next command cannot be selected through a still-visible turn announcement.");
            Assert.That(Find("ReactiveHeavy").activeInHierarchy, Is.False);
            Assert.That(Find("ReactiveTarget1").activeInHierarchy, Is.False);
            view.Tick(.15f);
            Assert.That(Find("ReactiveAnnouncement").GetComponent<UnityEngine.UI.Text>().color.a, Is.GreaterThan(.5f));
            view.Tick(.55f);
            Assert.That(view.AnnouncementActive, Is.True);
            float fadingAlpha = Find("ReactiveAnnouncement").GetComponent<UnityEngine.UI.Text>().color.a;
            Assert.That(fadingAlpha, Is.InRange(.1f, .9f));
            view.Tick(.16f);
            view.Refresh(display);
            Assert.That(view.AnnouncementActive, Is.False);
            Assert.That(Find("ReactiveAnnouncement").activeInHierarchy, Is.False);
            Assert.That(view.PresentationReady, Is.True);
            Assert.That(Find("ReactiveBasic").activeInHierarchy, Is.True);
            Assert.That(Find("ReactiveContactTrack").activeInHierarchy, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CoreAheadTurnHoldSuppressesReactionUntilPresentationStarts()
        {
            ReactiveBattleDisplay display = CommandDisplay();
            FinishEntrance(display);
            display.Phase = ReactiveDisplayPhase.Reacting;
            display.ActionId = "pending-enemy-action";
            display.HitId = "incoming-hit";
            display.ActingId = "E1";
            display.IncomingHit = true;
            display.TimingStartUs = 0;
            display.TimingImpactUs = 1000000;
            display.TimingEndUs = 1060000;
            display.DefenseWindow = DefenseWindowProfile.Standard;
            display.AllowedResponses = DefenseResponseMask.Dodge | DefenseResponseMask.Parry;
            display.ContactProgress = .65f;
            view.SetPresentationLocked(true);
            view.Refresh(display);
            foreach (string reaction in new[] { "ReactiveContactTrack", "ReactiveTimingBeacon",
                "ReactiveDefense", "ReactiveDefenseHint" })
                Assert.That(Find(reaction).activeInHierarchy, Is.False, reaction);
            Assert.That(Find("ReactiveBasic").activeInHierarchy, Is.False);
            Assert.That(Find("ReactiveHeavy").activeInHierarchy, Is.False);

            view.ShowTurnAnnouncement(false);
            view.Tick(.86f);
            Assert.That(view.AnnouncementActive, Is.False);
            Assert.That(Find("ReactiveContactTrack").activeInHierarchy, Is.False,
                "Fading the announcement alone must not release a still-held next actor.");
            view.SetPresentationLocked(false);
            view.Refresh(display);
            Assert.That(Find("ReactiveContactTrack").activeInHierarchy, Is.True);
            Assert.That(Find("ReactiveDefense").activeInHierarchy, Is.True);
            Assert.That(Find("ReactiveTimingPromptArt").activeInHierarchy, Is.False,
                "The old giant click-in-time artwork must not return during an actual reaction.");
            Assert.That(Find("ReactiveAttackWarningArt").activeInHierarchy, Is.False);
            RectTransform track = Find("ReactiveContactTrack").GetComponent<RectTransform>();
            float trackCenter = track.anchoredPosition.x + track.sizeDelta.x * .5f;
            Assert.That(trackCenter, Is.EqualTo(960f).Within(.01f));
            Assert.That(-track.anchoredPosition.y, Is.GreaterThan(640f), "Reaction belongs to the lower central HUD.");
            Assert.That(Find("ReactiveTarget1").activeInHierarchy, Is.False,
                "Selection cards must not cover the centered incoming reaction panel.");
            yield return null;
        }

        private void FinishEntrance(ReactiveBattleDisplay display)
        {
            view.Refresh(display);
            for (int step = 0; step < 400 && !view.PresentationReady; step++)
            {
                view.Tick(.05f);
                view.Refresh(display);
            }
            Assert.That(view.PresentationReady, Is.True, "Authored entrance should finish before commands become usable.");
        }

        private static ReactiveBattleDisplay CommandDisplay()
        {
            return new ReactiveBattleDisplay { HunterHp = 100, HunterAp = 6, Wave = 1, WaveCount = 3,
                WaveSlotCount = 2, WaveEnemyIds = new[] { "E1", "E2" }, SelectedTargetId = "E1",
                Phase = ReactiveDisplayPhase.Command, CanHeavy = true, Enemies = new[] {
                    new ReactiveEnemyDisplay { Id = "E1", Slot = 0, Name = "ЁКАЙ1", Hp = 100, MaxHp = 100, Seal = 60, SealMax = 60 },
                    new ReactiveEnemyDisplay { Id = "E2", Slot = 1, Name = "ЁКАЙ2", Hp = 100, MaxHp = 100, Seal = 60, SealMax = 60 }
                } };
        }

        private GameObject Find(string objectName)
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == objectName) return candidate.gameObject;
            Assert.Fail("Missing cinematic HUD object " + objectName);
            return null;
        }

        [UnityTest]
        public IEnumerator FocusPauseDuringInitialPresentationHoldFreezesThenResumesFreshEpoch()
        {
            view.ClearReferences();
            view = null;
            UnityEngine.Object.Destroy(root);
            root = null;
            yield return null;
            string directory = Path.Combine(Path.GetTempPath(), "rokas-cinematic-intro-pause-" + Guid.NewGuid().ToString("N"));
            saveDirectory = directory;
            root = new GameObject("ReactiveCinematicIntroPauseFixture");
            try
            {
                RokasBootstrap boot = root.AddComponent<RokasBootstrap>();
                boot.Initialize(directory);
                boot.SendMessage("OnApplicationFocus", true);
                yield return null;
                Assert.That(boot.Session.AcceptContract(), Is.True);
                Assert.That(boot.Session.LeaveHome(), Is.True);
                Assert.That(boot.Session.EnterReactiveDuelTestEncounter(), Is.True);
                yield return null;
                Assert.That(boot.ReactivePresentationHeld, Is.True);
                Assert.That(Find("ReactiveBasic").activeInHierarchy, Is.False);
                Transform hunter = null;
                GameObject world = GameObject.Find("ReactiveCombatWorld");
                Assert.That(world, Is.Not.Null);
                foreach (ReactiveCombatActorVisual actor in world.GetComponentsInChildren<ReactiveCombatActorVisual>(true))
                    if (actor.name == "CombatActor_Keiko") hunter = actor.transform;
                Assert.That(hunter, Is.Not.Null);
                boot.SendMessage("OnApplicationFocus", false);
                Vector3 stopped = hunter.localPosition;
                long stoppedUs = boot.Session.ReactiveCombat.CurrentCombatUs;
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(hunter.localPosition, Is.EqualTo(stopped));
                Assert.That(boot.Session.ReactiveCombat.CurrentCombatUs, Is.EqualTo(stoppedUs));
                Assert.That(boot.Session.ReactiveCombat.Phase, Is.EqualTo(ReactivePhase.Suspended));
                boot.SendMessage("OnApplicationFocus", true);
                yield return new WaitForSecondsRealtime(.25f);
                Assert.That(hunter.localPosition, Is.EqualTo(stopped), "The focus preparation beat must not advance the intro.");
                Assert.That(Find("ReactiveBasic").activeInHierarchy, Is.False);
                float deadline = Time.realtimeSinceStartup + 20f;
                while ((boot.ReactivePresentationHeld || !boot.View.ReactivePresentationReady) &&
                    Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Combat));
                Assert.That(boot.Session.ReactiveCombat.Phase, Is.EqualTo(ReactivePhase.PlayerCommand),
                    "A pause inside the initial hold must resume with a fresh Core epoch instead of throwing.");
                Assert.That(boot.ReactivePresentationHeld, Is.False);
                Assert.That(boot.View.HunterAtHome, Is.True);
                Assert.That(Find("ReactiveBasic").activeInHierarchy, Is.True);
                Assert.That(Find("ReactiveContactTrack").activeInHierarchy, Is.False);
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
                root = null;
            }
        }

        [UnityTest]
        public IEnumerator ActualBootstrapWaitsForFullReturnsAndAnnouncementsBeforeNextActor()
        {
            view.ClearReferences();
            view = null;
            UnityEngine.Object.Destroy(root);
            root = null;
            yield return null;
            saveDirectory = Path.Combine(Path.GetTempPath(), "rokas-cinematic-sequence-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("ReactiveCinematicSequenceFixture");
            InputSettings.EditorInputBehaviorInPlayMode priorEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSettings.BackgroundBehavior priorBackground = InputSystem.settings.backgroundBehavior;
            Keyboard keyboard = null;
            try
            {
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                keyboard = InputSystem.AddDevice<Keyboard>();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(), InputState.currentTime);
                InputSystem.Update();
                RokasBootstrap boot = root.AddComponent<RokasBootstrap>();
                boot.Initialize(saveDirectory);
                boot.SendMessage("OnApplicationFocus", true);
                yield return null;
                Assert.That(boot.Session.AcceptContract(), Is.True);
                Assert.That(boot.Session.LeaveHome(), Is.True);
                Assert.That(boot.Session.EnterReactiveDuelTestEncounter(), Is.True);
                yield return null;
                float deadline = Time.realtimeSinceStartup + 20f;
                while ((boot.ReactivePresentationHeld || !boot.View.ReactivePresentationReady) &&
                    Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(boot.ReactivePresentationHeld, Is.False);
                Assert.That(Find("ReactiveBasic").activeInHierarchy, Is.True);
                GameObject world = GameObject.Find("ReactiveCombatWorld");
                Assert.That(world, Is.Not.Null);
                ReactiveCombatActorVisual hunter = null;
                ReactiveCombatActorVisual enemy = null;
                foreach (ReactiveCombatActorVisual actor in world.GetComponentsInChildren<ReactiveCombatActorVisual>(true))
                {
                    if (actor.name == "CombatActor_Keiko") hunter = actor;
                    if (actor.name == "CombatActor_Yokai") enemy = actor;
                }
                Assert.That(hunter, Is.Not.Null);
                Assert.That(enemy, Is.Not.Null);
                var actualArena = NativeCombatFixtureObservables.Arena(boot);
                var normalProfile = hunter.AttackLicensedProfile(false);
                Vector3 hunterHome = hunter.transform.localPosition;
                Vector3 enemyHome = enemy.transform.localPosition;
                ReactiveCombatSession combat = boot.Session.ReactiveCombat;
                string enemyId = combat.ActiveEnemyIds[0];
                boot.SubmitReactiveCommand(CommandKind.Basic, null);
                Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.PlayerExecution));
                deadline = Time.realtimeSinceStartup + 12f;
                while (!(combat.Phase == ReactivePhase.EnemyExecution && boot.ReactivePresentationHeld) &&
                    Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.EnemyExecution));
                Assert.That(boot.ReactivePresentationHeld, Is.True,
                    "Core may select the next enemy immediately; presentation must hold its AttackStarted event.");
                long heldCombatUs = combat.CurrentCombatUs;
                long heldRevision = combat.Revision;
                bool sawHunterReturn = false;
                bool sawEnemyAnnouncement = false;
                bool defensePressSent = false;
                bool defensePressReleased = false;
                deadline = Time.realtimeSinceStartup + 8f;
                while (boot.ReactivePresentationHeld && Time.realtimeSinceStartup < deadline)
                {
                    NativeCombatFixtureObservables.AssertPassiveStageEnvelope(actualArena, world.transform,
                        hunter, normalProfile, combat.ActiveActorId);
                    Assert.That(enemy.CurrentPose, Is.Not.EqualTo("Approach"));
                    Assert.That(enemy.CurrentPose, Is.Not.EqualTo("Attack"));
                    Assert.That(enemy.CurrentPose, Is.Not.EqualTo("Heavy"));
                    AssertHeldHudHidden();
                    Assert.That(combat.CurrentCombatUs, Is.EqualTo(heldCombatUs));
                    Assert.That(combat.HunterHp, Is.EqualTo(100));
                    boot.SubmitReactiveCommand(CommandKind.Basic, null);
                    Assert.That(combat.Revision, Is.EqualTo(heldRevision));
                    if (actualArena.HunterMotionPhase == "Return")
                    {
                        sawHunterReturn = true;
                        if (!defensePressSent)
                        {
                            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q), InputState.currentTime);
                            InputSystem.Update();
                            defensePressSent = true;
                        }
                        else if (!defensePressReleased)
                        {
                            InputSystem.QueueStateEvent(keyboard, new KeyboardState(), InputState.currentTime);
                            InputSystem.Update();
                            defensePressReleased = true;
                        }
                    }
                    if (boot.View.ReactiveAnnouncementActive)
                    {
                        sawEnemyAnnouncement = true;
                        Assert.That(enemy.CurrentPose, Is.EqualTo("Idle"));
                        Assert.That(hunter.transform.localPosition, Is.EqualTo(hunterHome));
                        Assert.That(hunter.CurrentPose, Is.EqualTo("Idle"));
                        Assert.That(boot.View.HunterAtHome, Is.True);
                    }
                    yield return null;
                }
                Assert.That(sawHunterReturn, Is.True);
                Assert.That(defensePressReleased, Is.True, "A real Q press during the hold is discarded before the next actor starts.");
                Assert.That(sawEnemyAnnouncement, Is.True);
                Assert.That(boot.ReactivePresentationHeld, Is.False);
                Assert.That(boot.View.ReactiveAnnouncementActive, Is.False);
                Assert.That(hunter.transform.localPosition, Is.EqualTo(hunterHome));
                Assert.That(hunter.CurrentPose, Is.EqualTo("Idle"));
                Assert.That(Find("ReactiveContactTrack").activeInHierarchy, Is.True,
                    "Incoming timing is presented only after the hunter is home and the announcement has faded.");
                Assert.That(Find("ReactiveTarget1").activeInHierarchy, Is.False);

                deadline = Time.realtimeSinceStartup + 10f;
                while (!(combat.Phase == ReactivePhase.PlayerCommand && boot.ReactivePresentationHeld) &&
                    Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.PlayerCommand));
                Assert.That(boot.ReactivePresentationHeld, Is.True);
                Assert.That(combat.HunterHp, Is.GreaterThan(0), "The actual duel completes a cycle without forcing HP.");
                heldCombatUs = combat.CurrentCombatUs;
                heldRevision = combat.Revision;
                bool sawEnemyReturn = false;
                bool sawPlayerAnnouncement = false;
                float previousEnemyDistance = Vector3.Distance(enemy.transform.localPosition, enemyHome);
                deadline = Time.realtimeSinceStartup + 8f;
                while (boot.ReactivePresentationHeld && Time.realtimeSinceStartup < deadline)
                {
                    AssertHeldHudHidden();
                    Assert.That(combat.CurrentCombatUs, Is.EqualTo(heldCombatUs));
                    boot.SubmitReactiveCommand(CommandKind.Basic, null);
                    Assert.That(combat.Revision, Is.EqualTo(heldRevision),
                        "Core's next PlayerCommand phase must not accept a command while the enemy returns.");
                    Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.PlayerCommand));
                    float enemyDistance = Vector3.Distance(enemy.transform.localPosition, enemyHome);
                    sawEnemyReturn |= enemyDistance > .001f && enemyDistance < previousEnemyDistance &&
                        actualArena.EnemyMotionPhase(enemyId) == "Return";
                    previousEnemyDistance = enemyDistance;
                    if (boot.View.ReactiveAnnouncementActive)
                    {
                        sawPlayerAnnouncement = true;
                        Assert.That(enemy.transform.localPosition, Is.EqualTo(enemyHome));
                        Assert.That(enemy.CurrentPose, Is.EqualTo("Idle"));
                    }
                    yield return null;
                }
                Assert.That(sawEnemyReturn, Is.True);
                Assert.That(sawPlayerAnnouncement, Is.True);
                Assert.That(boot.ReactivePresentationHeld, Is.False);
                Assert.That(boot.View.ReactivePresentationReady, Is.True);
                Assert.That(boot.View.ReactiveAnnouncementActive, Is.False);
                Assert.That(enemy.transform.localPosition, Is.EqualTo(enemyHome));
                Assert.That(enemy.CurrentPose, Is.EqualTo("Idle"));
                Assert.That(Find("ReactiveBasic").activeInHierarchy, Is.True);
                Assert.That(Find("ReactiveHeavy").activeInHierarchy, Is.True);
                Assert.That(Find("ReactiveTarget1").activeInHierarchy, Is.True);
                Assert.That(Find("ReactiveContactTrack").activeInHierarchy, Is.False);
            }
            finally
            {
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.editorInputBehaviorInPlayMode = priorEditorInput;
                InputSystem.settings.backgroundBehavior = priorBackground;
                UnityEngine.Object.Destroy(root);
                root = null;
            }
        }

        private void AssertHeldHudHidden()
        {
            foreach (string objectName in new[] { "ReactiveBasic", "ReactiveHeavy", "ReactiveTarget1",
                "ReactiveContactTrack", "ReactiveDefense", "ReactiveDefenseHint" })
                Assert.That(Find(objectName).activeInHierarchy, Is.False, objectName + " must remain hidden during presentation holds.");
        }
    }
}
