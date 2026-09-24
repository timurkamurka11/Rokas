using System;
using System.IO;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed partial class VnPresentationWorkshopWindow
    {
        public string ComposerImportSelectedDialogueBeatPosePng(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                throw new ArgumentException(
                    "Pose PNG file does not exist.", nameof(sourcePath));
            if (!string.Equals(Path.GetExtension(sourcePath), ".png",
                    StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    "Pose / emotion image must use PNG.", nameof(sourcePath));

            VnSceneComposerScene scene = RequireSelectedScene();
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            if (beat == null)
                throw new InvalidOperationException("No dialogue Beat is selected.");

            string characterId = GetSelectedStagingCharacterId(
                scene, beat, GetSceneComposerBeatTargetCharacterIds(scene));
            characterId = ResolveSceneCharacterId(scene, characterId);
            if (string.IsNullOrEmpty(characterId))
                throw new InvalidOperationException(
                    "Choose the visual character for this Beat before importing a pose.");

            string stateName = Path.GetFileNameWithoutExtension(sourcePath);
            VnSceneComposerAssetOnboardResult result =
                VnSceneComposerAssetLibrary.Onboard(
                    GetProjectRoot(), sourcePath,
                    VnSceneComposerAssetPurpose.CharacterState,
                    stateName, characterId, stateName);
            if (!result.Success || result.Entry == null ||
                string.IsNullOrWhiteSpace(result.Entry.stateId))
                throw new InvalidOperationException(
                    result.Error ?? "Could not import pose PNG.");

            VnSceneComposerBeatCharacterStaging staging =
                FindCharacterStagingByCharacter(beat, characterId);
            ComposerSetSelectedDialogueBeatCharacterStaging(
                characterId,
                staging != null
                    ? staging.visibility
                    : VnSceneComposerBeatCharacterVisibility.KeepPrevious,
                staging != null
                    ? staging.position
                    : VnSceneComposerBeatCharacterPosition.KeepPrevious,
                staging != null
                    ? staging.customPositionOffset
                    : UnityEngine.Vector2.zero,
                true,
                result.Entry.stateId,
                staging != null
                    ? staging.effect
                    : VnSceneComposerBeatEffect.None,
                staging != null ? staging.effectStrength : 18f,
                staging != null ? staging.effectDuration : .28f,
                staging != null ? staging.delaySeconds : 0f);
            return result.Entry.stateId;
        }
    }
}
