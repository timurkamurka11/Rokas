using System.Collections;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class VnIntroPresentationContinuationPlayModeTests
    {
        [UnityTest]
        public IEnumerator KeikoUsesFullDarkPanelWithNameAndTextButNoBodyOrCircularPortrait()
        {
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            VnIntroArt art = CreateArt(assets);
            var host = new GameObject("VnKeikoTextOnlyFixture");
            VnIntroView view = VnIntroView.Create(host.transform, assets.sans, art,
                () => { }, () => { }, () => { }, () => { });

            try
            {
                view.ApplyBeat(VnIntroController.MapBeat("bus_stop", "Keiko", "dark", "keiko_neutral"));
                view.PresentLine("Keiko", "Дождь не прекращается.");
                yield return null;

                RawImage panel = GameObject.Find("DialoguePanel").GetComponent<RawImage>();
                Assert.That(panel.texture, Is.SameAs(art.DialoguePanelKeikoDark));
                Assert.That(panel.uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)),
                    "The new authored 2048x682 panel must render whole instead of using the old PanelBodyCrop.");

                GameObject primary = FindDescendantIncludingInactive(host.transform, "CharacterPrimary");
                GameObject secondary = FindDescendantIncludingInactive(host.transform, "CharacterSecondary");
                Assert.That(primary, Is.Not.Null);
                Assert.That(secondary, Is.Not.Null);
                Assert.That(primary.activeSelf, Is.False,
                    "Keiko protagonist narration must not stage a Keiko body.");
                Assert.That(secondary.activeSelf, Is.False);

                Assert.That(FindDescendantIncludingInactive(host.transform, "PortraitMask"), Is.Null);
                Assert.That(FindDescendantIncludingInactive(host.transform, "Portrait"), Is.Null);
                Assert.That(FindDescendantIncludingInactive(host.transform, "PortraitPrevious"), Is.Null);
                Assert.That(FindDescendantIncludingInactive(host.transform, "PortraitFrame"), Is.Null);

                Assert.That(GameObject.Find("SpeakerName").GetComponent<Text>().text, Is.EqualTo("Keiko"));
                Assert.That(GameObject.Find("DialogueText").GetComponent<Text>().text,
                    Is.EqualTo("Дождь не прекращается."));
            }
            finally
            {
                view.Dispose();
                Object.Destroy(host);
            }
        }

        [UnityTest]
        public IEnumerator MinaUsesFullLightPanelAndACloserSeparateStableBodyWithoutCircularPortrait()
        {
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            VnIntroArt art = CreateArt(assets);
            var host = new GameObject("VnMinaCloserFixture");
            VnIntroView view = VnIntroView.Create(host.transform, assets.sans, art,
                () => { }, () => { }, () => { }, () => { });

            try
            {
                view.ApplyBeat(VnIntroController.MapBeat("phone", "Mina", "light", "mina_neutral"));
                view.PresentLine("Mina", "Я дома, приходи, нужно поговорить.");
                yield return new WaitForSecondsRealtime(.30f);

                RawImage panel = GameObject.Find("DialoguePanel").GetComponent<RawImage>();
                Assert.That(panel.texture, Is.SameAs(art.DialoguePanelMinaLight));
                Assert.That(panel.uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));

                GameObject primary = FindDescendantIncludingInactive(host.transform, "CharacterPrimary");
                Assert.That(primary.activeSelf, Is.True,
                    "Mina remains a separately staged visible character.");
                Assert.That(primary.GetComponent<RawImage>().texture, Is.SameAs(art.MinaCharacterSheet));
                RectTransform bodyRect = primary.GetComponent<RectTransform>();
                Assert.That(bodyRect.rect.height, Is.GreaterThanOrEqualTo(1180f),
                    "Mina should be framed closer to camera than the old 980px full-length staging.");

                Assert.That(FindDescendantIncludingInactive(host.transform, "PortraitMask"), Is.Null);
                Assert.That(FindDescendantIncludingInactive(host.transform, "Portrait"), Is.Null);
                Assert.That(FindDescendantIncludingInactive(host.transform, "PortraitPrevious"), Is.Null);
                Assert.That(FindDescendantIncludingInactive(host.transform, "PortraitFrame"), Is.Null);

                Vector2 stablePosition = bodyRect.anchoredPosition;
                Vector3 stableScale = bodyRect.localScale;
                yield return new WaitForSecondsRealtime(.35f);
                Assert.That(Vector2.Distance(stablePosition, bodyRect.anchoredPosition), Is.LessThan(.01f),
                    "A lone Mina must not bob vertically.");
                Assert.That(Vector3.Distance(stableScale, bodyRect.localScale), Is.LessThan(.0002f),
                    "A lone Mina must not pulse or pump scale.");
                Assert.That(bodyRect.localScale.x, Is.EqualTo(1f).Within(.01f),
                    "Single-character presentation should remain at neutral focus scale.");
            }
            finally
            {
                view.Dispose();
                Object.Destroy(host);
            }
        }

        [UnityTest]
        public IEnumerator SpeakerFocusActivatesOnlyWhenTwoVisibleBodiesAreStaged()
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
                GameObject primary = FindDescendantIncludingInactive(host.transform, "CharacterPrimary");
                GameObject secondary = FindDescendantIncludingInactive(host.transform, "CharacterSecondary");

                view.SetCharacterStage(mina, null);
                view.PresentLine("Mina", "Single visible speaker");
                yield return new WaitForSecondsRealtime(.25f);
                Assert.That(primary.activeSelf, Is.True);
                Assert.That(secondary.activeSelf, Is.False);
                Assert.That(primary.transform.localScale.x, Is.EqualTo(1f).Within(.01f),
                    "One visible body must not receive active-speaker pulse/focus scaling.");
                Assert.That(Luminance(primary.GetComponent<RawImage>().color), Is.EqualTo(1f).Within(.02f));

                view.SetCharacterStage(keiko, mina);
                view.PresentLine("Mina", "Two visible speakers");
                yield return new WaitForSecondsRealtime(.25f);

                RawImage primaryImage = primary.GetComponent<RawImage>();
                RawImage secondaryImage = secondary.GetComponent<RawImage>();
                Assert.That(primary.activeSelf, Is.True);
                Assert.That(secondary.activeSelf, Is.True);
                Assert.That(secondary.transform.localScale.x, Is.GreaterThan(1.02f));
                Assert.That(primary.transform.localScale.x, Is.LessThan(.98f));
                Assert.That(Luminance(secondaryImage.color), Is.GreaterThan(Luminance(primaryImage.color)));
                Assert.That(secondaryImage.color.a, Is.GreaterThan(primaryImage.color.a));
            }
            finally
            {
                view.Dispose();
                Object.Destroy(host);
            }
        }

        [UnityTest]
        public IEnumerator ControlsUseTransparentHitTargetsAndKeepExistingSemantics()
        {
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            VnIntroArt art = CreateArt(assets);
            int continueCount = 0;
            var host = new GameObject("VnTransparentControlsFixture");
            VnIntroView view = VnIntroView.Create(host.transform, assets.sans, art,
                () => continueCount++, () => { }, () => { }, () => { });

            try
            {
                yield return null;
                foreach (string name in new[] { "MuteButton", "PauseButton", "SkipButton", "BackButton", "NextButton" })
                {
                    GameObject control = GameObject.Find(name);
                    Assert.That(control, Is.Not.Null, name + " must remain present.");
                    Button button = control.GetComponent<Button>();
                    Assert.That(button, Is.Not.Null);
                    if (button.targetGraphic != null)
                    {
                        Assert.That(button.targetGraphic.color.a, Is.LessThanOrEqualTo(.001f),
                            name + " must use a transparent hit target with no black/opaque backing artifact.");
                    }
                }

                Button back = GameObject.Find("BackButton").GetComponent<Button>();
                Button next = GameObject.Find("NextButton").GetComponent<Button>();
                Assert.That(back.interactable, Is.False);
                Assert.That(next.interactable, Is.True);
                next.onClick.Invoke();
                Assert.That(continueCount, Is.EqualTo(1),
                    "Next must remain on the existing one-click-one-beat continueStory path.");
            }
            finally
            {
                view.Dispose();
                Object.Destroy(host);
            }
        }

        private static GameObject FindDescendantIncludingInactive(Transform root, string name)
        {
            foreach (Transform descendant in root.GetComponentsInChildren<Transform>(true))
            {
                if (descendant.name == name)
                {
                    return descendant.gameObject;
                }
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
