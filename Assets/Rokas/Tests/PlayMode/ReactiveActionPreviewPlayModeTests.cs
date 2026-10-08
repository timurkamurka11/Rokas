using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Core.ReactiveTurns;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class ReactiveActionPreviewPlayModeTests
    {
        private GameObject root;
        private string profile;
        private RokasBootstrap boot;
        private Vector3 tacticalPosition;
        private Quaternion tacticalRotation;
        private bool tacticalOrthographic;
        private float tacticalSize, tacticalFov;
        private Matrix4x4 tacticalProjection;
        private GameObject equippedSword;

        [UnityTest]
        public IEnumerator ThreePreviewsThrowFlightContactAndRecoveryAreRenderedWithoutEarlyDamage()
        {
            yield return EnterBattle();
            var combat = boot.Session.ReactiveCombat;
            var actor = GameObject.Find("CombatActor_Keiko").GetComponent<ReactiveCombatActorVisual>();
            Vector3 home = actor.transform.localPosition;
            int ap = combat.HunterAp, hp = combat.GetActorState("E1").Hp;
            long revision = combat.Revision;
            Capture("base-battle");
            foreach (string button in new[] { "ReactiveBasic", "ReactiveHeavy", "ReactiveThrow" })
            {
                Click(button);
                float previewDeadline = Time.realtimeSinceStartup + .6f;
                while (Time.realtimeSinceStartup < previewDeadline) yield return null;
                float resumeDeadline = Time.realtimeSinceStartup + 4f;
                // GPU readback/PNG encoding can trip the existing frame-gap safety.
                // Wait for its actual resume lifecycle; never bypass the production clock.
                float readyPoseSeconds = button == "ReactiveBasic" ? .3f : button == "ReactiveHeavy" ? 56f / 120f : .4f;
                while ((combat.Phase == ReactivePhase.Suspended || actor.CurrentPoseSeconds < readyPoseSeconds - .002f) &&
                    Time.realtimeSinceStartup < resumeDeadline) yield return null;
                Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.PlayerCommand));
                Assert.That(combat.HunterAp, Is.EqualTo(ap));
                Assert.That(combat.GetActorState("E1").Hp, Is.EqualTo(hp));
                Assert.That(combat.Revision, Is.EqualTo(revision));
                Assert.That(actor.transform.localPosition, Is.EqualTo(home));
                Assert.That(actor.CurrentPose, Is.EqualTo(button == "ReactiveBasic" ? "Preparation" :
                    button == "ReactiveHeavy" ? "HeavyPreparation" : "ThrowPreparation"));
                Capture(button + "-preview");
            }
            Assert.That(actor.HeldDagger.gameObject.activeInHierarchy, Is.True);
            ReactiveCombatArena liveArena = FindLiveArena();
            int releasesBefore = liveArena.ThrowReleaseCount, contactsBefore = liveArena.ThrowContactCount;
            bool nativeThrow = actor.ActiveLicensedProfile != null;
            long acceptedThrowStartUs = -1;
            float maximumUnconfirmedThrowClock = 0f;
            Click("ReactiveThrow");
            float deadline = Time.realtimeSinceStartup + 8f, nextCapture = 0f;
            int frame = 0;
            bool sawFlight = false;
            while (combat.GetActorState("E1").Hp == hp && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(actor.transform.localPosition, Is.EqualTo(home), "Throw has no melee approach.");
                if (combat.Phase == ReactivePhase.PlayerExecution &&
                    combat.CurrentPlayerSkillId == ReactiveEightEnemyDefinitions.ThrowId && acceptedThrowStartUs < 0)
                    acceptedThrowStartUs = combat.CurrentActionStartUs;
                if (actor.CurrentPose == "Throw" && (!nativeThrow || !actor.LicensedContactConfirmed))
                    maximumUnconfirmedThrowClock = Mathf.Max(maximumUnconfirmedThrowClock, actor.CurrentPoseSeconds);
                if (nativeThrow) Assert.That(actor.LicensedContactConfirmed, Is.False,
                    "An actual flying projectile and unchanged HP precede the authoritative source contact.");
                Assert.That(liveArena.ThrowContactCount, Is.EqualTo(contactsBefore));
                GameObject projectile = GameObject.Find("KeikoThrownDagger");
                if (projectile != null)
                {
                    sawFlight = true;
                    Assert.That(actor.HeldDagger.gameObject.activeInHierarchy, Is.False, "One weapon leaves the hand.");
                    Assert.That(liveArena.ThrowReleaseCount, Is.EqualTo(releasesBefore + 1));
                    Assert.That(actor.CurrentPoseSeconds, Is.GreaterThanOrEqualTo(actor.ThrowReleaseSeconds),
                        "The observed projectile cannot exist before the actual visual release.");
                }
                if (actor.CurrentPose == "Throw" && Time.realtimeSinceStartup >= nextCapture)
                {
                    Capture("throw-motion-" + (frame++).ToString("D3"), 960, 540);
                    nextCapture = Time.realtimeSinceStartup + .025f;
                }
                yield return null;
            }
            Assert.That(sawFlight, Is.True);
            Assert.That(combat.GetActorState("E1").Hp, Is.LessThan(hp));
            Assert.That(acceptedThrowStartUs, Is.GreaterThanOrEqualTo(0), "Observe the actual accepted Core Throw.");
            // The existing Core hit is at +200000us, delivered through Bootstrap’s 40000us watermark.
            // +400000us is the action end and is not the physical contact deadline.
            Assert.That(combat.CurrentCombatUs - acceptedThrowStartUs, Is.GreaterThanOrEqualTo(240000),
                "Core damage waits for the authored hit plus its delivery watermark.");
            Assert.That(maximumUnconfirmedThrowClock, Is.GreaterThanOrEqualTo(actor.ThrowContactSeconds - .04f),
                "Before HP changes, the sampled visual clock must traverse release and physical flight.");
            Assert.That(liveArena.ThrowReleaseCount, Is.EqualTo(releasesBefore + 1));
            Assert.That(liveArena.ThrowContactCount, Is.EqualTo(contactsBefore + 1));
            if (nativeThrow) Assert.That(actor.LicensedContactConfirmed, Is.True,
                "Confirmed Native action starts its source clock at zero after the one physical contact.");
            else Assert.That(actor.CurrentPoseSeconds, Is.GreaterThanOrEqualTo(actor.ThrowContactSeconds - .04f),
                "Legacy Throw retains its continuous visual contact clock.");
            Assert.That(combat.HunterAp, Is.EqualTo(ap - 2));
            Capture("throw-impact");
            // The first contact sample is inside hit-stop. Capture the early burst
            // as well, while the actual presentation clock controls its lifetime.
            float contactAge = actor.CurrentPoseSeconds;
            deadline = Time.realtimeSinceStartup + 3f;
            while (actor.CurrentPose == "Throw" && actor.CurrentPoseSeconds < contactAge + .06f &&
                Time.realtimeSinceStartup < deadline) yield return null;
            Capture("throw-impact-burst");
            deadline = Time.realtimeSinceStartup + 4f;
            while ((!actor.IdleSettled || !boot.View.HunterAtHome) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(actor.IdleSettled && boot.View.HunterAtHome, Is.True);
            Assert.That(actor.transform.localPosition, Is.EqualTo(home));
            Assert.That(actor.HeldDagger.gameObject.activeInHierarchy, Is.False);
            AssertPersistentSword(actor);
            Capture("throw-recovery");
        }

        [UnityTest]
        public IEnumerator FocusLossCancelsPendingConfirmationWithoutSpendingAp()
        {
            yield return EnterBattle();
            var combat = boot.Session.ReactiveCombat;
            int ap = combat.HunterAp, hp = combat.GetActorState("E1").Hp;
            Click("ReactiveBasic");
            yield return null;
            Click("ReactiveBasic");
            boot.SendMessage("OnApplicationFocus", false);
            Assert.That(boot.ReactiveSelectedAction, Is.Null);
            boot.SendMessage("OnApplicationFocus", true);
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.PlayerCommand));
            Assert.That(combat.HunterAp, Is.EqualTo(ap));
            Assert.That(combat.GetActorState("E1").Hp, Is.EqualTo(hp));
        }

        [UnityTest]
        public IEnumerator FrameGapDuringCameraConfirmationSuspendsBeforeThrowCommitAndResumesAtClockTime()
        {
            yield return EnterBattle();
            ReactiveCombatSession combat = boot.Session.ReactiveCombat;
            int ap = combat.HunterAp, hp = combat.GetActorState("E1").Hp;
            long revision = combat.Revision;
            Assert.That(ap, Is.GreaterThanOrEqualTo(2));
            Click("ReactiveThrow");
            Click("ReactiveThrow");
            Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.PlayerCommand));

            // Time advances while Bootstrap's Core tick is held. The camera still
            // completes its real presentation lifecycle before the next Core tick.
            boot.enabled = false;
            yield return new WaitForSecondsRealtime(.2f);
            for (int i = 0; i < 20 && !boot.View.ReactivePreviewConfirmed; i++)
                boot.View.Tick(.02f);
            Assert.That(boot.View.ReactivePreviewConfirmed, Is.True);
            MethodInfo tick = typeof(RokasBootstrap).GetMethod("TickReactiveCombat",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(tick, Is.Not.Null);
            tick.Invoke(boot, null);

            Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.Suspended),
                "A frame gap must suspend the pending preview before Core accepts its command.");
            Assert.That(boot.ReactiveSelectedAction, Is.EqualTo("throw_blade"));
            Assert.That(combat.HunterAp, Is.EqualTo(ap));
            Assert.That(combat.GetActorState("E1").Hp, Is.EqualTo(hp));
            Assert.That(combat.Revision, Is.EqualTo(revision));

            boot.enabled = true;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (combat.Phase == ReactivePhase.Suspended && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.PlayerExecution));
            Assert.That(combat.HunterAp, Is.EqualTo(ap - 2));
            Assert.That(combat.GetActorState("E1").Hp, Is.EqualTo(hp));
            Assert.That(combat.CurrentActionStartUs, Is.EqualTo(combat.CurrentCombatUs),
                "The resumed clock must reach Core before the committed action starts.");
        }

        private void Capture(string name, int width = 1920, int height = 1080)
        {
            string folder = Path.Combine(Path.GetTempPath(), "rokas-final-vfx-visuals");
            Directory.CreateDirectory(folder);
            Canvas canvas = root.GetComponentInChildren<Canvas>();
            var actor = GameObject.Find("CombatActor_Keiko").GetComponent<ReactiveCombatActorVisual>();
            TestContext.WriteLine(name + " pose=" + actor.CurrentPose + " seconds=" + actor.CurrentPoseSeconds +
                " camera=" + GameObject.Find("ReactiveActorCamera").GetComponent<Camera>().orthographicSize +
                " hand=" + actor.WeaponAttachment.Socket.parent.position + " phase=" + boot.Session.ReactiveCombat.Phase);
            RectTransform stage = Find("AuthoredStage").GetComponent<RectTransform>();
            GameObject.Find("ReactiveActorCamera").GetComponent<Camera>().Render();
            var obj = new GameObject("FinalVfxCaptureCamera", typeof(Camera));
            var camera = obj.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            var render = new RenderTexture(width, height, 24);
            render.Create(); camera.targetTexture = render;
            RenderTexture priorRender = RenderTexture.active;
            RenderMode priorMode = canvas.renderMode;
            Camera priorCamera = canvas.worldCamera;
            float priorDistance = canvas.planeDistance;
            Vector3 priorScale = stage.localScale;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f;
                stage.localScale = Vector3.one * (width / 1920f);
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = render;
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                try { texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                    File.WriteAllBytes(Path.Combine(folder, name + ".png"), texture.EncodeToPNG()); }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
            finally
            {
                canvas.renderMode = priorMode; canvas.worldCamera = priorCamera; canvas.planeDistance = priorDistance;
                stage.localScale = priorScale; RenderTexture.active = priorRender; camera.targetTexture = null;
                render.Release(); UnityEngine.Object.DestroyImmediate(render); UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        private IEnumerator EnterBattle()
        {
            profile = Path.Combine(Path.GetTempPath(), "rokas-action-preview-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("ReactiveActionPreviewFixture");
            boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(profile);
            boot.SendMessage("OnApplicationFocus", true);
            yield return null;
            Assert.That(boot.Session.AcceptContract() && boot.Session.LeaveHome(), Is.True);
            yield return null;
            Click("EnterReactivePortal");
            float deadline = Time.realtimeSinceStartup + 35f;
            while ((boot.Session.ReactiveCombat == null || !Ready("ReactiveBasic")) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Ready("ReactiveBasic"), Is.True, "Entrance/announcement must finish before selection.");
            RememberTacticalCamera();
        }

        [UnityTest]
        public IEnumerator SwitchingAndCancelPreserveAuthorityAndRestoreCamera()
        {
            yield return EnterBattle();
            var combat = boot.Session.ReactiveCombat;
            int ap = combat.HunterAp;
            int hp = combat.GetActorState("E1").Hp;
            long revision = combat.Revision;
            var actor = GameObject.Find("CombatActor_Keiko").GetComponent<ReactiveCombatActorVisual>();
            var camera = GameObject.Find("ReactiveActorCamera").GetComponent<Camera>();
            Vector3 home = actor.transform.localPosition;
            var pointer = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(Find("ReactiveBasic"), pointer, ExecuteEvents.pointerEnterHandler);
            Assert.That(boot.ReactiveSelectedAction, Is.Null, "Hover never selects a command.");
            Click("ReactiveBasic");
            yield return null;
            Click("ReactiveHeavy");
            float deadline = Time.realtimeSinceStartup + 1.2f;
            while (Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(boot.ReactiveSelectedAction, Is.EqualTo("heavy"));
            Assert.That(actor.CurrentPose, Is.EqualTo("HeavyPreparation"));
            Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.PlayerCommand));
            Assert.That(combat.Revision, Is.EqualTo(revision));
            Assert.That(combat.HunterAp, Is.EqualTo(ap));
            Assert.That(combat.GetActorState("E1").Hp, Is.EqualTo(hp));
            Assert.That(actor.transform.localPosition, Is.EqualTo(home));
            Assert.That(boot.CancelReactivePreview(), Is.True);
            deadline = Time.realtimeSinceStartup + 1f;
            while ((!Ready("ReactiveBasic") || !CameraIsTacticalHome(camera)) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(boot.ReactiveSelectedAction, Is.Null);
            Assert.That(actor.CurrentPose, Is.EqualTo("Idle"));
            AssertTacticalCameraHome(camera);
            Assert.That(Ready("ReactiveBasic"), Is.True);
            Assert.That(combat.HunterAp, Is.EqualTo(ap));
            Assert.That(combat.Revision, Is.EqualTo(revision));
            foreach (string nextButton in new[] { "ReactiveHeavy", "ReactiveBasic" })
            {
                Click("ReactiveThrow");
                yield return null;
                Assert.That(actor.HeldDagger.gameObject.activeInHierarchy, Is.True);
                Click(nextButton);
                deadline = Time.realtimeSinceStartup + 2f;
                while ((ThrowFxActive() || combat.Phase == ReactivePhase.Suspended) &&
                    Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(boot.ReactiveSelectedAction, Is.EqualTo(nextButton == "ReactiveHeavy" ? "heavy" : "normal"));
                Assert.That(actor.HeldDagger.gameObject.activeInHierarchy, Is.False);
                AssertPersistentSword(actor);
                Assert.That(ThrowFxActive(), Is.False, "Switching away from Throw retires its charge and particles.");
                Assert.That(GameObject.Find("KeikoThrownDagger"), Is.Null);
                Assert.That(combat.Revision, Is.EqualTo(revision));
                Assert.That(combat.HunterAp, Is.EqualTo(ap));
                Assert.That(combat.GetActorState("E1").Hp, Is.EqualTo(hp));
            }
            Assert.That(boot.CancelReactivePreview(), Is.True);
            deadline = Time.realtimeSinceStartup + 2f;
            while (!Ready("ReactiveThrow") && Time.realtimeSinceStartup < deadline) yield return null;
            Click("ReactiveThrow");
            yield return null;
            Assert.That(boot.CancelReactivePreview(), Is.True);
            deadline = Time.realtimeSinceStartup + 2f;
            while ((!Ready("ReactiveThrow") || ThrowFxActive()) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Ready("ReactiveThrow") && !ThrowFxActive(), Is.True);
            Assert.That(actor.CurrentPose, Is.EqualTo("Idle"));
            AssertTacticalCameraHome(camera);
            Assert.That(combat.HunterAp, Is.EqualTo(ap));
            Assert.That(combat.Revision, Is.EqualTo(revision));
            Click("ReactiveThrow"); Click("ReactiveThrow");
            deadline = Time.realtimeSinceStartup + 5f;
            while (GameObject.Find("KeikoThrownDagger") == null && Time.realtimeSinceStartup < deadline) yield return null;
            int activeProjectiles = 0;
            foreach (Transform item in GameObject.Find("ReactiveCombatWorld").GetComponentsInChildren<Transform>(true))
                if (item.name == "KeikoThrownDagger" && item.gameObject.activeInHierarchy) activeProjectiles++;
            Assert.That(activeProjectiles, Is.EqualTo(1), "Throw after cancel releases one clean projectile.");
            Assert.That(actor.HeldDagger.gameObject.activeInHierarchy, Is.False);
            while ((combat.GetActorState("E1").Hp == hp || !actor.IdleSettled || ThrowFxActive()) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(combat.GetActorState("E1").Hp, Is.EqualTo(hp - 20));
            Assert.That(combat.HunterAp, Is.EqualTo(ap - 2));
            Assert.That(actor.IdleSettled && !ThrowFxActive(), Is.True);
            Assert.That(actor.transform.localPosition, Is.EqualTo(home));
        }

        private static bool ThrowFxActive()
        {
            GameObject world = GameObject.Find("ReactiveCombatWorld");
            foreach (var line in world.GetComponentsInChildren<LineRenderer>(true))
                if (line.name.StartsWith("Throw", StringComparison.Ordinal) && line.enabled) return true;
            foreach (var particles in world.GetComponentsInChildren<ParticleSystem>(true))
                if (particles.name == "ThrowReleaseAndFlightMotes" && particles.particleCount > 0) return true;
            return false;
        }

        [UnityTest]
        public IEnumerator SecondClickWaitsForCameraThenExecutesOnlyOnce()
        {
            yield return EnterBattle();
            var combat = boot.Session.ReactiveCombat;
            var actor = GameObject.Find("CombatActor_Keiko").GetComponent<ReactiveCombatActorVisual>();
            var camera = GameObject.Find("ReactiveActorCamera").GetComponent<Camera>();
            Vector3 home = actor.transform.localPosition;
            Click("ReactiveBasic");
            float deadline = Time.realtimeSinceStartup + .6f;
            while (Time.realtimeSinceStartup < deadline) yield return null;
            Click("ReactiveBasic");
            Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.PlayerCommand), "Confirm first restores camera.");
            deadline = Time.realtimeSinceStartup + 2f;
            while (combat.Phase == ReactivePhase.PlayerCommand && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(actor.transform.localPosition, Is.EqualTo(home), "No approach before camera restore.");
                yield return null;
            }
            Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.PlayerExecution));
            AssertTacticalCameraHome(camera);
            Assert.That(boot.ReactiveSelectedAction, Is.Null);
            Assert.That(Ready("ReactiveBasic"), Is.False, "Repeated UI clicks cannot dispatch a second attack.");
        }

        [UnityTest]
        public IEnumerator FirstClickNormalKeepsTurnApHpAndSlotWhileShowingPersistentPreview()
        {
            profile = Path.Combine(Path.GetTempPath(), "rokas-action-preview-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("ReactiveActionPreviewFixture");
            boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(profile);
            boot.SendMessage("OnApplicationFocus", true);
            yield return null;
            Assert.That(boot.Session.AcceptContract() && boot.Session.LeaveHome(), Is.True);
            yield return null;
            Click("EnterReactivePortal");
            float deadline = Time.realtimeSinceStartup + 35f;
            while ((boot.Session.ReactiveCombat == null || !Ready("ReactiveBasic")) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Ready("ReactiveBasic"), Is.True, "Entrance/announcement must finish before selection.");
            RememberTacticalCamera();
            ReactiveCombatSession combat = boot.Session.ReactiveCombat;
            int ap = combat.HunterAp;
            int hp = combat.GetActorState("E1").Hp;
            long revision = combat.Revision;
            var actor = GameObject.Find("CombatActor_Keiko").GetComponent<ReactiveCombatActorVisual>();
            Vector3 home = actor.transform.localPosition;
            Click("ReactiveBasic");
            Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.PlayerCommand), "First click is preview, not command execution.");
            float previewDeadline = Time.realtimeSinceStartup + 1.2f;
            while (Time.realtimeSinceStartup < previewDeadline) yield return null;
            Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.PlayerCommand));
            Assert.That(combat.Revision, Is.EqualTo(revision));
            Assert.That(combat.HunterAp, Is.EqualTo(ap));
            Assert.That(combat.GetActorState("E1").Hp, Is.EqualTo(hp));
            Assert.That(actor.transform.localPosition, Is.EqualTo(home));
            Assert.That(actor.CurrentPose, Is.EqualTo("Preparation"));
            var camera = GameObject.Find("ReactiveActorCamera").GetComponent<Camera>();
            if (actor.DefaultLicensedProfile == null) Assert.That(camera.orthographicSize, Is.LessThan(4.6f));
            else AssertTacticalCameraHome(camera);
            Assert.That(Ready("ReactiveBasic"), Is.True, "The selected command must stay available for confirmation.");
        }

        private void RememberTacticalCamera()
        {
            Camera camera = GameObject.Find("ReactiveActorCamera").GetComponent<Camera>();
            tacticalPosition = camera.transform.localPosition; tacticalRotation = camera.transform.localRotation;
            tacticalOrthographic = camera.orthographic; tacticalSize = camera.orthographicSize;
            tacticalFov = camera.fieldOfView; tacticalProjection = camera.projectionMatrix;
            ReactiveCombatActorVisual hunter = GameObject.Find("CombatActor_Keiko").GetComponent<ReactiveCombatActorVisual>();
            equippedSword = hunter.WeaponAttachment.CurrentWeapon;
            if (hunter.DefaultLicensedProfile != null)
            {
                Assert.That(camera.orthographic, Is.False);
                Assert.That(camera.fieldOfView, Is.EqualTo(hunter.DefaultLicensedProfile.camera.baseFov));
            }
            else
            {
                Assert.That(camera.orthographic, Is.True);
                Assert.That(camera.orthographicSize, Is.EqualTo(4.6f).Within(.001f));
            }
        }

        private bool CameraIsTacticalHome(Camera camera)
        {
            if (camera.orthographic != tacticalOrthographic ||
                Vector3.Distance(camera.transform.localPosition, tacticalPosition) > .00001f ||
                Quaternion.Angle(camera.transform.localRotation, tacticalRotation) > .001f ||
                Mathf.Abs(camera.orthographicSize - tacticalSize) > .001f ||
                Mathf.Abs(camera.fieldOfView - tacticalFov) > .0001f) return false;
            for (int i = 0; i < 16; i++) if (Mathf.Abs(camera.projectionMatrix[i] - tacticalProjection[i]) > .00001f) return false;
            return true;
        }

        private void AssertTacticalCameraHome(Camera camera)
        {
            Assert.That(CameraIsTacticalHome(camera), Is.True,
                "Restore the captured tactical position, rotation, projection and FOV exactly.");
        }

        private void AssertPersistentSword(ReactiveCombatActorVisual actor)
        {
            Assert.That(actor.WeaponAttachment.CurrentWeapon, Is.SameAs(equippedSword));
            Assert.That(equippedSword.transform.IsChildOf(actor.ModelRoot), Is.True);
            if (actor.DefaultLicensedProfile == null)
                Assert.That(equippedSword.transform.parent, Is.EqualTo(actor.WeaponAttachment.Socket));
        }

        private ReactiveCombatArena FindLiveArena()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            object mission = typeof(RokasView).GetField("mission", flags).GetValue(boot.View);
            object reactive = mission.GetType().GetField("reactiveView", flags).GetValue(mission);
            var arena = (ReactiveCombatArena)reactive.GetType().GetField("arena", flags).GetValue(reactive);
            Assert.That(arena, Is.Not.Null);
            return arena;
        }

        private GameObject Find(string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child.gameObject;
            return null;
        }
        private bool Ready(string name)
        {
            Button button = Find(name)?.GetComponent<Button>();
            return button != null && button.gameObject.activeInHierarchy && button.IsInteractable();
        }
        private void Click(string name)
        {
            Assert.That(Ready(name), Is.True, name);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            Assert.That(ExecuteEvents.Execute(Find(name), pointer, ExecuteEvents.pointerClickHandler), Is.True);
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root) UnityEngine.Object.Destroy(root);
            yield return null;
            if (profile != null && Directory.Exists(profile)) Directory.Delete(profile, true);
        }
    }
}
