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
                Assert.That(library.keiko.attack.name, Is.EqualTo("Keiko_Normal_attack"));
                Assert.That(library.keiko.heavy.name, Is.EqualTo("Keiko_Hard_jump_attack"));
                Assert.That(library.keiko.attack.length, Is.EqualTo(38f / 30f).Within(.04f));
                Assert.That(library.keiko.heavy.length, Is.EqualTo(57f / 30f).Within(.04f));
                Assert.That(library.keiko.attack.events, Is.Empty);
                Assert.That(library.keiko.heavy.events, Is.Empty);
                var actor = ReactiveCombatActorVisual.Spawn(CombatActorKind.Keiko, root.transform);
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
                attachment.Equip(null);
                yield return null;
                Assert.That(attachment.Socket.childCount, Is.Zero);
                attachment.Equip(library.keiko.weaponPrefab);
                Assert.That(attachment.Socket.childCount, Is.EqualTo(1));
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
                actor.SetStandingHeight(4f);
                actor.enabled = false; // Inspect exact authored poses without advancing the visual clock.
                actor.ModelRoot.GetComponent<Animation>().enabled = false;
                foreach (var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.updateWhenOffscreen = true;
                var library = Resources.Load<ReactiveCombatActorLibrary>("Combat/ReactiveCombatActorLibrary");
                var camera = new GameObject("SwordStudyCamera", typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                camera.transform.position = new Vector3(0f, 2f, -7f);
                camera.transform.LookAt(new Vector3(0f, 2f, 0f));
                camera.orthographic = true;
                camera.orthographicSize = 2.7f;
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
                foreach (var clip in new[] { library.keiko.idle, library.keiko.attack, library.keiko.heavy })
                {
                    var sheet = new Texture2D(2048, 1536, TextureFormat.RGB24, false);
                    try
                    {
                        for (int i = 0; i < 12; i++)
                        {
                            clip.SampleAnimation(actor.ModelRoot.gameObject, clip.length * i / 11f);
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
                actor.ModelRoot.GetComponent<Animation>().enabled = true;
                actor.enabled = true;
                actor.PlayIdle();
                actor.TickPresentation(.2f);
                yield return null;
                Save(camera, render, "rokas-sword-idle-front.png");
                camera.transform.position = new Vector3(-7f, 2f, 0f);
                camera.transform.LookAt(new Vector3(0f, 2f, 0f));
                Save(camera, render, "rokas-sword-idle-side.png");
                var attachment = actor.GetComponent<ReactiveCombatWeaponAttachment>();
                Vector3 grip = attachment.Socket.position;
                camera.orthographicSize = .5f;
                camera.transform.position = grip + new Vector3(-1.5f, .25f, -1.5f);
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
