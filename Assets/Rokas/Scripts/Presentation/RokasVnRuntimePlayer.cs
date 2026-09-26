using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

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
            if (owner == null) return;

            owner.Tick(Time.unscaledDeltaTime);
            if (Input.GetKeyDown(KeyCode.Escape))
                owner.HandleEscapeInput();
        }
    }

    public struct RokasVnPlaqueUiLayout
    {
        public Rect Mute;
        public Rect Forward;
        public Rect Menu;
        public Rect Triangle;
    }

    public struct RokasVnTriangleUiSample
    {
        public float OffsetY;
        public float Scale;
        public float Alpha;
    }

    public struct RokasVnButtonUiSample
    {
        public Rect Rect;
        public float Brightness;
        public float Alpha;
    }

    public struct RokasVnMenuUiLayout
    {
        public Rect Panel;
        public Rect Title;
        public Rect Resume;
        public Rect Settings;
        public Rect Save;
        public Rect MainMenu;
    }

    public static class RokasVnRuntimeUiSemantics
    {
        public const float ButtonTransitionDuration = .09f;
        public const float ButtonBaseScale = .97f;
        public const float ButtonBaseBrightness = .90f;
        public const float ButtonEnabledAlpha = .90f;
        public const float ButtonDisabledAlpha = .42f;
        public const float TrianglePeriod = .75f;
        public const float TriangleAspect = .90f;

        public static RokasVnPlaqueUiLayout Layout(Rect plaque)
        {
            float diameter = Mathf.Min(
                plaque.width * .038f,
                plaque.height * .10f);
            float y = plaque.y + plaque.height * .595f;
            float gap = diameter * 1.32f;
            float right = plaque.xMax - plaque.width * .06f;
            return new RokasVnPlaqueUiLayout
            {
                Mute = Center(right - (2f * gap), y, diameter),
                Forward = Center(right - gap, y, diameter),
                Menu = Center(right, y, diameter),
                Triangle = new Rect(
                    plaque.x + plaque.width * .913f -
                    diameter * .34f,
                    Mathf.Max(
                        24f,
                        plaque.y + plaque.height * .27f) -
                    diameter * .34f / TriangleAspect,
                    diameter * .68f,
                    diameter * .68f / TriangleAspect)
            };
        }

        public static RokasVnTriangleUiSample SampleTriangle(
            float unscaledSeconds)
        {
            float wave = Mathf.Sin(
                unscaledSeconds *
                (Mathf.PI * 2f / TrianglePeriod));
            return new RokasVnTriangleUiSample
            {
                OffsetY = wave * 2.25f,
                Scale = 1f + wave * .025f,
                Alpha = .87f + wave * .10f
            };
        }

        public static float ButtonScaleTarget(
            bool hover,
            bool pressed,
            bool enabled)
        {
            if (!enabled) return ButtonBaseScale;
            return pressed ? .95f : hover ? 1f : ButtonBaseScale;
        }

        public static float ButtonBrightnessTarget(
            bool hover,
            bool pressed,
            bool enabled)
        {
            if (!enabled) return ButtonBaseBrightness;
            return pressed ? .92f :
                hover ? .97f :
                ButtonBaseBrightness;
        }

        public static float ButtonAlpha(bool enabled)
        {
            return enabled
                ? ButtonEnabledAlpha
                : ButtonDisabledAlpha;
        }

        public static float ButtonEase(float elapsedSeconds)
        {
            return Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(
                    elapsedSeconds /
                    ButtonTransitionDuration));
        }

        public static RokasVnButtonUiSample SampleButton(
            Rect baseline,
            bool hover,
            bool pressed,
            bool enabled,
            float progress)
        {
            float t = Mathf.SmoothStep(
                0f, 1f, Mathf.Clamp01(progress));
            float scale = Mathf.Lerp(
                ButtonBaseScale,
                ButtonScaleTarget(
                    hover, pressed, enabled),
                t);
            Vector2 size = baseline.size * scale;
            return new RokasVnButtonUiSample
            {
                Rect = new Rect(
                    baseline.center - size * .5f,
                    size),
                Brightness = enabled
                    ? Mathf.Lerp(
                        ButtonBaseBrightness,
                        ButtonBrightnessTarget(
                            hover, pressed, true),
                        t)
                    : .55f,
                Alpha = ButtonAlpha(enabled)
            };
        }

        public const string MenuTitle =
            "ROKAS  /  ПАУЗА";

        public static Color MenuShadeColor =>
            new Color(.012f, .024f, .045f, .88f);

        public static Color MenuPanelColor =>
            new Color(.035f, .085f, .13f, .98f);

        public static Color MenuOutlineColor =>
            new Color(.2f, .75f, 1f, 1f);

        public static Color MenuTitleColor =>
            new Color(.62f, .9f, 1f, 1f);

        public static RokasVnMenuUiLayout MenuLayout(
            Rect canvas)
        {
            float width = Mathf.Min(
                canvas.width * .68f, 440f);
            float height = Mathf.Min(
                canvas.height * .84f, 410f);
            Rect panel = new Rect(
                canvas.center.x - width * .5f,
                canvas.center.y - height * .5f,
                width,
                height);
            float row = Mathf.Min(
                48f,
                (height - 90f) / 4f);
            float gap = 8f;
            return new RokasVnMenuUiLayout
            {
                Panel = panel,
                Title = new Rect(
                    panel.x + 24f,
                    panel.y + 18f,
                    panel.width - 48f,
                    32f),
                Resume = MenuRow(
                    panel, row, gap, 0),
                Settings = MenuRow(
                    panel, row, gap, 1),
                Save = MenuRow(
                    panel, row, gap, 2),
                MainMenu = MenuRow(
                    panel, row, gap, 3)
            };
        }

        public static string MenuLabel(int index)
        {
            switch (index)
            {
                case 0: return "Продолжить";
                case 1: return "Настройки  ·  Скоро";
                case 2: return "Сохранение  ·  Скоро";
                case 3: return "Главное меню  ·  Скоро";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(index));
            }
        }

        public static int MenuTitleFontSize(
            float canvasHeight)
        {
            return Mathf.RoundToInt(
                Mathf.Clamp(
                    canvasHeight * .044f,
                    16f,
                    24f));
        }

        public static int MenuRowFontSize(
            float rowHeight)
        {
            return Mathf.RoundToInt(
                Mathf.Clamp(
                    rowHeight * .35f,
                    12f,
                    18f));
        }

        private static Rect MenuRow(
            Rect panel,
            float row,
            float gap,
            int index)
        {
            return new Rect(
                panel.x + 24f,
                panel.y + 64f +
                index * (row + gap),
                panel.width - 48f,
                row);
        }

        private static Rect Center(
            float x,
            float y,
            float size)
        {
            return new Rect(
                x - size * .5f,
                y - size * .5f,
                size,
                size);
        }
    }

    internal sealed class RokasVnRuntimeControlPointerState :
        MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        public bool Hovered { get; private set; }
        public bool Pressed { get; private set; }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Hovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Hovered = false;
            Pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData != null &&
                eventData.button == PointerEventData.InputButton.Left)
                Pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Pressed = false;
        }

        public void ResetState()
        {
            Hovered = false;
            Pressed = false;
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

        private sealed class ControlMotion
        {
            public float ChangedAt;
            public float FromScale =
                RokasVnRuntimeUiSemantics.ButtonBaseScale;
            public float ToScale =
                RokasVnRuntimeUiSemantics.ButtonBaseScale;
            public float FromBrightness =
                RokasVnRuntimeUiSemantics.ButtonBaseBrightness;
            public float ToBrightness =
                RokasVnRuntimeUiSemantics.ButtonBaseBrightness;

            public float Scale(float now)
            {
                return Mathf.Lerp(
                    FromScale,
                    ToScale,
                    RokasVnRuntimeUiSemantics.ButtonEase(
                        now - ChangedAt));
            }

            public float Brightness(float now)
            {
                return Mathf.Lerp(
                    FromBrightness,
                    ToBrightness,
                    RokasVnRuntimeUiSemantics.ButtonEase(
                        now - ChangedAt));
            }

            public void Target(
                bool hover,
                bool pressed,
                bool enabled,
                float now)
            {
                float scale =
                    RokasVnRuntimeUiSemantics.ButtonScaleTarget(
                        hover, pressed, enabled);
                float brightness =
                    RokasVnRuntimeUiSemantics.ButtonBrightnessTarget(
                        hover, pressed, enabled);
                if (Mathf.Approximately(scale, ToScale) &&
                    Mathf.Approximately(
                        brightness, ToBrightness))
                    return;

                FromScale = Scale(now);
                FromBrightness = Brightness(now);
                ToScale = scale;
                ToBrightness = brightness;
                ChangedAt = now;
            }
        }

        private readonly RokasVnRuntimeIntroPackage package;
        private readonly RokasVnRuntimePlaybackState playback;
        private readonly Action onCompleted;
        private readonly RokasVnRuntimeAudioPlayback audioPlayback;
        private readonly Dictionary<string, CharacterView> characterViews =
            new Dictionary<string, CharacterView>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Button, ControlMotion> controlMotions =
            new Dictionary<Button, ControlMotion>();

        private readonly GameObject root;
        private readonly RawImage background;
        private readonly AspectRatioFitter backgroundAspect;
        private readonly VideoPlayer videoPlayer;
        private readonly RectTransform characterLayer;
        private readonly RawImage dialoguePlaque;
        private readonly Text speakerText;
        private readonly Text dialogueText;
        private readonly Button forwardButton;
        private readonly Button muteButton;
        private readonly Button menuButton;
        private readonly RawImage completionTriangle;
        private Vector2 completionTriangleBasePosition;
        private readonly CanvasGroup terminalFade;
        private readonly GameObject menuOverlay;
        private readonly Image sceneTransitionImage;
        private readonly CanvasGroup sceneTransitionOverlay;

        private bool disposed;
        private bool completionDelivered;
        private bool muted;
        private bool menuOpen;
        private float uiElapsedSeconds;
        private int renderedSceneIndex = -1;
        private int renderedBeatIndex = -1;

        private bool sceneTransitionActive;
        private bool sceneTransitionSwapped;
        private int sceneTransitionTargetIndex = -1;
        private int sceneTransitionType;
        private int sceneTransitionDirection;
        private float sceneTransitionElapsed;
        private float sceneTransitionDuration;

        private readonly List<Texture2D> gifFrames = new List<Texture2D>();
        private readonly List<float> gifDelays = new List<float>();
        private int gifFrameIndex;
        private float gifFrameElapsed;
        private bool gifLoop;
        private bool videoActive;
        private int currentMediaScaleMode = 2;

        public bool IsPlaying =>
            !disposed && !playback.IsSequenceCompleted;
        public bool IsMenuOpen => menuOpen;
        public bool IsMuted => muted;
        public bool IsSceneTransitionActive => sceneTransitionActive;
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

            audioPlayback =
                new RokasVnRuntimeAudioPlayback(root.transform, package);

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
            backgroundAspect =
                background.gameObject.AddComponent<AspectRatioFitter>();
            backgroundAspect.enabled = false;

            videoPlayer = root.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.renderMode = VideoRenderMode.APIOnly;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            videoPlayer.waitForFirstFrame = true;

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

            Rect plaqueLogical = new Rect(
                ReferenceWidth * .04f,
                -98f,
                plaqueRect.sizeDelta.x,
                plaqueRect.sizeDelta.y);
            RokasVnPlaqueUiLayout plaqueUi =
                RokasVnRuntimeUiSemantics.Layout(
                    plaqueLogical);

            muteButton = CreateControlButton(
                plaqueRect, "VnMuteButton", 0,
                plaqueUi.Mute, plaqueLogical,
                () => SetMuted(!muted));
            forwardButton = CreateControlButton(
                plaqueRect, "VnForwardButton", 1,
                plaqueUi.Forward, plaqueLogical,
                () => RequestAdvance());
            menuButton = CreateControlButton(
                plaqueRect, "VnMenuButton", 2,
                plaqueUi.Menu, plaqueLogical,
                ToggleMenu);

            completionTriangle = CreateRaw(
                plaqueRect, "VnCompletionTriangle",
                package.CompletionTriangle,
                new Vector2(.5f, 0f),
                new Vector2(.5f, 0f),
                Vector2.zero,
                Vector2.zero);
            ApplyPlaqueLogicalRect(
                (RectTransform)completionTriangle.transform,
                plaqueUi.Triangle,
                plaqueLogical);
            completionTriangleBasePosition =
                ((RectTransform)completionTriangle.transform)
                .anchoredPosition;
            completionTriangle.raycastTarget = false;

            menuOverlay = BuildMenuOverlay(rootRect, fallbackFont);

            sceneTransitionImage = CreateImage(
                rootRect, "VnSceneTransitionOverlay",
                new Color(.015f, .015f, .02f, 1f),
                Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);
            sceneTransitionImage.raycastTarget = true;
            sceneTransitionOverlay =
                sceneTransitionImage.gameObject.AddComponent<CanvasGroup>();
            sceneTransitionOverlay.alpha = 0f;
            sceneTransitionOverlay.blocksRaycasts = false;
            sceneTransitionOverlay.interactable = false;

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
            if (menuOpen || sceneTransitionActive ||
                playback.IsTerminalFadeActive ||
                playback.IsSequenceCompleted)
                return false;

            if (ShouldBeginSceneTransition())
            {
                BeginSceneTransition();
                RefreshControlVisuals();
                return true;
            }

            bool advanced = playback.RequestAdvance();
            RefreshPresentation(false);
            return advanced;
        }

        public void SetMuted(bool value)
        {
            ThrowIfDisposed();
            muted = value;
            audioPlayback.SetMuted(value);
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
            {
                if (sceneTransitionActive)
                    AdvanceSceneTransition(unscaledDeltaTime);
                else
                    playback.AdvanceTime(unscaledDeltaTime);

                audioPlayback.Advance(unscaledDeltaTime);
                AdvanceMotionMedia(unscaledDeltaTime);
            }
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
                renderedBeatIndex = playback.CurrentBeatIndex;
                ApplyPresentationGeometry(scene.presentation);
                RefreshSceneMedia(scene);
                RebuildCharacterViews(scene);
                audioPlayback.EnterScene(
                    scene,
                    playback.CurrentBeat != null
                        ? playback.CurrentBeat.beatId
                        : string.Empty);
            }
            else if (renderedBeatIndex != playback.CurrentBeatIndex)
            {
                renderedBeatIndex = playback.CurrentBeatIndex;
                audioPlayback.EnterBeat(
                    scene,
                    playback.CurrentBeat != null
                        ? playback.CurrentBeat.beatId
                        : string.Empty);
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

        private void ApplyPresentationGeometry(
            RokasVnRuntimePresentationSnapshot presentation)
        {
            dialoguePlaque.texture = package.DialoguePlaque;
            if (presentation != null &&
                !string.IsNullOrWhiteSpace(
                    presentation.dialoguePlaqueAssetGuid))
            {
                RokasVnRuntimeAssetBinding plaqueBinding;
                if (package.TryGetAsset(
                        presentation.dialoguePlaqueAssetGuid,
                        out plaqueBinding) &&
                    plaqueBinding != null)
                {
                    Texture authoredPlaque =
                        ResolveTexture(plaqueBinding.asset);
                    if (authoredPlaque != null)
                        dialoguePlaque.texture = authoredPlaque;
                }
            }

            if (presentation == null ||
                !presentation.hasResolvedOracleGeometry ||
                presentation.dialoguePanelRect.width <= 0f ||
                presentation.dialoguePanelRect.height <= 0f)
                return;

            Rect plaqueLogical = presentation.dialoguePanelRect;
            RectTransform plaqueRect =
                (RectTransform)dialoguePlaque.transform;
            plaqueRect.sizeDelta = plaqueLogical.size;
            plaqueRect.anchoredPosition =
                new Vector2(
                    plaqueLogical.center.x -
                    (ReferenceWidth * .5f),
                    plaqueLogical.y);

            ApplyPlaqueLogicalRect(
                speakerText.rectTransform,
                presentation.speakerNameRect,
                plaqueLogical);
            ApplyPlaqueLogicalRect(
                dialogueText.rectTransform,
                presentation.dialogueTextRect,
                plaqueLogical);

            RokasVnPlaqueUiLayout layout =
                RokasVnRuntimeUiSemantics.Layout(
                    plaqueLogical);
            ApplyPlaqueLogicalRect(
                muteButton.GetComponent<RectTransform>(),
                layout.Mute,
                plaqueLogical);
            ApplyPlaqueLogicalRect(
                forwardButton.GetComponent<RectTransform>(),
                layout.Forward,
                plaqueLogical);
            ApplyPlaqueLogicalRect(
                menuButton.GetComponent<RectTransform>(),
                layout.Menu,
                plaqueLogical);
            ApplyPlaqueLogicalRect(
                (RectTransform)completionTriangle.transform,
                layout.Triangle,
                plaqueLogical);
            completionTriangleBasePosition =
                ((RectTransform)completionTriangle.transform)
                .anchoredPosition;
        }

        private void RefreshSceneMedia(
            RokasVnRuntimeSceneSnapshot scene)
        {
            StopMotionMedia();
            Texture texture = null;
            currentMediaScaleMode =
                scene != null && scene.media != null
                    ? scene.media.scaleMode
                    : 2;

            if (scene != null && scene.media != null)
            {
                string key = !string.IsNullOrWhiteSpace(
                    scene.media.runtimeAssetKey)
                    ? scene.media.runtimeAssetKey
                    : scene.media.reference;
                RokasVnRuntimeAssetBinding binding;
                if (package.TryGetAsset(key, out binding) &&
                    binding != null)
                {
                    if (binding.kind == RokasVnRuntimeAssetKind.Video &&
                        binding.asset is VideoClip)
                    {
                        videoPlayer.clip = (VideoClip)binding.asset;
                        videoPlayer.isLooping = scene.media.loop;
                        videoPlayer.Play();
                        videoActive = true;
                        texture = videoPlayer.texture;
                    }
                    else if (binding.kind == RokasVnRuntimeAssetKind.Bytes &&
                             binding.asset is TextAsset)
                    {
                        TextAsset bytes = (TextAsset)binding.asset;
                        try
                        {
                            RokasVnRuntimeGifDecoder.Decode(
                                bytes.bytes, gifFrames, gifDelays);
                            gifLoop = scene.media.loop;
                            gifFrameIndex = 0;
                            gifFrameElapsed = 0f;
                            if (gifFrames.Count > 0)
                                texture = gifFrames[0];
                        }
                        catch (Exception exception)
                        {
                            ClearGifFrames();
                            Debug.LogWarning(
                                "ROKAS runtime VN GIF decode failed: " +
                                exception.Message);
                        }
                    }
                    else
                    {
                        texture = ResolveTexture(binding.asset);
                    }
                }
            }

            background.texture = texture;
            background.uvRect = new Rect(0f, 0f, 1f, 1f);
            ApplyBackgroundScaleMode(texture, currentMediaScaleMode);
        }

        private void AdvanceMotionMedia(float deltaSeconds)
        {
            if (videoActive)
            {
                Texture videoTexture = videoPlayer.texture;
                if (videoTexture != null &&
                    !ReferenceEquals(background.texture, videoTexture))
                {
                    background.texture = videoTexture;
                    ApplyBackgroundScaleMode(
                        videoTexture, currentMediaScaleMode);
                }
            }

            if (gifFrames.Count <= 1 || deltaSeconds <= 0f)
                return;

            gifFrameElapsed += deltaSeconds;
            int safety = 0;
            while (safety++ < gifFrames.Count + 2)
            {
                float delay = gifFrameIndex < gifDelays.Count
                    ? Mathf.Max(.02f, gifDelays[gifFrameIndex])
                    : .1f;
                if (gifFrameElapsed + .00001f < delay) break;
                gifFrameElapsed -= delay;

                if (gifFrameIndex + 1 < gifFrames.Count)
                {
                    gifFrameIndex++;
                }
                else if (gifLoop)
                {
                    gifFrameIndex = 0;
                }
                else
                {
                    gifFrameElapsed = 0f;
                    break;
                }

                background.texture = gifFrames[gifFrameIndex];
                ApplyBackgroundScaleMode(
                    background.texture, currentMediaScaleMode);
            }
        }

        private void StopMotionMedia()
        {
            if (videoPlayer != null)
            {
                videoPlayer.Stop();
                videoPlayer.clip = null;
            }
            videoActive = false;
            ClearGifFrames();
        }

        private void ClearGifFrames()
        {
            for (int i = 0; i < gifFrames.Count; i++)
            {
                Texture2D frame = gifFrames[i];
                if (!frame) continue;
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(frame);
                else
                    UnityEngine.Object.DestroyImmediate(frame);
            }
            gifFrames.Clear();
            gifDelays.Clear();
            gifFrameIndex = 0;
            gifFrameElapsed = 0f;
            gifLoop = false;
        }

        private void ApplyBackgroundScaleMode(
            Texture texture,
            int scaleMode)
        {
            if (backgroundAspect == null) return;

            if (scaleMode == 2 || texture == null ||
                texture.height <= 0)
            {
                backgroundAspect.enabled = false;
                return;
            }

            backgroundAspect.enabled = true;
            backgroundAspect.aspectRatio =
                Mathf.Max(.0001f,
                    (float)texture.width / texture.height);
            backgroundAspect.aspectMode =
                scaleMode == 0
                    ? AspectRatioFitter.AspectMode.FitInParent
                    : AspectRatioFitter.AspectMode.EnvelopeParent;
        }

        private bool ShouldBeginSceneTransition()
        {
            if (sceneTransitionActive ||
                !playback.IsDialogueRevealComplete)
                return false;

            RokasVnRuntimeSceneSnapshot current =
                playback.CurrentScene;
            if (current == null || current.isTerminal ||
                current.dialogueBeats == null ||
                playback.CurrentBeatIndex <
                    current.dialogueBeats.Count - 1)
                return false;

            int targetIndex = playback.CurrentSceneIndex + 1;
            if (package.Snapshot == null ||
                package.Snapshot.scenes == null ||
                targetIndex < 0 ||
                targetIndex >= package.Snapshot.scenes.Count)
                return false;

            RokasVnRuntimeSceneSnapshot target =
                package.Snapshot.scenes[targetIndex];
            return target != null &&
                   (target.sceneTransitionType == 1 ||
                    target.sceneTransitionType == 2) &&
                   target.sceneTransitionDuration > .0001f;
        }

        private void BeginSceneTransition()
        {
            int targetIndex = playback.CurrentSceneIndex + 1;
            RokasVnRuntimeSceneSnapshot target =
                package.Snapshot.scenes[targetIndex];

            sceneTransitionActive = true;
            sceneTransitionSwapped = false;
            sceneTransitionTargetIndex = targetIndex;
            sceneTransitionType = target.sceneTransitionType;
            sceneTransitionDirection =
                target.sceneTransitionDirection;
            sceneTransitionElapsed = 0f;
            sceneTransitionDuration = Mathf.Clamp(
                target.sceneTransitionDuration,
                .0001f, 10f);

            sceneTransitionImage.gameObject.SetActive(true);
            sceneTransitionOverlay.blocksRaycasts = true;
            sceneTransitionOverlay.interactable = true;
            UpdateSceneTransitionVisual();
        }

        private void AdvanceSceneTransition(float deltaSeconds)
        {
            if (!sceneTransitionActive) return;

            sceneTransitionElapsed += Mathf.Max(0f, deltaSeconds);
            float half = Mathf.Max(
                .0001f, sceneTransitionDuration * .5f);

            if (!sceneTransitionSwapped &&
                sceneTransitionElapsed + .000001f >= half)
            {
                sceneTransitionSwapped = true;
                if (!playback.AdvanceDialogue() ||
                    playback.CurrentSceneIndex !=
                        sceneTransitionTargetIndex)
                {
                    CancelSceneTransition();
                    throw new InvalidOperationException(
                        "Runtime VN Scene transition could not commit its target Scene.");
                }
            }

            if (sceneTransitionElapsed + .000001f >=
                sceneTransitionDuration)
            {
                CompleteSceneTransition();
                return;
            }

            UpdateSceneTransitionVisual();
        }

        private void UpdateSceneTransitionVisual()
        {
            if (!sceneTransitionActive)
            {
                sceneTransitionOverlay.alpha = 0f;
                sceneTransitionOverlay.blocksRaycasts = false;
                sceneTransitionOverlay.interactable = false;
                return;
            }

            float half = Mathf.Max(
                .0001f, sceneTransitionDuration * .5f);
            bool reveal = sceneTransitionElapsed >= half;
            float phaseProgress = reveal
                ? Mathf.Clamp01(
                    (sceneTransitionElapsed - half) / half)
                : Mathf.Clamp01(
                    sceneTransitionElapsed / half);
            float coverage = reveal
                ? 1f - phaseProgress
                : phaseProgress;

            RectTransform rect =
                (RectTransform)sceneTransitionImage.transform;
            // Wipe coverage is represented by the overlay RectTransform itself.
            // Keep every covered pixel opaque; fading the wipe re-exposes the
            // outgoing frame and creates a visible half-transparent transition.
            sceneTransitionOverlay.alpha =
                sceneTransitionType == 1
                    ? (coverage > .0001f ? 1f : 0f)
                    : coverage;
            sceneTransitionOverlay.blocksRaycasts = true;
            sceneTransitionOverlay.interactable = true;

            if (sceneTransitionType == 1)
            {
                bool anchorRight =
                    (!reveal && sceneTransitionDirection == 1) ||
                    (reveal && sceneTransitionDirection == 0);
                rect.anchorMin = anchorRight
                    ? new Vector2(1f - coverage, 0f)
                    : Vector2.zero;
                rect.anchorMax = anchorRight
                    ? Vector2.one
                    : new Vector2(coverage, 1f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            else
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
        }

        private void CompleteSceneTransition()
        {
            sceneTransitionActive = false;
            sceneTransitionSwapped = false;
            sceneTransitionTargetIndex = -1;
            sceneTransitionElapsed = 0f;
            sceneTransitionDuration = 0f;
            sceneTransitionType = 0;
            sceneTransitionOverlay.alpha = 0f;
            sceneTransitionOverlay.blocksRaycasts = false;
            sceneTransitionOverlay.interactable = false;
            sceneTransitionImage.gameObject.SetActive(false);
            RefreshControlVisuals();
        }

        private void CancelSceneTransition()
        {
            sceneTransitionActive = false;
            sceneTransitionSwapped = false;
            sceneTransitionTargetIndex = -1;
            sceneTransitionElapsed = 0f;
            sceneTransitionDuration = 0f;
            sceneTransitionType = 0;
            sceneTransitionOverlay.alpha = 0f;
            sceneTransitionOverlay.blocksRaycasts = false;
            sceneTransitionOverlay.interactable = false;
            sceneTransitionImage.gameObject.SetActive(false);
            RefreshControlVisuals();
        }

        private void RebuildCharacterViews(
            RokasVnRuntimeSceneSnapshot scene)
        {
            foreach (CharacterView view in characterViews.Values)
            {
                if (view == null || view.Image == null) continue;

                // Destroy is deferred in PlayMode. Remove outgoing characters
                // from render ownership synchronously at the covered Scene swap
                // so old and incoming render trees can never overlap for a frame.
                GameObject characterObject = view.Image.gameObject;
                characterObject.SetActive(false);
                characterObject.transform.SetParent(null, false);
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(characterObject);
                else
                    UnityEngine.Object.DestroyImmediate(characterObject);
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

            speakerText.text =
                beat.narration
                    ? string.Empty
                    : (beat.speaker ?? string.Empty);
            string full = beat.text ?? string.Empty;
            int visible = Mathf.Clamp(
                playback.VisibleDialogueCharacters,
                0,
                full.Length);
            dialogueText.text =
                visible >= full.Length
                    ? full
                    : full.Substring(0, visible);

            RokasVnRuntimeTypographySnapshot typography =
                beat.typography;
            bool hasBeatTypography =
                typography != null && typography.resolved;

            string speakerFontGuid =
                hasBeatTypography
                    ? typography.speakerFontAssetGuid
                    : presentation.speakerFontAssetGuid;
            string dialogueFontGuid =
                hasBeatTypography
                    ? typography.dialogueFontAssetGuid
                    : presentation.dialogueFontAssetGuid;
            int speakerFontPreset =
                hasBeatTypography
                    ? typography.speakerFontPreset
                    : presentation.speakerFontPreset;
            int dialogueFontPreset =
                hasBeatTypography
                    ? typography.dialogueFontPreset
                    : presentation.dialogueFontPreset;

            speakerText.font =
                ResolveRuntimeFont(
                    speakerFontGuid,
                    speakerFontPreset);
            dialogueText.font =
                ResolveRuntimeFont(
                    dialogueFontGuid,
                    dialogueFontPreset);
            speakerText.fontStyle =
                string.IsNullOrWhiteSpace(speakerFontGuid)
                    ? FontStyle.Bold
                    : FontStyle.Normal;
            dialogueText.fontStyle = FontStyle.Normal;

            speakerText.fontSize = Mathf.Max(
                1, Mathf.RoundToInt(
                    hasBeatTypography
                        ? typography.speakerFontSize
                        : presentation.speakerFontSize));
            dialogueText.fontSize = Mathf.Max(
                1, Mathf.RoundToInt(
                    hasBeatTypography
                        ? typography.dialogueFontSize
                        : presentation.dialogueFontSize));
            speakerText.color =
                hasBeatTypography
                    ? typography.speakerColor
                    : presentation.speakerColor;
            dialogueText.color =
                hasBeatTypography
                    ? typography.dialogueColor
                    : presentation.dialogueColor;
            speakerText.alignment =
                ToTextAnchor(
                    hasBeatTypography
                        ? typography.speakerAlignment
                        : presentation.speakerAlignment,
                    TextAnchor.MiddleLeft);
            dialogueText.alignment =
                ToTextAnchor(
                    hasBeatTypography
                        ? typography.dialogueAlignment
                        : presentation.dialogueAlignment,
                    TextAnchor.MiddleLeft);

            bool showTriangle =
                playback.IsDialogueRevealComplete &&
                !playback.IsTerminalFadeActive &&
                !playback.IsSequenceCompleted &&
                !menuOpen;
            completionTriangle.gameObject.SetActive(showTriangle);
            if (showTriangle)
            {
                RokasVnTriangleUiSample sample =
                    RokasVnRuntimeUiSemantics.SampleTriangle(
                        uiElapsedSeconds);
                RectTransform triangle =
                    (RectTransform)completionTriangle.transform;
                triangle.anchoredPosition =
                    completionTriangleBasePosition +
                    new Vector2(0f, sample.OffsetY);
                triangle.localScale =
                    new Vector3(
                        sample.Scale,
                        sample.Scale,
                        1f);
                completionTriangle.color =
                    new Color(
                        1f, 1f, 1f,
                        sample.Alpha);
            }
        }

        private void RefreshControlVisuals()
        {
            bool commonEnabled =
                !menuOpen &&
                !sceneTransitionActive &&
                !playback.IsTerminalFadeActive &&
                !playback.IsSequenceCompleted;
            forwardButton.interactable = commonEnabled;
            muteButton.interactable = commonEnabled;
            menuButton.interactable = commonEnabled;

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

            ApplyControlVisual(muteButton);
            ApplyControlVisual(forwardButton);
            ApplyControlVisual(menuButton);
        }

        private void ApplyControlVisual(Button button)
        {
            if (button == null) return;

            RokasVnRuntimeControlPointerState pointer =
                button.GetComponent<
                    RokasVnRuntimeControlPointerState>();
            bool enabled = button.interactable;
            if (!enabled && pointer != null)
                pointer.ResetState();

            bool hover =
                enabled && pointer != null && pointer.Hovered;
            bool pressed =
                hover && pointer != null && pointer.Pressed;

            ControlMotion motion;
            if (!controlMotions.TryGetValue(
                    button, out motion))
            {
                motion = new ControlMotion();
                controlMotions[button] = motion;
            }
            motion.Target(
                hover,
                pressed,
                enabled,
                uiElapsedSeconds);

            RawImage graphic =
                button.targetGraphic as RawImage;
            if (graphic == null) return;

            float scale =
                motion.Scale(uiElapsedSeconds);
            graphic.rectTransform.localScale =
                new Vector3(scale, scale, 1f);
            float brightness =
                motion.Brightness(uiElapsedSeconds);
            graphic.color = new Color(
                brightness,
                brightness,
                brightness,
                RokasVnRuntimeUiSemantics.ButtonAlpha(
                    enabled));
        }

        private Button CreateControlButton(
            RectTransform plaque,
            string name,
            int controlIndex,
            Rect logicalRect,
            Rect plaqueLogical,
            Action action)
        {
            RectTransform rect = CreateRect(
                plaque,
                name,
                new Vector2(.5f, 0f),
                new Vector2(.5f, 0f),
                Vector2.zero,
                Vector2.zero);
            ApplyPlaqueLogicalRect(
                rect,
                logicalRect,
                plaqueLogical);

            Image hitSurface =
                rect.gameObject.AddComponent<Image>();
            hitSurface.color =
                new Color(0f, 0f, 0f, 0f);
            hitSurface.raycastTarget = true;

            RectTransform visual = CreateRect(
                rect,
                name + "Visual",
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            RawImage graphic =
                visual.gameObject.AddComponent<RawImage>();
            graphic.texture = package.ControlSheet;
            graphic.uvRect = ControlUv(controlIndex);
            graphic.color = new Color(
                RokasVnRuntimeUiSemantics
                    .ButtonBaseBrightness,
                RokasVnRuntimeUiSemantics
                    .ButtonBaseBrightness,
                RokasVnRuntimeUiSemantics
                    .ButtonBaseBrightness,
                RokasVnRuntimeUiSemantics
                    .ButtonEnabledAlpha);
            graphic.raycastTarget = false;

            Button button =
                rect.gameObject.AddComponent<Button>();
            button.targetGraphic = graphic;
            button.transition =
                Selectable.Transition.None;
            rect.gameObject.AddComponent<
                RokasVnRuntimeControlPointerState>();
            button.onClick.AddListener(
                () =>
                {
                    if (!disposed && action != null)
                        action();
                });
            return button;
        }

        private static void ApplyPlaqueLogicalRect(
            RectTransform target,
            Rect logicalRect,
            Rect plaqueLogical)
        {
            target.anchorMin = new Vector2(.5f, 0f);
            target.anchorMax = new Vector2(.5f, 0f);
            target.pivot = new Vector2(.5f, .5f);
            target.sizeDelta = logicalRect.size;
            target.anchoredPosition = new Vector2(
                logicalRect.center.x -
                plaqueLogical.center.x,
                logicalRect.center.y -
                plaqueLogical.y);
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

            Rect canvasLogical = new Rect(
                0f,
                0f,
                ReferenceWidth,
                ReferenceHeight);
            RokasVnMenuUiLayout layout =
                RokasVnRuntimeUiSemantics.MenuLayout(
                    canvasLogical);

            Image shade = CreateImage(
                rect,
                "Shade",
                RokasVnRuntimeUiSemantics
                    .MenuShadeColor,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            shade.raycastTarget = true;

            Image panel = CreateImage(
                rect,
                "VnMenuPanel",
                RokasVnRuntimeUiSemantics
                    .MenuPanelColor,
                new Vector2(.5f, .5f),
                new Vector2(.5f, .5f),
                Vector2.zero,
                Vector2.zero);
            ApplyCanvasTopLeftRect(
                panel.rectTransform,
                layout.Panel,
                canvasLogical);
            panel.raycastTarget = false;
            Outline outline =
                panel.gameObject.AddComponent<Outline>();
            outline.effectColor =
                RokasVnRuntimeUiSemantics
                    .MenuOutlineColor;
            outline.effectDistance =
                new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = false;

            Text title = CreateText(
                rect,
                "VnMenuTitle",
                font,
                new Vector2(.5f, .5f),
                new Vector2(.5f, .5f),
                RokasVnRuntimeUiSemantics
                    .MenuTitle,
                RokasVnRuntimeUiSemantics
                    .MenuTitleFontSize(
                        ReferenceHeight),
                FontStyle.Bold);
            ApplyCanvasTopLeftRect(
                title.rectTransform,
                layout.Title,
                canvasLogical);
            title.color =
                RokasVnRuntimeUiSemantics
                    .MenuTitleColor;
            title.alignment =
                TextAnchor.MiddleLeft;

            CreateMenuButton(
                rect,
                "VnResumeButton",
                font,
                0,
                layout.Resume,
                canvasLogical,
                true,
                ToggleMenu);
            CreateMenuButton(
                rect,
                "VnSettingsButton",
                font,
                1,
                layout.Settings,
                canvasLogical,
                false,
                null);
            CreateMenuButton(
                rect,
                "VnSaveButton",
                font,
                2,
                layout.Save,
                canvasLogical,
                false,
                null);
            CreateMenuButton(
                rect,
                "VnMainMenuButton",
                font,
                3,
                layout.MainMenu,
                canvasLogical,
                false,
                null);

            overlay.SetActive(false);
            return overlay;
        }

        private void ToggleMenu()
        {
            if (disposed ||
                sceneTransitionActive ||
                playback.IsTerminalFadeActive ||
                playback.IsSequenceCompleted)
                return;
            menuOpen = !menuOpen;
            menuOverlay.SetActive(menuOpen);
            audioPlayback.SetPaused(menuOpen);
            RefreshControlVisuals();
        }

        internal void HandleEscapeInput()
        {
            if (!disposed && menuOpen)
                ToggleMenu();
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
            StopMotionMedia();
            audioPlayback.Dispose();
            if (root)
            {
                // Destroy is deferred until the end of the frame. Remove the VN
                // synchronously from input/render ownership before the caller
                // reveals its continuation, then let Unity destroy it normally.
                root.SetActive(false);
                root.transform.SetParent(null, false);
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(root);
                else
                    UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private Font ResolveFallbackFont()
        {
            return ResolveRuntimeFont(string.Empty, 0);
        }

        private Font ResolveRuntimeFont(
            string authoredGuid,
            int fontPreset)
        {
            if (!string.IsNullOrWhiteSpace(authoredGuid))
            {
                RokasVnRuntimeAssetBinding binding;
                if (package.TryGetAsset(
                        authoredGuid.Trim(),
                        out binding) &&
                    binding != null)
                {
                    Font authored = binding.asset as Font;
                    if (authored != null)
                        return authored;
                }
            }

            RokasAssets assets =
                Resources.Load<RokasAssets>("RokasAssets");
            if (assets != null)
            {
                if (fontPreset == 1 &&
                    assets.serif != null)
                    return assets.serif;
                if (assets.sans != null)
                    return assets.sans;
            }

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
            text.supportRichText = true;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateMenuButton(
            Transform parent,
            string name,
            Font font,
            int labelIndex,
            Rect logicalRect,
            Rect canvasLogical,
            bool interactable,
            Action action)
        {
            RectTransform rect = CreateRect(
                parent,
                name,
                new Vector2(.5f, .5f),
                new Vector2(.5f, .5f),
                Vector2.zero,
                Vector2.zero);
            ApplyCanvasTopLeftRect(
                rect,
                logicalRect,
                canvasLogical);

            Image image =
                rect.gameObject.AddComponent<Image>();
            image.color =
                new Color(.08f, .16f, .22f, .98f);
            Button button =
                rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = interactable;
            if (action != null)
            {
                button.onClick.AddListener(
                    () => action());
            }

            Text text = CreateText(
                rect,
                "Label",
                font,
                Vector2.zero,
                Vector2.one,
                RokasVnRuntimeUiSemantics
                    .MenuLabel(labelIndex),
                RokasVnRuntimeUiSemantics
                    .MenuRowFontSize(
                        logicalRect.height),
                FontStyle.Normal);
            text.rectTransform.offsetMin =
                new Vector2(16f, 4f);
            text.rectTransform.offsetMax =
                new Vector2(-10f, -4f);
            text.alignment =
                TextAnchor.MiddleLeft;
            text.color = Color.white;
            return button;
        }

        private static void ApplyCanvasTopLeftRect(
            RectTransform target,
            Rect logicalRect,
            Rect canvasLogical)
        {
            target.anchorMin =
                new Vector2(.5f, .5f);
            target.anchorMax =
                new Vector2(.5f, .5f);
            target.pivot =
                new Vector2(.5f, .5f);
            target.sizeDelta =
                logicalRect.size;
            target.anchoredPosition =
                new Vector2(
                    logicalRect.center.x -
                    canvasLogical.center.x,
                    canvasLogical.center.y -
                    logicalRect.center.y);
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
