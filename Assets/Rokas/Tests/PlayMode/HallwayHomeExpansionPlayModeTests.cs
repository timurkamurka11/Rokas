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

            CanvasGroup neighbor = Find<CanvasGroup>("MainRoomHallwayOffMask");
            Assert.That(neighbor, Is.Not.Null);
            Assert.That(neighbor.alpha, Is.GreaterThan(.70f),
                "Main Hub must visually retain Hallway OFF through its right-side doorway region.");

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
            Assert.That(Find<CanvasGroup>("MainRoomHallwayOffMask").alpha, Is.GreaterThan(.70f));

            Find<Button>("DoorHotspot").onClick.Invoke();
            yield return WaitForLocation(HomeLocation.Hallway);
            Assert.That(bootstrap.Session.HallwayLightOn, Is.False);
            Assert.That(Find<CanvasGroup>("HallwayOwnLightOffMask").alpha, Is.GreaterThan(.70f),
                "Hallway OFF must already be visible on the first frame after a completed fade.");

            Find<Button>("HallwayLightHotspot").onClick.Invoke();
            yield return null;
            Assert.That(bootstrap.Session.HallwayLightOn, Is.True);
            Assert.That(bootstrap.Session.State.lampOn, Is.EqualTo(originalMainRoomLight));
        }

        private void SetLights(bool mainOn, bool hallwayOn)
        {
            bootstrap.Session.SetLamp(mainOn);
            bootstrap.Session.SetHallwayLight(hallwayOn);
            for (int i = 0; i < 4; i++) bootstrap.View.Tick(.25f);
        }

        private void AssertLightGroups(bool ownOff, bool mainOff)
        {
            CanvasGroup own = Find<CanvasGroup>("HallwayOwnLightOffMask");
            CanvasGroup main = Find<CanvasGroup>("HallwayMainRoomOffMask");
            Assert.That(own, Is.Not.Null);
            Assert.That(main, Is.Not.Null);
            Assert.That(own.alpha, ownOff ? Is.GreaterThan(.70f) : Is.LessThan(.15f));
            Assert.That(main.alpha, mainOff ? Is.GreaterThan(.70f) : Is.LessThan(.15f));

            Assert.That(Find<Graphic>("CeilingPracticalDim"), Is.Not.Null);
            Assert.That(Find<Graphic>("CabinetPracticalDim"), Is.Not.Null);
            Assert.That(Find<Graphic>("EntryWarmDim"), Is.Not.Null);
            Assert.That(Find<Graphic>("VisibleMainRoomDim"), Is.Not.Null);
            Assert.That(Find<Graphic>("HallwayGlobalBlackOverlay"), Is.Null,
                "Hallway OFF must use localized light treatment, not a global black rectangle.");
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
