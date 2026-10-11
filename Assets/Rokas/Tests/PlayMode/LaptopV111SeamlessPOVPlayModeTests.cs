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
            // An art-integrated Unity test must never accept fallback text in place of the original assets.
            foreach (string name in new[] { "rokas_no_signal_chibi", "prompt_open",
                "prompt_back", "prompt_power", "back_arrow", "prompt_stand_r" })
                Assert.That(Resources.Load<Texture2D>("LaptopCinematic/UI/" + name),
                    Is.Not.Null, "Original approved PNG missing from Unity Resources: " + name);
            Assert.That(Resources.Load<AudioClip>("LaptopCinematic/chibi_bounce"), Is.Not.Null,
                "User MP3 must be imported as actual Unity AudioClip, not a synthesized replacement");
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
            Assert.That(standby.GetComponent<Mask>(), Is.Not.Null,
                "Standby chibi must be clipped by the actual four-corner LCD stencil");
            Assert.That(standby.GetComponent<LaptopPerspectiveQuad>(), Is.Not.Null,
                "OFF state uses the real asymmetric LCD polygon, not just a rectangle");
            var poweredViewport = Find<RectTransform>("PhysicalMiniYomiViewport");
            Assert.That(poweredViewport, Is.Not.Null);
            Assert.That(poweredViewport.GetComponent<Mask>(), Is.Not.Null,
                "ON state must use the same four-corner LCD stencil");
            Assert.That(poweredViewport.GetComponent<LaptopPerspectiveQuad>(), Is.Not.Null);
            Assert.That(poweredViewport.rect.width, Is.EqualTo(standby.rect.width).Within(.01f));
            Assert.That(poweredViewport.rect.height, Is.EqualTo(standby.rect.height).Within(.01f));
            Assert.That(standby.rect.width, Is.GreaterThan(400f),
                "LCD must fill the physical screen instead of stopping before lower-right bezel");
            var chibi=Find<RawImage>("ROKASNoSignalChibiDVD");
            Assert.That(chibi,Is.Not.Null,"Original chibi must be imported and shown");
            Assert.That(chibi.texture,
                Is.SameAs(Resources.Load<Texture2D>("LaptopCinematic/UI/rokas_no_signal_chibi")));
            Assert.That(chibi.gameObject.activeInHierarchy,Is.True);
            Assert.That(initial.gameObject.activeInHierarchy,Is.False,
                "Fallback NO SIGNAL text is not acceptable in art-integrated CI");
            Assert.That(Find<LaptopNoSignalBounce>("ROKASNoSignalScreen"),Is.Not.Null);
            Vector2 before=chibi.rectTransform.anchoredPosition;
            yield return new WaitForSecondsRealtime(.20f);
            Vector2 after=chibi.rectTransform.anchoredPosition;
            Assert.That(Vector2.Distance(before,after),Is.GreaterThan(2f));
            Assert.That(Find<RawImage>("PowerChoiceHint").texture,
                Is.SameAs(Resources.Load<Texture2D>("LaptopCinematic/UI/prompt_power")));
            Assert.That(Find<RawImage>("BackChoiceHint").texture,
                Is.SameAs(Resources.Load<Texture2D>("LaptopCinematic/UI/prompt_back")));
            Assert.That(Find<RawImage>("LaptopBackChoice").texture,
                Is.SameAs(Resources.Load<Texture2D>("LaptopCinematic/UI/back_arrow")));
            var originalPower = Find<RawImage>("PowerChoiceHint");
            var originalBack = Find<RawImage>("BackChoiceHint");
            const float bottomLine = 1045f;
            Assert.That(-originalPower.rectTransform.anchoredPosition.y + originalPower.rectTransform.rect.height,
                Is.EqualTo(bottomLine).Within(.1f), "E Power aligned on gold-button baseline");
            Assert.That(-originalBack.rectTransform.anchoredPosition.y + originalBack.rectTransform.rect.height,
                Is.EqualTo(bottomLine).Within(.1f), "ESC Back aligned on gold-button baseline");
            Assert.That(originalPower, Is.Not.Null);
            Assert.That(originalBack, Is.Not.Null);
            Assert.That(originalPower.GetComponentInChildren<RawImage>().color.a, Is.EqualTo(1f));
            Assert.That(originalPower.transform.Find("OriginalGoldLuminance"), Is.Not.Null,
                "Original user PNG keeps full alpha plus luminance layer");
            Find<Button>("PowerKeyClickTarget").onClick.Invoke();
            yield return null;
            Assert.That(chibi.gameObject.activeInHierarchy, Is.True,
                "Chibi must NOT disappear on input, only at boot video handoff");
            Assert.That(originalPower.gameObject.activeInHierarchy, Is.True,
                "Power prompt must remain visible during hand reach");
            Assert.That(originalBack.gameObject.activeInHierarchy, Is.True,
                "Back prompt must remain visible during hand reach");
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
            bool hasExactYomi = LaptopYomiMirror.Current != null;
            Assert.That(live.gameObject.activeInHierarchy,Is.EqualTo(!hasExactYomi),
                "Real full-YOMI screenshot must replace the simulated 640x360 clock atlas");
            var mini=Find<RawImage>("PhysicalMiniYomiVisible");
            Assert.That(mini,Is.Not.Null,"ON laptop must display a real mini YOMI layer");
            Assert.That(mini.gameObject.activeInHierarchy,Is.True);
            Assert.That(mini.texture,Is.Not.Null);
            if (hasExactYomi)
            {
                Assert.That(mini.texture, Is.SameAs(LaptopYomiMirror.Current),
                    "Mini LCD must display exactly the captured real fullscreen YOMI pixels");
                Assert.That(mini.rectTransform.rect.width / mini.rectTransform.rect.height,
                    Is.EqualTo((float)mini.texture.width / mini.texture.height).Within(.012f),
                    "Physical LCD must preserve true image aspect instead of stretching");
            }
            var atlas=mini.texture as Texture2D;
            Assert.That(atlas,Is.Not.Null);
            var colors=atlas.GetPixels32();
            int populated=0;
            for(int i=0;i<colors.Length;i++)
                if(colors[i].r>85 && colors[i].g>55 && colors[i].b>55)populated++;
            Assert.That(populated,Is.GreaterThan(700),
                "Mini YOMI must carry readable colored app UI, not a black texture");
            Assert.That(mini.GetComponentInParent<RectMask2D>(),Is.Not.Null,
                "Mini YOMI must be clipped to the calibrated physical LCD");
            for (int i=0;i<9;i++)
            {
                var icon=Find<LaptopIcon>("PhysicalMiniYomiAppIcon_"+i);
                Assert.That(icon,Is.Not.Null,
                    "The mini desktop must use the SAME vector glyphs as YOMI.");
                Assert.That(icon.gameObject.activeInHierarchy,Is.EqualTo(!hasExactYomi),
                    "Do not overlay synthetic app drawings on captured real YOMI image");
            }
            Assert.That(Find<Text>("StandbyNoSignal").gameObject.activeInHierarchy,Is.False);
            var reopen=Find<RectTransform>("PoweredLaptopOpenHint");
            Assert.That(reopen,Is.Not.Null);
            Assert.That(reopen.gameObject.activeInHierarchy,Is.True);
            var standArt = Find<RawImage>("PoweredStandOriginalPNG");
            Assert.That(standArt, Is.Not.Null,
                "Use exact user-provided E — ВСТАТЬ button rather than old arrow");
            Assert.That(standArt.texture, Is.SameAs(Resources.Load<Texture2D>("LaptopCinematic/UI/prompt_stand_r")),
                "Only exact owner-supplied R — ВСТАТЬ PNG is accepted");
            Assert.That(-standArt.rectTransform.anchoredPosition.y + standArt.rectTransform.rect.height,
                Is.EqualTo(bottomLine).Within(.1f));
            Assert.That(standArt.gameObject.activeInHierarchy, Is.True);
            Assert.That(standArt.color, Is.EqualTo(Color.white),
                "Do not dim/retint original golden PNG");
            Assert.That(standArt.GetComponent<LaptopChoiceHover>(), Is.Not.Null);
            Assert.That(standArt.transform.Find("OriginalGoldLuminance"), Is.Not.Null);
            Assert.That(Find<RawImage>("LaptopBackChoice").gameObject.activeInHierarchy, Is.False,
                "Old standalone arrow must disappear only after laptop is powered");
            Assert.That(Find<Button>("PoweredStandOriginalPNG"), Is.Not.Null);
            var open = Find<RawImage>("ExactGoldenOpenPrompt");
            Assert.That(open.texture,
                Is.SameAs(Resources.Load<Texture2D>("LaptopCinematic/UI/prompt_open")));
            Assert.That(-open.transform.parent.GetComponent<RectTransform>().anchoredPosition.y +
                open.rectTransform.rect.height, Is.EqualTo(bottomLine).Within(.1f),
                "R Stand, E Open and ESC Back must all share one lower line");
            var fourCorner = Find<LaptopPerspectiveQuad>("PhysicalExactYomiPerspective");
            Assert.That(fourCorner, Is.Not.Null,
                "YOMI must map all four texture edges directly onto the LCD quad rather than cover-cropping a RawImage");
            Assert.That(fourCorner.GetComponent<CanvasRenderer>(), Is.Not.Null);
            Assert.That(Find<LaptopChoiceHover>("ExactGoldenOpenPrompt"),Is.Not.Null,
                "Original gold powered-YOMI PNG must have hover on its own Graphic");
            Find<Button>("PhysicalDesktopClickTarget").onClick.Invoke();
            yield return null;
            Assert.That(boot.View.LaptopOpen,Is.True);
            Assert.That(Find<RectTransform>("LaptopCinematicOverlay"),Is.Not.Null);
            Assert.That(LaptopPowerSession.CompletedBoots,Is.EqualTo(1));

            // Close fullscreen YOMI again, then ACTUALLY click the new PNG.
            // The original standing/cancel sequence must restore the room.
            boot.View.Escape();
            yield return new WaitForSecondsRealtime(.75f);
            Assert.That(boot.View.LaptopOpen,Is.False);
            var stand = Find<Button>("PoweredStandOriginalPNG");
            Assert.That(stand,Is.Not.Null);
            Assert.That(stand.gameObject.activeInHierarchy,Is.True);
            stand.onClick.Invoke();
            yield return null;
            Assert.That(Find<RectTransform>("PhysicalMiniYomiViewport").gameObject.activeInHierarchy,
                Is.True, "Powered LCD must remain alive during standing transition; no black flash");
            yield return new WaitForSecondsRealtime(1.0f);
            Assert.That(Find<RectTransform>("LaptopCinematicOverlay"),Is.Null,
                "Stand control must run the real return-to-room animation and exit seated POV");
            var hotSpot = Find<Button>("LaptopHotspot");
            Assert.That(hotSpot, Is.Not.Null);
            hotSpot.onClick.Invoke();
            yield return null;
            Assert.That(Find<RectTransform>("PhysicalMiniYomiViewport").gameObject.activeInHierarchy,
                Is.True, "Powered LCD must be visible from the first frame of reseating");
        }

        [UnityTest]
        public IEnumerator ChibiCollisionCallbackFiresOnRealBoundaryBounce()
        {
            var area = new GameObject("BounceArea", typeof(RectTransform)).GetComponent<RectTransform>();
            area.sizeDelta = new Vector2(70f, 70f);
            var sticker = new GameObject("Sticker", typeof(RectTransform)).GetComponent<RectTransform>();
            sticker.SetParent(area, false);
            sticker.sizeDelta = new Vector2(20f, 20f);
            int sounds = 0;
            var mover = area.gameObject.AddComponent<LaptopNoSignalBounce>();
            mover.Initialize(area, sticker, () => sounds++);
            yield return new WaitForSecondsRealtime(1.70f);
            Assert.That(sounds, Is.GreaterThan(0), "Wall collision must trigger user SFX callback");
            Assert.That(mover.CollisionCount, Is.EqualTo(sounds),
                "Exactly one bounce SFX event per collision/update, including corners");
            UnityEngine.Object.Destroy(area.gameObject);
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
