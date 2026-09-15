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

        private static readonly string[] SceneComposerInspectorSections =
        {
            "Фон",
            "Персонажи",
            "Текст",
            "Анимация персонажа",
            "Анимация сцены",
            "Медиа",
            "Настройки сцены",
            "Дополнительно"
        };

        private const float SceneComposerStoryboardWidth = 224f;
        private const float SceneComposerInspectorWidth = 360f;
        private const float SceneComposerSceneThumbnailWidth = 72f;
        private const float SceneComposerSceneThumbnailHeight = 48f;

        private static GUIStyle _sceneComposerPrimaryTransportButtonStyle;
        private static GUIStyle SceneComposerPrimaryTransportButtonStyle
        {
            get
            {
                if (_sceneComposerPrimaryTransportButtonStyle == null)
                {
                    _sceneComposerPrimaryTransportButtonStyle = new GUIStyle(EditorStyles.toolbarButton)
                    {
                        fontStyle = FontStyle.Bold
                    };
                }
                return _sceneComposerPrimaryTransportButtonStyle;
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
        [SerializeField] private int _sceneComposerInspectorSection;

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
            comparisonView = VnWorkshopComparisonView.Current;
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
            DrawSceneComposerProjectStorage();
            if (!string.IsNullOrEmpty(_sceneComposerStatus))
                EditorGUILayout.HelpBox(_sceneComposerStatus, _sceneComposerStatusType);
            EditorGUILayout.Space(2f);
            EditorGUILayout.BeginHorizontal();
            DrawSceneComposerStoryboard();
            GUILayout.Space(4f);
            DrawSceneComposerPreview();
            GUILayout.Space(4f);
            DrawSceneComposerInspector();
            EditorGUILayout.EndHorizontal();
        }

        public void ComposerAddScene()
        {
            EnsureSceneComposerProject();
            RecordSceneComposerUndo("Add VN Scene");
            VnSceneComposerScene scene = VnSceneComposerEditing.AddScene(
                _sceneComposerProject, "Scene " + (_sceneComposerProject.scenes.Count + 1));
            _sceneComposerSelectedSceneId = scene.sceneId;
            _sceneComposerSelectedCharacterIndex = -1;
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
            _sceneComposerSelectedCharacterIndex = -1;
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
            _sceneComposerSelectedCharacterIndex = -1;
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
            _sceneComposerSelectedCharacterIndex = -1;
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
            SetSceneComposerStatus("Проект сохранён: " + VnSceneComposerStorage.ProjectFileName + ".", MessageType.Info);
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
                _sceneComposerSelectedCharacterIndex = -1;
                EnsureSceneComposerEditorUpdateRegistered();
                bool hasWarnings = result.Warnings != null && result.Warnings.Length > 0;
                SetSceneComposerStatus(hasWarnings ? string.Join("\n", result.Warnings) : "Проект загружен.",
                    hasWarnings ? MessageType.Warning : MessageType.Info);
                Repaint();
            }
            else SetSceneComposerStatus("Не удалось загрузить проект: " + result.Error, MessageType.Error);
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
            _sceneComposerSelectedCharacterIndex = scene.characters.Count - 1;
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
            EditorGUILayout.BeginVertical(GUILayout.Width(SceneComposerStoryboardWidth));
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Сцены", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(_sceneComposerProject.scenes.Count.ToString(), EditorStyles.miniLabel, GUILayout.Width(22f));
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("+ Добавить сцену", GUILayout.Height(28f))) ComposerAddScene();
            EditorGUILayout.Space(2f);
            _sceneComposerStoryboardScroll = EditorGUILayout.BeginScrollView(_sceneComposerStoryboardScroll, GUILayout.ExpandHeight(true));
            if (_sceneComposerProject.scenes.Count == 0)
                EditorGUILayout.HelpBox("Создайте первую сцену.", MessageType.Info);
            for (int i = 0; i < _sceneComposerProject.scenes.Count; i++)
            {
                VnSceneComposerScene scene = _sceneComposerProject.scenes[i];
                if (scene == null) continue;
                bool selected = string.Equals(scene.sceneId, _sceneComposerSelectedSceneId, StringComparison.Ordinal);
                EditorGUILayout.BeginVertical(selected ? "SelectionRect" : "box");
                EditorGUILayout.BeginHorizontal();
                Texture thumbnail = ComposerGetSceneThumbnail(i);
                Rect thumbnailRect = GUILayoutUtility.GetRect(
                    SceneComposerSceneThumbnailWidth,
                    SceneComposerSceneThumbnailHeight,
                    GUILayout.Width(SceneComposerSceneThumbnailWidth),
                    GUILayout.Height(SceneComposerSceneThumbnailHeight));
                if (thumbnail != null)
                    GUI.DrawTexture(thumbnailRect, thumbnail, ScaleMode.ScaleAndCrop, true);
                else
                {
                    GUI.Box(thumbnailRect, GUIContent.none);
                    GUI.Label(thumbnailRect, "Без фона", EditorStyles.centeredGreyMiniLabel);
                }

                EditorGUILayout.BeginVertical();
                string label = GetSceneComposerDisplayLabel(scene, i);
                string numberedLabel = (i + 1).ToString("00") + "  " + label;
                if (GUILayout.Button(numberedLabel, selected ? EditorStyles.miniButtonMid : EditorStyles.miniButton)) ComposerSelectScene(i);
                string media = GetSceneComposerMediaKindLabel(scene.media != null ? scene.media.kind : VnSceneComposerMediaKind.None);
                string detail = !string.IsNullOrEmpty(scene.speaker) ? scene.speaker : (scene.narration ? "Текст без персонажа" : "Без говорящего");
                EditorGUILayout.LabelField(media + " · " + detail, EditorStyles.miniLabel);
                if (!string.IsNullOrEmpty(scene.previewText))
                {
                    string shortText = scene.previewText.Replace('\n', ' ');
                    if (shortText.Length > 30) shortText = shortText.Substring(0, 30) + "…";
                    EditorGUILayout.LabelField(shortText, EditorStyles.miniLabel);
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();

                if (selected)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Дублировать", EditorStyles.miniButtonLeft)) ComposerDuplicateSelectedScene();
                    if (GUILayout.Button("Удалить", EditorStyles.miniButtonMid)) ComposerDeleteSelectedScene();
                    using (new EditorGUI.DisabledScope(i == 0))
                    {
                        if (GUILayout.Button("↑", EditorStyles.miniButtonMid, GUILayout.Width(26f))) ComposerMoveSelectedScene(-1);
                    }
                    using (new EditorGUI.DisabledScope(i >= _sceneComposerProject.scenes.Count - 1))
                    {
                        if (GUILayout.Button("↓", EditorStyles.miniButtonRight, GUILayout.Width(26f))) ComposerMoveSelectedScene(1);
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2f);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSceneComposerPreview()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Предпросмотр сцены", EditorStyles.boldLabel);
            VnSceneComposerScene selectedScene = GetSelectedScene();
            if (selectedScene != null)
            {
                GUILayout.FlexibleSpace();
                int selectedIndex = FindSceneIndex(selectedScene.sceneId);
                EditorGUILayout.LabelField(GetSceneComposerDisplayLabel(selectedScene, Mathf.Max(0, selectedIndex)),
                    EditorStyles.miniLabel, GUILayout.MaxWidth(180f));
            }
            EditorGUILayout.EndHorizontal();

            VnWorkshopPreviewFrame frame = null;
            bool staticAuthoringPreview = _sceneComposerPlayback == null ||
                                          !_sceneComposerPlayback.IsPlaying ||
                                          _sceneComposerPlayback.CurrentFrame == null;
            try
            {
                if (!staticAuthoringPreview) frame = _sceneComposerPlayback.CurrentFrame.WorkshopFrame;
                if (frame == null && selectedScene != null)
                {
                    ComposerPrepareSelectedVideoForAuthoring();
                    frame = ComposerBuildSelectedPreviewFrameForDisplay();
                    staticAuthoringPreview = true;
                }
            }
            catch (Exception exception) { SetSceneComposerStatus("Не удалось обновить предпросмотр: " + exception.Message, MessageType.Error); }
            float previewHeight = Mathf.Max(300f, position.height - 178f);
            Rect previewRect = EditorGUILayout.GetControlRect(false, previewHeight, GUILayout.ExpandWidth(true));
            if (frame != null)
            {
                bool advancedLayout = staticAuthoringPreview && IsSceneComposerAdvancedLayoutEditingVisible();
                bool advancedUiFeedback = staticAuthoringPreview && IsSceneComposerAdvancedUiFeedbackPreviewVisible();
                VnWorkshopElement? selectedUi = advancedLayout && _sceneComposerSelectedCharacterIndex < 0
                    ? (VnWorkshopElement?)selectedElement : null;
                VnWorkshopElement? uiFeedbackElement = null;
                VnWorkshopUiFeedbackSample? uiFeedbackSample = null;
                if (advancedUiFeedback && comparisonView == VnWorkshopComparisonView.Current)
                {
                    if (!IsSceneComposerUiFeedbackElement(_sceneComposerUiFeedbackPreviewElement))
                        _sceneComposerUiFeedbackPreviewElement = VnWorkshopElement.Back;
                    uiFeedbackElement = _sceneComposerUiFeedbackPreviewElement;
                    uiFeedbackSample = ComposerSampleUiFeedback(
                        _sceneComposerUiFeedbackPreviewElement,
                        _sceneComposerUiFeedbackPreviewState,
                        _sceneComposerUiFeedbackPreviewProgress);
                }
                VnPresentationWorkshopPreviewRenderer.Draw(previewRect, frame, selectedUi, advancedLayout,
                    uiFeedbackElement, uiFeedbackSample);
                if (staticAuthoringPreview)
                {
                    DrawSceneComposerSelectionOverlay(previewRect, frame);
                    HandleSceneComposerPreviewInput(previewRect, frame, Event.current);
                    DrawSceneComposerVideoPreparationState(previewRect);
                }
            }
            else GUI.Box(previewRect, "Выберите или добавьте сцену.");

            EditorGUILayout.Space(2f);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            using (new EditorGUI.DisabledScope(_sceneComposerProject.scenes.Count == 0))
            {
                if (GUILayout.Button("Предыдущая", EditorStyles.toolbarButton, GUILayout.Width(88f))) ComposerPrevious();
                if (GUILayout.Button("Проиграть сцену", SceneComposerPrimaryTransportButtonStyle, GUILayout.Width(124f))) ComposerPlayScene();
                if (GUILayout.Button("Проиграть всё", SceneComposerPrimaryTransportButtonStyle, GUILayout.Width(110f))) ComposerPlayAll();
                if (GUILayout.Button("Пауза", EditorStyles.toolbarButton, GUILayout.Width(62f))) ComposerPause();
                if (GUILayout.Button("Сначала", EditorStyles.toolbarButton, GUILayout.Width(68f))) ComposerRestart();
                if (GUILayout.Button("Следующая", EditorStyles.toolbarButton, GUILayout.Width(84f))) ComposerNext();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Проиграть отсюда", EditorStyles.toolbarButton, GUILayout.Width(118f))) ComposerPlayFromHere();
            }
            EditorGUILayout.EndHorizontal();
            if (_sceneComposerPlayback != null && _sceneComposerPlayback.CurrentSceneIndex >= 0)
            {
                string state = _sceneComposerPlayback.IsPlaying ? "Воспроизведение" : "Пауза / остановлено";
                EditorGUILayout.LabelField(state + " · Сцена " + (_sceneComposerPlayback.CurrentSceneIndex + 1) + " · " +
                    _sceneComposerPlayback.SceneElapsedSeconds.ToString("0.00") + " с", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawSceneComposerInspector()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(SceneComposerInspectorWidth));
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Свойства сцены", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            _sceneComposerInspectorScroll = EditorGUILayout.BeginScrollView(_sceneComposerInspectorScroll, GUILayout.ExpandHeight(true));
            VnSceneComposerScene scene = GetSelectedScene();
            if (scene == null)
            {
                EditorGUILayout.HelpBox("Создайте или выберите сцену слева.", MessageType.Info);
                EditorGUILayout.EndScrollView(); EditorGUILayout.EndVertical(); return;
            }

            EditorGUILayout.Space(4f);
            _sceneComposerInspectorSection = Mathf.Clamp(_sceneComposerInspectorSection, 0, SceneComposerInspectorSections.Length - 1);
            _sceneComposerInspectorSection = GUILayout.SelectionGrid(
                _sceneComposerInspectorSection, SceneComposerInspectorSections, 2, EditorStyles.miniButton);
            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            switch (_sceneComposerInspectorSection)
            {
                case 0: DrawSceneComposerBackgroundInspector(scene); break;
                case 1: DrawSceneComposerCharacterInspector(scene); break;
                case 2: DrawSceneComposerTextInspector(scene); break;
                case 3: DrawSceneComposerCharacterAnimationInspector(scene); break;
                case 4: DrawSceneComposerSceneAnimationInspector(scene); break;
                case 5: DrawSceneComposerMediaInspector(scene); break;
                case 6: DrawSceneComposerSceneSettings(scene); break;
                default: DrawSceneComposerAdditionalInspector(scene); break;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawSceneComposerProjectStorage()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("ROKAS — Редактор новеллы", EditorStyles.boldLabel, GUILayout.Width(190f));
            EditorGUILayout.LabelField("Проект", EditorStyles.miniLabel, GUILayout.Width(48f));
            EditorGUI.BeginChangeCheck();
            string title = EditorGUILayout.TextField(_sceneComposerProject.title ?? string.Empty,
                GUILayout.MinWidth(120f), GUILayout.MaxWidth(280f));
            if (EditorGUI.EndChangeCheck())
            {
                RecordSceneComposerUndo("Rename Scene Composer Project");
                _sceneComposerProject.title = title;
                MarkSceneComposerChanged();
            }

            string[] saved = ListSceneComposerProjectIds();
            GUILayout.FlexibleSpace();
            if (saved.Length > 0)
            {
                _sceneComposerSavedProjectIndex = Mathf.Clamp(_sceneComposerSavedProjectIndex, 0, saved.Length - 1);
                _sceneComposerSavedProjectIndex = EditorGUILayout.Popup(
                    _sceneComposerSavedProjectIndex, saved, EditorStyles.toolbarPopup, GUILayout.Width(150f));
            }
            if (GUILayout.Button("Сохранить", EditorStyles.toolbarButton, GUILayout.Width(78f)))
            {
                try { ComposerSaveProject(GetProjectRoot()); }
                catch (Exception exception) { SetSceneComposerStatus("Не удалось сохранить проект: " + exception.Message, MessageType.Error); }
            }
            using (new EditorGUI.DisabledScope(saved.Length == 0))
            {
                if (GUILayout.Button("Открыть", EditorStyles.toolbarButton, GUILayout.Width(68f)))
                {
                    _sceneComposerSavedProjectIndex = Mathf.Clamp(_sceneComposerSavedProjectIndex, 0, Mathf.Max(0, saved.Length - 1));
                    if (saved.Length > 0) ComposerLoadProject(GetProjectRoot(), saved[_sceneComposerSavedProjectIndex]);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSceneComposerProjectTechnicalInfo()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Технические данные проекта", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("ID проекта", _sceneComposerProject.projectId, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Хранение: Library/ROKAS/VnSceneComposer.", EditorStyles.miniLabel);
        }

        private void DrawSceneComposerBackgroundInspector(VnSceneComposerScene scene)
        {
            EditorGUILayout.LabelField("Фон", EditorStyles.boldLabel);
            string[] backgroundIds = ComposerGetAvailableBackgroundAssetIds();
            string[] backgroundNames = ComposerGetAvailableBackgroundDisplayNames();
            if (backgroundIds.Length == 0)
            {
                EditorGUILayout.HelpBox("В библиотеке пока нет фонов. Добавить фон можно в «Дополнительно».", MessageType.Info);
                return;
            }
            _sceneComposerBackgroundAssetIndex = Mathf.Clamp(_sceneComposerBackgroundAssetIndex, 0, backgroundIds.Length - 1);
            _sceneComposerBackgroundAssetIndex = EditorGUILayout.Popup("Фон", _sceneComposerBackgroundAssetIndex, backgroundNames);
            Texture2D thumbnail = ComposerGetLibraryAssetThumbnail(backgroundIds[_sceneComposerBackgroundAssetIndex]);
            if (thumbnail != null)
            {
                Rect thumbRect = GUILayoutUtility.GetRect(160f, 90f, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(thumbRect, thumbnail, ScaleMode.ScaleAndCrop, true);
            }
            if (GUILayout.Button("Выбрать фон"))
                TrySceneComposerMediaAction(() => ComposerSetLibraryBackground(backgroundIds[_sceneComposerBackgroundAssetIndex]));
        }

        private void DrawSceneComposerMediaInspector(VnSceneComposerScene scene)
        {
            EditorGUILayout.LabelField("Медиа", EditorStyles.boldLabel);
            if (scene.media == null) scene.media = new VnSceneComposerMediaReference();
            EditorGUILayout.LabelField("Тип", GetSceneComposerMediaKindLabel(scene.media.kind));
            EditorGUILayout.LabelField("Выбрано", string.IsNullOrEmpty(scene.media.displayName) ? "Нет" : scene.media.displayName);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Изображение"))
            {
                string path = EditorUtility.OpenFilePanel("Выбрать изображение сцены", string.Empty, "png,jpg,jpeg");
                if (!string.IsNullOrEmpty(path)) TrySceneComposerMediaAction(() => ComposerSetExternalImage(path));
            }
            if (GUILayout.Button("Видео"))
            {
                string path = EditorUtility.OpenFilePanel("Выбрать видео сцены", string.Empty, "mp4,mov,m4v,webm");
                if (!string.IsNullOrEmpty(path)) TrySceneComposerMediaAction(() => ComposerSetExternalVideo(path));
            }
            if (GUILayout.Button("GIF"))
            {
                string path = EditorUtility.OpenFilePanel("Выбрать GIF сцены", string.Empty, "gif");
                if (!string.IsNullOrEmpty(path)) TrySceneComposerMediaAction(() => ComposerSetExternalGif(path));
            }
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Убрать медиа")) ComposerClearMedia();

            string[] displayModeLabels = { "Вписать", "Заполнить", "Растянуть" };
            int currentDisplayMode = scene.media.scaleMode == VnSceneComposerMediaScaleMode.Fill ? 1 :
                (scene.media.scaleMode == VnSceneComposerMediaScaleMode.Stretch ? 2 : 0);
            EditorGUI.BeginChangeCheck();
            int nextDisplayMode = EditorGUILayout.Popup("Отображение", currentDisplayMode, displayModeLabels);
            VnSceneComposerMediaScaleMode scaleMode = nextDisplayMode == 1
                ? VnSceneComposerMediaScaleMode.Fill
                : (nextDisplayMode == 2 ? VnSceneComposerMediaScaleMode.Stretch : VnSceneComposerMediaScaleMode.Fit);
            bool loop = scene.media.loop;
            if (scene.media.kind == VnSceneComposerMediaKind.ExternalGif || scene.media.kind == VnSceneComposerMediaKind.ExternalVideo)
                loop = EditorGUILayout.Toggle("Зациклить", loop);
            if (EditorGUI.EndChangeCheck())
            {
                RecordSceneComposerUndo("Edit VN Scene Media"); scene.media.scaleMode = scaleMode; scene.media.loop = loop;
                InvalidateSceneComposerThumbnail(scene.sceneId); ResetSceneComposerPlayback(); MarkSceneComposerChanged();
            }
            string warning = GetSceneComposerMediaWarning(scene.media);
            if (!string.IsNullOrEmpty(warning)) EditorGUILayout.HelpBox(warning, MessageType.Warning);
            else if (scene.media.localPreviewDependency)
                EditorGUILayout.HelpBox("Внешний файл используется только в редакторе и не копируется в игровые ресурсы.", MessageType.Info);
        }

        private void DrawSceneComposerTextInspector(VnSceneComposerScene scene)
        {
            EditorGUILayout.LabelField("Текст", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            bool narration = EditorGUILayout.Toggle("Текст без персонажа", scene.narration);
            string speaker = EditorGUILayout.TextField("Говорящий", scene.speaker ?? string.Empty);
            EditorGUILayout.LabelField("Текст сцены");
            string text = EditorGUILayout.TextArea(scene.previewText ?? string.Empty, GUILayout.MinHeight(70f));
            if (EditorGUI.EndChangeCheck())
            {
                RecordSceneComposerUndo("Edit VN Scene Text"); scene.narration = narration;
                scene.speaker = narration ? string.Empty : speaker; scene.previewText = text;
                ResetSceneComposerPlayback(); MarkSceneComposerChanged();
            }

            VnPresentationWorkshopPreset effective = VnSceneComposerComposition.ResolvePresentation(_sceneComposerProject, scene);
            VnWorkshopTypographyValues typography = VnPresentationWorkshopVn10Resolver.ResolveTypography(effective);
            VnWorkshopTypewriterValues typewriter = VnPresentationWorkshopVn10Resolver.ResolveTypewriter(effective);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Оформление текста", EditorStyles.miniBoldLabel);
            int fontIndex = typography.DialogueFontPreset == VnWorkshopFontPreset.ProjectSerif ? 1 : 0;
            EditorGUI.BeginChangeCheck();
            int nextFontIndex = EditorGUILayout.Popup("Шрифт", fontIndex, new[] { "Без засечек", "С засечками" });
            float nextFontSize = EditorGUILayout.Slider("Размер текста", typography.DialogueFontSize, 8f, 96f);
            float nextSpeed = EditorGUILayout.Slider(new GUIContent("Скорость текста", "Скорость появления символов во время реплики."), typewriter.CharactersPerSecond, 1f, 240f);
            if (EditorGUI.EndChangeCheck())
            {
                VnWorkshopFontPreset nextFont = nextFontIndex == 1 ? VnWorkshopFontPreset.ProjectSerif : VnWorkshopFontPreset.ProjectSans;
                SetSceneComposerBasicTextStyle(nextFont, nextFontSize, nextSpeed);
            }
        }

        private void SetSceneComposerBasicTextStyle(VnWorkshopFontPreset dialogueFontPreset, float dialogueFontSize, float charactersPerSecond)
        {
            EnsureSceneComposerProject();
            VnPresentationWorkshopPreset inheritedPreset = _sceneComposerPresentationProjectDefaults
                ? new VnPresentationWorkshopPreset()
                : (_sceneComposerProject.defaultPresentation ?? new VnPresentationWorkshopPreset());
            VnWorkshopTypographyValues inheritedTypography = VnPresentationWorkshopVn10Resolver.ResolveTypography(inheritedPreset);
            VnWorkshopTypewriterValues inheritedTypewriter = VnPresentationWorkshopVn10Resolver.ResolveTypewriter(inheritedPreset);
            MutateComposerPresentation("Edit VN Scene Basic Text Style", preset =>
            {
                if (preset.typography == null) preset.typography = new VnWorkshopTypographyOverride();
                if (preset.typewriter == null) preset.typewriter = new VnWorkshopTypewriterOverride();
                preset.typography.hasDialogueFontPreset = dialogueFontPreset != inheritedTypography.DialogueFontPreset;
                preset.typography.dialogueFontPreset = dialogueFontPreset;
                preset.typography.hasDialogueFontSize = !Mathf.Approximately(dialogueFontSize, inheritedTypography.DialogueFontSize);
                preset.typography.dialogueFontSize = dialogueFontSize;
                preset.typewriter.hasCharactersPerSecond = !Mathf.Approximately(charactersPerSecond, inheritedTypewriter.CharactersPerSecond);
                preset.typewriter.charactersPerSecond = charactersPerSecond;
            });
        }

        private void DrawSceneComposerCharacterInspector(VnSceneComposerScene scene)
        {
            EditorGUILayout.LabelField("Персонажи", EditorStyles.boldLabel);
            if (scene.characters == null) scene.characters = new List<VnSceneComposerCharacter>();
            string[] characters = VnSceneComposerCharacterStateResolver.GetCharacters();
            if (scene.characters.Count == 0)
            {
                EditorGUILayout.HelpBox("В сцене пока нет персонажей.", MessageType.Info);
                using (new EditorGUI.DisabledScope(characters.Length == 0))
                {
                    if (GUILayout.Button("+ Добавить персонажа"))
                    {
                        string[] states = ComposerGetAuthoredStateIds(characters[0]);
                        if (states.Length > 0) ComposerAddCharacter(characters[0], states[0]);
                    }
                }
                if (characters.Length == 0) EditorGUILayout.HelpBox("В каталоге пока нет доступных персонажей и поз.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Персонажи в сцене", EditorStyles.miniBoldLabel);
            for (int i = 0; i < scene.characters.Count; i++)
            {
                VnSceneComposerCharacter listed = scene.characters[i];
                if (listed == null) continue;
                bool selected = _sceneComposerSelectedCharacterIndex == i;
                string stateName = GetSceneComposerCharacterStateDisplayName(listed.stateId);
                string rowLabel = (selected ? "● " : string.Empty) + (listed.characterId ?? string.Empty) +
                    (string.IsNullOrEmpty(stateName) ? string.Empty : " · " + stateName);
                if (GUILayout.Button(rowLabel, selected ? EditorStyles.miniButtonMid : EditorStyles.miniButton))
                {
                    _sceneComposerSelectedCharacterIndex = i;
                    Repaint();
                }
            }

            using (new EditorGUI.DisabledScope(scene.characters.Count >= 3 || characters.Length == 0))
            {
                if (GUILayout.Button("+ Добавить персонажа"))
                {
                    string[] states = ComposerGetAuthoredStateIds(characters[0]);
                    if (states.Length > 0) ComposerAddCharacter(characters[0], states[0]);
                }
            }

            if (_sceneComposerSelectedCharacterIndex < 0 || _sceneComposerSelectedCharacterIndex >= scene.characters.Count || scene.characters[_sceneComposerSelectedCharacterIndex] == null)
            {
                EditorGUILayout.HelpBox("Выберите персонажа в списке или на сцене.", MessageType.Info);
                return;
            }

            int selectedIndex = _sceneComposerSelectedCharacterIndex;
            VnSceneComposerCharacter character = scene.characters[selectedIndex];
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Выбранный персонаж", EditorStyles.miniBoldLabel);
            int characterIndex = Mathf.Max(0, Array.FindIndex(characters, name => string.Equals(name, character.characterId, StringComparison.OrdinalIgnoreCase)));
            string currentCharacter = characters.Length > 0 ? characters[characterIndex] : character.characterId;
            string[] currentStates = ComposerGetAuthoredStateIds(currentCharacter);
            int currentStateIndex = Mathf.Max(0, Array.IndexOf(currentStates, character.stateId));
            int currentPosition = character.hasPositionOffset ? 3 : (character.stageSlot == VnWorkshopStageSlot.Left ? 0 : (character.stageSlot == VnWorkshopStageSlot.Right ? 2 : 1));
            string[] positionLabels = { "Слева", "Центр", "Справа", "Свободно" };
            float currentScale = character.hasScaleMultiplier ? character.scaleMultiplier : 1f;
            EditorGUI.BeginChangeCheck();
            int nextCharacterIndex = characters.Length > 0 ? EditorGUILayout.Popup("Персонаж", characterIndex, characters) : 0;
            string nextCharacter = characters.Length > 0 ? characters[nextCharacterIndex] : character.characterId;
            string[] nextStates = ComposerGetAuthoredStateIds(nextCharacter);
            string[] nextStateNames = nextStates.Select(GetSceneComposerCharacterStateDisplayName).ToArray();
            int nextStateIndex = string.Equals(nextCharacter, currentCharacter, StringComparison.OrdinalIgnoreCase)
                ? Mathf.Clamp(currentStateIndex, 0, Mathf.Max(0, nextStates.Length - 1)) : 0;
            if (nextStates.Length > 0)
                nextStateIndex = EditorGUILayout.Popup(new GUIContent("Поза / эмоция", "Внешний вид персонажа в этой сцене."), nextStateIndex, nextStateNames);
            string nextState = nextStates.Length > 0 ? nextStates[nextStateIndex] : character.stateId;
            int nextPosition = EditorGUILayout.Popup(new GUIContent("Положение", "Быстро разместить персонажа на сцене."), currentPosition, positionLabels);
            float nextScale = EditorGUILayout.Slider(new GUIContent("Размер", "Размер персонажа в кадре."), currentScale, .05f, 5f);
            if (EditorGUI.EndChangeCheck())
            {
                RecordSceneComposerUndo("Edit VN Scene Character");
                character.characterId = nextCharacter;
                character.stateId = nextState;
                if (nextPosition == 3) character.hasPositionOffset = true;
                else
                {
                    character.stageSlot = nextPosition == 0 ? VnWorkshopStageSlot.Left : (nextPosition == 2 ? VnWorkshopStageSlot.Right : VnWorkshopStageSlot.Center);
                    character.hasPositionOffset = false;
                    character.positionOffset = Vector2.zero;
                }
                character.hasScaleMultiplier = !Mathf.Approximately(nextScale, 1f);
                character.scaleMultiplier = nextScale;
                ResetSceneComposerPlayback(); MarkSceneComposerChanged();
            }

            DrawSceneComposerCharacterTransformControls(selectedIndex, character);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Сделать говорящим"))
            {
                RecordSceneComposerUndo("Set VN Scene Speaker"); scene.narration = false; scene.speaker = character.characterId;
                ResetSceneComposerPlayback(); MarkSceneComposerChanged();
            }
            if (GUILayout.Button("Удалить"))
            {
                RecordSceneComposerUndo("Remove VN Scene Character");
                scene.characters.RemoveAt(selectedIndex);
                _sceneComposerSelectedCharacterIndex = scene.characters.Count == 0 ? -1 : Mathf.Clamp(selectedIndex, 0, scene.characters.Count - 1);
                ResetSceneComposerPlayback(); MarkSceneComposerChanged();
            }
            EditorGUILayout.EndHorizontal();
        }

        private static string GetSceneComposerCharacterStateDisplayName(string stateId)
        {
            if (!VnSceneComposerCharacterStateResolver.TryResolve(stateId, out VnSceneComposerResolvedCharacterState state) || state == null)
                return stateId ?? string.Empty;
            string display = string.IsNullOrWhiteSpace(state.DisplayName) ? state.Id : state.DisplayName;
            string prefix = (state.Character ?? string.Empty) + "_";
            if (!string.IsNullOrEmpty(prefix) && display.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) display = display.Substring(prefix.Length);
            return (display ?? string.Empty).Replace('_', ' ');
        }

        private void DrawSceneComposerAnimationScope()
        {
            EditorGUILayout.LabelField("Применить:", EditorStyles.miniBoldLabel);
            int current = _sceneComposerPresentationProjectDefaults ? 1 : 0;
            int next = GUILayout.Toolbar(current, new[] { "Только к этой сцене", "Ко всем сценам" });
            if (next != current) ComposerSetPresentationScope(next == 1);
            EditorGUILayout.Space();
        }

        private VnPresentationWorkshopPreset GetSceneComposerAnimationDisplayPreset(VnSceneComposerScene scene)
        {
            if (_sceneComposerPresentationProjectDefaults)
                return _sceneComposerProject.defaultPresentation ?? new VnPresentationWorkshopPreset();
            return VnSceneComposerComposition.ResolvePresentation(_sceneComposerProject, scene);
        }

        private void DrawSceneComposerCharacterAnimationInspector(VnSceneComposerScene scene)
        {
            EditorGUILayout.LabelField("Анимация персонажа", EditorStyles.boldLabel);
            DrawSceneComposerAnimationScope();
            VnPresentationWorkshopPreset effective = GetSceneComposerAnimationDisplayPreset(scene);
            VnWorkshopCharacterTransitionValues transition = VnPresentationWorkshopVn10Resolver.ResolveCharacterTransition(effective);
            VnWorkshopExpressionTransitionValues expression = VnPresentationWorkshopVn10Resolver.ResolveExpressionTransition(effective);
            VnWorkshopActionBounceValues bounce = VnPresentationWorkshopVn10Resolver.ResolveActionBounce(effective);
            string[] enterLabels = { "Без анимации", "Плавное появление", "Появление со сдвигом" };
            string[] exitLabels = { "Без анимации", "Плавное исчезновение", "Исчезновение со сдвигом" };
            string[] directionLabels = { "Слева", "Справа" };
            EditorGUI.BeginChangeCheck();
            int nextMode = Mathf.Clamp((int)transition.Mode, 0, enterLabels.Length - 1);
            EditorGUILayout.LabelField(new GUIContent("Появление", "Как персонаж появляется в сцене."), EditorStyles.miniBoldLabel);
            nextMode = EditorGUILayout.Popup("Способ", nextMode, enterLabels);
            EditorGUILayout.LabelField(new GUIContent("Исчезновение", "Как персонаж покидает сцену."), EditorStyles.miniBoldLabel);
            nextMode = EditorGUILayout.Popup("Способ", nextMode, exitLabels);
            float nextDuration = EditorGUILayout.Slider("Длительность, с", transition.Duration, 0f, 3f);
            VnWorkshopSlideDirection nextDirection = transition.SlideDirection;
            if ((VnWorkshopCharacterTransitionMode)nextMode == VnWorkshopCharacterTransitionMode.SlideAndFade)
            {
                int directionIndex = EditorGUILayout.Popup("Направление", (int)transition.SlideDirection, directionLabels);
                nextDirection = (VnWorkshopSlideDirection)directionIndex;
            }
            if (EditorGUI.EndChangeCheck())
                ComposerSetCharacterTransition((VnWorkshopCharacterTransitionMode)nextMode, nextDuration, transition.FadeDuration, transition.SlideDistance, nextDirection, transition.Easing);
            EditorGUILayout.HelpBox("Появление и исчезновение используют единый профиль перехода, сохраняя существующую логику сцены.", MessageType.None);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(new GUIContent("Смена позы / эмоции", "Как меняется внешний вид персонажа между сценами."), EditorStyles.miniBoldLabel);
            EditorGUI.BeginChangeCheck();
            float expressionDuration = EditorGUILayout.Slider("Длительность, с", expression.Duration, 0f, 3f);
            if (EditorGUI.EndChangeCheck()) ComposerSetExpressionTransition(expressionDuration, expression.Easing);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(new GUIContent("Акцент / движение", "Короткое движение, выделяющее персонажа."), EditorStyles.miniBoldLabel);
            bool triggered = scene.transition != null && scene.transition.triggerActionBounce;
            EditorGUI.BeginChangeCheck();
            bool nextTriggered = EditorGUILayout.Toggle("Использовать акцент", triggered);
            if (EditorGUI.EndChangeCheck())
            {
                RecordSceneComposerUndo("Toggle VN Scene Character Accent");
                if (scene.transition == null) scene.transition = new VnSceneComposerTransition();
                scene.transition.triggerActionBounce = nextTriggered;
                ResetSceneComposerPlayback(); MarkSceneComposerChanged();
            }
            EditorGUI.BeginChangeCheck();
            float nextAmplitude = EditorGUILayout.Slider("Сила", bounce.Amplitude, 0f, 100f);
            float nextBounceDuration = EditorGUILayout.Slider("Длительность, с", bounce.Duration, .01f, 3f);
            if (EditorGUI.EndChangeCheck()) ComposerSetBounce(nextAmplitude, nextBounceDuration, bounce.ScaleEmphasis, bounce.Overshoot, bounce.Easing);
        }

        private void DrawSceneComposerSceneAnimationInspector(VnSceneComposerScene scene)
        {
            EditorGUILayout.LabelField("Анимация сцены", EditorStyles.boldLabel);
            DrawSceneComposerAnimationScope();
            VnPresentationWorkshopPreset effective = GetSceneComposerAnimationDisplayPreset(scene);
            VnWorkshopBackgroundTransitionValues background = VnPresentationWorkshopVn10Resolver.ResolveBackgroundTransition(effective);
            VnWorkshopStageLayoutValues stage = VnPresentationWorkshopVn10Resolver.ResolveStageLayout(effective);
            VnWorkshopSpeakerFocusValues focus = VnPresentationWorkshopVn10Resolver.ResolveSpeakerFocus(effective);
            VnWorkshopTimingValues timing = VnPresentationWorkshopVn10Resolver.ResolveTiming(effective);
            EditorGUILayout.LabelField(new GUIContent("Переход фона", "Как фон меняется при переходе к этой сцене."), EditorStyles.miniBoldLabel);
            string[] backgroundLabels = { "Без перехода", "Плавный переход", "Шторка" };
            string[] curtainDirectionLabels = { "Справа налево", "Слева направо" };
            EditorGUI.BeginChangeCheck();
            int backgroundMode = EditorGUILayout.Popup("Способ", (int)background.Mode, backgroundLabels);
            float backgroundDuration = EditorGUILayout.Slider("Длительность, с", background.Duration, 0f, 3f);
            VnWorkshopCurtainDirection curtainDirection = background.Direction;
            if ((VnWorkshopBackgroundTransitionMode)backgroundMode == VnWorkshopBackgroundTransitionMode.Curtain)
            {
                int direction = EditorGUILayout.Popup("Направление", (int)background.Direction, curtainDirectionLabels);
                curtainDirection = (VnWorkshopCurtainDirection)direction;
            }
            if (EditorGUI.EndChangeCheck()) ComposerSetBackgroundTransition((VnWorkshopBackgroundTransitionMode)backgroundMode, backgroundDuration, background.CurtainDarkness, curtainDirection, background.Easing);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(new GUIContent("Расположение персонажей", "Как персонажи располагаются и перестраиваются на сцене."), EditorStyles.miniBoldLabel);
            string[] characterCountLabels = { "Нет персонажей", "1 персонаж", "2 персонажа", "3 персонажа" };
            string[] characterSlotLabels = { "", "Центр", "Слева / Справа", "Слева / Центр / Справа" };
            int characterCount = Mathf.Clamp(scene.characters != null ? scene.characters.Count : 0, 0, 3);
            EditorGUILayout.LabelField("Схема", characterCountLabels[characterCount] + (characterCount > 0 ? " · " + characterSlotLabels[characterCount] : string.Empty));
            EditorGUI.BeginChangeCheck();
            float repositionDuration = EditorGUILayout.Slider("Длительность перестановки, с", stage.RepositionDuration, 0f, 3f);
            if (EditorGUI.EndChangeCheck()) ComposerSetStageLayout(stage.LeftX, stage.CenterX, stage.RightX, stage.SlotY, stage.LeftScale, stage.CenterScale, stage.RightScale, stage.Spacing, repositionDuration, stage.Easing);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(new GUIContent("Фокус говорящего", "Выделяет говорящего персонажа и приглушает остальных."), EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox("Фокус автоматически следует за полем «Говорящий» при нескольких персонажах.", MessageType.None);
            EditorGUI.BeginChangeCheck();
            float inactiveBrightness = EditorGUILayout.Slider("Яркость остальных", focus.InactiveBrightness, 0f, 1.5f);
            float focusDuration = EditorGUILayout.Slider("Длительность фокуса, с", focus.TransitionDuration, 0f, 3f);
            if (EditorGUI.EndChangeCheck()) ComposerSetSpeakerFocus(focus.ActiveScale, focus.ActiveBrightness, focus.ActiveForwardOffset, focus.InactiveScale, inactiveBrightness, focus.InactiveAlpha, focusDuration, focus.Easing);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(new GUIContent("Тайминг сцены", "Продолжительность сцены и переход к следующей сцене."), EditorStyles.miniBoldLabel);
            if (scene.timing == null) scene.timing = new VnSceneComposerTiming();
            string[] advanceLabels = { "Вручную", "Автоматически" };
            int currentAdvance = scene.timing.previewAdvanceMode == VnSceneComposerPreviewAdvanceMode.PreviewAutoDuration ? 1 : 0;
            EditorGUI.BeginChangeCheck();
            int nextAdvance = EditorGUILayout.Popup(new GUIContent("Переход к следующей сцене", "Как продолжается воспроизведение после этой сцены."), currentAdvance, advanceLabels);
            float nextAutoDuration = scene.timing.previewAutoDuration;
            if (nextAdvance == 1) nextAutoDuration = EditorGUILayout.FloatField("Длительность сцены, с", Mathf.Max(0f, nextAutoDuration));
            if (EditorGUI.EndChangeCheck())
            {
                RecordSceneComposerUndo("Edit VN Scene Advance Timing");
                scene.timing.previewAdvanceMode = nextAdvance == 1 ? VnSceneComposerPreviewAdvanceMode.PreviewAutoDuration : VnSceneComposerPreviewAdvanceMode.ManualBeat;
                scene.timing.previewAutoDuration = Mathf.Max(0f, nextAutoDuration);
                ResetSceneComposerPlayback(); MarkSceneComposerChanged();
            }
            if (nextAdvance == 1)
            {
                EditorGUI.BeginChangeCheck();
                float sequenceGap = EditorGUILayout.Slider("Пауза между сценами, с", timing.AutoPreviewSequenceGap, 0f, 10f);
                if (EditorGUI.EndChangeCheck()) ComposerSetTiming(timing.MinimumBeatSettleDuration, timing.PostTransitionBreathingRoom, sequenceGap);
            }
        }

        private void DrawSceneComposerSceneSettings(VnSceneComposerScene scene)
        {
            EditorGUILayout.LabelField("Настройки сцены", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            string label = EditorGUILayout.TextField("Название сцены", scene.label ?? string.Empty);
            if (EditorGUI.EndChangeCheck()) ComposerRenameSelectedScene(label);
            EditorGUILayout.HelpBox("Расширенные параметры переходов, тайминга и оформления находятся в «Дополнительно».", MessageType.None);
        }

        private void DrawSceneComposerAdditionalInspector(VnSceneComposerScene scene)
        {
            EditorGUILayout.LabelField("Дополнительно", EditorStyles.boldLabel);
            DrawSceneComposerProjectTechnicalInfo();
            DrawSceneComposerAssetLibraryControls(scene);
            DrawSceneComposerPresentationInspector(scene);
        }

        private void DrawSceneComposerPresentationInspector(VnSceneComposerScene scene) { DrawSceneComposerPresentationControls(scene); }

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
                _sceneComposerPlayback.Pause(); SetSceneComposerStatus("Не удалось воспроизвести предпросмотр: " + exception.Message, MessageType.Error);
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

        private static string GetSceneComposerDisplayLabel(VnSceneComposerScene scene, int index)
        {
            string fallback = "Сцена " + (index + 1);
            if (scene == null || string.IsNullOrWhiteSpace(scene.label)) return fallback;
            string generated = "Scene " + (index + 1);
            return string.Equals(scene.label, generated, StringComparison.Ordinal) ? fallback : scene.label;
        }

        private static string GetSceneComposerMediaKindLabel(VnSceneComposerMediaKind kind)
        {
            switch (kind)
            {
                case VnSceneComposerMediaKind.None: return "Без медиа";
                case VnSceneComposerMediaKind.ExistingRokasAsset: return "Ресурс";
                case VnSceneComposerMediaKind.ExternalImage: return "Изображение";
                case VnSceneComposerMediaKind.ExternalGif: return "GIF";
                case VnSceneComposerMediaKind.ExternalVideo: return "Видео";
                default: return kind.ToString();
            }
        }

        private void RecordSceneComposerUndo(string label) { Undo.RegisterCompleteObjectUndo(this, label); }
        private void MarkSceneComposerChanged() { EditorUtility.SetDirty(this); Repaint(); }
        private void SetSceneComposerStatus(string message, MessageType type) { _sceneComposerStatus = message ?? string.Empty; _sceneComposerStatusType = type; Repaint(); }

        private void TrySceneComposerMediaAction(Action action)
        {
            try { action(); SetSceneComposerStatus("Медиа сцены обновлено.", MessageType.Info); }
            catch (Exception exception) { SetSceneComposerStatus(exception.Message, MessageType.Error); }
        }

        private string GetSceneComposerMediaWarning(VnSceneComposerMediaReference media)
        {
            if (media == null) return "Ссылка на медиа отсутствует.";
            if (media.kind == VnSceneComposerMediaKind.None) return string.Empty;
            if (media.kind == VnSceneComposerMediaKind.ExistingRokasAsset)
            {
                if (string.IsNullOrEmpty(media.reference)) return "Не указан ресурс ROKAS.";
                return string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(media.reference)) ? "Ресурс ROKAS не найден." : string.Empty;
            }
            if (VnSceneComposerSerialization.IsExternalMedia(media.kind) &&
                (string.IsNullOrEmpty(media.reference) || VnSceneComposerSerialization.IsPortableExternalReference(media.reference) ||
                 !Path.IsPathRooted(media.reference) || !File.Exists(media.reference)))
                return "Внешний файл не найден или не привязан на этом компьютере.";
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
                SetSceneComposerStatus("Не удалось прочитать сохранённые проекты: " + exception.Message, MessageType.Warning);
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
