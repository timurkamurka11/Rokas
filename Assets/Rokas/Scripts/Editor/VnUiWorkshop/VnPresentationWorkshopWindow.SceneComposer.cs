using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Rokas.Presentation;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed partial class VnPresentationWorkshopWindow
    {
        private sealed class SceneComposerThumbnailCacheEntry : IDisposable
        {
            public string signature;
            public Texture texture;
            public IDisposable owner;

            public void Dispose()
            {
                if (owner != null) owner.Dispose();
                owner = null;
                texture = null;
                signature = null;
            }
        }

        [SerializeField] private bool _sceneComposerWorkspaceActive;
        [SerializeField] private VnSceneComposerProject _sceneComposerProject = new VnSceneComposerProject();
        [SerializeField] private string _sceneComposerSelectedSceneId = string.Empty;
        [SerializeField] private Vector2 _sceneComposerStoryboardScroll;
        [SerializeField] private Vector2 _sceneComposerInspectorScroll;
        [SerializeField] private bool _sceneComposerProjectDefaultsExpanded;
        [SerializeField] private bool _sceneComposerSceneOverridesExpanded;
        [SerializeField] private int _sceneComposerSavedProjectIndex;

        [NonSerialized] private VnSceneComposerPlaybackController _sceneComposerPlayback;
        [NonSerialized] private Dictionary<string, SceneComposerThumbnailCacheEntry> _sceneComposerThumbnailCache;
        [NonSerialized] private string _sceneComposerStatus = string.Empty;
        [NonSerialized] private MessageType _sceneComposerStatusType = MessageType.None;
        [NonSerialized] private double _sceneComposerLastPlaybackTick;
        [NonSerialized] private bool _sceneComposerUpdateRegistered;

        public void ActivateSceneComposerWorkspace()
        {
            EnsureSceneComposerProject();
            _sceneComposerWorkspaceActive = true;
            EnsureSelectedScene();
            EnsureSceneComposerEditorUpdateRegistered();
            _sceneComposerLastPlaybackTick = EditorApplication.timeSinceStartup;
            Repaint();
        }

        private void DeactivateSceneComposerWorkspace()
        {
            _sceneComposerWorkspaceActive = false;
            DisposeSceneComposerRuntimeResources();
            Repaint();
        }

        private void DrawWorkspaceModeToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            int current = _sceneComposerWorkspaceActive ? 1 : 0;
            int next = GUILayout.Toolbar(current, new[] { "Presentation Workshop", "Scene Composer" }, EditorStyles.toolbarButton);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Editor authoring only", EditorStyles.miniLabel, GUILayout.Width(125f));
            EditorGUILayout.EndHorizontal();
            if (next == current) return;
            if (next == 1) ActivateSceneComposerWorkspace();
            else DeactivateSceneComposerWorkspace();
        }

        public void DrawSceneComposerWorkspace()
        {
            EnsureSceneComposerProject();
            EnsureSelectedScene();
            EditorGUILayout.BeginHorizontal();
            DrawSceneComposerStoryboard();
            DrawSceneComposerPreview();
            DrawSceneComposerInspector();
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_sceneComposerStatus))
                EditorGUILayout.HelpBox(_sceneComposerStatus, _sceneComposerStatusType);
        }

        public void ComposerAddScene()
        {
            EnsureSceneComposerProject();
            RecordSceneComposerUndo("Add VN Scene");
            VnSceneComposerScene scene = VnSceneComposerEditing.AddScene(
                _sceneComposerProject, "Scene " + (_sceneComposerProject.scenes.Count + 1));
            _sceneComposerSelectedSceneId = scene.sceneId;
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        public void ComposerRenameSelectedScene(string label)
        {
            VnSceneComposerScene scene = GetSelectedScene();
            if (scene == null) return;
            RecordSceneComposerUndo("Rename VN Scene");
            VnSceneComposerEditing.RenameScene(_sceneComposerProject, scene.sceneId, label ?? string.Empty);
            MarkSceneComposerChanged();
        }

        public void ComposerDuplicateSelectedScene()
        {
            VnSceneComposerScene scene = GetSelectedScene();
            if (scene == null) return;
            RecordSceneComposerUndo("Duplicate VN Scene");
            VnSceneComposerScene copy = VnSceneComposerEditing.DuplicateScene(_sceneComposerProject, scene.sceneId);
            if (copy != null) _sceneComposerSelectedSceneId = copy.sceneId;
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        public void ComposerMoveSelectedScene(int direction)
        {
            VnSceneComposerScene scene = GetSelectedScene();
            if (scene == null || direction == 0) return;
            int source = FindSceneIndex(scene.sceneId);
            if (source < 0) return;
            int target = Mathf.Clamp(source + direction, 0, _sceneComposerProject.scenes.Count - 1);
            if (target == source) return;
            RecordSceneComposerUndo("Reorder VN Scene");
            VnSceneComposerEditing.MoveScene(_sceneComposerProject, scene.sceneId, target);
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        public void ComposerDeleteSelectedScene()
        {
            VnSceneComposerScene scene = GetSelectedScene();
            if (scene == null) return;
            int deletedIndex = FindSceneIndex(scene.sceneId);
            if (deletedIndex < 0) return;
            RecordSceneComposerUndo("Delete VN Scene");
            InvalidateSceneComposerThumbnail(scene.sceneId);
            VnSceneComposerEditing.DeleteScene(_sceneComposerProject, scene.sceneId);
            if (_sceneComposerProject.scenes.Count == 0) _sceneComposerSelectedSceneId = string.Empty;
            else
            {
                int nextIndex = Mathf.Clamp(deletedIndex, 0, _sceneComposerProject.scenes.Count - 1);
                _sceneComposerSelectedSceneId = _sceneComposerProject.scenes[nextIndex].sceneId;
            }
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        public void ComposerSelectScene(int index)
        {
            EnsureSceneComposerProject();
            if (index < 0 || index >= _sceneComposerProject.scenes.Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Scene index is outside the Composer project.");
            VnSceneComposerScene scene = _sceneComposerProject.scenes[index];
            if (scene == null) throw new InvalidOperationException("Selected Scene Composer scene is missing.");
            _sceneComposerSelectedSceneId = scene.sceneId;
            Repaint();
        }

        public void ComposerSetExistingRokasAsset(UnityEngine.Object asset)
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            VnSceneComposerMediaScaleMode scaleMode = scene.media != null ? scene.media.scaleMode : VnSceneComposerMediaScaleMode.Fit;
            RecordSceneComposerUndo("Choose VN Scene ROKAS Media");
            VnSceneComposerMediaEditing.SetExistingRokasAsset(scene, asset, scaleMode);
            InvalidateSceneComposerThumbnail(scene.sceneId);
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        private void ComposerSetExternalImage(string path)
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            VnSceneComposerMediaScaleMode scaleMode = scene.media != null ? scene.media.scaleMode : VnSceneComposerMediaScaleMode.Fit;
            RecordSceneComposerUndo("Load VN Scene Image");
            VnSceneComposerMediaEditing.SetExternalImage(scene, path, scaleMode);
            InvalidateSceneComposerThumbnail(scene.sceneId);
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        private void ComposerSetExternalVideo(string path)
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            VnSceneComposerMediaScaleMode scaleMode = scene.media != null ? scene.media.scaleMode : VnSceneComposerMediaScaleMode.Fit;
            bool loop = scene.media != null && scene.media.loop;
            RecordSceneComposerUndo("Load VN Scene Video");
            VnSceneComposerMediaEditing.SetExternalVideo(scene, path, scaleMode, loop);
            InvalidateSceneComposerThumbnail(scene.sceneId);
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        private void ComposerSetExternalGif(string path)
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            VnSceneComposerMediaScaleMode scaleMode = scene.media != null ? scene.media.scaleMode : VnSceneComposerMediaScaleMode.Fit;
            bool loop = scene.media != null && scene.media.loop;
            RecordSceneComposerUndo("Load VN Scene GIF");
            VnSceneComposerMediaEditing.SetExternalGif(scene, path, scaleMode, loop);
            InvalidateSceneComposerThumbnail(scene.sceneId);
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        private void ComposerClearMedia()
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            RecordSceneComposerUndo("Clear VN Scene Media");
            scene.media = new VnSceneComposerMediaReference();
            InvalidateSceneComposerThumbnail(scene.sceneId);
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        public VnWorkshopPreviewFrame ComposerBuildSelectedPreviewFrame()
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            int index = FindSceneIndex(scene.sceneId);
            Texture2D background = ComposerGetSceneThumbnail(index) as Texture2D;
            return VnSceneComposerComposition.BuildFrame(_sceneComposerProject, scene, previewResolution, background);
        }

        public void ComposerPlayScene()
        {
            int index = GetSelectedSceneIndexOrThrow();
            EnsureSceneComposerPlayback().PlayScene(index);
            BeginSceneComposerPlaybackTick();
        }

        public void ComposerPlayFromHere()
        {
            int index = GetSelectedSceneIndexOrThrow();
            EnsureSceneComposerPlayback().PlayFromHere(index);
            BeginSceneComposerPlaybackTick();
        }

        public void ComposerPlayAll()
        {
            EnsureSceneComposerPlayback().PlayAll();
            SyncSceneComposerSelectionFromPlayback();
            BeginSceneComposerPlaybackTick();
        }

        public void ComposerPause()
        {
            if (_sceneComposerPlayback == null) return;
            _sceneComposerPlayback.Pause();
            _sceneComposerLastPlaybackTick = EditorApplication.timeSinceStartup;
            Repaint();
        }

        public void ComposerRestart()
        {
            if (_sceneComposerPlayback == null) return;
            _sceneComposerPlayback.Restart();
            SyncSceneComposerSelectionFromPlayback();
            BeginSceneComposerPlaybackTick();
        }

        public void ComposerPrevious()
        {
            EnsureSceneComposerPlayback().Previous();
            SyncSceneComposerSelectionFromPlayback();
            BeginSceneComposerPlaybackTick();
        }

        public void ComposerNext()
        {
            EnsureSceneComposerPlayback().Next();
            SyncSceneComposerSelectionFromPlayback();
            BeginSceneComposerPlaybackTick();
        }

        public void ComposerSaveProject(string projectRoot)
        {
            EnsureSceneComposerProject();
            VnSceneComposerStorage.SaveProject(projectRoot, _sceneComposerProject);
            SetSceneComposerStatus("Saved " + VnSceneComposerStorage.ProjectFileName + ".", MessageType.Info);
        }

        public VnSceneComposerImportResult ComposerLoadProject(string projectRoot, string projectId)
        {
            VnSceneComposerImportResult result = VnSceneComposerStorage.LoadProject(projectRoot, projectId);
            if (result.Success && result.Project != null)
            {
                DisposeSceneComposerRuntimeResources();
                _sceneComposerProject = result.Project;
                _sceneComposerSelectedSceneId = _sceneComposerProject.scenes != null && _sceneComposerProject.scenes.Count > 0 && _sceneComposerProject.scenes[0] != null
                    ? _sceneComposerProject.scenes[0].sceneId : string.Empty;
                EnsureSceneComposerEditorUpdateRegistered();
                bool hasWarnings = result.Warnings != null && result.Warnings.Length > 0;
                SetSceneComposerStatus(hasWarnings ? string.Join("\n", result.Warnings) : "Loaded Scene Composer project.",
                    hasWarnings ? MessageType.Warning : MessageType.Info);
                Repaint();
            }
            else SetSceneComposerStatus("Load failed: " + result.Error, MessageType.Error);
            return result;
        }

        public string[] ComposerGetAuthoredStateIds(string character)
        {
            return VnSceneComposerCharacterStateResolver.GetStateIds(character);
        }

        public void ComposerAddCharacter(string character, string stateId)
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            if (scene.characters == null) scene.characters = new List<VnSceneComposerCharacter>();
            if (scene.characters.Count >= 3) throw new InvalidOperationException("Scene Composer supports at most three visible characters.");
            if (!VnSceneComposerCharacterStateResolver.TryResolve(stateId, out VnSceneComposerResolvedCharacterState state))
                throw new ArgumentException("Unknown authored VN character state: " + (stateId ?? string.Empty), nameof(stateId));
            if (!string.Equals(state.Character, character ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Authored state does not belong to selected character.", nameof(stateId));
            RecordSceneComposerUndo("Add VN Scene Character");
            scene.characters.Add(new VnSceneComposerCharacter
            {
                characterId = state.Character,
                stateId = state.Id,
                stageSlot = scene.characters.Count == 0 ? VnWorkshopStageSlot.Center :
                    (scene.characters.Count == 1 ? VnWorkshopStageSlot.Left : VnWorkshopStageSlot.Right)
            });
            ResetSceneComposerPlayback();
            MarkSceneComposerChanged();
        }

        public Texture ComposerGetSceneThumbnail(int sceneIndex)
        {
            EnsureSceneComposerProject();
            if (sceneIndex < 0 || sceneIndex >= _sceneComposerProject.scenes.Count) return null;
            VnSceneComposerScene scene = _sceneComposerProject.scenes[sceneIndex];
            if (scene == null || scene.media == null || scene.media.kind == VnSceneComposerMediaKind.None) return null;
            if (_sceneComposerThumbnailCache == null)
                _sceneComposerThumbnailCache = new Dictionary<string, SceneComposerThumbnailCacheEntry>(StringComparer.Ordinal);
            string signature = BuildSceneComposerMediaSignature(scene.media);
            SceneComposerThumbnailCacheEntry cached;
            if (_sceneComposerThumbnailCache.TryGetValue(scene.sceneId, out cached))
            {
                if (cached != null && string.Equals(cached.signature, signature, StringComparison.Ordinal)) return cached.texture;
                if (cached != null) cached.Dispose();
                _sceneComposerThumbnailCache.Remove(scene.sceneId);
            }
            var entry = new SceneComposerThumbnailCacheEntry { signature = signature };
            switch (scene.media.kind)
            {
                case VnSceneComposerMediaKind.ExistingRokasAsset:
                case VnSceneComposerMediaKind.ExternalImage:
                    VnSceneComposerImagePreview image = VnSceneComposerMediaEditing.OpenImagePreview(scene.media);
                    entry.owner = image; entry.texture = image.texture;
                    if (!string.IsNullOrEmpty(image.warning)) SetSceneComposerStatus(image.warning, MessageType.Warning);
                    break;
                case VnSceneComposerMediaKind.ExternalGif:
                    VnSceneComposerGifPreview gif = VnSceneComposerMediaEditing.OpenGifPreview(scene.media);
                    entry.owner = gif; entry.texture = gif.currentTexture;
                    if (!string.IsNullOrEmpty(gif.warning)) SetSceneComposerStatus(gif.warning, MessageType.Warning);
                    break;
                case VnSceneComposerMediaKind.ExternalVideo:
                    VnSceneComposerVideoPreview video = VnSceneComposerMediaEditing.OpenVideoPreview(scene.media, 640, 360);
                    entry.owner = video; entry.texture = video.texture;
                    if (!string.IsNullOrEmpty(video.warning)) SetSceneComposerStatus(video.warning, MessageType.Warning);
                    break;
            }
            _sceneComposerThumbnailCache[scene.sceneId] = entry;
            return entry.texture;
        }

        private void DrawSceneComposerStoryboard()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(250f));
            EditorGUILayout.LabelField("Storyboard", EditorStyles.boldLabel);
            if (GUILayout.Button("+ Add Scene")) ComposerAddScene();
            _sceneComposerStoryboardScroll = EditorGUILayout.BeginScrollView(_sceneComposerStoryboardScroll, GUILayout.ExpandHeight(true));
            if (_sceneComposerProject.scenes.Count == 0)
                EditorGUILayout.HelpBox("No scenes yet. Add a scene to begin authoring.", MessageType.Info);
            for (int i = 0; i < _sceneComposerProject.scenes.Count; i++)
            {
                VnSceneComposerScene scene = _sceneComposerProject.scenes[i];
                if (scene == null) continue;
                bool selected = string.Equals(scene.sceneId, _sceneComposerSelectedSceneId, StringComparison.Ordinal);
                EditorGUILayout.BeginVertical(selected ? "SelectionRect" : "box");
                Texture thumbnail = ComposerGetSceneThumbnail(i);
                if (thumbnail != null)
                {
                    Rect thumbnailRect = GUILayoutUtility.GetRect(220f, 100f, GUILayout.ExpandWidth(true));
                    GUI.DrawTexture(thumbnailRect, thumbnail, ScaleMode.ScaleAndCrop, true);
                }
                string label = string.IsNullOrWhiteSpace(scene.label) ? "Scene " + (i + 1) : scene.label;
                if (GUILayout.Button(label, selected ? EditorStyles.miniButtonMid : EditorStyles.miniButton)) ComposerSelectScene(i);
                string media = scene.media != null ? scene.media.kind.ToString() : "None";
                string detail = !string.IsNullOrEmpty(scene.speaker) ? scene.speaker : (scene.narration ? "Narration" : "No speaker");
                EditorGUILayout.LabelField(media + " · " + detail, EditorStyles.miniLabel);
                if (!string.IsNullOrEmpty(scene.previewText))
                {
                    string shortText = scene.previewText.Replace('\n', ' ');
                    if (shortText.Length > 42) shortText = shortText.Substring(0, 42) + "…";
                    EditorGUILayout.LabelField(shortText, EditorStyles.miniLabel);
                }
                if (selected)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Duplicate")) ComposerDuplicateSelectedScene();
                    if (GUILayout.Button("Delete")) ComposerDeleteSelectedScene();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    using (new EditorGUI.DisabledScope(i == 0)) if (GUILayout.Button("Move Up")) ComposerMoveSelectedScene(-1);
                    using (new EditorGUI.DisabledScope(i >= _sceneComposerProject.scenes.Count - 1)) if (GUILayout.Button("Move Down")) ComposerMoveSelectedScene(1);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSceneComposerPreview()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField("Scene Preview", EditorStyles.boldLabel);
            VnWorkshopPreviewFrame frame = null;
            try
            {
                if (_sceneComposerPlayback != null && _sceneComposerPlayback.CurrentFrame != null) frame = _sceneComposerPlayback.CurrentFrame.WorkshopFrame;
                if (frame == null && GetSelectedScene() != null) frame = ComposerBuildSelectedPreviewFrame();
            }
            catch (Exception exception) { SetSceneComposerStatus("Preview failed: " + exception.Message, MessageType.Error); }
            float previewHeight = Mathf.Max(300f, position.height - 145f);
            Rect previewRect = EditorGUILayout.GetControlRect(false, previewHeight, GUILayout.ExpandWidth(true));
            if (frame != null) VnPresentationWorkshopPreviewRenderer.Draw(previewRect, frame, null, false);
            else GUI.Box(previewRect, "Select or add a scene to preview.");
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(_sceneComposerProject.scenes.Count == 0))
            {
                if (GUILayout.Button("Previous")) ComposerPrevious();
                if (GUILayout.Button("Play Scene")) ComposerPlayScene();
                if (GUILayout.Button("Play From Here")) ComposerPlayFromHere();
                if (GUILayout.Button("Play All")) ComposerPlayAll();
                if (GUILayout.Button("Pause")) ComposerPause();
                if (GUILayout.Button("Restart")) ComposerRestart();
                if (GUILayout.Button("Next")) ComposerNext();
            }
            EditorGUILayout.EndHorizontal();
            if (_sceneComposerPlayback != null && _sceneComposerPlayback.CurrentSceneIndex >= 0)
            {
                string state = _sceneComposerPlayback.IsPlaying ? "Playing" : "Paused / stopped";
                EditorGUILayout.LabelField(state + " · Scene " + (_sceneComposerPlayback.CurrentSceneIndex + 1) + " · " +
                    _sceneComposerPlayback.SceneElapsedSeconds.ToString("0.00") + "s", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawSceneComposerInspector()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(370f));
            _sceneComposerInspectorScroll = EditorGUILayout.BeginScrollView(_sceneComposerInspectorScroll, GUILayout.ExpandHeight(true));
            DrawSceneComposerProjectStorage();
            VnSceneComposerScene scene = GetSelectedScene();
            if (scene == null)
            {
                EditorGUILayout.HelpBox("No scene selected.", MessageType.Info);
                EditorGUILayout.EndScrollView(); EditorGUILayout.EndVertical(); return;
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Selected Scene", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            string label = EditorGUILayout.TextField("Scene Label", scene.label ?? string.Empty);
            if (EditorGUI.EndChangeCheck()) ComposerRenameSelectedScene(label);
            DrawSceneComposerAssetLibraryControls(scene);
            DrawSceneComposerMediaInspector(scene);
            DrawSceneComposerTextInspector(scene);
            DrawSceneComposerCharacterInspector(scene);
            DrawSceneComposerPresentationInspector(scene);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSceneComposerProjectStorage()
        {
            EditorGUILayout.LabelField("Scene Composer Project", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            string title = EditorGUILayout.TextField("Title", _sceneComposerProject.title ?? string.Empty);
            if (EditorGUI.EndChangeCheck())
            {
                RecordSceneComposerUndo("Rename Scene Composer Project");
                _sceneComposerProject.title = title;
                MarkSceneComposerChanged();
            }
            EditorGUILayout.LabelField("Project ID", _sceneComposerProject.projectId, EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Project"))
            {
                try { ComposerSaveProject(GetProjectRoot()); }
                catch (Exception exception) { SetSceneComposerStatus("Save failed: " + exception.Message, MessageType.Error); }
            }
            string[] saved = ListSceneComposerProjectIds();
            using (new EditorGUI.DisabledScope(saved.Length == 0))
            {
                if (GUILayout.Button("Load Project"))
                {
                    _sceneComposerSavedProjectIndex = Mathf.Clamp(_sceneComposerSavedProjectIndex, 0, Mathf.Max(0, saved.Length - 1));
                    if (saved.Length > 0) ComposerLoadProject(GetProjectRoot(), saved[_sceneComposerSavedProjectIndex]);
                }
            }
            EditorGUILayout.EndHorizontal();
            if (saved.Length > 0)
            {
                _sceneComposerSavedProjectIndex = Mathf.Clamp(_sceneComposerSavedProjectIndex, 0, saved.Length - 1);
                _sceneComposerSavedProjectIndex = EditorGUILayout.Popup("Saved Project", _sceneComposerSavedProjectIndex, saved);
            }
            EditorGUILayout.LabelField("Stored under Library/ROKAS/VnSceneComposer only.", EditorStyles.miniLabel);
        }

        private void DrawSceneComposerMediaInspector(VnSceneComposerScene scene)
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Media", EditorStyles.boldLabel);
            if (scene.media == null) scene.media = new VnSceneComposerMediaReference();
            EditorGUILayout.LabelField("Type", scene.media.kind.ToString());
            EditorGUILayout.LabelField("Current", string.IsNullOrEmpty(scene.media.displayName) ? "(none)" : scene.media.displayName);
            UnityEngine.Object currentAsset = null;
            if (scene.media.kind == VnSceneComposerMediaKind.ExistingRokasAsset && !string.IsNullOrEmpty(scene.media.reference))
            {
                string path = AssetDatabase.GUIDToAssetPath(scene.media.reference);
                if (!string.IsNullOrEmpty(path)) currentAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            }
            EditorGUI.BeginChangeCheck();
            UnityEngine.Object nextAsset = EditorGUILayout.ObjectField("Choose ROKAS Asset", currentAsset, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck() && nextAsset != null)
                TrySceneComposerMediaAction(() => ComposerSetExistingRokasAsset(nextAsset));
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load Image"))
            {
                string path = EditorUtility.OpenFilePanel("Load Scene Image", string.Empty, "png,jpg,jpeg");
                if (!string.IsNullOrEmpty(path)) TrySceneComposerMediaAction(() => ComposerSetExternalImage(path));
            }
            if (GUILayout.Button("Load Video"))
            {
                string path = EditorUtility.OpenFilePanel("Load Scene Video", string.Empty, "mp4,mov,m4v,webm");
                if (!string.IsNullOrEmpty(path)) TrySceneComposerMediaAction(() => ComposerSetExternalVideo(path));
            }
            if (GUILayout.Button("Load GIF"))
            {
                string path = EditorUtility.OpenFilePanel("Load Scene GIF", string.Empty, "gif");
                if (!string.IsNullOrEmpty(path)) TrySceneComposerMediaAction(() => ComposerSetExternalGif(path));
            }
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Clear / Remove Media")) ComposerClearMedia();
            EditorGUI.BeginChangeCheck();
            VnSceneComposerMediaScaleMode scaleMode = (VnSceneComposerMediaScaleMode)EditorGUILayout.EnumPopup("Scale", scene.media.scaleMode);
            bool loop = scene.media.loop;
            if (scene.media.kind == VnSceneComposerMediaKind.ExternalGif || scene.media.kind == VnSceneComposerMediaKind.ExternalVideo)
                loop = EditorGUILayout.Toggle("Loop", loop);
            if (EditorGUI.EndChangeCheck())
            {
                RecordSceneComposerUndo("Edit VN Scene Media"); scene.media.scaleMode = scaleMode; scene.media.loop = loop;
                InvalidateSceneComposerThumbnail(scene.sceneId); ResetSceneComposerPlayback(); MarkSceneComposerChanged();
            }
            string warning = GetSceneComposerMediaWarning(scene.media);
            if (!string.IsNullOrEmpty(warning)) EditorGUILayout.HelpBox(warning, MessageType.Warning);
            else if (scene.media.localPreviewDependency)
                EditorGUILayout.HelpBox("External media is local to this editor machine and is not copied into production Assets.", MessageType.Info);
        }

        private void DrawSceneComposerTextInspector(VnSceneComposerScene scene)
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Text / Narration", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            bool narration = EditorGUILayout.Toggle("Narration / No Character", scene.narration);
            string speaker = EditorGUILayout.TextField("Speaker Name", scene.speaker ?? string.Empty);
            EditorGUILayout.LabelField("Dialogue / Narration");
            string text = EditorGUILayout.TextArea(scene.previewText ?? string.Empty, GUILayout.MinHeight(70f));
            if (EditorGUI.EndChangeCheck())
            {
                RecordSceneComposerUndo("Edit VN Scene Text"); scene.narration = narration;
                scene.speaker = narration ? string.Empty : speaker; scene.previewText = text;
                ResetSceneComposerPlayback(); MarkSceneComposerChanged();
            }
        }

        private void DrawSceneComposerCharacterInspector(VnSceneComposerScene scene)
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Characters", EditorStyles.boldLabel);
            if (scene.characters == null) scene.characters = new List<VnSceneComposerCharacter>();
            string[] characters = VnSceneComposerCharacterStateResolver.GetCharacters();
            if (scene.characters.Count < 3 && characters.Length > 0 && GUILayout.Button("+ Add Character"))
            {
                string[] states = ComposerGetAuthoredStateIds(characters[0]);
                if (states.Length > 0) ComposerAddCharacter(characters[0], states[0]);
            }
            for (int i = 0; i < scene.characters.Count; i++)
            {
                VnSceneComposerCharacter character = scene.characters[i];
                if (character == null) continue;
                EditorGUILayout.BeginVertical("box");
                int characterIndex = Mathf.Max(0, Array.FindIndex(characters,
                    name => string.Equals(name, character.characterId, StringComparison.OrdinalIgnoreCase)));
                EditorGUI.BeginChangeCheck();
                characterIndex = characters.Length > 0 ? EditorGUILayout.Popup("Character", characterIndex, characters) : 0;
                string nextCharacter = characters.Length > 0 ? characters[characterIndex] : character.characterId;
                string[] states = ComposerGetAuthoredStateIds(nextCharacter);
                int stateIndex = Mathf.Max(0, Array.IndexOf(states, character.stateId));
                stateIndex = states.Length > 0 ? EditorGUILayout.Popup("Authored State / Pose", stateIndex, states) : 0;
                string nextState = states.Length > 0 ? states[stateIndex] : character.stateId;
                VnWorkshopStageSlot slot = (VnWorkshopStageSlot)EditorGUILayout.EnumPopup("Stage Slot", character.stageSlot);
                if (EditorGUI.EndChangeCheck())
                {
                    RecordSceneComposerUndo("Edit VN Scene Character"); character.characterId = nextCharacter;
                    character.stateId = nextState; character.stageSlot = slot;
                    ResetSceneComposerPlayback(); MarkSceneComposerChanged();
                }
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Set Active Speaker"))
                {
                    RecordSceneComposerUndo("Set VN Scene Speaker"); scene.narration = false; scene.speaker = character.characterId;
                    ResetSceneComposerPlayback(); MarkSceneComposerChanged();
                }
                if (GUILayout.Button("Remove"))
                {
                    RecordSceneComposerUndo("Remove VN Scene Character"); scene.characters.RemoveAt(i);
                    ResetSceneComposerPlayback(); MarkSceneComposerChanged();
                    EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical(); break;
                }
                EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical();
            }
            if (characters.Length == 0)
                EditorGUILayout.HelpBox("No authored VN character states are available in the current catalog or Composer Asset Library.", MessageType.Warning);
        }

        private void DrawSceneComposerPresentationInspector(VnSceneComposerScene scene)
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("VN10 Presentation", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Project Defaults → Scene Overrides", EditorStyles.miniLabel);
            SerializedObject serialized = new SerializedObject(this); serialized.Update();
            SerializedProperty project = serialized.FindProperty("_sceneComposerProject");
            if (project != null)
            {
                SerializedProperty defaults = project.FindPropertyRelative("defaultPresentation");
                _sceneComposerProjectDefaultsExpanded = EditorGUILayout.Foldout(_sceneComposerProjectDefaultsExpanded, "Project Defaults", true);
                if (_sceneComposerProjectDefaultsExpanded && defaults != null) EditorGUILayout.PropertyField(defaults, true);
                int sceneIndex = FindSceneIndex(scene.sceneId);
                SerializedProperty scenes = project.FindPropertyRelative("scenes");
                SerializedProperty sceneProperty = scenes != null && sceneIndex >= 0 && sceneIndex < scenes.arraySize ? scenes.GetArrayElementAtIndex(sceneIndex) : null;
                SerializedProperty overrides = sceneProperty != null ? sceneProperty.FindPropertyRelative("presentationOverrides") : null;
                _sceneComposerSceneOverridesExpanded = EditorGUILayout.Foldout(_sceneComposerSceneOverridesExpanded, "Scene Overrides", true);
                if (_sceneComposerSceneOverridesExpanded && overrides != null) EditorGUILayout.PropertyField(overrides, true);
            }
            if (serialized.ApplyModifiedProperties()) { ResetSceneComposerPlayback(); Repaint(); }
            EditorGUI.BeginChangeCheck();
            bool bounce = scene.transition != null && scene.transition.triggerActionBounce;
            bounce = EditorGUILayout.Toggle("Trigger Action Bounce", bounce);
            VnSceneComposerPreviewAdvanceMode advance = scene.timing != null ? scene.timing.previewAdvanceMode : VnSceneComposerPreviewAdvanceMode.ManualBeat;
            advance = (VnSceneComposerPreviewAdvanceMode)EditorGUILayout.EnumPopup("Preview Advance", advance);
            float duration = scene.timing != null ? scene.timing.previewAutoDuration : 2f;
            if (advance == VnSceneComposerPreviewAdvanceMode.PreviewAutoDuration) duration = EditorGUILayout.FloatField("Auto Duration", duration);
            if (EditorGUI.EndChangeCheck())
            {
                RecordSceneComposerUndo("Edit VN Scene Timing");
                if (scene.transition == null) scene.transition = new VnSceneComposerTransition();
                if (scene.timing == null) scene.timing = new VnSceneComposerTiming();
                scene.transition.triggerActionBounce = bounce; scene.timing.previewAdvanceMode = advance;
                scene.timing.previewAutoDuration = Mathf.Max(0f, duration); ResetSceneComposerPlayback(); MarkSceneComposerChanged();
            }
        }

        private void EnsureSceneComposerProject()
        {
            if (_sceneComposerProject == null) _sceneComposerProject = new VnSceneComposerProject();
            if (_sceneComposerProject.scenes == null) _sceneComposerProject.scenes = new List<VnSceneComposerScene>();
            if (_sceneComposerProject.defaultPresentation == null) _sceneComposerProject.defaultPresentation = new VnPresentationWorkshopPreset();
        }

        private VnSceneComposerScene GetSelectedScene()
        {
            EnsureSceneComposerProject(); int index = FindSceneIndex(_sceneComposerSelectedSceneId);
            return index >= 0 ? _sceneComposerProject.scenes[index] : null;
        }

        private VnSceneComposerScene RequireSelectedScene()
        {
            VnSceneComposerScene scene = GetSelectedScene();
            if (scene == null) throw new InvalidOperationException("No Scene Composer scene is selected.");
            return scene;
        }

        private void EnsureSelectedScene()
        {
            EnsureSceneComposerProject();
            if (GetSelectedScene() != null) return;
            _sceneComposerSelectedSceneId = _sceneComposerProject.scenes.Count > 0 && _sceneComposerProject.scenes[0] != null
                ? _sceneComposerProject.scenes[0].sceneId : string.Empty;
        }

        private int FindSceneIndex(string sceneId)
        {
            if (_sceneComposerProject == null || _sceneComposerProject.scenes == null || string.IsNullOrEmpty(sceneId)) return -1;
            for (int i = 0; i < _sceneComposerProject.scenes.Count; i++)
            {
                VnSceneComposerScene scene = _sceneComposerProject.scenes[i];
                if (scene != null && string.Equals(scene.sceneId, sceneId, StringComparison.Ordinal)) return i;
            }
            return -1;
        }

        private int GetSelectedSceneIndexOrThrow()
        {
            EnsureSelectedScene(); int index = FindSceneIndex(_sceneComposerSelectedSceneId);
            if (index < 0) throw new InvalidOperationException("No Scene Composer scene is selected.");
            return index;
        }

        private VnSceneComposerPlaybackController EnsureSceneComposerPlayback()
        {
            EnsureSceneComposerProject();
            if (_sceneComposerPlayback == null) _sceneComposerPlayback = new VnSceneComposerPlaybackController(_sceneComposerProject);
            EnsureSceneComposerEditorUpdateRegistered(); return _sceneComposerPlayback;
        }

        private void ResetSceneComposerPlayback()
        {
            if (_sceneComposerPlayback != null) { _sceneComposerPlayback.Dispose(); _sceneComposerPlayback = null; }
            _sceneComposerLastPlaybackTick = EditorApplication.timeSinceStartup;
        }

        private void BeginSceneComposerPlaybackTick()
        {
            EnsureSceneComposerEditorUpdateRegistered(); _sceneComposerLastPlaybackTick = EditorApplication.timeSinceStartup;
            SyncSceneComposerSelectionFromPlayback(); Repaint();
        }

        private void EnsureSceneComposerEditorUpdateRegistered()
        {
            if (_sceneComposerUpdateRegistered) return;
            EditorApplication.update += SceneComposerEditorUpdate; _sceneComposerUpdateRegistered = true;
        }

        private void SceneComposerEditorUpdate()
        {
            if (this == null) { EditorApplication.update -= SceneComposerEditorUpdate; return; }
            if (!_sceneComposerWorkspaceActive || _sceneComposerPlayback == null || !_sceneComposerPlayback.IsPlaying)
            {
                _sceneComposerLastPlaybackTick = EditorApplication.timeSinceStartup; return;
            }
            double now = EditorApplication.timeSinceStartup;
            float delta = Mathf.Max(0f, (float)(now - _sceneComposerLastPlaybackTick)); _sceneComposerLastPlaybackTick = now;
            try { _sceneComposerPlayback.Advance(delta); SyncSceneComposerSelectionFromPlayback(); Repaint(); }
            catch (Exception exception)
            {
                _sceneComposerPlayback.Pause(); SetSceneComposerStatus("Playback preview failed: " + exception.Message, MessageType.Error);
            }
        }

        private void SyncSceneComposerSelectionFromPlayback()
        {
            if (_sceneComposerPlayback == null) return;
            int index = _sceneComposerPlayback.CurrentSceneIndex;
            if (index >= 0 && index < _sceneComposerProject.scenes.Count && _sceneComposerProject.scenes[index] != null)
                _sceneComposerSelectedSceneId = _sceneComposerProject.scenes[index].sceneId;
        }

        private void DisposeSceneComposerRuntimeResources()
        {
            if (_sceneComposerUpdateRegistered)
            {
                EditorApplication.update -= SceneComposerEditorUpdate; _sceneComposerUpdateRegistered = false;
            }
            ResetSceneComposerPlayback(); ClearSceneComposerThumbnailCache();
        }

        private void ClearSceneComposerThumbnailCache()
        {
            if (_sceneComposerThumbnailCache == null) return;
            foreach (SceneComposerThumbnailCacheEntry entry in _sceneComposerThumbnailCache.Values) if (entry != null) entry.Dispose();
            _sceneComposerThumbnailCache.Clear();
        }

        private void InvalidateSceneComposerThumbnail(string sceneId)
        {
            if (_sceneComposerThumbnailCache == null || string.IsNullOrEmpty(sceneId)) return;
            SceneComposerThumbnailCacheEntry entry;
            if (_sceneComposerThumbnailCache.TryGetValue(sceneId, out entry) && entry != null) entry.Dispose();
            _sceneComposerThumbnailCache.Remove(sceneId);
        }

        private static string BuildSceneComposerMediaSignature(VnSceneComposerMediaReference media)
        {
            if (media == null) return "none";
            return media.kind + "|" + (media.reference ?? string.Empty) + "|" + (media.contentHash ?? string.Empty) + "|" + media.loop + "|" + media.scaleMode;
        }

        private void RecordSceneComposerUndo(string label) { Undo.RegisterCompleteObjectUndo(this, label); }
        private void MarkSceneComposerChanged() { EditorUtility.SetDirty(this); Repaint(); }
        private void SetSceneComposerStatus(string message, MessageType type) { _sceneComposerStatus = message ?? string.Empty; _sceneComposerStatusType = type; Repaint(); }

        private void TrySceneComposerMediaAction(Action action)
        {
            try { action(); SetSceneComposerStatus("Scene media updated.", MessageType.Info); }
            catch (Exception exception) { SetSceneComposerStatus(exception.Message, MessageType.Error); }
        }

        private string GetSceneComposerMediaWarning(VnSceneComposerMediaReference media)
        {
            if (media == null) return "Scene media reference is missing.";
            if (media.kind == VnSceneComposerMediaKind.None) return string.Empty;
            if (media.kind == VnSceneComposerMediaKind.ExistingRokasAsset)
            {
                if (string.IsNullOrEmpty(media.reference)) return "ROKAS asset GUID is missing.";
                return string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(media.reference)) ? "ROKAS asset reference is missing or invalid." : string.Empty;
            }
            if (VnSceneComposerSerialization.IsExternalMedia(media.kind) &&
                (string.IsNullOrEmpty(media.reference) || VnSceneComposerSerialization.IsPortableExternalReference(media.reference) ||
                 !Path.IsPathRooted(media.reference) || !File.Exists(media.reference)))
                return "External media is missing or not locally bound on this machine.";
            return string.Empty;
        }

        private string[] ListSceneComposerProjectIds()
        {
            try
            {
                string directory = VnSceneComposerStorage.GetProjectsDirectory(GetProjectRoot());
                if (!Directory.Exists(directory)) return Array.Empty<string>();
                return Directory.GetDirectories(directory).Select(Path.GetFileName).Where(VnSceneComposerSerialization.IsStableId)
                    .OrderBy(id => id, StringComparer.Ordinal).ToArray();
            }
            catch (Exception exception)
            {
                SetSceneComposerStatus("Could not list saved Composer projects: " + exception.Message, MessageType.Warning);
                return Array.Empty<string>();
            }
        }

        private static VnCharacterVisualState[] EnumerateAuthoredCatalogStates()
        {
            FieldInfo statesField = typeof(Rokas.Presentation.VnCharacterVisualCatalog).GetField("States", BindingFlags.NonPublic | BindingFlags.Static);
            IEnumerable entries = statesField != null ? statesField.GetValue(null) as IEnumerable : null;
            if (entries == null) return Array.Empty<VnCharacterVisualState>();
            var result = new List<VnCharacterVisualState>();
            foreach (object pair in entries)
            {
                if (pair == null) continue;
                PropertyInfo valueProperty = pair.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                object boxed = valueProperty != null ? valueProperty.GetValue(pair, null) : null;
                if (boxed is VnCharacterVisualState)
                {
                    VnCharacterVisualState state = (VnCharacterVisualState)boxed;
                    if (!string.IsNullOrEmpty(state.Id)) result.Add(state);
                }
            }
            return result.OrderBy(state => state.Id, StringComparer.Ordinal).ToArray();
        }
    }
}
