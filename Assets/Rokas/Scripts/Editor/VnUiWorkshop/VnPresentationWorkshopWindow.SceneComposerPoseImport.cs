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

            string characterId = ResolveSceneCharacterId(
                scene, beat.targetCharacterId);
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

            ComposerSetSelectedDialogueBeatCharacterState(
                characterId, true, result.Entry.stateId);
            return result.Entry.stateId;
        }
    }
}
