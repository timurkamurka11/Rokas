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
            "Дополнительно",
            "Декорации",
            "Звуки",
            "Музыка"
        };

        private const float SceneComposerStoryboardWidth = 224f;
        private const float SceneComposerInspectorWidth = 360f;
        private const float SceneComposerStoryboardCompactWidth = 176f;
        private const float SceneComposerInspectorCompactWidth = 288f;
        private const float SceneComposerResponsiveWidth = 1180f;
        private const float SceneComposerCompactTransportWidth = 680f;
        private const float SceneComposerCompactHeaderWidth = 1050f;
        private const int SceneComposerInspectorColumns = 2;
        private const float SceneComposerSceneThumbnailWidth = 72f;
        private const float SceneComposerSceneThumbnailHeight = 48f;
        private const int SceneComposerDialogueEditorMinVisibleLines = 4;
        private const int SceneComposerDialogueEditorMaxVisibleLines = 12;

        private static GUIStyle _sceneComposerPrimaryTransportButtonStyle;
        private static GUIStyle _sceneComposerInspectorTabStyle;
        private static GUIStyle _sceneComposerInspectorSelectedTabStyle;
        private static GUIStyle _sceneComposerSceneButtonStyle;
        private static GUIStyle _sceneComposerSelectedSceneButtonStyle;
        private static GUIStyle _sceneComposerDialogueTextAreaStyle;

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

        private static GUIStyle SceneComposerInspectorTabStyle
        {
            get
            {
                if (_sceneComposerInspectorTabStyle == null)
                {
                    _sceneComposerInspectorTabStyle = new GUIStyle(EditorStyles.miniButton)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fixedHeight = 24f
                    };
                }
                return _sceneComposerInspectorTabStyle;
            }
        }

        private static GUIStyle SceneComposerInspectorSelectedTabStyle
        {
            get
            {
                if (_sceneComposerInspectorSelectedTabStyle == null)
                {
                    _sceneComposerInspectorSelectedTabStyle = new GUIStyle(SceneComposerInspectorTabStyle)
                    {
                        fontStyle = FontStyle.Bold
                    };
                }
                return _sceneComposerInspectorSelectedTabStyle;
            }
        }

        private static GUIStyle SceneComposerSceneButtonStyle
        {
            get
            {
                if (_sceneComposerSceneButtonStyle == null)
                {
                    _sceneComposerSceneButtonStyle = new GUIStyle(EditorStyles.miniButton)
                    {
                        alignment = TextAnchor.MiddleLeft
                    };
                }
                return _sceneComposerSceneButtonStyle;
            }
        }

        private static GUIStyle SceneComposerSelectedSceneButtonStyle
        {
            get
            {
                if (_sceneComposerSelectedSceneButtonStyle == null)
                {
                    _sceneComposerSelectedSceneButtonStyle = new GUIStyle(SceneComposerSceneButtonStyle)
                    {
                        fontStyle = FontStyle.Bold
                    };
                }
                return _sceneComposerSelectedSceneButtonStyle;
            }
        }

        private static GUIStyle SceneComposerDialogueTextAreaStyle
        {
            get
            {
                if (_sceneComposerDialogueTextAreaStyle == null)
                {
                    _sceneComposerDialogueTextAreaStyle = new GUIStyle(EditorStyles.textArea)
                    {
                        wordWrap = true,
                        stretchWidth = true,
                        fixedHeight = 0f,
                        fixedWidth = 0f
                    };
                }
                return _sceneComposerDialogueTextAreaStyle;
            }
        }

        [SerializeField] private bool _sceneComposerWorkspaceActive;
        [SerializeField] private VnSceneComposerProject _sceneComposerProject = new VnSceneComposerProject();
        [SerializeField] private string _sceneComposerSelectedSceneId = string.Empty;
        [SerializeField] private string _sceneComposerSelectedDialogueBeatId = string.Empty;
        [SerializeField] private Vector2 _sceneComposerStoryboardScroll;
        [SerializeField] private Vector2 _sceneComposerInspectorScroll;
        [SerializeField] private bool _sceneComposerProjectDefaultsExpanded;
        [SerializeField] private bool _sceneComposerSceneOverridesExpanded;
        [SerializeField] private int _sceneComposerSavedProjectIndex;
        [SerializeField] private int _sceneComposerInspectorSection;
        [SerializeField] private bool _sceneComposerAdvancedAnimationGroupExpanded;
        [SerializeField] private bool _sceneComposerAdvancedPresentationGroupExpanded;
        [SerializeField] private bool _sceneComposerAdvancedTechnicalGroupExpanded;
        [SerializeField] private bool _sceneComposerTextDialogueExpanded = true;
        [SerializeField] private bool _sceneComposerTextStagingExpanded = true;
        [SerializeField] private bool _sceneComposerTextPresentationExpanded;

        [NonSerialized] private VnSceneComposerPlaybackController _sceneComposerPlayback;
        [NonSerialized] private Dictionary<string, SceneComposerThumbnailCacheEntry> _sceneComposerThumbnailCache;
        [NonSerialized] private Dictionary<string, Vector2> _sceneComposerDialogueEditorScrollByBeat;
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
            ComposerPrepareSelectedVideoForAuthoring();
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
            SelectFirstSceneComposerDialogueBeat(scene);
            _sceneComposerSelectedCharacterIndex = -1;
            _sceneComposerSelectedDecorationId = string.Empty;
            _sceneComposerSelectedTextId = string.Empty;
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
            if (copy != null)
            {
                _sceneComposerSelectedSceneId = copy.sceneId;
                SelectFirstSceneComposerDialogueBeat(copy);
            }
            _sceneComposerSelectedCharacterIndex = -1;
            _sceneComposerSelectedDecorationId = string.Empty;
            _sceneComposerSelectedTextId = string.Empty;
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
            if (_sceneComposerProject.scenes.Count == 0)
            {
                _sceneComposerSelectedSceneId = string.Empty;
                _sceneComposerSelectedDialogueBeatId = string.Empty;
            }
            else
            {
                int nextIndex = Mathf.Clamp(deletedIndex, 0, _sceneComposerProject.scenes.Count - 1);
                VnSceneComposerScene nextScene = _sceneComposerProject.scenes[nextIndex];
                _sceneComposerSelectedSceneId = nextScene.sceneId;
                SelectFirstSceneComposerDialogueBeat(nextScene);
            }
            _sceneComposerSelectedCharacterIndex = -1;
            _sceneComposerSelectedDecorationId = string.Empty;
            _sceneComposerSelectedTextId = string.Empty;
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
            SelectFirstSceneComposerDialogueBeat(scene);
            _sceneComposerSelectedCharacterIndex = -1;
            _sceneComposerSelectedDecorationId = string.Empty;
            _sceneComposerSelectedTextId = string.Empty;
            ComposerPrepareSelectedVideoForAuthoring();
            Repaint();
        }

        public string ComposerGetSelectedDialogueBeatId()
        {
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            return beat != null ? beat.beatId ?? string.Empty : string.Empty;
        }

        public void ComposerSelectDialogueBeat(string beatId)
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            if (VnSceneComposerDialogue.FindIndex(scene, beatId) < 0)
                throw new ArgumentException("Dialogue beat is not part of the selected Scene.", nameof(beatId));
            _sceneComposerSelectedDialogueBeatId = beatId;
            _sceneComposerSelectedCharacterStagingId = string.Empty;
            Repaint();
        }

        public void ComposerSetSelectedDialogueBeatCharacterState(
            string targetCharacterId, bool hasStateOverride, string stateId)
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            if (beat == null) throw new InvalidOperationException("No dialogue Beat is selected.");

            string target = targetCharacterId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(target))
            {
                if (hasStateOverride)
                    throw new ArgumentException("State override requires a target character.", nameof(targetCharacterId));
                target = string.Empty;
            }
            else
            {
                bool found = false;
                for (int i = 0; scene.characters != null && i < scene.characters.Count; i++)
                {
                    VnSceneComposerCharacter character = scene.characters[i];
                    if (character == null) continue;
                    string id = VnSceneComposerBeatCharacterStateResolver.ResolveCharacterId(character);
                    if (string.Equals(id, target, StringComparison.OrdinalIgnoreCase))
                    {
                        target = id;
                        found = true;
                        break;
                    }
                }
                if (!found)
                    throw new ArgumentException("Beat target character is not visible in the selected Scene.",
                        nameof(targetCharacterId));
            }

            string nextState = hasStateOverride ? stateId ?? string.Empty : string.Empty;
            if (hasStateOverride)
            {
                if (!VnSceneComposerCharacterStateResolver.TryResolve(
                        nextState, out VnSceneComposerResolvedCharacterState resolved))
                    throw new ArgumentException("Unknown Beat character state: " + nextState, nameof(stateId));
                if (!string.Equals(resolved.Character, target, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException(
                        "Beat character state belongs to " + resolved.Character + ", not " + target + ".",
                        nameof(stateId));
            }

            RecordSceneComposerUndo("Edit VN Dialogue Beat Character State");
            beat.targetCharacterId = target;
            beat.hasStateOverride = hasStateOverride;
            beat.stateId = nextState;
            if (string.IsNullOrEmpty(target))
                beat.effect = VnSceneComposerBeatEffect.None;
            MarkSceneComposerChanged();
        }

        public void ComposerSetSelectedDialogueBeatEffect(
            VnSceneComposerBeatEffect effect, float strength, float duration)
        {
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            if (beat == null) throw new InvalidOperationException("No dialogue Beat is selected.");
            if (!Enum.IsDefined(typeof(VnSceneComposerBeatEffect), effect))
                throw new ArgumentOutOfRangeException(nameof(effect), effect, null);
            if (effect != VnSceneComposerBeatEffect.None &&
                string.IsNullOrWhiteSpace(beat.targetCharacterId))
                throw new InvalidOperationException("Beat animation requires a target character.");

            float safeStrength = Mathf.Max(0f, strength);
            float safeDuration = Mathf.Clamp(duration, .01f, 10f);
            RecordSceneComposerUndo("Edit VN Dialogue Beat Animation");
            beat.effect = effect;
            beat.effectStrength = safeStrength;
            beat.effectDuration = safeDuration;
            MarkSceneComposerChanged();
        }

        public void ComposerAddDialogueBeat()
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            VnSceneComposerDialogueBeat selected = ComposerGetSelectedDialogueBeat();
            RecordSceneComposerUndo("Add VN Dialogue Beat");
            VnSceneComposerDialogueBeat added = VnSceneComposerDialogue.AddBeat(
                scene, selected != null ? selected.beatId : string.Empty);
            _sceneComposerSelectedDialogueBeatId = added != null ? added.beatId ?? string.Empty : string.Empty;
            MarkSceneComposerChanged();
        }

        public void ComposerDuplicateSelectedDialogueBeat()
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            VnSceneComposerDialogueBeat selected = ComposerGetSelectedDialogueBeat();
            if (selected == null) return;
            RecordSceneComposerUndo("Duplicate VN Dialogue Beat");
            VnSceneComposerDialogueBeat copy = VnSceneComposerDialogue.DuplicateBeat(scene, selected.beatId);
            if (copy != null) _sceneComposerSelectedDialogueBeatId = copy.beatId ?? string.Empty;
            MarkSceneComposerChanged();
        }

        public void ComposerDeleteSelectedDialogueBeat()
        {
            VnSceneComposerScene scene = RequireSelectedScene();
            VnSceneComposerDialogueBeat selected = ComposerGetSelectedDialogueBeat();
            if (selected == null) return;
            RecordSceneComposerUndo("Delete VN Dialogue Beat");
            _sceneComposerSelectedDialogueBeatId = VnSceneComposerDialogue.DeleteBeat(scene, selected.beatId);
            MarkSceneComposerChanged();
        }

        public void ComposerMoveSelectedDialogueBeat(int direction)
        {
            if (direction == 0) return;
            VnSceneComposerScene scene = RequireSelectedScene();
            VnSceneComposerDialogueBeat selected = ComposerGetSelectedDialogueBeat();
            if (selected == null || scene.dialogueBeats == null) return;
            int source = VnSceneComposerDialogue.FindIndex(scene, selected.beatId);
            if (source < 0) return;
            int target = Mathf.Clamp(source + direction, 0, scene.dialogueBeats.Count - 1);
            if (target == source) return;
            RecordSceneComposerUndo("Reorder VN Dialogue Beat");
            VnSceneComposerDialogue.MoveBeat(scene, selected.beatId, target);
            _sceneComposerSelectedDialogueBeatId = selected.beatId;
            MarkSceneComposerChanged();
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
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            int index = FindSceneIndex(scene.sceneId);
            Texture2D background = ComposerGetSceneThumbnail(index) as Texture2D;
            return VnSceneComposerComposition.BuildFrame(_sceneComposerProject, scene, beat, previewResolution, background);
        }

        public void ComposerSetSelectedSceneBoundaryTransition(
            VnSceneComposerSceneTransitionType type,
            VnSceneComposerSceneTransitionDirection direction,
            float duration)
        {
            if (!Enum.IsDefined(typeof(VnSceneComposerSceneTransitionType), type))
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
            if (!Enum.IsDefined(typeof(VnSceneComposerSceneTransitionDirection), direction))
                throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            if (float.IsNaN(duration) || float.IsInfinity(duration))
                throw new ArgumentOutOfRangeException(nameof(duration), "Transition duration must be finite.");

            VnSceneComposerScene scene = RequireSelectedScene();
            if (scene.transition == null) scene.transition = new VnSceneComposerTransition();
            RecordSceneComposerUndo("Edit VN Scene Boundary Transition");
            scene.transition.sceneTransitionType = type;
            scene.transition.sceneTransitionDirection = direction;
            scene.transition.sceneTransitionDuration = Mathf.Clamp(duration, 0f, 10f);
            MarkSceneComposerChanged();
        }

        public void ComposerPlayScene()
        {
            ComposerStopMusicPreview();
            int index = GetSelectedSceneIndexOrThrow();
            EnsureSceneComposerPlayback().PlaySceneFromNeutralStart(index);
            BeginSceneComposerPlaybackTick();
        }

        public void ComposerPlayFromHere()
        {
            ComposerStopMusicPreview();
            int index = GetSelectedSceneIndexOrThrow();
            VnSceneComposerScene scene = RequireSelectedScene();
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            int beatIndex = beat != null ? VnSceneComposerDialogue.FindIndex(scene, beat.beatId) : 0;
            if (beatIndex < 0) beatIndex = 0;
            EnsureSceneComposerPlayback().PlayFromHereFromNeutralStart(index, beatIndex);
            BeginSceneComposerPlaybackTick();
        }

        public void ComposerPlayAll()
        {
            ComposerStopMusicPreview();
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

        public void ComposerAdvanceDialogue()
        {
            if (_sceneComposerPlayback == null) return;
            int previousScene = _sceneComposerPlayback.CurrentSceneIndex;
            _sceneComposerPlayback.AdvanceDialogue();
            if (_sceneComposerPlayback.CurrentSceneIndex != previousScene)
                SyncSceneComposerSelectionFromPlayback();
            SyncSelectedDialogueBeatFromPlayback();
            _sceneComposerLastPlaybackTick = EditorApplication.timeSinceStartup;
            Repaint();
        }

        public void ComposerPreviousDialogue()
        {
            if (_sceneComposerPlayback == null) return;
            _sceneComposerPlayback.PreviousDialogue();
            SyncSelectedDialogueBeatFromPlayback();
            _sceneComposerLastPlaybackTick = EditorApplication.timeSinceStartup;
            Repaint();
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
                SelectFirstSceneComposerDialogueBeat(GetSelectedScene());
                _sceneComposerSelectedCharacterIndex = -1;
                EnsureSceneComposerEditorUpdateRegistered();
                ComposerPrepareSelectedVideoForAuthoring();
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

        private float GetSceneComposerStoryboardWidth()
        {
            if (position.width >= SceneComposerResponsiveWidth) return SceneComposerStoryboardWidth;
            return Mathf.Clamp(position.width * .17f, SceneComposerStoryboardCompactWidth, SceneComposerStoryboardWidth);
        }

        private float GetSceneComposerInspectorWidth()
        {
            if (position.width >= SceneComposerResponsiveWidth) return SceneComposerInspectorWidth;
            return Mathf.Clamp(position.width * .27f, SceneComposerInspectorCompactWidth, SceneComposerInspectorWidth);
        }

        private float GetSceneComposerPreviewAvailableWidth()
        {
            return Mathf.Max(0f, position.width - GetSceneComposerStoryboardWidth() -
                GetSceneComposerInspectorWidth() - 12f);
        }

        private bool UseCompactSceneComposerTransport()
        {
            return GetSceneComposerPreviewAvailableWidth() < SceneComposerCompactTransportWidth;
        }

        private bool UseCompactSceneComposerHeader()
        {
            return position.width < SceneComposerCompactHeaderWidth;
        }

        private void DrawSceneComposerInspectorSelector()
        {
            for (int rowStart = 0; rowStart < SceneComposerInspectorSections.Length; rowStart += SceneComposerInspectorColumns)
            {
                EditorGUILayout.BeginHorizontal();
                for (int column = 0; column < SceneComposerInspectorColumns; column++)
                {
                    int index = rowStart + column;
                    if (index >= SceneComposerInspectorSections.Length)
                    {
                        GUILayout.FlexibleSpace();
                        continue;
                    }

                    bool selected = index == _sceneComposerInspectorSection;
                    GUIStyle style = selected ? SceneComposerInspectorSelectedTabStyle : SceneComposerInspectorTabStyle;
                    if (GUILayout.Toggle(selected, SceneComposerInspectorSections[index], style, GUILayout.ExpandWidth(true)) &&
                        !selected)
                    {
                        _sceneComposerInspectorSection = index;
                        GUI.FocusControl(null);
                    }
                }
                EditorGUILayout.EndHorizontal();
                if (rowStart + SceneComposerInspectorColumns < SceneComposerInspectorSections.Length)
                    GUILayout.Space(2f);
            }
        }

        private void DrawSceneComposerStoryboard()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(GetSceneComposerStoryboardWidth()));
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
                GUIStyle sceneButtonStyle = selected ? SceneComposerSelectedSceneButtonStyle : SceneComposerSceneButtonStyle;
                if (GUILayout.Button(numberedLabel, sceneButtonStyle, GUILayout.Height(22f))) ComposerSelectScene(i);
                string media = GetSceneComposerMediaKindLabel(scene.media != null ? scene.media.kind : VnSceneComposerMediaKind.None);
                VnSceneComposerDialogueBeat summaryBeat = ResolveSceneComposerDialogueBeat(scene, string.Empty);
                string detail = summaryBeat != null && !string.IsNullOrEmpty(summaryBeat.speaker)
                    ? summaryBeat.speaker
                    : (summaryBeat != null && summaryBeat.narration ? "Текст без персонажа" : "Без говорящего");
                EditorGUILayout.LabelField(media + " · " + detail, EditorStyles.miniLabel);
                if (summaryBeat != null && !string.IsNullOrEmpty(summaryBeat.text))
                {
                    string shortText = summaryBeat.text.Replace('\n', ' ');
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
                if (staticAuthoringPreview)
                {
                    VnPresentationWorkshopPreviewRenderer.Draw(previewRect, frame, selectedUi, advancedLayout,
                        uiFeedbackElement, uiFeedbackSample);
                }
                else
                {
                    // Playback must go through the playback-frame overload so the renderer
                    // registers the authoritative controller/visibility state before drawing
                    // plaque controls and the DialogueComplete indicator.
                    VnPresentationWorkshopPreviewRenderer.Draw(
                        previewRect, _sceneComposerPlayback.CurrentFrame, selectedUi, advancedLayout);
                }
                if (staticAuthoringPreview)
                {
                    DrawSceneComposerSelectionOverlay(previewRect, frame);
                    HandleSceneComposerPreviewInput(previewRect, frame, Event.current);
                    DrawSceneComposerVideoPreparationState(previewRect);
                }
                else
                {
                    HandleSceneComposerPlaybackInput(previewRect, frame, Event.current);
                }
            }
            else GUI.Box(previewRect, "Выберите или добавьте сцену.");

            EditorGUILayout.Space(2f);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            bool compactTransport = UseCompactSceneComposerTransport();
            using (new EditorGUI.DisabledScope(_sceneComposerProject.scenes.Count == 0))
            {
                if (GUILayout.Button(
                        new GUIContent(compactTransport ? "◀" : "Предыдущая", "Предыдущая сцена"),
                        EditorStyles.toolbarButton, GUILayout.Width(compactTransport ? 34f : 88f)))
                    ComposerPrevious();
                if (GUILayout.Button(
                        new GUIContent(compactTransport ? "Сцена" : "Проиграть сцену", "Проиграть выбранную сцену"),
                        SceneComposerPrimaryTransportButtonStyle, GUILayout.Width(compactTransport ? 84f : 124f)))
                    ComposerPlayScene();
                if (GUILayout.Button(
                        new GUIContent(compactTransport ? "Всё" : "Проиграть всё", "Проиграть все сцены"),
                        SceneComposerPrimaryTransportButtonStyle, GUILayout.Width(compactTransport ? 62f : 110f)))
                    ComposerPlayAll();
                if (GUILayout.Button(new GUIContent("Пауза", "Пауза"), EditorStyles.toolbarButton,
                        GUILayout.Width(compactTransport ? 58f : 62f)))
                    ComposerPause();
                if (GUILayout.Button(new GUIContent("Сначала", "Перезапустить с начала"), EditorStyles.toolbarButton,
                        GUILayout.Width(68f)))
                    ComposerRestart();
                if (GUILayout.Button(
                        new GUIContent(compactTransport ? "◁Р" : "Пред. реп.", "Предыдущая реплика в текущей сцене"),
                        EditorStyles.toolbarButton, GUILayout.Width(compactTransport ? 40f : 76f)))
                    ComposerPreviousDialogue();
                if (GUILayout.Button(
                        new GUIContent(compactTransport ? "Р▷" : "След. реп.", "Следующая реплика в текущей сцене"),
                        EditorStyles.toolbarButton, GUILayout.Width(compactTransport ? 40f : 76f)))
                    ComposerAdvanceDialogue();
                if (GUILayout.Button(
                        new GUIContent(compactTransport ? "▶" : "Следующая", "Следующая сцена"),
                        EditorStyles.toolbarButton, GUILayout.Width(compactTransport ? 34f : 84f)))
                    ComposerNext();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(
                        new GUIContent(compactTransport ? "Отсюда" : "Проиграть отсюда", "Проиграть с выбранной сцены"),
                        EditorStyles.toolbarButton, GUILayout.Width(compactTransport ? 78f : 118f)))
                    ComposerPlayFromHere();
            }
            EditorGUILayout.EndHorizontal();
            if (_sceneComposerPlayback != null && _sceneComposerPlayback.CurrentSceneIndex >= 0)
            {
                string state = _sceneComposerPlayback.IsPlaying ? "Воспроизведение" : "Пауза / остановлено";
                EditorGUILayout.LabelField(state + " · Сцена " + (_sceneComposerPlayback.CurrentSceneIndex + 1) + " · " +
                    _sceneComposerPlayback.SceneElapsedSeconds.ToString("0.00") + " с", EditorStyles.miniLabel);
                string videoWarning = _sceneComposerPlayback.CurrentVideoWarning;
                if (!string.IsNullOrEmpty(videoWarning))
                    EditorGUILayout.HelpBox(videoWarning, MessageType.Warning);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawSceneComposerInspector()
        {
            _sceneComposerInspectorSection = Mathf.Clamp(
                _sceneComposerInspectorSection, 0, SceneComposerInspectorSections.Length - 1);
            EditorGUILayout.BeginVertical(GUILayout.Width(GetSceneComposerInspectorWidth()));
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Свойства сцены", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                SceneComposerInspectorSections[_sceneComposerInspectorSection],
                EditorStyles.miniBoldLabel, GUILayout.MaxWidth(150f));
            EditorGUILayout.EndHorizontal();
            _sceneComposerInspectorScroll.x = 0f;
            _sceneComposerInspectorScroll = EditorGUILayout.BeginScrollView(
                _sceneComposerInspectorScroll,
                GUILayout.ExpandHeight(true),
                GUILayout.Width(GetSceneComposerInspectorWidth()));
            _sceneComposerInspectorScroll.x = 0f;
            VnSceneComposerScene scene = GetSelectedScene();
            if (scene == null)
            {
                EditorGUILayout.HelpBox("Создайте или выберите сцену слева.", MessageType.Info);
                EditorGUILayout.EndScrollView(); EditorGUILayout.EndVertical(); return;
            }

            EditorGUILayout.Space(4f);
            DrawSceneComposerInspectorSelector();
            EditorGUILayout.Space(6f);
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
                case 7: DrawSceneComposerAdditionalInspector(scene); break;
                case 8: DrawSceneComposerDecorationInspector(scene); break;
                case 9: DrawSceneComposerAdditionalAudioInspector(scene); break;
                default: DrawSceneComposerMusicInspector(scene); break;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
            _sceneComposerInspectorScroll.x = 0f;
            EditorGUILayout.EndVertical();
        }

        private void DrawSceneComposerProjectStorage()
        {
            bool compactHeader = UseCompactSceneComposerHeader();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(
                new GUIContent(compactHeader ? "ROKAS · Новелла" : "ROKAS — Редактор новеллы", "ROKAS VN Scene Composer"),
                EditorStyles.boldLabel, GUILayout.Width(compactHeader ? 138f : 190f));
            if (!compactHeader)
                EditorGUILayout.LabelField("Проект", EditorStyles.miniLabel, GUILayout.Width(48f));
            EditorGUI.BeginChangeCheck();
            string title = EditorGUILayout.TextField(_sceneComposerProject.title ?? string.Empty,
                GUILayout.MinWidth(compactHeader ? 100f : 120f),
                GUILayout.MaxWidth(compactHeader ? 200f : 280f));
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
                    _sceneComposerSavedProjectIndex, saved, EditorStyles.toolbarPopup,
                    GUILayout.Width(compactHeader ? 118f : 150f));
            }
            if (GUILayout.Button(
                    new GUIContent(compactHeader ? "Сохр." : "Сохранить", "Сохранить проект"),
                    EditorStyles.toolbarButton, GUILayout.Width(compactHeader ? 58f : 78f)))
            {
                try { ComposerSaveProject(GetProjectRoot()); }
                catch (Exception exception) { SetSceneComposerStatus("Не удалось сохранить проект: " + exception.Message, MessageType.Error); }
            }
            using (new EditorGUI.DisabledScope(saved.Length == 0))
            {
                if (GUILayout.Button(
                        new GUIContent(compactHeader ? "Откр." : "Открыть", "Открыть сохранённый проект"),
                        EditorStyles.toolbarButton, GUILayout.Width(compactHeader ? 52f : 68f)))
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

        private void DrawSceneComposerMusicInspector(VnSceneComposerScene scene)
        {
            ComposerEnsureSceneMusic(scene);
            EditorGUILayout.LabelField("Музыка", EditorStyles.boldLabel);
            string[] modeLabels = { "Без музыки", "Трек", "Оставить предыдущую" };
            int modeIndex = scene.music.mode == VnSceneComposerMusicMode.Track ? 1 :
                (scene.music.mode == VnSceneComposerMusicMode.KeepPrevious ? 2 : 0);
            EditorGUILayout.LabelField("Режим", modeLabels[modeIndex]);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Без музыки")) ComposerClearSelectedSceneMusic();
            if (GUILayout.Button("Оставить предыдущую")) ComposerKeepPreviousSceneMusic();
            EditorGUILayout.EndHorizontal();

            AudioClip current = ComposerResolveSelectedSceneMusicClip();
            EditorGUI.BeginChangeCheck();
            AudioClip next = (AudioClip)EditorGUILayout.ObjectField(
                "Трек", current, typeof(AudioClip), false);
            if (EditorGUI.EndChangeCheck() && next != null)
                ComposerSetSelectedSceneMusicTrack(next);

            VnSceneComposerAssetEntry[] musicAssets = VnSceneComposerAssetLibrary.FindByPurpose(
                GetProjectRoot(), VnSceneComposerAssetPurpose.Music);
            if (musicAssets.Length > 0)
            {
                _sceneComposerMusicLibraryIndex = Mathf.Clamp(
                    _sceneComposerMusicLibraryIndex, 0, musicAssets.Length - 1);
                string[] names = new string[musicAssets.Length];
                for (int i = 0; i < musicAssets.Length; i++)
                    names[i] = string.IsNullOrWhiteSpace(musicAssets[i].displayName)
                        ? "Музыка " + (i + 1)
                        : musicAssets[i].displayName;
                _sceneComposerMusicLibraryIndex = EditorGUILayout.Popup(
                    "Из библиотеки", _sceneComposerMusicLibraryIndex, names);
                if (GUILayout.Button("Выбрать из библиотеки"))
                    ComposerSetSelectedSceneMusicAsset(musicAssets[_sceneComposerMusicLibraryIndex]);
            }

            if (GUILayout.Button("Добавить аудиофайл"))
            {
                string path = EditorUtility.OpenFilePanel("Добавить музыку", string.Empty, "mp3,wav");
                if (!string.IsNullOrEmpty(path))
                    TrySceneComposerMusicAction(() => ComposerAddExternalMusic(path));
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Предпрослушать")) ComposerPreviewSelectedSceneMusic();
            if (GUILayout.Button("Стоп")) ComposerStopMusicPreview();
            EditorGUILayout.EndHorizontal();

            if (scene.music.mode == VnSceneComposerMusicMode.Track)
            {
                EditorGUI.BeginChangeCheck();
                float volume = EditorGUILayout.Slider("Громкость", scene.music.volume, 0f, 1f);
                if (EditorGUI.EndChangeCheck()) ComposerSetSelectedSceneMusicVolume(volume);

                EditorGUI.BeginChangeCheck();
                bool loop = EditorGUILayout.Toggle("Зациклить", scene.music.loop);
                if (EditorGUI.EndChangeCheck()) ComposerSetSelectedSceneMusicLoop(loop);

                EditorGUI.BeginChangeCheck();
                float fadeIn = Mathf.Max(0f, EditorGUILayout.FloatField("Fade In", scene.music.fadeInSeconds));
                float fadeOut = Mathf.Max(0f, EditorGUILayout.FloatField("Fade Out", scene.music.fadeOutSeconds));
                if (EditorGUI.EndChangeCheck()) ComposerSetSelectedSceneMusicFades(fadeIn, fadeOut);
            }
            else if (scene.music.mode == VnSceneComposerMusicMode.KeepPrevious)
            {
                EditorGUILayout.HelpBox(
                    "Оставить предыдущую: эффективный трек продолжается без лишнего перезапуска.",
                    MessageType.Info);
            }

            string warning = ComposerGetSelectedSceneMusicWarning();
            if (!string.IsNullOrEmpty(warning)) EditorGUILayout.HelpBox(warning, MessageType.Warning);
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

        private string DrawSceneComposerDialogueBodyEditor(VnSceneComposerDialogueBeat beat)
        {
            string currentText = beat != null ? beat.text ?? string.Empty : string.Empty;
            string beatId = beat != null ? beat.beatId ?? string.Empty : string.Empty;
            GUIStyle style = SceneComposerDialogueTextAreaStyle;

            float lineHeight = Mathf.Max(EditorGUIUtility.singleLineHeight, style.lineHeight);
            float verticalPadding = style.padding != null ? style.padding.vertical : 0f;
            float minHeight = lineHeight * SceneComposerDialogueEditorMinVisibleLines + verticalPadding;
            float maxHeight = lineHeight * SceneComposerDialogueEditorMaxVisibleLines + verticalPadding;
            float availableWidth = Mathf.Max(
                lineHeight * 8f,
                GetSceneComposerInspectorWidth() - lineHeight * 2f);

            GUIContent content = new GUIContent(string.IsNullOrEmpty(currentText) ? " " : currentText);
            float measuredHeight = Mathf.Max(minHeight, style.CalcHeight(content, availableWidth));
            float viewportHeight = Mathf.Clamp(measuredHeight, minHeight, maxHeight);
            bool needsVerticalScroll = measuredHeight > viewportHeight + .5f;

            if (_sceneComposerDialogueEditorScrollByBeat == null)
                _sceneComposerDialogueEditorScrollByBeat =
                    new Dictionary<string, Vector2>(StringComparer.Ordinal);

            if (!_sceneComposerDialogueEditorScrollByBeat.TryGetValue(beatId, out Vector2 scroll))
                scroll = Vector2.zero;

            scroll.x = 0f;
            scroll.y = needsVerticalScroll
                ? Mathf.Clamp(scroll.y, 0f, Mathf.Max(0f, measuredHeight - viewportHeight))
                : 0f;

            scroll = EditorGUILayout.BeginScrollView(
                scroll,
                false,
                needsVerticalScroll,
                GUILayout.Height(viewportHeight),
                GUILayout.ExpandWidth(true));

            string nextText = EditorGUILayout.TextArea(
                currentText,
                style,
                GUILayout.Height(measuredHeight),
                GUILayout.Width(availableWidth),
                GUILayout.ExpandWidth(false));

            EditorGUILayout.EndScrollView();

            scroll.x = 0f;
            if (!needsVerticalScroll) scroll.y = 0f;
            _sceneComposerDialogueEditorScrollByBeat[beatId] = scroll;
            return nextText;
        }

        private void DrawSceneComposerTextInspector(VnSceneComposerScene scene)
        {
            EditorGUILayout.LabelField("Текст", EditorStyles.boldLabel);

            _sceneComposerTextDialogueExpanded = EditorGUILayout.Foldout(
                _sceneComposerTextDialogueExpanded, "Реплика", true);
            if (_sceneComposerTextDialogueExpanded)
            {
                EditorGUI.indentLevel++;
                DrawSceneComposerDialogueAuthoringSection(scene);
                EditorGUI.indentLevel--;
            }

            VnSceneComposerDialogueBeat selectedBeat = ComposerGetSelectedDialogueBeat();
            _sceneComposerTextStagingExpanded = EditorGUILayout.Foldout(
                _sceneComposerTextStagingExpanded, "Персонажи и постановка", true);
            if (_sceneComposerTextStagingExpanded)
            {
                EditorGUI.indentLevel++;
                if (selectedBeat != null)
                    DrawSceneComposerBeatCharacterStagingInspector(scene, selectedBeat);
                else
                    EditorGUILayout.LabelField(
                        "Выберите реплику, чтобы настроить постановку.",
                        EditorStyles.wordWrappedMiniLabel);
                EditorGUI.indentLevel--;
            }

            _sceneComposerTextPresentationExpanded = EditorGUILayout.Foldout(
                _sceneComposerTextPresentationExpanded, "Оформление диалога", true);
            if (_sceneComposerTextPresentationExpanded)
            {
                EditorGUI.indentLevel++;
                DrawSceneComposerDialoguePresentationSection(scene);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawSceneComposerDialogueAuthoringSection(VnSceneComposerScene scene)
        {
            EditorGUILayout.LabelField("Реплики", EditorStyles.miniBoldLabel);

            VnSceneComposerDialogueBeat selectedBeat = ComposerGetSelectedDialogueBeat();
            if (scene.dialogueBeats != null)
            {
                for (int i = 0; i < scene.dialogueBeats.Count; i++)
                {
                    VnSceneComposerDialogueBeat beat = scene.dialogueBeats[i];
                    if (beat == null) continue;
                    bool selected = selectedBeat != null &&
                                    string.Equals(
                                        beat.beatId, selectedBeat.beatId,
                                        StringComparison.Ordinal);
                    string speakerLabel = beat.narration
                        ? "Текст без персонажа"
                        : (string.IsNullOrWhiteSpace(beat.speaker)
                            ? "Без говорящего"
                            : beat.speaker);
                    string textLabel =
                        (beat.text ?? string.Empty).Replace('\n', ' ').Trim();
                    if (textLabel.Length > 28)
                        textLabel = textLabel.Substring(0, 28) + "…";
                    string rowLabel = (i + 1) + ". " + speakerLabel +
                                      (string.IsNullOrEmpty(textLabel)
                                          ? string.Empty
                                          : " — " + textLabel);
                    if (GUILayout.Button(
                            rowLabel,
                            selected
                                ? EditorStyles.miniButtonMid
                                : EditorStyles.miniButton))
                        ComposerSelectDialogueBeat(beat.beatId);
                }
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Реплика")) ComposerAddDialogueBeat();
            selectedBeat = ComposerGetSelectedDialogueBeat();
            using (new EditorGUI.DisabledScope(selectedBeat == null))
            {
                if (GUILayout.Button("Дублировать"))
                    ComposerDuplicateSelectedDialogueBeat();
                if (GUILayout.Button("Удалить"))
                    ComposerDeleteSelectedDialogueBeat();
            }
            EditorGUILayout.EndHorizontal();

            selectedBeat = ComposerGetSelectedDialogueBeat();
            int selectedIndex = selectedBeat != null
                ? VnSceneComposerDialogue.FindIndex(scene, selectedBeat.beatId)
                : -1;
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(selectedIndex <= 0))
            {
                if (GUILayout.Button("↑", GUILayout.Width(36f)))
                    ComposerMoveSelectedDialogueBeat(-1);
            }
            using (new EditorGUI.DisabledScope(
                       selectedIndex < 0 || scene.dialogueBeats == null ||
                       selectedIndex >= scene.dialogueBeats.Count - 1))
            {
                if (GUILayout.Button("↓", GUILayout.Width(36f)))
                    ComposerMoveSelectedDialogueBeat(1);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            selectedBeat = ComposerGetSelectedDialogueBeat();
            if (selectedBeat == null) return;

            EditorGUI.BeginChangeCheck();
            bool narration =
                EditorGUILayout.Toggle(
                    "Текст без персонажа", selectedBeat.narration);
            string speaker =
                EditorGUILayout.TextField(
                    "Говорящий", selectedBeat.speaker ?? string.Empty);
            EditorGUILayout.LabelField("Текст реплики");
            string text = DrawSceneComposerDialogueBodyEditor(selectedBeat);
            if (EditorGUI.EndChangeCheck())
            {
                RecordSceneComposerUndo("Edit VN Dialogue Beat");
                selectedBeat.narration = narration;
                selectedBeat.speaker = narration ? string.Empty : speaker;
                selectedBeat.text = text;
                MarkSceneComposerChanged();
            }
        }

        private void DrawSceneComposerDialoguePresentationSection(
            VnSceneComposerScene scene)
        {
            DrawSceneComposerDialogueTypographyInspector(scene);

            VnPresentationWorkshopPreset effective =
                VnSceneComposerComposition.ResolvePresentation(
                    _sceneComposerProject, scene);
            VnWorkshopTypewriterValues typewriter =
                VnPresentationWorkshopVn10Resolver.ResolveTypewriter(effective);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Появление текста", EditorStyles.miniBoldLabel);
            EditorGUI.BeginChangeCheck();
            float nextSpeed = EditorGUILayout.Slider(
                new GUIContent(
                    "Скорость текста",
                    "Скорость появления символов во время реплики."),
                typewriter.CharactersPerSecond, 1f, 240f);
            if (EditorGUI.EndChangeCheck())
                SetSceneComposerTypewriterSpeed(nextSpeed);

            DrawSceneComposerDialoguePanelVisualControls(scene);
        }

        private void DrawSceneComposerDialoguePanelVisualControls(VnSceneComposerScene scene)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Плашка диалога", EditorStyles.miniBoldLabel);

            EditorGUILayout.LabelField("Применить:", EditorStyles.miniLabel);
            int scope = _sceneComposerPresentationProjectDefaults ? 1 : 0;
            int nextScope = GUILayout.Toolbar(scope, new[] { "Только к этой сцене", "Ко всем сценам" });
            if (nextScope != scope) ComposerSetPresentationScope(nextScope == 1);

            VnPresentationWorkshopPreset active = ComposerGetActivePresentationPreset();
            if (active.dialoguePanelVisual == null)
                active.dialoguePanelVisual = new VnWorkshopDialoguePanelVisualOverride();
            bool hasCustom = active.dialoguePanelVisual.hasAssetGuid;

            EditorGUI.BeginChangeCheck();
            int nextMode = EditorGUILayout.Popup("Плашка", hasCustom ? 1 : 0,
                new[] { "По умолчанию", "Своя PNG" });
            if (EditorGUI.EndChangeCheck())
            {
                if (nextMode == 0)
                {
                    ComposerResetDialoguePanelVisual();
                }
                else if (!hasCustom)
                {
                    string source = EditorUtility.OpenFilePanel(
                        "Выбрать PNG плашки диалога", string.Empty, "png");
                    if (!string.IsNullOrEmpty(source))
                        TrySceneComposerPresentationAction(() => ComposerSetExternalDialoguePanelPng(source));
                }
            }

            active = ComposerGetActivePresentationPreset();
            hasCustom = active.dialoguePanelVisual != null && active.dialoguePanelVisual.hasAssetGuid;
            Texture2D activeTexture = ComposerGetActiveDialoguePanelVisualAsset();
            if (hasCustom)
            {
                EditorGUI.BeginChangeCheck();
                Texture2D nextTexture = (Texture2D)EditorGUILayout.ObjectField(
                    "Ресурс", activeTexture, typeof(Texture2D), false);
                if (EditorGUI.EndChangeCheck())
                {
                    if (nextTexture != null)
                        TrySceneComposerPresentationAction(() => ComposerSetDialoguePanelVisualAsset(nextTexture));
                    else
                        ComposerResetDialoguePanelVisual();
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Выбрать PNG"))
                {
                    string source = EditorUtility.OpenFilePanel(
                        "Выбрать PNG плашки диалога", string.Empty, "png");
                    if (!string.IsNullOrEmpty(source))
                        TrySceneComposerPresentationAction(() => ComposerSetExternalDialoguePanelPng(source));
                }
                if (GUILayout.Button("Сбросить")) ComposerResetDialoguePanelVisual();
                EditorGUILayout.EndHorizontal();
            }
            else if (!_sceneComposerPresentationProjectDefaults &&
                     ComposerGetEffectiveDialoguePanelVisualAsset(scene) != null)
            {
                EditorGUILayout.LabelField("Используется общая плашка проекта.", EditorStyles.miniLabel);
            }

            string warning = ComposerGetEffectiveDialoguePanelVisualWarning(scene);
            if (!string.IsNullOrEmpty(warning))
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }

        private void SetSceneComposerTypewriterSpeed(float charactersPerSecond)
        {
            EnsureSceneComposerProject();
            VnPresentationWorkshopPreset inheritedPreset = _sceneComposerPresentationProjectDefaults
                ? new VnPresentationWorkshopPreset()
                : (_sceneComposerProject.defaultPresentation ?? new VnPresentationWorkshopPreset());
            VnWorkshopTypewriterValues inheritedTypewriter =
                VnPresentationWorkshopVn10Resolver.ResolveTypewriter(inheritedPreset);
            MutateComposerPresentation("Edit VN Scene Typewriter Speed", preset =>
            {
                if (preset.typewriter == null) preset.typewriter = new VnWorkshopTypewriterOverride();
                preset.typewriter.hasCharactersPerSecond =
                    !Mathf.Approximately(charactersPerSecond, inheritedTypewriter.CharactersPerSecond);
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

        private static float DrawSceneComposerDurationControl(string label, float value, float minimum)
        {
            Rect rowRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            const float fieldWidth = 58f;
            const float unitWidth = 14f;
            const float gap = 4f;
            float labelWidth = Mathf.Min(EditorGUIUtility.labelWidth, rowRect.width * .42f);
            Rect labelRect = new Rect(rowRect.x, rowRect.y, labelWidth, rowRect.height);
            Rect unitRect = new Rect(rowRect.xMax - unitWidth, rowRect.y, unitWidth, rowRect.height);
            Rect fieldRect = new Rect(unitRect.x - gap - fieldWidth, rowRect.y, fieldWidth, rowRect.height);
            float sliderWidth = Mathf.Max(40f, fieldRect.x - gap - labelRect.xMax);
            Rect sliderRect = new Rect(labelRect.xMax, rowRect.y, sliderWidth, rowRect.height);

            EditorGUI.LabelField(labelRect, label);
            float nextValue = GUI.HorizontalSlider(
                sliderRect, value, minimum, ComposerDurationMaximum);
            nextValue = EditorGUI.FloatField(fieldRect, nextValue);
            EditorGUI.LabelField(unitRect, "с");
            return nextValue;
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
            string[] transitionLabels = { "Без анимации", "Плавный переход", "Переход со сдвигом" };
            string[] directionLabels = { "Слева", "Справа" };
            EditorGUILayout.LabelField(new GUIContent(
                "Переход персонажа",
                "Один общий способ используется для появления и исчезновения персонажа."),
                EditorStyles.miniBoldLabel);
            EditorGUI.BeginChangeCheck();
            int nextMode = Mathf.Clamp((int)transition.Mode, 0, transitionLabels.Length - 1);
            nextMode = EditorGUILayout.Popup("Способ", nextMode, transitionLabels);
            float nextDuration = DrawSceneComposerDurationControl("Длительность", transition.Duration, 0f);
            VnWorkshopSlideDirection nextDirection = transition.SlideDirection;
            if ((VnWorkshopCharacterTransitionMode)nextMode == VnWorkshopCharacterTransitionMode.SlideAndFade)
            {
                int directionIndex = EditorGUILayout.Popup("Направление", (int)transition.SlideDirection, directionLabels);
                nextDirection = (VnWorkshopSlideDirection)directionIndex;
            }
            if (EditorGUI.EndChangeCheck())
                ComposerSetCharacterTransition((VnWorkshopCharacterTransitionMode)nextMode, nextDuration, transition.FadeDuration, transition.SlideDistance, nextDirection, transition.Easing);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("▶ Проверить появление")) ComposerPreviewFocusedEffect(VnWorkshopPreviewEffect.CharacterEnter);
            if (GUILayout.Button("▶ Проверить исчезновение")) ComposerPreviewFocusedEffect(VnWorkshopPreviewEffect.CharacterExit);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("Появление и исчезновение используют единый профиль перехода, сохраняя существующую логику сцены.", MessageType.None);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(new GUIContent("Смена позы / эмоции", "Как меняется внешний вид персонажа между сценами."), EditorStyles.miniBoldLabel);
            EditorGUI.BeginChangeCheck();
            float expressionDuration = DrawSceneComposerDurationControl("Длительность", expression.Duration, 0f);
            if (EditorGUI.EndChangeCheck()) ComposerSetExpressionTransition(expressionDuration, expression.Easing);
            if (GUILayout.Button("▶ Проверить")) ComposerPreviewFocusedEffect(VnWorkshopPreviewEffect.Expression);
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
            if (EditorGUI.EndChangeCheck()) ComposerSetBounceAmplitude(nextAmplitude);

            EditorGUI.BeginChangeCheck();
            float nextBounceDuration = DrawSceneComposerDurationControl("Длительность", bounce.Duration, .01f);
            if (EditorGUI.EndChangeCheck()) ComposerSetBounceDuration(nextBounceDuration);
            if (GUILayout.Button("▶ Проверить")) ComposerPreviewFocusedEffect(VnWorkshopPreviewEffect.Bounce);
        }

        private void DrawSceneComposerSceneAnimationInspector(VnSceneComposerScene scene)
        {
            EditorGUILayout.LabelField("Анимация сцены", EditorStyles.boldLabel);
            DrawSceneComposerAnimationScope();
            VnPresentationWorkshopPreset effective = GetSceneComposerAnimationDisplayPreset(scene);
            VnWorkshopBackgroundTransitionValues background = VnPresentationWorkshopVn10Resolver.ResolveBackgroundTransition(effective);
            VnWorkshopStageLayoutValues stage = VnPresentationWorkshopVn10Resolver.ResolveStageLayout(effective);
            VnWorkshopSpeakerFocusValues focus = VnPresentationWorkshopVn10Resolver.ResolveSpeakerFocus(effective);

            if (scene.transition == null) scene.transition = new VnSceneComposerTransition();
            EditorGUILayout.LabelField(
                new GUIContent("Переход между сценами", "Кинематографический переход при входе в эту сцену с предыдущей."),
                EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "Настройка входа в эту сцену. Прямой запуск сцены переход не проигрывает.",
                MessageType.None);
            string[] sceneTransitionLabels = { "Без перехода", "Тёмная шторка" };
            string[] sceneTransitionDirectionLabels = { "Слева направо", "Справа налево" };
            EditorGUI.BeginChangeCheck();
            int sceneTransitionType = EditorGUILayout.Popup(
                "Способ",
                scene.transition.sceneTransitionType == VnSceneComposerSceneTransitionType.DarkCurtain ? 1 : 0,
                sceneTransitionLabels);
            VnSceneComposerSceneTransitionDirection sceneTransitionDirection =
                scene.transition.sceneTransitionDirection;
            float sceneTransitionDuration = scene.transition.sceneTransitionDuration;
            if (sceneTransitionType == 1)
            {
                int directionIndex = sceneTransitionDirection == VnSceneComposerSceneTransitionDirection.RightToLeft ? 1 : 0;
                directionIndex = EditorGUILayout.Popup("Направление", directionIndex, sceneTransitionDirectionLabels);
                sceneTransitionDirection = directionIndex == 1
                    ? VnSceneComposerSceneTransitionDirection.RightToLeft
                    : VnSceneComposerSceneTransitionDirection.LeftToRight;
                sceneTransitionDuration = DrawSceneComposerDurationControl(
                    "Длительность", sceneTransitionDuration, 0f);
            }
            if (EditorGUI.EndChangeCheck())
            {
                ComposerSetSelectedSceneBoundaryTransition(
                    sceneTransitionType == 1
                        ? VnSceneComposerSceneTransitionType.DarkCurtain
                        : VnSceneComposerSceneTransitionType.None,
                    sceneTransitionDirection,
                    sceneTransitionDuration);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                new GUIContent("Переход фона", "Внутренняя анимация смены фона после входа в сцену."),
                EditorStyles.miniBoldLabel);
            string[] backgroundLabels = { "Без перехода", "Плавный переход", "Шторка" };
            string[] curtainDirectionLabels = { "Справа налево", "Слева направо" };
            EditorGUI.BeginChangeCheck();
            int backgroundMode = EditorGUILayout.Popup("Способ", (int)background.Mode, backgroundLabels);
            float backgroundDuration = DrawSceneComposerDurationControl("Длительность", background.Duration, 0f);
            VnWorkshopCurtainDirection curtainDirection = background.Direction;
            if ((VnWorkshopBackgroundTransitionMode)backgroundMode == VnWorkshopBackgroundTransitionMode.Curtain)
            {
                int direction = EditorGUILayout.Popup("Направление", (int)background.Direction, curtainDirectionLabels);
                curtainDirection = (VnWorkshopCurtainDirection)direction;
            }
            if (EditorGUI.EndChangeCheck()) ComposerSetBackgroundTransition((VnWorkshopBackgroundTransitionMode)backgroundMode, backgroundDuration, background.CurtainDarkness, curtainDirection, background.Easing);
            if (GUILayout.Button("▶ Проверить")) ComposerPreviewFocusedEffect(VnWorkshopPreviewEffect.BackgroundTransition);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(new GUIContent("Расположение персонажей", "Как персонажи располагаются и перестраиваются на сцене."), EditorStyles.miniBoldLabel);
            string[] characterCountLabels = { "Нет персонажей", "1 персонаж", "2 персонажа", "3 персонажа" };
            string[] characterSlotLabels = { "", "Центр", "Слева / Справа", "Слева / Центр / Справа" };
            int characterCount = Mathf.Clamp(scene.characters != null ? scene.characters.Count : 0, 0, 3);
            EditorGUILayout.LabelField("Схема", characterCountLabels[characterCount] + (characterCount > 0 ? " · " + characterSlotLabels[characterCount] : string.Empty));
            EditorGUI.BeginChangeCheck();
            float repositionDuration = DrawSceneComposerDurationControl("Длительность перестановки", stage.RepositionDuration, 0f);
            if (EditorGUI.EndChangeCheck()) ComposerSetStageLayout(stage.LeftX, stage.CenterX, stage.RightX, stage.SlotY, stage.LeftScale, stage.CenterScale, stage.RightScale, stage.Spacing, repositionDuration, stage.Easing);
            if (GUILayout.Button("▶ Проверить")) ComposerPreviewFocusedEffect(VnWorkshopPreviewEffect.StageOneTwo);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(new GUIContent("Фокус говорящего", "Выделяет говорящего персонажа и приглушает остальных."), EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox("Фокус автоматически следует за полем «Говорящий» при нескольких персонажах.", MessageType.None);
            EditorGUI.BeginChangeCheck();
            float inactiveBrightness = EditorGUILayout.Slider("Яркость остальных", focus.InactiveBrightness, 0f, 1.5f);
            float focusDuration = DrawSceneComposerDurationControl("Длительность фокуса", focus.TransitionDuration, 0f);
            if (EditorGUI.EndChangeCheck()) ComposerSetSpeakerFocus(focus.ActiveScale, focus.ActiveBrightness, focus.ActiveForwardOffset, focus.InactiveScale, inactiveBrightness, focus.InactiveAlpha, focusDuration, focus.Easing);
            if (GUILayout.Button("▶ Проверить")) ComposerPreviewFocusedEffect(VnWorkshopPreviewEffect.SpeakerSwitch);
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
        }

        private void DrawSceneComposerAdvancedTimingControls(VnSceneComposerScene scene)
        {
            VnPresentationWorkshopPreset effective = GetSceneComposerAnimationDisplayPreset(scene);
            VnWorkshopTimingValues timing = VnPresentationWorkshopVn10Resolver.ResolveTiming(effective);
            EditorGUILayout.LabelField("Точный тайминг перехода", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "Редкая настройка паузы между автоматически проигрываемыми сценами. Обычной сцене она не требуется.",
                MessageType.None);
            EditorGUI.BeginChangeCheck();
            float sequenceGap = EditorGUILayout.Slider(
                "Пауза между сценами, с", timing.AutoPreviewSequenceGap, 0f, 10f);
            if (EditorGUI.EndChangeCheck())
                ComposerSetTiming(timing.MinimumBeatSettleDuration, timing.PostTransitionBreathingRoom, sequenceGap);
        }

        private void DrawSceneComposerDecorationInspector(VnSceneComposerScene scene)
        {
            ComposerEnsureDecorations(scene);
            EditorGUILayout.LabelField("Декорации", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Статичные PNG-элементы этой сцены. Они не сбрасывают фон, видео, реплики или состояния персонажей.",
                MessageType.None);

            if (GUILayout.Button("Добавить PNG"))
            {
                string path = EditorUtility.OpenFilePanel("Добавить PNG-декорацию", string.Empty, "png");
                if (!string.IsNullOrEmpty(path))
                    TrySceneComposerDecorationAction(() => ComposerAddExternalDecorationPng(path));
            }

            VnSceneComposerAssetEntry[] overlays = VnSceneComposerAssetLibrary.FindByPurpose(
                GetProjectRoot(), VnSceneComposerAssetPurpose.UiOverlay);
            if (overlays.Length > 0)
            {
                _sceneComposerDecorationLibraryIndex = Mathf.Clamp(_sceneComposerDecorationLibraryIndex, 0, overlays.Length - 1);
                string[] labels = new string[overlays.Length];
                for (int i = 0; i < overlays.Length; i++)
                    labels[i] = string.IsNullOrWhiteSpace(overlays[i].displayName) ? "PNG " + (i + 1) : overlays[i].displayName;
                _sceneComposerDecorationLibraryIndex = EditorGUILayout.Popup(
                    "Из библиотеки", _sceneComposerDecorationLibraryIndex, labels);
                if (GUILayout.Button("Добавить выбранное"))
                {
                    VnSceneComposerAssetEntry entry = overlays[_sceneComposerDecorationLibraryIndex];
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(entry.assetPath);
                    if (texture != null) ComposerAddDecorationAsset(texture);
                    else SetSceneComposerStatus("Файл выбранной декорации не найден.", MessageType.Warning);
                }
            }

            EditorGUILayout.Space();
            if (scene.decorations.Count == 0)
            {
                EditorGUILayout.HelpBox("В этой сцене пока нет декораций.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Элементы сцены", EditorStyles.miniBoldLabel);
            for (int i = 0; i < scene.decorations.Count; i++)
            {
                VnSceneComposerDecoration item = scene.decorations[i];
                if (item == null) continue;
                bool selected = string.Equals(item.decorationId, _sceneComposerSelectedDecorationId, StringComparison.Ordinal);
                string label = string.IsNullOrWhiteSpace(item.displayName) ? "Декорация " + (i + 1) : item.displayName;
                if (!item.visible) label += " (скрыта)";
                if (GUILayout.Button(label, selected ? EditorStyles.miniButtonMid : EditorStyles.miniButton))
                    ComposerSelectDecoration(item.decorationId);
            }

            VnSceneComposerDecoration decoration = ComposerGetSelectedDecoration();
            if (decoration == null)
            {
                EditorGUILayout.HelpBox("Выберите декорацию в списке или прямо в предпросмотре.", MessageType.None);
                return;
            }

            Texture2D currentTexture = ComposerResolveDecorationTexture(decoration);
            EditorGUI.BeginChangeCheck();
            Texture2D nextTexture = (Texture2D)EditorGUILayout.ObjectField(
                "Изображение", currentTexture, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck() && nextTexture != null) ComposerSetSelectedDecorationAsset(nextTexture);

            EditorGUI.BeginChangeCheck();
            float x = EditorGUILayout.FloatField("X", decoration.position.x);
            float y = EditorGUILayout.FloatField("Y", decoration.position.y);
            if (EditorGUI.EndChangeCheck()) ComposerSetSelectedDecorationPosition(new Vector2(x, y));

            EditorGUI.BeginChangeCheck();
            float scale = EditorGUILayout.Slider("Размер", decoration.scale, .05f, 5f);
            if (EditorGUI.EndChangeCheck()) ComposerSetSelectedDecorationScale(scale);

            EditorGUI.BeginChangeCheck();
            float opacity = EditorGUILayout.Slider("Прозрачность", decoration.opacity, 0f, 1f);
            if (EditorGUI.EndChangeCheck()) ComposerSetSelectedDecorationOpacity(opacity);

            EditorGUI.BeginChangeCheck();
            bool visible = EditorGUILayout.Toggle("Показывать", decoration.visible);
            if (EditorGUI.EndChangeCheck()) ComposerSetSelectedDecorationVisible(visible);

            string[] layerLabels = { "За персонажами", "Перед персонажами" };
            int layer = decoration.layer == VnSceneComposerDecorationLayer.FrontCharacters ? 1 : 0;
            EditorGUI.BeginChangeCheck();
            int nextLayer = EditorGUILayout.Popup("Слой", layer, layerLabels);
            if (EditorGUI.EndChangeCheck())
                ComposerSetSelectedDecorationLayer(nextLayer == 1
                    ? VnSceneComposerDecorationLayer.FrontCharacters
                    : VnSceneComposerDecorationLayer.BehindCharacters);

            string warning = ComposerGetDecorationWarning(decoration);
            if (!string.IsNullOrEmpty(warning)) EditorGUILayout.HelpBox(warning, MessageType.Warning);
            if (GUILayout.Button("Удалить декорацию")) ComposerDeleteSelectedDecoration();
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
            EditorGUILayout.HelpBox(
                "Редкие настройки для точной настройки проекта. Для обычной сцены этот раздел не нужен.",
                MessageType.None);

            DrawSceneComposerAssetLibraryControls(scene);

            _sceneComposerAdvancedAnimationGroupExpanded = EditorGUILayout.Foldout(
                _sceneComposerAdvancedAnimationGroupExpanded, "Расширенная анимация", true);
            if (_sceneComposerAdvancedAnimationGroupExpanded)
            {
                EditorGUI.indentLevel++;
                DrawSceneComposerAdvancedTimingControls(scene);
                EditorGUI.indentLevel--;
            }

            _sceneComposerAdvancedPresentationGroupExpanded = EditorGUILayout.Foldout(
                _sceneComposerAdvancedPresentationGroupExpanded, "Расширенное оформление", true);
            if (_sceneComposerAdvancedPresentationGroupExpanded)
            {
                EditorGUI.indentLevel++;
                DrawSceneComposerPresentationInspector(scene);
                EditorGUI.indentLevel--;
            }

            _sceneComposerAdvancedTechnicalGroupExpanded = EditorGUILayout.Foldout(
                _sceneComposerAdvancedTechnicalGroupExpanded, "Технические параметры", true);
            if (_sceneComposerAdvancedTechnicalGroupExpanded)
            {
                EditorGUI.indentLevel++;
                DrawSceneComposerProjectTechnicalInfo();
                EditorGUI.indentLevel--;
            }
        }

        private void DrawSceneComposerPresentationInspector(VnSceneComposerScene scene) { DrawSceneComposerPresentationControls(scene); }

        private void EnsureSceneComposerProject()
        {
            if (_sceneComposerProject == null) _sceneComposerProject = new VnSceneComposerProject();
            bool migrated = VnSceneComposerSerialization.EnsureCurrentSchema(_sceneComposerProject);
            if (_sceneComposerProject.scenes == null) _sceneComposerProject.scenes = new List<VnSceneComposerScene>();
            if (_sceneComposerProject.defaultPresentation == null) _sceneComposerProject.defaultPresentation = new VnPresentationWorkshopPreset();
            if (migrated) EditorUtility.SetDirty(this);
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

        private VnSceneComposerDialogueBeat ComposerGetSelectedDialogueBeat()
        {
            VnSceneComposerScene scene = GetSelectedScene();
            if (scene == null) return null;
            VnSceneComposerDialogueBeat selected = ResolveSceneComposerDialogueBeat(
                scene, _sceneComposerSelectedDialogueBeatId);
            if (selected == null)
            {
                _sceneComposerSelectedDialogueBeatId = string.Empty;
                return null;
            }
            _sceneComposerSelectedDialogueBeatId = selected.beatId ?? string.Empty;
            return selected;
        }

        private static VnSceneComposerDialogueBeat ResolveSceneComposerDialogueBeat(
            VnSceneComposerScene scene, string beatId)
        {
            if (scene == null || scene.dialogueBeats == null || scene.dialogueBeats.Count == 0) return null;
            int selectedIndex = VnSceneComposerDialogue.FindIndex(scene, beatId);
            if (selectedIndex >= 0) return scene.dialogueBeats[selectedIndex];
            for (int i = 0; i < scene.dialogueBeats.Count; i++)
            {
                if (scene.dialogueBeats[i] != null) return scene.dialogueBeats[i];
            }
            return null;
        }

        private void SelectFirstSceneComposerDialogueBeat(VnSceneComposerScene scene)
        {
            VnSceneComposerDialogueBeat beat = ResolveSceneComposerDialogueBeat(scene, string.Empty);
            _sceneComposerSelectedDialogueBeatId = beat != null ? beat.beatId ?? string.Empty : string.Empty;
        }

        private void EnsureSelectedScene()
        {
            EnsureSceneComposerProject();
            VnSceneComposerScene selected = GetSelectedScene();
            if (selected != null)
            {
                ComposerGetSelectedDialogueBeat();
                return;
            }
            _sceneComposerSelectedSceneId = _sceneComposerProject.scenes.Count > 0 && _sceneComposerProject.scenes[0] != null
                ? _sceneComposerProject.scenes[0].sceneId : string.Empty;
            SelectFirstSceneComposerDialogueBeat(GetSelectedScene());
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
            if (!_sceneComposerWorkspaceActive || _sceneComposerPlayback == null ||
                (!_sceneComposerPlayback.IsPlaying && !_sceneComposerPlayback.IsSceneTransitionActive))
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
            if (index < 0 || index >= _sceneComposerProject.scenes.Count) return;
            VnSceneComposerScene scene = _sceneComposerProject.scenes[index];
            if (scene == null || string.Equals(scene.sceneId, _sceneComposerSelectedSceneId, StringComparison.Ordinal)) return;
            _sceneComposerSelectedSceneId = scene.sceneId;
            SelectFirstSceneComposerDialogueBeat(scene);
            _sceneComposerSelectedCharacterIndex = -1;
        }

        private void SyncSelectedDialogueBeatFromPlayback()
        {
            if (_sceneComposerPlayback == null) return;
            int sceneIndex = _sceneComposerPlayback.CurrentSceneIndex;
            if (sceneIndex < 0 || sceneIndex >= _sceneComposerProject.scenes.Count) return;
            VnSceneComposerScene scene = _sceneComposerProject.scenes[sceneIndex];
            int beatIndex = _sceneComposerPlayback.CurrentBeatIndex;
            if (scene == null || scene.dialogueBeats == null ||
                beatIndex < 0 || beatIndex >= scene.dialogueBeats.Count)
                return;
            VnSceneComposerDialogueBeat beat = scene.dialogueBeats[beatIndex];
            if (beat == null) return;
            _sceneComposerSelectedDialogueBeatId = beat.beatId ?? string.Empty;
            _sceneComposerSelectedCharacterStagingId = string.Empty;
        }

        private void DisposeSceneComposerRuntimeResources()
        {
            ComposerStopMusicPreview();
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

        private static string GetSceneComposerBaseStateId(
            VnSceneComposerScene scene, string characterId)
        {
            if (scene == null || scene.characters == null ||
                string.IsNullOrWhiteSpace(characterId))
                return string.Empty;
            for (int i = 0; i < scene.characters.Count; i++)
            {
                VnSceneComposerCharacter character = scene.characters[i];
                if (character == null) continue;
                string id =
                    VnSceneComposerBeatCharacterStateResolver.ResolveCharacterId(character);
                if (string.Equals(id, characterId, StringComparison.OrdinalIgnoreCase))
                    return character.stateId ?? string.Empty;
            }
            return string.Empty;
        }

        private static string[] GetSceneComposerBeatTargetCharacterIds(VnSceneComposerScene scene)
        {
            if (scene == null || scene.characters == null) return Array.Empty<string>();
            return scene.characters
                .Where(character => character != null)
                .Select(VnSceneComposerBeatCharacterStateResolver.ResolveCharacterId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string GetSceneComposerBeatStateDisplayName(string characterId, string stateId)
        {
            if (VnSceneComposerCharacterStateResolver.TryResolve(
                    stateId, out VnSceneComposerResolvedCharacterState resolved) &&
                !string.IsNullOrWhiteSpace(resolved.DisplayName) &&
                !string.Equals(resolved.DisplayName, resolved.Id, StringComparison.Ordinal))
                return resolved.DisplayName;

            string label = stateId ?? string.Empty;
            string prefix = (characterId ?? string.Empty).Trim().ToLowerInvariant() + "_";
            if (label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                label = label.Substring(prefix.Length);
            label = label.Replace('_', ' ').Trim();
            if (label.Length == 0) return "Состояние";
            return char.ToUpperInvariant(label[0]) + label.Substring(1);
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
