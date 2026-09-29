using System;
using System.Collections.Generic;
using Rokas.Core;
using Rokas.Core.ReactiveTurns;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    // A separate camera renders only the animated actors into the illustrated arena.
    // Combat rules and targeting remain in Core and the HUD; this layer is visual only.
    public sealed class ReactiveCombatArena
    {
        private const int ActorLayer = 30;
        private readonly Dictionary<string, ReactiveCombatActorVisual> enemies =
            new Dictionary<string, ReactiveCombatActorVisual>(StringComparer.Ordinal);
        private readonly List<string> retired = new List<string>();
        private readonly List<string> dying = new List<string>();
        private readonly Dictionary<string, float> deathTimes = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector3> deathScales = new Dictionary<string, Vector3>(StringComparer.Ordinal);
        private enum HunterMotion { None, Approach, Return }
        private enum HunterCommandPose { None, Attack, Heavy, Defend }
        private HunterMotion hunterMotion;
        private HunterCommandPose hunterCommandPose;
        private Vector3 hunterHome;
        private Vector3 hunterMotionStart;
        private Vector3 hunterMotionGoal;
        private float hunterMotionElapsed;
        private float hunterMotionDuration;
        private string hunterMotionTargetId;
        private bool hunterApproachComplete;
        private GameObject world;
        private Camera camera;
        private RenderTexture texture;
        private RawImage image;
        private ReactiveCombatActorVisual hunter;
        private string activeActorId;
        private string heavyHunterActionId;
        private string heavyEnemyActionId;

        public bool Ready { get { return world != null && hunter != null && texture != null; } }
        public int VisibleEnemyCount { get { return enemies.Count; } }
        public bool HunterApproachComplete { get { return hunterApproachComplete && hunterMotion == HunterMotion.None; } }
        public bool HunterAtHome { get { return hunter != null && hunterMotion == HunterMotion.None &&
            (hunter.transform.localPosition - hunterHome).sqrMagnitude < .0001f; } }

        public ReactiveCombatArena(UiKit ui, RectTransform parent)
        {
            image = ui.Art(parent, "ReactiveAnimatedWorld", null, 0, 0, 1920, 906);
            image.color = Color.white;
            world = new GameObject("ReactiveCombatWorld");
            // Keep models outside all normal cameras, including PlayMode screenshot cameras.
            world.transform.position = new Vector3(1000f, 1000f, 1000f);
            texture = new RenderTexture(1920, 906, 24, RenderTextureFormat.ARGB32)
            {
                name = "ReactiveCombatActors",
                antiAliasing = 2,
                filterMode = FilterMode.Bilinear,
                useMipMap = false
            };
            texture.Create();
            image.texture = texture;

            var cameraObject = new GameObject("ReactiveActorCamera", typeof(Camera));
            cameraObject.transform.SetParent(world.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 2.25f, -20f);
            cameraObject.transform.localRotation = Quaternion.identity;
            camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.6f;
            camera.aspect = 1920f / 906f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 60f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << ActorLayer;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.targetTexture = texture;

            var keyObject = new GameObject("ReactiveActorKey", typeof(Light));
            keyObject.transform.SetParent(world.transform, false);
            keyObject.transform.localRotation = Quaternion.Euler(32f, -36f, 0f);
            var key = keyObject.GetComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(.76f, .86f, 1f);
            key.intensity = 1.7f;
            key.cullingMask = 1 << ActorLayer;
            key.shadows = LightShadows.None;

            var rimObject = new GameObject("ReactiveActorRim", typeof(Light));
            rimObject.transform.SetParent(world.transform, false);
            rimObject.transform.localRotation = Quaternion.Euler(25f, 145f, 0f);
            var rim = rimObject.GetComponent<Light>();
            rim.type = LightType.Directional;
            rim.color = new Color(1f, .19f, .32f);
            rim.intensity = .9f;
            rim.cullingMask = 1 << ActorLayer;
            rim.shadows = LightShadows.None;

            hunter = ReactiveCombatActorVisual.Spawn(CombatActorKind.Keiko, world.transform);
            if (hunter != null)
            {
                hunter.transform.localPosition = new Vector3(-5.25f, -.62f, 0f);
                hunterHome = hunter.transform.localPosition;
                hunter.transform.localRotation = Quaternion.identity;
                hunter.SetStandingHeight(4.0f);
                SetLayerRecursive(hunter.gameObject, ActorLayer);
                hunter.SetFacing(true);
                hunter.PlayIdle();
            }
        }

        public void SetEnemies(IReadOnlyList<string> activeIds, string actingId)
        {
            SetEnemies(activeIds, actingId, activeIds);
        }

        public void SetEnemies(IReadOnlyList<string> activeIds, string actingId,
            IReadOnlyList<string> waveEnemyIds)
        {
            if (world == null) return;
            activeActorId = actingId;
            var active = new HashSet<string>(StringComparer.Ordinal);
            if (activeIds != null)
            {
                for (int i = 0; i < activeIds.Count; i++)
                {
                    string id = activeIds[i];
                    if (string.IsNullOrEmpty(id)) continue;
                    active.Add(id);
                    ReactiveCombatActorVisual actor;
                    if (!enemies.TryGetValue(id, out actor) || actor == null)
                    {
                        actor = ReactiveCombatActorVisual.Spawn(CombatActorKind.Yokai, world.transform);
                        if (actor == null) continue;
                        enemies[id] = actor;
                        actor.SetStandingHeight(3.65f);
                        SetLayerRecursive(actor.gameObject, ActorLayer);
                        actor.SetFacing(false);
                        actor.PlayWaveEnter();
                    }
                }
            }
            retired.Clear();
            foreach (var pair in enemies)
                if (!active.Contains(pair.Key) && !deathTimes.ContainsKey(pair.Key)) retired.Add(pair.Key);
            for (int i = 0; i < retired.Count; i++) Retire(retired[i]);
            Layout(activeIds, waveEnemyIds != null && waveEnemyIds.Count > 0 ? waveEnemyIds : activeIds);
            if (hunterMotion == HunterMotion.Approach && hunterMotionTargetId != null &&
                !active.Contains(hunterMotionTargetId)) CancelHunterMotion();
        }

        private void Layout(IReadOnlyList<string> activeIds, IReadOnlyList<string> waveEnemyIds)
        {
            if (activeIds == null) return;
            int count = waveEnemyIds == null ? activeIds.Count : waveEnemyIds.Count;
            for (int activeIndex = 0; activeIndex < activeIds.Count; activeIndex++)
            {
                string id = activeIds[activeIndex];
                ReactiveCombatActorVisual actor;
                if (!enemies.TryGetValue(id, out actor) || actor == null) continue;
                int i = activeIndex;
                if (waveEnemyIds != null)
                    for (int slot = 0; slot < waveEnemyIds.Count; slot++)
                        if (waveEnemyIds[slot] == id) { i = slot; break; }
                float x;
                float y;
                float z;
                float scale;
                if (count == 1)
                {
                    x = 4.1f; y = -.54f; z = 0f; scale = 1f;
                }
                else if (count == 2)
                {
                    x = i == 0 ? 2.55f : 5.65f;
                    y = i == 0 ? -.4f : -.64f;
                    z = i == 0 ? 1f : 0f;
                    scale = i == 0 ? .85f : .93f;
                }
                else if (count == 3)
                {
                    x = i == 0 ? 2.3f : i == 1 ? 4.55f : 6.65f;
                    y = i == 1 ? -.38f : -.66f;
                    z = i == 1 ? 1.5f : 0f;
                    scale = i == 1 ? .77f : .83f;
                }
                else
                {
                    x = 1.75f + 1.75f * i;
                    y = i == 1 || i == 2 ? -.38f : -.66f;
                    z = i == 1 || i == 2 ? 1.5f : 0f;
                    scale = .71f;
                }
                actor.transform.localPosition = new Vector3(x, y, z);
                actor.transform.localScale = Vector3.one * scale;
            }
        }

        // Movement precedes the committed command. Core still owns the impact and damage.
        public bool StartHunterApproach(string targetId)
        {
            ReactiveCombatActorVisual target;
            if (hunter == null || string.IsNullOrEmpty(targetId) ||
                !enemies.TryGetValue(targetId, out target) || target == null ||
                target.IsDead || deathTimes.ContainsKey(targetId)) return false;
            if (hunterMotion == HunterMotion.Approach && hunterMotionTargetId == targetId) return true;
            Vector3 targetPosition = target.transform.localPosition;
            Vector3 attackPoint = new Vector3(targetPosition.x - 1.35f, hunterHome.y,
                targetPosition.z - .45f);
            hunterMotionTargetId = targetId;
            hunterApproachComplete = false;
            BeginHunterMotion(HunterMotion.Approach, attackPoint, .7f);
            hunter.PlayApproach(hunterMotionDuration);
            return true;
        }

        public void StartHunterReturn()
        {
            if (hunter == null || hunterMotion == HunterMotion.Return) return;
            hunterCommandPose = HunterCommandPose.None;
            hunterMotionTargetId = null;
            hunterApproachComplete = false;
            if ((hunter.transform.localPosition - hunterHome).sqrMagnitude < .0001f)
            {
                hunter.transform.localPosition = hunterHome;
                hunterMotion = HunterMotion.None;
                return;
            }
            BeginHunterMotion(HunterMotion.Return, hunterHome, .62f);
            hunter.PlayReturnHome(hunterMotionDuration);
        }

        public void CancelHunterMotion()
        {
            hunterMotion = HunterMotion.None;
            hunterCommandPose = HunterCommandPose.None;
            hunterMotionTargetId = null;
            hunterApproachComplete = false;
            if (hunter == null) return;
            hunter.transform.localPosition = hunterHome;
            hunter.PlayIdle();
        }

        private void BeginHunterMotion(HunterMotion motion, Vector3 goal, float seconds)
        {
            hunterMotion = motion;
            hunterMotionStart = hunter.transform.localPosition;
            hunterMotionGoal = goal;
            hunterMotionElapsed = 0f;
            hunterMotionDuration = Mathf.Max(.1f, seconds);
        }

        public void Present(CombatEvent combatEvent)
        {
            if (combatEvent == null) return;
            ReactiveCombatActorVisual actor;
            switch (combatEvent.Kind)
            {
                case CombatEventKind.CommandCommitted:
                    heavyHunterActionId = combatEvent.Detail == "heavy" ? combatEvent.ActionId : null;
                    if (hunter == null) break;
                    hunterCommandPose = combatEvent.Detail == "heavy" ? HunterCommandPose.Heavy :
                        combatEvent.Detail == "Defend" ? HunterCommandPose.Defend :
                        combatEvent.Detail == "Retreat" ? HunterCommandPose.None : HunterCommandPose.Attack;
                    if (hunterMotion != HunterMotion.Approach) PlayHunterCommandPose();
                    break;
                case CombatEventKind.AttackStarted:
                    heavyEnemyActionId = combatEvent.Detail == "heavy" ? combatEvent.ActionId : null;
                    if (combatEvent.ActorId != null && enemies.TryGetValue(combatEvent.ActorId, out actor) && actor != null)
                    {
                        if (combatEvent.Detail == "heavy") actor.PlayHeavy();
                        else actor.PlayAttack();
                    }
                    break;
                case CombatEventKind.HitResolved:
                    if (combatEvent.TargetId == ReactiveDuelDefinitions.HunterId)
                    {
                        if (hunter != null && combatEvent.Amount > 0)
                        {
                            if (heavyEnemyActionId != null && combatEvent.ActionId == heavyEnemyActionId)
                                hunter.PlayStagger();
                            else hunter.PlayHit();
                        }
                    }
                    else if (combatEvent.TargetId != null && enemies.TryGetValue(combatEvent.TargetId, out actor) && actor != null)
                    {
                        if (heavyHunterActionId != null && combatEvent.ActionId == heavyHunterActionId)
                            actor.PlayStagger();
                        else actor.PlayHit();
                    }
                    break;
                case CombatEventKind.Defeat:
                    CancelHunterMotion();
                    if (hunter != null) hunter.PlayDeath();
                    break;
                case CombatEventKind.WaveCleared:
                    StartHunterReturn();
                    foreach (var pair in enemies) Retire(pair.Key);
                    break;
                case CombatEventKind.ActionSettled:
                    if (combatEvent.ActorId == ReactiveDuelDefinitions.HunterId) StartHunterReturn();
                    break;
                case CombatEventKind.CommandCancelled:
                    StartHunterReturn();
                    break;
            }
        }

        private void PlayHunterCommandPose()
        {
            if (hunter == null) return;
            switch (hunterCommandPose)
            {
                case HunterCommandPose.Attack: hunter.PlayAttack(.4f); break;
                case HunterCommandPose.Heavy: hunter.PlayHeavy(.4f); break;
                case HunterCommandPose.Defend: hunter.PlayDefend(); break;
            }
        }

        private void Retire(string id)
        {
            ReactiveCombatActorVisual actor;
            if (string.IsNullOrEmpty(id) || deathTimes.ContainsKey(id) ||
                !enemies.TryGetValue(id, out actor) || actor == null) return;
            if (hunterMotion == HunterMotion.Approach && hunterMotionTargetId == id)
                CancelHunterMotion();
            actor.PlayDeath();
            deathTimes[id] = 1.1f;
            deathScales[id] = actor.transform.localScale;
        }

        public void Tick(float deltaTime)
        {
            if (world == null) return;
            float step = Mathf.Max(0f, deltaTime);
            if (hunter != null && hunterMotion != HunterMotion.None)
            {
                hunterMotionElapsed = Mathf.Min(hunterMotionDuration, hunterMotionElapsed + step);
                float fraction = Mathf.Clamp01(hunterMotionElapsed / hunterMotionDuration);
                hunter.transform.localPosition = Vector3.Lerp(hunterMotionStart, hunterMotionGoal,
                    Mathf.SmoothStep(0f, 1f, fraction));
                if (fraction >= 1f)
                {
                    hunter.transform.localPosition = hunterMotionGoal;
                    hunterApproachComplete = hunterMotion == HunterMotion.Approach;
                    hunterMotion = HunterMotion.None;
                    hunterMotionTargetId = null;
                    if (hunterApproachComplete && hunterCommandPose != HunterCommandPose.None)
                        PlayHunterCommandPose();
                    else hunter.PlayIdle();
                }
            }
            retired.Clear();
            foreach (var pair in deathTimes)
            {
                float remaining = Mathf.Max(0f, pair.Value - step);
                if (remaining <= 0f) { retired.Add(pair.Key); continue; }
                ReactiveCombatActorVisual actor;
                Vector3 originalScale;
                if (remaining < .2f && enemies.TryGetValue(pair.Key, out actor) && actor != null &&
                    deathScales.TryGetValue(pair.Key, out originalScale))
                    actor.transform.localScale = originalScale * Mathf.Clamp01(remaining / .2f);
            }
            // Keep dictionary updates outside enumeration.
            dying.Clear();
            foreach (string id in deathTimes.Keys) dying.Add(id);
            for (int i = 0; i < dying.Count; i++)
                deathTimes[dying[i]] = Mathf.Max(0f, deathTimes[dying[i]] - step);
            for (int i = 0; i < retired.Count; i++)
            {
                string id = retired[i];
                ReactiveCombatActorVisual actor;
                if (enemies.TryGetValue(id, out actor) && actor != null)
                    UnityEngine.Object.Destroy(actor.gameObject);
                enemies.Remove(id);
                deathTimes.Remove(id);
                deathScales.Remove(id);
            }
        }

        public void Dispose()
        {
            if (camera != null) camera.targetTexture = null;
            if (image != null) image.texture = null;
            if (world != null) UnityEngine.Object.Destroy(world);
            if (texture != null)
            {
                texture.Release();
                UnityEngine.Object.Destroy(texture);
            }
            world = null;
            camera = null;
            texture = null;
            image = null;
            hunter = null;
            enemies.Clear();
            deathTimes.Clear();
            deathScales.Clear();
            hunterMotion = HunterMotion.None;
        }

        private static void SetLayerRecursive(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform) SetLayerRecursive(child.gameObject, layer);
        }
    }
}
