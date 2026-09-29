using System.Collections.Generic;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEditor;
using UnityEngine;

namespace Rokas.Tests
{
    public sealed class ReactiveSwordAssetEditModeTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void AuthoredSwordClipBindsFullyWithoutWorldDriftOrAudioEvents(bool heavy)
        {
            var library = Resources.Load<ReactiveCombatActorLibrary>("Combat/ReactiveCombatActorLibrary");
            AnimationClip clip = heavy ? library.keiko.heavy : library.keiko.attack;
            Assert.That(clip.legacy, Is.True);
            Assert.That(clip.isLooping, Is.False);
            Assert.That(clip.events, Is.Empty);
            var paths = new HashSet<string> { string.Empty };
            foreach (Transform child in library.keiko.model.GetComponentsInChildren<Transform>(true))
                paths.Add(AnimationUtility.CalculateTransformPath(child, library.keiko.model.transform));
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            Assert.That(bindings.Length, Is.GreaterThan(160));
            foreach (var binding in bindings)
            {
                Assert.That(paths.Contains(binding.path), Is.True, binding.path);
                Assert.That(binding.path == "mixamorig:Hips" &&
                    (binding.propertyName == "m_LocalPosition.x" || binding.propertyName == "m_LocalPosition.z"),
                    Is.False, "Only the arena controller may add horizontal travel.");
            }
            string source = "Assets/Rokas/Art/CombatActors/Keiko/" + (heavy ? "Hard jump attack.fbx" : "Normal attack.fbx");
            var importer = (ModelImporter)AssetImporter.GetAtPath(source);
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Legacy));
            Assert.That(importer.optimizeGameObjects, Is.False);
            Assert.That(importer.clipAnimations.Length, Is.EqualTo(1));
            Assert.That(importer.clipAnimations[0].lastFrame, Is.EqualTo(heavy ? 57f : 38f));
        }

        [Test]
        public void ReusableWeaponPrefabResolvesItsMeshMaterialAndTexture()
        {
            var library = Resources.Load<ReactiveCombatActorLibrary>("Combat/ReactiveCombatActorLibrary");
            GameObject weapon = library.keiko.weaponPrefab;
            Assert.That(weapon, Is.Not.Null);
            Assert.That(weapon.GetComponentsInChildren<SkinnedMeshRenderer>(true), Is.Empty);
            foreach (Transform node in weapon.GetComponentsInChildren<Transform>(true))
                foreach (Component component in node.GetComponents<Component>())
                    Assert.That(component, Is.Not.Null, "No Missing Script is allowed in the weapon.");
            var renderer = weapon.GetComponentInChildren<MeshRenderer>(true);
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
            Assert.That(renderer.sharedMaterial, Is.Not.Null);
            Assert.That(renderer.sharedMaterial.mainTexture, Is.Not.Null);
            string[] dependencies = AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(weapon));
            Assert.That(dependencies, Has.Some.EndsWith("metal+sword+3d+model.fbx"));
            Assert.That(dependencies, Has.Some.EndsWith("metal+sword+3d+model_basecolor.jpg"));
            Assert.That(library.keiko.model.transform.Find(library.keiko.weaponBonePath), Is.Not.Null);
        }
    }
}
