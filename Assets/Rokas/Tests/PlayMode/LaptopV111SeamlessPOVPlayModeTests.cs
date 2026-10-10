using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class LaptopV111SeamlessPOVPlayModeTests
    {
        private GameObject root;
        private string dir;
        private T Find<T>(string name) where T:Component
        {
            foreach (var v in root.GetComponentsInChildren<T>(true))
                if(v.name == name)return v;
            return null;
        }

        [UnityTest]
        public IEnumerator ExistingPOVFillsFrameAndSurvivesYomiWithoutHubBackground()
        {
            LaptopPowerSession.ResetForTests();
            if (!Resources.Load<TextAsset>("LaptopCinematic/right_hand_manifest") ||
                !Resources.Load<Texture2D>("LaptopCinematic/LaptopPOV_screen_off_APPROVED_CINEMATIC_DOF"))
                Assert.Ignore("Requires explicitly staged art and test-only POV");
            dir=Path.Combine(Path.GetTempPath(),"rokas-v111-pov-"+Guid.NewGuid().ToString("N"));
            root=new GameObject("V111POVTest");
            var boot=root.AddComponent<RokasBootstrap>();
            boot.Initialize(dir);
            yield return null;
            Find<Button>("LaptopHotspot").onClick.Invoke();
            yield return new WaitForSecondsRealtime(.95f);
            var image=Find<RawImage>("UnmodifiedScreenOffPOV");
            Assert.That(image,Is.Not.Null);
            Assert.That(image.rectTransform.rect.width,Is.GreaterThanOrEqualTo(1920f),
                "POV must cover horizontal screen edges without exposing doorway lines");
            Assert.That(image.rectTransform.rect.height,Is.GreaterThanOrEqualTo(1080f),
                "POV must cover vertical screen edges");
            var initial=Find<Text>("StandbyNoSignal");
            Assert.That(initial,Is.Not.Null);
            var standby=Find<RectTransform>("ROKASNoSignalScreen");
            Assert.That(standby,Is.Not.Null,"OFF state must cover physical LCD");
            Assert.That(standby.GetComponent<RectMask2D>(),Is.Not.Null);
            if (Resources.Load<Texture2D>("LaptopCinematic/UI/rokas_no_signal_chibi"))
            {
                var chibi=Find<RawImage>("ROKASNoSignalChibiDVD");
                Assert.That(chibi,Is.Not.Null,"Original ROKAS chibi must appear in LCD");
                Assert.That(chibi.gameObject.activeInHierarchy,Is.True);
                Assert.That(Find<LaptopNoSignalBounce>("ROKASNoSignalScreen"),Is.Not.Null);
            }
            else
                Assert.That(initial.gameObject.activeInHierarchy,Is.True);
            Find<Button>("PowerKeyClickTarget").onClick.Invoke();
            yield return new WaitForSecondsRealtime(2.35f);
            Assert.That(LaptopPowerSession.PoweredOn,Is.True,
                "Power state must latch at the finger-contact timeline");
            Assert.That(boot.View.LaptopOpen,Is.True);
            var pov=Find<RectTransform>("LaptopCinematicOverlay");
            var panels=Find<RectTransform>("Panels");
            Assert.That(pov,Is.Not.Null,"Approved POV remains visible below YOMI");
            Assert.That(panels.GetSiblingIndex(),Is.GreaterThan(pov.parent.GetSiblingIndex()),
                "Fullscreen YOMI must be stacked over the approved POV");
            boot.View.Escape();
            yield return new WaitForSecondsRealtime(.75f);
            Assert.That(boot.View.LaptopOpen,Is.False);
            var live=Find<LaptopPhysicalDesktopClock>("PhysicalDesktopLiveClock");
            Assert.That(live,Is.Not.Null);
            Assert.That(live.gameObject.activeInHierarchy,Is.True);
            var mini=Find<RawImage>("PhysicalMiniYomiVisible");
            Assert.That(mini,Is.Not.Null,"ON laptop must display a real mini YOMI layer");
            Assert.That(mini.gameObject.activeInHierarchy,Is.True);
            Assert.That(mini.texture,Is.Not.Null);
            Assert.That(mini.GetComponentInParent<RectMask2D>(),Is.Not.Null,
                "Mini YOMI must be clipped to the calibrated physical LCD");
            Assert.That(Find<Text>("StandbyNoSignal").gameObject.activeInHierarchy,Is.False);
            var reopen=Find<RectTransform>("PoweredLaptopOpenHint");
            Assert.That(reopen,Is.Not.Null);
            Assert.That(reopen.gameObject.activeInHierarchy,Is.True);
            Assert.That(reopen.GetComponent<LaptopChoiceHover>(),Is.Not.Null,
                "Gold powered-YOMI action shares the hover effect");
            Find<Button>("PhysicalDesktopClickTarget").onClick.Invoke();
            yield return null;
            Assert.That(boot.View.LaptopOpen,Is.True);
            Assert.That(Find<RectTransform>("LaptopCinematicOverlay"),Is.Not.Null);
            Assert.That(LaptopPowerSession.CompletedBoots,Is.EqualTo(1));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if(root)UnityEngine.Object.Destroy(root);
            root=null;
            yield return null;
            LaptopPowerSession.ResetForTests();
            if(!string.IsNullOrEmpty(dir)&&Directory.Exists(dir))Directory.Delete(dir,true);
        }
    }
}
