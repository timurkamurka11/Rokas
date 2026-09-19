using System;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed partial class VnPresentationWorkshopWindow
    {
        [SerializeField] private string _sceneComposerSelectedDecorationId = string.Empty;
        [SerializeField] private int _sceneComposerDecorationLibraryIndex;

        public string ComposerGetSelectedDecorationId() => _sceneComposerSelectedDecorationId ?? string.Empty;

        public void ComposerSelectDecoration(string decorationId)
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            ComposerEnsureDecorations(scene);
            int index = FindDecorationIndex(scene, decorationId);
            if (index < 0) throw new ArgumentException("Decoration is not part of the selected Scene.", nameof(decorationId));
            _sceneComposerSelectedDecorationId = scene.decorations[index].decorationId;
            _sceneComposerSelectedCharacterIndex = -1;
            Repaint();
        }

        public string ComposerAddDecorationAsset(Texture2D texture)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Decoration image must be a Unity project asset.", nameof(texture));
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrWhiteSpace(guid))
                throw new InvalidOperationException("Decoration image does not have a stable Unity asset GUID.");

            VnSceneComposerScene scene = RequireSelectedScene();
            ComposerEnsureDecorations(scene);
            RecordSceneComposerUndo("Add VN Scene Decoration");
            var decoration = new VnSceneComposerDecoration
            {
                decorationId = VnSceneComposerScene.NewStableId(),
                assetGuid = guid,
                displayName = texture.name ?? string.Empty,
                position = VnPresentationWorkshopBaseline.ReferenceResolution * .5f,
                scale = 1f,
                opacity = 1f,
                visible = true,
                layer = VnSceneComposerDecorationLayer.BehindCharacters
            };
            scene.decorations.Add(decoration);
            _sceneComposerSelectedDecorationId = decoration.decorationId;
            _sceneComposerSelectedCharacterIndex = -1;
            MarkSceneComposerChanged();
            return decoration.decorationId;
        }

        public void ComposerSetSelectedDecorationAsset(Texture2D texture)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            string path = AssetDatabase.GetAssetPath(texture);
            string guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrWhiteSpace(guid))
                throw new ArgumentException("Decoration image must be a Unity project asset with a stable GUID.", nameof(texture));
            VnSceneComposerDecoration decoration = RequireSelectedDecoration();
            RecordSceneComposerUndo("Change VN Scene Decoration Image");
            decoration.assetGuid = guid;
            decoration.displayName = texture.name ?? string.Empty;
            MarkSceneComposerChanged();
        }

        public void ComposerSetSelectedDecorationPosition(Vector2 position)
        {
            if (!IsFinite(position)) throw new ArgumentException("Decoration position must be finite.", nameof(position));
            VnSceneComposerDecoration decoration = RequireSelectedDecoration();
            RecordSceneComposerUndo("Move VN Scene Decoration");
            decoration.position = position;
            MarkSceneComposerChanged();
        }

        public void ComposerSetSelectedDecorationScale(float scale)
        {
            if (!IsFinite(scale)) throw new ArgumentException("Decoration scale must be finite.", nameof(scale));
            VnSceneComposerDecoration decoration = RequireSelectedDecoration();
            RecordSceneComposerUndo("Scale VN Scene Decoration");
            decoration.scale = Mathf.Clamp(scale, .05f, 5f);
            MarkSceneComposerChanged();
        }

        public void ComposerSetSelectedDecorationOpacity(float opacity)
        {
            if (!IsFinite(opacity)) throw new ArgumentException("Decoration opacity must be finite.", nameof(opacity));
            VnSceneComposerDecoration decoration = RequireSelectedDecoration();
            RecordSceneComposerUndo("Change VN Scene Decoration Opacity");
            decoration.opacity = Mathf.Clamp01(opacity);
            MarkSceneComposerChanged();
        }

        public void ComposerSetSelectedDecorationVisible(bool visible)
        {
            VnSceneComposerDecoration decoration = RequireSelectedDecoration();
            RecordSceneComposerUndo("Toggle VN Scene Decoration Visibility");
            decoration.visible = visible;
            MarkSceneComposerChanged();
        }

        public void ComposerSetSelectedDecorationLayer(VnSceneComposerDecorationLayer layer)
        {
            if (!Enum.IsDefined(typeof(VnSceneComposerDecorationLayer), layer))
                throw new ArgumentOutOfRangeException(nameof(layer), layer, null);
            VnSceneComposerDecoration decoration = RequireSelectedDecoration();
            RecordSceneComposerUndo("Change VN Scene Decoration Layer");
            decoration.layer = layer;
            MarkSceneComposerChanged();
        }

        public bool ComposerDeleteSelectedDecoration()
        {
            VnSceneComposerScene scene = GetSelectedScene();
            if (scene == null || scene.decorations == null || string.IsNullOrEmpty(_sceneComposerSelectedDecorationId)) return false;
            int index = FindDecorationIndex(scene, _sceneComposerSelectedDecorationId);
            if (index < 0) { _sceneComposerSelectedDecorationId = string.Empty; return false; }
            RecordSceneComposerUndo("Delete VN Scene Decoration");
            scene.decorations.RemoveAt(index);
            _sceneComposerSelectedDecorationId = string.Empty;
            MarkSceneComposerChanged();
            return true;
        }

        internal VnSceneComposerDecoration ComposerGetSelectedDecoration()
        {
            VnSceneComposerScene scene = GetSelectedScene();
            if (scene == null || scene.decorations == null) return null;
            int index = FindDecorationIndex(scene, _sceneComposerSelectedDecorationId);
            return index >= 0 ? scene.decorations[index] : null;
        }

        internal Texture2D ComposerResolveDecorationTexture(VnSceneComposerDecoration decoration)
        {
            if (decoration == null || string.IsNullOrWhiteSpace(decoration.assetGuid)) return null;
            string path = AssetDatabase.GUIDToAssetPath(decoration.assetGuid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        internal string ComposerGetDecorationWarning(VnSceneComposerDecoration decoration)
        {
            if (decoration == null || ComposerResolveDecorationTexture(decoration) != null) return string.Empty;
            string label = string.IsNullOrWhiteSpace(decoration.displayName)
                ? "Выбранная декорация"
                : "Декорация «" + decoration.displayName + "»";
            return label + ": файл изображения не найден. Данные элемента сохранены.";
        }

        private VnSceneComposerDecoration RequireSelectedDecoration()
        {
            VnSceneComposerDecoration decoration = ComposerGetSelectedDecoration();
            if (decoration == null) throw new InvalidOperationException("No Scene decoration is selected.");
            return decoration;
        }

        private static int FindDecorationIndex(VnSceneComposerScene scene, string decorationId)
        {
            if (scene == null || scene.decorations == null || string.IsNullOrEmpty(decorationId)) return -1;
            for (int i = 0; i < scene.decorations.Count; i++)
            {
                VnSceneComposerDecoration decoration = scene.decorations[i];
                if (decoration != null && string.Equals(decoration.decorationId, decorationId, StringComparison.Ordinal)) return i;
            }
            return -1;
        }

        internal void ComposerEnsureDecorations(VnSceneComposerScene scene)
        {
            if (scene != null && scene.decorations == null)
                scene.decorations = new System.Collections.Generic.List<VnSceneComposerDecoration>();
        }

        private bool ComposerSelectPreviewDecorationAt(
            VnWorkshopPreviewFrame frame, Vector2 logicalPoint, VnSceneComposerDecorationLayer layer)
        {
            if (frame == null || frame.ComposerDecorations == null) return false;
            for (int i = frame.ComposerDecorations.Length - 1; i >= 0; i--)
            {
                VnWorkshopPreviewDecoration decoration = frame.ComposerDecorations[i];
                if (decoration == null || decoration.Layer != layer || !decoration.Body.Contains(logicalPoint)) continue;
                _sceneComposerSelectedDecorationId = decoration.DecorationId ?? string.Empty;
                _sceneComposerSelectedCharacterIndex = -1;
                Repaint();
                return true;
            }
            return false;
        }

        private VnWorkshopPreviewDecoration ComposerGetSelectedPreviewDecoration(VnWorkshopPreviewFrame frame)
        {
            if (frame == null || frame.ComposerDecorations == null || string.IsNullOrEmpty(_sceneComposerSelectedDecorationId)) return null;
            for (int i = 0; i < frame.ComposerDecorations.Length; i++)
            {
                VnWorkshopPreviewDecoration decoration = frame.ComposerDecorations[i];
                if (decoration != null && string.Equals(decoration.DecorationId, _sceneComposerSelectedDecorationId, StringComparison.Ordinal))
                    return decoration;
            }
            return null;
        }

        private void ApplySceneComposerDecorationDrag(Vector2 logicalDelta)
        {
            if (!IsFinite(logicalDelta)) return;
            VnSceneComposerDecoration decoration = ComposerGetSelectedDecoration();
            if (decoration == null) return;
            decoration.position += logicalDelta;
            MarkSceneComposerChanged();
        }

        internal void TrySceneComposerDecorationAction(Action action)
        {
            try { action(); SetSceneComposerStatus("Декорация сцены обновлена.", MessageType.Info); }
            catch (Exception exception) { SetSceneComposerStatus(exception.Message, MessageType.Error); }
        }
    }
}
