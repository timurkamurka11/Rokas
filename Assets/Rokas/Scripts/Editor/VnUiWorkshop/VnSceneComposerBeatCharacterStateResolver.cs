using System;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public static class VnSceneComposerBeatCharacterStateResolver
    {
        public static string ResolveStateId(
            VnSceneComposerScene scene,
            VnSceneComposerDialogueBeat beat,
            string characterId)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            if (beat == null) throw new ArgumentNullException(nameof(beat));
            if (string.IsNullOrWhiteSpace(characterId))
                throw new ArgumentException("Character identity is required.", nameof(characterId));

            VnSceneComposerCharacter authored = FindAuthoredCharacter(scene, characterId);
            if (authored == null)
                throw new ArgumentException(
                    "Character '" + characterId + "' is not visible in the current Scene.", nameof(characterId));

            string effective = authored.stateId ?? string.Empty;
            ValidateStateOwnership(effective, characterId, "Scene base");

            int beatIndex = FindBeatIndex(scene, beat);
            if (beatIndex < 0)
                throw new ArgumentException("Dialogue Beat is not part of the current Scene.", nameof(beat));

            for (int i = 0; i <= beatIndex; i++)
            {
                VnSceneComposerDialogueBeat current = scene.dialogueBeats[i];
                if (current == null ||
                    !string.Equals(current.targetCharacterId ?? string.Empty, characterId,
                        StringComparison.OrdinalIgnoreCase) ||
                    !current.hasStateOverride)
                    continue;

                ValidateStateOwnership(current.stateId, characterId, "Dialogue Beat");
                effective = current.stateId ?? string.Empty;
            }

            return effective;
        }

        public static string ResolveCharacterId(VnSceneComposerCharacter character)
        {
            if (character == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(character.characterId))
                return character.characterId;
            if (!string.IsNullOrWhiteSpace(character.stateId) &&
                VnSceneComposerCharacterStateResolver.TryResolve(
                    character.stateId, out VnSceneComposerResolvedCharacterState state))
                return state.Character ?? string.Empty;
            return string.Empty;
        }

        private static VnSceneComposerCharacter FindAuthoredCharacter(
            VnSceneComposerScene scene, string characterId)
        {
            if (scene.characters == null) return null;
            for (int i = 0; i < scene.characters.Count; i++)
            {
                VnSceneComposerCharacter candidate = scene.characters[i];
                if (candidate == null) continue;
                string candidateId = ResolveCharacterId(candidate);
                if (string.Equals(candidateId, characterId, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
            return null;
        }

        private static int FindBeatIndex(VnSceneComposerScene scene, VnSceneComposerDialogueBeat beat)
        {
            if (scene.dialogueBeats == null) return -1;
            for (int i = 0; i < scene.dialogueBeats.Count; i++)
            {
                VnSceneComposerDialogueBeat candidate = scene.dialogueBeats[i];
                if (ReferenceEquals(candidate, beat)) return i;
                if (candidate != null && !string.IsNullOrEmpty(beat.beatId) &&
                    string.Equals(candidate.beatId, beat.beatId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private static void ValidateStateOwnership(
            string stateId, string characterId, string context)
        {
            if (!VnSceneComposerCharacterStateResolver.TryResolve(
                    stateId, out VnSceneComposerResolvedCharacterState state))
                throw new ArgumentException(
                    context + " state '" + (stateId ?? string.Empty) + "' cannot be resolved.");

            if (!string.Equals(state.Character, characterId, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    context + " state '" + stateId + "' belongs to " + state.Character +
                    ", not " + characterId + ".");
        }
    }
}
