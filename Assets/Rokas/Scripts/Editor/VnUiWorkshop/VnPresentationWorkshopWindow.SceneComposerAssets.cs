using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed partial class VnPresentationWorkshopWindow
    {
        [SerializeField] private bool _sceneComposerAssetLibraryExpanded = true;
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
                    ? "Missing asset: " + (entry.assetPath ?? string.Empty)
                    : entry.warning)
                .Distinct()
                .ToArray();
            SetSceneComposerStatus(issues.Length == 0
                ? "Scene Composer Asset Library refreshed."
                : "Asset Library refreshed with warnings:\n" + string.Join("\n", issues),
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
                _sceneComposerAssetLibraryExpanded, "Asset Library / Onboard Asset", true);
            if (!_sceneComposerAssetLibraryExpanded) return;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Assets")) ComposerRefreshAssets();
            if (GUILayout.Button("Open Managed Folder"))
            {
                string relative = VnSceneComposerAssetLibrary.ManagedRootRelative.Replace('/', Path.DirectorySeparatorChar);
                EditorUtility.RevealInFinder(Path.Combine(GetProjectRoot(), relative));
            }
            EditorGUILayout.EndHorizontal();

            _sceneComposerOnboardPurpose = (VnSceneComposerAssetPurpose)EditorGUILayout.EnumPopup("Purpose", _sceneComposerOnboardPurpose);
            _sceneComposerOnboardDisplayName = EditorGUILayout.TextField("Display Name", _sceneComposerOnboardDisplayName ?? string.Empty);
            if (_sceneComposerOnboardPurpose == VnSceneComposerAssetPurpose.CharacterState)
            {
                _sceneComposerOnboardCharacter = EditorGUILayout.TextField("Character", _sceneComposerOnboardCharacter ?? string.Empty);
                _sceneComposerOnboardState = EditorGUILayout.TextField("State / Pose", _sceneComposerOnboardState ?? string.Empty);
            }
            if (GUILayout.Button("Onboard Image Asset"))
            {
                string source = EditorUtility.OpenFilePanel("Onboard Scene Composer Image", string.Empty, "png,jpg,jpeg");
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
                            throw new InvalidOperationException(result.Error ?? "Asset onboarding failed.");
                        ComposerRefreshAssets();
                        SetSceneComposerStatus((result.Duplicate ? "Already registered: " : "Onboarded asset: ") +
                            result.Entry.displayName + ".", result.Duplicate ? MessageType.Warning : MessageType.Info);
                    }
                    catch (Exception exception)
                    {
                        SetSceneComposerStatus("Asset onboarding failed: " + exception.Message, MessageType.Error);
                    }
                }
            }

            string[] backgroundIds = ComposerGetAvailableBackgroundAssetIds();
            string[] backgroundNames = ComposerGetAvailableBackgroundDisplayNames();
            if (backgroundIds.Length > 0)
            {
                _sceneComposerBackgroundAssetIndex = Mathf.Clamp(_sceneComposerBackgroundAssetIndex, 0, backgroundIds.Length - 1);
                _sceneComposerBackgroundAssetIndex = EditorGUILayout.Popup(
                    "Background", _sceneComposerBackgroundAssetIndex, backgroundNames);
                Texture2D thumbnail = ComposerGetLibraryAssetThumbnail(backgroundIds[_sceneComposerBackgroundAssetIndex]);
                if (thumbnail != null)
                {
                    Rect thumbRect = GUILayoutUtility.GetRect(120f, 72f, GUILayout.ExpandWidth(true));
                    GUI.DrawTexture(thumbRect, thumbnail, ScaleMode.ScaleAndCrop, true);
                }
                if (GUILayout.Button("Use Selected Background"))
                    TrySceneComposerMediaAction(() => ComposerSetLibraryBackground(backgroundIds[_sceneComposerBackgroundAssetIndex]));
            }
            else
            {
                EditorGUILayout.LabelField("Background", "(no onboarded backgrounds)");
            }
            EditorGUILayout.LabelField("Managed assets stay editor-only until a future explicit production-apply task.", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }
    }
}
