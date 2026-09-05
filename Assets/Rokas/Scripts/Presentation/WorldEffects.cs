using Rokas.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    public sealed class WorldEffects
    {
        private readonly RectTransform background;
        private readonly SettingsData settings;
        private readonly Image darkness;
        private readonly Image portalLight;
        private readonly RectTransform[] rain = new RectTransform[44];
        private readonly RectTransform[] steam = new RectTransform[7];
        private readonly Image[] tears = new Image[3];
        private readonly float[] rainPhase = new float[44];
        private readonly RectTransform rainRoot;
        private readonly RectTransform steamRoot;
        private float time;
        private float impact;
        private float glitch;
        private bool atHome;
        private bool atPortal;

        public WorldEffects(UiKit ui, RectTransform parent, RectTransform background, SettingsData settings)
        {
            this.background = background;
            this.settings = settings;
            darkness = ui.Box(parent, "LampMood", 0, 0, 1920, 1080, Color.clear);
            portalLight = ui.Box(parent, "PortalBreath", 0, 100, 1920, 906, Color.clear);
            rainRoot = ui.Rect(parent, "WindowRain", 350, 107, 605, 396);
            rainRoot.gameObject.AddComponent<RectMask2D>();
            var random = new System.Random(719);
            for (int i = 0; i < rain.Length; i++)
            {
                rainPhase[i] = (float)random.NextDouble();
                rain[i] = ui.Box(rainRoot, "RainDrop" + i, (float)random.NextDouble() * 605, 0, .9f, 9 + (float)random.NextDouble() * 22,
                    new Color(.62f, .85f, .9f, .14f + (float)random.NextDouble() * .18f)).rectTransform;
                rain[i].localRotation = Quaternion.Euler(0, 0, 12);
            }
            steamRoot = ui.Rect(parent, "TeaSteam", 791, 535, 50, 90);
            for (int i = 0; i < steam.Length; i++)
            {
                steam[i] = ui.Box(steamRoot, "Steam" + i, 13, 0, 2, 13, new Color(.96f, .9f, .72f, .08f)).rectTransform;
                steam[i].localRotation = Quaternion.Euler(0, 0, -25 + i * 7);
            }
            for (int i = 0; i < tears.Length; i++)
                tears[i] = ui.Box(parent, "RealityTear" + i, 0, 285 + i * 217, 1920, 2 + i, Color.clear);
        }

        public void SetLocation(bool home, bool portal)
        {
            atHome = home;
            atPortal = portal;
            rainRoot.gameObject.SetActive(home || portal);
            rainRoot.anchoredPosition = home ? new Vector2(350, -107) : new Vector2(0, -100);
            rainRoot.sizeDelta = home ? new Vector2(605, 396) : new Vector2(1920, 906);
            steamRoot.gameObject.SetActive(home);
            if (portal) glitch = .22f;
            impact = 0;
        }

        public void Impact(float strength)
        {
            impact = Mathf.Max(impact, strength * .18f);
            if (strength >= .9f) glitch = .2f;
        }

        public void Tick(float dt, bool lampOn)
        {
            time += dt;
            impact = Mathf.Max(0, impact - dt);
            glitch = Mathf.Max(0, glitch - dt);
            darkness.color = new Color(.01f, .045f, .07f, atHome && !lampOn ? .67f : 0);
            portalLight.color = new Color(.24f, .47f, .6f, atPortal ? .018f + Mathf.Sin(time * .8f) * .011f : 0);
            float shake = settings.screenShake ? impact * 17 : 0;
            float distortion = glitch * Mathf.Clamp(settings.glitchIntensity, 0, 1.5f);
            background.anchoredPosition = new Vector2(Mathf.Sin(time * 63) * shake + distortion * 8, Mathf.Cos(time * 53) * shake);
            for (int i = 0; i < tears.Length; i++)
                tears[i].color = new Color(i == 1 ? .8f : .26f, .62f, .62f, distortion * .25f);
            float areaH = atHome ? 396 : 906;
            for (int i = 0; i < rain.Length; i++)
            {
                var p = rain[i].anchoredPosition;
                p.x = atHome ? i * 13.6f : i * 43.6f;
                p.y = -Mathf.Repeat(time * (80 + i % 4 * 22) + rainPhase[i] * areaH, areaH);
                rain[i].anchoredPosition = p;
            }
            for (int i = 0; i < steam.Length; i++)
            {
                float t = Mathf.Repeat(time * .32f + i / 7f, 1);
                steam[i].anchoredPosition = new Vector2(18 + Mathf.Sin(t * 6 + i) * 5, -72 + t * 64);
            }
        }
    }
}
