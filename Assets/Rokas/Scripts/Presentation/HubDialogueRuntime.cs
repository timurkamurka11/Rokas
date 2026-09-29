using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    [Serializable]
    public sealed class HubDialogueDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private string speaker;
        [SerializeField] private List<string> lines;

        public string Id => id ?? string.Empty;
        public string Speaker => speaker ?? string.Empty;
        public IReadOnlyList<string> Lines => lines;

        public HubDialogueDefinition(
            string id,
            string speaker,
            params string[] lines)
        {
            this.id = id ?? string.Empty;
            this.speaker = speaker ?? string.Empty;
            this.lines = new List<string>();
            if (lines == null) return;
            for (int i = 0; i < lines.Length; i++)
                if (!string.IsNullOrEmpty(lines[i]))
                    this.lines.Add(lines[i]);
        }
    }

    public enum HubDialogueAdvanceResult
    {
        Ignored,
        CompletedCurrentLine,
        AdvancedLine,
        Closed
    }

    public enum HubDialoguePortraitState
    {
        Idle,
        Talk
    }

    public sealed class HubDialoguePlaybackState
    {
        private HubDialogueDefinition definition;
        private RokasDialogueTypewriterSettings typewriter;
        private int lineIndex = -1;
        private float elapsed;
        private bool forcedComplete;

        public bool IsOpen { get; private set; }
        public int CurrentLineIndex => lineIndex;
        public HubDialogueDefinition Definition => definition;
        public string CurrentLine
        {
            get
            {
                if (!IsOpen || definition == null || definition.Lines == null ||
                    lineIndex < 0 || lineIndex >= definition.Lines.Count)
                    return string.Empty;
                return definition.Lines[lineIndex] ?? string.Empty;
            }
        }

        public int VisibleCharacters
        {
            get
            {
                string text = CurrentLine;
                if (forcedComplete) return text.Length;
                return RokasDialogueTypewriter.CalculateVisibleCharacters(
                    text, elapsed, typewriter);
            }
        }

        public bool IsTyping =>
            IsOpen && VisibleCharacters < CurrentLine.Length;

        public bool ShowCompletionIndicator =>
            IsOpen && !IsTyping;

        public HubDialoguePortraitState PortraitState =>
            IsTyping
                ? HubDialoguePortraitState.Talk
                : HubDialoguePortraitState.Idle;

        public bool Open(
            HubDialogueDefinition value,
            RokasDialogueTypewriterSettings settings)
        {
            if (IsOpen || value == null || value.Lines == null ||
                value.Lines.Count == 0)
                return false;

            definition = value;
            typewriter = settings;
            lineIndex = 0;
            elapsed = 0f;
            forcedComplete = false;
            IsOpen = true;
            return true;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (unscaledDeltaTime < 0f ||
                float.IsNaN(unscaledDeltaTime) ||
                float.IsInfinity(unscaledDeltaTime))
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaTime));
            if (!IsOpen) return;
            elapsed += unscaledDeltaTime;
        }

        public HubDialogueAdvanceResult RequestAdvance()
        {
            if (!IsOpen) return HubDialogueAdvanceResult.Ignored;

            if (IsTyping)
            {
                forcedComplete = true;
                return HubDialogueAdvanceResult.CompletedCurrentLine;
            }

            if (definition != null && definition.Lines != null &&
                lineIndex + 1 < definition.Lines.Count)
            {
                lineIndex++;
                elapsed = 0f;
                forcedComplete = false;
                return HubDialogueAdvanceResult.AdvancedLine;
            }

            Close();
            return HubDialogueAdvanceResult.Closed;
        }

        public void Close()
        {
            IsOpen = false;
            definition = null;
            lineIndex = -1;
            elapsed = 0f;
            forcedComplete = false;
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HubDialogueOctagonMaskGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = GetPixelAdjustedRect();
            float cut = Mathf.Min(r.width, r.height) * .20f;
            Vector2[] points =
            {
                new Vector2(r.xMin + cut, r.yMax),
                new Vector2(r.xMax - cut, r.yMax),
                new Vector2(r.xMax, r.yMax - cut),
                new Vector2(r.xMax, r.yMin + cut),
                new Vector2(r.xMax - cut, r.yMin),
                new Vector2(r.xMin + cut, r.yMin),
                new Vector2(r.xMin, r.yMin + cut),
                new Vector2(r.xMin, r.yMax - cut)
            };

            Vector2 center = r.center;
            mesh.AddVert(center, Color.white, new Vector2(.5f, .5f));
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 uv = new Vector2(
                    Mathf.InverseLerp(r.xMin, r.xMax, points[i].x),
                    Mathf.InverseLerp(r.yMin, r.yMax, points[i].y));
                mesh.AddVert(points[i], Color.white, uv);
            }

            for (int i = 0; i < points.Length; i++)
                mesh.AddTriangle(
                    0,
                    i + 1,
                    ((i + 1) % points.Length) + 1);
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HubDialogueTriangleGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = GetPixelAdjustedRect();
            int first = mesh.currentVertCount;
            mesh.AddVert(
                new Vector3(r.xMin, r.yMin, 0f),
                color, new Vector2(0f, 0f));
            mesh.AddVert(
                new Vector3(r.xMin, r.yMax, 0f),
                color, new Vector2(0f, 1f));
            mesh.AddVert(
                new Vector3(r.xMax, r.center.y, 0f),
                color, new Vector2(1f, .5f));
            mesh.AddTriangle(first, first + 1, first + 2);
        }
    }

    public sealed class HubDialogueController : IDisposable
    {
        private readonly UiKit ui;
        private readonly RokasAssets assets;
        private readonly RokasAudio audio;
        private readonly Action<bool> setSceneInteractable;
        private readonly Action openMenu;
        private readonly HubDialoguePlaybackState playback =
            new HubDialoguePlaybackState();
        private readonly HubDialogueConfigData config;
        private readonly AudioClip voiceClip;
        private readonly RectTransform root;
        private readonly RawImage portrait;
        private readonly Text speaker;
        private readonly Text dialogue;
        private readonly HubDialogueTriangleGraphic completionTriangle;
        private readonly Vector2 triangleBasePosition;
        private readonly Button muteButton;
        private readonly Button forwardButton;
        private readonly Button menuButton;
        private float uiElapsed;
        private float portraitElapsed;
        private HubDialoguePortraitState portraitState;
        private int portraitFrame = -1;
        private int focusedControl;
        private bool voiceActive;

        public bool IsOpen => playback.IsOpen;
        public HubDialoguePortraitState PortraitState => playback.PortraitState;
        public bool IsTyping => playback.IsTyping;

        public HubDialogueController(
            UiKit ui,
            Transform parent,
            RokasAssets assets,
            RokasAudio audio,
            Action<bool> setSceneInteractable,
            Action openMenu)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
            this.audio = audio ?? throw new ArgumentNullException(nameof(audio));
            this.setSceneInteractable =
                setSceneInteractable ?? throw new ArgumentNullException(nameof(setSceneInteractable));
            this.openMenu = openMenu;
            config = HubDialogueConfig.Current;
            voiceClip = config.voiceEnabled
                ? Resources.Load<AudioClip>(config.voiceResourcePath)
                : null;

            if (!assets.hubDialoguePlaque || !assets.hubDialoguePortraitAtlas)
                throw new InvalidOperationException(
                    "ROKAS Hub Dialogue assets are missing from RokasAssets.");

            root = ui.Rect(parent, "HubDialogueRoot", 0, 0, 1920, 1080);
            root.SetAsLastSibling();

            RectTransform layoutRoot = ui.Rect(
                root, "HubDialogueLayoutRoot",
                config.layoutRoot.x, config.layoutRoot.y,
                config.layoutRoot.width, config.layoutRoot.height);
            layoutRoot.localScale =
                new Vector3(config.layoutScale, config.layoutScale, 1f);
            ApplyRotation(layoutRoot, config.layoutRoot);

            Image blocker = ui.Box(
                root, "HubDialogueAdvanceSurface",
                0, 0, 1920, 1080,
                new Color(0f, 0f, 0f, 0f), true);
            Button blockerButton = blocker.gameObject.AddComponent<Button>();
            blockerButton.transition = Selectable.Transition.None;
            blockerButton.targetGraphic = blocker;
            blockerButton.navigation = new Navigation
            {
                mode = Navigation.Mode.None
            };
            blockerButton.onClick.AddListener(RequestAdvance);

            RectTransform plaqueRoot = ui.Rect(
                layoutRoot, "HubDialoguePlaque",
                config.plaque.x, config.plaque.y,
                config.plaque.width, config.plaque.height);

            ApplyRotation(plaqueRoot, config.plaque);
            RectTransform maskRoot = ui.Rect(
                plaqueRoot, "PortraitMask",
                config.portraitMask.x, config.portraitMask.y,
                config.portraitMask.width, config.portraitMask.height);
            ApplyRotation(maskRoot, config.portraitMask);
            var maskGraphic =
                maskRoot.gameObject.AddComponent<HubDialogueOctagonMaskGraphic>();
            maskGraphic.color = Color.white;
            maskGraphic.raycastTarget = false;
            Mask mask = maskRoot.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            portrait = ui.Art(
                maskRoot, "PortraitImage",
                assets.hubDialoguePortraitAtlas,
                config.portrait.x, config.portrait.y,
                config.portrait.width, config.portrait.height);
            portrait.raycastTarget = false;
            ApplyRotation(portrait.rectTransform, config.portrait);

            RawImage plaque = ui.Art(
                plaqueRoot, "PlaqueArt",
                assets.hubDialoguePlaque,
                config.plaqueArt.x, config.plaqueArt.y,
                config.plaqueArt.width, config.plaqueArt.height);
            plaque.raycastTarget = false;
            ApplyRotation(plaque.rectTransform, config.plaqueArt);

            speaker = ui.Label(
                plaqueRoot, "SpeakerName", string.Empty,
                config.speaker.x, config.speaker.y,
                config.speaker.width, config.speaker.height,
                config.speakerFontSize,
                new Color(.90f, .96f, 1f, 1f),
                false, config.speakerAlignment);
            speaker.fontStyle = FontStyle.Bold;
            ApplyRotation(speaker.rectTransform, config.speaker);

            dialogue = ui.Label(
                plaqueRoot, "DialogueText", string.Empty,
                config.dialogue.x, config.dialogue.y,
                config.dialogue.width, config.dialogue.height,
                config.dialogueFontSize, Color.white,
                false, config.dialogueAlignment);
            dialogue.lineSpacing = config.dialogueLineSpacing;
            ApplyRotation(dialogue.rectTransform, config.dialogue);

            // PLANK.png contains a baked static arrow. Cover it with a nearby
            // sample of the same panel texture, then render the shared animated
            // completion semantic on top only when reveal is complete.
            RawImage arrowCover = ui.Art(
                plaqueRoot, "BakedArrowCover",
                assets.hubDialoguePlaque,
                config.arrowCover.x, config.arrowCover.y,
                config.arrowCover.width, config.arrowCover.height);
            arrowCover.uvRect = new Rect(
                1680f / 2048f, 155f / 682f,
                90f / 2048f, 90f / 682f);
            arrowCover.raycastTarget = false;
            ApplyRotation(arrowCover.rectTransform, config.arrowCover);

            RectTransform triangleRect = ui.Rect(
                plaqueRoot, "CompletionArrow",
                config.completionArrow.x, config.completionArrow.y,
                config.completionArrow.width, config.completionArrow.height);
            completionTriangle =
                triangleRect.gameObject.AddComponent<HubDialogueTriangleGraphic>();
            completionTriangle.color = Color.white;
            completionTriangle.raycastTarget = false;
            ApplyRotation(triangleRect, config.completionArrow);
            triangleBasePosition = triangleRect.anchoredPosition;

            muteButton = TransparentButton(
                plaqueRoot, "HubMuteButton",
                config.muteButton.x, config.muteButton.y,
                config.muteButton.width, config.muteButton.height,
                () =>
                {
                    audio.SetVnMuted(!audio.VnMuted);
                    Refresh();
                });
            muteButton.gameObject.SetActive(config.muteButtonEnabled);
            ApplyRotation((RectTransform)muteButton.transform, config.muteButton);
            forwardButton = TransparentButton(
                plaqueRoot, "HubForwardButton",
                config.forwardButton.x, config.forwardButton.y,
                config.forwardButton.width, config.forwardButton.height,
                RequestAdvance);
            forwardButton.gameObject.SetActive(config.forwardButtonEnabled);
            ApplyRotation((RectTransform)forwardButton.transform, config.forwardButton);
            menuButton = TransparentButton(
                plaqueRoot, "HubMenuButton",
                config.menuButton.x, config.menuButton.y,
                config.menuButton.width, config.menuButton.height,
                () =>
                {
                    Close();
                    openMenu?.Invoke();
                });
            menuButton.gameObject.SetActive(config.menuButtonEnabled);
            ApplyRotation((RectTransform)menuButton.transform, config.menuButton);

            root.gameObject.SetActive(false);
        }

        public bool Open(HubDialogueDefinition definition)
        {
            if (!playback.Open(
                    definition,
                    RokasDialogueTypewriterSettings.HubDefault))
                return false;

            uiElapsed = 0f;
            portraitElapsed = 0f;
            portraitFrame = -1;
            portraitState = HubDialoguePortraitState.Talk;
            focusedControl = 0;
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            setSceneInteractable(false);
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(null);
            RefreshPortrait(true);
            Refresh();
            SyncVoice();
            return true;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!playback.IsOpen) return;
            playback.Tick(unscaledDeltaTime);
            uiElapsed += unscaledDeltaTime;
            portraitElapsed += unscaledDeltaTime;
            RefreshPortrait(false);
            Refresh();
            SyncVoice();
        }

        public void RequestAdvance()
        {
            if (!playback.IsOpen) return;
            HubDialogueAdvanceResult result = playback.RequestAdvance();
            SyncVoice();

            if (result == HubDialogueAdvanceResult.Closed)
            {
                FinishClose();
                return;
            }

            if (result == HubDialogueAdvanceResult.AdvancedLine)
            {
                portraitElapsed = 0f;
                portraitFrame = -1;
            }

            RefreshPortrait(true);
            Refresh();
        }

        public void Close()
        {
            if (!playback.IsOpen) return;
            playback.Close();
            FinishClose();
        }

        public void FocusNextControl()
        {
            if (!playback.IsOpen || !EventSystem.current) return;
            Button[] controls = { forwardButton, muteButton, menuButton };
            for (int i = 0; i < controls.Length; i++)
            {
                focusedControl = (focusedControl + 1) % controls.Length;
                Button candidate = controls[focusedControl];
                if (candidate != null && candidate.IsInteractable())
                {
                    EventSystem.current.SetSelectedGameObject(
                        candidate.gameObject);
                    return;
                }
            }
        }

        public void Dispose()
        {
            playback.Close();
            StopVoice();
            if (root)
                UnityEngine.Object.Destroy(root.gameObject);
        }

        private void FinishClose()
        {
            StopVoice();
            root.gameObject.SetActive(false);
            speaker.text = string.Empty;
            dialogue.text = string.Empty;
            completionTriangle.gameObject.SetActive(false);
            setSceneInteractable(true);
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void Refresh()
        {
            if (!playback.IsOpen) return;

            speaker.text = playback.Definition != null
                ? playback.Definition.Speaker
                : string.Empty;

            string full = playback.CurrentLine ?? string.Empty;
            int visible = Mathf.Clamp(
                playback.VisibleCharacters, 0, full.Length);
            dialogue.text =
                visible >= full.Length
                    ? full
                    : full.Substring(0, visible);

            bool showCompletion = playback.ShowCompletionIndicator;
            completionTriangle.gameObject.SetActive(showCompletion);
            if (showCompletion)
            {
                RokasVnTriangleUiSample sample =
                    RokasVnRuntimeUiSemantics.SampleTriangle(uiElapsed);
                RectTransform triangle =
                    (RectTransform)completionTriangle.transform;
                triangle.anchoredPosition =
                    triangleBasePosition +
                    new Vector2(
                        0f,
                        sample.OffsetY * config.completionBobScale);
                float completionScale =
                    1f + (sample.Scale - 1f) *
                    config.completionPulseScale;
                triangle.localScale =
                    new Vector3(
                        completionScale,
                        completionScale,
                        1f);
                completionTriangle.color =
                    new Color(1f, 1f, 1f, sample.Alpha);
            }

            muteButton.interactable = true;
            forwardButton.interactable = true;
            menuButton.interactable = true;
        }

        private void RefreshPortrait(bool force)
        {
            HubDialoguePortraitState requested = playback.PortraitState;
            if (requested != portraitState)
            {
                portraitState = requested;
                portraitElapsed = 0f;
                portraitFrame = -1;
                force = true;
            }

            float fps =
                portraitState == HubDialoguePortraitState.Talk
                    ? config.talkFramesPerSecond
                    : config.idleFramesPerSecond;
            int frame = Mathf.FloorToInt(
                portraitElapsed * Mathf.Max(.01f, fps)) % 6;
            if (!force && frame == portraitFrame) return;
            portraitFrame = frame;

            float rowY =
                portraitState == HubDialoguePortraitState.Idle
                    ? .5f
                    : 0f;
            portrait.uvRect = new Rect(
                portraitFrame / 6f,
                rowY,
                1f / 6f,
                .5f);
        }

        private static void ApplyRotation(
            RectTransform rect,
            HubDialogueRectData data)
        {
            if (!rect || data == null) return;
            rect.localEulerAngles =
                new Vector3(0f, 0f, data.rotationZ);
        }

        private void SyncVoice()
        {
            bool shouldPlay =
                playback.IsOpen &&
                playback.IsTyping &&
                config.voiceEnabled &&
                voiceClip;

            if (shouldPlay)
            {
                if (voiceActive) return;
                audio.StartHubTextVoice(
                    voiceClip,
                    config.voiceVolume,
                    config.voicePitch,
                    config.voiceLoop);
                voiceActive = true;
                return;
            }

            StopVoice();
        }

        private void StopVoice()
        {
            if (!voiceActive && !audio.HubVoicePlaying) return;
            audio.StopHubTextVoice();
            voiceActive = false;
        }

        private static Button TransparentButton(
            Transform parent,
            string name,
            float x,
            float y,
            float width,
            float height,
            Action action)
        {
            var go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);

            Image image = go.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, .001f);
            image.raycastTarget = true;

            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, .72f);
            colors.pressedColor = new Color(.78f, .88f, 1f, .72f);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = .08f;
            button.colors = colors;
            button.navigation = new Navigation
            {
                mode = Navigation.Mode.Automatic
            };
            button.onClick.AddListener(() => action?.Invoke());
            return button;
        }
    }
}
