using System;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public static class VnSceneComposerEditing
    {
        public static VnSceneComposerScene AddScene(VnSceneComposerProject project, string label)
        {
            RequireProject(project);
            VnSceneComposerScene scene = new VnSceneComposerScene();
            scene.label = label ?? string.Empty;
            project.scenes.Add(scene);
            return scene;
        }

        public static bool RenameScene(VnSceneComposerProject project, string sceneId, string label)
        {
            int index = FindSceneIndex(project, sceneId);
            if (index < 0) return false;
            project.scenes[index].label = label ?? string.Empty;
            return true;
        }

        public static bool DeleteScene(VnSceneComposerProject project, string sceneId)
        {
            int index = FindSceneIndex(project, sceneId);
            if (index < 0) return false;
            project.scenes.RemoveAt(index);
            return true;
        }

        public static bool MoveScene(VnSceneComposerProject project, string sceneId, int targetIndex)
        {
            int sourceIndex = FindSceneIndex(project, sceneId);
            if (sourceIndex < 0 || targetIndex < 0 || targetIndex >= project.scenes.Count) return false;
            if (sourceIndex == targetIndex) return true;

            VnSceneComposerScene scene = project.scenes[sourceIndex];
            project.scenes.RemoveAt(sourceIndex);
            project.scenes.Insert(targetIndex, scene);
            return true;
        }

        public static VnSceneComposerScene DuplicateScene(VnSceneComposerProject project, string sceneId)
        {
            int sourceIndex = FindSceneIndex(project, sceneId);
            if (sourceIndex < 0) return null;

            VnSceneComposerScene source = project.scenes[sourceIndex];
            VnSceneComposerScene copy = JsonUtility.FromJson<VnSceneComposerScene>(JsonUtility.ToJson(source));
            if (copy == null) throw new InvalidOperationException("Could not duplicate Scene Composer scene.");

            copy.sceneId = VnSceneComposerScene.NewStableId();
            if (copy.media == null) copy.media = new VnSceneComposerMediaReference();
            if (copy.characters == null) copy.characters = new System.Collections.Generic.List<VnSceneComposerCharacter>();
            if (copy.dialogueBeats == null) copy.dialogueBeats = new System.Collections.Generic.List<VnSceneComposerDialogueBeat>();
            if (copy.dialogueBeats.Count == 0) copy.dialogueBeats.Add(new VnSceneComposerDialogueBeat());
            for (int i = 0; i < copy.dialogueBeats.Count; i++)
            {
                if (copy.dialogueBeats[i] == null) copy.dialogueBeats[i] = new VnSceneComposerDialogueBeat();
                copy.dialogueBeats[i].beatId = VnSceneComposerScene.NewStableId();
            }
            if (copy.presentationOverrides == null) copy.presentationOverrides = new VnPresentationWorkshopPreset();
            if (copy.transition == null) copy.transition = new VnSceneComposerTransition();
            if (copy.timing == null) copy.timing = new VnSceneComposerTiming();

            project.scenes.Insert(sourceIndex + 1, copy);
            return copy;
        }

        private static int FindSceneIndex(VnSceneComposerProject project, string sceneId)
        {
            RequireProject(project);
            if (string.IsNullOrEmpty(sceneId)) return -1;

            for (int i = 0; i < project.scenes.Count; i++)
            {
                VnSceneComposerScene scene = project.scenes[i];
                if (scene != null && string.Equals(scene.sceneId, sceneId, StringComparison.Ordinal)) return i;
            }
            return -1;
        }

        private static void RequireProject(VnSceneComposerProject project)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (project.scenes == null) throw new ArgumentException("Scene Composer project scenes list is missing.", nameof(project));
        }
    }
}
