using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    public readonly struct VnIntroArt
    {
        public Texture2D KeikoCharacterSheet { get; }
        public Texture2D MinaCharacterSheet { get; }
        public Texture2D BusStopRainNight { get; }
        public Texture2D NightSkyRain { get; }
        public Texture2D BusStopPhoneMessageMina { get; }
        public Texture2D DialoguePanelKeikoDark { get; }
        public Texture2D DialoguePanelMinaLight { get; }
        public Texture2D IconMute { get; }
        public Texture2D IconPause { get; }
        public Texture2D IconSkip { get; }

        public Texture2D[] AllTextures => new[]
        {
            KeikoCharacterSheet, MinaCharacterSheet, BusStopRainNight, NightSkyRain,
            BusStopPhoneMessageMina, DialoguePanelKeikoDark, DialoguePanelMinaLight,
            IconMute, IconPause, IconSkip
        };

        public VnIntroArt(Texture2D keikoCharacterSheet, Texture2D minaCharacterSheet,
            Texture2D busStopRainNight, Texture2D nightSkyRain, Texture2D busStopPhoneMessageMina,
            Texture2D dialoguePanelKeikoDark, Texture2D dialoguePanelMinaLight,
            Texture2D iconMute, Texture2D iconPause, Texture2D iconSkip)
        {
            KeikoCharacterSheet = Require(keikoCharacterSheet, nameof(keikoCharacterSheet));
            MinaCharacterSheet = Require(minaCharacterSheet, nameof(minaCharacterSheet));
            BusStopRainNight = Require(busStopRainNight, nameof(busStopRainNight));
            NightSkyRain = Require(nightSkyRain, nameof(nightSkyRain));
            BusStopPhoneMessageMina = Require(busStopPhoneMessageMina, nameof(busStopPhoneMessageMina));
            DialoguePanelKeikoDark = Require(dialoguePanelKeikoDark, nameof(dialoguePanelKeikoDark));
            DialoguePanelMinaLight = Require(dialoguePanelMinaLight, nameof(dialoguePanelMinaLight));
            IconMute = Require(iconMute, nameof(iconMute));
            IconPause = Require(iconPause, nameof(iconPause));
            IconSkip = Require(iconSkip, nameof(iconSkip));
        }

        private static Texture2D Require(Texture2D texture, string name)
        {
            if (!texture) throw new ArgumentNullException(name);
            return texture;
        }
    }

    public sealed class VnIntroView : IDisposable
    {
        private static readonly Rect KeikoNeutralCrop = new Rect(.49f, .47f, .235f, .43f);
        private static readonly Rect MinaNeutralCrop = new Rect(.47f, .47f, .25f, .43f);

        private readonly VnIntroArt art;
        private readonly GameObject root;
        private readonly RawImage background;
        private readonly RawImage portrait;
        private readonly RawImage dialoguePanel;
        private readonly Text speakerName;
        private readonly Text dialogueText;
        private readonly Image pauseButtonBackground;
        private bool disposed;

        private VnIntroView(Transform parent, Font font, VnIntroArt art,
            Action continueStory, Action toggleMute, Action togglePause, Action skip)
        {
            if (!parent) throw new ArgumentNullException(nameof(parent));
            if (!font) throw new ArgumentNullException(nameof(font));
            this.art = art;

            root = new GameObject("VnIntroRoot", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            RectTransform rootRect = (RectTransform)root.transform;
            Stretch(rootRect);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;

            background = Raw(rootRect, "Background", null, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Image clickSurface = Image(rootRect, "StoryClickSurface", new Color(0f, 0f, 0f, 0f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            Button storyButton = clickSurface.gameObject.AddComponent<Button>();
            storyButton.targetGraphic = clickSurface;
            storyButton.transition = Selectable.Transition.None;
            storyButton.onClick.AddListener(() => continueStory?.Invoke());

            portrait = Raw(rootRect, "Portrait", null,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(174f, -1010f), new Vector2(436f, -748f));
            portrait.raycastTarget = false;

            dialoguePanel = Raw(rootRect, "DialoguePanel", art.DialoguePanelKeikoDark,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(120f, -1050f), new Vector2(1800f, -730f));
            dialoguePanel.raycastTarget = false;

            speakerName = Label(rootRect, "SpeakerName", font, string.Empty,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(472f, -834f), new Vector2(820f, -770f),
                31, FontStyle.Bold, TextAnchor.MiddleLeft);
            dialogueText = Label(rootRect, "DialogueText", font, string.Empty,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(472f, -1018f), new Vector2(1604f, -842f),
                28, FontStyle.Normal, TextAnchor.UpperLeft);

            CreateIconButton(rootRect, "MuteButton", art.IconMute, 1518f, 38f, toggleMute, out _);
            CreateIconButton(rootRect, "PauseButton", art.IconPause, 1624f, 38f, togglePause, out pauseButtonBackground);
            CreateIconButton(rootRect, "SkipButton", art.IconSkip, 1730f, 38f, skip, out _);
        }

        public static VnIntroView Create(Transform parent, Font font, VnIntroArt art,
            Action continueStory, Action toggleMute, Action togglePause, Action skip)
        {
            return new VnIntroView(parent, font, art, continueStory, toggleMute, togglePause, skip);
        }

        public void ApplyBeat(VnIntroBeatState state)
        {
            ThrowIfDisposed();
            switch (state.BackgroundId)
            {
                case "bus_stop":
                    background.texture = art.BusStopRainNight;
                    break;
                case "night_sky":
                    background.texture = art.NightSkyRain;
                    break;
                case "phone":
                    background.texture = art.BusStopPhoneMessageMina;
                    break;
                default:
                    throw new ArgumentException("Unknown VN intro background id: " + state.BackgroundId, nameof(state));
            }

            if (state.PanelStyle == "dark")
            {
                dialoguePanel.texture = art.DialoguePanelKeikoDark;
            }
            else if (state.PanelStyle == "light")
            {
                dialoguePanel.texture = art.DialoguePanelMinaLight;
            }
            else
            {
                throw new ArgumentException("Unknown VN intro panel style: " + state.PanelStyle, nameof(state));
            }

            if (state.PortraitId == "keiko_neutral")
            {
                portrait.texture = art.KeikoCharacterSheet;
                portrait.uvRect = KeikoNeutralCrop;
            }
            else if (state.PortraitId == "mina_neutral")
            {
                portrait.texture = art.MinaCharacterSheet;
                portrait.uvRect = MinaNeutralCrop;
            }
            else
            {
                throw new ArgumentException("Unknown VN intro portrait id: " + state.PortraitId, nameof(state));
            }

            speakerName.text = state.Speaker ?? string.Empty;
        }

        public void PresentLine(string speaker, string text)
        {
            ThrowIfDisposed();
            speakerName.text = speaker ?? string.Empty;
            dialogueText.text = text ?? string.Empty;
        }

        public void SetPausedVisual(bool paused)
        {
            ThrowIfDisposed();
            pauseButtonBackground.color = paused
                ? new Color(.52f, .38f, .18f, .94f)
                : new Color(.035f, .055f, .06f, .78f);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (root) UnityEngine.Object.Destroy(root);
        }

        private static void CreateIconButton(RectTransform parent, string name, Texture2D icon,
            float x, float y, Action action, out Image backgroundImage)
        {
            RectTransform rect = Rect(parent, name,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(x, -(y + 82f)), new Vector2(x + 82f, -y));
            backgroundImage = rect.gameObject.AddComponent<Image>();
            backgroundImage.color = new Color(.035f, .055f, .06f, .78f);
            backgroundImage.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = backgroundImage;
            button.onClick.AddListener(() => action?.Invoke());

            RawImage iconImage = Raw(rect, "Icon", icon,
                new Vector2(.16f, .16f), new Vector2(.84f, .84f), Vector2.zero, Vector2.zero);
            iconImage.raycastTarget = false;
        }

        private static RawImage Raw(Transform parent, string name, Texture texture,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rect = Rect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax);
            RawImage raw = rect.gameObject.AddComponent<RawImage>();
            raw.texture = texture;
            raw.color = Color.white;
            raw.raycastTarget = false;
            return raw;
        }

        private static Image Image(Transform parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, bool raycast)
        {
            RectTransform rect = Rect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private static Text Label(Transform parent, string name, Font font, string value,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            int size, FontStyle style, TextAnchor alignment)
        {
            RectTransform rect = Rect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Color.white;
            text.text = value;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(VnIntroView));
        }
    }
}
