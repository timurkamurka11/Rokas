using System;
using Rokas.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    // Fixed proxy pool and camera; domain snapshots are the only source of combat state.
    public sealed class Combat3ArenaView : IDisposable
    {
        private const float LaneSpacing = 2.4f;
        private readonly GameSession session;
        private readonly GameObject worldRoot;
        private readonly Camera camera;
        private readonly RenderTexture texture;
        private readonly Material floorMaterial;
        private readonly Material playerMaterial;
        private readonly Material enemyMaterial;
        private readonly Material warningMaterial;
        private readonly Material dangerMaterial;
        private readonly Material hitMaterial;
        private readonly Renderer player;
        private readonly Renderer enemy;
        private readonly Renderer warning;
        private readonly Renderer strike;
        private readonly Text health;
        private readonly Text enemyHealth;
        private readonly Text seal;
        private readonly Text resonance;
        private readonly Text attackCue;
        private readonly Text counterCue;
        private readonly Text bonusCue;
        private readonly Combat3InputSurface input;
        private float playerHit;
        private float enemyHit;

        public Combat3ArenaView(UiKit ui, RectTransform parent, GameSession session, Func<bool> paused)
        {
            this.session = session;
            ui.Box(parent, "Combat3Backdrop", 0, 100, 1920, 906, new Color(.035f, .055f, .085f));
            ui.Label(parent, "Combat3Title", "БОЙ 3 / ПРОБНАЯ АРЕНА", 70, 130, 1100, 50, 27, UiKit.Paper, true);
            attackCue = ui.Label(parent, "Combat3AttackCue", "", 300, 190, 1320, 50, 28, UiKit.Gold, true, TextAnchor.MiddleCenter);
            health = ui.Label(parent, "Combat3PlayerHp", "", 70, 340, 250, 130, 25, UiKit.Paper, true);
            enemyHealth = ui.Label(parent, "Combat3EnemyHp", "", 1625, 340, 250, 130, 25, UiKit.Paper, true);
            seal = ui.Label(parent, "Combat3Seal", "", 70, 500, 250, 100, 23, UiKit.Gold);
            resonance = ui.Label(parent, "Combat3Resonance", "", 1625, 500, 250, 130, 23, UiKit.Gold);
            counterCue = ui.Label(parent, "Combat3CounterCue", "", 300, 830, 1320, 55, 30, UiKit.Gold, true, TextAnchor.MiddleCenter);
            bonusCue = ui.Label(parent, "Combat3BonusCue", "", 300, 885, 1320, 45, 20, UiKit.Paper, false, TextAnchor.MiddleCenter);
            ui.Label(parent, "Combat3Controls", "A / D  или  ← / → — шаг     ·     ЛКМ — контратака в открытое окно     ·     R — резонанс", 180, 947, 1560, 40, 21, UiKit.Muted, false, TextAnchor.MiddleCenter);

            worldRoot = new GameObject("Combat3ProxyWorld");
            worldRoot.transform.SetParent(parent.root, false);
            // Keep preview geometry outside the existing Home weather cameras, without editing layers/settings.
            worldRoot.transform.localPosition = new Vector3(10000, 0, 0);
            floorMaterial = Material(new Color(.13f, .21f, .29f));
            playerMaterial = Material(new Color(.6f, .95f, .94f));
            enemyMaterial = Material(new Color(.62f, .51f, .77f));
            warningMaterial = Material(new Color(.95f, .56f, .15f));
            dangerMaterial = Material(new Color(.95f, .2f, .25f));
            hitMaterial = Material(new Color(1, .93f, .67f));
            for (int lane = 0; lane < 5; lane++)
                Proxy("Combat3Lane" + lane, PrimitiveType.Cube, new Vector3(X(lane), -.12f, 2.1f), new Vector3(2.25f, .1f, 9), floorMaterial);
            player = Proxy("Combat3Player", PrimitiveType.Capsule, new Vector3(0, .6f, -.7f), new Vector3(.6f, .6f, .6f), playerMaterial);
            enemy = Proxy("Combat3Enemy", PrimitiveType.Capsule, new Vector3(0, .95f, 5.3f), new Vector3(1.05f, .95f, 1.05f), enemyMaterial);
            warning = Proxy("Combat3HeavyTelegraph", PrimitiveType.Cube, new Vector3(0, -.025f, 2.1f), new Vector3(2.05f, .045f, 9), warningMaterial);
            strike = Proxy("Combat3HeavyStrike", PrimitiveType.Cube, new Vector3(0, .7f, -.7f), new Vector3(1.45f, 1.4f, .45f), dangerMaterial);

            var cameraRoot = new GameObject("Combat3ArenaCamera", typeof(Camera));
            cameraRoot.transform.SetParent(worldRoot.transform, false);
            cameraRoot.transform.localPosition = new Vector3(0, 7, -8);
            cameraRoot.transform.LookAt(worldRoot.transform.TransformPoint(new Vector3(0, 0, 2.1f)));
            camera = cameraRoot.GetComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f, .055f, .085f);
            camera.orthographic = true; camera.orthographicSize = 4.2f;
            camera.nearClipPlane = .1f; camera.farClipPlane = 40;
            texture = new RenderTexture(1320, 630, 24) { name = "Combat3ArenaTexture" };
            camera.targetTexture = texture;
            RawImage surface = ui.Art(parent, "Combat3ArenaSurface", texture, 300, 240, 1320, 590);
            surface.raycastTarget = true;
            input = surface.gameObject.AddComponent<Combat3InputSurface>();
            input.Initialize(session, paused);
            Refresh();
            Render();
        }

        public void HandleInput(bool left, bool right, bool attack) { input.Sample(left, right, attack); }
        public void CancelInput() { input.CancelInput(); }
        public void OnHit(CombatHit hit) { if (hit.targetIsEnemy) enemyHit = .12f; else playerHit = .12f; }

        public void Tick(float dt, bool paused)
        {
            if (!paused)
            {
                playerHit = Mathf.Max(0, playerHit - dt);
                enemyHit = Mathf.Max(0, enemyHit - dt);
            }
            Refresh();
            Render();
        }

        public void Refresh()
        {
            Combat3Encounter encounter = session.Combat.Combat3;
            if (encounter == null) return;
            player.transform.localPosition = new Vector3(X(encounter.LanePosition), .6f, -.7f);
            player.sharedMaterial = playerHit > 0 ? dangerMaterial : playerMaterial;
            enemy.sharedMaterial = enemyHit > 0 ? hitMaterial : enemyMaterial;
            warning.transform.localPosition = new Vector3(X(encounter.AttackLane), -.025f, 2.1f);
            warning.gameObject.SetActive(encounter.StageName == "Telegraph" || encounter.StageName == "Active");
            warning.sharedMaterial = encounter.StageName == "Active" ? dangerMaterial : warningMaterial;
            strike.transform.localPosition = new Vector3(X(encounter.AttackLane), .7f, -.7f);
            strike.gameObject.SetActive(encounter.StageName == "Active");
            health.text = "ОХОТНИК\n" + Mathf.CeilToInt(session.State.playerHp) + " / 100";
            enemyHealth.text = "ПРОТИВНИК\n" + Mathf.CeilToInt(session.State.enemyHp) + " / " + Mathf.CeilToInt(session.Contract.enemyHealth);
            seal.text = "ПЕЧАТЬ\n" + Mathf.CeilToInt(session.Combat.Seal) + " / 100";
            resonance.text = "РЕЗОНАНС\n" + Mathf.CeilToInt(session.Combat.Resonance) + " / 100" + (session.Combat.ResonanceReserved ? "\nЗаряжен" : "");
            bool window = encounter.StageName == "CounterWindow";
            attackCue.text = encounter.ReadDelayRemaining > 0 ? "ПРОЧИТАЙТЕ ПОЛЕ  ·  " + encounter.ReadDelayRemaining.ToString("0.0")
                : encounter.StageName == "Telegraph" ? "ТЯЖЁЛЫЙ УДАР  ·  ПОЛОСА " + (encounter.AttackLane + 1) + "  ·  " + encounter.StageRemaining.ToString("0.00")
                : encounter.StageName == "Active" ? "УДАР" : "";
            counterCue.text = window ? encounter.CounterAvailable ? "ЛКМ — КОНТРАТАКА   " + encounter.CounterWindowRemaining.ToString("0.00") : "КОНТРАТАКА ВЫПОЛНЕНА"
                : encounter.StageName == "Recovery" ? "ПРОТИВНИК ОТКРЫВАЕТСЯ…" : "УЙДИТЕ С ОТМЕЧЕННОЙ ПОЛОСЫ";
            bonusCue.text = session.Combat.SealBonusPending ? "Печать сломана: следующая контратака +60%" : "";
        }

        private void Render()
        {
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) camera.Render();
        }

        private static float X(float lane) { return (lane - 2) * LaneSpacing; }
        private static Material Material(Color color)
        {
            return new Material(Shader.Find("Unlit/Color")) { color = color };
        }
        private Renderer Proxy(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.name = name; item.transform.SetParent(worldRoot.transform, false);
            item.transform.localPosition = position; item.transform.localScale = scale;
            UnityEngine.Object.Destroy(item.GetComponent<Collider>());
            Renderer renderer = item.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            return renderer;
        }
        public void Dispose()
        {
            CancelInput();
            camera.targetTexture = null; texture.Release();
            UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(worldRoot);
            UnityEngine.Object.Destroy(floorMaterial); UnityEngine.Object.Destroy(playerMaterial);
            UnityEngine.Object.Destroy(enemyMaterial); UnityEngine.Object.Destroy(warningMaterial);
            UnityEngine.Object.Destroy(dangerMaterial); UnityEngine.Object.Destroy(hitMaterial);
        }
    }
}
