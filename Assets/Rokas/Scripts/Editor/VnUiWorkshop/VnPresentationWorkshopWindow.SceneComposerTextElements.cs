using System;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed partial class VnPresentationWorkshopWindow
    {
        [SerializeField] private string _sceneComposerSelectedTextId = string.Empty;
        [SerializeField] private bool _sceneComposerSpeakerTypographyExpanded;
        [SerializeField] private bool _sceneComposerDialogueTypographyExpanded;

        public VnWorkshopTypographyValues ComposerGetSharedTypography()
        {
            return VnPresentationWorkshopVn10Resolver.ResolveTypography(GetSharedDialoguePresentation());
        }

        // Legacy combined APIs stay source-compatible. Normal authoring no longer uses them.
        public void ComposerSetSharedSpeakerTypography(
            string fontAssetGuid, float fontSize, Color color,
            VnWorkshopTextAlignment alignment, Vector2 position, Vector2 size)
        {
            ValidateTypographyStyle(fontAssetGuid, fontSize, color, alignment);
            ValidateTextGeometry(position, size);
            RecordSceneComposerUndo("Edit Shared VN Speaker Typography");
            CancelSceneComposerPreviewDrag();
            SetDefaultStyle(true, fontAssetGuid, fontSize, color, alignment);
            ApplySharedTextRect(GetSharedDialoguePresentation().speakerName,
                VnWorkshopElement.SpeakerName, position, size);
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        public void ComposerSetSharedDialogueTypography(
            string fontAssetGuid, float fontSize, Color color,
            VnWorkshopTextAlignment alignment, Vector2 position, Vector2 size)
        {
            ValidateTypographyStyle(fontAssetGuid, fontSize, color, alignment);
            ValidateTextGeometry(position, size);
            RecordSceneComposerUndo("Edit Shared VN Dialogue Typography");
            CancelSceneComposerPreviewDrag();
            SetDefaultStyle(false, fontAssetGuid, fontSize, color, alignment);
            ApplySharedTextRect(GetSharedDialoguePresentation().dialogueText,
                VnWorkshopElement.DialogueText, position, size);
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        public void ComposerSetSharedSpeakerStyle(
            string fontAssetGuid, float fontSize, Color defaultColor,
            VnWorkshopTextAlignment alignment)
        {
            ValidateTypographyStyle(fontAssetGuid, fontSize, defaultColor, alignment);
            EnsureSceneComposerProject();
            RecordSceneComposerUndo("Edit Shared VN Speaker Style");
            CancelSceneComposerPreviewDrag();
            SetDefaultStyle(true, fontAssetGuid, fontSize, defaultColor, alignment);
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        public void ComposerSetSelectedSceneSpeakerColorScope(
            VnSceneComposerSpeakerColorScope scope)
        {
            if (!Enum.IsDefined(typeof(VnSceneComposerSpeakerColorScope), scope))
                throw new ArgumentOutOfRangeException(nameof(scope));

            VnSceneComposerScene scene = RequireSelectedScene();
            if (scene.speakerColorScope == scope) return;

            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            if (scope == VnSceneComposerSpeakerColorScope.ThisSpeakerInScene &&
                string.IsNullOrEmpty(VnSceneComposerTextStyleResolver.ResolveSpeakerKey(beat)))
                throw new InvalidOperationException(
                    "Speaker-in-Scene color requires a non-empty current speaker.");

            RecordSceneComposerUndo("Change VN Speaker Color Scope");
            CancelSceneComposerPreviewDrag();

            if (scope == VnSceneComposerSpeakerColorScope.ThisScene)
            {
                if (!scene.hasSpeakerColorOverride)
                {
                    scene.speakerColor = ComposerGetSharedTypography().SpeakerColor;
                    scene.hasSpeakerColorOverride = true;
                }
            }
            else if (scope == VnSceneComposerSpeakerColorScope.AllScenes)
            {
                // Returning the Scene default to Global does NOT destroy explicit
                // Scene+Speaker overrides. Those remain deliberate local entries.
                scene.hasSpeakerColorOverride = false;
                scene.speakerColor = Color.white;
            }
            else
            {
                // Selecting the third scope seeds only the current speaker when needed.
                // It does not create or change the Scene default.
                if (!VnSceneComposerTextStyleResolver.HasSpeakerColorOverride(scene, beat))
                {
                    Color inherited =
                        VnSceneComposerTextStyleResolver.Resolve(
                            _sceneComposerProject, scene, beat).SpeakerColor;
                    VnSceneComposerTextStyleResolver.SetSpeakerColorOverride(
                        scene, beat, inherited);
                }
            }

            scene.speakerColorScope = scope;
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        public VnSceneComposerSpeakerColorScope ComposerGetSelectedSceneSpeakerColorScope()
        {
            return RequireSelectedScene().speakerColorScope;
        }

        public void ComposerSetSpeakerColor(Color color)
        {
            if (!IsFinite(color.r) || !IsFinite(color.g) ||
                !IsFinite(color.b) || !IsFinite(color.a))
                throw new ArgumentException("Speaker color must be finite.", nameof(color));

            VnSceneComposerScene scene = RequireSelectedScene();
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            string undoLabel = scene.speakerColorScope == VnSceneComposerSpeakerColorScope.ThisSpeakerInScene
                ? "Edit VN Speaker-in-Scene Color"
                : scene.speakerColorScope == VnSceneComposerSpeakerColorScope.ThisScene
                    ? "Edit Scene VN Speaker Color"
                    : "Edit Shared VN Speaker Color";
            RecordSceneComposerUndo(undoLabel);
            CancelSceneComposerPreviewDrag();

            if (scene.speakerColorScope == VnSceneComposerSpeakerColorScope.ThisSpeakerInScene)
            {
                Color localSpeaker = color;
                localSpeaker.a = ComposerGetSharedTypography().SpeakerColor.a;
                VnSceneComposerTextStyleResolver.SetSpeakerColorOverride(
                    scene, beat, localSpeaker);
            }
            else if (scene.speakerColorScope == VnSceneComposerSpeakerColorScope.ThisScene)
            {
                Color localScene = color;
                localScene.a = ComposerGetSharedTypography().SpeakerColor.a;
                scene.hasSpeakerColorOverride = true;
                scene.speakerColor = localScene;
            }
            else
            {
                VnWorkshopTypographyValues shared = ComposerGetSharedTypography();
                Color global = color;
                global.a = shared.SpeakerColor.a;
                SetDefaultStyle(
                    true,
                    shared.SpeakerFontAssetGuid,
                    shared.SpeakerFontSize,
                    global,
                    shared.SpeakerAlignment);
            }

            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        public bool ComposerHasCurrentSceneSpeakerPaletteEntry()
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            string key = VnSceneComposerTextStyleResolver.ResolveSpeakerKey(scene, beat);
            return key.Length > 0 &&
                   VnSceneComposerTextStyleResolver.FindSpeakerColorOverride(scene, key) != null;
        }

        public string ComposerGetCurrentSceneSpeakerPaletteKey()
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            return VnSceneComposerTextStyleResolver.ResolveSpeakerKey(
                scene, ComposerGetSelectedDialogueBeat());
        }

        public void ComposerSetCurrentSceneSpeakerNameColor(Color color)
        {
            ValidatePaletteColor(color, "Speaker-name color");
            VnSceneComposerScene scene = RequireSelectedScene();
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            if (VnSceneComposerTextStyleResolver.ResolveSpeakerKey(scene, beat).Length == 0)
                throw new InvalidOperationException(
                    "Speaker palette color requires a non-empty current speaker.");

            RecordSceneComposerUndo("Edit Scene Speaker Palette Name Color");
            CancelSceneComposerPreviewDrag();
            VnSceneComposerTextStyleResolver.SetSpeakerPaletteNameColor(
                _sceneComposerProject, scene, beat, color);
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        public void ComposerSetCurrentSceneSpeakerDialogueColor(Color color)
        {
            ValidatePaletteColor(color, "Dialogue-body color");
            VnSceneComposerScene scene = RequireSelectedScene();
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            if (VnSceneComposerTextStyleResolver.ResolveSpeakerKey(scene, beat).Length == 0)
                throw new InvalidOperationException(
                    "Speaker palette color requires a non-empty current speaker.");

            RecordSceneComposerUndo("Edit Scene Speaker Palette Dialogue Color");
            CancelSceneComposerPreviewDrag();
            VnSceneComposerTextStyleResolver.SetSpeakerPaletteDialogueBodyColor(
                _sceneComposerProject, scene, beat, color);
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        public bool ComposerHasCurrentSceneSpeakerDialogueColorOverride()
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            string key = VnSceneComposerTextStyleResolver.ResolveSpeakerKey(scene, beat);
            VnSceneComposerSpeakerColorOverride entry =
                VnSceneComposerTextStyleResolver.FindSpeakerColorOverride(scene, key);
            return entry != null && entry.hasDialogueBodyColor;
        }

        public void ComposerClearCurrentSceneSpeakerDialogueColorOverride()
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            if (!ComposerHasCurrentSceneSpeakerDialogueColorOverride())
                return;

            RecordSceneComposerUndo("Reset Scene Speaker Palette Dialogue Color");
            CancelSceneComposerPreviewDrag();
            VnSceneComposerTextStyleResolver.ClearSpeakerPaletteDialogueBodyColor(
                scene, beat);
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        public void ComposerClearCurrentSceneSpeakerPaletteEntry()
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            if (!VnSceneComposerTextStyleResolver.HasSpeakerColorOverride(scene, beat))
                return;

            RecordSceneComposerUndo("Reset Scene Speaker Palette");
            CancelSceneComposerPreviewDrag();
            VnSceneComposerTextStyleResolver.RemoveSpeakerPaletteEntry(scene, beat);
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        private static void ValidatePaletteColor(Color color, string label)
        {
            if (!IsFinite(color.r) || !IsFinite(color.g) ||
                !IsFinite(color.b) || !IsFinite(color.a))
                throw new ArgumentException(label + " must be finite.", nameof(color));
        }

        public void ComposerClearSelectedSceneSpeakerColorOverride()
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            if (!scene.hasSpeakerColorOverride &&
                scene.speakerColorScope != VnSceneComposerSpeakerColorScope.ThisScene)
                return;

            RecordSceneComposerUndo("Restore Shared VN Speaker Color");
            CancelSceneComposerPreviewDrag();
            scene.hasSpeakerColorOverride = false;
            scene.speakerColor = Color.white;
            if (scene.speakerColorScope == VnSceneComposerSpeakerColorScope.ThisScene)
                scene.speakerColorScope = VnSceneComposerSpeakerColorScope.AllScenes;
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        public void ComposerClearSelectedSpeakerColorOverride()
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            if (!VnSceneComposerTextStyleResolver.HasSpeakerColorOverride(scene, beat))
                return;

            RecordSceneComposerUndo("Restore VN Speaker-in-Scene Color");
            CancelSceneComposerPreviewDrag();
            VnSceneComposerTextStyleResolver.RemoveSpeakerColorOverride(scene, beat);
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        public void ComposerSetSelectedSceneDialogueBodyStyle(
            string fontAssetGuid, float fontSize, Color color,
            VnWorkshopTextAlignment alignment)
        {
            ValidateTypographyStyle(fontAssetGuid, fontSize, color, alignment);
            VnSceneComposerScene scene = RequireSelectedScene();
            RecordSceneComposerUndo("Edit VN Scene Dialogue Style");
            CancelSceneComposerPreviewDrag();
            if (scene.dialogueBodyStyleOverride == null)
                scene.dialogueBodyStyleOverride = new VnSceneComposerTextVisualStyleOverride();
            VnSceneComposerTextStyleResolver.SetStyle(
                scene.dialogueBodyStyleOverride, fontAssetGuid, fontSize, color, alignment);
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        public void ComposerClearSelectedSceneDialogueBodyStyle()
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            if (scene.dialogueBodyStyleOverride == null ||
                !scene.dialogueBodyStyleOverride.HasAnyOverride) return;
            RecordSceneComposerUndo("Restore VN Scene Dialogue Style");
            CancelSceneComposerPreviewDrag();
            scene.dialogueBodyStyleOverride.Clear();
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        public void ComposerSetSharedTextGeometry(bool speaker, Vector2 position, Vector2 size)
        {
            ValidateTextGeometry(position, size);
            EnsureSceneComposerProject();
            RecordSceneComposerUndo(speaker
                ? "Move Shared VN Speaker Text"
                : "Move Shared VN Dialogue Text");
            VnPresentationWorkshopPreset preset = GetSharedDialoguePresentation();
            ApplySharedTextRect(
                speaker ? preset.speakerName : preset.dialogueText,
                speaker ? VnWorkshopElement.SpeakerName : VnWorkshopElement.DialogueText,
                position, size);
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        public void ComposerSetSelectedSceneTextGeometryScope(
            VnSceneComposerTextGeometryScope scope)
        {
            if (!Enum.IsDefined(typeof(VnSceneComposerTextGeometryScope), scope))
                throw new ArgumentOutOfRangeException(nameof(scope));

            VnSceneComposerScene scene = RequireSelectedScene();
            if (scene.textGeometryScope == scope) return;

            RecordSceneComposerUndo("Change VN Text Geometry Scope");
            CancelSceneComposerPreviewDrag();
            if (scene.presentationOverrides == null)
                scene.presentationOverrides = new VnPresentationWorkshopPreset();

            if (scope == VnSceneComposerTextGeometryScope.ThisScene)
            {
                VnPresentationWorkshopPreset shared = GetSharedDialoguePresentation();
                SnapshotGeometryAsExplicitLocal(
                    scene.presentationOverrides.speakerName, shared.speakerName);
                SnapshotGeometryAsExplicitLocal(
                    scene.presentationOverrides.dialogueText, shared.dialogueText);
            }
            else
            {
                scene.presentationOverrides.ResetElement(VnWorkshopElement.SpeakerName);
                scene.presentationOverrides.ResetElement(VnWorkshopElement.DialogueText);
            }

            scene.textGeometryScope = scope;
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        public VnSceneComposerTextGeometryScope ComposerGetSelectedSceneTextGeometryScope()
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            return scene.textGeometryScope;
        }

        public void ComposerSetTextGeometry(
            bool speaker, Vector2 position, Vector2 size)
        {
            ValidateTextGeometry(position, size);
            VnSceneComposerScene scene = RequireSelectedScene();
            VnWorkshopElement element =
                speaker ? VnWorkshopElement.SpeakerName : VnWorkshopElement.DialogueText;

            RecordSceneComposerUndo(
                scene.textGeometryScope == VnSceneComposerTextGeometryScope.ThisScene
                    ? (speaker ? "Move Scene VN Speaker Text" : "Move Scene VN Dialogue Text")
                    : (speaker ? "Move Shared VN Speaker Text" : "Move Shared VN Dialogue Text"));

            VnPresentationWorkshopPreset preset =
                GetComposerElementGeometryPresentation(element);
            ApplySharedTextRect(
                speaker ? preset.speakerName : preset.dialogueText,
                element, position, size);
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        private static void SnapshotGeometryAsExplicitLocal(
            VnWorkshopElementOverride target, VnWorkshopElementOverride source)
        {
            if (target == null || source == null) return;
            target.hasPositionDelta = true;
            target.positionDelta = source.hasPositionDelta ? source.positionDelta : Vector2.zero;
            target.hasSizeDelta = true;
            target.sizeDelta = source.hasSizeDelta ? source.sizeDelta : Vector2.zero;
            target.hasScaleMultiplier = true;
            target.scaleMultiplier = source.hasScaleMultiplier ? source.scaleMultiplier : 1f;
        }

        public string ComposerGetSharedTypographyWarning()
        {
            return ResolveTypographyWarnings(ComposerGetSharedTypography());
        }

        private string ComposerGetScopedTypographyWarning(VnSceneComposerScene scene)
        {
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            if (beat == null && scene != null && scene.dialogueBeats != null && scene.dialogueBeats.Count > 0)
                beat = scene.dialogueBeats[0];
            if (scene == null || beat == null) return ComposerGetSharedTypographyWarning();
            return ResolveTypographyWarnings(
                VnSceneComposerTextStyleResolver.Resolve(_sceneComposerProject, scene, beat));
        }

        private static string ResolveTypographyWarnings(VnWorkshopTypographyValues typography)
        {
            string dialogue = ResolveProjectFontWarning(typography.DialogueFontAssetGuid, "Текст реплики");
            string speaker = ResolveProjectFontWarning(typography.SpeakerFontAssetGuid, "Текст говорящего");
            if (string.IsNullOrEmpty(dialogue)) return speaker;
            if (string.IsNullOrEmpty(speaker)) return dialogue;
            return dialogue + "\n" + speaker;
        }

        private VnPresentationWorkshopPreset GetSharedDialoguePresentation()
        {
            EnsureSceneComposerProject();
            if (_sceneComposerProject.defaultPresentation == null)
                _sceneComposerProject.defaultPresentation = new VnPresentationWorkshopPreset();
            return _sceneComposerProject.defaultPresentation;
        }

        private static void ApplySharedTextRect(
            VnWorkshopElementOverride target, VnWorkshopElement element,
            Vector2 position, Vector2 size)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            Rect baseline = VnPresentationWorkshopPreviewRenderer.GetReferenceTextRect(element);
            Rect authored = new Rect(position, size);
            Vector2 positionDelta = authored.center - baseline.center;
            Vector2 sizeDelta = authored.size - baseline.size;
            target.hasPositionDelta = positionDelta != Vector2.zero;
            target.positionDelta = positionDelta;
            target.hasSizeDelta = sizeDelta != Vector2.zero;
            target.sizeDelta = sizeDelta;
            target.hasScaleMultiplier = false;
            target.scaleMultiplier = 1f;
        }

        private static void ValidateTypographyStyle(
            string fontAssetGuid, float fontSize, Color color, VnWorkshopTextAlignment alignment)
        {
            if (!Enum.IsDefined(typeof(VnWorkshopTextAlignment), alignment))
                throw new ArgumentOutOfRangeException(nameof(alignment));
            if (!IsFinite(fontSize) || fontSize < 8f || fontSize > 160f)
                throw new ArgumentOutOfRangeException(nameof(fontSize));
            if (!IsFinite(color.r) || !IsFinite(color.g) ||
                !IsFinite(color.b) || !IsFinite(color.a))
                throw new ArgumentException("Dialogue typography color must be finite.");
            if (!string.IsNullOrWhiteSpace(fontAssetGuid))
            {
                UnityEngine.Object asset = VnSceneComposerTextFontResolver.ResolveAsset(fontAssetGuid);
                if (asset != null && !VnSceneComposerTextFontResolver.IsSupportedAsset(asset))
                    throw new ArgumentException("Dialogue typography font reference is not a supported project font.");
            }
        }

        private static void ValidateTextGeometry(Vector2 position, Vector2 size)
        {
            if (!IsFinite(position.x) || !IsFinite(position.y) ||
                !IsFinite(size.x) || !IsFinite(size.y))
                throw new ArgumentException("Dialogue typography layout must be finite.");
            if (size.x < 1f || size.y < 1f)
                throw new ArgumentOutOfRangeException(nameof(size),
                    "Dialogue typography Width and Height must be at least 1.");
        }

        private static void ValidateSharedTypography(
            string fontAssetGuid, float fontSize, Color color,
            VnWorkshopTextAlignment alignment, Vector2 position, Vector2 size)
        {
            ValidateTypographyStyle(fontAssetGuid, fontSize, color, alignment);
            ValidateTextGeometry(position, size);
        }

        private void SetDefaultStyle(
            bool speaker, string guid, float fontSize, Color color, VnWorkshopTextAlignment alignment)
        {
            VnPresentationWorkshopPreset preset = GetSharedDialoguePresentation();
            VnWorkshopTypographyValues baseline =
                VnPresentationWorkshopVn10Resolver.ResolveTypography(new VnPresentationWorkshopPreset());
            if (preset.typography == null) preset.typography = new VnWorkshopTypographyOverride();
            if (speaker)
            {
                preset.typography.hasSpeakerFontAssetGuid = !string.IsNullOrWhiteSpace(guid);
                preset.typography.speakerFontAssetGuid = guid ?? string.Empty;
                preset.typography.hasSpeakerFontSize = !Mathf.Approximately(fontSize, baseline.SpeakerFontSize);
                preset.typography.speakerFontSize = fontSize;
                preset.typography.hasSpeakerColor = !Approximately(color, baseline.SpeakerColor);
                preset.typography.speakerColor = color;
                preset.typography.hasSpeakerAlignment = alignment != baseline.SpeakerAlignment;
                preset.typography.speakerAlignment = alignment;
            }
            else
            {
                preset.typography.hasDialogueFontAssetGuid = !string.IsNullOrWhiteSpace(guid);
                preset.typography.dialogueFontAssetGuid = guid ?? string.Empty;
                preset.typography.hasDialogueFontSize = !Mathf.Approximately(fontSize, baseline.DialogueFontSize);
                preset.typography.dialogueFontSize = fontSize;
                preset.typography.hasDialogueColor = !Approximately(color, baseline.DialogueColor);
                preset.typography.dialogueColor = color;
                preset.typography.hasDialogueAlignment = alignment != baseline.DialogueAlignment;
                preset.typography.dialogueAlignment = alignment;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Approximately(a.r, b.r) && Mathf.Approximately(a.g, b.g) &&
                   Mathf.Approximately(a.b, b.b) && Mathf.Approximately(a.a, b.a);
        }

        private static string ResolveProjectFontWarning(string guid, string label)
        {
            if (string.IsNullOrWhiteSpace(guid)) return string.Empty;
            if (VnSceneComposerTextFontResolver.TryResolvePreviewFont(guid, out Font _, out string warning))
                return warning;
            return label + ": " + (string.IsNullOrEmpty(warning)
                ? "font asset не найден; будет использован RokasSans." : warning);
        }

        private void DrawSceneComposerDialogueTypographyInspector(VnSceneComposerScene scene)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Оформление диалога", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "Шрифт, размер, выравнивание и прозрачность остаются общими. " +
                "Цвет имени и цвет реплики для именованного говорящего автоматически принадлежат " +
                "его профилю в текущей Scene.",
                MessageType.Info);

            DrawCurrentSceneSpeakerPaletteControls(scene);

            EditorGUILayout.LabelField("Применить расположение:", EditorStyles.miniBoldLabel);
            int geometryScope = GUILayout.Toolbar(
                scene.textGeometryScope == VnSceneComposerTextGeometryScope.ThisScene ? 1 : 0,
                new[] { "Ко всем сценам", "Только к этой сцене" });
            VnSceneComposerTextGeometryScope nextGeometryScope = geometryScope == 1
                ? VnSceneComposerTextGeometryScope.ThisScene
                : VnSceneComposerTextGeometryScope.AllScenes;
            if (nextGeometryScope != scene.textGeometryScope)
                ComposerSetSelectedSceneTextGeometryScope(nextGeometryScope);

            EditorGUILayout.LabelField("Плашка диалога:", "Общее для всех сцен");
            EditorGUILayout.HelpBox(
                "Расположение Speaker/Dialogue следует выбранному scope. " +
                "Физическая геометрия плашки всегда общая для всего проекта; PNG плашки может отличаться по Scene.",
                MessageType.None);

            if (GUILayout.Button("Изменить текст говорящего", GUILayout.Height(25f)))
                _sceneComposerSpeakerTypographyExpanded = !_sceneComposerSpeakerTypographyExpanded;
            if (_sceneComposerSpeakerTypographyExpanded)
            {
                EditorGUI.indentLevel++;
                DrawScopedTypographyControls(true, scene);
                EditorGUI.indentLevel--;
            }

            if (GUILayout.Button("Изменить текст реплики", GUILayout.Height(25f)))
                _sceneComposerDialogueTypographyExpanded = !_sceneComposerDialogueTypographyExpanded;
            if (_sceneComposerDialogueTypographyExpanded)
            {
                EditorGUI.indentLevel++;
                DrawScopedTypographyControls(false, scene);
                EditorGUI.indentLevel--;
            }

            string warning = ComposerGetScopedTypographyWarning(scene);
            if (!string.IsNullOrEmpty(warning)) EditorGUILayout.HelpBox(warning, MessageType.Warning);
            string layoutWarning = ComposerGetDialogueOverflowWarning(scene);
            if (!string.IsNullOrEmpty(layoutWarning)) EditorGUILayout.HelpBox(layoutWarning, MessageType.Warning);
        }

        private void DrawScopedTypographyControls(bool speaker, VnSceneComposerScene scene)
        {
            if (speaker)
            {
                DrawSpeakerTypographyControls(scene);
                return;
            }

            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            if (beat == null && scene.dialogueBeats != null && scene.dialogueBeats.Count > 0)
                beat = scene.dialogueBeats[0];

            VnWorkshopTypographyValues values =
                VnSceneComposerTextStyleResolver.Resolve(_sceneComposerProject, scene, null);
            bool hasScoped =
                scene.dialogueBodyStyleOverride != null &&
                scene.dialogueBodyStyleOverride.HasAnyOverride;

            bool nextScoped = EditorGUILayout.Toggle("Свой стиль этой сцены", hasScoped);
            if (nextScoped != hasScoped)
            {
                if (nextScoped)
                {
                    ComposerSetSelectedSceneDialogueBodyStyle(
                        values.DialogueFontAssetGuid,
                        values.DialogueFontSize,
                        values.DialogueColor,
                        values.DialogueAlignment);
                }
                else
                {
                    ComposerClearSelectedSceneDialogueBodyStyle();
                }
                values = VnSceneComposerTextStyleResolver.Resolve(
                    _sceneComposerProject, scene, null);
            }

            string guid = values.DialogueFontAssetGuid;
            float fontSize = values.DialogueFontSize;
            Color color = values.DialogueColor;
            VnWorkshopTextAlignment alignment = values.DialogueAlignment;

            UnityEngine.Object currentFont =
                VnSceneComposerTextFontResolver.ResolveAsset(guid);
            EditorGUILayout.LabelField(
                "Текущий шрифт",
                currentFont != null ? currentFont.name : "RokasSans (по умолчанию)");

            VnSceneComposerInstalledFontFace[] faces =
                VnSceneComposerTextFontResolver.GetInstalledWindowsFonts();
            string[] options = new string[faces.Length + 1];
            options[0] = faces.Length == 0
                ? "Установленные Windows-шрифты не найдены"
                : "Выбрать установленный Windows-шрифт…";
            for (int i = 0; i < faces.Length; i++) options[i + 1] = faces[i].DisplayName;

            using (new EditorGUI.DisabledScope(faces.Length == 0))
            {
                int selected = EditorGUILayout.Popup("Шрифт Windows", 0, options);
                if (selected > 0)
                {
                    CancelSceneComposerPreviewDrag();
                    VnSceneComposerFontImportResult imported =
                        VnSceneComposerTextFontResolver.ImportWindowsFont(faces[selected - 1]);
                    if (!imported.Success)
                    {
                        SetSceneComposerStatus(imported.Error, MessageType.Error);
                    }
                    else
                    {
                        guid = imported.TmpFontAssetGuid;
                        ComposerSetSelectedSceneDialogueBodyStyle(
                            guid, fontSize, color, alignment);
                        SetSceneComposerStatus(
                            "Шрифт импортирован в проект и связан с TMP: " +
                            imported.TmpFontAssetPath, MessageType.Info);
                    }
                }
            }

            if (GUILayout.Button("RokasSans по умолчанию"))
            {
                CancelSceneComposerPreviewDrag();
                guid = string.Empty;
                ComposerSetSelectedSceneDialogueBodyStyle(
                    guid, fontSize, color, alignment);
            }

            EditorGUI.BeginChangeCheck();
            float nextFontSize =
                EditorGUILayout.Slider("Размер", fontSize, 8f, 160f);
            Color nextColor = EditorGUILayout.ColorField("Цвет реплики по умолчанию", color);
            float nextOpacity =
                EditorGUILayout.Slider("Прозрачность", color.a, 0f, 1f);
            nextColor.a = nextOpacity;
            VnWorkshopTextAlignment nextAlignment =
                (VnWorkshopTextAlignment)EditorGUILayout.EnumPopup(
                    "Выравнивание", alignment);
            if (EditorGUI.EndChangeCheck())
            {
                CancelSceneComposerPreviewDrag();
                ComposerSetSelectedSceneDialogueBodyStyle(
                    guid, nextFontSize, nextColor, nextAlignment);
            }

            DrawSharedFallbackPicker(guid);
            DrawTextGeometryControls(false, scene);

            EditorGUILayout.HelpBox(
                "X / Y / Width / Height хранятся в одном reference-canvas. " +
                "Длина реплики, фон, видео, персонаж и плашка не переписывают координаты.",
                MessageType.None);
            EditorGUILayout.HelpBox(
                "Windows используется только как источник при выборе. После импорта сохраняется GUID " +
                "TMP Font Asset внутри Assets; абсолютный путь Windows в данных сцены не сохраняется.",
                MessageType.None);
        }

        private void DrawSpeakerTypographyControls(VnSceneComposerScene scene)
        {
            VnWorkshopTypographyValues shared = ComposerGetSharedTypography();

            string guid = shared.SpeakerFontAssetGuid;
            float fontSize = shared.SpeakerFontSize;
            Color globalColor = shared.SpeakerColor;
            VnWorkshopTextAlignment alignment = shared.SpeakerAlignment;

            EditorGUILayout.LabelField("Общий стиль имени", EditorStyles.miniBoldLabel);
            UnityEngine.Object currentFont =
                VnSceneComposerTextFontResolver.ResolveAsset(guid);
            EditorGUILayout.LabelField(
                "Текущий шрифт",
                currentFont != null ? currentFont.name : "RokasSans (по умолчанию)");

            VnSceneComposerInstalledFontFace[] faces =
                VnSceneComposerTextFontResolver.GetInstalledWindowsFonts();
            string[] options = new string[faces.Length + 1];
            options[0] = faces.Length == 0
                ? "Установленные Windows-шрифты не найдены"
                : "Выбрать установленный Windows-шрифт…";
            for (int i = 0; i < faces.Length; i++) options[i + 1] = faces[i].DisplayName;

            using (new EditorGUI.DisabledScope(faces.Length == 0))
            {
                int selected = EditorGUILayout.Popup("Шрифт Windows", 0, options);
                if (selected > 0)
                {
                    CancelSceneComposerPreviewDrag();
                    VnSceneComposerFontImportResult imported =
                        VnSceneComposerTextFontResolver.ImportWindowsFont(faces[selected - 1]);
                    if (!imported.Success)
                    {
                        SetSceneComposerStatus(imported.Error, MessageType.Error);
                    }
                    else
                    {
                        guid = imported.TmpFontAssetGuid;
                        ComposerSetSharedSpeakerStyle(
                            guid, fontSize, globalColor, alignment);
                        SetSceneComposerStatus(
                            "Общий шрифт говорящего импортирован в TMP: " +
                            imported.TmpFontAssetPath, MessageType.Info);
                    }
                }
            }

            if (GUILayout.Button("RokasSans по умолчанию"))
            {
                CancelSceneComposerPreviewDrag();
                guid = string.Empty;
                ComposerSetSharedSpeakerStyle(
                    guid, fontSize, globalColor, alignment);
            }

            // Font, size, alignment and opacity are always shared/global.
            EditorGUI.BeginChangeCheck();
            float nextFontSize =
                EditorGUILayout.Slider("Размер", fontSize, 8f, 160f);
            float nextOpacity =
                EditorGUILayout.Slider("Общая прозрачность", globalColor.a, 0f, 1f);
            VnWorkshopTextAlignment nextAlignment =
                (VnWorkshopTextAlignment)EditorGUILayout.EnumPopup(
                    "Выравнивание", alignment);
            if (EditorGUI.EndChangeCheck())
            {
                CancelSceneComposerPreviewDrag();
                Color nextGlobal = globalColor;
                nextGlobal.a = nextOpacity;
                ComposerSetSharedSpeakerStyle(
                    guid, nextFontSize, nextGlobal, nextAlignment);
                fontSize = nextFontSize;
                globalColor = nextGlobal;
                alignment = nextAlignment;
            }

            DrawSharedFallbackPicker(guid);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Цвет имени по умолчанию", EditorStyles.miniBoldLabel);
            EditorGUI.BeginChangeCheck();
            Color nextDefaultSpeakerColor =
                EditorGUILayout.ColorField("Общий цвет имени", globalColor);
            if (EditorGUI.EndChangeCheck())
            {
                CancelSceneComposerPreviewDrag();
                nextDefaultSpeakerColor.a = globalColor.a;
                ComposerSetSharedSpeakerStyle(
                    guid, fontSize, nextDefaultSpeakerColor, alignment);
                globalColor = nextDefaultSpeakerColor;
            }

            DrawTextGeometryControls(true, scene);
            EditorGUILayout.HelpBox(
                "Цвет имени имеет отдельный scope. " +
                "Шрифт, размер, выравнивание, fallback и прозрачность остаются общими. " +
                "Изменение цвета не меняет X / Y / Width / Height и text geometry scope.",
                MessageType.None);
        }

        private void DrawCurrentSceneSpeakerPaletteControls(VnSceneComposerScene scene)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                "Цвета говорящего в этой сцене", EditorStyles.miniBoldLabel);

            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            string speakerKey =
                VnSceneComposerTextStyleResolver.ResolveSpeakerKey(scene, beat);
            string displaySpeaker = beat != null && !beat.narration
                ? (beat.speaker ?? string.Empty).Trim()
                : string.Empty;
            if (displaySpeaker.Length == 0 && beat != null && !beat.narration)
                displaySpeaker = (beat.targetCharacterId ?? string.Empty).Trim();

            EditorGUILayout.LabelField(
                "Текущий говорящий:",
                displaySpeaker.Length > 0 ? displaySpeaker : "Без говорящего");

            if (speakerKey.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "Для текста без говорящего используется существующий Scene/Global fallback. " +
                    "Пустой профиль говорящего не создаётся.",
                    MessageType.None);
                return;
            }

            VnWorkshopTypographyValues values =
                VnSceneComposerTextStyleResolver.Resolve(
                    _sceneComposerProject, scene, beat);
            bool hasProfile =
                VnSceneComposerTextStyleResolver.FindSpeakerColorOverride(
                    scene, speakerKey) != null;

            EditorGUI.BeginChangeCheck();
            Color nextName = EditorGUILayout.ColorField(
                "Цвет имени", values.SpeakerColor);
            if (EditorGUI.EndChangeCheck())
            {
                nextName.a = values.SpeakerColor.a;
                ComposerSetCurrentSceneSpeakerNameColor(nextName);
                values = VnSceneComposerTextStyleResolver.Resolve(
                    _sceneComposerProject, scene, beat);
                hasProfile = true;
            }

            EditorGUI.BeginChangeCheck();
            Color nextBody = EditorGUILayout.ColorField(
                "Цвет реплики", values.DialogueColor);
            if (EditorGUI.EndChangeCheck())
            {
                nextBody.a = values.DialogueColor.a;
                ComposerSetCurrentSceneSpeakerDialogueColor(nextBody);
                values = VnSceneComposerTextStyleResolver.Resolve(
                    _sceneComposerProject, scene, beat);
                hasProfile = true;
            }

            EditorGUILayout.LabelField(
                "Источник",
                hasProfile
                    ? "Профиль говорящего в этой сцене"
                    : "Наследуется из настроек сцены / проекта");

            using (new EditorGUI.DisabledScope(!hasProfile))
            {
                if (GUILayout.Button("Сбросить цвета говорящего"))
                    ComposerClearCurrentSceneSpeakerPaletteEntry();
            }
        }

        private void DrawTextGeometryControls(bool speaker, VnSceneComposerScene scene)
        {
            Rect rect = ResolveEffectiveTextRect(speaker, scene);
            EditorGUI.BeginChangeCheck();
            float x = EditorGUILayout.FloatField("X", rect.x);
            float y = EditorGUILayout.FloatField("Y", rect.y);
            float width = EditorGUILayout.FloatField("Width", rect.width);
            float height = EditorGUILayout.FloatField("Height", rect.height);
            if (EditorGUI.EndChangeCheck())
                ComposerSetTextGeometry(
                    speaker, new Vector2(x, y), new Vector2(width, height));

            EditorGUILayout.LabelField(
                "Scope",
                scene.textGeometryScope == VnSceneComposerTextGeometryScope.ThisScene
                    ? "Только к этой сцене"
                    : "Ко всем сценам");
        }

        private Rect ResolveEffectiveTextRect(bool speaker, VnSceneComposerScene scene)
        {
            VnPresentationWorkshopPreset preset =
                VnSceneComposerComposition.ResolvePresentation(
                    _sceneComposerProject, scene);
            VnWorkshopPreviewFrame frame =
                VnPresentationWorkshopPreviewRenderer.BuildFrame(
                    preset, VnWorkshopResolution.Reference1920x1080,
                    VnWorkshopPreviewScene.BusStopKeiko);
            return speaker ? frame.SpeakerName : frame.DialogueText;
        }

        private string ComposerGetDialogueOverflowWarning(VnSceneComposerScene scene)
        {
            if (scene == null) return string.Empty;
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            if (beat == null && scene.dialogueBeats != null && scene.dialogueBeats.Count > 0)
                beat = scene.dialogueBeats[0];
            if (beat == null) return string.Empty;
            VnWorkshopPreviewFrame frame = VnSceneComposerComposition.BuildFrame(
                _sceneComposerProject, scene, beat,
                VnWorkshopResolution.Reference1920x1080, Texture2D.blackTexture);
            float requiredHeight = VnPresentationWorkshopPreviewRenderer.MeasureWrappedTextHeight(
                frame.DialogueText, frame.Dialogue, frame.DialogueFont,
                Mathf.RoundToInt(frame.Typography.DialogueFontSize), FontStyle.Normal,
                frame.Typography.DialogueAlignment);
            if (requiredHeight <= frame.DialogueText.height + .5f) return string.Empty;
            return "Текст переносится внутри заданного Width, но по высоте требует примерно " +
                Mathf.CeilToInt(requiredHeight) + " px при доступных " +
                Mathf.CeilToInt(frame.DialogueText.height) + " px.";
        }

        private void DrawSharedFallbackPicker(string primaryGuid)
        {
            VnSceneComposerInstalledFontFace[] faces = VnSceneComposerTextFontResolver.GetInstalledWindowsFonts();
            if (faces.Length == 0) return;
            string[] options = new string[faces.Length + 1];
            options[0] = "Добавить TMP fallback (например, CJK)…";
            for (int i = 0; i < faces.Length; i++) options[i + 1] = faces[i].DisplayName;
            int selected = EditorGUILayout.Popup("Fallback", 0, options);
            if (selected <= 0) return;
            CancelSceneComposerPreviewDrag();
            VnSceneComposerFontImportResult imported =
                VnSceneComposerTextFontResolver.ImportWindowsFont(faces[selected - 1]);
            if (!imported.Success)
            {
                SetSceneComposerStatus(imported.Error, MessageType.Error);
                return;
            }
            string primary = string.IsNullOrWhiteSpace(primaryGuid)
                ? VnSceneComposerTextFontResolver.GetDefaultFontAssetGuid() : primaryGuid;
            if (!VnSceneComposerTextFontResolver.ConfigureFallback(
                    primary, imported.TmpFontAssetGuid, out string error))
            {
                SetSceneComposerStatus(error, MessageType.Error);
                return;
            }
            SetSceneComposerStatus("TMP fallback добавлен: " + faces[selected - 1].DisplayName, MessageType.Info);
        }
    }
}
