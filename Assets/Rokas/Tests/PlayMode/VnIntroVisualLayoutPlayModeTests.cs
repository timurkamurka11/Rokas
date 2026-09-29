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
        public IEnumerator DialogueCompositionUsesFullAuthoredPanelWithoutCircularPortraitAndTransparentControls()
        {
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            VnIntroArt art = CreateArt(assets);
            var host = new GameObject("VnFullPanelLayoutFixture");
            VnIntroView view = VnIntroView.Create(host.transform, assets.sans, art,
                () => { }, () => { }, () => { }, () => { });

            try
            {
                view.ApplyBeat(VnIntroController.MapBeat("bus_stop", "Keiko", "dark", "keiko_neutral"));
                view.PresentLine("Keiko", "Panel layout proof");
                yield return null;
                Canvas.ForceUpdateCanvases();

                RawImage panel = GameObject.Find("DialoguePanel").GetComponent<RawImage>();
                Assert.That(panel.texture, Is.SameAs(art.DialoguePanelKeikoDark));
                Assert.That(panel.uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)),
                    "The authored 2048x682 panel must render whole, without the retired crop composition.");
                AspectRatioFitter fitter = panel.GetComponent<AspectRatioFitter>();
                Assert.That(fitter, Is.Not.Null);
                Assert.That(fitter.aspectRatio, Is.EqualTo(2048f / 682f).Within(.001f));

                Assert.That(FindDescendantIncludingInactive(host.transform, "PortraitMask"), Is.Null);
                Assert.That(FindDescendantIncludingInactive(host.transform, "Portrait"), Is.Null);
                Assert.That(FindDescendantIncludingInactive(host.transform, "PortraitPrevious"), Is.Null);
                Assert.That(FindDescendantIncludingInactive(host.transform, "PortraitFrame"), Is.Null,
                    "Circular dialogue portrait composition is removed for every speaker.");

                foreach (string name in new[] { "MuteButton", "PauseButton", "SkipButton", "BackButton", "NextButton" })
                {
                    GameObject control = GameObject.Find(name);
                    Assert.That(control, Is.Not.Null, name + " must remain present.");
                    Assert.That(control.transform.IsChildOf(panel.transform), Is.True,
                        name + " must stay integrated with the authored panel composition.");
                    Button button = control.GetComponent<Button>();
                    Assert.That(button, Is.Not.Null);
                    Assert.That(button.targetGraphic, Is.Not.Null);
                    Assert.That(button.targetGraphic.color.a, Is.LessThanOrEqualTo(.001f),
                        name + " must use a transparent hit target with no black backing rectangle.");
                }

                Assert.That(GameObject.Find("BackButton").GetComponent<Button>().interactable, Is.False);
                Assert.That(GameObject.Find("NextButton").GetComponent<Button>().interactable, Is.True);
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

                Assert.That(Luminance(speaker.color), Is.LessThan(.35f));
                Assert.That(Luminance(dialogue.color), Is.LessThan(.35f));
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
        public IEnumerator KeikoIsTextOnlyWhileMinaUsesSeparateCloserAuthoredBody()
        {
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            VnIntroArt art = CreateArt(assets);
            var host = new GameObject("VnSpeakerPresentationFixture");
            VnIntroView view = VnIntroView.Create(host.transform, assets.sans, art,
                () => { }, () => { }, () => { }, () => { });

            try
            {
                GameObject primary = FindDescendantIncludingInactive(host.transform, "CharacterPrimary");
                GameObject secondary = FindDescendantIncludingInactive(host.transform, "CharacterSecondary");
                Assert.That(primary, Is.Not.Null);
                Assert.That(secondary, Is.Not.Null);

                view.ApplyBeat(VnIntroController.MapBeat("bus_stop", "Keiko", "dark", "keiko_serious"));
                view.PresentLine("Keiko", "Inner monologue");
                yield return null;
                Assert.That(primary.activeSelf, Is.False, "Keiko protagonist mode must not stage a body.");
                Assert.That(secondary.activeSelf, Is.False);
                Assert.That(GameObject.Find("DialoguePanel").GetComponent<RawImage>().texture,
                    Is.SameAs(art.DialoguePanelKeikoDark));
                Assert.That(GameObject.Find("SpeakerName").GetComponent<Text>().text, Is.EqualTo("Keiko"));

                VnCharacterVisualState minaNeutral = VnCharacterVisualCatalog.ResolveOrNeutral("mina_not_authored", "Mina");
                view.ApplyBeat(VnIntroController.MapBeat("phone", "Mina", "light", "mina_not_authored"));
                view.PresentLine("Mina", "Visible NPC");
                yield return new WaitForSecondsRealtime(.25f);

                Assert.That(primary.activeSelf, Is.True);
                Assert.That(secondary.activeSelf, Is.False);
                RawImage body = primary.GetComponent<RawImage>();
                RectTransform bodyRect = primary.GetComponent<RectTransform>();
                Assert.That(body.texture, Is.SameAs(art.MinaCharacterSheet));
                Assert.That(body.uvRect, Is.EqualTo(minaNeutral.BodyUv),
                    "Unknown Mina expression tokens must still resolve to authored neutral body state.");
                Assert.That(bodyRect.rect.height, Is.GreaterThanOrEqualTo(1180f),
                    "Mina should use the closer thigh-up framing rather than the old distant 980px staging.");
                Assert.That(GameObject.Find("DialoguePanel").GetComponent<RawImage>().texture,
                    Is.SameAs(art.DialoguePanelMinaLight));
            }
            finally
            {
                view.Dispose();
                Object.Destroy(host);
            }
        }

        [UnityTest]
        public IEnumerator SingleVisibleBodyRemainsStableAndPauseNeverChangesGlobalTimeScale()
        {
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            VnIntroArt art = CreateArt(assets);
            var host = new GameObject("VnStableSoloFixture");
            VnIntroView view = VnIntroView.Create(host.transform, assets.sans, art,
                () => { }, () => { }, () => { }, () => { });

            try
            {
                view.ApplyBeat(VnIntroController.MapBeat("phone", "Mina", "light", "mina_neutral"));
                view.PresentLine("Mina", "Stable solo speaker");
                yield return new WaitForSecondsRealtime(.25f);

                GameObject primary = FindDescendantIncludingInactive(host.transform, "CharacterPrimary");
                Assert.That(primary.activeSelf, Is.True);
                RectTransform rect = primary.GetComponent<RectTransform>();
                Vector2 initialPosition = rect.anchoredPosition;
                Vector3 initialScale = rect.localScale;
                Assert.That(initialScale.x, Is.EqualTo(1f).Within(.01f));
                float globalTimeScale = Time.timeScale;

                yield return new WaitForSecondsRealtime(.35f);
                Assert.That(Vector2.Distance(initialPosition, rect.anchoredPosition), Is.LessThan(.01f),
                    "One visible body must not bob vertically.");
                Assert.That(Vector3.Distance(initialScale, rect.localScale), Is.LessThan(.0002f),
                    "One visible body must not pulse or pump scale.");

                view.SetPausedVisual(true);
                yield return new WaitForSecondsRealtime(.20f);
                Assert.That(Time.timeScale, Is.EqualTo(globalTimeScale),
                    "VN Pause remains presentation-local and must not touch global time.");
                Assert.That(rect.anchoredPosition, Is.EqualTo(initialPosition));
                Assert.That(rect.localScale, Is.EqualTo(initialScale));
            }
            finally
            {
                view.Dispose();
                Object.Destroy(host);
            }
        }

        [UnityTest]
        public IEnumerator ActiveSpeakerFocusOnlyAppliesWhenTwoVisibleBodiesAreStagedAndPausesLocally()
        {
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            VnIntroArt art = CreateArt(assets);
            var host = new GameObject("VnTwoBodyFocusFixture");
            VnIntroView view = VnIntroView.Create(host.transform, assets.sans, art,
                () => { }, () => { }, () => { }, () => { });

            try
            {
                VnCharacterVisualState keiko = VnCharacterVisualCatalog.ResolveOrNeutral("keiko_neutral", "Keiko");
                VnCharacterVisualState mina = VnCharacterVisualCatalog.ResolveOrNeutral("mina_neutral", "Mina");
                GameObject primaryObject = FindDescendantIncludingInactive(host.transform, "CharacterPrimary");
                GameObject secondaryObject = FindDescendantIncludingInactive(host.transform, "CharacterSecondary");

                view.SetCharacterStage(null, null);
                yield return null;
                Assert.That(primaryObject.activeSelf, Is.False);
                Assert.That(secondaryObject.activeSelf, Is.False);

                view.SetCharacterStage(mina, null);
                view.PresentLine("Mina", "Single visible speaker");
                yield return new WaitForSecondsRealtime(.25f);
                Assert.That(primaryObject.activeSelf, Is.True);
                Assert.That(secondaryObject.activeSelf, Is.False);
                Assert.That(primaryObject.transform.localScale.x, Is.EqualTo(1f).Within(.01f));
                Assert.That(Luminance(primaryObject.GetComponent<RawImage>().color), Is.EqualTo(1f).Within(.02f));

                view.SetCharacterStage(keiko, mina);
                view.PresentLine("Keiko", "Keiko active in a two-body staging proof");
                yield return new WaitForSecondsRealtime(.25f);

                RawImage primary = primaryObject.GetComponent<RawImage>();
                RawImage secondary = secondaryObject.GetComponent<RawImage>();
                Assert.That(primaryObject.activeSelf, Is.True);
                Assert.That(secondaryObject.activeSelf, Is.True);
                Assert.That(primaryObject.transform.localScale.x, Is.GreaterThan(1.02f));
                Assert.That(secondaryObject.transform.localScale.x, Is.LessThan(.98f));
                Assert.That(Luminance(primary.color), Is.GreaterThan(Luminance(secondary.color)));
                Assert.That(primary.color.a, Is.GreaterThan(secondary.color.a));
                Assert.That(primaryObject.transform.GetSiblingIndex(), Is.GreaterThan(secondaryObject.transform.GetSiblingIndex()));

                view.PresentLine("Mina", "Mina active");
                view.SetPausedVisual(true);
                Vector3 pausedPrimaryScale = primaryObject.transform.localScale;
                Vector3 pausedSecondaryScale = secondaryObject.transform.localScale;
                Color pausedPrimaryColor = primary.color;
                Color pausedSecondaryColor = secondary.color;
                float globalTimeScale = Time.timeScale;
                yield return new WaitForSecondsRealtime(.25f);

                Assert.That(primaryObject.transform.localScale, Is.EqualTo(pausedPrimaryScale));
                Assert.That(secondaryObject.transform.localScale, Is.EqualTo(pausedSecondaryScale));
                Assert.That(primary.color, Is.EqualTo(pausedPrimaryColor));
                Assert.That(secondary.color, Is.EqualTo(pausedSecondaryColor));
                Assert.That(Time.timeScale, Is.EqualTo(globalTimeScale));

                view.SetPausedVisual(false);
                yield return new WaitForSecondsRealtime(.25f);
                Assert.That(secondaryObject.transform.localScale.x, Is.GreaterThan(1.02f));
                Assert.That(primaryObject.transform.localScale.x, Is.LessThan(.98f));
                Assert.That(Luminance(secondary.color), Is.GreaterThan(Luminance(primary.color)));
                Assert.That(secondary.color.a, Is.GreaterThan(primary.color.a));
                Assert.That(secondaryObject.transform.GetSiblingIndex(), Is.GreaterThan(primaryObject.transform.GetSiblingIndex()));
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

            Assert.That(VnCharacterVisualCatalog.ResolveOrNeutral("keiko_neutral", "Keiko").HasBlinkState, Is.False);
            Assert.That(VnCharacterVisualCatalog.ResolveOrNeutral("mina_neutral", "Mina").HasBlinkState, Is.False);
        }

        [Test]
        public void BlinkHookUsesOnlyExplicitAuthoredBlinkUvAndNeverBorrowsAnotherExpression()
        {
            VnCharacterVisualState minaNeutral = VnCharacterVisualCatalog.ResolveOrNeutral("mina_neutral", "Mina");
            VnCharacterVisualState minaHappy = VnCharacterVisualCatalog.ResolveOrNeutral("mina_happy", "Mina");

            Rect requestedMinaBlink = VnCharacterVisualCatalog.ResolvePortraitUv(minaNeutral, true);
            Assert.That(requestedMinaBlink, Is.EqualTo(minaNeutral.PortraitUv));
            Assert.That(requestedMinaBlink, Is.Not.EqualTo(minaHappy.PortraitUv));

            Rect authoredNeutral = new Rect(.10f, .20f, .30f, .40f);
            Rect authoredBlink = new Rect(.50f, .20f, .30f, .40f);
            var futureAuthoredState = new VnCharacterVisualState(
                "future_neutral", "Future", authoredNeutral, authoredNeutral, true, authoredBlink);

            Assert.That(VnCharacterVisualCatalog.ResolvePortraitUv(futureAuthoredState, false), Is.EqualTo(authoredNeutral));
            Assert.That(VnCharacterVisualCatalog.ResolvePortraitUv(futureAuthoredState, true), Is.EqualTo(authoredBlink));
        }

        [Test]
        public void UnknownExpressionFallsBackToTheInspectedNeutralForThatSpeaker()
        {
            VnCharacterVisualState keiko = VnCharacterVisualCatalog.ResolveOrNeutral("keiko_blink_not_authored", "Keiko");
            VnCharacterVisualState mina = VnCharacterVisualCatalog.ResolveOrNeutral("mina_blink_not_authored", "Mina");
            Assert.That(keiko.Id, Is.EqualTo("keiko_neutral"));
            Assert.That(mina.Id, Is.EqualTo("mina_neutral"));
        }

        private static GameObject FindDescendantIncludingInactive(Transform root, string name)
        {
            foreach (Transform descendant in root.GetComponentsInChildren<Transform>(true))
            {
                if (descendant.name == name) return descendant.gameObject;
            }
            return null;
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
