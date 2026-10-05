using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class WorkbenchUiPlayModeTests
    {
        private GameObject root;

        [UnityTest]
        public IEnumerator WorkbenchSwitchesCardsAndArrowsThroughOneAuthoritative3DPresentation()
        {
            int baselinePreviewTextures = Resources.FindObjectsOfTypeAll<RenderTexture>()
                .Count(texture => texture && texture.name == "WorkbenchWeaponPreviewRT");

            root = new GameObject("WorkbenchUiFixture");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            root.AddComponent<CanvasScaler>();
            root.AddComponent<GraphicRaycaster>();
            if (!EventSystem.current) root.AddComponent<EventSystem>();

            var stageObject = new GameObject("Stage", typeof(RectTransform));
            var stage = (RectTransform)stageObject.transform;
            stage.SetParent(root.transform, false);
            stage.anchorMin = stage.anchorMax = new Vector2(0, 1);
            stage.pivot = new Vector2(0, 1);
            stage.sizeDelta = new Vector2(1744, 812);

            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            var ui = new UiKit(assets, null);
            var state = new SaveData { phase = RunPhase.Home, yen = 2000, weaponLevel = 1 };
            var session = new GameSession(state, new ContractDefinition());
            bool closed = false;
            var view = new LaptopWorkbenchView(ui, session, (action, _) => action());

            view.Build(stage, () => closed = true);
            yield return new WaitForSecondsRealtime(.28f);

            Button twoHanded = FindButton("WorkbenchWeaponTwoHanded");
            Button dagger = FindButton("WorkbenchWeaponDagger");
            Button previous = FindButton("WorkbenchPreviousWeapon");
            Button next = FindButton("WorkbenchNextWeapon");
            Button upgrade = FindButton("UpgradeWeapon");
            WorkbenchWeaponPreview3D preview = Find("WorkbenchWeaponPreview").GetComponent<WorkbenchWeaponPreview3D>();

            Assert.That(twoHanded, Is.Not.Null);
            Assert.That(dagger, Is.Not.Null);
            Assert.That(previous, Is.Not.Null);
            Assert.That(next, Is.Not.Null);
            Assert.That(upgrade, Is.Not.Null);
            Assert.That(preview, Is.Not.Null);
            Assert.That(preview.HasModel, Is.True);
            Assert.That(preview.CurrentKind, Is.EqualTo(WorkbenchWeaponKind.TwoHanded));
            Assert.That(preview.CurrentVertexCount, Is.GreaterThanOrEqualTo(500));
            Assert.That(preview.CurrentTriangleCount, Is.GreaterThan(900));
            Assert.That(preview.CurrentSource, Does.Contain(WorkbenchWeaponMeshLibrary.SuppliedFbxName));
            Assert.That(preview.ActivePreviewCameraCount, Is.EqualTo(1));
            Assert.That(preview.HasCreatedRenderTexture, Is.True);
            Assert.That(preview.RenderTextureWidth, Is.EqualTo(640));
            Assert.That(preview.RenderTextureHeight, Is.EqualTo(640));
            Assert.That(FindText("WorkbenchInfoCategory").text, Is.EqualTo("ДВУРУЧНИК"));
            Assert.That(upgrade.IsInteractable(), Is.True);
            Assert.That(Find("WorkbenchHud").GetComponent<RectTransform>().pivot, Is.EqualTo(new Vector2(.5f, .5f)));
            Assert.That(Find("WorkbenchWeaponPreview").GetComponent<RectTransform>().pivot, Is.EqualTo(new Vector2(.5f, .5f)));
            Assert.That(previous.GetComponent<WorkbenchTechButtonFeedback>(), Is.Not.Null);
            Assert.That(next.GetComponent<WorkbenchTechButtonFeedback>(), Is.Not.Null);
            Assert.That(previous.GetComponentInChildren<WorkbenchArrowGraphic>(), Is.Not.Null);
            Assert.That(next.GetComponentInChildren<WorkbenchArrowGraphic>(), Is.Not.Null);

            preview.RenderNow();
            Capture("01-twohanded");

            WorkbenchTechButtonFeedback leftFeedback = previous.GetComponent<WorkbenchTechButtonFeedback>();
            leftFeedback.OnPointerEnter(new PointerEventData(EventSystem.current));
            yield return new WaitForSecondsRealtime(.08f);
            Capture("02-left-arrow-hover");
            leftFeedback.OnPointerExit(new PointerEventData(EventSystem.current));

            WorkbenchTechButtonFeedback rightFeedback = next.GetComponent<WorkbenchTechButtonFeedback>();
            rightFeedback.OnPointerEnter(new PointerEventData(EventSystem.current));
            yield return new WaitForSecondsRealtime(.08f);
            Capture("03-right-arrow-hover");

            next.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.06f);
            Capture("04-switch-midpoint");
            yield return new WaitForSecondsRealtime(.30f);
            rightFeedback.OnPointerExit(new PointerEventData(EventSystem.current));

            Assert.That(FindText("WorkbenchInfoCategory").text, Is.EqualTo("КИНЖАЛ"));
            Assert.That(preview.CurrentKind, Is.EqualTo(WorkbenchWeaponKind.Dagger));
            Assert.That(preview.HasModel, Is.True);
            Assert.That(preview.CurrentVertexCount, Is.GreaterThan(150));
            Assert.That(preview.ActiveRendererCount, Is.EqualTo(1), "A completed switch must keep exactly one 3D weapon renderer.");
            Assert.That(upgrade.IsInteractable(), Is.False,
                "Dagger must not fabricate an upgrade path that the domain model does not own.");
            Capture("05-dagger");

            previous.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.32f);
            Assert.That(FindText("WorkbenchInfoCategory").text, Is.EqualTo("ДВУРУЧНИК"));
            Assert.That(preview.CurrentKind, Is.EqualTo(WorkbenchWeaponKind.TwoHanded));
            Assert.That(preview.ActiveRendererCount, Is.EqualTo(1));
            Assert.That(preview.ActivePreviewCameraCount, Is.EqualTo(1));

            dagger.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.32f);
            Assert.That(FindText("WorkbenchInfoCategory").text, Is.EqualTo("КИНЖАЛ"));
            Assert.That(preview.CurrentKind, Is.EqualTo(WorkbenchWeaponKind.Dagger),
                "Weapon card and arrows must drive the same selected state.");

            twoHanded.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.32f);
            Assert.That(FindText("WorkbenchInfoCategory").text, Is.EqualTo("ДВУРУЧНИК"));
            Assert.That(preview.CurrentKind, Is.EqualTo(WorkbenchWeaponKind.TwoHanded));
            Assert.That(upgrade.IsInteractable(), Is.True);

            // Rapid input must converge on one authoritative selection without stacking preview rigs.
            for (int index = 0; index < 7; index++)
            {
                next.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.015f);
            }
            yield return new WaitForSecondsRealtime(.36f);
            Assert.That(FindText("WorkbenchInfoCategory").text, Is.EqualTo("КИНЖАЛ"));
            Assert.That(preview.CurrentKind, Is.EqualTo(WorkbenchWeaponKind.Dagger));
            Assert.That(preview.ActiveRendererCount, Is.EqualTo(1));
            Assert.That(preview.ActivePreviewCameraCount, Is.EqualTo(1));

            twoHanded.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.32f);
            Assert.That(preview.CurrentKind, Is.EqualTo(WorkbenchWeaponKind.TwoHanded));
            Assert.That(upgrade.IsInteractable(), Is.True);

            upgrade.onClick.Invoke();
            Assert.That(session.State.weaponLevel, Is.EqualTo(2),
                "The CTA must call the existing GameSession upgrade path instead of a local UI counter.");
            Assert.That(session.State.yen, Is.EqualTo(1700));
            Assert.That(FindText("WorkbenchInfoLevel").text, Does.Contain("2"),
                "Open Workbench must refresh its visible level immediately after a successful upgrade.");
            Assert.That(FindButton("UpgradeWeapon").GetComponentInChildren<Text>().text, Does.Contain("600"),
                "The next authoritative upgrade cost must refresh without closing the screen.");
            Capture("06-upgraded-info-tiers-cta");

            Assert.That(root.GetComponentsInChildren<Canvas>(true), Has.Length.EqualTo(1));
            FindButton("WorkbenchClose").onClick.Invoke();
            Assert.That(closed, Is.True);

            UnityEngine.Object.Destroy(root);
            root = null;
            yield return null;
            int remainingPreviewTextures = Resources.FindObjectsOfTypeAll<RenderTexture>()
                .Count(texture => texture && texture.name == "WorkbenchWeaponPreviewRT");
            Assert.That(remainingPreviewTextures, Is.EqualTo(baselinePreviewTextures),
                "Destroying the Workbench must release its private RenderTexture and preview rig.");
            LogAssert.NoUnexpectedReceived();
        }

        private void Capture(string name)
        {
            string output = Environment.GetEnvironmentVariable("ROKAS_WORKBENCH_CAPTURE_DIR");
            if (string.IsNullOrEmpty(output) ||
                SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;

            Canvas canvas = root.GetComponent<Canvas>();
            RectTransform stage = Find("Stage").GetComponent<RectTransform>();
            WorkbenchWeaponPreview3D preview = Find("WorkbenchWeaponPreview")?.GetComponent<WorkbenchWeaponPreview3D>();
            preview?.RenderNow();

            RenderMode oldMode = canvas.renderMode;
            Camera oldCamera = canvas.worldCamera;
            Vector3 oldScale = stage.localScale;
            var cameraObject = new GameObject("WorkbenchCaptureCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            var target = new RenderTexture(1744, 812, 24);
            var pixels = new Texture2D(1744, 812, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 406;
                camera.nearClipPlane = .01f;
                camera.farClipPlane = 100;
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                stage.localScale = Vector3.one;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1744, 812), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = oldMode;
                canvas.worldCamera = oldCamera;
                stage.localScale = oldScale;
                RenderTexture.active = previous;
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(pixels);
                UnityEngine.Object.Destroy(cameraObject);
                Canvas.ForceUpdateCanvases();
            }
        }

        private Button FindButton(string name)
        {
            GameObject item = Find(name);
            return item ? item.GetComponent<Button>() : null;
        }

        private Text FindText(string name)
        {
            GameObject item = Find(name);
            return item ? item.GetComponent<Text>() : null;
        }

        private GameObject Find(string name)
        {
            if (!root) return null;
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                if (item.name == name) return item.gameObject;
            return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root) UnityEngine.Object.Destroy(root);
            yield return null;
        }
    }
}
