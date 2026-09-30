using System.Collections;
using System.IO;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rokas.Tests
{
    public sealed class ReactiveSwordPlayModeTests
    {
        [UnityTest]
        public IEnumerator ExistingActorEquipsOneReplaceableSwordAndUsesDistinctAuthoredClips()
        {
            var root = new GameObject("SwordFixture");
            try
            {
                ReactiveCombatActorLibrary library = Resources.Load<ReactiveCombatActorLibrary>("Combat/ReactiveCombatActorLibrary");
                Assert.That(library, Is.Not.Null);
                Assert.That(library.keiko.model.name, Is.EqualTo("Keiko@Idle"));
                Assert.That(library.keiko.idle.name, Is.EqualTo("Keiko_Two_handed_battle_idle"));
                Assert.That(library.keiko.attack.name, Is.EqualTo("Keiko_Normal_attack_corrected"));
                Assert.That(library.keiko.heavy.name, Is.EqualTo("Keiko_Hard_jump_attack_corrected"));
                Assert.That(library.keiko.preparation.name, Is.EqualTo("Keiko_Normal_preparation"));
                Assert.That(library.keiko.heavyPreparation.name, Is.EqualTo("Keiko_Heavy_preparation"));
                Assert.That(library.keiko.entranceWalk.name, Is.EqualTo("Keiko_Enter_battle_corrected"));
                Assert.That(library.keiko.enterBattle.name, Is.EqualTo("Keiko_Enter_battle_settle"));
                Assert.That(library.keiko.attack.length, Is.EqualTo(33f / 30f).Within(.02f));
                Assert.That(library.keiko.heavy.length, Is.EqualTo(43f / 30f).Within(.02f));
                Assert.That(library.keiko.attack.events, Is.Empty);
                Assert.That(library.keiko.heavy.events, Is.Empty);
                var actor = ReactiveCombatActorVisual.Spawn(CombatActorKind.Keiko, root.transform);
                Assert.That(actor.AttackContactSeconds(false), Is.EqualTo(.6f).Within(.02f));
                Assert.That(actor.AttackContactSeconds(true), Is.EqualTo(26f / 30f).Within(.02f));
                Assert.That(actor.PreparationDuration(false), Is.EqualTo(9f / 30f).Within(.02f));
                Assert.That(actor.PreparationDuration(true), Is.EqualTo(14f / 30f).Within(.02f));
                Assert.That(actor.EnterBattleDuration, Is.EqualTo(14f / 30f).Within(.02f));
                var attachment = actor.GetComponent<ReactiveCombatWeaponAttachment>();
                Assert.That(attachment, Is.Not.Null);
                Assert.That(attachment.Socket.parent.name, Is.EqualTo("mixamorig:RightHand"));
                GameObject first = attachment.CurrentWeapon;
                for (int i = 0; i < 15; i++) attachment.Equip(library.keiko.weaponPrefab);
                Assert.That(attachment.CurrentWeapon, Is.SameAs(first));
                Assert.That(attachment.Socket.childCount, Is.EqualTo(1));
                Assert.That(first.GetComponentsInChildren<SkinnedMeshRenderer>(), Is.Empty,
                    "The weapon prefab must not carry a second Keiko character.");
                Assert.That(first.GetComponentInChildren<MeshRenderer>().sharedMaterial.mainTexture, Is.Not.Null);
                Assert.That(first.transform.Find("LeftHandGrip"), Is.Not.Null);
                attachment.Equip(null);
                yield return null;
                Assert.That(attachment.Socket.childCount, Is.Zero);
                attachment.Equip(library.keiko.weaponPrefab);
                Assert.That(attachment.Socket.childCount, Is.EqualTo(1));
            }
            finally { Object.Destroy(root); }
        }

        [UnityTest]
        public IEnumerator AuthoredTwoHandTakesKeepBothPalmsOnTheHandleAndAccelerateIntoContact()
        {
            var root = new GameObject("AuthoredTwoHandFixture");
            try
            {
                var actor = ReactiveCombatActorVisual.Spawn(CombatActorKind.Keiko, root.transform);
                actor.enabled = false;
                actor.ModelRoot.GetComponent<Animation>().enabled = false;
                var library = Resources.Load<ReactiveCombatActorLibrary>("Combat/ReactiveCombatActorLibrary");
                Transform left = null;
                foreach (Transform bone in actor.ModelRoot.GetComponentsInChildren<Transform>(true))
                    if (bone.name == "mixamorig:LeftHand") left = bone;
                Assert.That(left, Is.Not.Null);
                Transform grip = actor.WeaponAttachment.CurrentWeapon.transform.Find("LeftHandGrip");
                foreach (AnimationClip clip in new[] { library.keiko.idle, library.keiko.preparation,
                    library.keiko.heavyPreparation, library.keiko.attack, library.keiko.heavy,
                    library.keiko.approach, library.keiko.returnHome, library.keiko.enterBattle })
                {
                    Assert.That(clip.legacy, Is.True);
                    Assert.That(clip.events, Is.Empty, clip.name);
                    for (int i = 0; i <= 8; i++)
                    {
                        // Direct source sampling bypasses the runtime support arm constraint.
                        // This proves the FBX take itself contains the two handed pose.
                        clip.SampleAnimation(actor.ModelRoot.gameObject, clip.length * i / 8f);
                        Assert.That(Vector3.Distance(left.TransformPoint(new Vector3(0f, .033f, 0f)),
                            grip.position), Is.LessThan(.025f), clip.name + " sample " + i);
                    }
                }
                AnimationClip normal = library.keiko.attack;
                normal.SampleAnimation(actor.ModelRoot.gameObject, 0f);
                Vector3 readyDirection = grip.forward;
                Vector3 readyTip = SwordTip(actor);
                normal.SampleAnimation(actor.ModelRoot.gameObject, 13f / 30f);
                Vector3 backswingDirection = grip.forward;
                Vector3 backswingTip = SwordTip(actor);
                normal.SampleAnimation(actor.ModelRoot.gameObject, .6f);
                Vector3 contactDirection = grip.forward;
                Vector3 contactTip = SwordTip(actor);
                Assert.That(Vector3.Angle(readyDirection, backswingDirection), Is.GreaterThan(45f));
                Assert.That(Vector3.Angle(backswingDirection, contactDirection), Is.GreaterThan(60f));
                float anticipationSpeed = Vector3.Distance(readyTip, backswingTip) / (13f / 30f);
                float swingSpeed = Vector3.Distance(backswingTip, contactTip) / (5f / 30f);
                Assert.That(swingSpeed, Is.GreaterThan(anticipationSpeed * 2f));
                yield return null;
            }
            finally { Object.Destroy(root); }
        }

        private static Vector3 SwordTip(ReactiveCombatActorVisual actor) =>
            actor.WeaponAttachment.Socket.TransformPoint(new Vector3(0f, 0f, .63f));

        [UnityTest]
        public IEnumerator RuntimeSupportPalmStaysOnTheSwordAcrossAuthoredPoseBlends()
        {
            var root = new GameObject("RuntimeSwordGripFixture");
            try
            {
                var actor = ReactiveCombatActorVisual.Spawn(CombatActorKind.Keiko, root.transform);
                actor.SetStandingHeight(2f);
                actor.enabled = false;
                Transform left = null;
                foreach (Transform bone in actor.ModelRoot.GetComponentsInChildren<Transform>(true))
                    if (bone.name == "mixamorig:LeftHand") left = bone;
                Assert.That(left, Is.Not.Null);
                Transform grip = actor.WeaponAttachment.CurrentWeapon.transform.Find("LeftHandGrip");
                var poses = new System.Action[] { actor.PlayIdle, () => actor.PlayPreparation(false),
                    () => actor.PlayApproach(.8f), () => actor.PlayAttack(),
                    () => actor.PlayPreparation(true), () => actor.PlayHeavy(),
                    () => actor.PlayReturnHome(.8f), () => actor.PlayEnterBattle() };
                foreach (var pose in poses)
                {
                    pose();
                    for (int i = 0; i < 16; i++)
                    {
                        // Cover the Legacy crossfade as well as the settled take.
                        actor.TickPresentation(i < 4 ? .02f : .12f);
                        Assert.That(Vector3.Distance(left.TransformPoint(new Vector3(0f, .033f, 0f)),
                            grip.position), Is.LessThan(.025f), actor.CurrentPose + " tick " + i);
                        if (i >= 4)
                            Assert.That(Vector3.Angle(left.forward, grip.forward), Is.LessThan(12f),
                                "The constraint must retain the authored wrist direction: " + actor.CurrentPose + " tick " + i);
                    }
                }
                yield return null;
            }
            finally { Object.Destroy(root); }
        }

        [UnityTest]
        public IEnumerator RenderSwordGripAndAuthoredAttackFramesInPlayMode()
        {
            var root = new GameObject("SwordCaptureFixture");
            RenderTexture render = null;
            try
            {
                var actor = ReactiveCombatActorVisual.Spawn(CombatActorKind.Keiko, root.transform);
                actor.SetStandingHeight(2f);
                actor.enabled = false; // Inspect exact authored poses without advancing the visual clock.
                actor.ModelRoot.GetComponent<Animation>().enabled = false;
                foreach (var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.updateWhenOffscreen = true;
                var library = Resources.Load<ReactiveCombatActorLibrary>("Combat/ReactiveCombatActorLibrary");
                var camera = new GameObject("SwordStudyCamera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                Vector3 focus = new Vector3(0f, 1.5f, 0f);
                Vector3 front = actor.ModelRoot.forward;
                Vector3 side = actor.ModelRoot.right;
                Vector3 threeQuarter = (front + side).normalized;
                camera.transform.position = focus + front * 7f;
                camera.transform.LookAt(focus);
                camera.orthographic = true;
                camera.orthographicSize = 2.25f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.07f, .09f, .14f);
                var light = new GameObject("SwordStudyLight", typeof(Light)).GetComponent<Light>();
                light.transform.SetParent(root.transform, false);
                light.type = LightType.Directional;
                light.intensity = 1.3f;
                light.transform.rotation = Quaternion.Euler(25f, -25f, 0f);
                render = new RenderTexture(512, 512, 24);
                render.Create(); camera.targetTexture = render;
                yield return null;
                foreach (var clip in new[] { library.keiko.idle, library.keiko.preparation,
                    library.keiko.heavyPreparation, library.keiko.attack, library.keiko.heavy,
                    library.keiko.enterBattle })
                {
                    var sheet = new Texture2D(2048, 1536, TextureFormat.RGB24, false);
                    try
                    {
                        for (int i = 0; i < 12; i++)
                        {
                            int pose = i % 4;
                            float time = clip.length * pose / 3f;
                            if (clip == library.keiko.attack)
                                time = new[] { 0f, 13f / 30f, .6f, .86f }[pose];
                            else if (clip == library.keiko.heavy)
                                time = new[] { 0f, .68f, 26f / 30f, 1.12f }[pose];
                            Vector3 angle = i / 4 == 0 ? front : i / 4 == 1 ? side : threeQuarter;
                            camera.transform.position = focus + angle * 7f +
                                (i / 4 == 2 ? Vector3.up * .6f : Vector3.zero);
                            camera.transform.LookAt(focus);
                            clip.SampleAnimation(actor.ModelRoot.gameObject, time);
                            if (clip != library.keiko.heavy) actor.AlignFeet();
                            yield return null; // Let Unity skin the sampled pose before rendering it.
                            camera.Render();
                            RenderTexture.active = render;
                            sheet.ReadPixels(new Rect(0, 0, 512, 512), (i % 4) * 512, (2 - i / 4) * 512);
                        }
                        sheet.Apply();
                        string path = Path.Combine(Path.GetTempPath(), "rokas-sword-" + clip.name + "-frames.png");
                        File.WriteAllBytes(path, sheet.EncodeToPNG()); TestContext.WriteLine(path);
                    }
                    finally { Object.Destroy(sheet); }
                }
                actor.ModelRoot.GetComponent<Animation>().enabled = false;
                actor.enabled = true;
                actor.PlayIdle();
                actor.TickPresentation(.2f);
                yield return null;
                camera.transform.position = focus + front * 7f;
                camera.transform.LookAt(focus);
                Save(camera, render, "rokas-sword-idle-front.png");
                camera.transform.position = focus + side * 7f;
                camera.transform.LookAt(focus);
                Save(camera, render, "rokas-sword-idle-side.png");
                camera.transform.position = focus + threeQuarter * 7f + Vector3.up * .6f;
                camera.transform.LookAt(focus);
                Save(camera, render, "rokas-sword-idle-threequarter.png");
                var attachment = actor.GetComponent<ReactiveCombatWeaponAttachment>();
                Vector3 grip = attachment.Socket.position;
                camera.orthographicSize = .5f;
                camera.transform.position = grip + threeQuarter * 1.8f + Vector3.up * .25f;
                camera.transform.LookAt(grip);
                Save(camera, render, "rokas-sword-grip-close.png");
            }
            finally
            {
                RenderTexture.active = null;
                if (render != null) { render.Release(); Object.Destroy(render); }
                Object.Destroy(root);
            }
        }

        private static void Save(Camera camera, RenderTexture render, string filename)
        {
            camera.Render(); RenderTexture.active = render;
            var image = new Texture2D(render.width, render.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0); image.Apply();
            string path = Path.Combine(Path.GetTempPath(), filename);
            File.WriteAllBytes(path, image.EncodeToPNG()); TestContext.WriteLine(path);
            Object.Destroy(image);
        }
    }
}
