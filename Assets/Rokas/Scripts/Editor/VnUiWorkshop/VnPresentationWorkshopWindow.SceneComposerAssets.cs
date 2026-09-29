using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed partial class VnPresentationWorkshopWindow
    {
        [SerializeField] private bool _sceneComposerAssetLibraryExpanded;
        [SerializeField] private VnSceneComposerAssetPurpose _sceneComposerOnboardPurpose = VnSceneComposerAssetPurpose.ReferenceImage;
        [SerializeField] private string _sceneComposerOnboardDisplayName = string.Empty;
        [SerializeField] private string _sceneComposerOnboardCharacter = "Mina";
        [SerializeField] private string _sceneComposerOnboardState = string.Empty;
        [SerializeField] private int _sceneComposerBackgroundAssetIndex;

        public void ComposerRefreshAssets()
        {
            VnSceneComposerAssetCatalog catalog = VnSceneComposerAssetLibrary.Refresh(GetProjectRoot());
            ClearSceneComposerThumbnailCache();
            ResetSceneComposerPlayback();
            string[] issues = catalog.entries
                .Where(entry => entry != null && (entry.missing || !string.IsNullOrWhiteSpace(entry.warning)))
                .Select(entry => string.IsNullOrWhiteSpace(entry.warning)
                    ? "Файл ресурса не найден: " + (entry.assetPath ?? string.Empty)
                    : entry.warning)
                .Distinct()
                .ToArray();
            SetSceneComposerStatus(issues.Length == 0
                ? "Библиотека ресурсов обновлена."
                : "Библиотека ресурсов обновлена с предупреждениями:\n" + string.Join("\n", issues),
                issues.Length == 0 ? MessageType.Info : MessageType.Warning);
        }

        public string[] ComposerGetAvailableBackgroundAssetIds()
        {
            return VnSceneComposerAssetLibrary.FindByPurpose(GetProjectRoot(), VnSceneComposerAssetPurpose.Background)
                .Where(entry => entry != null && !entry.missing && !string.IsNullOrWhiteSpace(entry.stableAssetId))
                .Select(entry => entry.stableAssetId)
                .ToArray();
        }

        public string[] ComposerGetAvailableBackgroundDisplayNames()
        {
            return VnSceneComposerAssetLibrary.FindByPurpose(GetProjectRoot(), VnSceneComposerAssetPurpose.Background)
                .Where(entry => entry != null && !entry.missing && !string.IsNullOrWhiteSpace(entry.stableAssetId))
                .Select(entry => string.IsNullOrWhiteSpace(entry.displayName) ? entry.stableAssetId : entry.displayName)
                .ToArray();
        }

        public void ComposerSetLibraryBackground(string assetId)
        {
            if (string.IsNullOrWhiteSpace(assetId))
                throw new ArgumentException("Background asset identity is required.", nameof(assetId));
            VnSceneComposerAssetEntry entry = VnSceneComposerAssetLibrary
                .FindByPurpose(GetProjectRoot(), VnSceneComposerAssetPurpose.Background)
                .FirstOrDefault(candidate => candidate != null && !candidate.missing &&
                    string.Equals(candidate.stableAssetId, assetId, StringComparison.Ordinal));
            if (entry == null)
                throw new ArgumentException("Unknown or invalid Scene Composer background asset: " + assetId, nameof(assetId));
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(entry.assetPath);
            if (texture == null)
                throw new InvalidOperationException("Scene Composer background texture is missing: " + entry.assetPath);
            ComposerSetExistingRokasAsset(texture);
        }

        public Texture2D ComposerGetLibraryAssetThumbnail(string assetId)
        {
            if (string.IsNullOrWhiteSpace(assetId)) return null;
            VnSceneComposerAssetEntry entry = Enum.GetValues(typeof(VnSceneComposerAssetPurpose))
                .Cast<VnSceneComposerAssetPurpose>()
                .SelectMany(purpose => VnSceneComposerAssetLibrary.FindByPurpose(GetProjectRoot(), purpose))
                .FirstOrDefault(candidate => candidate != null && !candidate.missing &&
                    string.Equals(candidate.stableAssetId, assetId, StringComparison.Ordinal));
            return entry == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(entry.assetPath);
        }

        private void DrawSceneComposerAssetLibraryControls(VnSceneComposerScene scene)
        {
            EditorGUILayout.Space();
            _sceneComposerAssetLibraryExpanded = EditorGUILayout.Foldout(
                _sceneComposerAssetLibraryExpanded, "Библиотека ресурсов", true);
            if (!_sceneComposerAssetLibraryExpanded) return;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Обновить")) ComposerRefreshAssets();
            if (GUILayout.Button("Открыть папку"))
            {
                string relative = VnSceneComposerAssetLibrary.ManagedRootRelative.Replace('/', Path.DirectorySeparatorChar);
                EditorUtility.RevealInFinder(Path.Combine(GetProjectRoot(), relative));
            }
            EditorGUILayout.EndHorizontal();

            UnityEngine.Object currentAsset = null;
            if (scene != null && scene.media != null &&
                scene.media.kind == VnSceneComposerMediaKind.ExistingRokasAsset &&
                !string.IsNullOrEmpty(scene.media.reference))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(scene.media.reference);
                if (!string.IsNullOrEmpty(assetPath))
                    currentAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            }
            EditorGUI.BeginChangeCheck();
            UnityEngine.Object nextAsset = EditorGUILayout.ObjectField("Ресурс проекта", currentAsset, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck() && nextAsset != null)
                TrySceneComposerMediaAction(() => ComposerSetExistingRokasAsset(nextAsset));

            VnSceneComposerAssetPurpose[] purposes = Enum.GetValues(typeof(VnSceneComposerAssetPurpose))
                .Cast<VnSceneComposerAssetPurpose>().ToArray();
            string[] purposeLabels = purposes.Select(GetSceneComposerAssetPurposeLabel).ToArray();
            int purposeIndex = Mathf.Max(0, Array.IndexOf(purposes, _sceneComposerOnboardPurpose));
            purposeIndex = EditorGUILayout.Popup("Назначение", purposeIndex, purposeLabels);
            _sceneComposerOnboardPurpose = purposes[Mathf.Clamp(purposeIndex, 0, purposes.Length - 1)];
            _sceneComposerOnboardDisplayName = EditorGUILayout.TextField("Название", _sceneComposerOnboardDisplayName ?? string.Empty);
            if (_sceneComposerOnboardPurpose == VnSceneComposerAssetPurpose.CharacterState)
            {
                _sceneComposerOnboardCharacter = EditorGUILayout.TextField("Персонаж", _sceneComposerOnboardCharacter ?? string.Empty);
                _sceneComposerOnboardState = EditorGUILayout.TextField("Поза / состояние", _sceneComposerOnboardState ?? string.Empty);
            }
            if (GUILayout.Button("Добавить изображение"))
            {
                string source = EditorUtility.OpenFilePanel("Добавить изображение в библиотеку", string.Empty, "png,jpg,jpeg");
                if (!string.IsNullOrEmpty(source))
                {
                    try
                    {
                        string display = string.IsNullOrWhiteSpace(_sceneComposerOnboardDisplayName)
                            ? Path.GetFileNameWithoutExtension(source)
                            : _sceneComposerOnboardDisplayName;
                        VnSceneComposerAssetOnboardResult result = VnSceneComposerAssetLibrary.Onboard(
                            GetProjectRoot(), source, _sceneComposerOnboardPurpose, display,
                            _sceneComposerOnboardPurpose == VnSceneComposerAssetPurpose.CharacterState ? _sceneComposerOnboardCharacter : string.Empty,
                            _sceneComposerOnboardPurpose == VnSceneComposerAssetPurpose.CharacterState ? _sceneComposerOnboardState : string.Empty);
                        if (!result.Success || result.Entry == null)
                            throw new InvalidOperationException(result.Error ?? "Не удалось добавить ресурс.");
                        ComposerRefreshAssets();
                        SetSceneComposerStatus((result.Duplicate ? "Ресурс уже зарегистрирован: " : "Ресурс добавлен: ") +
                            result.Entry.displayName + ".", result.Duplicate ? MessageType.Warning : MessageType.Info);
                    }
                    catch (Exception exception)
                    {
                        SetSceneComposerStatus("Не удалось добавить ресурс: " + exception.Message, MessageType.Error);
                    }
                }
            }

            string[] backgroundIds = ComposerGetAvailableBackgroundAssetIds();
            string[] backgroundNames = ComposerGetAvailableBackgroundDisplayNames();
            if (backgroundIds.Length > 0)
            {
                _sceneComposerBackgroundAssetIndex = Mathf.Clamp(_sceneComposerBackgroundAssetIndex, 0, backgroundIds.Length - 1);
                _sceneComposerBackgroundAssetIndex = EditorGUILayout.Popup(
                    "Фон", _sceneComposerBackgroundAssetIndex, backgroundNames);
                Texture2D thumbnail = ComposerGetLibraryAssetThumbnail(backgroundIds[_sceneComposerBackgroundAssetIndex]);
                if (thumbnail != null)
                {
                    Rect thumbRect = GUILayoutUtility.GetRect(120f, 72f, GUILayout.ExpandWidth(true));
                    GUI.DrawTexture(thumbRect, thumbnail, ScaleMode.ScaleAndCrop, true);
                }
                if (GUILayout.Button("Использовать выбранный фон"))
                    TrySceneComposerMediaAction(() => ComposerSetLibraryBackground(backgroundIds[_sceneComposerBackgroundAssetIndex]));
            }
            else
            {
                EditorGUILayout.LabelField("Фон", "Нет добавленных фонов");
            }
            EditorGUILayout.LabelField("Добавленные файлы остаются ресурсами редактора до отдельного явного переноса в игру.", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private static string GetSceneComposerAssetPurposeLabel(VnSceneComposerAssetPurpose purpose)
        {
            switch (purpose)
            {
                case VnSceneComposerAssetPurpose.CharacterState: return "Персонаж / поза";
                case VnSceneComposerAssetPurpose.Background: return "Фон";
                case VnSceneComposerAssetPurpose.ImageStill: return "Изображение сцены";
                case VnSceneComposerAssetPurpose.UiOverlay: return "Элемент интерфейса";
                case VnSceneComposerAssetPurpose.ReferenceImage: return "Референс";
                default: return purpose.ToString();
            }
        }
    }
}
