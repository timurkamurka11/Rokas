using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed partial class VnPresentationWorkshopWindow
    {
        private const float ComposerDurationMaximum = 10f;
        [SerializeField] private bool _sceneComposerPresentationProjectDefaults;

        public static string[] ComposerGetPresentationCapabilityIds()
        {
            return new[]
            {
                "preview-modes", "element-layout", "character-layout", "variants", "portable-preset", "profiles",
                "typography", "preview-text", "typewriter", "expression", "character-enter-exit", "bounce",
                "background-transition", "stage-layout", "speaker-focus", "ui-feedback", "timing", "resolution-preview",
                "dialogue-panel-visual"
            };
        }

        public void ComposerSetPresentationScope(bool projectDefaults)
        {
            EnsureSceneComposerProject();
            if (!projectDefaults) RequireSelectedScene();
            _sceneComposerPresentationProjectDefaults = projectDefaults;
            Repaint();
        }

        public VnPresentationWorkshopPreset ComposerGetActivePresentationPreset()
        {
            EnsureSceneComposerProject();
            if (_sceneComposerPresentationProjectDefaults)
            {
                if (_sceneComposerProject.defaultPresentation == null)
                    _sceneComposerProject.defaultPresentation = new VnPresentationWorkshopPreset();
                return _sceneComposerProject.defaultPresentation;
            }

            VnSceneComposerScene scene = RequireSelectedScene();
            if (scene.presentationOverrides == null)
                scene.presentationOverrides = new VnPresentationWorkshopPreset();
            return scene.presentationOverrides;
        }

        public void ComposerSetDialoguePanelVisualAsset(Texture2D texture)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            string assetPath = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("Dialogue panel image must be a Unity project asset.", nameof(texture));
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrWhiteSpace(guid))
                throw new InvalidOperationException("Dialogue panel image does not have a stable Unity asset GUID.");

            RecordSceneComposerUndo("Choose VN Dialogue Panel Visual");
            VnPresentationWorkshopPreset preset = ComposerGetActivePresentationPreset();
            if (preset.dialoguePanelVisual == null)
                preset.dialoguePanelVisual = new VnWorkshopDialoguePanelVisualOverride();
            preset.dialoguePanelVisual.hasAssetGuid = true;
            preset.dialoguePanelVisual.assetGuid = guid;
            MarkSceneComposerChanged();
        }

        public void ComposerResetDialoguePanelVisual()
        {
            VnPresentationWorkshopPreset preset = ComposerGetActivePresentationPreset();
            if (preset.dialoguePanelVisual == null || !preset.dialoguePanelVisual.HasAnyOverride) return;

            RecordSceneComposerUndo("Reset VN Dialogue Panel Visual");
            preset.dialoguePanelVisual.Clear();
            MarkSceneComposerChanged();
        }

        public Texture2D ComposerGetActiveDialoguePanelVisualAsset()
        {
            return ResolveDialoguePanelVisualAsset(ComposerGetActivePresentationPreset());
        }

        public Texture2D ComposerGetEffectiveDialoguePanelVisualAsset(VnSceneComposerScene scene)
        {
            if (scene == null) return null;
            return ResolveDialoguePanelVisualAsset(VnSceneComposerComposition.ResolvePresentation(_sceneComposerProject, scene));
        }

        public string ComposerGetEffectiveDialoguePanelVisualWarning(VnSceneComposerScene scene)
        {
            if (scene == null) return string.Empty;
            VnPresentationWorkshopPreset preset = VnSceneComposerComposition.ResolvePresentation(_sceneComposerProject, scene);
            if (preset == null || preset.dialoguePanelVisual == null || !preset.dialoguePanelVisual.hasAssetGuid)
                return string.Empty;

            string guid = (preset.dialoguePanelVisual.assetGuid ?? string.Empty).Trim();
            if (guid.Length == 0)
                return "Выбранная плашка диалога не содержит корректной ссылки. Будет использована плашка по умолчанию.";
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath) || AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath) == null)
                return "Файл выбранной плашки диалога не найден. Будет использована плашка по умолчанию.";
            return string.Empty;
        }

        public void ComposerSetExternalDialoguePanelPng(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                throw new ArgumentException("PNG file does not exist.", nameof(sourcePath));
            if (!string.Equals(Path.GetExtension(sourcePath), ".png", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Dialogue panel image must use PNG.", nameof(sourcePath));

            VnSceneComposerAssetOnboardResult result = VnSceneComposerAssetLibrary.Onboard(
                GetProjectRoot(), sourcePath, VnSceneComposerAssetPurpose.UiOverlay,
                Path.GetFileNameWithoutExtension(sourcePath), string.Empty, string.Empty);
            if (!result.Success || result.Entry == null)
                throw new InvalidOperationException(result.Error ?? "Could not add dialogue panel PNG.");

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(result.Entry.assetPath);
            if (texture == null)
                throw new InvalidOperationException("Onboarded dialogue panel PNG could not be loaded: " + result.Entry.assetPath);
            ComposerSetDialoguePanelVisualAsset(texture);
        }

        private static Texture2D ResolveDialoguePanelVisualAsset(VnPresentationWorkshopPreset preset)
        {
            if (preset == null || preset.dialoguePanelVisual == null || !preset.dialoguePanelVisual.hasAssetGuid)
                return null;
            string guid = (preset.dialoguePanelVisual.assetGuid ?? string.Empty).Trim();
            if (guid.Length == 0) return null;
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        public void ComposerSetElementLayout(VnWorkshopElement element, Vector2 positionDelta, Vector2 sizeDelta, float scaleMultiplier)
        {
            if (float.IsNaN(scaleMultiplier) || float.IsInfinity(scaleMultiplier) || scaleMultiplier < .05f || scaleMultiplier > 5f)
                throw new ArgumentOutOfRangeException(nameof(scaleMultiplier));
            MutateComposerElementPresentation("Edit VN Scene UI Layout", element, preset =>
            {
                VnWorkshopElementOverride target = preset.GetElementOverride(element);
                target.hasPositionDelta = positionDelta != Vector2.zero;
                target.positionDelta = positionDelta;
                target.hasSizeDelta = sizeDelta != Vector2.zero;
                target.sizeDelta = sizeDelta;
                target.hasScaleMultiplier = !Mathf.Approximately(scaleMultiplier, 1f);
                target.scaleMultiplier = scaleMultiplier;
            });
        }

        public void ComposerResetElement(VnWorkshopElement element)
        {
            MutateComposerElementPresentation("Reset VN Scene UI Element", element, preset => preset.ResetElement(element));
        }

        public void ComposerResetAllPresentation()
        {
            MutateComposerPresentation("Reset VN Scene Presentation", preset => preset.ResetAll());
        }

        public void ComposerSetCharacterTransform(int characterIndex, Vector2 positionOffset, float scaleMultiplier)
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            if (scene.characters == null || characterIndex < 0 || characterIndex >= scene.characters.Count || scene.characters[characterIndex] == null)
                throw new ArgumentOutOfRangeException(nameof(characterIndex));
            if (float.IsNaN(scaleMultiplier) || float.IsInfinity(scaleMultiplier) || scaleMultiplier < .05f || scaleMultiplier > 5f)
                throw new ArgumentOutOfRangeException(nameof(scaleMultiplier));

            RecordSceneComposerUndo("Edit VN Scene Character Transform");
            VnSceneComposerCharacter character = scene.characters[characterIndex];
            character.hasPositionOffset = positionOffset != Vector2.zero;
            character.positionOffset = positionOffset;
            character.hasScaleMultiplier = !Mathf.Approximately(scaleMultiplier, 1f);
            character.scaleMultiplier = scaleMultiplier;
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        public void ComposerResetCharacterTransform(int characterIndex)
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            if (scene.characters == null || characterIndex < 0 || characterIndex >= scene.characters.Count || scene.characters[characterIndex] == null)
                throw new ArgumentOutOfRangeException(nameof(characterIndex));
            RecordSceneComposerUndo("Reset VN Scene Character Transform");
            VnSceneComposerCharacter character = scene.characters[characterIndex];
            character.hasPositionOffset = false;
            character.positionOffset = Vector2.zero;
            character.hasScaleMultiplier = false;
            character.scaleMultiplier = 1f;
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        public void ComposerSetTypography(
            VnWorkshopFontPreset dialogueFontPreset, float dialogueFontSize, float dialogueCharacterSpacing,
            float dialogueLineSpacing, float dialogueParagraphSpacing, VnWorkshopTextAlignment dialogueAlignment,
            VnWorkshopFontPreset speakerFontPreset, float speakerFontSize, float speakerCharacterSpacing)
        {
            MutateComposerPresentation("Edit VN Scene Typography", preset =>
                VnPresentationWorkshopVn10Resolver.SetTypographyPreviewOverrides(
                    preset, dialogueFontPreset, dialogueFontSize, dialogueCharacterSpacing, dialogueLineSpacing,
                    dialogueParagraphSpacing, dialogueAlignment, speakerFontPreset, speakerFontSize, speakerCharacterSpacing));
        }

        public void ComposerSetTypewriter(bool enabled, float charactersPerSecond, float baseCharacterDelay,
            float commaPause, float periodPause, float ellipsisPause, float questionPause,
            float exclamationPause, float lineStartDelay)
        {
            MutateComposerPresentation("Edit VN Scene Typewriter", preset =>
                VnPresentationWorkshopVn10Resolver.SetTypewriterPreviewOverrides(
                    preset, enabled, charactersPerSecond, baseCharacterDelay, commaPause, periodPause,
                    ellipsisPause, questionPause, exclamationPause, lineStartDelay));
        }

        public void ComposerSetExpressionTransition(float duration, VnWorkshopEasing easing)
        {
            MutateComposerPresentation("Edit VN Scene Expression Transition", preset =>
            {
                float safeDuration = SanitizeComposerDuration(duration, preset.expressionTransition.duration, 0f);
                VnPresentationWorkshopVn10Resolver.SetExpressionTransitionPreviewOverrides(preset, safeDuration, easing);
            });
        }

        public void ComposerSetCharacterTransition(VnWorkshopCharacterTransitionMode mode, float duration,
            float fadeDuration, float slideDistance, VnWorkshopSlideDirection slideDirection, VnWorkshopEasing easing)
        {
            MutateComposerPresentation("Edit VN Scene Character Transition", preset =>
            {
                float safeDuration = SanitizeComposerDuration(duration, preset.characterTransition.duration, 0f);
                VnPresentationWorkshopVn10Resolver.SetCharacterTransitionPreviewOverrides(
                    preset, mode, safeDuration, fadeDuration, slideDistance, slideDirection, easing);
            });
        }

        public void ComposerSetBounce(float amplitude, float duration, float scaleEmphasis, float overshoot,
            VnWorkshopEasing easing)
        {
            MutateComposerPresentation("Edit VN Scene Bounce", preset =>
            {
                float safeDuration = SanitizeComposerDuration(duration, preset.actionBounce.duration, .01f);
                VnPresentationWorkshopVn10Resolver.SetActionBouncePreviewOverrides(
                    preset, amplitude, safeDuration, scaleEmphasis, overshoot, easing);
            });
        }

        public void ComposerSetBounceAmplitude(float amplitude)
        {
            VnWorkshopActionBounceValues current = ResolveComposerBounceForTargetedMutation();
            ComposerSetBounce(amplitude, current.Duration, current.ScaleEmphasis, current.Overshoot, current.Easing);
        }

        public void ComposerSetBounceDuration(float duration)
        {
            VnWorkshopActionBounceValues current = ResolveComposerBounceForTargetedMutation();
            ComposerSetBounce(current.Amplitude, duration, current.ScaleEmphasis, current.Overshoot, current.Easing);
        }

        private VnWorkshopActionBounceValues ResolveComposerBounceForTargetedMutation()
        {
            EnsureSceneComposerProject();
            VnPresentationWorkshopPreset effective = _sceneComposerPresentationProjectDefaults
                ? _sceneComposerProject.defaultPresentation ?? new VnPresentationWorkshopPreset()
                : VnSceneComposerComposition.ResolvePresentation(_sceneComposerProject, RequireSelectedScene());
            return VnPresentationWorkshopVn10Resolver.ResolveActionBounce(effective);
        }

        public void ComposerSetBackgroundTransition(VnWorkshopBackgroundTransitionMode mode, float duration,
            float curtainDarkness, VnWorkshopCurtainDirection direction, VnWorkshopEasing easing)
        {
            MutateComposerPresentation("Edit VN Scene Background Transition", preset =>
            {
                float safeDuration = SanitizeComposerDuration(duration, preset.backgroundTransition.duration, 0f);
                VnPresentationWorkshopVn10Resolver.SetBackgroundTransitionPreviewOverrides(
                    preset, mode, safeDuration, curtainDarkness, direction, easing);
            });
        }

        public void ComposerSetStageLayout(float leftX, float centerX, float rightX, float slotY,
            float leftScale, float centerScale, float rightScale, float spacing, float repositionDuration,
            VnWorkshopEasing easing)
        {
            MutateComposerPresentation("Edit VN Scene Stage Layout", preset =>
            {
                float safeDuration = SanitizeComposerDuration(repositionDuration, preset.stageLayout.repositionDuration, 0f);
                VnPresentationWorkshopVn10Resolver.SetStageLayoutPreviewOverrides(
                    preset, leftX, centerX, rightX, slotY, leftScale, centerScale, rightScale,
                    spacing, safeDuration, easing);
            });
        }

        public void ComposerSetSpeakerFocus(float activeScale, float activeBrightness, float activeForwardOffset,
            float inactiveScale, float inactiveBrightness, float inactiveAlpha, float transitionDuration,
            VnWorkshopEasing easing)
        {
            MutateComposerPresentation("Edit VN Scene Speaker Focus", preset =>
            {
                float safeDuration = SanitizeComposerDuration(transitionDuration, preset.focus.transitionDuration, 0f);
                VnPresentationWorkshopVn10Resolver.SetSpeakerFocusPreviewOverrides(
                    preset, activeScale, activeBrightness, activeForwardOffset, inactiveScale,
                    inactiveBrightness, inactiveAlpha, safeDuration, easing);
            });
        }

        public void ComposerSetUiFeedback(float hoverScale, float pressedScale, Vector2 pressedOffset,
            float duration, VnWorkshopEasing easing, float hoverBrightness, float pressedBrightness,
            float hoverAlpha, float pressedAlpha, float hoverOverlayHighlight, float pressedOverlayHighlight)
        {
            MutateComposerPresentation("Edit VN Scene UI Feedback", preset =>
                VnPresentationWorkshopVn10Resolver.SetUiFeedbackPreviewOverrides(
                    preset, hoverScale, pressedScale, pressedOffset, duration, easing, hoverBrightness,
                    pressedBrightness, hoverAlpha, pressedAlpha, hoverOverlayHighlight, pressedOverlayHighlight));
        }

        public void ComposerSetTiming(float minimumBeatSettleDuration, float postTransitionBreathingRoom,
            float autoPreviewSequenceGap)
        {
            MutateComposerPresentation("Edit VN Scene Timing", preset =>
                VnPresentationWorkshopVn10Resolver.SetTimingPreviewOverrides(
                    preset, minimumBeatSettleDuration, postTransitionBreathingRoom, autoPreviewSequenceGap));
        }

        public void ComposerApplyReferenceMotionProfile()
        {
            MutateComposerPresentation("Apply VN Reference Motion Profile",
                VnPresentationWorkshopVn10Profiles.ApplyReferenceMotionPreview);
        }

        public void ComposerSetPreviewResolution(VnWorkshopResolution resolution)
        {
            if (!Enum.IsDefined(typeof(VnWorkshopResolution), resolution))
                throw new ArgumentOutOfRangeException(nameof(resolution));
            RecordSceneComposerUndo("Change VN Scene Preview Resolution");
            previewResolution = resolution;
            MarkSceneComposerChanged();
        }

        public void ComposerSavePresentationPreset(string projectRoot, string name)
        {
            VnPresentationWorkshopPreset active = ComposerGetActivePresentationPreset();
            VnPresentationWorkshopStorage.SaveVariant(projectRoot, name, active);
            SetSceneComposerStatus("Saved presentation preset '" + VnPresentationWorkshopStorage.SanitizeVariantName(name) + "'.", MessageType.Info);
        }

        public VnWorkshopImportResult ComposerLoadPresentationPreset(string projectRoot, string name)
        {
            VnWorkshopImportResult result = VnPresentationWorkshopStorage.LoadVariant(projectRoot, name);
            if (result.Success && result.Document != null && result.Document.preset != null)
                ApplyComposerPresentationPreset(result.Document.preset, result.Document.previewSampleText, "Load VN Scene Presentation Preset");
            else
                SetSceneComposerStatus("Presentation preset load failed: " + result.Error, MessageType.Error);
            return result;
        }

        public string ComposerExportPresentationPresetJson(string name)
        {
            VnPresentationWorkshopPreset active = ComposerGetActivePresentationPreset();
            string sample = VnWorkshopPreviewSampleStore.Get(active);
            return VnPresentationWorkshopSerialization.Serialize(active, name ?? string.Empty, sample);
        }

        public VnWorkshopImportResult ComposerImportPresentationPresetJson(string json)
        {
            VnWorkshopImportResult result = VnPresentationWorkshopSerialization.Deserialize(json);
            if (result.Success && result.Document != null && result.Document.preset != null)
                ApplyComposerPresentationPreset(result.Document.preset, result.Document.previewSampleText, "Import VN Scene Presentation Preset");
            else
                SetSceneComposerStatus("Presentation preset import failed: " + result.Error, MessageType.Error);
            return result;
        }

        public string ComposerGetPreviewSampleText()
        {
            return VnWorkshopPreviewSampleStore.Get(ComposerGetActivePresentationPreset());
        }

        public void ComposerSetPreviewSampleText(string text)
        {
            RecordSceneComposerUndo("Edit VN Preview Sample Text");
            VnWorkshopPreviewSampleStore.Set(ComposerGetActivePresentationPreset(), text ?? string.Empty);
            MarkSceneComposerChanged();
        }

        public void ComposerResetPreviewSampleText()
        {
            ComposerSetPreviewSampleText(VnWorkshopPreviewSampleStore.DefaultText);
        }

        private void ApplyComposerPresentationPreset(VnPresentationWorkshopPreset imported, string previewSampleText, string undoLabel)
        {
            if (imported == null) throw new ArgumentNullException(nameof(imported));
            RecordSceneComposerUndo(undoLabel);
            VnPresentationWorkshopPreset replacement = JsonUtility.FromJson<VnPresentationWorkshopPreset>(JsonUtility.ToJson(imported));
            if (replacement == null) replacement = new VnPresentationWorkshopPreset();
            if (_sceneComposerPresentationProjectDefaults)
                _sceneComposerProject.defaultPresentation = replacement;
            else
                RequireSelectedScene().presentationOverrides = replacement;
            VnWorkshopPreviewSampleStore.Set(replacement, previewSampleText ?? VnWorkshopPreviewSampleStore.DefaultText);
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
            SetSceneComposerStatus("Presentation preset applied to " +
                (_sceneComposerPresentationProjectDefaults ? "Project Defaults." : "Scene Overrides."), MessageType.Info);
        }

        private static float SanitizeComposerDuration(float value, float previousValue, float minimum)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return previousValue;
            return Mathf.Clamp(value, minimum, ComposerDurationMaximum);
        }

        private void MutateComposerElementPresentation(
            string undoLabel, VnWorkshopElement element, Action<VnPresentationWorkshopPreset> mutation)
        {
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            RecordSceneComposerUndo(undoLabel);
            VnPresentationWorkshopPreset target =
                element == VnWorkshopElement.SpeakerName || element == VnWorkshopElement.DialogueText
                    ? GetSharedDialoguePresentation()
                    : ComposerGetActivePresentationPreset();
            mutation(target);
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        private void MutateComposerPresentation(string undoLabel, Action<VnPresentationWorkshopPreset> mutation)
        {
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            RecordSceneComposerUndo(undoLabel);
            mutation(ComposerGetActivePresentationPreset());
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }
    }
}
