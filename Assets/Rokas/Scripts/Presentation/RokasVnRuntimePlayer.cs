using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    internal sealed class RokasVnRuntimePlayerTicker : MonoBehaviour
    {
        private RokasVnRuntimePlayer owner;

        public void Bind(RokasVnRuntimePlayer player)
        {
            owner = player;
        }

        private void Update()
        {
            if (owner != null)
                owner.Tick(Time.unscaledDeltaTime);
        }
    }

    public sealed class RokasVnRuntimePlayer : IDisposable
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private const float CharacterBodyHeight = 1240f;
        private const float CharacterBaseCenterY = CharacterBodyHeight * .37f;

        private sealed class CharacterView
        {
            public string CharacterId;
            public RectTransform Rect;
            public RawImage Image;
        }

        private readonly RokasVnRuntimeIntroPackage package;
        private readonly RokasVnRuntimePlaybackState playback;
        private readonly Action onCompleted;
        private readonly Dictionary<string, CharacterView> characterViews =
            new Dictionary<string, CharacterView>(StringComparer.OrdinalIgnoreCase);

        private readonly GameObject root;
        private readonly RawImage background;
        private readonly RectTransform characterLayer;
        private readonly RawImage dialoguePlaque;
        private readonly Text speakerText;
        private readonly Text dialogueText;
        private readonly Button forwardButton;
        private readonly Button muteButton;
        private readonly Button menuButton;
        private readonly RawImage completionTriangle;
        private readonly CanvasGroup terminalFade;
        private readonly GameObject menuOverlay;

        private bool disposed;
        private bool completionDelivered;
        private bool muted;
        private bool menuOpen;
        private float uiElapsedSeconds;
        private int renderedSceneIndex = -1;

        public bool IsPlaying =>
            !disposed && !playback.IsSequenceCompleted;
        public bool IsMenuOpen => menuOpen;
        public bool IsMuted => muted;
        public RokasVnRuntimePlaybackState Playback => playback;

        private RokasVnRuntimePlayer(
            Transform parent,
            RokasVnRuntimeIntroPackage runtimePackage,
            Action completed)
        {
            if (!parent) throw new ArgumentNullException(nameof(parent));
            package = runtimePackage
                ? runtimePackage
                : throw new ArgumentNullException(nameof(runtimePackage));

            string packageError;
            if (!package.TryValidate(out packageError))
                throw new InvalidOperationException(
                    "Runtime VN package is invalid: " + packageError);

            onCompleted = completed;
            playback = new RokasVnRuntimePlaybackState(package.Snapshot);
            playback.VnSequenceCompleted += HandleSequenceCompleted;

            root = new GameObject(
                "RokasVnRuntimeRoot",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            Stretch((RectTransform)root.transform);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1200;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution =
                new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;

            RectTransform rootRect = (RectTransform)root.transform;
            background = CreateRaw(
                rootRect, "VnBackground", null,
                Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);
            background.raycastTarget = false;

            characterLayer = CreateRect(
                rootRect, "VnCharacters",
                Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);

            Image clickSurface = CreateImage(
                rootRect, "VnStoryClickSurface",
                new Color(0f, 0f, 0f, 0f),
                Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);
            clickSurface.raycastTarget = true;
            Button storyButton =
                clickSurface.gameObject.AddComponent<Button>();
            storyButton.transition = Selectable.Transition.None;
            storyButton.targetGraphic = clickSurface;
            storyButton.onClick.AddListener(() => RequestAdvance());

            dialoguePlaque = CreateRaw(
                rootRect, "VnDialoguePlaque",
                package.DialoguePlaque,
                new Vector2(.5f, 0f),
                new Vector2(.5f, 0f),
                Vector2.zero,
                Vector2.zero);
            RectTransform plaqueRect =
                (RectTransform)dialoguePlaque.transform;
            plaqueRect.pivot = new Vector2(.5f, 0f);
            float plaqueWidth = ReferenceWidth * .92f;
            float plaqueAspect =
                package.DialoguePlaque != null &&
                package.DialoguePlaque.width > 0
                    ? (float)package.DialoguePlaque.height /
                      package.DialoguePlaque.width
                    : 682f / 2048f;
            plaqueRect.sizeDelta =
                new Vector2(plaqueWidth, plaqueWidth * plaqueAspect);
            plaqueRect.anchoredPosition = new Vector2(0f, -98f);
            dialoguePlaque.raycastTarget = false;

            Font fallbackFont = ResolveFallbackFont();
            speakerText = CreateText(
                plaqueRect, "VnSpeaker", fallbackFont,
                new Vector2(.12f, .48f),
                new Vector2(.42f, .84f),
                string.Empty,
                26,
                FontStyle.Bold);
            dialogueText = CreateText(
                plaqueRect, "VnDialogue", fallbackFont,
                new Vector2(.10f, .14f),
                new Vector2(.90f, .54f),
                string.Empty,
                22,
                FontStyle.Normal);

            muteButton = CreateControlButton(
                plaqueRect, "VnMuteButton", 0, .84f,
                () => SetMuted(!muted));
            forwardButton = CreateControlButton(
                plaqueRect, "VnForwardButton", 1, .90f,
                () => RequestAdvance());
            menuButton = CreateControlButton(
                plaqueRect, "VnMenuButton", 2, .96f,
                ToggleMenu);

            completionTriangle = CreateRaw(
                plaqueRect, "VnCompletionTriangle",
                package.CompletionTriangle,
                new Vector2(.913f, .27f),
                new Vector2(.913f, .27f),
                new Vector2(-24f, -28f),
                new Vector2(24f, 28f));
            completionTriangle.raycastTarget = false;

            menuOverlay = BuildMenuOverlay(rootRect, fallbackFont);

            Image terminalImage = CreateImage(
                rootRect, "VnTerminalFadeImage",
                Color.black,
                Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);
            terminalImage.raycastTarget = true;
            terminalFade =
                terminalImage.gameObject.AddComponent<CanvasGroup>();
            terminalFade.alpha = 0f;
            terminalFade.blocksRaycasts = false;
            terminalFade.interactable = false;
            terminalImage.gameObject.name = "VnTerminalFade";

            root.AddComponent<RokasVnRuntimePlayerTicker>().Bind(this);

            playback.StartFromBeginning();
            RefreshPresentation(true);
        }

        public static RokasVnRuntimePlayer Create(
            Transform parent,
            RokasVnRuntimeIntroPackage runtimePackage,
            Action onCompleted)
        {
            return new RokasVnRuntimePlayer(
                parent, runtimePackage, onCompleted);
        }

        public bool RequestAdvance()
        {
            ThrowIfDisposed();
            if (menuOpen || playback.IsTerminalFadeActive ||
                playback.IsSequenceCompleted)
                return false;

            bool advanced = playback.RequestAdvance();
            RefreshPresentation(false);
            return advanced;
        }

        public void SetMuted(bool value)
        {
            ThrowIfDisposed();
            muted = value;
            RefreshControlVisuals();
        }

        public void TickForTests(float unscaledDeltaTime)
        {
            Tick(unscaledDeltaTime);
        }

        internal void Tick(float unscaledDeltaTime)
        {
            if (disposed) return;
            if (unscaledDeltaTime < 0f ||
                float.IsNaN(unscaledDeltaTime) ||
                float.IsInfinity(unscaledDeltaTime))
                throw new ArgumentOutOfRangeException(
                    nameof(unscaledDeltaTime));

            uiElapsedSeconds += unscaledDeltaTime;
            if (!menuOpen)
                playback.AdvanceTime(unscaledDeltaTime);
            RefreshPresentation(false);
        }

        private void RefreshPresentation(bool forceScene)
        {
            if (disposed) return;
            RokasVnRuntimeSceneSnapshot scene = playback.CurrentScene;
            if (scene == null) return;

            if (forceScene ||
                renderedSceneIndex != playback.CurrentSceneIndex)
            {
                renderedSceneIndex = playback.CurrentSceneIndex;
                RefreshSceneMedia(scene);
                RebuildCharacterViews(scene);
            }

            RefreshCharacters(scene);
            RefreshDialogue(scene);
            RefreshControlVisuals();

            terminalFade.alpha = playback.TerminalFadeAlpha;
            terminalFade.blocksRaycasts =
                playback.IsTerminalFadeActive &&
                !playback.IsSequenceCompleted;
            terminalFade.interactable =
                terminalFade.blocksRaycasts;
        }

        private void RefreshSceneMedia(
            RokasVnRuntimeSceneSnapshot scene)
        {
            Texture texture = null;
            if (scene.media != null)
            {
                string key = !string.IsNullOrWhiteSpace(
                    scene.media.runtimeAssetKey)
                    ? scene.media.runtimeAssetKey
                    : scene.media.reference;
                RokasVnRuntimeAssetBinding binding;
                if (package.TryGetAsset(key, out binding) &&
                    binding != null)
                    texture = ResolveTexture(binding.asset);
            }
            background.texture = texture;
            background.uvRect = new Rect(0f, 0f, 1f, 1f);
        }

        private void RebuildCharacterViews(
            RokasVnRuntimeSceneSnapshot scene)
        {
            foreach (CharacterView view in characterViews.Values)
            {
                if (view != null && view.Image != null)
                    UnityEngine.Object.Destroy(view.Image.gameObject);
            }
            characterViews.Clear();

            if (scene.characters == null) return;
            for (int i = 0; i < scene.characters.Count; i++)
            {
                RokasVnRuntimeCharacterSnapshot character =
                    scene.characters[i];
                if (character == null ||
                    string.IsNullOrWhiteSpace(character.characterId))
                    continue;

                RawImage image = CreateRaw(
                    characterLayer,
                    "VnCharacter_" + character.characterId,
                    null,
                    Vector2.zero,
                    Vector2.zero,
                    Vector2.zero,
                    Vector2.zero);
                RectTransform rect = (RectTransform)image.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(.5f, .5f);
                image.raycastTarget = false;

                characterViews[character.characterId] =
                    new CharacterView
                    {
                        CharacterId = character.characterId,
                        Rect = rect,
                        Image = image
                    };
            }
        }

        private void RefreshCharacters(
            RokasVnRuntimeSceneSnapshot scene)
        {
            if (scene.characters == null) return;
            for (int i = 0; i < scene.characters.Count; i++)
            {
                RokasVnRuntimeCharacterSnapshot authored =
                    scene.characters[i];
                if (authored == null ||
                    string.IsNullOrWhiteSpace(authored.characterId))
                    continue;

                CharacterView view;
                if (!characterViews.TryGetValue(
                        authored.characterId, out view))
                    continue;

                RokasVnRuntimeCharacterSample sample =
                    playback.SampleCharacter(authored.characterId);
                RokasVnRuntimeCharacterStateBinding state;
                bool hasState = package.TryGetCharacterState(
                    sample.stateId, out state) &&
                    state != null &&
                    state.texture != null;

                view.Image.gameObject.SetActive(
                    sample.visible && sample.alpha > .0001f &&
                    hasState);
                if (!hasState) continue;

                view.Image.texture = state.texture;
                view.Image.uvRect = state.bodyUv;
                float sourceWidth =
                    state.texture.width *
                    Mathf.Max(.0001f, state.bodyUv.width);
                float sourceHeight =
                    state.texture.height *
                    Mathf.Max(.0001f, state.bodyUv.height);
                float aspect =
                    sourceHeight > .0001f
                        ? sourceWidth / sourceHeight
                        : .4f;
                view.Rect.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal,
                    CharacterBodyHeight * aspect);
                view.Rect.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    CharacterBodyHeight);
                view.Rect.anchoredPosition =
                    new Vector2(
                        ReferenceWidth * .5f + sample.position.x,
                        CharacterBaseCenterY + sample.position.y);
                view.Rect.localScale =
                    new Vector3(
                        sample.scale, sample.scale, 1f);
                view.Image.color =
                    new Color(
                        sample.brightness,
                        sample.brightness,
                        sample.brightness,
                        sample.alpha);
            }

            RokasVnRuntimeReplicaEffectSample effect =
                playback.SampleReplicaEffect();
            if (!effect.active) return;

            string target = playback.CurrentBeat != null
                ? playback.CurrentBeat.targetCharacterId
                : string.Empty;
            if (string.IsNullOrWhiteSpace(target) &&
                playback.CurrentBeat != null)
                target = playback.CurrentBeat.speaker;
            CharacterView effectView;
            if (!string.IsNullOrWhiteSpace(target) &&
                characterViews.TryGetValue(target, out effectView) &&
                effectView.Image.gameObject.activeSelf)
            {
                effectView.Rect.anchoredPosition += effect.offset;
                effectView.Rect.localScale *= effect.scale;
                if (effect.flash.a > .0001f)
                    effectView.Image.color = Color.Lerp(
                        effectView.Image.color,
                        effect.flash,
                        effect.flash.a);
            }
        }

        private void RefreshDialogue(
            RokasVnRuntimeSceneSnapshot scene)
        {
            RokasVnRuntimeBeatSnapshot beat = playback.CurrentBeat;
            RokasVnRuntimePresentationSnapshot presentation =
                scene.presentation ??
                new RokasVnRuntimePresentationSnapshot();

            if (beat == null)
            {
                speakerText.text = string.Empty;
                dialogueText.text = string.Empty;
                completionTriangle.gameObject.SetActive(false);
                return;
            }

            speakerText.text = beat.speaker ?? string.Empty;
            string full = beat.text ?? string.Empty;
            int visible = Mathf.Clamp(
                playback.VisibleDialogueCharacters,
                0,
                full.Length);
            dialogueText.text =
                visible >= full.Length
                    ? full
                    : full.Substring(0, visible);

            speakerText.fontSize = Mathf.Max(
                1, Mathf.RoundToInt(
                    presentation.speakerFontSize));
            dialogueText.fontSize = Mathf.Max(
                1, Mathf.RoundToInt(
                    presentation.dialogueFontSize));
            speakerText.color = presentation.speakerColor;
            dialogueText.color = presentation.dialogueColor;
            speakerText.alignment =
                ToTextAnchor(
                    presentation.speakerAlignment,
                    TextAnchor.MiddleLeft);
            dialogueText.alignment =
                ToTextAnchor(
                    presentation.dialogueAlignment,
                    TextAnchor.UpperLeft);

            bool showTriangle =
                playback.IsDialogueRevealComplete &&
                !playback.IsTerminalFadeActive &&
                !playback.IsSequenceCompleted &&
                !menuOpen;
            completionTriangle.gameObject.SetActive(showTriangle);
            if (showTriangle)
            {
                float wave = Mathf.Sin(
                    uiElapsedSeconds *
                    (Mathf.PI * 2f / .75f));
                RectTransform triangle =
                    (RectTransform)completionTriangle.transform;
                triangle.anchoredPosition =
                    new Vector2(0f, wave * 2.25f);
                float scale = 1f + wave * .025f;
                triangle.localScale =
                    new Vector3(scale, scale, 1f);
                completionTriangle.color =
                    new Color(
                        1f, 1f, 1f,
                        .87f + wave * .10f);
            }
        }

        private void RefreshControlVisuals()
        {
            forwardButton.interactable =
                !menuOpen &&
                !playback.IsTerminalFadeActive &&
                !playback.IsSequenceCompleted;
            muteButton.interactable =
                !playback.IsSequenceCompleted;
            menuButton.interactable =
                !playback.IsSequenceCompleted;

            RawImage muteGraphic =
                muteButton.targetGraphic as RawImage;
            if (muteGraphic != null)
            {
                muteGraphic.texture =
                    muted && package.MutedSpeaker != null
                        ? package.MutedSpeaker
                        : package.ControlSheet;
                muteGraphic.uvRect =
                    muted && package.MutedSpeaker != null
                        ? new Rect(0f, 0f, 1f, 1f)
                        : ControlUv(0);
            }
        }

        private Button CreateControlButton(
            RectTransform plaque,
            string name,
            int controlIndex,
            float normalizedX,
            Action action)
        {
            RectTransform rect = CreateRect(
                plaque,
                name,
                new Vector2(normalizedX, .595f),
                new Vector2(normalizedX, .595f),
                new Vector2(-30f, -30f),
                new Vector2(30f, 30f));
            RawImage graphic =
                rect.gameObject.AddComponent<RawImage>();
            graphic.texture = package.ControlSheet;
            graphic.uvRect = ControlUv(controlIndex);
            graphic.color = Color.white;
            graphic.raycastTarget = true;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = graphic;
            button.transition = Selectable.Transition.ColorTint;
            button.onClick.AddListener(
                () =>
                {
                    if (!disposed && action != null) action();
                });
            return button;
        }

        private GameObject BuildMenuOverlay(
            RectTransform rootRect,
            Font font)
        {
            GameObject overlay = new GameObject(
                "VnRuntimeMenu",
                typeof(RectTransform),
                typeof(CanvasGroup));
            RectTransform rect =
                (RectTransform)overlay.transform;
            rect.SetParent(rootRect, false);
            Stretch(rect);

            Image shade = CreateImage(
                rect, "Shade",
                new Color(.02f, .04f, .07f, .88f),
                Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);
            shade.raycastTarget = true;

            Button resume = CreateTextButton(
                rect,
                "VnResumeButton",
                font,
                "ПРОДОЛЖИТЬ",
                new Vector2(.5f, .5f),
                new Vector2(360f, 84f),
                ToggleMenu);
            resume.targetGraphic.color =
                new Color(.08f, .16f, .22f, .98f);

            overlay.SetActive(false);
            return overlay;
        }

        private void ToggleMenu()
        {
            if (disposed ||
                playback.IsTerminalFadeActive ||
                playback.IsSequenceCompleted)
                return;
            menuOpen = !menuOpen;
            menuOverlay.SetActive(menuOpen);
            RefreshControlVisuals();
        }

        private void HandleSequenceCompleted()
        {
            if (completionDelivered) return;
            completionDelivered = true;
            RefreshPresentation(false);
            if (onCompleted != null) onCompleted();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            playback.VnSequenceCompleted -=
                HandleSequenceCompleted;
            if (root)
                UnityEngine.Object.Destroy(root);
        }

        private Font ResolveFallbackFont()
        {
            RokasAssets assets =
                Resources.Load<RokasAssets>("RokasAssets");
            if (assets != null && assets.sans != null)
                return assets.sans;
            return Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
        }

        private static Texture ResolveTexture(
            UnityEngine.Object asset)
        {
            Texture texture = asset as Texture;
            if (texture != null) return texture;
            Sprite sprite = asset as Sprite;
            return sprite != null
                ? sprite.texture
                : null;
        }

        private static Rect ControlUv(int index)
        {
            return new Rect(
                (63f + 661f * index) / 2048f,
                49f / 682f,
                600f / 2048f,
                600f / 682f);
        }

        private static TextAnchor ToTextAnchor(
            int value,
            TextAnchor fallback)
        {
            switch (value)
            {
                case 0: return fallback;
                case 1: return TextAnchor.MiddleCenter;
                case 2: return TextAnchor.MiddleRight;
                default: return fallback;
            }
        }

        private static RawImage CreateRaw(
            Transform parent,
            string name,
            Texture texture,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            RectTransform rect = CreateRect(
                parent, name,
                anchorMin, anchorMax,
                offsetMin, offsetMax);
            RawImage image =
                rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.color = Color.white;
            return image;
        }

        private static Image CreateImage(
            Transform parent,
            string name,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            RectTransform rect = CreateRect(
                parent, name,
                anchorMin, anchorMax,
                offsetMin, offsetMax);
            Image image =
                rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            Font font,
            Vector2 anchorMin,
            Vector2 anchorMax,
            string value,
            int size,
            FontStyle style)
        {
            RectTransform rect = CreateRect(
                parent, name,
                anchorMin, anchorMax,
                Vector2.zero, Vector2.zero);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value ?? string.Empty;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow =
                HorizontalWrapMode.Wrap;
            text.verticalOverflow =
                VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateTextButton(
            Transform parent,
            string name,
            Font font,
            string label,
            Vector2 anchor,
            Vector2 size,
            Action action)
        {
            RectTransform rect = CreateRect(
                parent, name,
                anchor, anchor,
                -size * .5f, size * .5f);
            Image image =
                rect.gameObject.AddComponent<Image>();
            image.color =
                new Color(.08f, .16f, .22f, .98f);
            Button button =
                rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(
                () =>
                {
                    if (action != null) action();
                });

            Text text = CreateText(
                rect, "Label", font,
                Vector2.zero, Vector2.one,
                label, 26, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static RectTransform CreateRect(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            var go = new GameObject(
                name, typeof(RectTransform));
            RectTransform rect =
                (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        private static void Stretch(
            RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(
                    nameof(RokasVnRuntimePlayer));
        }
    }
}
