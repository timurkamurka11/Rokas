using System;
using UnityEditor;
using UnityEngine;

namespace Rokas.Editor
{
    // The library builder copies clips into standalone .anim assets. Only the three idle
    // FBXs are referenced as runtime models; the other source meshes stay out of builds.
    public sealed class ReactiveCombatModelImporter : AssetPostprocessor
    {
        private const string Prefix = "Assets/Rokas/Art/CombatActors/";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Prefix, StringComparison.Ordinal)) return;
            ModelImporter importer = (ModelImporter)assetImporter;
            bool baseModel =
                assetPath.EndsWith("/Keiko/Keiko@Idle.fbx", StringComparison.Ordinal) ||
                assetPath.EndsWith("/Mina/anime girl character 3d model@Standing Idle.fbx", StringComparison.Ordinal) ||
                assetPath.EndsWith("/Yokai/Still stance.fbx", StringComparison.Ordinal);

            // Skeletons match within each source character. Legacy binds directly to those bones
            // and avoids retargeting an unusual Yokai rig through a human Avatar.
            importer.animationType = ModelImporterAnimationType.Legacy;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = baseModel
                ? ModelImporterMaterialImportMode.ImportStandard
                : ModelImporterMaterialImportMode.None;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.isReadable = false;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.resampleCurves = true;

            ModelImporterClipAnimation[] animations = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in animations)
            {
                bool idle = assetPath.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    assetPath.EndsWith("/Yokai/Still stance.fbx", StringComparison.Ordinal);
                clip.loopTime = idle;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
            }
            if (animations.Length > 0) importer.clipAnimations = animations;
        }
    }
}
