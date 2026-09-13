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
        private const float FocusTransitionDuration = .18f;
        private const float ActiveFocusScale = 1.05f;
        private const float InactiveFocusScale = .94f;
        private const float InactiveBrightness = .76f;
        private const float InactiveAlpha = .84f;
        private const float CharacterBodyHeight = 980f;
        private const float MinaCharacterBodyHeight = 1240f;
        private const float TwoCharacterOffset = 310f;
        private const float FullPanelAspect = 2048f / 682f;
        private static readonly Color LightPanelTextColor = new Color(.10f, .12f, .14f, 1f);
        private static readonly Color TransparentHitTarget = new Color(1f, 1f, 1f, 0f);

        private sealed class CharacterStageSlot
        {
            public readonly RawImage Image;
            public readonly RectTransform Rect;
            public string Character = string.Empty;
            public Vector2 BasePosition;
            public float FocusScale = 1f;
            public float FocusScaleStart = 1f;
            public float FocusScaleTarget = 1f;
            public float Brightness = 1f;
            public float BrightnessStart = 1f;
            public float BrightnessTarget = 1f;
            public float Alpha = 1f;
            public float AlphaStart = 1f;
            public float AlphaTarget = 1f;

            public CharacterStageSlot(RawImage image)
            {
                Image = image;
                Rect = (RectTransform)image.transform;
            }
        }

        private readonly VnIntroArt art;
        private readonly GameObject root;
        private readonly RawImage background;
        private readonly CharacterStageSlot characterPrimary;
        private readonly CharacterStageSlot characterSecondary;
        private readonly RawImage dialoguePanel;
        private readonly Text speakerName;
        private readonly Text dialogueText;
        private readonly Image pauseButtonBackground;
        private bool focusTransitionActive;
        private bool presentationPaused;
        private float focusTransitionElapsed;
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

            characterPrimary = new CharacterStageSlot(Raw(rootRect, "CharacterPrimary", null,
                new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                new Vector2(-180f, 0f), new Vector2(180f, CharacterBodyHeight)));
            characterSecondary = new CharacterStageSlot(Raw(rootRect, "CharacterSecondary", null,
                new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                new Vector2(-180f, 0f), new Vector2(180f, CharacterBodyHeight)));
            characterPrimary.Image.gameObject.SetActive(false);
            characterSecondary.Image.gameObject.SetActive(false);

            Image clickSurface = Image(rootRect, "StoryClickSurface", new Color(0f, 0f, 0f, 0f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            Button storyButton = clickSurface.gameObject.AddComponent<Button>();
            storyButton.targetGraphic = clickSurface;
            storyButton.transition = Selectable.Transition.None;
            storyButton.onClick.AddListener(() => continueStory?.Invoke());

            // The latest authored 2048x682 panels are complete compositions. Render the whole asset;
            // no circular portrait, portrait frame, or panel crop is reconstructed in Unity.
            dialoguePanel = Raw(rootRect, "DialoguePanel", art.DialoguePanelKeikoDark,
                new Vector2(.04f, 0f), new Vector2(.96f, 0f), Vector2.zero, new Vector2(0f, 100f));
            dialoguePanel.uvRect = new Rect(0f, 0f, 1f, 1f);
            dialoguePanel.raycastTarget = false;
            RectTransform panelRect = (RectTransform)dialoguePanel.transform;
            panelRect.pivot = new Vector2(.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, -98f);
            AspectRatioFitter panelAspect = dialoguePanel.gameObject.AddComponent<AspectRatioFitter>();
            panelAspect.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
            panelAspect.aspectRatio = FullPanelAspect;

            speakerName = Label(panelRect, "SpeakerName", font, string.Empty,
                new Vector2(.17f, .69f), new Vector2(.42f, .81f), new Vector2(12f, 0f), new Vector2(-8f, 0f),
                31, FontStyle.Bold, TextAnchor.MiddleLeft);
            dialogueText = Label(panelRect, "DialogueText", font, string.Empty,
                new Vector2(.075f, .25f), new Vector2(.86f, .66f), new Vector2(18f, 8f), new Vector2(-18f, -8f),
                28, FontStyle.Normal, TextAnchor.UpperLeft);

            RectTransform controls = Rect(panelRect, "ControlsRow",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CreateTransparentButton(controls, "MuteButton", new Vector2(.962f, .585f), new Vector2(76f, 76f),
                true, toggleMute, null, font, out _);
            CreateTransparentButton(controls, "PauseButton", new Vector2(.962f, .440f), new Vector2(76f, 76f),
                true, togglePause, null, font, out pauseButtonBackground);
            CreateTransparentButton(controls, "SkipButton", new Vector2(.962f, .297f), new Vector2(76f, 76f),
                true, skip, null, font, out _);
            CreateTransparentButton(controls, "BackButton", new Vector2(.900f, .440f), new Vector2(64f, 72f),
                false, null, "‹", font, out _);
            CreateTransparentButton(controls, "NextButton", new Vector2(.900f, .297f), new Vector2(64f, 72f),
                true, continueStory, "›", font, out _);
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

            VnCharacterVisualState visualState = VnCharacterVisualCatalog.ResolveOrNeutral(state.PortraitId, state.Speaker);
            bool protagonist = string.Equals(visualState.Character, "Keiko", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(state.Speaker, "Keiko", StringComparison.OrdinalIgnoreCase);

            dialoguePanel.texture = protagonist ? art.DialoguePanelKeikoDark : art.DialoguePanelMinaLight;
            speakerName.color = protagonist ? Color.white : LightPanelTextColor;
            dialogueText.color = protagonist ? Color.white : LightPanelTextColor;
            speakerName.text = state.Speaker ?? string.Empty;

            // Protagonist narration is text-only. Non-protagonists remain separately staged on screen.
            SetCharacterStage(protagonist ? (VnCharacterVisualState?)null : visualState, null);
            SetActiveSpeakerFocus(state.Speaker);
        }

        public void PresentLine(string speaker, string text)
        {
            ThrowIfDisposed();
            speakerName.text = speaker ?? string.Empty;
            dialogueText.text = text ?? string.Empty;
            SetActiveSpeakerFocus(speaker);
        }

        public void SetCharacterStage(VnCharacterVisualState? primary, VnCharacterVisualState? secondary)
        {
            ThrowIfDisposed();

            bool hasPrimary = primary.HasValue;
            bool hasSecondary = secondary.HasValue;
            float primaryX = hasPrimary && hasSecondary ? -TwoCharacterOffset : 0f;
            float secondaryX = hasPrimary && hasSecondary ? TwoCharacterOffset : 0f;

            ConfigureStageSlot(characterPrimary, primary, primaryX);
            ConfigureStageSlot(characterSecondary, secondary, secondaryX);

            if (!hasPrimary && !hasSecondary)
            {
                focusTransitionActive = false;
                return;
            }

            SetActiveSpeakerFocus(speakerName.text);
        }

        public void SetPausedVisual(bool paused)
        {
            ThrowIfDisposed();
            presentationPaused = paused;
            // The authored panel already owns the button artwork. Keep the hit surface invisible in all states.
            pauseButtonBackground.color = TransparentHitTarget;
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
            UpdateFocusTransition(delta);
            UpdateCharacterPresentation(characterPrimary);
            UpdateCharacterPresentation(characterSecondary);
        }

        private void ConfigureStageSlot(CharacterStageSlot slot, VnCharacterVisualState? state, float x)
        {
            if (!state.HasValue)
            {
                slot.Character = string.Empty;
                slot.Image.gameObject.SetActive(false);
                return;
            }

            VnCharacterVisualState visualState = state.Value;
            bool mina = visualState.Character.Equals("Mina", StringComparison.OrdinalIgnoreCase);
            Texture2D targetTexture = mina ? art.MinaCharacterSheet : art.KeikoCharacterSheet;
            float bodyHeight = mina ? MinaCharacterBodyHeight : CharacterBodyHeight;

            slot.Character = visualState.Character;
            slot.Image.texture = targetTexture;
            slot.Image.uvRect = visualState.BodyUv;

            float sourceWidth = visualState.BodyUv.width * targetTexture.width;
            float sourceHeight = visualState.BodyUv.height * targetTexture.height;
            float aspect = sourceHeight > 0f ? sourceWidth / sourceHeight : .4f;
            slot.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bodyHeight * aspect);
            slot.Rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bodyHeight);
            slot.BasePosition = new Vector2(x, mina ? bodyHeight * .37f : bodyHeight * .5f);

            if (!slot.Image.gameObject.activeSelf)
            {
                slot.FocusScale = 1f;
                slot.Brightness = 1f;
                slot.Alpha = 1f;
            }

            slot.Image.gameObject.SetActive(true);
            ApplySlotColor(slot);
            UpdateCharacterPresentation(slot);
        }

        private void SetActiveSpeakerFocus(string speaker)
        {
            bool primaryVisible = characterPrimary.Image.gameObject.activeSelf;
            bool secondaryVisible = characterSecondary.Image.gameObject.activeSelf;
            if (!primaryVisible && !secondaryVisible)
            {
                focusTransitionActive = false;
                return;
            }

            // Zero/one visible body is intentionally stable: no active-speaker pulse or scale pumping.
            if (!(primaryVisible && secondaryVisible))
            {
                BeginNeutralTarget(characterPrimary);
                BeginNeutralTarget(characterSecondary);
                focusTransitionElapsed = 0f;
                focusTransitionActive = true;
                return;
            }

            bool primaryActive = string.Equals(speaker, characterPrimary.Character, StringComparison.OrdinalIgnoreCase);
            bool secondaryActive = string.Equals(speaker, characterSecondary.Character, StringComparison.OrdinalIgnoreCase);
            if (!primaryActive && !secondaryActive)
            {
                primaryActive = true;
            }

            BeginFocusTarget(characterPrimary, primaryActive);
            BeginFocusTarget(characterSecondary, secondaryActive);
            focusTransitionElapsed = 0f;
            focusTransitionActive = true;

            if (primaryActive) BringStageSlotForward(characterPrimary, characterSecondary);
            else if (secondaryActive) BringStageSlotForward(characterSecondary, characterPrimary);
        }

        private static void BeginNeutralTarget(CharacterStageSlot slot)
        {
            slot.FocusScaleStart = slot.FocusScale;
            slot.BrightnessStart = slot.Brightness;
            slot.AlphaStart = slot.Alpha;
            slot.FocusScaleTarget = 1f;
            slot.BrightnessTarget = 1f;
            slot.AlphaTarget = 1f;
        }

        private static void BeginFocusTarget(CharacterStageSlot slot, bool active)
        {
            slot.FocusScaleStart = slot.FocusScale;
            slot.BrightnessStart = slot.Brightness;
            slot.AlphaStart = slot.Alpha;
            slot.FocusScaleTarget = active ? ActiveFocusScale : InactiveFocusScale;
            slot.BrightnessTarget = active ? 1f : InactiveBrightness;
            slot.AlphaTarget = active ? 1f : InactiveAlpha;
        }

        private void UpdateFocusTransition(float delta)
        {
            if (!focusTransitionActive) return;

            focusTransitionElapsed += delta;
            float progress = FocusTransitionDuration <= 0f
                ? 1f
                : Mathf.Clamp01(focusTransitionElapsed / FocusTransitionDuration);
            float eased = progress * progress * (3f - 2f * progress);
            ApplyFocusProgress(characterPrimary, eased);
            ApplyFocusProgress(characterSecondary, eased);

            if (progress < 1f) return;
            focusTransitionActive = false;
        }

        private static void ApplyFocusProgress(CharacterStageSlot slot, float progress)
        {
            if (!slot.Image.gameObject.activeSelf) return;
            slot.FocusScale = Mathf.Lerp(slot.FocusScaleStart, slot.FocusScaleTarget, progress);
            slot.Brightness = Mathf.Lerp(slot.BrightnessStart, slot.BrightnessTarget, progress);
            slot.Alpha = Mathf.Lerp(slot.AlphaStart, slot.AlphaTarget, progress);
            ApplySlotColor(slot);
        }

        private static void ApplySlotColor(CharacterStageSlot slot)
        {
            slot.Image.color = new Color(slot.Brightness, slot.Brightness, slot.Brightness, slot.Alpha);
        }

        private static void UpdateCharacterPresentation(CharacterStageSlot slot)
        {
            if (!slot.Image.gameObject.activeSelf) return;
            slot.Rect.anchoredPosition = slot.BasePosition;
            slot.Rect.localScale = new Vector3(slot.FocusScale, slot.FocusScale, 1f);
        }

        private static void BringStageSlotForward(CharacterStageSlot active, CharacterStageSlot inactive)
        {
            int activeIndex = active.Rect.GetSiblingIndex();
            int inactiveIndex = inactive.Rect.GetSiblingIndex();
            int frontIndex = Mathf.Max(activeIndex, inactiveIndex);
            int backIndex = Mathf.Min(activeIndex, inactiveIndex);
            inactive.Rect.SetSiblingIndex(backIndex);
            active.Rect.SetSiblingIndex(frontIndex);
        }

        private static void CreateTransparentButton(RectTransform parent, string name, Vector2 anchor,
            Vector2 size, bool interactable, Action action, string glyph, Font font, out Image targetImage)
        {
            RectTransform rect = Rect(parent, name, anchor, anchor, -size * .5f, size * .5f);
            targetImage = rect.gameObject.AddComponent<Image>();
            targetImage.color = TransparentHitTarget;
            targetImage.raycastTarget = interactable;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = targetImage;
            button.transition = Selectable.Transition.None;
            button.interactable = interactable;
            if (interactable) button.onClick.AddListener(() => action?.Invoke());

            if (string.IsNullOrEmpty(glyph)) return;
            RectTransform glyphRect = Rect(rect, "Glyph", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Text arrow = glyphRect.gameObject.AddComponent<Text>();
            arrow.font = font;
            arrow.text = glyph;
            arrow.fontSize = 48;
            arrow.fontStyle = FontStyle.Bold;
            arrow.alignment = TextAnchor.MiddleCenter;
            arrow.color = interactable ? new Color(1f, .92f, .80f, 1f) : new Color(1f, .92f, .80f, .42f);
            arrow.raycastTarget = false;
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
