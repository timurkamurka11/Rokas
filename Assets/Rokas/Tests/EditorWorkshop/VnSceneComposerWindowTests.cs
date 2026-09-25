using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using Rokas.Presentation;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerWindowTests
    {
        private const string EditorAssembly = "Rokas.Editor";
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";
        private const string KnownBackgroundPath = "Assets/Rokas/Art/VN/Backgrounds/VN_BusStop_Rain_Night.png";
        private const string KnownBackgroundGuid = "88761c34d7af479ea57efce7073ff8d4";

        [Test]
        public void SceneComposerWorkspaceIsHostedByExistingWorkshopWindow()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            Assert.That(typeof(EditorWindow).IsAssignableFrom(windowType), Is.True);
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                RequireInstance(windowType, "ActivateSceneComposerWorkspace").Invoke(window, null);
                Assert.That((bool)GetField(window, "_sceneComposerWorkspaceActive"), Is.True);
                RequireInstance(windowType, "DrawSceneComposerWorkspace");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ActivatingSceneComposerRestoresEditableCurrentPreview()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                object comparison = GetField(window, "comparisonView");
                SetField(window, "comparisonView", Enum.Parse(comparison.GetType(), "Original"));
                Assert.That(GetField(window, "comparisonView").ToString(), Is.EqualTo("Original"));

                RequireInstance(windowType, "ActivateSceneComposerWorkspace").Invoke(window, null);

                Assert.That(GetField(window, "comparisonView").ToString(), Is.EqualTo("Current"),
                    "The normal Scene Composer must never inherit the hidden legacy Original comparison state, because that makes the preview ignore ordinary edits and pointer input.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void SceneCrudWiringPreservesStableIdsAndDuplicateGetsNewId()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                MethodInfo add = RequireInstance(windowType, "ComposerAddScene");
                MethodInfo rename = RequireInstance(windowType, "ComposerRenameSelectedScene", typeof(string));
                MethodInfo duplicate = RequireInstance(windowType, "ComposerDuplicateSelectedScene");
                MethodInfo move = RequireInstance(windowType, "ComposerMoveSelectedScene", typeof(int));
                MethodInfo delete = RequireInstance(windowType, "ComposerDeleteSelectedScene");

                add.Invoke(window, null);
                rename.Invoke(window, new object[] { "Intro" });
                IList scenes = Scenes(window);
                Assert.That(scenes.Count, Is.EqualTo(1));
                string originalId = GetString(scenes[0], "sceneId");
                Assert.That(GetString(scenes[0], "label"), Is.EqualTo("Intro"));

                duplicate.Invoke(window, null);
                scenes = Scenes(window);
                Assert.That(scenes.Count, Is.EqualTo(2));
                string duplicateId = GetString(scenes[1], "sceneId");
                Assert.That(duplicateId, Is.Not.EqualTo(originalId));

                move.Invoke(window, new object[] { -1 });
                scenes = Scenes(window);
                Assert.That(GetString(scenes[0], "sceneId"), Is.EqualTo(duplicateId));
                Assert.That(GetString(scenes[1], "sceneId"), Is.EqualTo(originalId));

                delete.Invoke(window, null);
                scenes = Scenes(window);
                Assert.That(scenes.Count, Is.EqualTo(1));
                Assert.That(GetString(scenes[0], "sceneId"), Is.EqualTo(originalId));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ExistingMediaSelectionUpdatesSelectedSceneWithoutGuidTyping()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                RequireInstance(windowType, "ComposerAddScene").Invoke(window, null);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(KnownBackgroundPath);
                Assert.That(texture, Is.Not.Null);
                Assert.That(AssetDatabase.AssetPathToGUID(KnownBackgroundPath), Is.EqualTo(KnownBackgroundGuid));

                RequireInstance(windowType, "ComposerSetExistingRokasAsset", typeof(UnityEngine.Object))
                    .Invoke(window, new object[] { texture });

                object media = GetField(Scenes(window)[0], "media");
                Assert.That(GetField(media, "kind").ToString(), Is.EqualTo("ExistingRokasAsset"));
                Assert.That(GetString(media, "reference"), Is.EqualTo(KnownBackgroundGuid));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void SelectedSceneDrivesExistingRendererPreviewFrame()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                RequireInstance(windowType, "ComposerAddScene").Invoke(window, null);
                object scene = Scenes(window)[0];
                SetProperty(scene, "speaker", "Mina");
                SetProperty(scene, "previewText", "Composer selected scene preview");

                object frame = RequireInstance(windowType, "ComposerBuildSelectedPreviewFrame").Invoke(window, null);
                Assert.That(frame, Is.Not.Null);
                Assert.That(frame.GetType().Name, Is.EqualTo("VnWorkshopPreviewFrame"));
                Assert.That(GetProperty(frame, "Speaker"), Is.EqualTo("Mina"));
                Assert.That(GetProperty(frame, "Dialogue"), Is.EqualTo("Composer selected scene preview"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void TransportRoutesToRealSceneComposerPlaybackController()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            Type playbackType = RequireType("VnSceneComposerPlaybackController");
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                RequireInstance(windowType, "ComposerAddScene").Invoke(window, null);
                RequireInstance(windowType, "ComposerAddScene").Invoke(window, null);
                RequireInstance(windowType, "ComposerSelectScene", typeof(int)).Invoke(window, new object[] { 1 });

                RequireInstance(windowType, "ComposerPlayScene").Invoke(window, null);
                object playback = GetField(window, "_sceneComposerPlayback");
                Assert.That(playback, Is.Not.Null);
                Assert.That(playback.GetType(), Is.EqualTo(playbackType));
                Assert.That(GetProperty(playback, "CurrentSceneIndex"), Is.EqualTo(1));
                Assert.That(GetProperty(playback, "IsPlaying"), Is.True);

                RequireInstance(windowType, "ComposerPause").Invoke(window, null);
                Assert.That(GetProperty(playback, "IsPlaying"), Is.False);
                RequireInstance(windowType, "ComposerRestart").Invoke(window, null);
                Assert.That(GetProperty(playback, "CurrentSceneIndex"), Is.EqualTo(1));

                RequireInstance(windowType, "ComposerPlayAll").Invoke(window, null);
                Assert.That(GetProperty(playback, "CurrentSceneIndex"), Is.EqualTo(0));
                RequireInstance(windowType, "ComposerNext").Invoke(window, null);
                Assert.That(GetProperty(playback, "CurrentSceneIndex"), Is.EqualTo(1));
                RequireInstance(windowType, "ComposerPrevious").Invoke(window, null);
                Assert.That(GetProperty(playback, "CurrentSceneIndex"), Is.EqualTo(0));

                RequireInstance(windowType, "ComposerSelectScene", typeof(int)).Invoke(window, new object[] { 1 });
                RequireInstance(windowType, "ComposerPlayFromHere").Invoke(window, null);
                Assert.That(GetProperty(playback, "CurrentSceneIndex"), Is.EqualTo(1));
                Assert.That(GetProperty(playback, "IsPlaying"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void SaveAndLoadRouteThroughSceneComposerStorage()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = CreateWindow(windowType);
            string root = Path.Combine(Path.GetTempPath(), "rokas-sc-i-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                RequireInstance(windowType, "ComposerAddScene").Invoke(window, null);
                RequireInstance(windowType, "ComposerRenameSelectedScene", typeof(string)).Invoke(window, new object[] { "Stored Scene" });
                object project = GetField(window, "_sceneComposerProject");
                string projectId = GetString(project, "projectId");

                RequireInstance(windowType, "ComposerSaveProject", typeof(string)).Invoke(window, new object[] { root });
                string expected = Path.Combine(root, "Library", "ROKAS", "VnSceneComposer", "projects", projectId,
                    "ROKAS_VN_SCENE_COMPOSER_PROJECT.json");
                Assert.That(File.Exists(expected), Is.True);
                Assert.That(Directory.Exists(Path.Combine(root, "Assets")), Is.False);

                RequireInstance(windowType, "ComposerDeleteSelectedScene").Invoke(window, null);
                Assert.That(Scenes(window).Count, Is.EqualTo(0));

                object result = RequireInstance(windowType, "ComposerLoadProject", typeof(string), typeof(string))
                    .Invoke(window, new object[] { root, projectId });
                Assert.That(GetProperty(result, "Success"), Is.True, GetProperty(result, "Error") as string);
                Assert.That(GetString(GetField(window, "_sceneComposerProject"), "projectId"), Is.EqualTo(projectId));
                Assert.That(Scenes(window).Count, Is.EqualTo(1));
                Assert.That(GetString(Scenes(window)[0], "label"), Is.EqualTo("Stored Scene"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void SelectedBeatRemainsInCurrentSceneAcrossSceneAndBeatChanges()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                MethodInfo addScene = RequireInstance(windowType, "ComposerAddScene");
                MethodInfo selectScene = RequireInstance(windowType, "ComposerSelectScene", typeof(int));
                MethodInfo selectedId = RequireInstance(windowType, "ComposerGetSelectedDialogueBeatId");
                addScene.Invoke(window, null);
                VnSceneComposerScene first = (VnSceneComposerScene)Scenes(window)[0];
                RequireInstance(windowType, "ComposerAddDialogueBeat").Invoke(window, null);
                string laterFirstSceneBeatId = (string)selectedId.Invoke(window, null);
                addScene.Invoke(window, null);
                VnSceneComposerScene second = (VnSceneComposerScene)Scenes(window)[1];

                selectScene.Invoke(window, new object[] { 0 });
                RequireInstance(windowType, "ComposerSelectDialogueBeat", typeof(string))
                    .Invoke(window, new object[] { laterFirstSceneBeatId });
                selectScene.Invoke(window, new object[] { 1 });
                Assert.That((string)selectedId.Invoke(window, null), Is.EqualTo(second.dialogueBeats[0].beatId));

                // Simulate an editor reload/Undo restoring a Beat ID from another Scene.
                SetField(window, "_sceneComposerSelectedDialogueBeatId", laterFirstSceneBeatId);
                RequireInstance(windowType, "ComposerBuildSelectedPreviewFrame").Invoke(window, null);
                Assert.That((string)selectedId.Invoke(window, null), Is.EqualTo(second.dialogueBeats[0].beatId));
                Assert.That(first.dialogueBeats.Any(beat => beat.beatId == laterFirstSceneBeatId), Is.True);

                RequireInstance(windowType, "ComposerPreviewSelectedReplicaEffect").Invoke(window, null);
                Assert.That((string)selectedId.Invoke(window, null), Is.EqualTo(second.dialogueBeats[0].beatId));

                RequireInstance(windowType, "ComposerDuplicateSelectedScene").Invoke(window, null);
                VnSceneComposerScene duplicate = (VnSceneComposerScene)Scenes(window)[2];
                Assert.That(duplicate.dialogueBeats.Any(beat =>
                    beat.beatId == (string)selectedId.Invoke(window, null)), Is.True);
                RequireInstance(windowType, "ComposerDeleteSelectedScene").Invoke(window, null);
                VnSceneComposerScene selected = (VnSceneComposerScene)Scenes(window)[1];
                Assert.That(selected.dialogueBeats.Any(beat =>
                    beat.beatId == (string)selectedId.Invoke(window, null)), Is.True);

                selectScene.Invoke(window, new object[] { 0 });
                RequireInstance(windowType, "ComposerSelectDialogueBeat", typeof(string))
                    .Invoke(window, new object[] { laterFirstSceneBeatId });
                RequireInstance(windowType, "ComposerDeleteSelectedDialogueBeat").Invoke(window, null);
                Assert.That(first.dialogueBeats.Any(beat =>
                    beat.beatId == (string)selectedId.Invoke(window, null)), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void LoadingProjectSelectsBeatFromLoadedScene()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = CreateWindow(windowType);
            string root = Path.Combine(Path.GetTempPath(), "rokas-sc-beat-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                RequireInstance(windowType, "ComposerAddScene").Invoke(window, null);
                VnSceneComposerScene savedScene = (VnSceneComposerScene)Scenes(window)[0];
                string savedBeatId = savedScene.dialogueBeats[0].beatId;
                string projectId = GetString(GetField(window, "_sceneComposerProject"), "projectId");
                RequireInstance(windowType, "ComposerSaveProject", typeof(string))
                    .Invoke(window, new object[] { root });

                RequireInstance(windowType, "ComposerAddScene").Invoke(window, null);
                string otherBeatId = ((VnSceneComposerScene)Scenes(window)[1]).dialogueBeats[0].beatId;
                SetField(window, "_sceneComposerSelectedDialogueBeatId", otherBeatId);
                object result = RequireInstance(windowType, "ComposerLoadProject", typeof(string), typeof(string))
                    .Invoke(window, new object[] { root, projectId });
                Assert.That(GetProperty(result, "Success"), Is.True);
                Assert.That((string)RequireInstance(windowType, "ComposerGetSelectedDialogueBeatId")
                    .Invoke(window, null), Is.EqualTo(savedBeatId));
                Assert.That(Scenes(window).Count, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void AuthoredCharacterPickerOnlyReturnsCatalogStatesAndAddCharacterUsesSelectedState()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                RequireInstance(windowType, "ComposerAddScene").Invoke(window, null);
                string[] stateIds = (string[])RequireInstance(windowType, "ComposerGetAuthoredStateIds", typeof(string))
                    .Invoke(window, new object[] { "Mina" });
                Assert.That(stateIds, Is.Not.Null.And.Not.Empty);
                foreach (string stateId in stateIds)
                {
                    Assert.That(VnCharacterVisualCatalog.TryResolve(stateId, out VnCharacterVisualState state), Is.True);
                    Assert.That(state.Character, Is.EqualTo("Mina").IgnoreCase);
                }

                RequireInstance(windowType, "ComposerAddCharacter", typeof(string), typeof(string))
                    .Invoke(window, new object[] { "Mina", stateIds[0] });
                IList characters = (IList)GetField(Scenes(window)[0], "characters");
                Assert.That(characters.Count, Is.EqualTo(1));
                Assert.That(GetString(characters[0], "characterId"), Is.EqualTo("Mina"));
                Assert.That(GetString(characters[0], "stateId"), Is.EqualTo(stateIds[0]));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void SceneThumbnailUsesCachedRepresentativeTextureForExistingAsset()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                RequireInstance(windowType, "ComposerAddScene").Invoke(window, null);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(KnownBackgroundPath);
                RequireInstance(windowType, "ComposerSetExistingRokasAsset", typeof(UnityEngine.Object))
                    .Invoke(window, new object[] { texture });
                MethodInfo thumbnail = RequireInstance(windowType, "ComposerGetSceneThumbnail", typeof(int));
                object first = thumbnail.Invoke(window, new object[] { 0 });
                object second = thumbnail.Invoke(window, new object[] { 0 });
                Assert.That(first, Is.SameAs(second));
                Assert.That(first, Is.SameAs(texture));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void DeleteReorderAndMajorEditAreRecoverableWithUnityUndo()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                MethodInfo add = RequireInstance(windowType, "ComposerAddScene");
                MethodInfo select = RequireInstance(windowType, "ComposerSelectScene", typeof(int));
                MethodInfo rename = RequireInstance(windowType, "ComposerRenameSelectedScene", typeof(string));
                MethodInfo move = RequireInstance(windowType, "ComposerMoveSelectedScene", typeof(int));
                MethodInfo delete = RequireInstance(windowType, "ComposerDeleteSelectedScene");
                add.Invoke(window, null);
                add.Invoke(window, null);
                add.Invoke(window, null);
                Undo.ClearAll();

                select.Invoke(window, new object[] { 1 });
                string beforeRename = GetString(Scenes(window)[1], "label");
                rename.Invoke(window, new object[] { "Renamed" });
                Undo.FlushUndoRecordObjects();
                Assert.That(GetString(Scenes(window)[1], "label"), Is.EqualTo("Renamed"));
                Undo.PerformUndo();
                Assert.That(GetString(Scenes(window)[1], "label"), Is.EqualTo(beforeRename));

                string[] orderBefore = SceneIds(window);
                select.Invoke(window, new object[] { 1 });
                move.Invoke(window, new object[] { -1 });
                Undo.FlushUndoRecordObjects();
                Assert.That(SceneIds(window), Is.Not.EqualTo(orderBefore));
                Undo.PerformUndo();
                Assert.That(SceneIds(window), Is.EqualTo(orderBefore));

                select.Invoke(window, new object[] { 1 });
                delete.Invoke(window, null);
                Undo.FlushUndoRecordObjects();
                Assert.That(Scenes(window).Count, Is.EqualTo(2));
                Undo.PerformUndo();
                Assert.That(Scenes(window).Count, Is.EqualTo(3));
            }
            finally
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MD_DialogueBeatSelectionCrudAndPreviewFollowStableIdentity()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                RequireInstance(windowType, "ComposerAddScene").Invoke(window, null);
                object scene = Scenes(window)[0];
                IList beats = (IList)GetField(scene, "dialogueBeats");
                Assert.That(beats, Has.Count.EqualTo(1));
                string firstId = GetString(beats[0], "beatId");
                SetField(beats[0], "speaker", "Aiko");
                SetField(beats[0], "text", "First beat");

                MethodInfo selectedId = RequireInstance(windowType, "ComposerGetSelectedDialogueBeatId");
                MethodInfo select = RequireInstance(windowType, "ComposerSelectDialogueBeat", typeof(string));
                MethodInfo add = RequireInstance(windowType, "ComposerAddDialogueBeat");
                MethodInfo duplicate = RequireInstance(windowType, "ComposerDuplicateSelectedDialogueBeat");
                MethodInfo delete = RequireInstance(windowType, "ComposerDeleteSelectedDialogueBeat");
                MethodInfo move = RequireInstance(windowType, "ComposerMoveSelectedDialogueBeat", typeof(int));

                Assert.That((string)selectedId.Invoke(window, null), Is.EqualTo(firstId));

                add.Invoke(window, null);
                beats = (IList)GetField(scene, "dialogueBeats");
                Assert.That(beats, Has.Count.EqualTo(2));
                string secondId = (string)selectedId.Invoke(window, null);
                Assert.That(secondId, Is.Not.EqualTo(firstId));
                object second = beats.Cast<object>().Single(beat => GetString(beat, "beatId") == secondId);
                SetField(second, "speaker", "Tim");
                SetField(second, "text", "Second beat");

                object frame = RequireInstance(windowType, "ComposerBuildSelectedPreviewFrame").Invoke(window, null);
                Assert.That(GetProperty(frame, "Speaker"), Is.EqualTo("Tim"));
                Assert.That(GetProperty(frame, "Dialogue"), Is.EqualTo("Second beat"),
                    "Authoring preview must resolve the selected canonical dialogue beat.");

                select.Invoke(window, new object[] { firstId });
                Assert.That((string)selectedId.Invoke(window, null), Is.EqualTo(firstId));
                duplicate.Invoke(window, null);
                beats = (IList)GetField(scene, "dialogueBeats");
                Assert.That(beats, Has.Count.EqualTo(3));
                string copyId = (string)selectedId.Invoke(window, null);
                Assert.That(copyId, Is.Not.EqualTo(firstId));
                object copy = beats.Cast<object>().Single(beat => GetString(beat, "beatId") == copyId);
                Assert.That(GetString(copy, "speaker"), Is.EqualTo("Aiko"));
                Assert.That(GetString(copy, "text"), Is.EqualTo("First beat"));
                SetField(copy, "text", "Independent copy");
                Assert.That(GetString(beats.Cast<object>().Single(beat => GetString(beat, "beatId") == firstId), "text"),
                    Is.EqualTo("First beat"));

                string[] beforeMove = BeatIds(scene);
                move.Invoke(window, new object[] { 1 });
                Assert.That((string)selectedId.Invoke(window, null), Is.EqualTo(copyId),
                    "Beat reorder must preserve selection by stable identity.");
                Assert.That(BeatIds(scene), Is.Not.EqualTo(beforeMove));

                delete.Invoke(window, null);
                Assert.That(((IList)GetField(scene, "dialogueBeats")).Count, Is.EqualTo(2));
                string afterDelete = (string)selectedId.Invoke(window, null);
                Assert.That(BeatIds(scene), Does.Contain(afterDelete),
                    "Deleting the selected Beat must choose a deterministic existing neighbor.");

                object project = GetField(window, "_sceneComposerProject");
                string portable = (string)RequireType("VnSceneComposerSerialization")
                    .GetMethod("SerializePortable", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new[] { project });
                Assert.That(portable, Does.Not.Contain("_sceneComposerSelectedDialogueBeatId"));
                Assert.That(portable, Does.Not.Contain("selectedDialogueBeatId"),
                    "Authoring Beat selection is transient editor state, not portable story data.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MD_DialogueBeatMutationsAreUndoableAndSelectionRecoversByStableIdentity()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                RequireInstance(windowType, "ComposerAddScene").Invoke(window, null);
                object scene = Scenes(window)[0];
                MethodInfo selectedId = RequireInstance(windowType, "ComposerGetSelectedDialogueBeatId");
                MethodInfo add = RequireInstance(windowType, "ComposerAddDialogueBeat");
                MethodInfo move = RequireInstance(windowType, "ComposerMoveSelectedDialogueBeat", typeof(int));
                MethodInfo delete = RequireInstance(windowType, "ComposerDeleteSelectedDialogueBeat");

                Undo.ClearAll();
                string initialId = (string)selectedId.Invoke(window, null);
                add.Invoke(window, null);
                Undo.FlushUndoRecordObjects();
                Assert.That(((IList)GetField(scene, "dialogueBeats")).Count, Is.EqualTo(2));
                string addedId = (string)selectedId.Invoke(window, null);
                Assert.That(addedId, Is.Not.EqualTo(initialId));
                Undo.PerformUndo();
                scene = Scenes(window)[0];
                Assert.That(((IList)GetField(scene, "dialogueBeats")).Count, Is.EqualTo(1));
                Assert.That(BeatIds(scene), Does.Contain((string)selectedId.Invoke(window, null)));

                add.Invoke(window, null);
                scene = Scenes(window)[0];
                string movingId = (string)selectedId.Invoke(window, null);
                string[] beforeMove = BeatIds(scene);
                Undo.ClearAll();
                move.Invoke(window, new object[] { -1 });
                Undo.FlushUndoRecordObjects();
                Assert.That(BeatIds(scene), Is.Not.EqualTo(beforeMove));
                Undo.PerformUndo();
                scene = Scenes(window)[0];
                Assert.That(BeatIds(scene), Is.EqualTo(beforeMove));
                Assert.That(BeatIds(scene), Does.Contain((string)selectedId.Invoke(window, null)));

                Undo.ClearAll();
                delete.Invoke(window, null);
                Undo.FlushUndoRecordObjects();
                Assert.That(((IList)GetField(Scenes(window)[0], "dialogueBeats")).Count, Is.EqualTo(1));
                Undo.PerformUndo();
                scene = Scenes(window)[0];
                Assert.That(((IList)GetField(scene, "dialogueBeats")).Count, Is.EqualTo(2));
                Assert.That(BeatIds(scene), Does.Contain((string)selectedId.Invoke(window, null)));
                Assert.That(BeatIds(scene), Does.Contain(movingId));
            }
            finally
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MD_DialogueBeatSelectionDoesNotReopenPrepareRestartOrDisposeAuthoringVideo()
        {
            var factory = new WindowBeatVideoFactory();
            VnSceneComposerMediaEditing.VideoPreviewFactory = factory;
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                RequireInstance(windowType, "ComposerAddScene").Invoke(window, null);
                var scene = (VnSceneComposerScene)Scenes(window)[0];
                scene.media = new VnSceneComposerMediaReference
                {
                    kind = VnSceneComposerMediaKind.ExternalVideo,
                    reference = "md-authoring-selection.mp4",
                    displayName = "md-authoring-selection.mp4",
                    contentHash = "md-authoring-selection",
                    scaleMode = VnSceneComposerMediaScaleMode.Fit,
                    loop = false
                };
                VnSceneComposerDialogueBeat first = scene.dialogueBeats[0];
                var second = new VnSceneComposerDialogueBeat { speaker = "Tim", text = "Second" };
                scene.dialogueBeats.Add(second);

                Assert.That((bool)RequireInstance(windowType, "ComposerPrepareSelectedVideoForAuthoring").Invoke(window, null), Is.True);
                Assert.That(factory.Created, Has.Count.EqualTo(1));
                WindowBeatVideoPreview preview = factory.Created[0];
                Texture before = RequireInstance(windowType, "ComposerGetSceneThumbnail", typeof(int))
                    .Invoke(window, new object[] { 0 }) as Texture;
                int prepareBefore = preview.PrepareCalls;
                int restartBefore = preview.RestartCalls;
                int disposeBefore = preview.DisposeCalls;

                RequireInstance(windowType, "ComposerSelectDialogueBeat", typeof(string))
                    .Invoke(window, new object[] { second.beatId });

                Texture after = RequireInstance(windowType, "ComposerGetSceneThumbnail", typeof(int))
                    .Invoke(window, new object[] { 0 }) as Texture;
                Assert.That(factory.Created, Has.Count.EqualTo(1),
                    "Beat selection must not reopen the selected Scene video.");
                Assert.That(after, Is.SameAs(before),
                    "Beat selection must retain the same authoring video texture/resource identity.");
                Assert.That(preview.PrepareCalls, Is.EqualTo(prepareBefore));
                Assert.That(preview.RestartCalls, Is.EqualTo(restartBefore));
                Assert.That(preview.DisposeCalls, Is.EqualTo(disposeBefore));
                Assert.That((string)RequireInstance(windowType, "ComposerGetSelectedDialogueBeatId").Invoke(window, null),
                    Is.EqualTo(second.beatId));
                Assert.That(first.beatId, Is.Not.EqualTo(second.beatId));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                VnSceneComposerMediaEditing.ResetVideoPreviewFactory();
                for (int i = 0; i < factory.Created.Count; i++)
                {
                    WindowBeatVideoPreview preview = factory.Created[i];
                    if (preview != null && preview.texture != null) preview.Dispose();
                }
            }
        }

        [Test]
        public void ComposerEditorSurfaceHasNoProductionApplyOrYarnWritePath()
        {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == EditorAssembly);
            string[] forbiddenMethodNames = assembly.GetTypes()
                .Where(type => type.Namespace != null && type.Namespace.StartsWith(Namespace.TrimEnd('.'), StringComparison.Ordinal))
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                .Select(method => method.Name)
                .Where(name => name.IndexOf("ApplyToProduction", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               name.IndexOf("GenerateYarn", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               name.IndexOf("WriteYarn", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();
            Assert.That(forbiddenMethodNames, Is.Empty);
        }

        private sealed class WindowBeatVideoPreview : VnSceneComposerVideoPreview
        {
            public int PrepareCalls;
            public int RestartCalls;
            public int DisposeCalls;
            private bool prepared;

            public WindowBeatVideoPreview(int serial)
                : base(null, 16, 16, false)
            {
                warning = string.Empty;
                texture = new RenderTexture(32, 18, 0) { name = "ROKAS_MD_WindowVideo_" + serial };
                texture.Create();
            }

            public override bool IsPrepared { get { return prepared; } }
            public override bool IsPreparing { get { return false; } }
            public override bool HasVisibleFrame { get { return prepared; } }

            public override void Prepare()
            {
                PrepareCalls++;
                prepared = true;
            }

            public override void Restart()
            {
                RestartCalls++;
            }

            public override void Dispose()
            {
                DisposeCalls++;
                base.Dispose();
            }
        }

        private sealed class WindowBeatVideoFactory : IVnSceneComposerVideoPreviewFactory
        {
            public readonly System.Collections.Generic.List<WindowBeatVideoPreview> Created =
                new System.Collections.Generic.List<WindowBeatVideoPreview>();

            public VnSceneComposerVideoPreview Open(VnSceneComposerMediaReference media, int width, int height)
            {
                var preview = new WindowBeatVideoPreview(Created.Count);
                Created.Add(preview);
                return preview;
            }
        }

        private static UnityEngine.Object CreateWindow(Type windowType)
        {
            UnityEngine.Object window = ScriptableObject.CreateInstance(windowType);
            Assert.That(window, Is.Not.Null);
            return window;
        }

        private static IList Scenes(object window)
        {
            object project = GetField(window, "_sceneComposerProject");
            Assert.That(project, Is.Not.Null);
            IList scenes = (IList)GetField(project, "scenes");
            Assert.That(scenes, Is.Not.Null);
            return scenes;
        }

        private static string[] BeatIds(object scene)
        {
            IList beats = (IList)GetField(scene, "dialogueBeats");
            return beats.Cast<object>().Select(beat => GetString(beat, "beatId")).ToArray();
        }

        private static string[] SceneIds(object window)
        {
            return Scenes(window).Cast<object>().Select(scene => GetString(scene, "sceneId")).ToArray();
        }

        private static Type RequireType(string shortName)
        {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == EditorAssembly);
            Assert.That(assembly, Is.Not.Null);
            Type type = assembly.GetType(Namespace + shortName, false);
            Assert.That(type, Is.Not.Null, "Missing Scene Composer/Workshop type: " + shortName);
            return type;
        }

        private static MethodInfo RequireInstance(Type type, string name, params Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, parameters ?? Type.EmptyTypes, null);
            Assert.That(method, Is.Not.Null, "Missing Scene Composer window method: " + type.Name + "." + name);
            return method;
        }

        private static object GetField(object instance, string name)
        {
            Assert.That(instance, Is.Not.Null);
            FieldInfo field = instance.GetType().GetField(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing field: " + instance.GetType().Name + "." + name);
            return field.GetValue(instance);
        }

        private static void SetField(object instance, string name, object value)
        {
            Assert.That(instance, Is.Not.Null);
            FieldInfo field = instance.GetType().GetField(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing field: " + instance.GetType().Name + "." + name);
            field.SetValue(instance, value);
        }

        private static void SetProperty(object instance, string name, object value)
        {
            Assert.That(instance, Is.Not.Null);
            PropertyInfo property = instance.GetType().GetProperty(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, "Missing property: " + instance.GetType().Name + "." + name);
            property.SetValue(instance, value, null);
        }

        private static object GetProperty(object instance, string name)
        {
            Assert.That(instance, Is.Not.Null);
            PropertyInfo property = instance.GetType().GetProperty(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, "Missing property: " + instance.GetType().Name + "." + name);
            return property.GetValue(instance, null);
        }

        private static string GetString(object instance, string name)
        {
            return (string)GetField(instance, name);
        }
    }
}
