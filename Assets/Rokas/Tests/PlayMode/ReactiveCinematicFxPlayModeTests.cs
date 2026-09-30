using System.Collections;
using System.IO;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Core.ReactiveTurns;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rokas.Tests
{
    public sealed class ReactiveCinematicFxPlayModeTests
    {
        [UnityTest]
        public IEnumerator PortalActuallyRendersVisibleCoverageIntoTransparentActorTexture()
        {
            var parent = new GameObject("ReactivePortalRenderFixture");
            parent.transform.position = Vector3.one * 3000f;
            var portal = new ReactiveCombatPortalEffect(parent.transform, Vector3.zero, 30);
            Camera camera = CreateIsolatedCamera(parent.transform, 1.7f, 2.5f);
            try
            {
                portal.SetProgress(0f);
                Color32[] closed = RenderPixels(camera, "portal-closed.png");
                Assert.That(CountCoverage(closed), Is.Zero);
                portal.SetProgress(1f);
                portal.Tick(.3f);
                Color32[] open = RenderPixels(camera, "portal-open.png");
                Assert.That(CountCoverage(open), Is.GreaterThan(open.Length / 10),
                    "A supported shader and nonzero material opacity are insufficient: the portal must reach the render texture.");
                int middle = (camera.targetTexture.height / 2) * camera.targetTexture.width + camera.targetTexture.width / 2;
                Assert.That(open[middle].a, Is.GreaterThan(220), "The tear's dark interior should retain its coverage alpha.");
                int rimPixels = 0;
                foreach (Color32 pixel in open)
                    if (pixel.a > 40 && Mathf.Max(pixel.r, pixel.b) > 35) rimPixels++;
                Assert.That(rimPixels, Is.GreaterThan(50), "The restrained rim must remain visible around the dark interior.");
                yield return null;
            }
            finally
            {
                portal.Dispose();
                ReleaseCamera(camera);
                Object.Destroy(parent);
            }
        }

        [UnityTest]
        public IEnumerator AshActuallyClipsTheRenderedActorAsProgressAdvances()
        {
            var parent = new GameObject("ReactiveAshRenderFixture");
            parent.transform.position = Vector3.one * 3000f;
            ReactiveCombatActorVisual actor = ReactiveCombatActorVisual.Spawn(CombatActorKind.Yokai, parent.transform);
            actor.SetStandingHeight(3.5f);
            foreach (Transform child in actor.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 30;
            Camera camera = CreateIsolatedCamera(parent.transform, 1.7f, 2.5f);
            var lightObject = new GameObject("AshRenderKey", typeof(Light));
            lightObject.transform.SetParent(parent.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(30f, -35f, 0f);
            Light key = lightObject.GetComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.7f;
            key.cullingMask = 1 << 30;
            key.shadows = LightShadows.None;
            var ash = new ReactiveCombatAshDissolve(actor);
            try
            {
                Vector3 position = actor.transform.position;
                ash.Begin();
                // This test measures body clipping; separate clock tests cover emitted ash.
                actor.GetComponentInChildren<ParticleSystemRenderer>().enabled = false;
                int intact = CountCoverage(RenderPixels(camera, "ash-intact.png"));
                Assert.That(intact, Is.GreaterThan(500));
                ash.SetProgress(.55f);
                int partial = CountCoverage(RenderPixels(camera, "ash-partial.png"));
                Assert.That(partial, Is.GreaterThan(intact / 20));
                Assert.That(partial, Is.LessThan(intact * .85f),
                    "At mid-progress the real model must have visible gaps, rather than retain an opaque body.");
                ash.SetProgress(1f);
                Assert.That(CountCoverage(RenderPixels(camera, "ash-gone.png")), Is.Zero);
                Assert.That(actor.transform.position, Is.EqualTo(position));
                yield return null;
            }
            finally
            {
                ash.Dispose();
                ReleaseCamera(camera);
                Object.Destroy(parent);
            }
        }

        [UnityTest]
        public IEnumerator ProductionVictoryWithoutActionSettledStillReturnsHunterAndSettles()
        {
            var parent = new GameObject("ReactiveTerminalVictoryFixture", typeof(RectTransform));
            var arena = new ReactiveCombatArena(new UiKit(null, null), parent.GetComponent<RectTransform>());
            try
            {
                var contract = new ContractDefinition { id = "terminal-victory", enemyId = "E1",
                    enemyHealth = 10f, enemyDamage = 8f, clickDamage = 20f };
                var combat = new ReactiveCombatSession(ReactiveDuelDefinitions.Create(contract, new SaveData()));
                combat.Start();
                arena.SetEnemies(combat.ActiveEnemyIds, null);
                for (int warmup = 0; !arena.PresentationReady && warmup < 100; warmup++) arena.Tick(.02f);
                Vector3 home = arena.HunterHome;
                Assert.That(combat.SubmitCommand(new CommandIntent("victory-normal", combat.Revision,
                    CommandKind.Basic, null, new[] { "E1" })).Accepted, Is.True);
                Assert.That(arena.StartHunterApproach("E1"), Is.True);
                int steps = 0;
                while (!arena.HunterApproachComplete && steps++ < 200) arena.Tick(.02f);
                Assert.That(arena.HunterApproachComplete, Is.True);
                Assert.That(arena.HunterAtHome, Is.False);
                Present(arena, combat.Advance(0), combat.CurrentAttack);
                arena.Tick(.2f);
                CombatStep terminal = combat.Advance(240000);
                Assert.That(terminal.TerminalResult, Is.EqualTo(CombatOutcome.Victory));
                Assert.That(HasEvent(terminal, CombatEventKind.Victory), Is.True);
                Assert.That(HasEvent(terminal, CombatEventKind.ActionSettled), Is.False,
                    "The production lethal-hit path terminates before ActionSettled.");
                Present(arena, terminal, combat.CurrentAttack);
                arena.SetEnemies(combat.ActiveEnemyIds, null, new[] { "E1" });
                Assert.That(arena.PresentationReady, Is.False);
                steps = 0;
                while (!arena.PresentationReady && steps++ < 150) arena.Tick(.02f);
                Assert.That(arena.PresentationReady, Is.True,
                    "Victory must finish return and settle without waiting for an event Core never emits.");
                Assert.That(arena.HunterAtHome, Is.True);
                Assert.That(arena.HunterPosition, Is.EqualTo(home));
                Assert.That(arena.CorpseCount, Is.EqualTo(1),
                    "Cosmetic ash cleanup continues independently after terminal return is complete.");
                yield return null;
            }
            finally
            {
                arena.Dispose();
                Object.Destroy(parent);
            }
        }

        [UnityTest]
        public IEnumerator ProductionDefeatWithoutActionSettledReturnsAttackerWhileHunterIsDead()
        {
            var parent = new GameObject("ReactiveTerminalDefeatFixture", typeof(RectTransform));
            var arena = new ReactiveCombatArena(new UiKit(null, null), parent.GetComponent<RectTransform>());
            try
            {
                var contract = new ContractDefinition { id = "terminal-defeat", enemyId = "E1",
                    enemyHealth = 200f, enemyDamage = 300f, clickDamage = 20f };
                var combat = new ReactiveCombatSession(ReactiveDuelDefinitions.Create(contract, new SaveData()));
                combat.Start();
                arena.SetEnemies(combat.ActiveEnemyIds, null);
                Vector3 enemyHome = arena.EnemyHome("E1");
                Assert.That(combat.SubmitCommand(new CommandIntent("defeat-defend", combat.Revision,
                    CommandKind.Defend, null, null)).Accepted, Is.True);
                CombatStep enemyStarted = combat.Advance(440000);
                Assert.That(combat.Phase, Is.EqualTo(ReactivePhase.EnemyExecution));
                Present(arena, enemyStarted, combat.CurrentAttack);
                for (int travel = 0; !arena.EnemyApproachComplete("E1") && travel < 100; travel++) arena.Tick(.02f);
                Assert.That(arena.EnemyApproachComplete("E1"), Is.True);
                Assert.That(arena.EnemyAtHome("E1"), Is.False);
                arena.Tick(.35f);
                CombatStep terminal = combat.Advance(combat.CurrentActionStartUs + 1100000);
                Assert.That(terminal.TerminalResult, Is.EqualTo(CombatOutcome.Defeat));
                Assert.That(HasEvent(terminal, CombatEventKind.Defeat), Is.True);
                Assert.That(HasEvent(terminal, CombatEventKind.ActionSettled), Is.False,
                    "The production lethal enemy hit does not settle its interrupted action.");
                Present(arena, terminal, combat.CurrentAttack);
                ReactiveCombatActorVisual hunter = null;
                foreach (var actor in GameObject.Find("ReactiveCombatWorld").GetComponentsInChildren<ReactiveCombatActorVisual>())
                    if (actor.name == "CombatActor_Keiko") hunter = actor;
                Assert.That(hunter, Is.Not.Null);
                Assert.That(hunter.IsDead, Is.True);
                Assert.That(arena.PresentationReady, Is.False);
                int steps = 0;
                while (!arena.PresentationReady && steps++ < 100) arena.Tick(.02f);
                Assert.That(arena.EnemyAtHome("E1"), Is.True);
                Assert.That(arena.EnemyPosition("E1"), Is.EqualTo(enemyHome));
                Assert.That(arena.PresentationReady, Is.True,
                    "Defeat must settle the surviving attacker even though the dead hunter cannot play idle.");
                Assert.That(hunter.IsDead, Is.True);
                yield return null;
            }
            finally
            {
                arena.Dispose();
                Object.Destroy(parent);
            }
        }

        [UnityTest]
        public IEnumerator PortalOnlyAdvancesWhenItsPresentationClockTicks()
        {
            var parent = new GameObject("ReactivePortalClockFixture");
            var portal = new ReactiveCombatPortalEffect(parent.transform, Vector3.zero, 30);
            try
            {
                portal.SetProgress(1f);
                portal.Tick(.3f);
                float elapsed = portal.PresentationElapsed;
                Vector3 scale = portal.Transform.localScale;
                var particles = portal.Transform.GetComponentInChildren<ParticleSystem>();
                var before = new ParticleSystem.Particle[32];
                int beforeCount = particles.GetParticles(before);
                Assert.That(beforeCount, Is.GreaterThan(0));
                LineRenderer line = portal.Transform.GetComponentInChildren<LineRenderer>();
                Vector3 rimPoint = line.GetPosition(5);
                Material interior = portal.Transform.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                Assert.That(interior.shader.isSupported, Is.True);
                float phase = interior.GetFloat("_Phase");
                yield return new WaitForSecondsRealtime(.15f);
                var after = new ParticleSystem.Particle[32];
                int afterCount = particles.GetParticles(after);
                Assert.That(portal.PresentationElapsed, Is.EqualTo(elapsed));
                Assert.That(portal.Transform.localScale, Is.EqualTo(scale));
                Assert.That(line.GetPosition(5), Is.EqualTo(rimPoint));
                Assert.That(interior.GetFloat("_Phase"), Is.EqualTo(phase));
                Assert.That(afterCount, Is.EqualTo(beforeCount));
                for (int i = 0; i < beforeCount; i++)
                {
                    Assert.That(after[i].position, Is.EqualTo(before[i].position));
                    Assert.That(after[i].remainingLifetime, Is.EqualTo(before[i].remainingLifetime));
                }
                foreach (Transform child in portal.Transform.GetComponentsInChildren<Transform>(true))
                    Assert.That(child.gameObject.layer, Is.EqualTo(30));
                portal.Tick(.1f);
                Assert.That(portal.PresentationElapsed, Is.GreaterThan(elapsed));
                portal.SetProgress(0f);
                Assert.That(portal.IsClosed, Is.True);
                Assert.That(interior.GetFloat("_Opacity"), Is.Zero);
            }
            finally
            {
                portal.Dispose();
                Object.Destroy(parent);
            }
        }

        [UnityTest]
        public IEnumerator AshClonesMaterialsPreservesSurfaceAndRestoresOwnedSlots()
        {
            var parent = new GameObject("ReactiveAshMaterialFixture");
            ReactiveCombatActorVisual first = ReactiveCombatActorVisual.Spawn(CombatActorKind.Yokai, parent.transform);
            ReactiveCombatActorVisual second = ReactiveCombatActorVisual.Spawn(CombatActorKind.Yokai, parent.transform);
            first.transform.localPosition = new Vector3(1f, -.5f, 2f);
            second.transform.localPosition = new Vector3(5f, -.5f, 2f);
            var ash = new ReactiveCombatAshDissolve(first);
            try
            {
                SkinnedMeshRenderer renderer = first.ModelRoot.GetComponentInChildren<SkinnedMeshRenderer>();
                SkinnedMeshRenderer sibling = second.ModelRoot.GetComponentInChildren<SkinnedMeshRenderer>();
                Material original = renderer.sharedMaterial;
                Assert.That(sibling.sharedMaterial, Is.SameAs(original));
                Shader originalShader = original.shader;
                Color color = original.GetColor("_Color");
                Texture texture = original.GetTexture("_MainTex");
                Vector2 scale = original.GetTextureScale("_MainTex");
                Vector2 offset = original.GetTextureOffset("_MainTex");
                float metallic = original.GetFloat("_Metallic");
                float smoothness = original.GetFloat("_Glossiness");
                Vector3 actorPosition = first.transform.localPosition;
                Vector3 actorScale = first.transform.localScale;
                ash.CaptureMaterials();
                Assert.That(renderer.sharedMaterial, Is.SameAs(original));
                ash.Begin();
                Material owned = renderer.sharedMaterial;
                Assert.That(owned, Is.Not.SameAs(original));
                Assert.That(owned.shader.name, Is.EqualTo("Rokas/ReactiveCombat/AshDissolve"));
                Assert.That(owned.shader.isSupported, Is.True);
                Assert.That(owned.GetColor("_Color"), Is.EqualTo(color));
                Assert.That(owned.GetTexture("_MainTex"), Is.SameAs(texture));
                Assert.That(owned.GetTextureScale("_MainTex"), Is.EqualTo(scale));
                Assert.That(owned.GetTextureOffset("_MainTex"), Is.EqualTo(offset));
                Assert.That(owned.GetFloat("_Metallic"), Is.EqualTo(metallic));
                Assert.That(owned.GetFloat("_Glossiness"), Is.EqualTo(smoothness));
                for (int i = 1; i <= 24; i++) ash.SetProgress(i * .02f);
                Assert.That(owned.GetFloat("_Dissolve"), Is.EqualTo(.48f).Within(.0001f));
                Assert.That(first.transform.localPosition, Is.EqualTo(actorPosition));
                Assert.That(first.transform.localScale, Is.EqualTo(actorScale));
                Assert.That(sibling.sharedMaterial, Is.SameAs(original));
                Assert.That(original.shader, Is.SameAs(originalShader));
                Assert.That(original.GetColor("_Color"), Is.EqualTo(color));
                Assert.That(original.GetTexture("_MainTex"), Is.SameAs(texture));
                float progress = ash.Progress;
                var particles = first.GetComponentInChildren<ParticleSystem>();
                var before = new ParticleSystem.Particle[160];
                int beforeCount = particles.GetParticles(before);
                Assert.That(beforeCount, Is.GreaterThan(0));
                yield return new WaitForSecondsRealtime(.15f);
                var after = new ParticleSystem.Particle[160];
                Assert.That(particles.GetParticles(after), Is.EqualTo(beforeCount));
                Assert.That(ash.Progress, Is.EqualTo(progress));
                Assert.That(owned.GetFloat("_Dissolve"), Is.EqualTo(progress));
                for (int i = 0; i < beforeCount; i++)
                {
                    Assert.That(after[i].position, Is.EqualTo(before[i].position));
                    Assert.That(after[i].remainingLifetime, Is.EqualTo(before[i].remainingLifetime));
                }
                ash.Dispose();
                Assert.That(renderer.sharedMaterial, Is.SameAs(original));
                yield return null;
                Assert.That(owned == null, Is.True, "Owned dissolve materials must be released.");
                Assert.That(first.GetComponentInChildren<ParticleSystem>(), Is.Null);
            }
            finally
            {
                ash.Dispose();
                Object.Destroy(parent);
            }
        }

        [UnityTest]
        public IEnumerator ArenaRetainsCorpsePositionThroughAshThenCleansTheActor()
        {
            var parent = new GameObject("ReactiveAshLifecycleFixture", typeof(RectTransform));
            var arena = new ReactiveCombatArena(new UiKit(null, null), parent.GetComponent<RectTransform>());
            try
            {
                arena.SetEnemies(new[] { "E1" }, null);
                GameObject world = GameObject.Find("ReactiveCombatWorld");
                ReactiveCombatActorVisual target = null;
                foreach (var actor in world.GetComponentsInChildren<ReactiveCombatActorVisual>())
                    if (actor.name == "CombatActor_Yokai") target = actor;
                Assert.That(target, Is.Not.Null);
                Vector3 origin = target.transform.localPosition;
                Vector3 scale = target.transform.localScale;
                var renderer = target.ModelRoot.GetComponentInChildren<SkinnedMeshRenderer>();
                Material original = renderer.sharedMaterial;
                arena.SetEnemies(new string[0], null, new[] { "E1" });
                Assert.That(target.IsDead, Is.True);
                Assert.That(arena.VisibleEnemyCount, Is.Zero);
                Assert.That(arena.StartHunterApproach("E1"), Is.False);
                arena.Tick(arena.DeathFallDuration + arena.CorpseHoldDuration - .01f);
                Assert.That(renderer.sharedMaterial, Is.SameAs(original));
                arena.Tick(.21f);
                Assert.That(renderer.sharedMaterial, Is.Not.SameAs(original));
                Assert.That(renderer.sharedMaterial.GetFloat("_Dissolve"), Is.GreaterThan(0f));
                Assert.That(target.transform.localPosition, Is.EqualTo(origin),
                    "Death breaks into ash instead of moving the whole corpse under the floor.");
                Assert.That(target.transform.localScale, Is.EqualTo(scale));
                float elapsed = arena.CorpseElapsed("E1");
                float dissolve = renderer.sharedMaterial.GetFloat("_Dissolve");
                yield return new WaitForSecondsRealtime(.12f);
                Assert.That(arena.CorpseElapsed("E1"), Is.EqualTo(elapsed));
                Assert.That(renderer.sharedMaterial.GetFloat("_Dissolve"), Is.EqualTo(dissolve));
                arena.Tick(arena.CorpseDissolveDuration + .01f);
                Assert.That(arena.IsCorpse("E1"), Is.False);
                Assert.That(arena.CorpseCount, Is.Zero);
                yield return null;
                Assert.That(target == null, Is.True);
                Assert.That(world.GetComponentsInChildren<ReactiveCombatActorVisual>().Length, Is.EqualTo(1));
            }
            finally
            {
                arena.Dispose();
                Object.Destroy(parent);
            }
        }

        private static bool HasEvent(CombatStep step, CombatEventKind kind)
        {
            foreach (CombatEvent evt in step.Events) if (evt.Kind == kind) return true;
            return false;
        }

        private static void Present(ReactiveCombatArena arena, CombatStep step, AttackSequenceDefinition sequence)
        {
            foreach (CombatEvent evt in step.Events) arena.Present(evt, sequence);
        }

        private static Camera CreateIsolatedCamera(Transform parent, float height, float orthographicSize)
        {
            var cameraObject = new GameObject("ReactiveFxIsolatedCamera", typeof(Camera));
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.localPosition = new Vector3(0f, height, -12f);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            camera.aspect = 1f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 30f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << 30;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.targetTexture = new RenderTexture(384, 384, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture.Create();
            return camera;
        }

        private static Color32[] RenderPixels(Camera camera, string imageName)
        {
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            var readable = new Texture2D(camera.targetTexture.width, camera.targetTexture.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = camera.targetTexture;
                readable.ReadPixels(new Rect(0, 0, readable.width, readable.height), 0, 0, false);
                readable.Apply(false, false);
                string directory = Path.Combine(Path.GetTempPath(), "rokas-cinematic-fx-render");
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, imageName), readable.EncodeToPNG());
                return readable.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(readable);
            }
        }

        private static int CountCoverage(Color32[] pixels)
        {
            int count = 0;
            foreach (Color32 pixel in pixels) if (pixel.a > 40) count++;
            return count;
        }

        private static void ReleaseCamera(Camera camera)
        {
            RenderTexture texture = camera.targetTexture;
            camera.targetTexture = null;
            texture.Release();
            Object.Destroy(texture);
        }
    }
}
