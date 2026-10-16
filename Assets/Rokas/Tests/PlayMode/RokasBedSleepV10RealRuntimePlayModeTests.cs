using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    /// <summary>
    /// Real Unity PlayMode gate, not a YAML/prefab/compile-only check.
    /// Click the actual HOME bed hotspot and reject any silent fallback to the
    /// legacy flat picture when authentic V10 resources are present.
    /// Does not modify production save data on disk.
    /// </summary>
    public sealed class RokasBedSleepV10RealRuntimePlayModeTests
    {
        private GameObject bootstrapRoot;
        private string tempSaveDirectory;

        [UnityTest]
        public IEnumerator ClickingRealHomeBedHotspotActivatesLiveV10SkinnedRenderTexture()
        {
            const string resource = "BedSleep/V10ReleaseCandidate/PF_BedSleep_3DComposite_V10_Candidate";
            var prefab = Resources.Load<GameObject>(resource);
            Assert.That(prefab, Is.Not.Null,
                "V10 composite prefab must be accessible in a built player Resources path.");

            tempSaveDirectory = Path.Combine(Path.GetTempPath(),
                "rokas-v10-runtime-smoke-" + Guid.NewGuid().ToString("N"));
            bootstrapRoot = new GameObject("ROKAS_V10_Bed_RealPlayModeSmoke");
            var boot = bootstrapRoot.AddComponent<RokasBootstrap>();
            boot.Initialize(tempSaveDirectory);
            yield return null;

            var bed = bootstrapRoot.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(x => x.name == "BedInteractable_Hotspot");
            Assert.That(bed, Is.Not.Null, "Actual existing HOME bed hotspot not found.");
            bed.onClick.Invoke();

            // Begin() creates the 3D rig synchronously, but wait one frame so
            // MonoBehaviour initialisation and the true RenderTexture complete.
            yield return null;

            var roots = UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject rig = roots.FirstOrDefault(x =>
                x.name == "ROKAS_BedSleep_V10_RealFBX_Rig");
            Assert.That(rig, Is.Not.Null,
                "Clicking bed fell back to V3/flat PNG instead of creating the REAL V10 FBX rig.");

            var smr = rig.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .FirstOrDefault(x => x.sharedMesh != null && x.sharedMesh.vertexCount > 9000);
            Assert.That(smr, Is.Not.Null, "Real skinned hand mesh missing at runtime.");
            Assert.That(smr.bones, Has.Length.EqualTo(25));
            Assert.That(smr.sharedMesh.subMeshCount, Is.EqualTo(3),
                "Skin, sleeve and nail surfaces must all be real submeshes.");
            Assert.That(smr.sharedMaterials.Length, Is.EqualTo(3));
            foreach (var material in smr.sharedMaterials)
                Assert.That(material, Is.Not.Null, "Missing one of three actual hand materials.");

            var watchParts = new HashSet<Mesh>();
            foreach (var meshFilter in rig.GetComponentsInChildren<MeshFilter>(true))
                if (meshFilter.sharedMesh != null) watchParts.Add(meshFilter.sharedMesh);
            Assert.That(watchParts.Count, Is.GreaterThanOrEqualTo(7),
                "Seven independent imported watch meshes were lost at runtime.");

            var composite = rig.GetComponents<MonoBehaviour>().FirstOrDefault(x =>
                x != null && x.GetType().FullName ==
                    "Rokas.BedSleep.V10Release.BedSleepVisualComposite");
            Assert.That(composite, Is.Not.Null,
                "The real prefab lacks an active V10 runtime composite script.");
            var type = composite.GetType();
            var rt = type.GetProperty("CompositeRenderTexture",
                BindingFlags.Public | BindingFlags.Instance)?.GetValue(composite) as RenderTexture;
            Assert.That(rt, Is.Not.Null, "V10 RenderTexture failed to initialize.");
            Assert.That(rt.IsCreated(), Is.True);
            Assert.That(rt.width, Is.EqualTo(1920));
            Assert.That(rt.height, Is.EqualTo(1080));

            var camera = type.GetProperty("PresentationCamera",
                BindingFlags.Public | BindingFlags.Instance)?.GetValue(composite) as Camera;
            Assert.That(camera, Is.Not.Null, "Real presentation camera missing.");
            Assert.That(camera.targetTexture, Is.SameAs(rt));
            Assert.That(camera.cullingMask, Is.EqualTo(1 << 30),
                "3D camera must only render isolated imported hand/watch meshes.");

            // Ensure the game actually shows a live composited texture and
            // controls, not only an invisible prefab in the scene.
            var liveImage = bootstrapRoot.GetComponentsInChildren<RawImage>(true)
                .FirstOrDefault(x => x.texture == rt);
            Assert.That(liveImage, Is.Not.Null,
                "V10 RenderTexture is not connected to the active Bed Sleep UI.");
            Assert.That(liveImage.gameObject.activeInHierarchy, Is.True);

            yield return new WaitForSecondsRealtime(1.8f);
            Assert.That(rig != null && rig.activeInHierarchy, Is.True);
            Assert.That(camera.targetTexture, Is.SameAs(rt));

            Debug.Log("ROKAS_V10_REAL_BED_RUNTIME_PASS: HOME hotspot -> genuine 25-bone " +
                "FBX hand + seven independent watch meshes + 3 PBR groups + live RenderTexture.");
        }

        /// <summary>
        /// Unlike the old smoke test, this actually clicks Set Alarm, enters
        /// TimeSelection, renders the real camera, and rejects a blank transparent
        /// RenderTexture / off-screen watch. The regression reported by the
        /// player was invisible despite passing all earlier V10 prefab tests.
        /// </summary>
        [UnityTest]
        public IEnumerator SetAlarmActuallyRendersVisibleArmAndOLEDInHome()
        {
            tempSaveDirectory = Path.Combine(Path.GetTempPath(),
                "rokas-v10-visibility-" + Guid.NewGuid().ToString("N"));
            bootstrapRoot = new GameObject("ROKAS_V10_Visibility_RealHome");
            var boot = bootstrapRoot.AddComponent<RokasBootstrap>();
            boot.Initialize(tempSaveDirectory);
            yield return null;

            var bed = bootstrapRoot.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(x => x.name == "BedInteractable_Hotspot");
            Assert.That(bed, Is.Not.Null, "Missing real HOME bed hotspot");
            bed.onClick.Invoke();
            yield return new WaitForSecondsRealtime(2.5f);

            var alarm = bootstrapRoot.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(x => x.name == "SetAlarm");
            Assert.That(alarm, Is.Not.Null, "Missing Set Alarm button");
            Assert.That(alarm.interactable, Is.True, "Alarm button must be enabled in HOME");
            alarm.onClick.Invoke();
            yield return new WaitForSecondsRealtime(3.0f);
            // Unity batchmode never invokes WaitForEndOfFrame; Camera.Render()
            // below performs the explicit isolated 3D frame for pixel QA.
            yield return null;

            var rig = UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(x => x.name == "ROKAS_BedSleep_V10_RealFBX_Rig");
            Assert.That(rig, Is.Not.Null, "3D rig not created");
            var composite = rig.GetComponents<MonoBehaviour>().FirstOrDefault(x =>
                x != null && x.GetType().FullName ==
                    "Rokas.BedSleep.V10Release.BedSleepVisualComposite");
            Assert.That(composite, Is.Not.Null, "3D composite not created");
            var type = composite.GetType();
            var rt = type.GetProperty("CompositeRenderTexture")?.GetValue(composite)
                as RenderTexture;
            var camera = type.GetProperty("PresentationCamera")?.GetValue(composite)
                as Camera;
            Assert.That(rt, Is.Not.Null, "Missing 3D RenderTexture");
            Assert.That(camera, Is.Not.Null, "Missing 3D camera");
            Assert.That(camera.enabled, Is.True);
            Assert.That(camera.targetTexture, Is.SameAs(rt));

            var raw = bootstrapRoot.GetComponentsInChildren<RawImage>(true)
                .FirstOrDefault(x => x.texture == rt);
            Assert.That(raw, Is.Not.Null, "3D RenderTexture not connected to UI");
            Assert.That(raw.gameObject.activeInHierarchy, Is.True);
            Assert.That(raw.color.a, Is.GreaterThan(0.99f),
                "3D RawImage is still invisible after Set Alarm");

            var arm = rig.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.That(arm, Is.Not.Null, "Missing 3D arm renderer");
            var allWatches = rig.GetComponentsInChildren<MeshRenderer>(true);
            var oled = allWatches.FirstOrDefault(x => x.name.Contains("Watch_OLED_Screen"));
            Assert.That(oled, Is.Not.Null, "Missing real OLED renderer");
            Assert.That(arm.enabled && arm.gameObject.activeInHierarchy, Is.True);
            Assert.That(oled.enabled && oled.gameObject.activeInHierarchy, Is.True);
            Assert.That(oled.sharedMaterial, Is.Not.Null);
            Assert.That(oled.sharedMaterial.mainTexture, Is.InstanceOf<RenderTexture>(),
                "The OLED has no live digital display bound to its real imported mesh.");
            if (oled.sharedMaterial.HasProperty("_Color"))
                Assert.That(oled.sharedMaterial.color.r, Is.GreaterThan(0.9f),
                    "Dark PBR color must not multiply live OLED digits nearly to black.");

            // Both actual rendered surfaces must intersect the real camera.
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            Assert.That(GeometryUtility.TestPlanesAABB(planes, arm.bounds), Is.True,
                "Real hand bounds are outside the presentation camera frustum");
            Assert.That(GeometryUtility.TestPlanesAABB(planes, oled.bounds), Is.True,
                "Real OLED bounds are outside the presentation camera frustum");

            // Check pixels, not only C# object presence. Re-render the exact
            // camera used by the hub into its actual 1920x1080 RenderTexture.
            camera.Render();
            var scaled = RenderTexture.GetTemporary(320, 180, 0,
                RenderTextureFormat.ARGB32);
            Texture2D pixels = new Texture2D(320, 180, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            int visible = 0;
            int brightOledPixels = 0;
            int minX = 320, minY = 180, maxX = -1, maxY = -1;
            try
            {
                Graphics.Blit(rt, scaled);
                RenderTexture.active = scaled;
                pixels.ReadPixels(new Rect(0, 0, 320, 180), 0, 0);
                pixels.Apply();
                var framePixels = pixels.GetPixels32();
                for (int index = 0; index < framePixels.Length; index++)
                {
                    var p = framePixels[index];
                    if (p.a > 24 && (p.r > 14 || p.g > 14 || p.b > 14))
                    {
                        if (p.b > 115 && p.g > 92 && p.b > p.r)
                            brightOledPixels++;
                        visible++;
                        int x = index % 320;
                        int y = index / 320;
                        minX = Mathf.Min(minX, x);
                        minY = Mathf.Min(minY, y);
                        maxX = Mathf.Max(maxX, x);
                        maxY = Mathf.Max(maxY, y);
                    }
                }

                // Preserve actual visual evidence for Actions even if the
                // regression assertion fails.
                string output = Path.Combine(Application.dataPath, "..",
                    "Artifacts", "BedSleepVisibility");
                Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output, "watch-3d-visible-rt.png"),
                    pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(scaled);
                UnityEngine.Object.Destroy(pixels);
            }

            int renderedWidth = Mathf.Max(0, maxX - minX + 1);
            int renderedHeight = Mathf.Max(0, maxY - minY + 1);
            Debug.Log("ROKAS_V10_VISIBILITY: opaque-colored pixels=" + visible +
                "/57600 bright-OLED-pixels=" + brightOledPixels +
                " silhouette=" + renderedWidth + "x" + renderedHeight +
                " skin=" + arm.bounds + " oled=" + oled.bounds +
                " camera=" + camera.transform.position +
                " alpha=" + raw.color.a);
            Assert.That(visible, Is.GreaterThan(100),
                "Bed Sleep returned a blank 3D image even though the arm/watch prefabs exist.");
            Assert.That(brightOledPixels, Is.GreaterThan(5),
                "OLED screen remains visually dark: live watch graphics must be luminous.");
            Assert.That(renderedHeight, Is.GreaterThan(65),
                "Real arm must be prominent from the lower POV, not a miniature horizontal strip.");
            Assert.That(renderedHeight, Is.GreaterThan(renderedWidth * 0.65f),
                "FPS forearm must rise toward the watch instead of lying horizontally across the screen.");
        }

        [TearDown]
        public void Cleanup()
        {
            if (bootstrapRoot != null)
                UnityEngine.Object.DestroyImmediate(bootstrapRoot);
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (go != null && go.name == "ROKAS_BedSleep_V10_RealFBX_Rig")
                    UnityEngine.Object.DestroyImmediate(go);
            if (!string.IsNullOrEmpty(tempSaveDirectory) &&
                Directory.Exists(tempSaveDirectory))
            {
                try { Directory.Delete(tempSaveDirectory, true); }
                catch (IOException) { /* temporary files may still be open in Unity */ }
            }
        }
    }
}
