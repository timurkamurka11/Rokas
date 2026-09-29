using System;
using System.Collections.Generic;

namespace Rokas.EditorTools.VnUiWorkshop
{
    internal static class VnSceneComposerDialogue
    {
        internal static VnSceneComposerDialogueBeat AddBeat(VnSceneComposerScene scene, string afterBeatId)
        {
            RequireScene(scene);
            if (scene.dialogueBeats == null) scene.dialogueBeats = new List<VnSceneComposerDialogueBeat>();

            var beat = new VnSceneComposerDialogueBeat();
            int sourceIndex = FindIndex(scene, afterBeatId);
            if (sourceIndex >= 0) scene.dialogueBeats.Insert(sourceIndex + 1, beat);
            else scene.dialogueBeats.Add(beat);
            return beat;
        }

        internal static VnSceneComposerDialogueBeat DuplicateBeat(VnSceneComposerScene scene, string beatId)
        {
            RequireScene(scene);
            int sourceIndex = FindIndex(scene, beatId);
            if (sourceIndex < 0) return null;

            VnSceneComposerDialogueBeat source = scene.dialogueBeats[sourceIndex];
            if (source == null) return null;

            var copy = new VnSceneComposerDialogueBeat
            {
                speaker = source.speaker ?? string.Empty,
                text = source.text ?? string.Empty,
                narration = source.narration,
                targetCharacterId = source.targetCharacterId ?? string.Empty,
                hasStateOverride = source.hasStateOverride,
                stateId = source.stateId ?? string.Empty,
                effect = source.effect,
                effectStrength = source.effectStrength,
                effectDuration = source.effectDuration,
                replicaEffect = source.replicaEffect != null
                    ? UnityEngine.JsonUtility.FromJson<VnSceneComposerReplicaEffect>(
                        UnityEngine.JsonUtility.ToJson(source.replicaEffect))
                    : new VnSceneComposerReplicaEffect(),
                movement = source.movement != null
                    ? UnityEngine.JsonUtility.FromJson<VnSceneComposerBeatMovement>(
                        UnityEngine.JsonUtility.ToJson(source.movement))
                    : new VnSceneComposerBeatMovement()
            };
            if (source.characterStaging != null)
            {
                for (int i = 0; i < source.characterStaging.Count; i++)
                {
                    VnSceneComposerBeatCharacterStaging staging = source.characterStaging[i];
                    if (staging == null) continue;
                    VnSceneComposerBeatCharacterStaging stagingCopy =
                        UnityEngine.JsonUtility.FromJson<VnSceneComposerBeatCharacterStaging>(
                            UnityEngine.JsonUtility.ToJson(staging));
                    if (stagingCopy == null) continue;
                    stagingCopy.stagingId = VnSceneComposerScene.NewStableId();
                    copy.characterStaging.Add(stagingCopy);
                }
            }
            scene.dialogueBeats.Insert(sourceIndex + 1, copy);
            return copy;
        }

        internal static string DeleteBeat(VnSceneComposerScene scene, string beatId)
        {
            RequireScene(scene);
            int sourceIndex = FindIndex(scene, beatId);
            if (sourceIndex < 0) return string.Empty;

            scene.dialogueBeats.RemoveAt(sourceIndex);
            if (scene.dialogueBeats.Count == 0)
            {
                var replacement = new VnSceneComposerDialogueBeat();
                scene.dialogueBeats.Add(replacement);
                return replacement.beatId;
            }

            int selectionIndex = sourceIndex < scene.dialogueBeats.Count
                ? sourceIndex
                : scene.dialogueBeats.Count - 1;
            VnSceneComposerDialogueBeat selected = scene.dialogueBeats[selectionIndex];
            return selected != null ? selected.beatId ?? string.Empty : string.Empty;
        }

        internal static bool MoveBeat(VnSceneComposerScene scene, string beatId, int targetIndex)
        {
            RequireScene(scene);
            if (scene.dialogueBeats == null || targetIndex < 0 || targetIndex >= scene.dialogueBeats.Count)
                return false;

            int sourceIndex = FindIndex(scene, beatId);
            if (sourceIndex < 0) return false;
            if (sourceIndex == targetIndex) return true;

            VnSceneComposerDialogueBeat beat = scene.dialogueBeats[sourceIndex];
            scene.dialogueBeats.RemoveAt(sourceIndex);
            scene.dialogueBeats.Insert(targetIndex, beat);
            return true;
        }

        internal static int FindIndex(VnSceneComposerScene scene, string beatId)
        {
            if (scene == null || scene.dialogueBeats == null || string.IsNullOrEmpty(beatId)) return -1;
            for (int i = 0; i < scene.dialogueBeats.Count; i++)
            {
                VnSceneComposerDialogueBeat beat = scene.dialogueBeats[i];
                if (beat != null && string.Equals(beat.beatId, beatId, StringComparison.Ordinal)) return i;
            }
            return -1;
        }

        private static void RequireScene(VnSceneComposerScene scene)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
        }
    }
}
