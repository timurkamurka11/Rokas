using System.Collections;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rokas.Tests
{
    public sealed class VnIntroRuntimeProofHarnessPlayModeTests
    {
        [Test]
        public void RuntimeProofUsesRequestedResponsiveResolution()
        {
            Assert.That(
                RokasVnIntroRuntimeProofRunner.GetRequestedResolution(new[]
                {
                    "Rokas.exe", "-screen-width", "1280", "-screen-height", "720"
                }),
                Is.EqualTo(new Vector2Int(1280, 720)));

            Assert.That(
                RokasVnIntroRuntimeProofRunner.GetRequestedResolution(new[]
                {
                    "Rokas.exe", "-screen-width", "1024", "-screen-height", "768"
                }),
                Is.EqualTo(new Vector2Int(1024, 768)));

            Assert.That(
                RokasVnIntroRuntimeProofRunner.GetRequestedResolution(new[]
                {
                    "Rokas.exe", "-screen-width", "0", "-screen-height", "broken"
                }),
                Is.EqualTo(new Vector2Int(1920, 1080)),
                "Invalid proof dimensions must retain the known-safe default.");
        }

        [Test]
        public void RuntimeProofDoesNotCaptureWhileEnterWorldCurtainIsActive()
        {
            var curtain = new GameObject("EnterWorldCurtain");
            try
            {
                Assert.That(RokasVnIntroRuntimeProofRunner.IsVnPresentationUnobscured(), Is.False,
                    "Runtime screenshots must not be captured through the Enter World fade curtain.");

                curtain.SetActive(false);
                Assert.That(RokasVnIntroRuntimeProofRunner.IsVnPresentationUnobscured(), Is.True,
                    "An inactive transition curtain must not block proof capture.");
            }
            finally
            {
                Object.DestroyImmediate(curtain);
            }
        }

        [UnityTest]
        public IEnumerator RuntimeProofRecognizesPortraitFreeKeikoAndStableSoloMinaComposition()
        {
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            VnIntroArt art = CreateArt(assets);
            var host = new GameObject("VnRuntimeProofPresentationSettlementHost");
            VnIntroView view = VnIntroView.Create(host.transform, assets.sans, art,
                () => { }, () => { }, () => { }, () => { });

            try
            {
                view.ApplyBeat(VnIntroController.MapBeat("bus_stop", "Keiko", "dark", "keiko_neutral"));
                view.PresentLine("Keiko", "Portrait-free protagonist proof");
                yield return null;

                GameObject root = GameObject.Find("VnIntroRoot");
                Assert.That(root, Is.Not.Null);
                Assert.That(RokasVnIntroRuntimeProofRunner.IsPresentationSettled(
                        root, "VN_DialoguePanel_Keiko_Dark", "Keiko", false, string.Empty),
                    Is.True,
                    "Keiko proof capture should require the dark full panel, text-only protagonist state and transparent controls.");
                Assert.That(FindDescendantIncludingInactive(root.transform, "PortraitMask"), Is.Null);
                Assert.That(FindDescendantIncludingInactive(root.transform, "Portrait"), Is.Null);
                Assert.That(FindDescendantIncludingInactive(root.transform, "PortraitPrevious"), Is.Null);
                Assert.That(FindDescendantIncludingInactive(root.transform, "PortraitFrame"), Is.Null);

                view.ApplyBeat(VnIntroController.MapBeat("phone", "Mina", "light", "mina_neutral"));
                view.PresentLine("Mina", "Я дома, приходи, нужно поговорить.");
                yield return new WaitForSecondsRealtime(.25f);

                Assert.That(RokasVnIntroRuntimeProofRunner.IsPresentationSettled(
                        root, "VN_DialoguePanel_Mina_Light", "Mina", true, "Mina_CharacterSheet"),
                    Is.True,
                    "Mina proof capture should require the light full panel and one neutral staged Mina body.");

                GameObject primary = FindDescendantIncludingInactive(root.transform, "CharacterPrimary");
                Assert.That(primary, Is.Not.Null);
                Assert.That(primary.activeSelf, Is.True);
                RectTransform minaRect = primary.GetComponent<RectTransform>();
                Vector2 stablePosition = minaRect.anchoredPosition;
                Vector3 stableScale = minaRect.localScale;

                yield return new WaitForSecondsRealtime(.35f);
                Assert.That(Vector2.Distance(stablePosition, minaRect.anchoredPosition), Is.LessThan(.01f),
                    "The runtime proof must only capture Mina after the solo body is stable with no bobbing.");
                Assert.That(Vector3.Distance(stableScale, minaRect.localScale), Is.LessThan(.0002f),
                    "The runtime proof must only capture Mina after the solo body is stable with no pulsing.");
                Assert.That(RokasVnIntroRuntimeProofRunner.IsPresentationSettled(
                        root, "VN_DialoguePanel_Mina_Light", "Mina", true, "Mina_CharacterSheet"),
                    Is.True);
            }
            finally
            {
                view.Dispose();
                Object.Destroy(host);
            }
        }

        [UnityTest]
        public IEnumerator RuntimeProofRecognizesNestedPolishedVnHierarchy()
        {
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            VnIntroArt art = CreateArt(assets);
            var host = new GameObject("VnRuntimeProofHarnessHost");
            VnIntroView view = VnIntroView.Create(host.transform, assets.sans, art,
                () => { }, () => { }, () => { }, () => { });

            try
            {
                view.ApplyBeat(VnIntroController.MapBeat("bus_stop", "Keiko", "dark", "keiko_neutral"));
                view.PresentLine("Keiko", "Nested proof hierarchy");
                yield return null;

                GameObject root = GameObject.Find("VnIntroRoot");
                Assert.That(root, Is.Not.Null);
                Assert.That(root.transform.Find("PauseButton"), Is.Null,
                    "The polished layout intentionally nests controls below the dialogue panel.");
                Assert.That(root.transform.Find("DialogueText"), Is.Null,
                    "The polished layout intentionally nests dialogue text below the dialogue panel.");
                Assert.That(root.transform.Find("Portrait"), Is.Null,
                    "The retired dialogue portrait must not be reintroduced as a direct child or nested composition.");

                Assert.That(RokasVnIntroRuntimeProofRunner.HasRequiredVnUi(root), Is.True,
                    "The real-player proof must recognize the approved nested portrait-free VN hierarchy.");
            }
            finally
            {
                view.Dispose();
                Object.Destroy(host);
            }
        }

        private static GameObject FindDescendantIncludingInactive(Transform root, string name)
        {
            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform descendant in descendants)
            {
                if (descendant.name == name)
                    return descendant.gameObject;
            }

            return null;
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

