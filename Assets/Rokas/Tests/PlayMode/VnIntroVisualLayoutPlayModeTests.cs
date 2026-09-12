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
        public IEnumerator ControlsUseTopRightResponsiveAnchors()
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
                foreach (string name in new[] { "MuteButton", "PauseButton", "SkipButton" })
                {
                    GameObject buttonObject = GameObject.Find(name);
                    Assert.That(buttonObject, Is.Not.Null, name + " must exist.");
                    RectTransform rect = buttonObject.GetComponent<RectTransform>();
                    Assert.That(rect.anchorMin, Is.EqualTo(Vector2.one),
                        name + " must anchor to the top-right corner so narrow 4:3 runtime windows cannot clip it.");
                    Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
                    Assert.That(rect.pivot, Is.EqualTo(Vector2.one),
                        name + " pivot must match its top-right anchor.");
                    Assert.That(rect.offsetMax.x, Is.LessThanOrEqualTo(0f),
                        name + " right edge must stay inside the canvas.");
                    Assert.That(rect.offsetMin.x, Is.LessThan(rect.offsetMax.x));
                }
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
