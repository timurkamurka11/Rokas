using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public enum VnWorkshopPreviewEffect
    {
        None,
        Typewriter,
        Expression,
        CharacterEnter,
        CharacterExit,
        Bounce,
        BackgroundTransition,
        StageOneTwo,
        StageTwoThree,
        StageThreeTwo,
        StageTwoOne,
        SpeakerSwitch,
        UiPress
    }

    public struct VnWorkshopPreviewPlaybackSnapshot
    {
        public VnWorkshopPreviewEffect Effect;
        public bool IsPlaying;
        public bool IsPaused;
        public float NormalizedProgress;
        public float Duration;
    }

    public sealed partial class VnPresentationWorkshopWindow
    {
        public const string DefaultPreviewSampleText = "Привет, Мина! Это пробный текст VN Workshop.\nМожно вставить свой текст или загрузить UTF-8 TXT.";

        private static readonly string[] PresentationSections =
        {
            "Typography",
            "Preview Text",
            "Text Reveal",
            "Character Expression",
            "Character Enter / Exit",
            "Action Bounce",
            "Background Transition",
            "Stage Layout",
            "Speaker Focus",
            "UI Feedback",
            "Timing / Pacing"
        };

        private static readonly string[] MinaExpressionIds =
        {
            "mina_neutral", "mina_happy", "mina_serious", "mina_surprised", "mina_reaching"
        };

        [SerializeField] private string previewSampleText = DefaultPreviewSampleText;
        [SerializeField] private Vector2 vn10InspectorScroll;
        [SerializeField] private bool foldTypography = true;
        [SerializeField] private bool foldPreviewText = true;
        [SerializeField] private bool foldTextReveal;
        [SerializeField] private bool foldExpression;
        [SerializeField] private bool foldCharacterTransition;
        [SerializeField] private bool foldBounce;
        [SerializeField] private bool foldBackground;
        [SerializeField] private bool foldStage;
        [SerializeField] private bool foldSpeakerFocus;
        [SerializeField] private bool foldUiFeedback;
        [SerializeField] private bool foldTiming;
        [SerializeField] private int expressionFromIndex;
        [SerializeField] private int expressionToIndex = 1;
        [SerializeField] private VnWorkshopPreviewScene backgroundSource = VnWorkshopPreviewScene.BusStopKeiko;
        [SerializeField] private VnWorkshopPreviewScene backgroundTarget = VnWorkshopPreviewScene.NightSkyKeiko;

        [NonSerialized] private VnWorkshopPreviewEffect previewEffect;
        [NonSerialized] private bool previewPlaying;
        [NonSerialized] private bool previewPaused;
        [NonSerialized] private double previewStartedAt;
        [NonSerialized] private double previewPausedAt;
        [NonSerialized] private float previewDuration;
        [NonSerialized] private float previewProgress;

        public string PreviewSampleText => previewSampleText ?? string.Empty;

        public static string[] GetPresentationSectionLabels()
        {
            return (string[])PresentationSections.Clone();
        }

        public VnWorkshopPreviewPlaybackSnapshot GetPreviewPlaybackSnapshot()
        {
            UpdatePreviewClock();
            return new VnWorkshopPreviewPlaybackSnapshot
            {
                Effect = previewEffect,
                IsPlaying = previewPlaying,
                IsPaused = previewPaused,
                NormalizedProgress = previewProgress,
                Duration = previewDuration
            };
        }

        public void SetTypographyPreviewValues(
            VnWorkshopFontPreset dialogueFontPreset,
            float dialogueFontSize,
            float dialogueCharacterSpacing,
            float dialogueLineSpacing,
            float dialogueParagraphSpacing,
            VnWorkshopTextAlignment dialogueAlignment,
            VnWorkshopFontPreset speakerFontPreset,
            float speakerFontSize,
            float speakerCharacterSpacing)
        {
            VnPresentationWorkshopVn10Resolver.SetTypographyPreviewOverrides(
                CurrentPreset,
                dialogueFontPreset,
                dialogueFontSize,
                dialogueCharacterSpacing,
                dialogueLineSpacing,
                dialogueParagraphSpacing,
                dialogueAlignment,
                speakerFontPreset,
                speakerFontSize,
                speakerCharacterSpacing);
            Repaint();
        }

        public void SetPreviewSampleText(string value)
        {
            previewSampleText = value ?? string.Empty;
            previewEffect = VnWorkshopPreviewEffect.None;
            previewPlaying = false;
            previewPaused = false;
            previewProgress = 0f;
            Repaint();
        }

        public void LoadPreviewSampleTextFromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Preview TXT path is required.", nameof(path));
            previewSampleText = File.ReadAllText(path, Encoding.UTF8);
            previewEffect = VnWorkshopPreviewEffect.None;
            previewPlaying = false;
            previewPaused = false;
            previewProgress = 0f;
            Repaint();
        }

        public void ClearPreviewSampleText()
        {
            SetPreviewSampleText(string.Empty);
        }

        public void ResetPreviewSampleText()
        {
            SetPreviewSampleText(DefaultPreviewSampleText);
        }

        public void PreviewTypewriter()
        {
            VnWorkshopTypewriterValues values = VnPresentationWorkshopVn10Resolver.ResolveTypewriter(CurrentPreset);
            float duration = VnPresentationWorkshopVn10Resolver.CalculateTypewriterDuration(PreviewSampleText, values);
            StartPreviewEffect(VnWorkshopPreviewEffect.Typewriter, Mathf.Max(.01f, duration));
        }

        public void PreviewExpression()
        {
            VnWorkshopExpressionTransitionValues values = VnPresentationWorkshopVn10Resolver.ResolveExpressionTransition(CurrentPreset);
            StartPreviewEffect(VnWorkshopPreviewEffect.Expression, Mathf.Max(.01f, values.Duration));
        }

        public void PreviewCharacterEnter()
        {
            VnWorkshopCharacterTransitionValues values = VnPresentationWorkshopVn10Resolver.ResolveCharacterTransition(CurrentPreset);
            StartPreviewEffect(VnWorkshopPreviewEffect.CharacterEnter, Mathf.Max(.01f, values.Duration));
        }

        public void PreviewCharacterExit()
        {
            VnWorkshopCharacterTransitionValues values = VnPresentationWorkshopVn10Resolver.ResolveCharacterTransition(CurrentPreset);
            StartPreviewEffect(VnWorkshopPreviewEffect.CharacterExit, Mathf.Max(.01f, values.Duration));
        }

        public void PreviewBounce()
        {
            VnWorkshopActionBounceValues values = VnPresentationWorkshopVn10Resolver.ResolveActionBounce(CurrentPreset);
            StartPreviewEffect(VnWorkshopPreviewEffect.Bounce, Mathf.Max(.01f, values.Duration));
        }

        public void PreviewBackgroundTransition()
        {
            VnWorkshopBackgroundTransitionValues values = VnPresentationWorkshopVn10Resolver.ResolveBackgroundTransition(CurrentPreset);
            StartPreviewEffect(VnWorkshopPreviewEffect.BackgroundTransition, Mathf.Max(.01f, values.Duration));
        }

        public void PreviewSpeakerSwitch()
        {
            VnWorkshopSpeakerFocusValues values = VnPresentationWorkshopVn10Resolver.ResolveSpeakerFocus(CurrentPreset);
            StartPreviewEffect(VnWorkshopPreviewEffect.SpeakerSwitch, Mathf.Max(.01f, values.TransitionDuration));
        }

        public void PreviewButtonPress()
        {
            VnWorkshopUiFeedbackValues values = VnPresentationWorkshopVn10Resolver.ResolveUiFeedback(CurrentPreset);
            StartPreviewEffect(VnWorkshopPreviewEffect.UiPress, Mathf.Max(.01f, values.Duration));
        }

        public void PausePreview()
        {
            UpdatePreviewClock();
            if (!previewPlaying) return;
            previewPaused = !previewPaused;
            if (previewPaused)
            {
                previewPausedAt = EditorApplication.timeSinceStartup;
            }
            else
            {
                previewStartedAt += EditorApplication.timeSinceStartup - previewPausedAt;
            }
            Repaint();
        }

        public void RestartPreview()
        {
            if (previewEffect == VnWorkshopPreviewEffect.None)
            {
                PreviewTypewriter();
                return;
            }
            previewStartedAt = EditorApplication.timeSinceStartup;
            previewPausedAt = 0d;
            previewProgress = 0f;
            previewPlaying = true;
            previewPaused = false;
            Repaint();
        }

        public void CompletePreviewLine()
        {
            previewEffect = VnWorkshopPreviewEffect.Typewriter;
            previewProgress = 1f;
            previewPlaying = false;
            previewPaused = false;
            Repaint();
        }

        internal string GetPreviewDialogueText()
        {
            UpdatePreviewClock();
            if (previewEffect != VnWorkshopPreviewEffect.Typewriter || previewProgress >= 1f)
                return PreviewSampleText;

            VnWorkshopTypewriterValues values = VnPresentationWorkshopVn10Resolver.ResolveTypewriter(
                comparisonView == VnWorkshopComparisonView.Original ? new VnPresentationWorkshopPreset() : CurrentPreset);
            float total = VnPresentationWorkshopVn10Resolver.CalculateTypewriterDuration(PreviewSampleText, values);
            int visible = VnPresentationWorkshopVn10Resolver.CalculateTypewriterVisibleCharacters(
                PreviewSampleText, total * previewProgress, values);
            return PreviewSampleText.Substring(0, Mathf.Clamp(visible, 0, PreviewSampleText.Length));
        }

        public string GetPreviewTextGlyphWarning()
        {
            if (string.IsNullOrEmpty(PreviewSampleText)) return string.Empty;
            try
            {
                VnPresentationWorkshopPreset active = comparisonView == VnWorkshopComparisonView.Original
                    ? new VnPresentationWorkshopPreset()
                    : CurrentPreset;
                VnWorkshopTypographyValues typography = VnPresentationWorkshopVn10Resolver.ResolveTypography(active);
                Rokas.Presentation.RokasAssets assets = VnPresentationWorkshopPreviewRenderer.LoadAssets();
                Font dialogueFont = typography.DialogueFontPreset == VnWorkshopFontPreset.ProjectSerif ? assets.serif : assets.sans;
                Font speakerFont = typography.SpeakerFontPreset == VnWorkshopFontPreset.ProjectSerif ? assets.serif : assets.sans;
                char[] missing = PreviewSampleText
                    .Where(c => !char.IsControl(c) && (!dialogueFont.HasCharacter(c) || !speakerFont.HasCharacter(c)))
                    .Distinct()
                    .Take(12)
                    .ToArray();
                return missing.Length == 0
                    ? string.Empty
                    : "Selected project font is missing glyphs: " + new string(missing) + ". Choose another supported preset.";
            }
            catch (Exception exception)
            {
                return "Could not validate preview glyph coverage: " + exception.Message;
            }
        }

        private void OnEnable()
        {
            EditorApplication.update -= OnWorkshopEditorUpdate;
            EditorApplication.update += OnWorkshopEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnWorkshopEditorUpdate;
        }

        private void OnWorkshopEditorUpdate()
        {
            if (!previewPlaying || previewPaused) return;
            float before = previewProgress;
            UpdatePreviewClock();
            if (!Mathf.Approximately(before, previewProgress)) Repaint();
        }

        private void StartPreviewEffect(VnWorkshopPreviewEffect effect, float duration)
        {
            previewEffect = effect;
            previewDuration = Mathf.Max(.01f, duration);
            previewProgress = 0f;
            previewStartedAt = EditorApplication.timeSinceStartup;
            previewPausedAt = 0d;
            previewPlaying = true;
            previewPaused = false;
            Repaint();
        }

        private void StartStagePreview(VnWorkshopPreviewEffect effect)
        {
            VnWorkshopStageLayoutValues values = VnPresentationWorkshopVn10Resolver.ResolveStageLayout(CurrentPreset);
            StartPreviewEffect(effect, Mathf.Max(.01f, values.RepositionDuration));
        }

        private void UpdatePreviewClock()
        {
            if (!previewPlaying || previewPaused || previewDuration <= 0f) return;
            previewProgress = Mathf.Clamp01((float)((EditorApplication.timeSinceStartup - previewStartedAt) / previewDuration));
            if (previewProgress >= 1f) previewPlaying = false;
        }

        private void DrawPresentationMotionInspector()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("VN10 Presentation / Motion", EditorStyles.boldLabel);
            VnWorkshopPreviewPlaybackSnapshot playback = GetPreviewPlaybackSnapshot();
            EditorGUILayout.LabelField("Preview", playback.Effect + "  " + Mathf.RoundToInt(playback.NormalizedProgress * 100f) + "%", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(playback.IsPaused ? "Resume" : "Pause")) PausePreview();
            if (GUILayout.Button("Restart")) RestartPreview();
            EditorGUILayout.EndHorizontal();

            foldTypography = EditorGUILayout.Foldout(foldTypography, "1. Typography", true);
            if (foldTypography) DrawVn10TypographySection();
            foldPreviewText = EditorGUILayout.Foldout(foldPreviewText, "2. Preview Text", true);
            if (foldPreviewText) DrawVn10PreviewTextSection();
            foldTextReveal = EditorGUILayout.Foldout(foldTextReveal, "3. Text Reveal", true);
            if (foldTextReveal) DrawVn10TypewriterSection();
            foldExpression = EditorGUILayout.Foldout(foldExpression, "4. Character Expression", true);
            if (foldExpression) DrawVn10ExpressionSection();
            foldCharacterTransition = EditorGUILayout.Foldout(foldCharacterTransition, "5. Character Enter / Exit", true);
            if (foldCharacterTransition) DrawVn10CharacterTransitionSection();
            foldBounce = EditorGUILayout.Foldout(foldBounce, "6. Action Bounce", true);
            if (foldBounce) DrawVn10BounceSection();
            foldBackground = EditorGUILayout.Foldout(foldBackground, "7. Background Transition", true);
            if (foldBackground) DrawVn10BackgroundSection();
            foldStage = EditorGUILayout.Foldout(foldStage, "8. Stage Layout", true);
            if (foldStage) DrawVn10StageSection();
            foldSpeakerFocus = EditorGUILayout.Foldout(foldSpeakerFocus, "9. Speaker Focus", true);
            if (foldSpeakerFocus) DrawVn10SpeakerFocusSection();
            foldUiFeedback = EditorGUILayout.Foldout(foldUiFeedback, "10. UI Feedback", true);
            if (foldUiFeedback) DrawVn10UiFeedbackSection();
            foldTiming = EditorGUILayout.Foldout(foldTiming, "11. Timing / Pacing", true);
            if (foldTiming) DrawVn10TimingSection();
        }

        private void DrawVn10TypographySection()
        {
            VnWorkshopTypographyValues v = VnPresentationWorkshopVn10Resolver.ResolveTypography(CurrentPreset);
            EditorGUI.BeginChangeCheck();
            VnWorkshopFontPreset dFont = (VnWorkshopFontPreset)EditorGUILayout.EnumPopup("Dialogue Font Preset", v.DialogueFontPreset);
            float dSize = EditorGUILayout.FloatField("Dialogue Font Size", v.DialogueFontSize);
            float dChar = EditorGUILayout.FloatField("Dialogue Character Spacing", v.DialogueCharacterSpacing);
            float dLine = EditorGUILayout.FloatField("Dialogue Line Spacing", v.DialogueLineSpacing);
            float dParagraph = EditorGUILayout.FloatField("Dialogue Paragraph Spacing", v.DialogueParagraphSpacing);
            VnWorkshopTextAlignment align = (VnWorkshopTextAlignment)EditorGUILayout.EnumPopup("Dialogue Alignment", v.DialogueAlignment);
            VnWorkshopFontPreset sFont = (VnWorkshopFontPreset)EditorGUILayout.EnumPopup("Speaker Font Preset", v.SpeakerFontPreset);
            float sSize = EditorGUILayout.FloatField("Speaker Font Size", v.SpeakerFontSize);
            float sChar = EditorGUILayout.FloatField("Speaker Character Spacing", v.SpeakerCharacterSpacing);
            EditorGUILayout.LabelField("Bounds / padding", "Use Dialogue Text / Speaker Name layout controls above", EditorStyles.miniLabel);
            if (EditorGUI.EndChangeCheck())
                SetTypographyPreviewValues(dFont, dSize, dChar, dLine, dParagraph, align, sFont, sSize, sChar);
        }

        private void DrawVn10PreviewTextSection()
        {
            EditorGUI.BeginChangeCheck();
            string next = EditorGUILayout.TextArea(PreviewSampleText, GUILayout.MinHeight(72f));
            if (EditorGUI.EndChangeCheck()) SetPreviewSampleText(next);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load TXT"))
            {
                string path = EditorUtility.OpenFilePanel("Load VN Workshop Preview Text", string.Empty, "txt");
                if (!string.IsNullOrEmpty(path)) LoadPreviewSampleTextFromFile(path);
            }
            if (GUILayout.Button("Clear")) ClearPreviewSampleText();
            if (GUILayout.Button("Reset Sample")) ResetPreviewSampleText();
            EditorGUILayout.EndHorizontal();
            string glyphWarning = GetPreviewTextGlyphWarning();
            if (!string.IsNullOrEmpty(glyphWarning)) EditorGUILayout.HelpBox(glyphWarning, MessageType.Warning);
            EditorGUILayout.HelpBox("Preview sample only. It never writes Yarn, story, quests or production dialogue.", MessageType.Info);
        }

        private void DrawVn10TypewriterSection()
        {
            VnWorkshopTypewriterValues v = VnPresentationWorkshopVn10Resolver.ResolveTypewriter(CurrentPreset);
            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.Toggle("Enabled", v.Enabled);
            float cps = EditorGUILayout.FloatField("Characters Per Second", v.CharactersPerSecond);
            float baseDelay = EditorGUILayout.FloatField("Base Character Delay", v.BaseCharacterDelay);
            float comma = EditorGUILayout.FloatField("Comma Pause", v.CommaPause);
            float period = EditorGUILayout.FloatField("Period Pause", v.PeriodPause);
            float ellipsis = EditorGUILayout.FloatField("Ellipsis Pause", v.EllipsisPause);
            float question = EditorGUILayout.FloatField("Question Pause", v.QuestionPause);
            float exclamation = EditorGUILayout.FloatField("Exclamation Pause", v.ExclamationPause);
            float lineStart = EditorGUILayout.FloatField("Line Start Delay", v.LineStartDelay);
            if (EditorGUI.EndChangeCheck())
                VnPresentationWorkshopVn10Resolver.SetTypewriterPreviewOverrides(CurrentPreset, enabled, cps, baseDelay, comma, period, ellipsis, question, exclamation, lineStart);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Preview Typewriter")) PreviewTypewriter();
            if (GUILayout.Button("Complete Line")) CompletePreviewLine();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawVn10ExpressionSection()
        {
            expressionFromIndex = EditorGUILayout.Popup("Expression From", Mathf.Clamp(expressionFromIndex, 0, MinaExpressionIds.Length - 1), MinaExpressionIds);
            expressionToIndex = EditorGUILayout.Popup("Expression To", Mathf.Clamp(expressionToIndex, 0, MinaExpressionIds.Length - 1), MinaExpressionIds);
            VnWorkshopExpressionTransitionValues v = VnPresentationWorkshopVn10Resolver.ResolveExpressionTransition(CurrentPreset);
            EditorGUI.BeginChangeCheck();
            float duration = EditorGUILayout.FloatField("Transition Duration", v.Duration);
            VnWorkshopEasing easing = (VnWorkshopEasing)EditorGUILayout.EnumPopup("Easing", v.Easing);
            if (EditorGUI.EndChangeCheck())
                VnPresentationWorkshopVn10Resolver.SetExpressionTransitionPreviewOverrides(CurrentPreset, duration, easing);
            if (GUILayout.Button("Preview Expression")) PreviewExpression();
            EditorGUILayout.LabelField("Authored states only", MinaExpressionIds[expressionFromIndex] + " → " + MinaExpressionIds[expressionToIndex], EditorStyles.miniLabel);
        }

        private void DrawVn10CharacterTransitionSection()
        {
            VnWorkshopCharacterTransitionValues v = VnPresentationWorkshopVn10Resolver.ResolveCharacterTransition(CurrentPreset);
            EditorGUI.BeginChangeCheck();
            VnWorkshopCharacterTransitionMode mode = (VnWorkshopCharacterTransitionMode)EditorGUILayout.EnumPopup("Mode", v.Mode);
            float duration = EditorGUILayout.FloatField("Duration", v.Duration);
            float fade = EditorGUILayout.FloatField("Fade Duration", v.FadeDuration);
            float slide = EditorGUILayout.FloatField("Slide Distance", v.SlideDistance);
            VnWorkshopSlideDirection direction = (VnWorkshopSlideDirection)EditorGUILayout.EnumPopup("Direction", v.SlideDirection);
            VnWorkshopEasing easing = (VnWorkshopEasing)EditorGUILayout.EnumPopup("Easing", v.Easing);
            if (EditorGUI.EndChangeCheck())
                VnPresentationWorkshopVn10Resolver.SetCharacterTransitionPreviewOverrides(CurrentPreset, mode, duration, fade, slide, direction, easing);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Preview Enter")) PreviewCharacterEnter();
            if (GUILayout.Button("Preview Exit")) PreviewCharacterExit();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawVn10BounceSection()
        {
            VnWorkshopActionBounceValues v = VnPresentationWorkshopVn10Resolver.ResolveActionBounce(CurrentPreset);
            EditorGUI.BeginChangeCheck();
            float amplitude = EditorGUILayout.FloatField("Amplitude", v.Amplitude);
            float duration = EditorGUILayout.FloatField("Duration", v.Duration);
            float scale = EditorGUILayout.FloatField("Scale Emphasis", v.ScaleEmphasis);
            float overshoot = EditorGUILayout.FloatField("Overshoot", v.Overshoot);
            VnWorkshopEasing easing = (VnWorkshopEasing)EditorGUILayout.EnumPopup("Easing", v.Easing);
            if (EditorGUI.EndChangeCheck())
                VnPresentationWorkshopVn10Resolver.SetActionBouncePreviewOverrides(CurrentPreset, amplitude, duration, scale, overshoot, easing);
            if (GUILayout.Button("Preview Bounce")) PreviewBounce();
            EditorGUILayout.LabelField("Event-only; idle returns to exact baseline.", EditorStyles.miniLabel);
        }

        private void DrawVn10BackgroundSection()
        {
            VnWorkshopBackgroundTransitionValues v = VnPresentationWorkshopVn10Resolver.ResolveBackgroundTransition(CurrentPreset);
            backgroundSource = (VnWorkshopPreviewScene)EditorGUILayout.EnumPopup("Source Background", backgroundSource);
            backgroundTarget = (VnWorkshopPreviewScene)EditorGUILayout.EnumPopup("Target Background", backgroundTarget);
            EditorGUI.BeginChangeCheck();
            VnWorkshopBackgroundTransitionMode mode = (VnWorkshopBackgroundTransitionMode)EditorGUILayout.EnumPopup("Mode", v.Mode);
            float duration = EditorGUILayout.FloatField("Duration", v.Duration);
            float darkness = EditorGUILayout.Slider("Curtain Darkness", v.CurtainDarkness, 0f, 1f);
            VnWorkshopCurtainDirection direction = (VnWorkshopCurtainDirection)EditorGUILayout.EnumPopup("Curtain Direction", v.Direction);
            VnWorkshopEasing easing = (VnWorkshopEasing)EditorGUILayout.EnumPopup("Easing", v.Easing);
            if (EditorGUI.EndChangeCheck())
                VnPresentationWorkshopVn10Resolver.SetBackgroundTransitionPreviewOverrides(CurrentPreset, mode, duration, darkness, direction, easing);
            if (GUILayout.Button("Preview Background Transition")) PreviewBackgroundTransition();
        }

        private void DrawVn10StageSection()
        {
            VnWorkshopStageLayoutValues v = VnPresentationWorkshopVn10Resolver.ResolveStageLayout(CurrentPreset);
            EditorGUI.BeginChangeCheck();
            float left = EditorGUILayout.FloatField("Left X", v.LeftX);
            float center = EditorGUILayout.FloatField("Center X", v.CenterX);
            float right = EditorGUILayout.FloatField("Right X", v.RightX);
            float y = EditorGUILayout.FloatField("Slot Y", v.SlotY);
            float leftScale = EditorGUILayout.FloatField("Left Scale", v.LeftScale);
            float centerScale = EditorGUILayout.FloatField("Center Scale", v.CenterScale);
            float rightScale = EditorGUILayout.FloatField("Right Scale", v.RightScale);
            float spacing = EditorGUILayout.FloatField("Spacing", v.Spacing);
            float duration = EditorGUILayout.FloatField("Reposition Duration", v.RepositionDuration);
            VnWorkshopEasing easing = (VnWorkshopEasing)EditorGUILayout.EnumPopup("Easing", v.Easing);
            if (EditorGUI.EndChangeCheck())
                VnPresentationWorkshopVn10Resolver.SetStageLayoutPreviewOverrides(CurrentPreset, left, center, right, y, leftScale, centerScale, rightScale, spacing, duration, easing);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("1 Character")) { previewEffect = VnWorkshopPreviewEffect.None; previewPlaying = false; previewProgress = 0f; }
            if (GUILayout.Button("1 → 2")) StartStagePreview(VnWorkshopPreviewEffect.StageOneTwo);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("2 → 3")) StartStagePreview(VnWorkshopPreviewEffect.StageTwoThree);
            if (GUILayout.Button("3 → 2")) StartStagePreview(VnWorkshopPreviewEffect.StageThreeTwo);
            if (GUILayout.Button("2 → 1")) StartStagePreview(VnWorkshopPreviewEffect.StageTwoOne);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawVn10SpeakerFocusSection()
        {
            VnWorkshopSpeakerFocusValues v = VnPresentationWorkshopVn10Resolver.ResolveSpeakerFocus(CurrentPreset);
            EditorGUI.BeginChangeCheck();
            float activeScale = EditorGUILayout.FloatField("Active Scale", v.ActiveScale);
            float activeBrightness = EditorGUILayout.FloatField("Active Brightness", v.ActiveBrightness);
            float forward = EditorGUILayout.FloatField("Forward Offset", v.ActiveForwardOffset);
            float inactiveScale = EditorGUILayout.FloatField("Inactive Scale", v.InactiveScale);
            float inactiveBrightness = EditorGUILayout.Slider("Inactive Brightness", v.InactiveBrightness, 0f, 1.5f);
            float inactiveAlpha = EditorGUILayout.Slider("Inactive Alpha", v.InactiveAlpha, 0f, 1f);
            float duration = EditorGUILayout.FloatField("Transition Duration", v.TransitionDuration);
            VnWorkshopEasing easing = (VnWorkshopEasing)EditorGUILayout.EnumPopup("Easing", v.Easing);
            if (EditorGUI.EndChangeCheck())
                VnPresentationWorkshopVn10Resolver.SetSpeakerFocusPreviewOverrides(CurrentPreset, activeScale, activeBrightness, forward, inactiveScale, inactiveBrightness, inactiveAlpha, duration, easing);
            if (GUILayout.Button("Preview Speaker Switch")) PreviewSpeakerSwitch();
            EditorGUILayout.LabelField("Solo character remains stable (no pulse/bob/scale pumping).", EditorStyles.miniLabel);
        }

        private void DrawVn10UiFeedbackSection()
        {
            VnWorkshopUiFeedbackValues v = VnPresentationWorkshopVn10Resolver.ResolveUiFeedback(CurrentPreset);
            EditorGUI.BeginChangeCheck();
            float hoverScale = EditorGUILayout.FloatField("Hover Scale", v.HoverScale);
            float pressedScale = EditorGUILayout.FloatField("Pressed Scale", v.PressedScale);
            Vector2 pressedOffset = EditorGUILayout.Vector2Field("Pressed Offset", v.PressedOffset);
            float duration = EditorGUILayout.FloatField("Duration", v.Duration);
            VnWorkshopEasing easing = (VnWorkshopEasing)EditorGUILayout.EnumPopup("Easing", v.Easing);
            float hoverBrightness = EditorGUILayout.FloatField("Hover Brightness", v.HoverBrightness);
            float pressedBrightness = EditorGUILayout.FloatField("Pressed Brightness", v.PressedBrightness);
            float hoverAlpha = EditorGUILayout.Slider("Hover Alpha", v.HoverAlpha, 0f, 1f);
            float pressedAlpha = EditorGUILayout.Slider("Pressed Alpha", v.PressedAlpha, 0f, 1f);
            float hoverOverlay = EditorGUILayout.Slider("Hover Overlay", v.HoverOverlayHighlight, 0f, 1f);
            float pressedOverlay = EditorGUILayout.Slider("Pressed Overlay", v.PressedOverlayHighlight, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
                VnPresentationWorkshopVn10Resolver.SetUiFeedbackPreviewOverrides(CurrentPreset, hoverScale, pressedScale, pressedOffset, duration, easing, hoverBrightness, pressedBrightness, hoverAlpha, pressedAlpha, hoverOverlay, pressedOverlay);
            if (GUILayout.Button("Preview Button Press")) PreviewButtonPress();
            EditorGUILayout.HelpBox("Back / Next use independent transform. Mute / Pause / Skip artwork is baked into the panel; only overlay / brightness / alpha feedback is previewed.", MessageType.Info);
        }

        private void DrawVn10TimingSection()
        {
            VnWorkshopTimingValues v = VnPresentationWorkshopVn10Resolver.ResolveTiming(CurrentPreset);
            EditorGUI.BeginChangeCheck();
            float beat = EditorGUILayout.FloatField("Beat Settle Duration", v.MinimumBeatSettleDuration);
            float breathing = EditorGUILayout.FloatField("Post-transition Breathing Room", v.PostTransitionBreathingRoom);
            float sequenceGap = EditorGUILayout.FloatField("Auto Preview Sequence Gap", v.AutoPreviewSequenceGap);
            if (EditorGUI.EndChangeCheck())
                VnPresentationWorkshopVn10Resolver.SetTimingPreviewOverrides(CurrentPreset, beat, breathing, sequenceGap);
            EditorGUILayout.LabelField("Presentation timing only — no automatic story/Yarn progression.", EditorStyles.miniLabel);
        }
    }
}
