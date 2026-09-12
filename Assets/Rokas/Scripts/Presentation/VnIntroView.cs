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

    internal sealed class VnIntroPresentationTicker : MonoBehaviour
    {
        private VnIntroView owner;

        public void Bind(VnIntroView view)
        {
            owner = view;
        }

        private void Update()
        {
            owner?.TickPresentation(Time.unscaledDeltaTime);
        }
    }

    public sealed class VnIntroView : IDisposable
    {
        private const float ExpressionCrossfadeDuration = .18f;
        private const float CharacterBodyHeight = 980f;
        private const float IdlePeriodSeconds = 2.8f;
        private const float IdleBobPixels = 3f;
        private const float IdleScaleAmount = .0045f;
        private static readonly Rect KeikoNeutralCrop = new Rect(.49f, .47f, .235f, .43f);
        private static readonly Rect MinaNeutralCrop = new Rect(.47f, .47f, .25f, .43f);
        private static readonly Rect PanelBodyCrop = new Rect(.225f, .10f, .775f, .80f);
        private static readonly Rect PanelFrameCrop = new Rect(0f, 0f, .31f, 1f);
        private static readonly Color LightPanelTextColor = new Color(.10f, .12f, .14f, 1f);
        private static Sprite circleSprite;

        private readonly VnIntroArt art;
        private readonly GameObject root;
        private readonly RawImage background;
        private readonly RawImage characterPrimary;
        private readonly RectTransform characterPrimaryRect;
        private readonly RawImage portraitPrevious;
        private readonly RawImage portrait;
        private readonly RawImage portraitFrame;
        private readonly RawImage dialoguePanel;
        private readonly Text speakerName;
        private readonly Text dialogueText;
        private readonly Image pauseButtonBackground;
        private Vector2 characterPrimaryBasePosition;
        private Vector3 characterPrimaryBaseScale;
        private bool portraitInitialized;
        private bool expressionTransitionActive;
        private bool presentationPaused;
        private float expressionTransitionElapsed;
        private float presentationElapsed;
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
            root.AddComponent<VnIntroPresentationTicker>().Bind(this);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;

            background = Raw(rootRect, "Background", null, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            characterPrimary = Raw(rootRect, "CharacterPrimary", null,
                new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                new Vector2(-180f, 0f), new Vector2(180f, CharacterBodyHeight));
            characterPrimaryRect = (RectTransform)characterPrimary.transform;
            characterPrimaryBasePosition = characterPrimaryRect.anchoredPosition;
            characterPrimaryBaseScale = characterPrimaryRect.localScale;
            characterPrimary.gameObject.SetActive(false);

            Image clickSurface = Image(rootRect, "StoryClickSurface", new Color(0f, 0f, 0f, 0f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            Button storyButton = clickSurface.gameObject.AddComponent<Button>();
            storyButton.targetGraphic = clickSurface;
            storyButton.transition = Selectable.Transition.None;
            storyButton.onClick.AddListener(() => continueStory?.Invoke());

            // The approved source panel already contains the premium trim. Use its body and circular
            // frame as two independently-scaled slices so the long dialogue box can be wide without
            // flattening the portrait ring.
            dialoguePanel = Raw(rootRect, "DialoguePanel", art.DialoguePanelKeikoDark,
                new Vector2(.045f, 0f), new Vector2(.965f, 0f), new Vector2(0f, 44f), new Vector2(0f, 338f));
            dialoguePanel.uvRect = PanelBodyCrop;
            dialoguePanel.raycastTarget = false;
            RectTransform panelRect = (RectTransform)dialoguePanel.transform;

            RectTransform maskRect = Rect(panelRect, "PortraitMask",
                new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(-18f, -114f), new Vector2(210f, 114f));
            Image maskGraphic = maskRect.gameObject.AddComponent<Image>();
            maskGraphic.sprite = GetCircleSprite();
            maskGraphic.color = Color.white;
            maskGraphic.raycastTarget = false;
            Mask mask = maskRect.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            portraitPrevious = Raw(maskRect, "PortraitPrevious", null,
                Vector2.zero, Vector2.one, new Vector2(-20f, -20f), new Vector2(20f, 20f));
            portraitPrevious.raycastTarget = false;
            SetAlpha(portraitPrevious, 0f);

            portrait = Raw(maskRect, "Portrait", null,
                Vector2.zero, Vector2.one, new Vector2(-20f, -20f), new Vector2(20f, 20f));
            portrait.raycastTarget = false;

            portraitFrame = Raw(panelRect, "PortraitFrame", art.DialoguePanelKeikoDark,
                new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(-70f, -158f), new Vector2(246f, 158f));
            portraitFrame.uvRect = PanelFrameCrop;
            portraitFrame.raycastTarget = false;

            speakerName = Label(panelRect, "SpeakerName", font, string.Empty,
                new Vector2(.155f, 1f), new Vector2(.56f, 1f), new Vector2(0f, -80f), new Vector2(0f, -24f),
                31, FontStyle.Bold, TextAnchor.MiddleLeft);
            dialogueText = Label(panelRect, "DialogueText", font, string.Empty,
                new Vector2(.155f, 0f), new Vector2(.70f, 1f), new Vector2(0f, 30f), new Vector2(0f, -94f),
                28, FontStyle.Normal, TextAnchor.UpperLeft);

            RectTransform controls = Rect(panelRect, "ControlsRow",
                new Vector2(1f, .5f), new Vector2(1f, .5f), new Vector2(-475f, -48f), new Vector2(-20f, 48f));
            CreateIconButton(controls, "MuteButton", art.IconMute, 0f, toggleMute, out _);
            CreateIconButton(controls, "PauseButton", art.IconPause, 91f, togglePause, out pauseButtonBackground);
            CreateIconButton(controls, "SkipButton", art.IconSkip, 182f, skip, out _);
            CreateArrowButton(controls, "BackButton", 273f, "‹", false, null);
            CreateArrowButton(controls, "NextButton", 364f, "›", true, continueStory);
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
                portraitFrame.texture = art.DialoguePanelKeikoDark;
                speakerName.color = Color.white;
                dialogueText.color = Color.white;
            }
            else if (state.PanelStyle == "light")
            {
                dialoguePanel.texture = art.DialoguePanelMinaLight;
                portraitFrame.texture = art.DialoguePanelMinaLight;
                speakerName.color = LightPanelTextColor;
                dialogueText.color = LightPanelTextColor;
            }
            else
            {
                throw new ArgumentException("Unknown VN intro panel style: " + state.PanelStyle, nameof(state));
            }

            VnCharacterVisualState visualState = VnCharacterVisualCatalog.ResolveOrNeutral(state.PortraitId, state.Speaker);
            ApplyCharacterBodyVisual(visualState);
            ApplyPortraitVisual(visualState);
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
            presentationPaused = paused;
            pauseButtonBackground.color = paused
                ? new Color(.72f, .50f, .24f, .88f)
                : new Color(.035f, .055f, .06f, .06f);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (root) UnityEngine.Object.Destroy(root);
        }

        internal void TickPresentation(float unscaledDeltaTime)
        {
            if (disposed || presentationPaused) return;

            float delta = Mathf.Max(0f, unscaledDeltaTime);
            presentationElapsed += delta;
            UpdateCharacterIdle();

            if (!expressionTransitionActive) return;

            expressionTransitionElapsed += delta;
            float progress = ExpressionCrossfadeDuration <= 0f
                ? 1f
                : Mathf.Clamp01(expressionTransitionElapsed / ExpressionCrossfadeDuration);
            SetAlpha(portrait, progress);
            SetAlpha(portraitPrevious, 1f - progress);

            if (progress < 1f) return;
            expressionTransitionActive = false;
            SetAlpha(portrait, 1f);
            SetAlpha(portraitPrevious, 0f);
        }

        private void ApplyCharacterBodyVisual(VnCharacterVisualState visualState)
        {
            Texture2D targetTexture = visualState.Character.Equals("Mina", StringComparison.OrdinalIgnoreCase)
                ? art.MinaCharacterSheet
                : art.KeikoCharacterSheet;

            characterPrimary.texture = targetTexture;
            characterPrimary.uvRect = visualState.BodyUv;

            float sourceWidth = visualState.BodyUv.width * targetTexture.width;
            float sourceHeight = visualState.BodyUv.height * targetTexture.height;
            float aspect = sourceHeight > 0f ? sourceWidth / sourceHeight : .4f;
            characterPrimaryRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, CharacterBodyHeight * aspect);
            characterPrimaryRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, CharacterBodyHeight);
            characterPrimaryBasePosition = new Vector2(0f, CharacterBodyHeight * .5f);
            characterPrimaryBaseScale = Vector3.one;
            characterPrimaryRect.anchoredPosition = characterPrimaryBasePosition;
            characterPrimaryRect.localScale = characterPrimaryBaseScale;
            characterPrimary.gameObject.SetActive(true);
        }

        private void UpdateCharacterIdle()
        {
            if (!characterPrimary.gameObject.activeSelf) return;

            float wave = Mathf.Sin(presentationElapsed * Mathf.PI * 2f / IdlePeriodSeconds);
            characterPrimaryRect.anchoredPosition = characterPrimaryBasePosition + Vector2.up * (wave * IdleBobPixels);
            characterPrimaryRect.localScale = characterPrimaryBaseScale * (1f + wave * IdleScaleAmount);
        }

        private void ApplyPortraitVisual(VnCharacterVisualState visualState)
        {
            Texture targetTexture = visualState.Character.Equals("Mina", StringComparison.OrdinalIgnoreCase)
                ? art.MinaCharacterSheet
                : art.KeikoCharacterSheet;
            Rect targetUv = visualState.PortraitUv;

            if (!portraitInitialized)
            {
                portrait.texture = targetTexture;
                portrait.uvRect = targetUv;
                SetAlpha(portrait, 1f);
                SetAlpha(portraitPrevious, 0f);
                portraitInitialized = true;
                expressionTransitionActive = false;
                return;
            }

            if (portrait.texture == targetTexture && portrait.uvRect == targetUv)
            {
                SetAlpha(portrait, 1f);
                SetAlpha(portraitPrevious, 0f);
                expressionTransitionActive = false;
                return;
            }

            portraitPrevious.texture = portrait.texture;
            portraitPrevious.uvRect = portrait.uvRect;
            SetAlpha(portraitPrevious, 1f);

            portrait.texture = targetTexture;
            portrait.uvRect = targetUv;
            SetAlpha(portrait, 0f);
            expressionTransitionElapsed = 0f;
            expressionTransitionActive = true;
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            Color color = graphic.color;
            color.a = Mathf.Clamp01(alpha);
            graphic.color = color;
        }

        private static void CreateIconButton(RectTransform parent, string name, Texture2D icon,
            float left, Action action, out Image backgroundImage)
        {
            RectTransform rect = Rect(parent, name,
                new Vector2(0f, .5f), new Vector2(0f, .5f),
                new Vector2(left, -39f), new Vector2(left + 78f, 39f));
            backgroundImage = rect.gameObject.AddComponent<Image>();
            backgroundImage.color = new Color(.035f, .055f, .06f, .06f);
            backgroundImage.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = backgroundImage;
            button.transition = Selectable.Transition.ColorTint;
            button.onClick.AddListener(() => action?.Invoke());

            RawImage iconImage = Raw(rect, "Icon", icon,
                new Vector2(.02f, .02f), new Vector2(.98f, .98f), Vector2.zero, Vector2.zero);
            iconImage.raycastTarget = false;
        }

        private static void CreateArrowButton(RectTransform parent, string name, float left,
            string glyph, bool interactable, Action action)
        {
            RectTransform rect = Rect(parent, name,
                new Vector2(0f, .5f), new Vector2(0f, .5f),
                new Vector2(left, -39f), new Vector2(left + 78f, 39f));
            Image backgroundImage = rect.gameObject.AddComponent<Image>();
            backgroundImage.sprite = GetCircleSprite();
            backgroundImage.color = interactable
                ? new Color(.10f, .12f, .14f, .90f)
                : new Color(.10f, .12f, .14f, .34f);
            backgroundImage.raycastTarget = interactable;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = backgroundImage;
            button.transition = Selectable.Transition.ColorTint;
            button.interactable = interactable;
            if (interactable) button.onClick.AddListener(() => action?.Invoke());

            RectTransform glyphRect = Rect(rect, "Glyph", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Text arrow = glyphRect.gameObject.AddComponent<Text>();
            arrow.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            arrow.text = glyph;
            arrow.fontSize = 54;
            arrow.alignment = TextAnchor.MiddleCenter;
            arrow.color = interactable ? new Color(1f, .92f, .80f, 1f) : new Color(1f, .92f, .80f, .42f);
            arrow.raycastTarget = false;
        }

        private static Sprite GetCircleSprite()
        {
            if (circleSprite) return circleSprite;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "VnRuntimeCircleMask",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            float center = (size - 1) * .5f;
            float radius = center - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = Mathf.Clamp01(radius + 1f - distance);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f);
            circleSprite.name = "VnRuntimeCircleMaskSprite";
            circleSprite.hideFlags = HideFlags.HideAndDontSave;
            return circleSprite;
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