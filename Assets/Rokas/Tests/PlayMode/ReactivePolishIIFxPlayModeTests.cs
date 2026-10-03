using System.Collections;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rokas.Tests
{
    public sealed class ReactivePolishIIFxPlayModeTests
    {
        [UnityTest]
        public IEnumerator PortalEmergenceUsesOwnedLitMaterialsAndRestoresThem()
        {
            var parent = new GameObject("PolishIIEmergenceFixture");
            var actor = ReactiveCombatActorVisual.Spawn(CombatActorKind.Yokai, parent.transform);
            var portal = new ReactiveCombatPortalEffect(parent.transform, Vector3.zero, 30);
            var surface = actor.ModelRoot.GetComponentInChildren<SkinnedMeshRenderer>();
            Material original = surface.sharedMaterial;
            var emergence = new ReactiveCombatPortalEmergence(actor, portal);
            try
            {
                emergence.Begin();
                emergence.Begin();
                Assert.That(emergence.Begun, Is.True);
                Assert.That(surface.sharedMaterial, Is.Not.SameAs(original));
                Assert.That(surface.sharedMaterial.shader.name, Is.EqualTo("Rokas/ReactiveCombat/PortalEmergence"));
                Assert.That(surface.sharedMaterial.shader.isSupported, Is.True);
                Assert.That(surface.sharedMaterial.mainTexture, Is.SameAs(original.mainTexture));
                Assert.That(surface.sharedMaterial.GetVector("_PortalCenter"), Is.EqualTo((Vector4)portal.ApertureCenter));
                Assert.That(surface.sharedMaterial.GetVector("_PortalNormal"), Is.EqualTo((Vector4)portal.PlaneNormal));
                emergence.Dispose();
                Assert.That(surface.sharedMaterial, Is.SameAs(original));
                yield return null;
            }
            finally
            {
                emergence.Dispose(); portal.Dispose(); Object.Destroy(parent);
            }
        }

        [UnityTest]
        public IEnumerator BladeRibbonEmitsOnlyDuringSwingAndStopsAfterBladeStops()
        {
            var parent = new GameObject("PolishIIBladeFixture");
            var actor = ReactiveCombatActorVisual.Spawn(CombatActorKind.Keiko, parent.transform);
            var effect = new ReactiveCombatSwordEffect(actor);
            var attachment = actor.GetComponentInChildren<ReactiveCombatWeaponAttachment>(true);
            try
            {
                // Let the actor's first LateUpdate apply its stable model anchor
                // before comparing world-space blade transforms across paused frames.
                yield return null;
                effect.Tick(.02f, false, false);
                Assert.That(effect.ActiveSamples, Is.Zero);
                Assert.That(effect.Emitting, Is.False);
                effect.Tick(.02f, true, false);
                attachment.CurrentWeapon.transform.localRotation = Quaternion.Euler(0, 20, 0);
                effect.Tick(.02f, true, false);
                Assert.That(effect.ActiveSamples, Is.GreaterThan(1));
                Vector3 tip = effect.BladeTip;
                float elapsed = effect.PresentationElapsed;
                yield return new WaitForSecondsRealtime(.08f);
                Assert.That(effect.PresentationElapsed, Is.EqualTo(elapsed));
                Assert.That(effect.BladeTip, Is.EqualTo(tip));
                effect.Tick(.25f, false, false);
                Assert.That(effect.ActiveSamples, Is.Zero);
                Assert.That(effect.Emitting, Is.False);
            }
            finally { effect.Dispose(); Object.Destroy(parent); }
        }

        [UnityTest]
        public IEnumerator DodgeProducesFootDustWithoutBodyContactAndParticlesPause()
        {
            var parent = new GameObject("PolishIIImpactFixture");
            var effect = new ReactiveCombatImpactEffect(parent.transform, 30);
            try
            {
                effect.DodgeDust(Vector3.zero);
                Assert.That(effect.DodgeDispatches, Is.EqualTo(1));
                Assert.That(effect.FleshDispatches, Is.Zero);
                Assert.That(effect.GuardDispatches, Is.Zero);
                effect.Tick(.02f);
                Assert.That(effect.ParticleCount, Is.GreaterThan(0));
                var system = parent.GetComponentsInChildren<ParticleSystem>()[1];
                var before = new ParticleSystem.Particle[40];
                int count = system.GetParticles(before);
                yield return new WaitForSecondsRealtime(.1f);
                var after = new ParticleSystem.Particle[40];
                Assert.That(system.GetParticles(after), Is.EqualTo(count));
                for (int i = 0; i < count; i++)
                {
                    Assert.That(after[i].position, Is.EqualTo(before[i].position));
                    Assert.That(after[i].remainingLifetime, Is.EqualTo(before[i].remainingLifetime));
                }
                effect.Tick(.5f);
                Assert.That(effect.ParticleCount, Is.Zero);
            }
            finally { effect.Dispose(); Object.Destroy(parent); }
        }

        [UnityTest]
        public IEnumerator LayeredPortalAnimatesActualPixelsAndLeavesCarrierCornersClear()
        {
            var parent = new GameObject("PolishIIPortalPixelFixture");
            parent.transform.position = Vector3.one * 3000f;
            var portal = new ReactiveCombatPortalEffect(parent.transform, Vector3.zero, 30);
            var item = new GameObject("PortalPixelCamera", typeof(Camera));
            item.transform.SetParent(parent.transform, false);
            // Frame the new reference-sized aperture including its outer ribbons.
            item.transform.localPosition = new Vector3(0, 2.9f, -10);
            var camera = item.GetComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << 30; camera.allowHDR = false;
            var target = new RenderTexture(160, 240, 24, RenderTextureFormat.ARGB32);
            target.Create(); camera.targetTexture = target;
            var readback = new Texture2D(160, 240, TextureFormat.RGBA32, false);
            try
            {
                portal.SetProgress(1);
                portal.Tick(.3f);
                Color32[] first = Pixels(camera, readback);
                portal.Tick(.2f);
                Color32[] second = Pixels(camera, readback);
                int changed = 0, covered = 0;
                for (int i = 0; i < first.Length; i++)
                {
                    if (first[i].a > 20) covered++;
                    if (Mathf.Abs(first[i].r - second[i].r) + Mathf.Abs(first[i].b - second[i].b) > 4) changed++;
                }
                Assert.That(covered, Is.GreaterThan(1000));
                Assert.That(changed, Is.GreaterThan(150), "The vortex must animate in rendered pixels, not only change a timer.");
                Assert.That(second[0].a, Is.Zero);
                Assert.That(second[159].a, Is.Zero);
                Assert.That(second[second.Length - 1].a, Is.Zero);
                portal.SetProgress(0);
                // A reference collapse leaves a short ghost edge and motes after
                // the aperture closes; only the completed residual must be empty.
                portal.Tick(.81f);
                Color32[] closed = Pixels(camera, readback);
                foreach (Color32 pixel in closed) Assert.That(pixel.a, Is.Zero);
                yield return null;
            }
            finally
            {
                camera.targetTexture = null; target.Release();
                Object.Destroy(target); Object.Destroy(readback); portal.Dispose(); Object.Destroy(parent);
            }
        }
        private static Color32[] Pixels(Camera camera, Texture2D readback)
        {
            RenderTexture previous = RenderTexture.active;
            camera.Render(); RenderTexture.active = camera.targetTexture;
            readback.ReadPixels(new Rect(0, 0, readback.width, readback.height), 0, 0);
            readback.Apply(); RenderTexture.active = previous;
            return readback.GetPixels32();
        }

        [UnityTest]
        public IEnumerator ActualSkinnedBodyAppearsInsideApertureBeforeCrossingAndRestoresAfterExit()
        {
            var parent = new GameObject("PolishIIEmergencePixelFixture");
            parent.transform.position = Vector3.one * 3000f;
            var actor = ReactiveCombatActorVisual.Spawn(CombatActorKind.Yokai, parent.transform);
            actor.SetStandingHeight(3.5f);
            actor.SetFacing(false);
            foreach (Transform child in actor.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 30;
            var portal = new ReactiveCombatPortalEffect(parent.transform, Vector3.zero, 30);
            portal.SetProgress(1f); portal.Tick(.3f);
            var emergence = new ReactiveCombatPortalEmergence(actor, portal);
            var item = new GameObject("EmergencePixelCamera", typeof(Camera));
            item.transform.SetParent(parent.transform, false);
            item.transform.localPosition = new Vector3(0, 1.7f, -10);
            var camera = item.GetComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 2.7f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << 30; camera.allowHDR = false;
            var target = new RenderTexture(360, 240, 24, RenderTextureFormat.ARGB32);
            target.Create(); camera.targetTexture = target;
            var readback = new Texture2D(360, 240, TextureFormat.RGBA32, false);
            var light = new GameObject("EmergencePixelKey", typeof(Light));
            light.transform.SetParent(parent.transform, false);
            light.transform.localRotation = Quaternion.Euler(30, -35, 0);
            light.GetComponent<Light>().type = LightType.Directional;
            light.GetComponent<Light>().intensity = 1.7f;
            light.GetComponent<Light>().cullingMask = 1 << 30;
            try
            {
                yield return null;
                emergence.Begin();
                actor.transform.position = portal.FloorPosition - portal.PlaneNormal * 3f;
                Color32[] hidden = Pixels(camera, readback);
                actor.transform.position = portal.FloorPosition - portal.PlaneNormal * .28f;
                Color32[] inside = Pixels(camera, readback);
                int changed = 0;
                for (int i = 0; i < hidden.Length; i++)
                    if (Mathf.Abs(hidden[i].r - inside[i].r) + Mathf.Abs(hidden[i].g - inside[i].g) +
                        Mathf.Abs(hidden[i].b - inside[i].b) > 12) changed++;
                Assert.That(changed, Is.GreaterThan(150),
                    "Real body pixels must remain visible inside the aperture before its center crosses the fixed plane.");
                actor.transform.position = portal.FloorPosition + portal.PlaneNormal * 1.5f;
                Color32[] crossed = Pixels(camera, readback);
                int emerged = 0;
                for (int i = 0; i < inside.Length; i++)
                    if (Mathf.Abs(inside[i].r - crossed[i].r) + Mathf.Abs(inside[i].g - crossed[i].g) +
                        Mathf.Abs(inside[i].b - crossed[i].b) > 12) emerged++;
                Assert.That(emerged, Is.GreaterThan(changed / 2));
                yield return null;
            }
            finally
            {
                emergence.Dispose(); portal.Dispose(); camera.targetTexture = null; target.Release();
                Object.Destroy(target); Object.Destroy(readback); Object.Destroy(parent);
            }
        }
    }
}
