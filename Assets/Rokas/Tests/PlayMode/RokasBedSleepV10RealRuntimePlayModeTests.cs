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
