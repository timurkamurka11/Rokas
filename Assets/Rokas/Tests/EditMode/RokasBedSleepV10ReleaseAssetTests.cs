using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Rokas.Tests
{
    /// <summary>
    /// Native Unity asset gate for the user-supplied V10 GitHub Release. Does not
    /// touch original production Hub/Laptop/V3 BedSleep prefabs. The first clean
    /// run builds genuine imported candidate prefabs; later runs only validate.
    /// </summary>
    public sealed class RokasBedSleepV10ReleaseAssetTests
    {
        const string Root = "Assets/Rokas/Resources/BedSleep/V10ReleaseCandidate/";
        const string ModelDir = "Assets/Rokas/BedSleep/V10Release/Models/";
        const string MatDir = "Assets/Rokas/BedSleep/V10Release/Materials/";

        [Test]
        public void RealBlenderSkinnedArmAndSevenPartWatchGenerateValidUnityPrefabs()
        {
            var originalArm = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "Keiko_FPS_Arm_Rigged.fbx");
            var originalWatch = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "ROKAS_Smartwatch_HighDetail.fbx");
            Assert.NotNull(originalArm, "True Blender FBX arm was not imported into AssetDatabase");
            Assert.NotNull(originalWatch, "True seven-part Blender FBX watch was not imported into AssetDatabase");

            string[] names =
            {
                "PF_Keiko_FPS_Arm_V10_Candidate.prefab",
                "PF_ROKAS_Smartwatch_V10_Candidate.prefab",
                "PF_BedSleep_3DComposite_V10_Candidate.prefab"
            };
            var arm = AssetDatabase.LoadAssetAtPath<GameObject>(Root + names[0]);
            if (arm == null)
            {
                foreach (var other in names.Skip(1))
                    Assert.IsNull(AssetDatabase.LoadMainAssetAtPath(Root + other),
                        "Partially generated candidate found; no file may be silently overwritten");
                var type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("Rokas.BedSleep.V10Release.Editor.ROKASBedSleepV10MaterialSetup", false))
                    .FirstOrDefault(t => t != null);
                Assert.NotNull(type, "V10Release imported PBR material bootstrap missing");
                var method = type.GetMethod("BuildAllForCloudCI", BindingFlags.Static | BindingFlags.Public);
                Assert.NotNull(method);
                try { method.Invoke(null, null); }
                catch (TargetInvocationException ex)
                {
                    throw new AssertionException("Real Unity prefab build failed: " +
                        (ex.InnerException ?? ex).ToString());
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            arm = AssetDatabase.LoadAssetAtPath<GameObject>(Root + names[0]);
            var watch = AssetDatabase.LoadAssetAtPath<GameObject>(Root + names[1]);
            var composite = AssetDatabase.LoadAssetAtPath<GameObject>(Root + names[2]);
            Assert.NotNull(arm, "No generated arm prefab");
            Assert.NotNull(watch, "No generated watch prefab");
            Assert.NotNull(composite, "No generated RenderTexture composite prefab");

            var skinned = arm.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .OrderByDescending(x => x.sharedMesh == null ? 0 : x.sharedMesh.vertexCount).FirstOrDefault();
            Assert.NotNull(skinned, "No real skinned renderer");
            Assert.NotNull(skinned.sharedMesh, "No physical arm mesh");
            Assert.Greater(skinned.sharedMesh.vertexCount, 9000, "Imported hand became procedural placeholder");
            Assert.AreEqual(25, skinned.bones.Length, "Imported authored 25-bone hierarchy lost");
            Assert.AreEqual(25, skinned.sharedMesh.bindposes.Length);
            Assert.AreEqual(3, skinned.sharedMesh.subMeshCount, "Real skin/sleeve/nails material groups missing");
            Assert.AreEqual(3, skinned.sharedMaterials.Length);
            Assert.IsTrue(skinned.sharedMaterials.All(x => x != null && x.shader != null));

            var parts = watch.GetComponentsInChildren<MeshFilter>(true)
                .Where(x => x.sharedMesh != null).ToArray();
            Assert.GreaterOrEqual(parts.Length, 7, "Real seven 3D watch parts not imported");
            Assert.GreaterOrEqual(new HashSet<Mesh>(parts.Select(x => x.sharedMesh)).Count, 7,
                "Watch components reuse the same mesh instead of seven separate parts");
            Assert.IsTrue(parts.Any(x => x.name.StartsWith("Watch_DigitalCrown_Rotary", StringComparison.Ordinal)));
            Assert.IsTrue(parts.Any(x => x.name.StartsWith("Watch_OLED_Screen", StringComparison.Ordinal)));

            var skinMat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "M_Keiko_Skin.mat");
            Assert.NotNull(skinMat);
            Assert.NotNull(skinMat.mainTexture, "Skin texture missing, no fake flat stand-in");
            Assert.IsTrue(composite.GetComponents<Component>()
                .Any(x => x != null && x.GetType().Name == "BedSleepVisualComposite"));
        }
    }
}
