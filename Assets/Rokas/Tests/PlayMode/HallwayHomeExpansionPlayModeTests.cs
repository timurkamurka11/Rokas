using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class HallwayHomeExpansionPlayModeTests
    {
        private GameObject root;
        private string directory;
        private RokasBootstrap bootstrap;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(),
                "rokas-hallway-home-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("HallwayHomeExpansionFixture");
            bootstrap = root.AddComponent<RokasBootstrap>();
            bootstrap.Initialize(directory);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MainDoorOpensHallwayAndLeftOpeningReturnsHome()
        {
            Assert.That(bootstrap.View.CurrentHomeLocation, Is.EqualTo(HomeLocation.MainRoom));
            Assert.That(Find<Button>("DoorHotspot"), Is.Not.Null);

            Find<Button>("DoorHotspot").onClick.Invoke();
            yield return WaitForLocation(HomeLocation.Hallway);

            Assert.That(bootstrap.Session.State.phase, Is.EqualTo(RunPhase.Home),
                "Walking into the hallway must not start the external route.");
            Assert.That(HallwayView.LoadBackground(), Is.Not.Null,
                "The supplied hallway reference must be committed as a runtime Resource.");

            RawImage world = Find<RawImage>("WorldIllustration");
            Assert.That(world, Is.Not.Null);
            Assert.That(world.texture, Is.SameAs(HallwayView.LoadBackground()));
            Assert.That(HallwayView.LoadBackground().width, Is.EqualTo(1920),
                "The full-resolution hallway reference must be used, not a downscaled 1024px placeholder.");
            Assert.That(HallwayView.LoadBackground().height, Is.EqualTo(1080));
            Assert.That(world.uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)),
                "The approved 16:9 asset must fill the stage without stretching or further cropping.");

            Assert.That(Find<Button>("HallwayReturnHotspot"), Is.Not.Null);
            Assert.That(Find<Button>("HallwayFrontDoorHotspot"), Is.Not.Null);
            Assert.That(Find<Button>("HallwayLightHotspot"), Is.Not.Null);
            RectTransform returnMarker = Find<RectTransform>("HallwayReturnHotspotMarker");
            Assert.That(returnMarker, Is.Not.Null);
            RectTransform returnGlyph = returnMarker.Find("ScanGlyph") as RectTransform;
            Assert.That(returnGlyph, Is.Not.Null);
            Assert.That(returnGlyph.pivot, Is.EqualTo(new Vector2(.5f, .5f)),
                "Rotating around the original top-left pivot displaced the exit icon outside its badge.");
            Assert.That(Find<Button>("LaptopHotspot"), Is.Null,
                "Hallway currently exposes exactly its three intended interactions.");
            Assert.That(Find<Button>("WorkbenchHotspot"), Is.Null,
                "Workbench must remain Laptop-only.");

            Find<Button>("HallwayReturnHotspot").onClick.Invoke();
            yield return WaitForLocation(HomeLocation.MainRoom);
            Assert.That(Find<Button>("DoorHotspot"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator FrontDoorReusesExistingAcceptedContractPortalRoute()
        {
            Assert.That(bootstrap.Session.AcceptContract(), Is.True);
            Assert.That(bootstrap.Session.State.phase, Is.EqualTo(RunPhase.Accepted));

            Find<Button>("DoorHotspot").onClick.Invoke();
            yield return WaitForLocation(HomeLocation.Hallway);
            Assert.That(bootstrap.Session.State.phase, Is.EqualTo(RunPhase.Accepted));

            Find<Button>("HallwayFrontDoorHotspot").onClick.Invoke();
            yield return WaitForPhase(RunPhase.Portal);
            yield return null; // Unity destroys the previous frame's UI at end of frame.

            Assert.That(bootstrap.Session.State.phase, Is.EqualTo(RunPhase.Portal),
                "Hallway front door must call the existing LeaveHome route.");
            Assert.That(Find<Button>("HallwayFrontDoorHotspot"), Is.Null,
                "Hallway UI must be gone once the canonical Portal state owns the screen.");
        }

        [UnityTest]
        public IEnumerator RoomTransitionUsesOneShortCurtainWithoutJourneyCaption()
        {
            Find<Button>("DoorHotspot").onClick.Invoke();
            yield return null;

            Assert.That(Find<CanvasGroup>("HomeRoomCurtain"), Is.Not.Null);
            Assert.That(Count<CanvasGroup>("HomeRoomCurtain"), Is.EqualTo(1),
                "Room movement must use one fade curtain.");
            Assert.That(Find<Text>("JourneyCaption"), Is.Null,
                "Apartment room movement must not show external-travel text.");

            yield return WaitForLocation(HomeLocation.Hallway);
            yield return WaitForMissing<CanvasGroup>("HomeRoomCurtain");
            Assert.That(Find<CanvasGroup>("HomeRoomCurtain"), Is.Null);
        }

        [UnityTest]
        public IEnumerator FourIndependentLightStatesAreCrossVisible()
        {
            Find<Button>("DoorHotspot").onClick.Invoke();
            yield return WaitForLocation(HomeLocation.Hallway);

            SetLights(true, true);
            AssertLightGroups(ownOff: false, mainOff: false);

            SetLights(false, true);
            AssertLightGroups(ownOff: false, mainOff: true);

            SetLights(true, false);
            AssertLightGroups(ownOff: true, mainOff: false);

            SetLights(false, false);
            AssertLightGroups(ownOff: true, mainOff: true);

            Find<Button>("HallwayReturnHotspot").onClick.Invoke();
            yield return WaitForLocation(HomeLocation.MainRoom);
            bootstrap.View.Tick(.5f);

            HomeDoorwayPhotoGraphic doorway = Find<HomeDoorwayPhotoGraphic>("MainRoomHallwayPhotographicPortal");
            Assert.That(doorway, Is.Not.Null,
                "Main Room must display the real Hallway light state inside the right doorway.");
            Assert.That(doorway.mainTexture, Is.SameAs(
                Resources.Load<Texture2D>("Home/ApartmentNightLightOff")),
                "Hallway OFF must select the authored unlit doorway photo.");

            Assert.That(bootstrap.Session.State.lampOn, Is.False);
            Assert.That(bootstrap.Session.HallwayLightOn, Is.False);
        }

        [UnityTest]
        public IEnumerator HallwaySwitchIsIndependentAndSurvivesHomeRoundTrip()
        {
            Assert.That(bootstrap.Session.HallwayLightOn, Is.True,
                "Fresh and legacy saves should start with the hallway illuminated.");
            bool originalMainRoomLight = bootstrap.Session.State.lampOn;

            Find<Button>("DoorHotspot").onClick.Invoke();
            yield return WaitForLocation(HomeLocation.Hallway);

            Button hallwaySwitch = Find<Button>("HallwayLightHotspot");
            Assert.That(hallwaySwitch, Is.Not.Null);
            hallwaySwitch.onClick.Invoke();
            yield return null;

            Assert.That(bootstrap.Session.HallwayLightOn, Is.False);
            Assert.That(bootstrap.Session.State.lampOn, Is.EqualTo(originalMainRoomLight),
                "Hallway switch must not affect the Main Room light.");

            Find<Button>("HallwayReturnHotspot").onClick.Invoke();
            yield return WaitForLocation(HomeLocation.MainRoom);
            Assert.That(bootstrap.Session.HallwayLightOn, Is.False);
            Assert.That(Find<HomeDoorwayPhotoGraphic>("MainRoomHallwayPhotographicPortal").mainTexture,
                Is.SameAs(Resources.Load<Texture2D>("Home/ApartmentNightLightOff")));

            Find<Button>("DoorHotspot").onClick.Invoke();
            yield return WaitForLocation(HomeLocation.Hallway);
            Assert.That(bootstrap.Session.HallwayLightOn, Is.False);
            RawImage hallwayImage = Find<RawImage>("WorldIllustration");
            Assert.That(hallwayImage.material.GetFloat("_HallwayOn"), Is.LessThan(.01f),
                "Hallway OFF must already render its unlit photo after the room fade.");

            Find<Button>("HallwayLightHotspot").onClick.Invoke();
            yield return null;
            Assert.That(bootstrap.Session.HallwayLightOn, Is.True);
            Assert.That(bootstrap.Session.State.lampOn, Is.EqualTo(originalMainRoomLight));
        }


        [UnityTest]
        public IEnumerator NoContractDoorReturnsHomeBeforeOneLaptopOpens()
        {
            Assert.That(bootstrap.Session.State.phase, Is.EqualTo(RunPhase.Home));
            bootstrap.Session.SetLamp(false);
            bootstrap.Session.SetHallwayLight(true);
            Find<Button>("DoorHotspot").onClick.Invoke();
            yield return WaitForLocation(HomeLocation.Hallway);

            Button exit = Find<Button>("HallwayFrontDoorHotspot");
            Assert.That(exit, Is.Not.Null);
            exit.onClick.Invoke();
            // The first click begins the existing room fade, never the Laptop.
            Assert.That(bootstrap.View.LaptopOpen, Is.False);
            Assert.That(bootstrap.View.CurrentHomeLocation, Is.EqualTo(HomeLocation.Hallway));
            Assert.That(Count<CanvasGroup>("HomeRoomCurtain"), Is.EqualTo(1));
            exit.onClick.Invoke(); // Repeated input must not schedule another return.
            Assert.That(Count<CanvasGroup>("HomeRoomCurtain"), Is.EqualTo(1));
            yield return WaitForLocation(HomeLocation.MainRoom);
            Assert.That(bootstrap.View.LaptopOpen, Is.True);
            Assert.That(bootstrap.Session.State.phase, Is.EqualTo(RunPhase.Home));
            Assert.That(Count<RectTransform>("YomiLaptop"), Is.EqualTo(1));
            Assert.That(Find<Button>("HallwayFrontDoorHotspot"), Is.Null);
            Assert.That(bootstrap.Session.State.lampOn, Is.False);
            Assert.That(bootstrap.Session.HallwayLightOn, Is.True);

            bootstrap.View.Escape();
            float until = Time.realtimeSinceStartup + 3f;
            while (bootstrap.View.LaptopOpen && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.That(bootstrap.View.LaptopOpen, Is.False);
            Assert.That(bootstrap.View.CurrentHomeLocation, Is.EqualTo(HomeLocation.MainRoom));
            Assert.That(Find<Button>("DoorHotspot"), Is.Not.Null);

            Find<Button>("DoorHotspot").onClick.Invoke();
            yield return WaitForLocation(HomeLocation.Hallway);
            Assert.That(bootstrap.Session.State.lampOn, Is.False);
            Assert.That(bootstrap.Session.HallwayLightOn, Is.True);
        }

        [UnityTest]
        public IEnumerator MainRoomDoorwayRenderedPolygonExcludesWallCurtainAndFloor()
        {
            // Measured authored 1920x1080 pixel bounds. Applies equally at QHD
            // because AuthoredStage scales as a whole rather than changing UVs.
            Assert.That(HomeDoorwayPhotoGraphic.CoverageAt(1650f,230f),
                Is.GreaterThan(.95f), "Lit Hallway must appear INSIDE the upper doorway.");
            Assert.That(HomeDoorwayPhotoGraphic.CoverageAt(1628f,76f),
                Is.GreaterThan(.95f), "OFF light must cover the upper-left inner door corner.");
            Assert.That(HomeDoorwayPhotoGraphic.CoverageAt(1618f,85f),
                Is.EqualTo(0f), "Do not darken the outer left wooden door frame.");
            Assert.That(HomeDoorwayPhotoGraphic.CoverageAt(1670f,565f),
                Is.GreaterThan(.95f), "Lower exposed Hallway must be visible.");
            Assert.That(HomeDoorwayPhotoGraphic.CoverageAt(1480f,250f),
                Is.EqualTo(0f), "Adjacent wall must never get the Hallway photo.");
            Assert.That(HomeDoorwayPhotoGraphic.CoverageAt(1750f,180f),
                Is.EqualTo(0f), "Hanging curtain must stay opaque.");
            Assert.That(HomeDoorwayPhotoGraphic.CoverageAt(1705f,220f),
                Is.GreaterThan(.95f), "Hallway strip beside the curtain must not remain warm.");
            Assert.That(HomeDoorwayPhotoGraphic.CoverageAt(1725f,220f),
                Is.EqualTo(0f), "Upper fabric must never be made transparent.");
            Assert.That(HomeDoorwayPhotoGraphic.CoverageAt(1730f,385f),
                Is.EqualTo(0f), "Photo must not tint the photographed curtain hem.");
            Assert.That(HomeDoorwayPhotoGraphic.CoverageAt(1730f,425f),
                Is.GreaterThan(.95f), "Exposed Hallway below the curtain must remain visible.");
            // The lower opening is not horizontal: it follows the real
            // photographed wood sill, approximately (1618,623) -> (1740,645).
            Assert.That(HomeDoorwayPhotoGraphic.CoverageAt(1660f,619f),
                Is.GreaterThan(.95f), "The lower left door floor must reach the sill.");
            Assert.That(HomeDoorwayPhotoGraphic.CoverageAt(1700f,626f),
                Is.GreaterThan(.95f), "The center floor should not retain the old warm strip.");
            Assert.That(HomeDoorwayPhotoGraphic.CoverageAt(1730f,631f),
                Is.GreaterThan(.95f), "The lower right door floor must not stop at y=618.");
            Assert.That(HomeDoorwayPhotoGraphic.CoverageAt(1746f,535f),
                Is.GreaterThan(.95f), "OFF state must cover the last visible strip on the right.");
            Assert.That(HomeDoorwayPhotoGraphic.CoverageAt(1745f,632f),
                Is.GreaterThan(.95f), "Lower-right photo must reach the sloped sill.");
            Assert.That(HomeDoorwayPhotoGraphic.CoverageAt(1765f,545f),
                Is.EqualTo(0f), "The widened photo must not cover the outside doorframe.");
            Assert.That(HomeDoorwayPhotoGraphic.CoverageAt(1660f,640f),
                Is.EqualTo(0f), "Do not repaint the Main Room floor outside the sill.");
            Assert.That(HomeDoorwayPhotoGraphic.CoverageAt(1730f,660f),
                Is.EqualTo(0f), "Never paint beyond the sloped wood threshold.");
            Assert.That(HomeDoorwayPhotoGraphic.CoverageAt(1690f,680f),
                Is.EqualTo(0f), "MainRoom floor outside the opening must not change.");
            Assert.That(HomeDoorwayPhotoGraphic.CoverageAt(1580f,450f),
                Is.EqualTo(0f), "Door jamb must not be overwritten.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator HomeRainAndOutsidePhotoCoverGlazingAtBothLightStates()
        {
            RawImage world = Find<RawImage>("WorldIllustration");
            RawImage outside = Find<RawImage>("HomeOutsideParallax");
            RawImage composite = Find<RawImage>("HomeWeatherComposite");
            RawImage glazing = Find<RawImage>("HomeGlazingMask");
            RawImage droplets = Find<RawImage>("HomeWetGlassDroplets");
            RawImage frameBottom = Find<RawImage>("FrameBottom");
            RectTransform outsideClip = Find<RectTransform>("HomeOutsideDepthMask");
            RectTransform rainClip = Find<RectTransform>("WindowRain");
            Assert.That(world, Is.Not.Null);
            Assert.That(outside, Is.Not.Null);
            Assert.That(composite, Is.Not.Null);
            Assert.That(glazing, Is.Not.Null);
            Assert.That(droplets, Is.Not.Null);
            Assert.That(frameBottom, Is.Not.Null);
            Assert.That(outsideClip, Is.Not.Null);
            Assert.That(rainClip, Is.Not.Null);

            Assert.That(outsideClip.sizeDelta.y, Is.EqualTo(428f).Within(.01f));
            Assert.That(rainClip.sizeDelta.y, Is.EqualTo(428f).Within(.01f));
            Assert.That(glazing.rectTransform.sizeDelta.y, Is.EqualTo(428f).Within(.01f));
            Assert.That(composite.rectTransform.sizeDelta.y, Is.EqualTo(428f).Within(.01f));
            Assert.That(droplets.rectTransform.sizeDelta.y, Is.EqualTo(428f).Within(.01f));
            Assert.That(outsideClip.anchoredPosition.y, Is.EqualTo(-107f).Within(.01f));
            Assert.That(frameBottom.rectTransform.anchoredPosition.y, Is.EqualTo(-503f).Within(.01f));

            for (int k = 0; k < 2; k++)
            {
                bool lampOn = k == 0;
                bootstrap.Session.SetLamp(lampOn);
                bootstrap.View.Tick(.25f);
                Assert.That(outside.texture, Is.SameAs(world.texture),
                    "Parallax and main art must use the same ON/OFF photograph.");
                Assert.That(frameBottom.texture, Is.SameAs(world.texture));
                Assert.That(composite.gameObject.activeInHierarchy, Is.True);
            }
            yield return null;
        }

        private void SetLights(bool mainOn, bool hallwayOn)
        {
            bootstrap.Session.SetLamp(mainOn);
            bootstrap.Session.SetHallwayLight(hallwayOn);
            for (int i = 0; i < 4; i++) bootstrap.View.Tick(.25f);
        }

        private void AssertLightGroups(bool ownOff, bool mainOff)
        {
            RawImage image = Find<RawImage>("WorldIllustration");
            Assert.That(image, Is.Not.Null);
            Assert.That(image.material, Is.Not.Null);
            Assert.That(image.material.shader.name, Is.EqualTo("ROKAS/UI/HallwayLightStates"));
            Assert.That(HallwayLightPresentation.OffPhoto, Is.Not.Null,
                "Unlit user-approved photograph is required for the two-state composite.");
            Assert.That(HallwayLightPresentation.OffPhoto.width, Is.EqualTo(1920));
            Assert.That(HallwayLightPresentation.OffPhoto.height, Is.EqualTo(1080));
            Assert.That(image.material.GetTexture("_OffTex"), Is.SameAs(HallwayLightPresentation.OffPhoto));
            Assert.That(image.material.GetFloat("_HallwayOn"),
                ownOff ? Is.LessThan(.05f) : Is.GreaterThan(.95f));
            Assert.That(image.material.GetFloat("_MainRoomOn"),
                mainOff ? Is.LessThan(.05f) : Is.GreaterThan(.95f));

            Assert.That(Find<CanvasGroup>("HallwayOwnLightOffMask"), Is.Null,
                "The old circular/rectangular patches must not be instantiated.");
            Assert.That(Find<CanvasGroup>("HallwayMainRoomOffMask"), Is.Null);
        }

        [UnityTest]
        public IEnumerator MainRoomDoorwayUsesHallwayPhotographIndependently()
        {
            RawImage world = Find<RawImage>("WorldIllustration");
            Texture mainOn = world.texture;
            Texture mainOff = Resources.Load<Texture2D>("Home/ApartmentNightLightOff");
            Assert.That(mainOn, Is.Not.Null);
            Assert.That(mainOff, Is.Not.Null);

            bootstrap.Session.SetLamp(false);
            bootstrap.Session.SetHallwayLight(true);
            bootstrap.View.Tick(.25f);

            var doorway = Find<HomeDoorwayPhotoGraphic>("MainRoomHallwayPhotographicPortal");
            Assert.That(doorway, Is.Not.Null);
            Assert.That(world.texture, Is.SameAs(mainOff));
            Assert.That(doorway.mainTexture, Is.SameAs(mainOn),
                "Hallway ON must remain lit inside the MainRoom doorway, even when MainRoom is OFF.");

            bootstrap.Session.SetHallwayLight(false);
            bootstrap.View.Tick(.25f);
            Assert.That(world.texture, Is.SameAs(mainOff));
            Assert.That(doorway.mainTexture, Is.SameAs(mainOff));

            bootstrap.Session.SetLamp(true);
            bootstrap.View.Tick(.25f);
            Assert.That(world.texture, Is.SameAs(mainOn));
            Assert.That(doorway.mainTexture, Is.SameAs(mainOff),
                "MainRoom ON must not turn on the Hallway doorway.");
            yield return null;
        }

        private IEnumerator WaitForLocation(HomeLocation location)
        {
            float deadline = Time.realtimeSinceStartup + 3.5f;
            while ((bootstrap.View.CurrentHomeLocation != location ||
                    Find<CanvasGroup>("HomeRoomCurtain") != null) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(bootstrap.View.CurrentHomeLocation, Is.EqualTo(location));
            Assert.That(Find<CanvasGroup>("HomeRoomCurtain"), Is.Null);
        }

        private IEnumerator WaitForPhase(RunPhase expected)
        {
            float deadline = Time.realtimeSinceStartup + 2.5f;
            while (bootstrap.Session.State.phase != expected &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(bootstrap.Session.State.phase, Is.EqualTo(expected));
        }

        private IEnumerator WaitForMissing<T>(string name) where T : Component
        {
            float deadline = Time.realtimeSinceStartup + 2.5f;
            while (Find<T>(name) != null && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        private T Find<T>(string name) where T : Component
        {
            if (!root) return null;
            foreach (T item in root.GetComponentsInChildren<T>(true))
                if (item.name == name) return item;
            return null;
        }

        private int Count<T>(string name) where T : Component
        {
            int count = 0;
            if (!root) return count;
            foreach (T item in root.GetComponentsInChildren<T>(true))
                if (item.name == name) count++;
            return count;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root) UnityEngine.Object.Destroy(root);
            root = null;
            bootstrap = null;
            yield return null;
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}
