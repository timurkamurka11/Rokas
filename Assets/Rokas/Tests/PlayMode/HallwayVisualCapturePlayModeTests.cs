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
    // Actual Unity GameView UI rendering, not simulated or AI-generated reference images.
    public sealed class HallwayVisualCapturePlayModeTests
    {
        private GameObject root;
        private string profileDirectory;
        private RokasBootstrap boot;
        private string outputDirectory;

        [UnityTest]
        public IEnumerator CaptureLiveMainRoomAndHallwayLightingStates()
        {
            profileDirectory = Path.Combine(Path.GetTempPath(), "rokas-hallway-capture-" + Guid.NewGuid().ToString("N"));
            outputDirectory = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "artifacts", "hallway-visual");
            Directory.CreateDirectory(outputDirectory);
            root = new GameObject("HallwayLiveCaptureFixture");
            boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(profileDirectory);
            yield return null;

            Capture("mainroom_hallway_on");
            Press("DoorHotspot");
            yield return null;
            Capture("hallway_fade_transition");
            yield return WaitForLocation(HomeLocation.Hallway);
            Capture("hallway_on");
            Capture("hallway_hotspots");

            boot.Session.SetLamp(false);
            boot.View.Tick(.5f);
            Capture("hallway_mainroom_off");

            boot.Session.SetLamp(true);
            boot.Session.SetHallwayLight(false);
            boot.View.Tick(.5f);
            Capture("hallway_off");

            boot.Session.SetLamp(false);
            boot.View.Tick(.5f);
            Capture("hallway_both_off");

            boot.Session.SetLamp(true);
            boot.Session.SetHallwayLight(true);
            boot.View.Tick(.5f);
            Capture("hallway_front_door");

            boot.Session.SetHallwayLight(false);
            Press("HallwayReturnHotspot");
            yield return WaitForLocation(HomeLocation.MainRoom);
            boot.View.Tick(.5f);
            Capture("mainroom_hallway_off");

            boot.Session.SetLamp(false);
            boot.Session.SetHallwayLight(true);
            boot.View.Tick(.5f);
            Capture("mainroom_off_hallway_on");

            boot.Session.SetHallwayLight(false);
            boot.View.Tick(.5f);
            Capture("mainroom_both_off");
        }


        [UnityTest]
        public IEnumerator CaptureThresholdAndContractReturnProof()
        {
            profileDirectory = Path.Combine(Path.GetTempPath(),
                "rokas-door-seams-" + Guid.NewGuid().ToString("N"));
            outputDirectory = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "artifacts", "hallway-visual");
            Directory.CreateDirectory(outputDirectory);
            root = new GameObject("HallwayDoorSeamCapture");
            boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(profileDirectory);
            yield return null;

            boot.Session.SetLamp(true);
            boot.Session.SetHallwayLight(true);
            boot.View.Tick(.3f);
            Capture("04_mainroom_hallway_on_view");
            Press("DoorHotspot");
            yield return WaitForLocation(HomeLocation.Hallway);
            Capture("01_hallway_threshold_main_on");
            Capture("09_both_on");

            boot.Session.SetLamp(false);
            boot.View.Tick(.5f);
            Capture("02_hallway_threshold_main_off");
            Capture("07_main_off_hall_on");

            boot.Session.SetLamp(true);
            boot.Session.SetHallwayLight(false);
            boot.View.Tick(.5f);
            Capture("08_main_on_hall_off");
            Capture("03_bottom_mask_gap_fixed_closeup",new RectInt(260,610,500,430));

            boot.Session.SetLamp(false);
            boot.View.Tick(.5f);
            Capture("10_both_off");

            boot.Session.SetLamp(true);
            boot.View.Tick(.5f);
            Press("HallwayReturnHotspot");
            yield return WaitForLocation(HomeLocation.MainRoom);
            Capture("05_mainroom_hallway_off_view");
            Capture("06_halo_removed_comparison");

            Press("DoorHotspot");
            yield return WaitForLocation(HomeLocation.Hallway);
            Press("HallwayFrontDoorHotspot");
            yield return WaitForLocation(HomeLocation.MainRoom);
            Assert.That(boot.View.LaptopOpen,Is.True);
            Capture("12_laptop_open_only_in_main");
            boot.View.Escape();
            float deadline=Time.realtimeSinceStartup+3f;
            while(boot.View.LaptopOpen && Time.realtimeSinceStartup<deadline)
                yield return null;
            Assert.That(boot.View.LaptopOpen,Is.False);
            Capture("11_no_contract_return_to_main");

            Assert.That(boot.Session.AcceptContract(),Is.True);
            Press("DoorHotspot");
            yield return WaitForLocation(HomeLocation.Hallway);
            Press("HallwayFrontDoorHotspot");
            float phaseDeadline=Time.realtimeSinceStartup+3f;
            while(boot.Session.State.phase!=RunPhase.Portal &&
                  Time.realtimeSinceStartup<phaseDeadline)
                yield return null;
            Assert.That(boot.Session.State.phase,Is.EqualTo(RunPhase.Portal));
            yield return null;
            Capture("13_contract_selected_exit");
        }

        private IEnumerator WaitForLocation(HomeLocation expected)
        {
            float until = Time.realtimeSinceStartup + 4f;
            while ((boot.View.CurrentHomeLocation != expected ||
                    Find<CanvasGroup>("HomeRoomCurtain") != null) &&
                   Time.realtimeSinceStartup < until)
                yield return null;
            Assert.That(boot.View.CurrentHomeLocation, Is.EqualTo(expected));
            Assert.That(Find<CanvasGroup>("HomeRoomCurtain"), Is.Null);
        }

        private void Press(string buttonName)
        {
            Button button = Find<Button>(buttonName);
            Assert.That(button, Is.Not.Null, buttonName);
            Assert.That(button.IsInteractable(), Is.True, buttonName);
            button.onClick.Invoke();
        }

        private T Find<T>(string objectName) where T : Component
        {
            if (!root) return null;
            foreach (T component in root.GetComponentsInChildren<T>(true))
                if (component.name == objectName) return component;
            return null;
        }

        private void Capture(string filename, RectInt? crop = null)
        {
            Canvas canvas = root.GetComponentInChildren<Canvas>(true);
            RectTransform stage = Find<RectTransform>("AuthoredStage");
            Assert.That(canvas, Is.Not.Null);
            Assert.That(stage, Is.Not.Null);

            GameObject cameraObject = new GameObject("HallwayActualFrameCamera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            const int width = 1920, height = 1080;
            RenderTexture render = new RenderTexture(width, height, 24);
            render.Create();
            camera.targetTexture = render;

            RenderMode priorMode = canvas.renderMode;
            Camera priorCamera = canvas.worldCamera;
            float priorDistance = canvas.planeDistance;
            Vector3 priorScale = stage.localScale;
            RenderTexture priorActive = RenderTexture.active;

            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                stage.localScale = Vector3.one;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = render;

                Texture2D pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                try
                {
                    pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    pixels.Apply();
                    string destination = Path.Combine(outputDirectory, filename + ".png");
                    if (crop.HasValue)
                    {
                        RectInt region = crop.Value;
                        Texture2D closeup = new Texture2D(region.width,region.height,
                            TextureFormat.RGB24,false);
                        try
                        {
                            closeup.SetPixels(pixels.GetPixels(region.x,
                                height-region.y-region.height,region.width,region.height));
                            closeup.Apply();
                            File.WriteAllBytes(destination,closeup.EncodeToPNG());
                        }
                        finally { UnityEngine.Object.DestroyImmediate(closeup); }
                    }
                    else File.WriteAllBytes(destination,pixels.EncodeToPNG());
                    Assert.That(new FileInfo(destination).Length, Is.GreaterThan(1000),
                        "Capture must contain actual encoded Unity output.");
                    TestContext.WriteLine("HALLWAY_CAPTURE " + destination);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(pixels);
                }
            }
            finally
            {
                canvas.renderMode = priorMode;
                canvas.worldCamera = priorCamera;
                canvas.planeDistance = priorDistance;
                stage.localScale = priorScale;
                RenderTexture.active = priorActive;
                camera.targetTexture = null;
                render.Release();
                UnityEngine.Object.DestroyImmediate(render);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root) UnityEngine.Object.Destroy(root);
            root = null;
            boot = null;
            yield return null;
            if (!string.IsNullOrEmpty(profileDirectory) && Directory.Exists(profileDirectory))
                Directory.Delete(profileDirectory, true);
        }
    }
}
