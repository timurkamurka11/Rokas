using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class ReactiveWavePortalPlayModeTests
    {
        private const float Step = .02f;
        private const string MaskShader = "Rokas/ReactiveCombat/PortalEmergence";
        private GameObject root;
        private ReactiveCombatArena arena;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            root = new GameObject("WavePortalFixture", typeof(RectTransform));
            arena = new ReactiveCombatArena(new UiKit(null, null), root.GetComponent<RectTransform>());
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (arena != null) arena.Dispose();
            if (root != null) Object.Destroy(root);
            yield return null;
        }

        [UnityTest] public IEnumerator OneEnemyDoesNotBecomeReadyBeforeTheSharedPortalCloses() { yield return CompleteWave(1); }
        [UnityTest] public IEnumerator TwoEnemiesUseOneContinuousPortalClock() { yield return CompleteWave(2); }
        [UnityTest] public IEnumerator ThreeEnemiesCrossOnePortalSequentiallyAndRestoreTheirSlots() { yield return CompleteWave(3); }
        [UnityTest] public IEnumerator FiveEnemyFormationDoesNotLeaveRightmostActorsBehindThePortalMask() { yield return CompleteWave(5); }
        [UnityTest] public IEnumerator EightEnemyWaveKeepsOnePortalAndClearsEveryFinalBodySlot() { yield return CompleteWave(8); }

        private IEnumerator CompleteWave(int count)
        {
            string[] ids = Ids("first-", count);
            arena.BeginEncounterIntro();
            arena.SetEnemies(ids, null);
            var probe = new WaveProbe(arena);
            Dictionary<string, ReactiveCombatActorVisual> actors = ActorsAtHomes(ids);
            Assert.That(arena.VisibleEnemyCount, Is.Zero);
            yield return FinishWave(probe);
            AssertWave(probe, ids, actors);
            Assert.That(arena.PortalFormationCount, Is.EqualTo(1));
            Assert.That(arena.PortalOpenCount, Is.EqualTo(1));
            Assert.That(arena.PortalCloseCount, Is.EqualTo(1));
            Assert.That(arena.PortalCrossingCount, Is.EqualTo(count));
        }

        [UnityTest]
        public IEnumerator NextWaveCreatesOneNewPortalWithoutReusingItsPredecessorsClockOrMask()
        {
            string[] first = Ids("first-", 2);
            arena.BeginEncounterIntro(); arena.SetEnemies(first, null);
            var firstProbe = new WaveProbe(arena);
            yield return FinishWave(firstProbe);
            AssertWave(firstProbe, first, ActorsAtHomes(first));
            arena.SetEnemies(new string[0], null);
            for (int tick = 0; tick < 210; tick++) arena.Tick(Step);
            yield return null;

            string[] second = Ids("second-", 3);
            arena.SetEnemies(second, null);
            var secondActors = ActorsAtHomes(second);
            var secondProbe = new WaveProbe(arena);
            yield return FinishWave(secondProbe);
            AssertWave(secondProbe, second, secondActors);
            Assert.That(secondProbe.RootId, Is.Not.EqualTo(firstProbe.RootId));
            Assert.That(arena.PortalFormationCount, Is.EqualTo(2));
            Assert.That(arena.PortalOpenCount, Is.EqualTo(2));
            Assert.That(arena.PortalCloseCount, Is.EqualTo(2));
            Assert.That(arena.PortalCrossingCount, Is.EqualTo(5));
        }

        [UnityTest]
        public IEnumerator RemovingQueuedEnemyCannotReopenPortalOrResurrectThatEnemy()
        {
            string[] ids = Ids("queued-", 3);
            arena.BeginEncounterIntro(); arena.SetEnemies(ids, null);
            var actors = ActorsAtHomes(ids); var probe = new WaveProbe(arena);
            yield return WaitForPortal(probe);
            int removedActor = actors[ids[2]].GetInstanceID();
            arena.SetEnemies(new[] { ids[0], ids[1] }, null, ids);
            yield return FinishWave(probe);
            Assert.That(probe.EnteredActors.Contains(removedActor), Is.False, "Removed queued actor must never receive an emergence mask.");
            AssertWave(probe, new[] { ids[0], ids[1] }, actors);
            Assert.That(arena.PortalFormationCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RemovingEnteringEnemyRestoresMaskAndLetsRemainingQueueFinish()
        {
            string[] ids = Ids("removed-", 3);
            arena.BeginEncounterIntro(); arena.SetEnemies(ids, null);
            var actors = ActorsAtHomes(ids); var probe = new WaveProbe(arena);
            yield return WaitForMaskedActor(probe);
            Assert.That(IsMasked(actors[ids[0]]), Is.True);
            var maskMaterials = MaskMaterials(actors[ids[0]]);
            arena.SetEnemies(new[] { ids[1], ids[2] }, null, ids);
            arena.Tick(Step); probe.Observe();
            Assert.That(IsMasked(actors[ids[0]]), Is.False, "Cancellation must release the current actor's mask before continuing.");
            yield return FinishWave(probe);
            AssertRemainingWave(probe, new[] { ids[1], ids[2] }, actors);
            foreach (Material material in maskMaterials) Assert.That(material == null, Is.True);
            Assert.That(arena.PortalFormationCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DisablingEnteringActorDoesNotReactivateItOrLeaveIntroBlocked()
        {
            string[] ids = Ids("disabled-", 3);
            arena.BeginEncounterIntro(); arena.SetEnemies(ids, null);
            var actors = ActorsAtHomes(ids); var probe = new WaveProbe(arena);
            yield return WaitForMaskedActor(probe);
            var disabled = actors[ids[0]];
            Assert.That(IsMasked(disabled), Is.True);
            disabled.gameObject.SetActive(false);
            arena.Tick(Step); probe.Observe();
            Assert.That(IsMasked(disabled), Is.False);
            yield return FinishWave(probe);
            Assert.That(disabled.gameObject.activeSelf, Is.False, "Interrupted entrance must not silently resurrect an externally disabled actor.");
            AssertRemainingWave(probe, new[] { ids[1], ids[2] }, actors);
        }

        [UnityTest]
        public IEnumerator DestroyingEnteringActorDoesNotDereferenceStaleQueueOrBlockRemainingActors()
        {
            string[] ids = Ids("destroyed-", 3);
            arena.BeginEncounterIntro(); arena.SetEnemies(ids, null);
            var actors = ActorsAtHomes(ids); var probe = new WaveProbe(arena);
            yield return WaitForMaskedActor(probe);
            Assert.That(IsMasked(actors[ids[0]]), Is.True);
            var masks = MaskMaterials(actors[ids[0]]);
            Object.Destroy(actors[ids[0]].gameObject); yield return null;
            yield return FinishWave(probe);
            AssertRemainingWave(probe, new[] { ids[1], ids[2] }, actors);
            foreach (Material material in masks) Assert.That(material == null, Is.True);
        }

        [UnityTest]
        public IEnumerator DestroyingQueuedActorClearsItsPendingMotionAndDoesNotHangAtWaveEnd()
        {
            string[] ids = Ids("queued-destroyed-", 3);
            arena.BeginEncounterIntro(); arena.SetEnemies(ids, null);
            var actors = ActorsAtHomes(ids); var probe = new WaveProbe(arena);
            yield return WaitForMaskedActor(probe);
            Assert.That(actors[ids[2]].gameObject.activeSelf, Is.False);
            int removedActor = actors[ids[2]].GetInstanceID();
            Object.Destroy(actors[ids[2]].gameObject); yield return null;
            yield return FinishWave(probe);
            Assert.That(probe.EnteredActors.Contains(removedActor), Is.False);
            AssertWave(probe, new[] { ids[0], ids[1] }, actors);
            Assert.That(arena.PortalFormationCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RefreshAfterDestroyedEnteringActorQueuesItsReplacementWithoutStaleMotionOrMask()
        {
            string[] ids = Ids("refreshed-", 3);
            arena.BeginEncounterIntro(); arena.SetEnemies(ids, null);
            var actors = ActorsAtHomes(ids); var probe = new WaveProbe(arena);
            var originalIds = new HashSet<int>();
            foreach (var actor in actors.Values) originalIds.Add(actor.GetInstanceID());
            yield return WaitForMaskedActor(probe);
            Vector3 originalHome = arena.EnemyHome(ids[0]);
            var masks = MaskMaterials(actors[ids[0]]);
            Object.Destroy(actors[ids[0]].gameObject); yield return null;
            arena.SetEnemies(ids, null, ids);
            ReactiveCombatActorVisual replacement = null;
            foreach (var actor in World().GetComponentsInChildren<ReactiveCombatActorVisual>(true))
                if (actor.name == "CombatActor_Yokai" && !originalIds.Contains(actor.GetInstanceID())) replacement = actor;
            Assert.That(replacement, Is.Not.Null);
            Assert.That(IsMasked(replacement), Is.False, "A replacement must not inherit its predecessor's temporary mask.");
            actors[ids[0]] = replacement;
            probe.RecordHome(replacement, originalHome);
            yield return FinishWave(probe);
            AssertRemainingWave(probe, ids, actors);
            Assert.That(probe.EnteredActors.Contains(replacement.GetInstanceID()), Is.True);
            foreach (Material material in masks) Assert.That(material == null, Is.True);
            Assert.That(arena.PortalFormationCount, Is.EqualTo(1));
            Assert.That(arena.PortalCrossingCount, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator PortalSeedCannotEmitTheEarlyWhiteMoteCloudButOpenPortalStillEmits()
        {
            var parent = new GameObject("PortalQuietSeedFixture");
            var effect = new ReactiveCombatPortalEffect(parent.transform, Vector3.zero, 30);
            try
            {
                foreach (float progress in new[] { 0f, .1f, .2f, .3f, .4f, .45f })
                {
                    effect.SetProgress(progress);
                    for (int tick = 0; tick < 12; tick++) effect.Tick(Step);
                    Assert.That(effect.MoteEmissionStrength, Is.Zero);
                    Assert.That(effect.ParticleCount, Is.Zero, "Early formation must not build the formerly overbright white particle cloud.");
                }
                effect.SetProgress(1f); effect.Tick(.1f);
                Assert.That(effect.MoteEmissionStrength, Is.GreaterThan(0f));
                Assert.That(effect.ParticleCount, Is.GreaterThan(0), "The repair must preserve motes once the portal opens.");
            }
            finally { effect.Dispose(); Object.Destroy(parent); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisposingDuringEntryDestroysPortalAndEmergenceResourcesTogether()
        {
            string[] ids = Ids("disposed-", 3);
            arena.BeginEncounterIntro(); arena.SetEnemies(ids, null);
            var actors = ActorsAtHomes(ids); var probe = new WaveProbe(arena);
            yield return WaitForMaskedActor(probe);
            var portal = Portal(); Assert.That(portal, Is.Not.Null);
            var meshes = new List<Mesh>(); var materials = new HashSet<Material>();
            foreach (var filter in portal.GetComponentsInChildren<MeshFilter>(true)) meshes.Add(filter.sharedMesh);
            foreach (var renderer in portal.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials) materials.Add(material);
            foreach (Material material in MaskMaterials(actors[ids[0]])) materials.Add(material);
            RawImage carrier = root.GetComponentInChildren<RawImage>(true);
            var target = carrier.texture; materials.Add(carrier.material);
            arena.Dispose(); arena = null;
            Assert.That(carrier.texture, Is.Null);
            yield return null; yield return null;
            Assert.That(GameObject.Find("ReactiveCombatWorld"), Is.Null);
            Assert.That(GameObject.Find("ReactiveEnemyPortal"), Is.Null);
            Assert.That(target == null, Is.True);
            foreach (Mesh mesh in meshes) Assert.That(mesh == null, Is.True, "Portal mesh leaked during interrupted entry.");
            foreach (Material material in materials) Assert.That(material == null, Is.True, "Owned portal/emergence/composite material leaked.");
        }

        private IEnumerator FinishWave(WaveProbe probe)
        {
            for (int tick = 0; tick < 1600 && !arena.PresentationReady; tick++)
            {
                arena.Tick(Step); probe.Observe();
                if (tick % 100 == 0) yield return null;
            }
            Assert.That(arena.PresentationReady, Is.True, "Entrance interruption or stale queue must not keep the combat intro blocked.");
            yield return null;
            Assert.That(Portal(), Is.Null, "The shared portal must close only once after the wave finishes entering.");
            AssertNoMasks();
        }

        private IEnumerator WaitForPortal(WaveProbe probe)
        {
            for (int tick = 0; tick < 1000 && Portal() == null; tick++) { arena.Tick(Step); probe.Observe(); }
            Assert.That(Portal(), Is.Not.Null);
            Assert.That(arena.PortalPhase, Is.EqualTo(ReactiveCombatArena.WavePortalPhase.Forming));
            yield return null;
        }

        private IEnumerator WaitForMaskedActor(WaveProbe probe)
        {
            for (int tick = 0; tick < 1000 && probe.EnteredActors.Count == 0; tick++) { arena.Tick(Step); probe.Observe(); }
            Assert.That(probe.EnteredActors.Count, Is.EqualTo(1));
            yield return null;
        }

        private void AssertWave(WaveProbe probe, string[] ids, Dictionary<string, ReactiveCombatActorVisual> actors)
        {
            Assert.That(probe.EnteredActors.Count, Is.EqualTo(ids.Length));
            AssertRemainingWave(probe, ids, actors);
        }

        private void AssertRemainingWave(WaveProbe probe, string[] ids, Dictionary<string, ReactiveCombatActorVisual> actors)
        {
            Assert.That(probe.RootId, Is.Not.Zero);
            Assert.That(probe.SawFormation, Is.True);
            Assert.That(probe.SawOpen, Is.True);
            Assert.That(probe.SawResidual, Is.True);
            Assert.That(probe.SawCollapse, Is.True);
            Assert.That(arena.PortalPhase, Is.EqualTo(ReactiveCombatArena.WavePortalPhase.Closed));
            Assert.That(arena.IntroComplete, Is.True);
            Assert.That(arena.VisibleEnemyCount, Is.EqualTo(ids.Length));
            foreach (string id in ids)
            {
                Assert.That(arena.EnemyAtHome(id), Is.True, "Every emerged actor must reach its own authoritative slot: " + id);
                Assert.That(arena.EnemyHome(id), Is.EqualTo(probe.Home(actors[id])), "The authoritative slot itself must not drift during entrance.");
                Assert.That(actors[id].transform.localPosition, Is.EqualTo(probe.Home(actors[id])));
                Assert.That(actors[id].IdleSettled, Is.True);
                Assert.That(probe.EnteredActors.Contains(actors[id].GetInstanceID()), Is.True);
                Assert.That(Vector3.Dot(actors[id].TorsoPoint - probe.PortalCenter, probe.PortalNormal), Is.GreaterThan(0f), "A final body must be in front of the shared portal plane before its mask is removed.");
            }
            Assert.That(arena.HunterAtHome, Is.True);
            Assert.That(arena.CommandReady, Is.True);
        }

        private Dictionary<string, ReactiveCombatActorVisual> ActorsAtHomes(string[] ids)
        {
            var result = new Dictionary<string, ReactiveCombatActorVisual>();
            foreach (string id in ids)
                foreach (var actor in World().GetComponentsInChildren<ReactiveCombatActorVisual>(true))
                    if (actor.name == "CombatActor_Yokai" && !actor.IsDead &&
                        (actor.transform.localPosition - arena.EnemyHome(id)).sqrMagnitude < .00001f) result.Add(id, actor);
            Assert.That(result.Count, Is.EqualTo(ids.Length), "Fixture must identify real actors at their authored slots before entry.");
            return result;
        }

        private static string[] Ids(string prefix, int count)
        {
            var ids = new string[count]; for (int i = 0; i < count; i++) ids[i] = prefix + (i + 1); return ids;
        }

        private static GameObject World() => GameObject.Find("ReactiveCombatWorld");
        private static GameObject Portal() => GameObject.Find("ReactiveEnemyPortal");
        private static bool IsMasked(ReactiveCombatActorVisual actor) => MaskMaterials(actor).Count > 0;
        private static HashSet<Material> MaskMaterials(ReactiveCombatActorVisual actor)
        {
            var result = new HashSet<Material>();
            if (actor != null)
                foreach (var renderer in actor.GetComponentsInChildren<Renderer>(true))
                    foreach (Material material in renderer.sharedMaterials)
                        if (material != null && material.shader.name == MaskShader) result.Add(material);
            return result;
        }

        private static void AssertNoMasks()
        {
            foreach (var actor in World().GetComponentsInChildren<ReactiveCombatActorVisual>(true))
                Assert.That(IsMasked(actor), Is.False, "Portal mask must never remain on an actor after wave closure.");
        }

        private sealed class WaveProbe
        {
            private readonly ReactiveCombatArena arena;
            private int phase;
            private float shaderClock = -1f;
            private Vector3 portalPosition;
            private Quaternion portalRotation;
            private readonly Dictionary<int, Vector3> positions = new Dictionary<int, Vector3>();
            private readonly Dictionary<int, Vector3> homes = new Dictionary<int, Vector3>();
            private readonly Dictionary<int, ReactiveCombatActorVisual> predecessors = new Dictionary<int, ReactiveCombatActorVisual>();
            private ReactiveCombatActorVisual previousEntry;
            internal readonly HashSet<int> EnteredActors = new HashSet<int>();
            internal int RootId;
            internal Vector3 PortalCenter, PortalNormal;
            internal bool SawFormation, SawOpen, SawResidual, SawCollapse;
            internal WaveProbe(ReactiveCombatArena arena)
            {
                this.arena = arena;
                foreach (var actor in World().GetComponentsInChildren<ReactiveCombatActorVisual>(true))
                    if (actor.name == "CombatActor_Yokai" && !actor.IsDead) homes.Add(actor.GetInstanceID(), actor.transform.localPosition);
            }
            internal void RecordHome(ReactiveCombatActorVisual actor, Vector3 home) { homes.Add(actor.GetInstanceID(), home); }
            internal Vector3 Home(ReactiveCombatActorVisual actor) => homes[actor.GetInstanceID()];

            internal void Observe()
            {
                int currentPhase = Stage(arena.PortalPhase);
                Assert.That(currentPhase, Is.GreaterThanOrEqualTo(phase), "A shared wave portal must never reseed between actors or reopen after its final collapse.");
                phase = currentPhase;
                SawFormation |= arena.PortalPhase == ReactiveCombatArena.WavePortalPhase.Forming;
                SawResidual |= arena.PortalPhase == ReactiveCombatArena.WavePortalPhase.Residual;
                SawCollapse |= arena.PortalPhase == ReactiveCombatArena.WavePortalPhase.Collapsing;
                if (arena.PortalPhase != ReactiveCombatArena.WavePortalPhase.Inactive && arena.PortalPhase != ReactiveCombatArena.WavePortalPhase.Closed)
                {
                    Assert.That(arena.CommandReady, Is.False);
                    Assert.That(arena.SelectHunterPreview("Attack"), Is.False, "Open portal/queued entrance must gate player commands.");
                    Assert.That(arena.SelectionVisible, Is.False);
                }
                GameObject portal = Portal();
                int portalRoots = 0;
                foreach (Transform child in World().transform)
                    if (child.name == "ReactiveEnemyPortal" && child.gameObject.activeInHierarchy) portalRoots++;
                Assert.That(portalRoots, Is.LessThanOrEqualTo(1));
                if (portal != null)
                {
                    if (RootId == 0) { RootId = portal.GetInstanceID(); portalPosition = portal.transform.position; portalRotation = portal.transform.rotation; }
                    Assert.That(portal.GetInstanceID(), Is.EqualTo(RootId), "Every monster in this wave must use the same real portal root.");
                    Assert.That(portal.transform.position, Is.EqualTo(portalPosition));
                    Assert.That(Quaternion.Angle(portalRotation, portal.transform.rotation), Is.LessThan(.001f));
                    Assert.That(portal.transform.localScale, Is.EqualTo(Vector3.one));
                    Material depth = portal.transform.Find("PortalMovingDepth").GetComponent<MeshRenderer>().sharedMaterial;
                    float clock = depth.GetFloat("_Phase");
                    Assert.That(clock, Is.GreaterThanOrEqualTo(shaderClock), "The actual portal shader must share a continuous presentation clock.");
                    shaderClock = clock;
                    SawOpen |= depth.GetFloat("_Opening") >= .99f;
                    if (arena.PortalPhase == ReactiveCombatArena.WavePortalPhase.EmittingEnemies || arena.PortalPhase == ReactiveCombatArena.WavePortalPhase.Residual)
                        Assert.That(depth.GetFloat("_Opening"), Is.GreaterThanOrEqualTo(.99f), "The aperture must stay open between actors, rather than shrinking and reseeding.");
                    PortalCenter = portal.transform.position; PortalNormal = -portal.transform.forward;
                }
                int masks = 0;
                foreach (var actor in World().GetComponentsInChildren<ReactiveCombatActorVisual>(true))
                {
                    if (actor.name != "CombatActor_Yokai") continue;
                    int id = actor.GetInstanceID();
                    HashSet<Material> maskMaterials = MaskMaterials(actor);
                    if (maskMaterials.Count > 0)
                    {
                        masks++;
                        if (EnteredActors.Add(id))
                        {
                            if (previousEntry != null && !previousEntry.IsDead && previousEntry.gameObject.activeInHierarchy)
                            {
                                Assert.That(previousEntry.transform.localPosition, Is.EqualTo(homes[previousEntry.GetInstanceID()]), "The previous actor must reach its exact slot before the next actor begins entry.");
                                predecessors.Add(id, previousEntry);
                            }
                            previousEntry = actor;
                        }
                        foreach (Material mask in maskMaterials)
                            Assert.That(mask.GetFloat("_PortalPhase"), Is.EqualTo(shaderClock).Within(.001f), "Actor clipping and visible portal boundary must use the same live clock.");
                    }
                    if (!actor.gameObject.activeInHierarchy || actor.IsDead) continue;
                    if (actor.CurrentPose == "Approach" && predecessors.TryGetValue(id, out ReactiveCombatActorVisual predecessor) &&
                        predecessor != null && !predecessor.IsDead && predecessor.gameObject.activeInHierarchy)
                        Assert.That(predecessor.IdleSettled, Is.True, "The authored silhouette gap must let the preceding actor settle before the next actor starts locomotion.");
                    Vector3 position = actor.transform.localPosition;
                    Assert.That(float.IsNaN(position.x) || float.IsInfinity(position.x) || float.IsNaN(position.y) || float.IsInfinity(position.y) || float.IsNaN(position.z) || float.IsInfinity(position.z), Is.False);
                    if (positions.TryGetValue(id, out Vector3 previous)) Assert.That(Vector3.Distance(previous, position), Is.LessThan(.45f), "An activated monster must not teleport between portal and slot.");
                    positions[id] = position;
                }
                Assert.That(masks, Is.LessThanOrEqualTo(1), "Sequential entrances must never mask two monsters at once.");
            }

            private static int Stage(ReactiveCombatArena.WavePortalPhase value)
            {
                switch (value)
                {
                    case ReactiveCombatArena.WavePortalPhase.Forming: return 1;
                    case ReactiveCombatArena.WavePortalPhase.Open:
                    case ReactiveCombatArena.WavePortalPhase.EmittingEnemies: return 2;
                    case ReactiveCombatArena.WavePortalPhase.Residual: return 3;
                    case ReactiveCombatArena.WavePortalPhase.Collapsing: return 4;
                    case ReactiveCombatArena.WavePortalPhase.Closed: return 5;
                    default: return 0;
                }
            }
        }
    }
}
