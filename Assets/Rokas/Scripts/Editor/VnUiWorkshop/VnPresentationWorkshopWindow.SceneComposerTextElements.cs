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

        public void ComposerSetCharacterSpeakerStyle(
            string characterId, string fontAssetGuid, float fontSize, Color color,
            VnWorkshopTextAlignment alignment)
        {
            if (string.IsNullOrWhiteSpace(characterId))
                throw new ArgumentException("Stable character identity is required.", nameof(characterId));
            ValidateTypographyStyle(fontAssetGuid, fontSize, color, alignment);
            EnsureSceneComposerProject();
            RecordSceneComposerUndo("Edit VN Character Speaker Style");
            CancelSceneComposerPreviewDrag();
            if (_sceneComposerProject.speakerStyleOverrides == null)
                _sceneComposerProject.speakerStyleOverrides =
                    new System.Collections.Generic.List<VnSceneComposerSpeakerStyleOverride>();
            VnSceneComposerSpeakerStyleOverride entry =
                VnSceneComposerTextStyleResolver.FindSpeakerStyle(_sceneComposerProject, characterId);
            if (entry == null)
            {
                entry = new VnSceneComposerSpeakerStyleOverride { characterId = characterId.Trim() };
                _sceneComposerProject.speakerStyleOverrides.Add(entry);
            }
            if (entry.style == null) entry.style = new VnSceneComposerTextVisualStyleOverride();
            VnSceneComposerTextStyleResolver.SetStyle(
                entry.style, fontAssetGuid, fontSize, color, alignment);
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        public void ComposerClearCharacterSpeakerStyle(string characterId)
        {
            EnsureSceneComposerProject();
            if (_sceneComposerProject.speakerStyleOverrides == null ||
                string.IsNullOrWhiteSpace(characterId)) return;
            for (int i = _sceneComposerProject.speakerStyleOverrides.Count - 1; i >= 0; i--)
            {
                VnSceneComposerSpeakerStyleOverride item = _sceneComposerProject.speakerStyleOverrides[i];
                if (item == null || !string.Equals(item.characterId ?? string.Empty, characterId,
                    StringComparison.OrdinalIgnoreCase)) continue;
                RecordSceneComposerUndo("Restore VN Character Speaker Style");
                CancelSceneComposerPreviewDrag();
                _sceneComposerProject.speakerStyleOverrides.RemoveAt(i);
                ResetSceneComposerPlayback();
                MarkSceneComposerChanged();
                return;
            }
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
                "Стиль говорящего задаётся отдельно для персонажа, стиль реплики — для сцены. " +
                "X / Y / Width / Height общие для всех сцен и Beat.",
                MessageType.Info);

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
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            if (beat == null && scene.dialogueBeats != null && scene.dialogueBeats.Count > 0)
                beat = scene.dialogueBeats[0];
            VnWorkshopTypographyValues values =
                VnSceneComposerTextStyleResolver.Resolve(_sceneComposerProject, scene, beat);
            string characterId = speaker
                ? VnSceneComposerTextStyleResolver.ResolveSpeakerCharacterId(scene, beat)
                : string.Empty;
            bool hasScoped = speaker
                ? VnSceneComposerTextStyleResolver.FindSpeakerStyle(_sceneComposerProject, characterId) != null
                : scene.dialogueBodyStyleOverride != null && scene.dialogueBodyStyleOverride.HasAnyOverride;

            if (speaker && string.IsNullOrEmpty(characterId))
                EditorGUILayout.HelpBox("Нет стабильного ID персонажа: используется общий стиль.", MessageType.None);
            else
            {
                bool nextScoped = EditorGUILayout.Toggle(
                    speaker ? "Свой стиль персонажа" : "Свой стиль этой сцены", hasScoped);
                if (nextScoped != hasScoped)
                {
                    if (nextScoped)
                        SetScopedStyle(speaker, characterId,
                            speaker ? values.SpeakerFontAssetGuid : values.DialogueFontAssetGuid,
                            speaker ? values.SpeakerFontSize : values.DialogueFontSize,
                            speaker ? values.SpeakerColor : values.DialogueColor,
                            speaker ? values.SpeakerAlignment : values.DialogueAlignment);
                    else if (speaker) ComposerClearCharacterSpeakerStyle(characterId);
                    else ComposerClearSelectedSceneDialogueBodyStyle();
                    values = VnSceneComposerTextStyleResolver.Resolve(_sceneComposerProject, scene, beat);
                }
            }

            string guid = speaker ? values.SpeakerFontAssetGuid : values.DialogueFontAssetGuid;
            float fontSize = speaker ? values.SpeakerFontSize : values.DialogueFontSize;
            Color color = speaker ? values.SpeakerColor : values.DialogueColor;
            VnWorkshopTextAlignment alignment =
                speaker ? values.SpeakerAlignment : values.DialogueAlignment;

            UnityEngine.Object currentFont = VnSceneComposerTextFontResolver.ResolveAsset(guid);
            EditorGUILayout.LabelField("Текущий шрифт",
                currentFont != null ? currentFont.name : "RokasSans (по умолчанию)");

            VnSceneComposerInstalledFontFace[] faces = VnSceneComposerTextFontResolver.GetInstalledWindowsFonts();
            string[] options = new string[faces.Length + 1];
            options[0] = faces.Length == 0 ? "Установленные Windows-шрифты не найдены"
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
                    if (!imported.Success) SetSceneComposerStatus(imported.Error, MessageType.Error);
                    else
                    {
                        guid = imported.TmpFontAssetGuid;
                        SetScopedStyle(speaker, characterId, guid, fontSize, color, alignment);
                        SetSceneComposerStatus("Шрифт импортирован в проект и связан с TMP: " +
                            imported.TmpFontAssetPath, MessageType.Info);
                    }
                }
            }

            if (GUILayout.Button("RokasSans по умолчанию"))
            {
                CancelSceneComposerPreviewDrag();
                guid = string.Empty;
                SetScopedStyle(speaker, characterId, guid, fontSize, color, alignment);
            }

            // STYLE SCOPE: ColorField/font changes never read or write geometry.
            EditorGUI.BeginChangeCheck();
            float nextFontSize = EditorGUILayout.Slider("Размер", fontSize, 8f, 160f);
            Color nextColor = EditorGUILayout.ColorField("Цвет", color);
            float nextOpacity = EditorGUILayout.Slider("Прозрачность", color.a, 0f, 1f);
            nextColor.a = nextOpacity;
            VnWorkshopTextAlignment nextAlignment =
                (VnWorkshopTextAlignment)EditorGUILayout.EnumPopup("Выравнивание", alignment);
            if (EditorGUI.EndChangeCheck())
            {
                CancelSceneComposerPreviewDrag();
                SetScopedStyle(speaker, characterId, guid, nextFontSize, nextColor, nextAlignment);
            }

            DrawSharedFallbackPicker(guid);

            // GEOMETRY SCOPE: one project-level reference rect only.
            Rect rect = ResolveSharedTextRect(speaker);
            EditorGUI.BeginChangeCheck();
            float x = EditorGUILayout.FloatField("X", rect.x);
            float y = EditorGUILayout.FloatField("Y", rect.y);
            float width = EditorGUILayout.FloatField("Width", rect.width);
            float height = EditorGUILayout.FloatField("Height", rect.height);
            if (EditorGUI.EndChangeCheck())
                ComposerSetSharedTextGeometry(speaker, new Vector2(x, y), new Vector2(width, height));

            EditorGUILayout.HelpBox(
                "X / Y / Width / Height — единый общий Rect текста на reference-canvas. " +
                "Длина реплики, фон, видео, персонаж и плашка его не переписывают.", MessageType.None);
            EditorGUILayout.HelpBox(
                "Windows используется только как источник при выборе. После импорта сохраняется GUID " +
                "TMP Font Asset внутри Assets; абсолютный путь Windows в данных сцены не сохраняется.",
                MessageType.None);
        }

        private void SetScopedStyle(bool speaker, string characterId, string guid, float size,
            Color color, VnWorkshopTextAlignment alignment)
        {
            if (speaker && !string.IsNullOrEmpty(characterId))
                ComposerSetCharacterSpeakerStyle(characterId, guid, size, color, alignment);
            else if (!speaker)
                ComposerSetSelectedSceneDialogueBodyStyle(guid, size, color, alignment);
            else
            {
                ValidateTypographyStyle(guid, size, color, alignment);
                RecordSceneComposerUndo("Edit Default VN Speaker Style");
                CancelSceneComposerPreviewDrag();
                SetDefaultStyle(true, guid, size, color, alignment);
                ResetSceneComposerPlayback();
                MarkSceneComposerChanged();
            }
        }

        private Rect ResolveSharedTextRect(bool speaker)
        {
            VnWorkshopPreviewFrame frame = VnPresentationWorkshopPreviewRenderer.BuildFrame(
                GetSharedDialoguePresentation(), VnWorkshopResolution.Reference1920x1080,
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
