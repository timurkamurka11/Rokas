using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Rokas.Core.ReactiveTurns;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rokas.Tests
{
    public sealed class CombatCinematicFeedbackRegressionTests
    {
        private GameObject root;
        private ReactiveMissionView view;
        private ReactiveCombatArena arena;
        private RectTransform arenaRect;
        private Image background;
        private const string HunterId = "P";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            root = new GameObject("CinematicFeedbackFixture", typeof(RectTransform));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
            var ui = new UiKit(assets, null);
            view = new ReactiveMissionView(ui, assets, () => { }, () => { }, () => { }, () => { },
                () => { }, () => { }, () => { }, () => { }, () => { }, () => { }, id => { });
            view.Build(root.GetComponent<RectTransform>());
            arena = (ReactiveCombatArena)typeof(ReactiveMissionView).GetField("arena", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
            arenaRect = root.transform.Find("ReactiveArena").GetComponent<RectTransform>();
            background = ui.Box(arenaRect, "IllustratedBackgroundFixture", 0f, 0f, 1920f, 906f, Color.gray);
            background.transform.SetAsFirstSibling();
            view.Refresh(Display());
            StepUntil(() => arena.PresentationReady, 35f);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            view?.ClearReferences();
            Object.Destroy(root);
            view = null;
            arena = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator PreviewDoesNotDarkenAndVeilIsClippedBelowTransparentActors()
        {
            Image veil = Veil();
            Transform clip = veil.transform.parent;
            Transform actors = arenaRect.Find("ReactiveAnimatedWorld");
            Assert.That(clip.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(clip.parent, Is.SameAs(actors.parent));
            Assert.That(clip.GetSiblingIndex(), Is.GreaterThan(background.transform.GetSiblingIndex()));
            Assert.That(clip.GetSiblingIndex(), Is.LessThan(actors.GetSiblingIndex()));
            Assert.That(veil.raycastTarget, Is.False);
            Assert.That(clip.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(1920f, 1080f)));
            Assert.That(veil.rectTransform.sizeDelta, Is.EqualTo(new Vector2(1920f, 1080f)));
            Assert.That(clip.GetComponent<RectTransform>().anchoredPosition, Is.EqualTo(new Vector2(0f, 100f)), "Veil covers the whole authored illustration, including its top 100 pixels");
            Assert.That(veil.color.a, Is.Zero);
            Assert.That(arena.SelectHunterPreview("normal"), Is.True);
            view.Tick(.3f);
            Assert.That(veil.color.a, Is.Zero, "Hover/preview cannot impersonate a confirmed source contact");
            arena.CancelHunterPreview();
            view.Tick(.3f);
            Assert.That(veil.color.a, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SurvivingNormalContactUsesExistingCameraEnvelopeAndLeavesNativeRecoveryRunning()
        {
            CombatEvent contact = NormalContact("normal-envelope");
            Assert.That(Veil().color.a, Is.Zero, "Survival is resolved before the first veil frame, not from a preview");
            view.Tick(0f);
            Assert.That(Veil().color.a, Is.EqualTo(.24f).Within(.00001f));
            double initialClock = PresentationClock();
            var hunter = Hunter();
            var native = hunter.ActiveLicensedProfile;
            Assert.That(hunter.LicensedContactConfirmed, Is.True);
            view.Tick(62f / 60f);
            Assert.That(arena.LicensedCameraClock, Is.EqualTo(62f / 60f).Within(.00001f));
            Assert.That(Veil().color.a, Is.EqualTo(.24f).Within(.00001f));
            view.Tick(8f / 60f);
            Assert.That(Veil().color.a, Is.EqualTo(.12f).Within(.00001f), "Zero-tangent Hermite has half weight at the authored fade midpoint");
            view.Tick(8f / 60f);
            Assert.That(Veil().color.a, Is.LessThan(.00001f));
            Assert.That(arena.LicensedCameraActive, Is.True, "The veil cannot cancel the longer 83/60 camera shot at its own 78/60 deadline");
            Assert.That(hunter.ActiveLicensedProfile, Is.SameAs(native));
            Assert.That(hunter.LicensedMotionClock, Is.EqualTo(78f / 60f).Within(.00001f));
            Assert.That(PresentationClock() - initialClock, Is.EqualTo(78f / 60f).Within(.00001));
            Assert.That(hunter.ActionRecoveryComplete, Is.False, "UI envelope end cannot truncate source performer recovery");
            view.Present(contact);
            Assert.That(Veil().color.a, Is.Zero, "Duplicate domain contact cannot rearm an expired focus envelope");
            yield return null;
        }

        [UnityTest]
        public IEnumerator CancelAndDeathClearAnActiveFocusBeforeTheNextFrame()
        {
            NormalContact("cancel-focus");
            view.Tick(.1f);
            Assert.That(Veil().color.a, Is.GreaterThan(0f));
            arena.CancelHunterMotion();
            Assert.That(Veil().color.a, Is.Zero);
            Assert.That(Veil().gameObject.activeSelf, Is.False);
            StepUntil(() => arena.PresentationReady, 15f);
            NormalContact("death-focus");
            view.Tick(.1f);
            Assert.That(Veil().color.a, Is.GreaterThan(0f));
            view.Refresh(Display(false));
            Assert.That(arena.IsCorpse("E1"), Is.True);
            Assert.That(Veil().color.a, Is.Zero, "A removed/dead target cannot retain the surviving-target BasicDamage adaptation");
            yield return null;
        }

        [UnityTest]
        public IEnumerator HeavyAoeAndRealGuardResolutionDoNotApplyBasicDamageFocus()
        {
            Assert.That(arena.StartHunterApproach("E1", true), Is.True);
            StepUntil(() => arena.HunterApproachComplete && Hunter().AwaitingAttackContact, 15f);
            view.Present(new CombatEvent(CombatEventKind.CommandCommitted, HunterId, actionId: "heavy-aoe", detail: "heavy"));
            view.Present(new CombatEvent(CombatEventKind.HitResolved, HunterId, "E1", "heavy-aoe", amount: 10));
            view.Tick(0f);
            Assert.That(Veil().color.a, Is.Zero);
            arena.CancelHunterMotion();
            StepUntil(() => arena.PresentationReady, 15f);
            view.Present(new CombatEvent(CombatEventKind.AttackStarted, "E1", actionId: "guard-source", detail: "normal"));
            var enemy = Enemy();
            StepUntil(() => enemy.AwaitingAttackContact, 15f);
            arena.BeginDefense(false, .1f);
            view.Present(new CombatEvent(CombatEventKind.HitResolved, "E1", HunterId, "guard-source", amount: 0, detail: "Parry"));
            view.Tick(0f);
            Assert.That(Veil().color.a, Is.Zero);
            Assert.That(Feedback().text, Is.EqualTo("БЛОК"), "Existing defense feedback survives the cinematic exclusion");
            Assert.That(Feedback().gameObject.activeInHierarchy, Is.True, "Resolved Guard feedback must survive entry HUD hiding");
            Assert.That(Feedback().color.a, Is.GreaterThan(0f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DamageProjectionReadsTheActualCameraAndDuplicateContactDoesNotRestartFeedback()
        {
            CombatEvent contact = NormalContact("projected-damage");
            view.Tick(0f);
            Assert.That(Feedback().gameObject.activeInHierarchy, Is.True, "Entry HUD hiding cannot leave actual damage feedback disabled");
            Assert.That(Feedback().color.a, Is.GreaterThan(0f));
            Camera camera = rootSceneCamera();
            double beforeRenderClock = arena.PresentationClock;
            float beforeRenderCameraClock = arena.LicensedCameraClock;
            camera.Render();
            Assert.That(arena.PresentationClock, Is.EqualTo(beforeRenderClock));
            Assert.That(arena.LicensedCameraClock, Is.EqualTo(beforeRenderCameraClock));
            Vector3 cameraPosition = camera.transform.position;
            Vector3 actorPosition = Enemy().transform.position;
            float sourceClock = arena.LicensedCameraClock;
            double uiClock = PresentationClock();
            var method = typeof(ReactiveCombatArena).GetMethod("TryActorFeedbackAnchor");
            Assert.That(method, Is.Not.Null);
            var arguments = new object[] { "E1", Vector2.zero };
            Assert.That(method.Invoke(arena, arguments), Is.True);
            Vector2 anchor = (Vector2)arguments[1];
            Vector3 top = Enemy().BodyBounds.center;
            top.y = Enemy().BodyBounds.max.y;
            Vector3 viewport = camera.WorldToViewportPoint(top);
            var actorImage = arenaRect.Find("ReactiveAnimatedWorld").GetComponent<RawImage>();
            RectTransform actorRect = actorImage.rectTransform;
            Assert.That(anchor.x, Is.EqualTo(actorRect.anchoredPosition.x + viewport.x * actorRect.rect.width).Within(.02f));
            Assert.That(anchor.y, Is.EqualTo(actorRect.anchoredPosition.y - (1f - viewport.y) * actorRect.rect.height).Within(.02f));
            Assert.That(camera.transform.position, Is.EqualTo(cameraPosition));
            Assert.That(Enemy().transform.position, Is.EqualTo(actorPosition));
            Assert.That(arena.LicensedCameraClock, Is.EqualTo(sourceClock));
            Assert.That(PresentationClock(), Is.EqualTo(uiClock));
            Assert.That(Feedback().rectTransform.pivot, Is.EqualTo(new Vector2(.5f, .5f)));
            Assert.That(Feedback().alignment, Is.EqualTo(TextAnchor.MiddleCenter));
            Vector2 expectedPosition = anchor + new Vector2(0f, 12f);
            Rect displayedViewport = arena.ActorFeedbackViewport;
            expectedPosition.x = Mathf.Clamp(expectedPosition.x, displayedViewport.xMin + Feedback().rectTransform.rect.width * .5f, displayedViewport.xMax - Feedback().rectTransform.rect.width * .5f);
            expectedPosition.y = Mathf.Clamp(expectedPosition.y, displayedViewport.yMin + Feedback().rectTransform.rect.height * .5f, displayedViewport.yMax - Feedback().rectTransform.rect.height * .5f);
            Assert.That(Vector2.Distance(Feedback().rectTransform.anchoredPosition, expectedPosition), Is.LessThan(.02f), "Actual actor render may refresh skinned bounds; UI projection must follow without a second combat tick");
            view.Tick(.1f);
            view.Present(contact);
            view.Tick(.36f);
            Assert.That(Feedback().color.a, Is.Zero, "Duplicate HitResolved must not extend the existing visible damage lifetime");
            Assert.That(Feedback().gameObject.activeSelf, Is.False, "Expired feedback must hide without waiting for a layout refresh");
            Assert.That(arena.LicensedCameraClock, Is.EqualTo(.46f).Within(.00001f), "UI reads cannot independently advance the shot clock");
            yield return null;
        }

        [UnityTest]
        public IEnumerator FullActorGateRevealsTheFormerTopBorderWithoutChangingExistingPixelComposition()
        {
            Camera actual = rootSceneCamera();
            RawImage displayed = arenaRect.Find("ReactiveAnimatedWorld").GetComponent<RawImage>();
            Assert.That(displayed.rectTransform.sizeDelta, Is.EqualTo(new Vector2(1920f, 1080f)));
            Assert.That(displayed.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(0f, 100f)));
            Assert.That(actual.targetTexture.width, Is.EqualTo(1920));
            Assert.That(actual.targetTexture.height, Is.EqualTo(1080));
            var referenceObject = new GameObject("FormerActorGateProjectionReference", typeof(Camera));
            Camera reference = referenceObject.GetComponent<Camera>();
            reference.enabled = false;
            try
            {
                actual.Render();
                Matrix4x4 homeProjection = actual.projectionMatrix;
                for (int phase = 0; phase < 2; phase++)
                {
                    if (phase == 1) { NormalContact("full-gate-contact"); view.Tick(.1f); }
                    Vector3 position = actual.transform.position;
                    Quaternion rotation = actual.transform.rotation;
                    float lens = actual.fieldOfView;
                    double presentationTime = arena.PresentationClock;
                    float sourceTime = arena.LicensedCameraClock;
                    reference.CopyFrom(actual);
                    reference.enabled = false;
                    reference.targetTexture = null;
                    reference.transform.SetPositionAndRotation(position, rotation);
                    reference.aspect = 1920f / 906f;
                    reference.ResetProjectionMatrix();
                    Vector3[] points = {
                        reference.ViewportToWorldPoint(new Vector3(.25f, .25f, 10f)),
                        reference.ViewportToWorldPoint(new Vector3(.5f, .5f, 10f)),
                        reference.ViewportToWorldPoint(new Vector3(.7f, 1.04f, 10f))
                    };
                    actual.Render();
                    foreach (Vector3 point in points)
                    {
                        Vector3 oldView = reference.WorldToViewportPoint(point);
                        Vector3 newView = actual.WorldToViewportPoint(point);
                        Vector2 formerStagePixel = new Vector2(oldView.x * 1920f, 100f + (1f - oldView.y) * 906f);
                        Vector2 actualStagePixel = new Vector2(newView.x * 1920f, (1f - newView.y) * 1080f);
                        Assert.That(Vector2.Distance(actualStagePixel, formerStagePixel), Is.LessThan(.02f),
                            "Expanding the render gate must preserve the existing physical pixel scale and composition.");
                        Assert.That(newView.y, Is.InRange(0f, 1f),
                            "The former clipped top-border point must be inside the actual rendered frustum.");
                    }
                    Assert.That(actual.transform.position, Is.EqualTo(position));
                    Assert.That(actual.transform.rotation, Is.EqualTo(rotation));
                    Assert.That(actual.fieldOfView, Is.EqualTo(lens));
                    Assert.That(arena.PresentationClock, Is.EqualTo(presentationTime));
                    Assert.That(arena.LicensedCameraClock, Is.EqualTo(sourceTime));
                }
                arena.CancelHunterMotion();
                StepUntil(() => arena.PresentationReady && arena.CameraAtHome, 15f);
                actual.Render();
                for (int row = 0; row < 4; row++)
                    for (int column = 0; column < 4; column++)
                        Assert.That(actual.projectionMatrix[row, column], Is.EqualTo(homeProjection[row, column]).Within(.00001f),
                            "Cancellation must restore the actual rendered tactical projection, not just its FOV field.");
                yield return null;
            }
            finally { Object.Destroy(referenceObject); }
        }

        private CombatEvent NormalContact(string id)
        {
            Assert.That(arena.StartHunterApproach("E1", false), Is.True);
            StepUntil(() => arena.HunterApproachComplete && Hunter().AwaitingAttackContact, 15f);
            Assert.That(Veil().color.a, Is.Zero);
            view.Present(new CombatEvent(CombatEventKind.CommandCommitted, HunterId, actionId: id, detail: "Basic"));
            var contact = new CombatEvent(CombatEventKind.HitResolved, HunterId, "E1", id, amount: 10, detail: "Basic");
            view.Present(contact);
            Assert.That(Feedback().gameObject.activeInHierarchy, Is.True,
                "BeginHitFeedback must activate accepted damage immediately, before the next Tick or Refresh");
            Assert.That(Feedback().color.a, Is.GreaterThan(0f));
            view.Refresh(Display());
            return contact;
        }

        private void StepUntil(Func<bool> completed, float maximumSeconds)
        {
            for (int index = 0; index < maximumSeconds * 60f && !completed(); index++)
            { view.Tick(1f / 60f); view.Refresh(Display()); }
            Assert.That(completed(), Is.True, "Real arena presentation did not reach the requested contact/readiness boundary");
        }

        private ReactiveBattleDisplay Display(bool alive = true) => new ReactiveBattleDisplay {
            HunterHp = 100, HunterAp = 6, Phase = ReactiveDisplayPhase.Command, Wave = 1, WaveCount = 1,
            SelectedTargetId = alive ? "E1" : null, WaveEnemyIds = new[] { "E1" }, WaveSlotCount = 1,
            Enemies = alive ? new[] { new ReactiveEnemyDisplay { Id = "E1", Name = "Target", Hp = 100, MaxHp = 100 } } : Array.Empty<ReactiveEnemyDisplay>()
        };
        private Image Veil()
        {
            Transform found = arenaRect.Find("ReactiveCinematicVeilClip/ReactiveCinematicVeil");
            Assert.That(found, Is.Not.Null, "The scoped background-only veil is missing");
            return found.GetComponent<Image>();
        }
        private Text Feedback() => arenaRect.Find("ReactiveHitFeedback").GetComponent<Text>();
        private double PresentationClock()
        {
            var property = typeof(ReactiveCombatArena).GetProperty("PresentationClock");
            Assert.That(property, Is.Not.Null);
            return (double)property.GetValue(arena);
        }
        private ReactiveCombatActorVisual Hunter() => (ReactiveCombatActorVisual)typeof(ReactiveCombatArena).GetField("hunter", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(arena);
        private ReactiveCombatActorVisual Enemy()
        {
            var field = typeof(ReactiveCombatArena).GetField("enemies", BindingFlags.Instance | BindingFlags.NonPublic);
            return ((System.Collections.Generic.Dictionary<string, ReactiveCombatActorVisual>)field.GetValue(arena))["E1"];
        }
        private Camera rootSceneCamera() => (Camera)typeof(ReactiveCombatArena).GetField("camera", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(arena);
    }
}
