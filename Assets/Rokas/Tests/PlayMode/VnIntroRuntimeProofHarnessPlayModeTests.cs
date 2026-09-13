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

        [UnityTest]
        public IEnumerator RuntimeProofRecognizesNestedPolishedVnHierarchy()
        {
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            VnIntroArt art = new VnIntroArt(
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

                Assert.That(RokasVnIntroRuntimeProofRunner.HasRequiredVnUi(root), Is.True,
                    "The real-player proof must recognize the approved nested polished VN hierarchy.");
            }
            finally
            {
                view.Dispose();
                Object.Destroy(host);
            }
        }
    }
}
