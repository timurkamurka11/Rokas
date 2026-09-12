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
