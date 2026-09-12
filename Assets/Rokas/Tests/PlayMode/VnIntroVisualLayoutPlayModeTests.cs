using System.Collections;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class VnIntroVisualLayoutPlayModeTests
    {
        [UnityTest]
        public IEnumerator DialogueCompositionUsesResponsiveBottomPanelCircularPortraitAndFiveControls()
        {
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            VnIntroArt art = CreateArt(assets);
            var host = new GameObject("VnResponsiveLayoutFixture");
            VnIntroView view = VnIntroView.Create(host.transform, assets.sans, art,
                () => { }, () => { }, () => { }, () => { });

            try
            {
                yield return null;
                Canvas.ForceUpdateCanvases();

                RectTransform panel = GameObject.Find("DialoguePanel").GetComponent<RectTransform>();
                Assert.That(panel.anchorMin.x, Is.LessThanOrEqualTo(.10f),
                    "The polished panel must use the available width instead of a fixed top-left rectangle.");
                Assert.That(panel.anchorMax.x, Is.GreaterThanOrEqualTo(.90f));
                Assert.That(panel.anchorMin.y, Is.LessThanOrEqualTo(.10f),
                    "The dialogue composition must remain bottom anchored at 16:9 and 4:3 runtimes.");
                Assert.That(panel.anchorMax.y, Is.LessThanOrEqualTo(.20f));

                GameObject portraitMaskObject = GameObject.Find("PortraitMask");
                Assert.That(portraitMaskObject, Is.Not.Null,
                    "Target PNGs require a dedicated circular portrait mask on the lower-left panel edge.");
                Assert.That(portraitMaskObject.GetComponent<Mask>(), Is.Not.Null,
                    "Portrait framing must actually clip the sheet art rather than only place it in a square.");
                GameObject portrait = GameObject.Find("Portrait");
                Assert.That(portrait, Is.Not.Null);
                Assert.That(portrait.transform.IsChildOf(portraitMaskObject.transform), Is.True);

                foreach (string name in new[] { "MuteButton", "PauseButton", "SkipButton", "BackButton", "NextButton" })
                {
                    GameObject buttonObject = GameObject.Find(name);
                    Assert.That(buttonObject, Is.Not.Null, name + " must exist.");
                    Assert.That(buttonObject.transform.IsChildOf(panel.transform), Is.True,
                        name + " must live inside the bottom-panel composition so it cannot clip off narrow 4:3 windows.");
                }

                Button back = GameObject.Find("BackButton").GetComponent<Button>();
                Assert.That(back.interactable, Is.False,
                    "Back is intentionally visible but disabled until a real Yarn rewind design exists.");
            }
            finally
            {
                view.Dispose();
                Object.Destroy(host);
            }
        }

        [UnityTest]
        public IEnumerator NextUsesTheExistingOneBeatContinueCallbackExactlyOnce()
        {
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            VnIntroArt art = CreateArt(assets);
            int continueCount = 0;
            var host = new GameObject("VnNextButtonFixture");
            VnIntroView view = VnIntroView.Create(host.transform, assets.sans, art,
                () => continueCount++, () => { }, () => { }, () => { });

            try
            {
                yield return null;
                Button next = GameObject.Find("NextButton").GetComponent<Button>();
                Assert.That(next.interactable, Is.True);
                next.onClick.Invoke();
                Assert.That(continueCount, Is.EqualTo(1),
                    "One Next press must invoke exactly the same single continuation callback as the story surface.");

                Button storySurface = GameObject.Find("StoryClickSurface").GetComponent<Button>();
                storySurface.onClick.Invoke();
                Assert.That(continueCount, Is.EqualTo(2),
                    "Next must not add a second progression path or autoplay behaviour.");
            }
            finally
            {
                view.Dispose();
                Object.Destroy(host);
            }
        }

        [UnityTest]
        public IEnumerator LightDialoguePanelUsesDarkReadableTextWhileDarkPanelStaysLight()
        {
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            VnIntroArt art = CreateArt(assets);
            var host = new GameObject("VnDialogueContrastFixture");
            VnIntroView view = VnIntroView.Create(host.transform, assets.sans, art,
                () => { }, () => { }, () => { }, () => { });

            try
            {
                view.ApplyBeat(VnIntroController.MapBeat("bus_stop", "Keiko", "dark", "keiko_neutral"));
                view.PresentLine("Keiko", "Dark panel line");
                yield return null;

                Text speaker = GameObject.Find("SpeakerName").GetComponent<Text>();
                Text dialogue = GameObject.Find("DialogueText").GetComponent<Text>();
                Assert.That(speaker.color, Is.EqualTo(Color.white));
                Assert.That(dialogue.color, Is.EqualTo(Color.white));

                view.ApplyBeat(VnIntroController.MapBeat("phone", "Mina", "light", "mina_neutral"));
                view.PresentLine("Mina", "Я дома, приходи, нужно поговорить.");
                yield return null;

                Assert.That(Luminance(speaker.color), Is.LessThan(.35f),
                    "Mina's light dialogue panel needs dark text with clear contrast.");
                Assert.That(Luminance(dialogue.color), Is.LessThan(.35f),
                    "Mina's light dialogue panel needs dark dialogue text with clear contrast.");
                Assert.That(speaker.color.a, Is.EqualTo(1f).Within(.001f));
                Assert.That(dialogue.color.a, Is.EqualTo(1f).Within(.001f));
            }
            finally
            {
                view.Dispose();
                Object.Destroy(host);
            }
        }

        [UnityTest]
        public IEnumerator ViewUsesAuthoredExpressionAndUnknownStateFallsBackToSpeakerNeutral()
        {
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            VnIntroArt art = CreateArt(assets);
            var host = new GameObject("VnAuthoredExpressionFixture");
            VnIntroView view = VnIntroView.Create(host.transform, assets.sans, art,
                () => { }, () => { }, () => { }, () => { });

            try
            {
                VnCharacterVisualState keikoSerious = VnCharacterVisualCatalog.ResolveOrNeutral("keiko_serious", "Keiko");
                view.ApplyBeat(VnIntroController.MapBeat("bus_stop", "Keiko", "dark", "keiko_serious"));
                yield return null;

                RawImage portrait = GameObject.Find("Portrait").GetComponent<RawImage>();
                Assert.That(portrait.texture, Is.SameAs(art.KeikoCharacterSheet));
                Assert.That(portrait.uvRect, Is.EqualTo(keikoSerious.PortraitUv),
                    "The view must use the inspected authored expression UV instead of hard-coding neutral.");

                VnCharacterVisualState minaNeutral = VnCharacterVisualCatalog.ResolveOrNeutral("mina_not_authored", "Mina");
                view.ApplyBeat(VnIntroController.MapBeat("phone", "Mina", "light", "mina_not_authored"));
                yield return null;

                Assert.That(portrait.texture, Is.SameAs(art.MinaCharacterSheet));
                Assert.That(portrait.uvRect, Is.EqualTo(minaNeutral.PortraitUv),
                    "Unknown visual tokens must gracefully keep that speaker on authored neutral.");
            }
            finally
            {
                view.Dispose();
                Object.Destroy(host);
            }
        }

        [UnityTest]
        public IEnumerator ExpressionSwapCrossfadesOnUnscaledTimeAndPauseFreezesPresentationOnly()
        {
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            VnIntroArt art = CreateArt(assets);
            var host = new GameObject("VnExpressionCrossfadeFixture");
            VnIntroView view = VnIntroView.Create(host.transform, assets.sans, art,
                () => { }, () => { }, () => { }, () => { });

            try
            {
                VnCharacterVisualState neutral = VnCharacterVisualCatalog.ResolveOrNeutral("keiko_neutral", "Keiko");
                VnCharacterVisualState serious = VnCharacterVisualCatalog.ResolveOrNeutral("keiko_serious", "Keiko");
                VnCharacterVisualState thoughtful = VnCharacterVisualCatalog.ResolveOrNeutral("keiko_thoughtful", "Keiko");

                view.ApplyBeat(VnIntroController.MapBeat("bus_stop", "Keiko", "dark", "keiko_neutral"));
                yield return null;
                RawImage current = GameObject.Find("Portrait").GetComponent<RawImage>();
                GameObject previousObject = GameObject.Find("PortraitPrevious");
                Assert.That(previousObject, Is.Not.Null,
                    "Expression switching needs a reusable previous-portrait layer for a short authored-state crossfade.");
                RawImage previous = previousObject.GetComponent<RawImage>();

                view.ApplyBeat(VnIntroController.MapBeat("bus_stop", "Keiko", "dark", "keiko_serious"));
                Assert.That(current.uvRect, Is.EqualTo(serious.PortraitUv));
                Assert.That(previous.uvRect, Is.EqualTo(neutral.PortraitUv));
                Assert.That(previous.color.a, Is.GreaterThan(.05f));

                yield return new WaitForSecondsRealtime(.25f);
                Assert.That(current.color.a, Is.EqualTo(1f).Within(.02f));
                Assert.That(previous.color.a, Is.LessThan(.02f));

                view.ApplyBeat(VnIntroController.MapBeat("bus_stop", "Keiko", "dark", "keiko_thoughtful"));
                view.SetPausedVisual(true);
                float pausedCurrentAlpha = current.color.a;
                float pausedPreviousAlpha = previous.color.a;
                Rect pausedUv = current.uvRect;
                float globalTimeScale = Time.timeScale;

                yield return new WaitForSecondsRealtime(.25f);
                Assert.That(current.color.a, Is.EqualTo(pausedCurrentAlpha).Within(.001f));
                Assert.That(previous.color.a, Is.EqualTo(pausedPreviousAlpha).Within(.001f));
                Assert.That(current.uvRect, Is.EqualTo(pausedUv));
                Assert.That(Time.timeScale, Is.EqualTo(globalTimeScale),
                    "VN presentation pause must remain local and must not touch global Time.timeScale.");

                view.SetPausedVisual(false);
                yield return new WaitForSecondsRealtime(.25f);
                Assert.That(current.color.a, Is.EqualTo(1f).Within(.02f));
                Assert.That(previous.color.a, Is.LessThan(.02f));

                view.ApplyBeat(VnIntroController.MapBeat("phone", "Mina", "light", "mina_neutral"));
                Rect minaNeutralUv = VnCharacterVisualCatalog.ResolveOrNeutral("mina_neutral", "Mina").PortraitUv;
                yield return new WaitForSecondsRealtime(.35f);
                Assert.That(current.uvRect, Is.EqualTo(minaNeutralUv),
                    "With no authored neutral blink, Mina must retain neutral rather than borrowing happy/closed-eyes art.");
            }
            finally
            {
                view.Dispose();
                Object.Destroy(host);
            }
        }

        [UnityTest]
        public IEnumerator FullBodyIdleBreathingUsesUnscaledLocalTimeAndFreezesWhenPaused()
        {
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            VnIntroArt art = CreateArt(assets);
            var host = new GameObject("VnIdleBreathingFixture");
            VnIntroView view = VnIntroView.Create(host.transform, assets.sans, art,
                () => { }, () => { }, () => { }, () => { });

            try
            {
                VnCharacterVisualState keiko = VnCharacterVisualCatalog.ResolveOrNeutral("keiko_neutral", "Keiko");
                view.ApplyBeat(VnIntroController.MapBeat("bus_stop", "Keiko", "dark", "keiko_neutral"));
                yield return null;

                GameObject bodyObject = GameObject.Find("CharacterPrimary");
                Assert.That(bodyObject, Is.Not.Null,
                    "The polished VN needs a full-body authored character surface behind the dialogue panel.");
                RawImage body = bodyObject.GetComponent<RawImage>();
                RectTransform bodyRect = bodyObject.GetComponent<RectTransform>();
                Assert.That(body.texture, Is.SameAs(art.KeikoCharacterSheet));
                Assert.That(body.uvRect, Is.EqualTo(keiko.BodyUv),
                    "Full-body staging must reuse the inspected authored body crop from the catalog.");

                Vector2 initialPosition = bodyRect.anchoredPosition;
                Vector3 initialScale = bodyRect.localScale;
                float globalTimeScale = Time.timeScale;
                yield return new WaitForSecondsRealtime(.35f);

                bool moved = Vector2.Distance(initialPosition, bodyRect.anchoredPosition) > .1f ||
                             Vector3.Distance(initialScale, bodyRect.localScale) > .001f;
                Assert.That(moved, Is.True,
                    "Idle breathing should be subtle but measurably presentation-driven on unscaled time.");
                Assert.That(Time.timeScale, Is.EqualTo(globalTimeScale));

                view.SetPausedVisual(true);
                Vector2 pausedPosition = bodyRect.anchoredPosition;
                Vector3 pausedScale = bodyRect.localScale;
                yield return new WaitForSecondsRealtime(.35f);

                Assert.That(Vector2.Distance(pausedPosition, bodyRect.anchoredPosition), Is.LessThan(.01f));
                Assert.That(Vector3.Distance(pausedScale, bodyRect.localScale), Is.LessThan(.0001f));
                Assert.That(Time.timeScale, Is.EqualTo(globalTimeScale),
                    "VN Pause must freeze only local presentation animation and never global time.");

                view.SetPausedVisual(false);
                yield return new WaitForSecondsRealtime(.35f);
                bool resumed = Vector2.Distance(pausedPosition, bodyRect.anchoredPosition) > .1f ||
                               Vector3.Distance(pausedScale, bodyRect.localScale) > .001f;
                Assert.That(resumed, Is.True, "Idle breathing should resume from the frozen local phase.");
            }
            finally
            {
                view.Dispose();
                Object.Destroy(host);
            }
        }

        [Test]
        public void VisualCatalogContainsOnlyInspectedAuthoredStatesAndNoInventedBlink()
        {
            string[] expected =
            {
                "keiko_neutral", "keiko_surprised", "keiko_serious", "keiko_thoughtful", "keiko_uneasy",
                "mina_neutral", "mina_happy", "mina_serious", "mina_surprised", "mina_reaching"
            };

            foreach (string id in expected)
            {
                Assert.That(VnCharacterVisualCatalog.TryResolve(id, out VnCharacterVisualState state), Is.True, id);
                Assert.That(state.PortraitUv.width, Is.GreaterThan(0f), id);
                Assert.That(state.PortraitUv.height, Is.GreaterThan(0f), id);
                Assert.That(state.BodyUv.width, Is.GreaterThan(0f), id);
                Assert.That(state.BodyUv.height, Is.GreaterThan(0f), id);
            }

            Assert.That(VnCharacterVisualCatalog.ResolveOrNeutral("keiko_neutral", "Keiko").HasBlinkState, Is.False,
                "Keiko sheet inspection found no dedicated neutral blink cell; do not fake one.");
            Assert.That(VnCharacterVisualCatalog.ResolveOrNeutral("mina_neutral", "Mina").HasBlinkState, Is.False,
                "Mina's happy closed-eye portrait is a distinct expression, not a neutral blink frame.");
        }

        [Test]
        public void BlinkHookUsesOnlyExplicitAuthoredBlinkUvAndNeverBorrowsAnotherExpression()
        {
            VnCharacterVisualState minaNeutral = VnCharacterVisualCatalog.ResolveOrNeutral("mina_neutral", "Mina");
            VnCharacterVisualState minaHappy = VnCharacterVisualCatalog.ResolveOrNeutral("mina_happy", "Mina");

            Rect requestedMinaBlink = VnCharacterVisualCatalog.ResolvePortraitUv(minaNeutral, true);
            Assert.That(requestedMinaBlink, Is.EqualTo(minaNeutral.PortraitUv),
                "Mina neutral has no authored blink; requesting blink must keep neutral rather than borrow happy closed-eyes art.");
            Assert.That(requestedMinaBlink, Is.Not.EqualTo(minaHappy.PortraitUv));

            Rect authoredNeutral = new Rect(.10f, .20f, .30f, .40f);
            Rect authoredBlink = new Rect(.50f, .20f, .30f, .40f);
            var futureAuthoredState = new VnCharacterVisualState(
                "future_neutral", "Future", authoredNeutral, authoredNeutral, true, authoredBlink);

            Assert.That(VnCharacterVisualCatalog.ResolvePortraitUv(futureAuthoredState, false), Is.EqualTo(authoredNeutral));
            Assert.That(VnCharacterVisualCatalog.ResolvePortraitUv(futureAuthoredState, true), Is.EqualTo(authoredBlink),
                "The reusable hook should select a blink UV only when the state explicitly declares authored blink art.");
        }

        [Test]
        public void UnknownExpressionFallsBackToTheInspectedNeutralForThatSpeaker()
        {
            VnCharacterVisualState keiko = VnCharacterVisualCatalog.ResolveOrNeutral("keiko_blink_not_authored", "Keiko");
            VnCharacterVisualState mina = VnCharacterVisualCatalog.ResolveOrNeutral("mina_blink_not_authored", "Mina");
            Assert.That(keiko.Id, Is.EqualTo("keiko_neutral"));
            Assert.That(mina.Id, Is.EqualTo("mina_neutral"));
        }

        private static float Luminance(Color color)
        {
            return color.r * .2126f + color.g * .7152f + color.b * .0722f;
        }

        private static VnIntroArt CreateArt(RokasAssets assets)
        {
            return new VnIntroArt(
                assets.vnKeikoCharacterSheet,
                assets.vnMinaCharacterSheet,
                assets.vnBusStopRainNight,
                assets.vnNightSkyRain,
                assets.vnBusStopPhoneMessageMina,
                assets.vnDialoguePanelKeikoDark,
                assets.vnDialoguePanelMinaLight,
                assets.vnIconMute,
                assets.vnIconPause,
                assets.vnIconSkip);
        }
    }
}