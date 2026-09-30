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
            string source = "Assets/Rokas/Art/CombatActors/Keiko/" + (heavy ? "Hard jump attack corrected.fbx" : "Normal attack corrected.fbx");
            var importer = (ModelImporter)AssetImporter.GetAtPath(source);
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Legacy));
            Assert.That(importer.optimizeGameObjects, Is.False);
            Assert.That(importer.clipAnimations.Length, Is.EqualTo(1));
            Assert.That(importer.clipAnimations[0].firstFrame, Is.Zero);
            Assert.That(importer.clipAnimations[0].lastFrame, Is.EqualTo(heavy ? 172f : 132f));
            Assert.That(importer.animationCompression, Is.EqualTo(ModelImporterAnimationCompression.Off));
            var animatedSupportArmPaths = new HashSet<string>();
            foreach (EditorCurveBinding binding in bindings)
                if (binding.path.EndsWith("mixamorig:LeftArm") ||
                    binding.path.EndsWith("mixamorig:LeftForeArm") ||
                    binding.path.EndsWith("mixamorig:LeftHand"))
                    if (binding.propertyName.Contains("Rotation") || binding.propertyName.Contains("Euler"))
                        animatedSupportArmPaths.Add(binding.path);
            Assert.That(animatedSupportArmPaths.Count, Is.EqualTo(3),
                "The FBX take must author the support arm and palm, including recovery.");
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
            Assert.That(weapon.transform.Find("LeftHandGrip"), Is.Not.Null);
        }

        [TestCase("Guard")]
        [TestCase("Dodge")]
        [TestCase("EntranceWalk")]
        [TestCase("EntrySettle")]
        [TestCase("ClawNormal")]
        [TestCase("ClawHeavy")]
        [TestCase("ClawLocomotion")]
        public void PolishTwoTakesBindToExistingRigWithOneHorizontalMovementOwner(string take)
        {
            var library = Resources.Load<ReactiveCombatActorLibrary>("Combat/ReactiveCombatActorLibrary");
            bool enemy = take.StartsWith("Claw");
            ReactiveCombatActorClips actor = enemy ? library.yokai : library.keiko;
            AnimationClip clip = take == "Guard" ? actor.guard : take == "Dodge" ? actor.dodge :
                take == "EntranceWalk" ? actor.entranceWalk : take == "EntrySettle" ? actor.enterBattle :
                take == "ClawNormal" ? actor.attack : take == "ClawHeavy" ? actor.heavy : actor.approach;
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.legacy, Is.True);
            Assert.That(clip.events, Is.Empty, "Presentation has a single explicit event owner.");
            Assert.That(AssetDatabase.GetAssetPath(clip), Does.EndWith(".anim"));
            var paths = new HashSet<string> { string.Empty };
            foreach (Transform bone in actor.model.GetComponentsInChildren<Transform>(true))
                paths.Add(AnimationUtility.CalculateTransformPath(bone, actor.model.transform));
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                Assert.That(paths.Contains(binding.path), Is.True, take + ":" + binding.path);
                Assert.That(binding.path == "mixamorig:Hips" &&
                    (binding.propertyName == "m_LocalPosition.x" || binding.propertyName == "m_LocalPosition.z"),
                    Is.False, "Arena owns horizontal travel, including emergence and defence.");
            }
            Assert.That(clip.isLooping, Is.EqualTo(take == "EntranceWalk" || take == "ClawLocomotion"));
            if (take == "ClawNormal")
                Assert.That(clip.length * actor.attackContactNormalized, Is.EqualTo(.55f).Within(.01f));
            if (take == "ClawHeavy")
                Assert.That(clip.length * actor.heavyContactNormalized, Is.EqualTo(.8f).Within(.01f));
        }
    }
}
