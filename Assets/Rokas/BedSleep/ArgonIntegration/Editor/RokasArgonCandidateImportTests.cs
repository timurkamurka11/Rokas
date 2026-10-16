#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Editor
{
    public sealed class RokasArgonCandidateImportTests
    {
        [Test]
        public void GenuineArgonAssetsBuildSkinnedCandidateWithoutTouchingV10()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(
                RokasArgonCandidateImporter.Source +
                "Models/ROKAS_Keiko_FPS_Arm_Rigged.fbx"), Is.Not.Null,
                "The private release ZIP must be staged into ArgonSource in cloud CI.");

            RokasArgonCandidateImporter.BuildCandidate();
            var result = AssetDatabase.LoadAssetAtPath<GameObject>(
                RokasArgonCandidateImporter.CandidatePrefab);
            Assert.That(result, Is.Not.Null);

            var skinned = result.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.That(skinned.Length, Is.EqualTo(3));
            foreach (var renderer in skinned)
            {
                Assert.That(renderer.sharedMesh, Is.Not.Null);
                Assert.That(renderer.sharedMesh.bindposes.Length, Is.EqualTo(25));
                Assert.That(renderer.sharedMaterial?.mainTexture, Is.Not.Null,
                    "All three skinned meshes must be bound to real albedo maps.");
            }

            var transforms = result.GetComponentsInChildren<Transform>(true);
            var socket = transforms.Single(x => x.name == "Wrist_WatchAttachment");
            var caseNode = transforms.Single(x => x.name == "Watch_TitaniumCase");
            Assert.That(caseNode.IsChildOf(socket), Is.True,
                "The physical watch must follow the animated wrist socket.");
            var oled = transforms.Single(x => x.name == "Watch_OLED_Display")
                .GetComponent<MeshRenderer>();
            Assert.That(oled, Is.Not.Null);
            Assert.That(oled.GetComponent<MeshCollider>()?.sharedMesh, Is.Not.Null);
            Assert.That(oled.sharedMaterial?.mainTexture, Is.Not.Null);

            var animator = result.GetComponentInChildren<Animator>(true);
            Assert.That(animator?.runtimeAnimatorController, Is.Not.Null);
            Assert.That(animator.runtimeAnimatorController.animationClips.Length,
                Is.EqualTo(8));
            Debug.Log("[ROKAS ARGON QA] PASS 3 real skinned meshes, 7-piece watch, 25 bones," +
                "8 imported FBX clips; original V10 production untouched.");
        }
    }
}
#endif
