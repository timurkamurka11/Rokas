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
        private readonly Dictionary<string, float> deathTimes = new Dictionary<string, float>(StringComparer.Ordinal);
        private GameObject world;
        private Camera camera;
        private RenderTexture texture;
        private RawImage image;
        private ReactiveCombatActorVisual hunter;
        private string activeActorId;

        public bool Ready { get { return world != null && hunter != null && texture != null; } }
        public int VisibleEnemyCount { get { return enemies.Count; } }

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
                hunter.transform.localRotation = Quaternion.identity;
                hunter.SetStandingHeight(4.0f);
                SetLayerRecursive(hunter.gameObject, ActorLayer);
                hunter.SetFacing(true);
                hunter.PlayIdle();
            }
        }

        public void SetEnemies(IReadOnlyList<string> activeIds, string actingId)
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
            Layout(activeIds);
        }

        private void Layout(IReadOnlyList<string> activeIds)
        {
            if (activeIds == null) return;
            int count = activeIds.Count;
            for (int i = 0; i < count; i++)
            {
                ReactiveCombatActorVisual actor;
                if (!enemies.TryGetValue(activeIds[i], out actor) || actor == null) continue;
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

        public void Present(CombatEvent combatEvent)
        {
            if (combatEvent == null) return;
            ReactiveCombatActorVisual actor;
            switch (combatEvent.Kind)
            {
                case CombatEventKind.CommandCommitted:
                    if (hunter != null) hunter.PlayAttack();
                    break;
                case CombatEventKind.AttackStarted:
                    if (combatEvent.ActorId != null && enemies.TryGetValue(combatEvent.ActorId, out actor) && actor != null)
                        actor.PlayAttack();
                    break;
                case CombatEventKind.HitResolved:
                    if (combatEvent.TargetId == ReactiveDuelDefinitions.HunterId)
                    {
                        if (hunter != null && combatEvent.Amount > 0) hunter.PlayHit();
                    }
                    else if (combatEvent.TargetId != null && enemies.TryGetValue(combatEvent.TargetId, out actor) && actor != null)
                        actor.PlayHit();
                    break;
                case CombatEventKind.Defeat:
                    if (hunter != null) hunter.PlayDeath();
                    break;
                case CombatEventKind.WaveCleared:
                    foreach (var pair in enemies) Retire(pair.Key);
                    break;
            }
        }

        private void Retire(string id)
        {
            ReactiveCombatActorVisual actor;
            if (string.IsNullOrEmpty(id) || deathTimes.ContainsKey(id) ||
                !enemies.TryGetValue(id, out actor) || actor == null) return;
            actor.PlayDeath();
            deathTimes[id] = Time.unscaledTime + 1.2f;
        }

        public void Tick(float deltaTime)
        {
            if (world == null) return;
            retired.Clear();
            foreach (var pair in deathTimes)
                if (Time.unscaledTime >= pair.Value) retired.Add(pair.Key);
            for (int i = 0; i < retired.Count; i++)
            {
                string id = retired[i];
                ReactiveCombatActorVisual actor;
                if (enemies.TryGetValue(id, out actor) && actor != null)
                    UnityEngine.Object.Destroy(actor.gameObject);
                enemies.Remove(id);
                deathTimes.Remove(id);
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
        }

        private static void SetLayerRecursive(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform) SetLayerRecursive(child.gameObject, layer);
        }
    }
}
