using System;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed partial class VnPresentationWorkshopWindow
    {
        // Retained only so legacy serialized editor-window state and unrelated selection-clearing code stay harmless.
        // Arbitrary scene-text authoring itself is intentionally retired.
        [SerializeField] private string _sceneComposerSelectedTextId = string.Empty;
        [SerializeField] private bool _sceneComposerSpeakerTypographyExpanded;
        [SerializeField] private bool _sceneComposerDialogueTypographyExpanded;

        public VnWorkshopTypographyValues ComposerGetSharedTypography()
        {
            return VnPresentationWorkshopVn10Resolver.ResolveTypography(GetSharedDialoguePresentation());
        }

        public void ComposerSetSharedSpeakerTypography(
            string fontAssetGuid,
            float fontSize,
            Color color,
            VnWorkshopTextAlignment alignment,
            Vector2 positionDelta,
            Vector2 sizeDelta)
        {
            ValidateSharedTypography(fontAssetGuid, fontSize, color, alignment, positionDelta, sizeDelta);
            RecordSceneComposerUndo("Edit Shared VN Speaker Typography");
            VnPresentationWorkshopPreset preset = GetSharedDialoguePresentation();
            VnWorkshopTypographyValues baseline =
                VnPresentationWorkshopVn10Resolver.ResolveTypography(new VnPresentationWorkshopPreset());
            if (preset.typography == null) preset.typography = new VnWorkshopTypographyOverride();

            preset.typography.hasSpeakerFontAssetGuid = !string.IsNullOrWhiteSpace(fontAssetGuid);
            preset.typography.speakerFontAssetGuid = fontAssetGuid ?? string.Empty;
            preset.typography.hasSpeakerFontSize = !Mathf.Approximately(fontSize, baseline.SpeakerFontSize);
            preset.typography.speakerFontSize = fontSize;
            preset.typography.hasSpeakerColor = !Approximately(color, baseline.SpeakerColor);
            preset.typography.speakerColor = color;
            preset.typography.hasSpeakerAlignment = alignment != baseline.SpeakerAlignment;
            preset.typography.speakerAlignment = alignment;

            ApplySharedTextRect(preset.speakerName, positionDelta, sizeDelta);
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        public void ComposerSetSharedDialogueTypography(
            string fontAssetGuid,
            float fontSize,
            Color color,
            VnWorkshopTextAlignment alignment,
            Vector2 positionDelta,
            Vector2 sizeDelta)
        {
            ValidateSharedTypography(fontAssetGuid, fontSize, color, alignment, positionDelta, sizeDelta);
            RecordSceneComposerUndo("Edit Shared VN Dialogue Typography");
            VnPresentationWorkshopPreset preset = GetSharedDialoguePresentation();
            VnWorkshopTypographyValues baseline =
                VnPresentationWorkshopVn10Resolver.ResolveTypography(new VnPresentationWorkshopPreset());
            if (preset.typography == null) preset.typography = new VnWorkshopTypographyOverride();

            preset.typography.hasDialogueFontAssetGuid = !string.IsNullOrWhiteSpace(fontAssetGuid);
            preset.typography.dialogueFontAssetGuid = fontAssetGuid ?? string.Empty;
            preset.typography.hasDialogueFontSize = !Mathf.Approximately(fontSize, baseline.DialogueFontSize);
            preset.typography.dialogueFontSize = fontSize;
            preset.typography.hasDialogueColor = !Approximately(color, baseline.DialogueColor);
            preset.typography.dialogueColor = color;
            preset.typography.hasDialogueAlignment = alignment != baseline.DialogueAlignment;
            preset.typography.dialogueAlignment = alignment;

            ApplySharedTextRect(preset.dialogueText, positionDelta, sizeDelta);
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        public string ComposerGetSharedTypographyWarning()
        {
            VnWorkshopTypographyValues typography = ComposerGetSharedTypography();
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
            VnWorkshopElementOverride target, Vector2 positionDelta, Vector2 sizeDelta)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            target.hasPositionDelta = positionDelta != Vector2.zero;
            target.positionDelta = positionDelta;
            target.hasSizeDelta = sizeDelta != Vector2.zero;
            target.sizeDelta = sizeDelta;
        }

        private static void ValidateSharedTypography(
            string fontAssetGuid,
            float fontSize,
            Color color,
            VnWorkshopTextAlignment alignment,
            Vector2 positionDelta,
            Vector2 sizeDelta)
        {
            if (!Enum.IsDefined(typeof(VnWorkshopTextAlignment), alignment))
                throw new ArgumentOutOfRangeException(nameof(alignment));
            if (!IsFinite(fontSize) || fontSize < 8f || fontSize > 160f)
                throw new ArgumentOutOfRangeException(nameof(fontSize));
            if (!IsFinite(positionDelta) || !IsFinite(sizeDelta))
                throw new ArgumentException("Dialogue typography layout must be finite.");
            if (!IsFinite(color.r) || !IsFinite(color.g) || !IsFinite(color.b) || !IsFinite(color.a))
                throw new ArgumentException("Dialogue typography color must be finite.");
            if (!string.IsNullOrWhiteSpace(fontAssetGuid))
            {
                UnityEngine.Object asset = VnSceneComposerTextFontResolver.ResolveAsset(fontAssetGuid);
                if (asset != null && !VnSceneComposerTextFontResolver.IsSupportedAsset(asset))
                    throw new ArgumentException("Dialogue typography font reference is not a supported project font.");
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
                ? "font asset не найден; будет использован RokasSans."
                : warning);
        }

        private void DrawSceneComposerDialogueTypographyInspector(VnSceneComposerScene scene)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Оформление диалога", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "Настройки ниже относятся к существующим полям «Говорящий» и «Текст реплики». " +
                "Они общие для последующих реплик: содержание меняется по Beat, оформление остаётся тем же.",
                MessageType.Info);

            if (GUILayout.Button("Изменить текст говорящего", GUILayout.Height(25f)))
                _sceneComposerSpeakerTypographyExpanded = !_sceneComposerSpeakerTypographyExpanded;
            if (_sceneComposerSpeakerTypographyExpanded)
            {
                EditorGUI.indentLevel++;
                DrawSharedTypographyControls(true);
                EditorGUI.indentLevel--;
            }

            if (GUILayout.Button("Изменить текст реплики", GUILayout.Height(25f)))
                _sceneComposerDialogueTypographyExpanded = !_sceneComposerDialogueTypographyExpanded;
            if (_sceneComposerDialogueTypographyExpanded)
            {
                EditorGUI.indentLevel++;
                DrawSharedTypographyControls(false);
                EditorGUI.indentLevel--;
            }

            string warning = ComposerGetSharedTypographyWarning();
            if (!string.IsNullOrEmpty(warning))
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }

        private void DrawSharedTypographyControls(bool speaker)
        {
            VnPresentationWorkshopPreset preset = GetSharedDialoguePresentation();
            VnWorkshopTypographyValues values = VnPresentationWorkshopVn10Resolver.ResolveTypography(preset);
            VnWorkshopElementOverride layout = speaker ? preset.speakerName : preset.dialogueText;
            string guid = speaker ? values.SpeakerFontAssetGuid : values.DialogueFontAssetGuid;
            float fontSize = speaker ? values.SpeakerFontSize : values.DialogueFontSize;
            Color color = speaker ? values.SpeakerColor : values.DialogueColor;
            VnWorkshopTextAlignment alignment = speaker ? values.SpeakerAlignment : values.DialogueAlignment;
            Vector2 position = layout != null && layout.hasPositionDelta ? layout.positionDelta : Vector2.zero;
            Vector2 size = layout != null && layout.hasSizeDelta ? layout.sizeDelta : Vector2.zero;

            UnityEngine.Object currentFont = VnSceneComposerTextFontResolver.ResolveAsset(guid);
            EditorGUILayout.LabelField("Текущий шрифт",
                currentFont != null ? currentFont.name : "RokasSans (по умолчанию)");

            VnSceneComposerInstalledFontFace[] faces = VnSceneComposerTextFontResolver.GetInstalledWindowsFonts();
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
                    VnSceneComposerFontImportResult imported =
                        VnSceneComposerTextFontResolver.ImportWindowsFont(faces[selected - 1]);
                    if (!imported.Success)
                    {
                        SetSceneComposerStatus(imported.Error, MessageType.Error);
                    }
                    else
                    {
                        guid = imported.TmpFontAssetGuid;
                        ApplyTypographyValues(speaker, guid, fontSize, color, alignment, position, size);
                        SetSceneComposerStatus(
                            "Шрифт импортирован в проект и связан с TMP: " + imported.TmpFontAssetPath,
                            MessageType.Info);
                    }
                }
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("RokasSans по умолчанию"))
            {
                guid = string.Empty;
                ApplyTypographyValues(speaker, guid, fontSize, color, alignment, position, size);
            }
            if (GUILayout.Button("Обновить список Windows"))
            {
                VnSceneComposerTextFontResolver.RefreshInstalledWindowsFonts();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            float nextFontSize = EditorGUILayout.Slider("Размер", fontSize, 8f, 160f);
            Color nextColor = EditorGUILayout.ColorField("Цвет", color);
            float nextOpacity = EditorGUILayout.Slider("Прозрачность", color.a, 0f, 1f);
            nextColor.a = nextOpacity;
            VnWorkshopTextAlignment nextAlignment = (VnWorkshopTextAlignment)EditorGUILayout.EnumPopup(
                "Выравнивание", alignment);
            float x = EditorGUILayout.FloatField("Позиция X", position.x);
            float y = EditorGUILayout.FloatField("Позиция Y", position.y);
            float width = EditorGUILayout.FloatField("Ширина", size.x);
            float height = EditorGUILayout.FloatField("Высота", size.y);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyTypographyValues(
                    speaker, guid, nextFontSize, nextColor, nextAlignment,
                    new Vector2(x, y), new Vector2(width, height));
                fontSize = nextFontSize;
                color = nextColor;
                alignment = nextAlignment;
                position = new Vector2(x, y);
                size = new Vector2(width, height);
            }

            DrawSharedFallbackPicker(guid);

            EditorGUILayout.HelpBox(
                "Windows используется только как источник при выборе. После импорта сохраняется GUID " +
                "TMP Font Asset внутри Assets; абсолютный путь Windows в данных сцены не сохраняется.",
                MessageType.None);
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

            VnSceneComposerFontImportResult imported =
                VnSceneComposerTextFontResolver.ImportWindowsFont(faces[selected - 1]);
            if (!imported.Success)
            {
                SetSceneComposerStatus(imported.Error, MessageType.Error);
                return;
            }

            string effectivePrimary = string.IsNullOrWhiteSpace(primaryGuid)
                ? VnSceneComposerTextFontResolver.GetDefaultFontAssetGuid()
                : primaryGuid;
            if (!VnSceneComposerTextFontResolver.ConfigureFallback(
                    effectivePrimary, imported.TmpFontAssetGuid, out string error))
            {
                SetSceneComposerStatus(error, MessageType.Error);
                return;
            }

            SetSceneComposerStatus(
                "TMP fallback добавлен: " + faces[selected - 1].DisplayName,
                MessageType.Info);
        }

        private void ApplyTypographyValues(
            bool speaker,
            string guid,
            float fontSize,
            Color color,
            VnWorkshopTextAlignment alignment,
            Vector2 position,
            Vector2 size)
        {
            if (speaker)
                ComposerSetSharedSpeakerTypography(guid, fontSize, color, alignment, position, size);
            else
                ComposerSetSharedDialogueTypography(guid, fontSize, color, alignment, position, size);
        }
    }
}
