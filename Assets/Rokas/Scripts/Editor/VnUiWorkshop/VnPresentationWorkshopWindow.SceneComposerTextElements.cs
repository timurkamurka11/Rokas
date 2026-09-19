using System;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed partial class VnPresentationWorkshopWindow
    {
        [SerializeField] private string _sceneComposerSelectedTextId = string.Empty;

        public string ComposerGetSelectedTextId() => _sceneComposerSelectedTextId ?? string.Empty;

        public string ComposerAddText()
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            ComposerEnsureTextElements(scene);
            RecordSceneComposerUndo("Add VN Scene Text");
            string defaultGuid = VnSceneComposerTextFontResolver.GetDefaultFontAssetGuid();
            UnityEngine.Object defaultAsset = VnSceneComposerTextFontResolver.ResolveAsset(defaultGuid);
            var element = new VnSceneComposerTextElement
            {
                textElementId = VnSceneComposerScene.NewStableId(),
                text = "Новый текст",
                fontAssetGuid = defaultGuid,
                fontDisplayName = VnSceneComposerTextFontResolver.GetDisplayName(defaultAsset),
                fontSize = 48f,
                position = new Vector2(960f, 360f),
                size = new Vector2(720f, 160f),
                color = Color.white,
                opacity = 1f,
                alignment = VnSceneComposerTextAlignment.Center,
                visible = true,
                layer = VnSceneComposerTextLayer.FrontCharacters
            };
            scene.textElements.Add(element);
            _sceneComposerSelectedTextId = element.textElementId;
            _sceneComposerSelectedDecorationId = string.Empty;
            _sceneComposerSelectedCharacterIndex = -1;
            MarkSceneComposerChanged();
            return element.textElementId;
        }

        public void ComposerSelectText(string textElementId)
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            ComposerEnsureTextElements(scene);
            int index = FindTextIndex(scene, textElementId);
            if (index < 0)
                throw new ArgumentException("Text element is not part of the selected Scene.", nameof(textElementId));
            _sceneComposerSelectedTextId = scene.textElements[index].textElementId;
            _sceneComposerSelectedDecorationId = string.Empty;
            _sceneComposerSelectedCharacterIndex = -1;
            Repaint();
        }

        public string ComposerDuplicateSelectedText()
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            VnSceneComposerTextElement source = ComposerGetSelectedTextElement();
            if (source == null) return string.Empty;
            RecordSceneComposerUndo("Duplicate VN Scene Text");
            VnSceneComposerTextElement copy =
                JsonUtility.FromJson<VnSceneComposerTextElement>(JsonUtility.ToJson(source));
            if (copy == null) throw new InvalidOperationException("Could not duplicate Scene text.");
            copy.textElementId = VnSceneComposerScene.NewStableId();
            copy.position += new Vector2(24f, 24f);
            scene.textElements.Add(copy);
            _sceneComposerSelectedTextId = copy.textElementId;
            _sceneComposerSelectedDecorationId = string.Empty;
            _sceneComposerSelectedCharacterIndex = -1;
            MarkSceneComposerChanged();
            return copy.textElementId;
        }

        public void ComposerSetSelectedTextContent(string value)
        {
            VnSceneComposerTextElement element = RequireSelectedTextElement();
            RecordSceneComposerUndo("Edit VN Scene Text");
            element.text = value ?? string.Empty;
            MarkSceneComposerChanged();
        }

        public void ComposerSetSelectedTextFontAsset(UnityEngine.Object asset)
        {
            if (asset != null && !VnSceneComposerTextFontResolver.IsSupportedAsset(asset))
                throw new ArgumentException("Scene text font must be a TMP Font Asset or project Font.", nameof(asset));
            VnSceneComposerTextElement element = RequireSelectedTextElement();
            RecordSceneComposerUndo("Change VN Scene Text Font");
            element.fontAssetGuid = VnSceneComposerTextFontResolver.GetAssetGuid(asset);
            element.fontDisplayName = VnSceneComposerTextFontResolver.GetDisplayName(asset);
            MarkSceneComposerChanged();
        }

        public void ComposerSetSelectedTextFontSize(float fontSize)
        {
            if (!IsFiniteTextValue(fontSize))
                throw new ArgumentException("Text font size must be finite.", nameof(fontSize));
            VnSceneComposerTextElement element = RequireSelectedTextElement();
            RecordSceneComposerUndo("Resize VN Scene Text");
            element.fontSize = Mathf.Clamp(fontSize, 1f, 512f);
            MarkSceneComposerChanged();
        }

        public void ComposerSetSelectedTextPosition(Vector2 position)
        {
            if (!IsFinite(position)) throw new ArgumentException("Text position must be finite.", nameof(position));
            VnSceneComposerTextElement element = RequireSelectedTextElement();
            RecordSceneComposerUndo("Move VN Scene Text");
            element.position = position;
            MarkSceneComposerChanged();
        }

        public void ComposerSetSelectedTextSize(Vector2 size)
        {
            if (!IsFinite(size)) throw new ArgumentException("Text size must be finite.", nameof(size));
            VnSceneComposerTextElement element = RequireSelectedTextElement();
            RecordSceneComposerUndo("Resize VN Scene Text Box");
            element.size = new Vector2(Mathf.Clamp(size.x, 1f, 10000f), Mathf.Clamp(size.y, 1f, 10000f));
            MarkSceneComposerChanged();
        }

        public void ComposerSetSelectedTextColor(Color color)
        {
            if (!IsFiniteTextValue(color.r) || !IsFiniteTextValue(color.g) ||
                !IsFiniteTextValue(color.b) || !IsFiniteTextValue(color.a))
                throw new ArgumentException("Text color must contain finite values.", nameof(color));
            VnSceneComposerTextElement element = RequireSelectedTextElement();
            RecordSceneComposerUndo("Change VN Scene Text Color");
            element.color = color;
            MarkSceneComposerChanged();
        }

        public void ComposerSetSelectedTextOpacity(float opacity)
        {
            if (!IsFiniteTextValue(opacity))
                throw new ArgumentException("Text opacity must be finite.", nameof(opacity));
            VnSceneComposerTextElement element = RequireSelectedTextElement();
            RecordSceneComposerUndo("Change VN Scene Text Opacity");
            element.opacity = Mathf.Clamp01(opacity);
            MarkSceneComposerChanged();
        }

        public void ComposerSetSelectedTextAlignment(VnSceneComposerTextAlignment alignment)
        {
            if (!Enum.IsDefined(typeof(VnSceneComposerTextAlignment), alignment))
                throw new ArgumentOutOfRangeException(nameof(alignment), alignment, null);
            VnSceneComposerTextElement element = RequireSelectedTextElement();
            RecordSceneComposerUndo("Align VN Scene Text");
            element.alignment = alignment;
            MarkSceneComposerChanged();
        }

        public void ComposerSetSelectedTextVisible(bool visible)
        {
            VnSceneComposerTextElement element = RequireSelectedTextElement();
            RecordSceneComposerUndo("Toggle VN Scene Text Visibility");
            element.visible = visible;
            MarkSceneComposerChanged();
        }

        public void ComposerSetSelectedTextLayer(VnSceneComposerTextLayer layer)
        {
            if (!Enum.IsDefined(typeof(VnSceneComposerTextLayer), layer))
                throw new ArgumentOutOfRangeException(nameof(layer), layer, null);
            VnSceneComposerTextElement element = RequireSelectedTextElement();
            RecordSceneComposerUndo("Change VN Scene Text Layer");
            element.layer = layer;
            MarkSceneComposerChanged();
        }

        public bool ComposerDeleteSelectedText()
        {
            VnSceneComposerScene scene = GetSelectedScene();
            if (scene == null || scene.textElements == null || string.IsNullOrEmpty(_sceneComposerSelectedTextId))
                return false;
            int index = FindTextIndex(scene, _sceneComposerSelectedTextId);
            if (index < 0)
            {
                _sceneComposerSelectedTextId = string.Empty;
                return false;
            }
            RecordSceneComposerUndo("Delete VN Scene Text");
            scene.textElements.RemoveAt(index);
            _sceneComposerSelectedTextId = string.Empty;
            MarkSceneComposerChanged();
            return true;
        }

        internal VnSceneComposerTextElement ComposerGetSelectedTextElement()
        {
            VnSceneComposerScene scene = GetSelectedScene();
            if (scene == null || scene.textElements == null) return null;
            int index = FindTextIndex(scene, _sceneComposerSelectedTextId);
            return index >= 0 ? scene.textElements[index] : null;
        }

        internal void ComposerEnsureTextElements(VnSceneComposerScene scene)
        {
            if (scene != null && scene.textElements == null)
                scene.textElements = new System.Collections.Generic.List<VnSceneComposerTextElement>();
        }

        private VnSceneComposerTextElement RequireSelectedTextElement()
        {
            VnSceneComposerTextElement element = ComposerGetSelectedTextElement();
            if (element == null) throw new InvalidOperationException("No Scene text is selected.");
            return element;
        }

        private static int FindTextIndex(VnSceneComposerScene scene, string textElementId)
        {
            if (scene == null || scene.textElements == null || string.IsNullOrEmpty(textElementId)) return -1;
            for (int i = 0; i < scene.textElements.Count; i++)
            {
                VnSceneComposerTextElement element = scene.textElements[i];
                if (element != null &&
                    string.Equals(element.textElementId, textElementId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private bool ComposerSelectPreviewTextAt(
            VnWorkshopPreviewFrame frame, Vector2 logicalPoint, VnSceneComposerTextLayer layer)
        {
            if (frame == null || frame.ComposerTexts == null) return false;
            for (int i = frame.ComposerTexts.Length - 1; i >= 0; i--)
            {
                VnWorkshopPreviewText element = frame.ComposerTexts[i];
                if (element == null || element.Layer != layer || !element.Body.Contains(logicalPoint)) continue;
                _sceneComposerSelectedTextId = element.TextElementId ?? string.Empty;
                _sceneComposerSelectedDecorationId = string.Empty;
                _sceneComposerSelectedCharacterIndex = -1;
                Repaint();
                return true;
            }
            return false;
        }

        private VnWorkshopPreviewText ComposerGetSelectedPreviewText(VnWorkshopPreviewFrame frame)
        {
            if (frame == null || frame.ComposerTexts == null || string.IsNullOrEmpty(_sceneComposerSelectedTextId))
                return null;
            for (int i = 0; i < frame.ComposerTexts.Length; i++)
            {
                VnWorkshopPreviewText element = frame.ComposerTexts[i];
                if (element != null &&
                    string.Equals(element.TextElementId, _sceneComposerSelectedTextId, StringComparison.Ordinal))
                    return element;
            }
            return null;
        }

        private void ApplySceneComposerTextDrag(Vector2 logicalDelta)
        {
            if (!IsFinite(logicalDelta)) return;
            VnSceneComposerTextElement element = ComposerGetSelectedTextElement();
            if (element == null) return;
            element.position += logicalDelta;
            MarkSceneComposerChanged();
        }

        private string ComposerGetTextWarning(VnSceneComposerTextElement element)
        {
            if (element == null) return string.Empty;
            VnSceneComposerTextFontResolver.TryResolvePreviewFont(
                element.fontAssetGuid, out _, out string warning);
            return warning;
        }

        private void DrawSceneComposerArbitraryTextInspector(VnSceneComposerScene scene)
        {
            ComposerEnsureTextElements(scene);
            EditorGUILayout.LabelField("Текст сцены", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "Отдельные надписи сцены: заголовки, дата/время, вывески, интерфейс и декоративный текст. " +
                "Они не заменяют реплики и не меняют dialogue-систему.",
                MessageType.None);

            if (GUILayout.Button("+ Добавить текст", GUILayout.Height(26f)))
                ComposerAddText();

            if (scene.textElements.Count == 0)
            {
                EditorGUILayout.HelpBox("В этой сцене пока нет отдельных текстовых элементов.", MessageType.Info);
                return;
            }

            for (int i = 0; i < scene.textElements.Count; i++)
            {
                VnSceneComposerTextElement item = scene.textElements[i];
                if (item == null) continue;
                bool selected = string.Equals(item.textElementId, _sceneComposerSelectedTextId, StringComparison.Ordinal);
                string label = (item.text ?? string.Empty).Replace('\n', ' ').Trim();
                if (label.Length == 0) label = "Текст " + (i + 1);
                if (label.Length > 28) label = label.Substring(0, 28) + "…";
                if (!item.visible) label += " (скрыт)";
                if (GUILayout.Button(label, selected ? EditorStyles.miniButtonMid : EditorStyles.miniButton))
                    ComposerSelectText(item.textElementId);
            }

            VnSceneComposerTextElement element = ComposerGetSelectedTextElement();
            if (element == null)
            {
                EditorGUILayout.HelpBox("Выберите текст в списке или прямо в предпросмотре.", MessageType.None);
                return;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Содержимое");
            EditorGUI.BeginChangeCheck();
            string nextText = EditorGUILayout.TextArea(element.text ?? string.Empty, GUILayout.MinHeight(58f));
            if (EditorGUI.EndChangeCheck()) ComposerSetSelectedTextContent(nextText);

            UnityEngine.Object currentFont = VnSceneComposerTextFontResolver.ResolveAsset(element.fontAssetGuid);
            EditorGUI.BeginChangeCheck();
            UnityEngine.Object nextFont = EditorGUILayout.ObjectField(
                "Шрифт", currentFont, typeof(UnityEngine.Object), false);
            if (EditorGUI.EndChangeCheck())
            {
                try { ComposerSetSelectedTextFontAsset(nextFont); }
                catch (Exception exception) { SetSceneComposerStatus(exception.Message, MessageType.Error); }
            }

            string fallbackInfo = VnSceneComposerTextFontResolver.DescribeFallbacks(currentFont);
            if (!string.IsNullOrEmpty(fallbackInfo))
                EditorGUILayout.LabelField(fallbackInfo, EditorStyles.miniLabel);

            EditorGUI.BeginChangeCheck();
            float fontSize = EditorGUILayout.Slider("Размер шрифта", element.fontSize, 8f, 256f);
            if (EditorGUI.EndChangeCheck()) ComposerSetSelectedTextFontSize(fontSize);

            EditorGUI.BeginChangeCheck();
            float x = EditorGUILayout.FloatField("X", element.position.x);
            float y = EditorGUILayout.FloatField("Y", element.position.y);
            if (EditorGUI.EndChangeCheck()) ComposerSetSelectedTextPosition(new Vector2(x, y));

            EditorGUI.BeginChangeCheck();
            float width = EditorGUILayout.FloatField("Ширина", element.size.x);
            float height = EditorGUILayout.FloatField("Высота", element.size.y);
            if (EditorGUI.EndChangeCheck()) ComposerSetSelectedTextSize(new Vector2(width, height));

            EditorGUI.BeginChangeCheck();
            Color color = EditorGUILayout.ColorField("Цвет", element.color);
            if (EditorGUI.EndChangeCheck()) ComposerSetSelectedTextColor(color);

            EditorGUI.BeginChangeCheck();
            float opacity = EditorGUILayout.Slider("Прозрачность", element.opacity, 0f, 1f);
            if (EditorGUI.EndChangeCheck()) ComposerSetSelectedTextOpacity(opacity);

            string[] alignmentLabels = { "Слева", "По центру", "Справа" };
            EditorGUI.BeginChangeCheck();
            int alignment = EditorGUILayout.Popup("Выравнивание", (int)element.alignment, alignmentLabels);
            if (EditorGUI.EndChangeCheck())
                ComposerSetSelectedTextAlignment((VnSceneComposerTextAlignment)alignment);

            EditorGUI.BeginChangeCheck();
            bool visible = EditorGUILayout.Toggle("Показывать", element.visible);
            if (EditorGUI.EndChangeCheck()) ComposerSetSelectedTextVisible(visible);

            string[] layerLabels = { "За персонажами", "Перед персонажами" };
            EditorGUI.BeginChangeCheck();
            int layer = EditorGUILayout.Popup("Слой", (int)element.layer, layerLabels);
            if (EditorGUI.EndChangeCheck())
                ComposerSetSelectedTextLayer((VnSceneComposerTextLayer)layer);

            string warning = ComposerGetTextWarning(element);
            if (!string.IsNullOrEmpty(warning))
                EditorGUILayout.HelpBox(warning, MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Дублировать текст")) ComposerDuplicateSelectedText();
            if (GUILayout.Button("Удалить текст")) ComposerDeleteSelectedText();
            EditorGUILayout.EndHorizontal();
        }

        private static bool IsFiniteTextValue(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
