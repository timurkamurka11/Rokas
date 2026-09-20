using System;
using System.Collections.Generic;
using System.IO;
using Rokas.Presentation;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed class VnSceneComposerImportResult
    {
        public bool Success { get; private set; }
        public bool SourceHeadMismatch { get; private set; }
        public string Error { get; private set; }
        public string[] Warnings { get; private set; }
        public string ImportedSourceHead { get; private set; }
        public string ExpectedSourceHead { get; private set; }
        public VnSceneComposerProject Project { get; private set; }

        internal static VnSceneComposerImportResult Failed(string error)
        {
            return new VnSceneComposerImportResult
            {
                Success = false,
                SourceHeadMismatch = false,
                Error = string.IsNullOrEmpty(error) ? "Unknown Scene Composer persistence error." : error,
                Warnings = Array.Empty<string>(),
                ImportedSourceHead = string.Empty,
                ExpectedSourceHead = VnSceneComposerContract.SourceHead,
                Project = null
            };
        }

        internal static VnSceneComposerImportResult Loaded(VnSceneComposerProject project, IList<string> warnings)
        {
            string importedHead = project != null ? project.sourceHead ?? string.Empty : string.Empty;
            bool mismatch = !string.Equals(importedHead, VnSceneComposerContract.SourceHead, StringComparison.Ordinal);
            var messages = new List<string>();
            if (warnings != null)
            {
                for (int i = 0; i < warnings.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(warnings[i]) && !messages.Contains(warnings[i]))
                        messages.Add(warnings[i]);
                }
            }
            if (mismatch)
            {
                string sourceWarning = "Scene Composer source metadata mismatch. Imported: " + importedHead +
                                       "; expected: " + VnSceneComposerContract.SourceHead + ".";
                if (!messages.Contains(sourceWarning)) messages.Insert(0, sourceWarning);
            }
            return new VnSceneComposerImportResult
            {
                Success = true,
                SourceHeadMismatch = mismatch,
                Error = string.Empty,
                Warnings = messages.ToArray(),
                ImportedSourceHead = importedHead,
                ExpectedSourceHead = VnSceneComposerContract.SourceHead,
                Project = project
            };
        }
    }

    public static class VnSceneComposerSerialization
    {
        public const int SchemaVersion = VnSceneComposerContract.SchemaVersion;
        private const int LegacySchemaVersion = 1;
        private const int BeatStateSchemaVersion = 2;
        private const string ExternalReferencePrefix = "external://";
        private const float MaxPreviewDuration = 3600f;
        private const float MaxSceneTransitionDuration = 10f;

        [Serializable]
        private sealed class LegacyDialogueProjectV1
        {
            public List<LegacyDialogueSceneV1> scenes = new List<LegacyDialogueSceneV1>();
        }

        [Serializable]
        private sealed class LegacyDialogueSceneV1
        {
            public string speaker = string.Empty;
            public string previewText = string.Empty;
            public bool narration;
        }

        public static string SerializePortable(VnSceneComposerProject project)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (!ValidateProject(project, out string error, out _))
                throw new ArgumentException(error, nameof(project));

            VnSceneComposerProject portable = CloneProject(project);
            NormalizeProject(portable);
            portable.schemaVersion = SchemaVersion;
            MakeExternalReferencesPortable(portable);
            return JsonUtility.ToJson(portable, true);
        }

        public static VnSceneComposerImportResult DeserializePortable(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return VnSceneComposerImportResult.Failed("Scene Composer project JSON is empty.");

            VnSceneComposerProject project;
            try
            {
                project = JsonUtility.FromJson<VnSceneComposerProject>(json);
            }
            catch (Exception exception)
            {
                return VnSceneComposerImportResult.Failed(
                    "Scene Composer project JSON could not be parsed: " + exception.Message);
            }

            if (json.IndexOf("\"schemaVersion\"", StringComparison.Ordinal) < 0)
                return VnSceneComposerImportResult.Failed("Scene Composer project JSON is missing schemaVersion metadata.");
            if (json.IndexOf("\"sourceHead\"", StringComparison.Ordinal) < 0)
                return VnSceneComposerImportResult.Failed("Scene Composer project JSON is missing sourceHead metadata.");
            if (json.IndexOf("\"projectId\"", StringComparison.Ordinal) < 0)
                return VnSceneComposerImportResult.Failed("Scene Composer project JSON is missing projectId metadata.");
            if (json.IndexOf("\"scenes\"", StringComparison.Ordinal) < 0)
                return VnSceneComposerImportResult.Failed("Scene Composer project JSON is missing ordered scene data.");

            if (project == null)
                return VnSceneComposerImportResult.Failed("Scene Composer project JSON did not contain a project.");

            if (project.schemaVersion == LegacySchemaVersion)
            {
                VnSceneComposerImportResult migrationFailure = MigrateLegacyDialogueV1(json, project);
                if (migrationFailure != null) return migrationFailure;
            }
            else if (project.schemaVersion == BeatStateSchemaVersion)
            {
                MigrateBeatStateSchemaV2(project);
            }
            else if (project.schemaVersion != SchemaVersion)
            {
                return VnSceneComposerImportResult.Failed(
                    "Unsupported Scene Composer schema version: " + project.schemaVersion +
                    ". Supported versions are " + LegacySchemaVersion + ", " +
                    BeatStateSchemaVersion + " and " + SchemaVersion + ".");
            }

            NormalizeProject(project);
            return RevalidateImported(project);
        }

        public static bool ValidateProject(VnSceneComposerProject project, out string error, out string[] warnings)
        {
            var diagnostics = new List<string>();
            if (project == null)
            {
                error = "Scene Composer project is missing.";
                warnings = diagnostics.ToArray();
                return false;
            }
            if (project.schemaVersion != SchemaVersion)
            {
                error = "Unsupported Scene Composer schema version: " + project.schemaVersion + ".";
                warnings = diagnostics.ToArray();
                return false;
            }
            if (!IsStableId(project.projectId))
            {
                error = "Scene Composer project ID must be a stable 32-character hexadecimal ID.";
                warnings = diagnostics.ToArray();
                return false;
            }
            if (string.IsNullOrWhiteSpace(project.sourceHead))
            {
                error = "Scene Composer sourceHead metadata is empty.";
                warnings = diagnostics.ToArray();
                return false;
            }
            if (project.defaultPresentation == null)
            {
                error = "Scene Composer project presentation defaults are missing.";
                warnings = diagnostics.ToArray();
                return false;
            }
            if (!ValidatePresentation(project.defaultPresentation, out error))
            {
                warnings = diagnostics.ToArray();
                return false;
            }
            AppendTypographyFontWarnings(project.defaultPresentation, "shared dialogue typography", diagnostics);
            if (project.scenes == null)
            {
                error = "Scene Composer ordered scene list is missing.";
                warnings = diagnostics.ToArray();
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < project.scenes.Count; i++)
            {
                VnSceneComposerScene scene = project.scenes[i];
                if (scene == null)
                {
                    error = "Scene Composer scene at index " + i + " is null.";
                    warnings = diagnostics.ToArray();
                    return false;
                }
                if (!IsStableId(scene.sceneId))
                {
                    error = "Scene Composer scene ID is missing or invalid at index " + i + ".";
                    warnings = diagnostics.ToArray();
                    return false;
                }
                if (!ids.Add(scene.sceneId))
                {
                    error = "Duplicate Scene Composer scene ID: " + scene.sceneId + ".";
                    warnings = diagnostics.ToArray();
                    return false;
                }
                if (!ValidateScene(project, scene, i, diagnostics, out error))
                {
                    warnings = diagnostics.ToArray();
                    return false;
                }
            }

            error = string.Empty;
            warnings = diagnostics.ToArray();
            return true;
        }

        internal static bool EnsureCurrentSchema(VnSceneComposerProject project)
        {
            if (project == null) return false;
            if (project.schemaVersion == SchemaVersion) return false;
            if (project.schemaVersion != BeatStateSchemaVersion) return false;

            MigrateBeatStateSchemaV2(project);
            NormalizeProject(project);
            return true;
        }

        internal static VnSceneComposerImportResult RevalidateImported(VnSceneComposerProject project)
        {
            if (!ValidateProject(project, out string error, out string[] warnings))
                return VnSceneComposerImportResult.Failed(error);
            return VnSceneComposerImportResult.Loaded(project, warnings);
        }

        internal static bool IsStableId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 32) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex) return false;
            }
            return true;
        }

        internal static bool IsExternalMedia(VnSceneComposerMediaKind kind)
        {
            return kind == VnSceneComposerMediaKind.ExternalImage ||
                   kind == VnSceneComposerMediaKind.ExternalVideo ||
                   kind == VnSceneComposerMediaKind.ExternalGif;
        }

        internal static bool IsPortableExternalReference(string reference)
        {
            return !string.IsNullOrEmpty(reference) &&
                   reference.StartsWith(ExternalReferencePrefix, StringComparison.Ordinal);
        }

        private static VnSceneComposerImportResult MigrateLegacyDialogueV1(string json, VnSceneComposerProject project)
        {
            LegacyDialogueProjectV1 legacy;
            try
            {
                legacy = JsonUtility.FromJson<LegacyDialogueProjectV1>(json);
            }
            catch (Exception exception)
            {
                return VnSceneComposerImportResult.Failed(
                    "Scene Composer legacy dialogue could not be parsed: " + exception.Message);
            }

            if (project.scenes == null) project.scenes = new List<VnSceneComposerScene>();
            for (int i = 0; i < project.scenes.Count; i++)
            {
                VnSceneComposerScene scene = project.scenes[i];
                if (scene == null) continue;
                LegacyDialogueSceneV1 source = legacy != null && legacy.scenes != null && i < legacy.scenes.Count
                    ? legacy.scenes[i]
                    : null;
                scene.dialogueBeats = new List<VnSceneComposerDialogueBeat>
                {
                    new VnSceneComposerDialogueBeat
                    {
                        speaker = source != null ? source.speaker ?? string.Empty : string.Empty,
                        text = source != null ? source.previewText ?? string.Empty : string.Empty,
                        narration = source != null && source.narration
                    }
                };
            }

            project.schemaVersion = SchemaVersion;
            return null;
        }

        private static bool ValidateScene(VnSceneComposerProject project, VnSceneComposerScene scene, int index,
            List<string> diagnostics, out string error)
        {
            if (!ValidateDialogueBeats(scene, diagnostics, out error)) return false;

            if (scene.media == null)
            {
                error = "Scene Composer media reference is missing for scene " + scene.sceneId + ".";
                return false;
            }
            if (!ValidateMedia(scene, diagnostics, out error)) return false;
            if (!ValidateMusic(scene, diagnostics, out error)) return false;
            if (!ValidateAdditionalAudio(scene, diagnostics, out error)) return false;

            if (scene.characters == null)
            {
                error = "Scene Composer character list is missing for scene " + scene.sceneId + ".";
                return false;
            }
            if (scene.characters.Count > 3)
            {
                error = "Scene Composer supports zero to three authored characters per scene.";
                return false;
            }
            for (int i = 0; i < scene.characters.Count; i++)
            {
                VnSceneComposerCharacter character = scene.characters[i];
                if (character == null)
                {
                    error = "Scene Composer character entry is null in scene " + scene.sceneId + ".";
                    return false;
                }
                if (!VnCharacterVisualCatalog.TryResolve(character.stateId, out VnCharacterVisualState state))
                {
                    error = "Missing authored character state '" + (character.stateId ?? string.Empty) +
                            "' in scene " + scene.sceneId + ".";
                    return false;
                }
                if (!string.IsNullOrEmpty(character.characterId) &&
                    !string.Equals(character.characterId, state.Character, StringComparison.OrdinalIgnoreCase))
                {
                    error = "Authored state '" + character.stateId + "' belongs to " + state.Character +
                            ", not " + character.characterId + ".";
                    return false;
                }
                if (!Enum.IsDefined(typeof(VnWorkshopStageSlot), character.stageSlot))
                {
                    error = "Invalid stage slot in scene " + scene.sceneId + ".";
                    return false;
                }
                if (character.hasPositionOffset && (!IsFinite(character.positionOffset.x) || !IsFinite(character.positionOffset.y)))
                {
                    error = "Character position offset must be finite in scene " + scene.sceneId + ".";
                    return false;
                }
                if (character.hasScaleMultiplier &&
                    (!IsFinite(character.scaleMultiplier) || character.scaleMultiplier <= 0f || character.scaleMultiplier > 10f))
                {
                    error = "Character scale multiplier must be finite and greater than zero in scene " + scene.sceneId + ".";
                    return false;
                }
            }

            AppendCharacterStagingWarnings(scene, diagnostics);

            if (scene.decorations != null)
            {
                var decorationIds = new HashSet<string>(StringComparer.Ordinal);
                for (int d = 0; d < scene.decorations.Count; d++)
                {
                    VnSceneComposerDecoration decoration = scene.decorations[d];
                    if (decoration == null)
                    {
                        error = "Scene Composer decoration entry is null in scene " + scene.sceneId + ".";
                        return false;
                    }
                    if (!IsStableId(decoration.decorationId))
                    {
                        error = "Scene Composer decoration ID is missing or invalid in scene " + scene.sceneId + ".";
                        return false;
                    }
                    if (!decorationIds.Add(decoration.decorationId))
                    {
                        error = "Duplicate Scene Composer decoration ID: " + decoration.decorationId + ".";
                        return false;
                    }
                    if (!IsStableId(decoration.assetGuid))
                    {
                        error = "Scene Composer decoration asset GUID is missing or invalid in scene " + scene.sceneId + ".";
                        return false;
                    }
                    if (!IsFinite(decoration.position.x) || !IsFinite(decoration.position.y))
                    {
                        error = "Scene Composer decoration position must be finite in scene " + scene.sceneId + ".";
                        return false;
                    }
                    if (!IsFinite(decoration.scale) || decoration.scale <= 0f || decoration.scale > 10f)
                    {
                        error = "Scene Composer decoration scale must be finite and greater than zero in scene " + scene.sceneId + ".";
                        return false;
                    }
                    if (!IsFinite(decoration.opacity) || decoration.opacity < 0f || decoration.opacity > 1f)
                    {
                        error = "Scene Composer decoration opacity must be between 0 and 1 in scene " + scene.sceneId + ".";
                        return false;
                    }
                    if (!Enum.IsDefined(typeof(VnSceneComposerDecorationLayer), decoration.layer))
                    {
                        error = "Invalid Scene Composer decoration layer in scene " + scene.sceneId + ".";
                        return false;
                    }

                    string decorationPath = AssetDatabase.GUIDToAssetPath(decoration.assetGuid);
                    if (string.IsNullOrEmpty(decorationPath) ||
                        AssetDatabase.LoadAssetAtPath<Texture2D>(decorationPath) == null)
                    {
                        string label = string.IsNullOrWhiteSpace(decoration.displayName)
                            ? decoration.decorationId
                            : decoration.displayName;
                        diagnostics.Add("Decoration asset is missing in scene '" +
                                        (scene.label ?? scene.sceneId) + "': " + label + ".");
                    }
                }
            }

            if (scene.textElements != null)
            {
                var textIds = new HashSet<string>(StringComparer.Ordinal);
                for (int t = 0; t < scene.textElements.Count; t++)
                {
                    VnSceneComposerTextElement textElement = scene.textElements[t];
                    if (textElement == null)
                    {
                        error = "Scene Composer text entry is null in scene " + scene.sceneId + ".";
                        return false;
                    }
                    if (!IsStableId(textElement.textElementId))
                    {
                        error = "Scene Composer text element ID is missing or invalid in scene " + scene.sceneId + ".";
                        return false;
                    }
                    if (!textIds.Add(textElement.textElementId))
                    {
                        error = "Duplicate Scene Composer text element ID: " + textElement.textElementId + ".";
                        return false;
                    }
                    if (!string.IsNullOrEmpty(textElement.fontAssetGuid) && !IsStableId(textElement.fontAssetGuid))
                    {
                        error = "Scene Composer text font GUID is invalid in scene " + scene.sceneId + ".";
                        return false;
                    }
                    if (!IsFinite(textElement.fontSize) || textElement.fontSize < 1f || textElement.fontSize > 512f)
                    {
                        error = "Scene Composer text font size must be between 1 and 512 in scene " + scene.sceneId + ".";
                        return false;
                    }
                    if (!IsFinite(textElement.position.x) || !IsFinite(textElement.position.y) ||
                        !IsFinite(textElement.size.x) || !IsFinite(textElement.size.y) ||
                        textElement.size.x <= 0f || textElement.size.y <= 0f)
                    {
                        error = "Scene Composer text position and size must be finite and positive in scene " + scene.sceneId + ".";
                        return false;
                    }
                    if (!IsFinite(textElement.opacity) || textElement.opacity < 0f || textElement.opacity > 1f)
                    {
                        error = "Scene Composer text opacity must be between 0 and 1 in scene " + scene.sceneId + ".";
                        return false;
                    }
                    Color textColor = textElement.color;
                    if (!IsFinite(textColor.r) || !IsFinite(textColor.g) || !IsFinite(textColor.b) || !IsFinite(textColor.a))
                    {
                        error = "Scene Composer text color must contain finite values in scene " + scene.sceneId + ".";
                        return false;
                    }
                    if (!Enum.IsDefined(typeof(VnSceneComposerTextAlignment), textElement.alignment) ||
                        !Enum.IsDefined(typeof(VnSceneComposerTextLayer), textElement.layer))
                    {
                        error = "Invalid Scene Composer text alignment/layer in scene " + scene.sceneId + ".";
                        return false;
                    }

                    UnityEngine.Object fontAsset = VnSceneComposerTextFontResolver.ResolveAsset(textElement.fontAssetGuid);
                    if (fontAsset == null)
                    {
                        string label = string.IsNullOrWhiteSpace(textElement.fontDisplayName)
                            ? textElement.textElementId
                            : textElement.fontDisplayName;
                        diagnostics.Add("Text font asset is missing in scene '" +
                                        (scene.label ?? scene.sceneId) + "': " + label + ".");
                    }
                    else if (!VnSceneComposerTextFontResolver.IsSupportedAsset(fontAsset))
                    {
                        diagnostics.Add("Text font asset is not a supported TMP/Font asset in scene '" +
                                        (scene.label ?? scene.sceneId) + "': " + fontAsset.name + ".");
                    }
                }
            }

            for (int b = 0; b < scene.dialogueBeats.Count; b++)
            {
                VnSceneComposerDialogueBeat beat = scene.dialogueBeats[b];
                if (beat == null || string.IsNullOrWhiteSpace(beat.targetCharacterId)) continue;

                VnSceneComposerCharacter target = null;
                for (int c = 0; c < scene.characters.Count; c++)
                {
                    VnSceneComposerCharacter candidate = scene.characters[c];
                    if (candidate == null) continue;
                    string candidateId = candidate.characterId ?? string.Empty;
                    if (string.IsNullOrEmpty(candidateId) &&
                        VnSceneComposerCharacterStateResolver.TryResolve(candidate.stateId, out VnSceneComposerResolvedCharacterState baseState))
                        candidateId = baseState.Character;
                    if (string.Equals(candidateId, beat.targetCharacterId, StringComparison.OrdinalIgnoreCase))
                    {
                        target = candidate;
                        break;
                    }
                }
                if (target == null)
                {
                    error = "Dialogue Beat target character '" + beat.targetCharacterId +
                            "' is not visible in scene " + scene.sceneId + ".";
                    return false;
                }
                if (beat.hasStateOverride)
                {
                    if (!VnSceneComposerCharacterStateResolver.TryResolve(
                            beat.stateId, out VnSceneComposerResolvedCharacterState resolvedBeatState))
                    {
                        error = "Missing dialogue Beat character state '" + (beat.stateId ?? string.Empty) +
                                "' in scene " + scene.sceneId + ".";
                        return false;
                    }
                    if (!string.Equals(resolvedBeatState.Character, beat.targetCharacterId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        error = "Dialogue Beat state '" + beat.stateId + "' belongs to " +
                                resolvedBeatState.Character + ", not " + beat.targetCharacterId + ".";
                        return false;
                    }
                }
            }

            if (scene.presentationOverrides == null)
            {
                error = "Scene Composer presentation overrides are missing for scene " + scene.sceneId + ".";
                return false;
            }
            try
            {
                VnPresentationWorkshopPreset resolved = VnSceneComposerComposition.ResolvePresentation(project, scene);
                if (!ValidatePresentation(resolved, out error)) return false;
                AppendTypographyFontWarnings(resolved, "scene " + scene.sceneId + " dialogue typography", diagnostics);
            }
            catch (Exception exception)
            {
                error = "Invalid Scene Composer presentation data in scene " + scene.sceneId + ": " + exception.Message;
                return false;
            }

            if (scene.transition == null)
            {
                error = "Scene Composer transition data is missing for scene " + scene.sceneId + ".";
                return false;
            }
            if (!Enum.IsDefined(typeof(VnSceneComposerSceneTransitionType), scene.transition.sceneTransitionType))
            {
                error = "Invalid Scene Composer scene transition type in scene " + scene.sceneId + ".";
                return false;
            }
            if (!Enum.IsDefined(typeof(VnSceneComposerSceneTransitionDirection), scene.transition.sceneTransitionDirection))
            {
                error = "Invalid Scene Composer scene transition direction in scene " + scene.sceneId + ".";
                return false;
            }
            if (!IsFinite(scene.transition.sceneTransitionDuration) ||
                scene.transition.sceneTransitionDuration < 0f ||
                scene.transition.sceneTransitionDuration > MaxSceneTransitionDuration)
            {
                error = "Scene Composer scene transition duration must be finite and between 0 and " +
                        MaxSceneTransitionDuration + " seconds in scene " + scene.sceneId + ".";
                return false;
            }
            if (scene.timing == null)
            {
                error = "Scene Composer timing data is missing for scene " + scene.sceneId + ".";
                return false;
            }
            if (!Enum.IsDefined(typeof(VnSceneComposerPreviewAdvanceMode), scene.timing.previewAdvanceMode))
            {
                error = "Invalid Scene Composer preview advance mode in scene " + scene.sceneId + ".";
                return false;
            }
            if (!IsFinite(scene.timing.previewAutoDuration) || scene.timing.previewAutoDuration < 0f ||
                scene.timing.previewAutoDuration > MaxPreviewDuration)
            {
                error = "Scene Composer preview auto duration must be finite and between 0 and " +
                        MaxPreviewDuration + " seconds in scene " + scene.sceneId + ".";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool ValidateDialogueBeats(
            VnSceneComposerScene scene, List<string> diagnostics, out string error)
        {
            if (scene.dialogueBeats == null)
            {
                error = "Scene Composer dialogue beat list is missing for scene " + scene.sceneId + ".";
                return false;
            }
            if (scene.dialogueBeats.Count == 0)
            {
                error = "Scene Composer scene " + scene.sceneId + " must contain at least one dialogue beat.";
                return false;
            }

            var beatIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < scene.dialogueBeats.Count; i++)
            {
                VnSceneComposerDialogueBeat beat = scene.dialogueBeats[i];
                if (beat == null)
                {
                    error = "Scene Composer dialogue beat at index " + i + " is null in scene " + scene.sceneId + ".";
                    return false;
                }
                if (!IsStableId(beat.beatId))
                {
                    error = "Scene Composer dialogue beat ID is missing or invalid at index " + i +
                            " in scene " + scene.sceneId + ".";
                    return false;
                }
                if (!beatIds.Add(beat.beatId))
                {
                    error = "Duplicate Scene Composer dialogue beat ID: " + beat.beatId + ".";
                    return false;
                }
                if (!Enum.IsDefined(typeof(VnSceneComposerBeatEffect), beat.effect))
                {
                    error = "Invalid Scene Composer dialogue Beat effect at index " + i + ".";
                    return false;
                }
                bool hasTarget = !string.IsNullOrWhiteSpace(beat.targetCharacterId);
                if (beat.hasStateOverride && !hasTarget)
                {
                    error = "Dialogue Beat state override requires a target character in scene " + scene.sceneId + ".";
                    return false;
                }
                if (!hasTarget && beat.effect != VnSceneComposerBeatEffect.None)
                {
                    error = "Dialogue Beat effect requires a target character in scene " + scene.sceneId + ".";
                    return false;
                }
                if (beat.effect == VnSceneComposerBeatEffect.Accent &&
                    (!IsFinite(beat.effectStrength) || beat.effectStrength < 0f ||
                     !IsFinite(beat.effectDuration) || beat.effectDuration <= 0f || beat.effectDuration > 10f))
                {
                    error = "Dialogue Beat Accent parameters are invalid in scene " + scene.sceneId + ".";
                    return false;
                }

                if (beat.characterStaging == null)
                {
                    error = "Dialogue Beat character staging list is missing in scene " + scene.sceneId + ".";
                    return false;
                }

                var stagingIds = new HashSet<string>(StringComparer.Ordinal);
                var stagingCharacters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int accentCount = 0;
                for (int r = 0; r < beat.characterStaging.Count; r++)
                {
                    VnSceneComposerBeatCharacterStaging staging = beat.characterStaging[r];
                    if (staging == null)
                    {
                        error = "Character staging row is null in scene " + scene.sceneId + ".";
                        return false;
                    }
                    if (!IsStableId(staging.stagingId))
                    {
                        error = "Character staging ID is missing or invalid in scene " + scene.sceneId + ".";
                        return false;
                    }
                    if (!stagingIds.Add(staging.stagingId))
                    {
                        error = "Duplicate character staging ID: " + staging.stagingId + ".";
                        return false;
                    }
                    if (string.IsNullOrWhiteSpace(staging.characterId))
                    {
                        error = "Character staging row requires a character in scene " + scene.sceneId + ".";
                        return false;
                    }
                    if (!stagingCharacters.Add(staging.characterId))
                    {
                        error = "Dialogue Beat contains duplicate staging rows for character '" +
                                staging.characterId + "' in scene " + scene.sceneId + ".";
                        return false;
                    }
                    if (!Enum.IsDefined(typeof(VnSceneComposerBeatCharacterVisibility), staging.visibility) ||
                        !Enum.IsDefined(typeof(VnSceneComposerBeatCharacterPosition), staging.position) ||
                        !Enum.IsDefined(typeof(VnSceneComposerBeatEffect), staging.effect))
                    {
                        error = "Invalid character staging enum value in scene " + scene.sceneId + ".";
                        return false;
                    }
                    if (!IsFinite(staging.delaySeconds) || staging.delaySeconds < 0f)
                    {
                        error = "Character staging delay must be finite and non-negative in scene " +
                                scene.sceneId + ".";
                        return false;
                    }
                    if (staging.position == VnSceneComposerBeatCharacterPosition.Custom &&
                        (!IsFinite(staging.customPositionOffset.x) ||
                         !IsFinite(staging.customPositionOffset.y)))
                    {
                        error = "Character staging custom position must be finite in scene " +
                                scene.sceneId + ".";
                        return false;
                    }
                    if (staging.effect == VnSceneComposerBeatEffect.Accent)
                    {
                        accentCount++;
                        if (!IsFinite(staging.effectStrength) || staging.effectStrength < 0f ||
                            !IsFinite(staging.effectDuration) || staging.effectDuration <= 0f ||
                            staging.effectDuration > 10f)
                        {
                            error = "Character staging Accent parameters are invalid in scene " +
                                    scene.sceneId + ".";
                            return false;
                        }
                    }
                }
                if (accentCount > 1)
                {
                    error = "Dialogue Beat supports one Accent target at a time in scene " +
                            scene.sceneId + ".";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static void AppendCharacterStagingWarnings(
            VnSceneComposerScene scene, List<string> diagnostics)
        {
            if (scene == null || diagnostics == null || scene.dialogueBeats == null) return;
            for (int i = 0; i < scene.dialogueBeats.Count; i++)
            {
                VnSceneComposerDialogueBeat beat = scene.dialogueBeats[i];
                if (beat == null) continue;
                string[] warnings =
                    VnSceneComposerBeatCharacterStagingResolver.CollectWarnings(scene, beat);
                for (int w = 0; w < warnings.Length; w++)
                {
                    string warning = warnings[w];
                    if (!string.IsNullOrWhiteSpace(warning) && !diagnostics.Contains(warning))
                        diagnostics.Add(warning);
                }
            }
        }

        private static bool ValidateAdditionalAudio(
            VnSceneComposerScene scene, List<string> diagnostics, out string error)
        {
            if (scene.additionalAudioCues == null)
            {
                error = "Scene Composer additional audio cue list is missing for scene " + scene.sceneId + ".";
                return false;
            }

            var cueIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < scene.additionalAudioCues.Count; i++)
            {
                VnSceneComposerAdditionalAudioCue cue = scene.additionalAudioCues[i];
                if (cue == null)
                {
                    error = "Additional audio cue at index " + i + " is null in scene " + scene.sceneId + ".";
                    return false;
                }
                if (!IsStableId(cue.cueId))
                {
                    error = "Additional audio cue ID is missing or invalid at index " + i +
                            " in scene " + scene.sceneId + ".";
                    return false;
                }
                if (!cueIds.Add(cue.cueId))
                {
                    error = "Duplicate additional audio cue ID: " + cue.cueId + ".";
                    return false;
                }
                if (!Enum.IsDefined(typeof(VnSceneComposerAudioCategory), cue.category) ||
                    !Enum.IsDefined(typeof(VnSceneComposerAudioTrigger), cue.trigger) ||
                    !Enum.IsDefined(typeof(VnSceneComposerAudioStopMode), cue.stopMode))
                {
                    error = "Invalid additional audio cue enum value in scene " + scene.sceneId + ".";
                    return false;
                }
                if (!IsFinite(cue.volume) || cue.volume < 0f || cue.volume > 1f)
                {
                    error = "Additional audio cue volume must be between 0 and 1 in scene " + scene.sceneId + ".";
                    return false;
                }
                if (!IsFinite(cue.startDelaySeconds) || cue.startDelaySeconds < 0f ||
                    !IsFinite(cue.fadeInSeconds) || cue.fadeInSeconds < 0f ||
                    !IsFinite(cue.fadeOutSeconds) || cue.fadeOutSeconds < 0f)
                {
                    error = "Additional audio timing values must be finite and non-negative in scene " +
                            scene.sceneId + ".";
                    return false;
                }

                if (cue.trigger == VnSceneComposerAudioTrigger.BeatStart &&
                    VnSceneComposerDialogue.FindIndex(scene, cue.startBeatId) < 0)
                {
                    error = "Additional audio cue start Beat is missing from scene " + scene.sceneId + ".";
                    return false;
                }
                if (cue.stopMode == VnSceneComposerAudioStopMode.BeatStart &&
                    VnSceneComposerDialogue.FindIndex(scene, cue.stopBeatId) < 0)
                {
                    error = "Additional audio cue stop Beat is missing from scene " + scene.sceneId + ".";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(cue.assetGuid))
                {
                    diagnostics.Add("Additional audio cue has no AudioClip in scene '" +
                                    (scene.label ?? scene.sceneId) + "': " +
                                    (string.IsNullOrWhiteSpace(cue.displayName) ? cue.cueId : cue.displayName) + ".");
                    continue;
                }
                if (!IsStableId(cue.assetGuid))
                {
                    error = "Additional audio cue asset GUID is invalid in scene " + scene.sceneId + ".";
                    return false;
                }

                string assetPath = AssetDatabase.GUIDToAssetPath(cue.assetGuid);
                if (string.IsNullOrEmpty(assetPath))
                {
                    diagnostics.Add("Additional audio asset is missing in scene '" +
                                    (scene.label ?? scene.sceneId) + "': " +
                                    (string.IsNullOrWhiteSpace(cue.displayName) ? cue.assetGuid : cue.displayName) + ".");
                }
                else if (AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath) == null)
                {
                    error = "Additional audio reference is not an AudioClip in scene " + scene.sceneId + ".";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool ValidateMusic(
            VnSceneComposerScene scene, List<string> diagnostics, out string error)
        {
            VnSceneComposerMusic music = scene.music;
            if (music == null)
            {
                error = string.Empty;
                return true;
            }
            if (!Enum.IsDefined(typeof(VnSceneComposerMusicMode), music.mode))
            {
                error = "Invalid Scene Composer music mode in scene " + scene.sceneId + ".";
                return false;
            }
            if (!IsFinite(music.volume) || music.volume < 0f || music.volume > 1f)
            {
                error = "Scene Composer music volume must be between 0 and 1 in scene " + scene.sceneId + ".";
                return false;
            }
            if (!IsFinite(music.fadeInSeconds) || music.fadeInSeconds < 0f ||
                !IsFinite(music.fadeOutSeconds) || music.fadeOutSeconds < 0f)
            {
                error = "Scene Composer music fades must be finite and non-negative in scene " + scene.sceneId + ".";
                return false;
            }

            if (music.mode == VnSceneComposerMusicMode.Track)
            {
                if (!IsStableId(music.assetGuid))
                {
                    error = "Scene Composer music asset GUID is missing or invalid in scene " + scene.sceneId + ".";
                    return false;
                }
                string path = AssetDatabase.GUIDToAssetPath(music.assetGuid);
                if (string.IsNullOrEmpty(path))
                {
                    diagnostics.Add("Music asset is missing in scene '" +
                                    (scene.label ?? scene.sceneId) + "': " +
                                    (string.IsNullOrWhiteSpace(music.displayName) ? music.assetGuid : music.displayName) + ".");
                }
                else if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) == null)
                {
                    error = "Scene Composer music reference is not an AudioClip in scene " + scene.sceneId + ".";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool ValidateMedia(VnSceneComposerScene scene, List<string> diagnostics, out string error)
        {
            VnSceneComposerMediaReference media = scene.media;
            if (!Enum.IsDefined(typeof(VnSceneComposerMediaKind), media.kind))
            {
                error = "Invalid Scene Composer media kind in scene " + scene.sceneId + ".";
                return false;
            }

            if (media.kind == VnSceneComposerMediaKind.ExistingRokasAsset)
            {
                if (string.IsNullOrWhiteSpace(media.reference))
                {
                    error = "ROKAS asset GUID is missing in scene " + scene.sceneId + ".";
                    return false;
                }
                string path = AssetDatabase.GUIDToAssetPath(media.reference);
                if (string.IsNullOrEmpty(path))
                {
                    error = "ROKAS asset GUID is invalid or missing in scene " + scene.sceneId + ": " + media.reference + ".";
                    return false;
                }
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Sprite sprite = texture == null ? AssetDatabase.LoadAssetAtPath<Sprite>(path) : null;
                if (texture == null && sprite == null)
                {
                    error = "ROKAS asset reference is not a previewable image in scene " + scene.sceneId + ".";
                    return false;
                }
            }
            else if (IsExternalMedia(media.kind))
            {
                if (string.IsNullOrWhiteSpace(media.reference) && string.IsNullOrWhiteSpace(media.displayName))
                {
                    error = "External media reference is missing in scene " + scene.sceneId + ".";
                    return false;
                }

                bool available = !string.IsNullOrWhiteSpace(media.reference) &&
                                 !IsPortableExternalReference(media.reference) &&
                                 Path.IsPathRooted(media.reference) && File.Exists(media.reference);
                if (!available)
                {
                    string label = !string.IsNullOrWhiteSpace(media.displayName) ? media.displayName : media.reference;
                    diagnostics.Add("External media is missing or not locally bound for scene '" +
                                    (scene.label ?? scene.sceneId) + "': " + (label ?? string.Empty) + ".");
                }
            }

            error = string.Empty;
            return true;
        }

        private static void AppendTypographyFontWarnings(
            VnPresentationWorkshopPreset preset, string context, List<string> diagnostics)
        {
            if (preset == null || preset.typography == null || diagnostics == null) return;
            AppendFontWarning(preset.typography.hasSpeakerFontAssetGuid
                    ? preset.typography.speakerFontAssetGuid : string.Empty,
                context + " speaker font", diagnostics);
            AppendFontWarning(preset.typography.hasDialogueFontAssetGuid
                    ? preset.typography.dialogueFontAssetGuid : string.Empty,
                context + " dialogue font", diagnostics);
        }

        private static void AppendFontWarning(string guid, string label, List<string> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(guid)) return;
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(assetPath) &&
                VnSceneComposerTextFontResolver.ResolveAsset(guid) != null) return;
            string warning = label + ": project font asset is missing; authored GUID is preserved and RokasSans will be used.";
            if (!diagnostics.Contains(warning)) diagnostics.Add(warning);
        }

        private static bool ValidatePresentation(VnPresentationWorkshopPreset preset, out string error)
        {
            if (preset == null)
            {
                error = "Scene Composer presentation data is missing.";
                return false;
            }
            VnPresentationWorkshopPreset clone = JsonUtility.FromJson<VnPresentationWorkshopPreset>(JsonUtility.ToJson(preset));
            if (clone == null)
            {
                error = "Scene Composer presentation data could not be cloned for validation.";
                return false;
            }
            return VnPresentationWorkshopSerialization.ValidatePreset(clone, out error);
        }

        private static VnSceneComposerProject CloneProject(VnSceneComposerProject project)
        {
            VnSceneComposerProject clone = JsonUtility.FromJson<VnSceneComposerProject>(JsonUtility.ToJson(project));
            if (clone == null) throw new InvalidOperationException("Scene Composer project could not be cloned.");
            return clone;
        }

        private static void MigrateBeatStateSchemaV2(VnSceneComposerProject project)
        {
            if (project == null) return;
            if (project.scenes == null) project.scenes = new List<VnSceneComposerScene>();
            for (int i = 0; i < project.scenes.Count; i++)
            {
                VnSceneComposerScene scene = project.scenes[i];
                if (scene == null || scene.dialogueBeats == null) continue;
                for (int b = 0; b < scene.dialogueBeats.Count; b++)
                {
                    VnSceneComposerDialogueBeat beat = scene.dialogueBeats[b];
                    if (beat == null) continue;
                    beat.targetCharacterId = string.Empty;
                    beat.hasStateOverride = false;
                    beat.stateId = string.Empty;
                    beat.effect = VnSceneComposerBeatEffect.None;
                    beat.effectStrength = 18f;
                    beat.effectDuration = .28f;
                }
            }
            project.schemaVersion = SchemaVersion;
        }

        private static void NormalizeProject(VnSceneComposerProject project)
        {
            if (project.defaultPresentation == null) project.defaultPresentation = new VnPresentationWorkshopPreset();
            if (project.speakerStyleOverrides == null)
                project.speakerStyleOverrides = new List<VnSceneComposerSpeakerStyleOverride>();
            for (int s = project.speakerStyleOverrides.Count - 1; s >= 0; s--)
            {
                VnSceneComposerSpeakerStyleOverride speakerStyle = project.speakerStyleOverrides[s];
                if (speakerStyle == null)
                {
                    project.speakerStyleOverrides.RemoveAt(s);
                    continue;
                }
                if (speakerStyle.characterId == null) speakerStyle.characterId = string.Empty;
                if (speakerStyle.style == null)
                    speakerStyle.style = new VnSceneComposerTextVisualStyleOverride();
                NormalizeTextVisualStyle(speakerStyle.style);
            }
            if (project.scenes == null) project.scenes = new List<VnSceneComposerScene>();
            PromoteLegacySharedSpeakerTypography(project);
            PromoteLegacySharedPlaqueGeometry(project);
            if (project.title == null) project.title = string.Empty;
            if (project.sourceHead == null) project.sourceHead = string.Empty;
            if (project.projectId == null) project.projectId = string.Empty;

            for (int i = 0; i < project.scenes.Count; i++)
            {
                VnSceneComposerScene scene = project.scenes[i];
                if (scene == null) continue;
                if (scene.sceneId == null) scene.sceneId = string.Empty;
                if (scene.label == null) scene.label = string.Empty;
                if (scene.dialogueBeats == null) scene.dialogueBeats = new List<VnSceneComposerDialogueBeat>();
                if (scene.dialogueBeats.Count == 0) scene.dialogueBeats.Add(new VnSceneComposerDialogueBeat());
                for (int b = 0; b < scene.dialogueBeats.Count; b++)
                {
                    VnSceneComposerDialogueBeat beat = scene.dialogueBeats[b];
                    if (beat == null)
                    {
                        scene.dialogueBeats[b] = beat = new VnSceneComposerDialogueBeat();
                    }
                    if (beat.speaker == null) beat.speaker = string.Empty;
                    if (beat.text == null) beat.text = string.Empty;
                    if (beat.targetCharacterId == null) beat.targetCharacterId = string.Empty;
                    if (beat.stateId == null) beat.stateId = string.Empty;
                    if (!IsFinite(beat.effectStrength) || beat.effectStrength < 0f) beat.effectStrength = 18f;
                    if (!IsFinite(beat.effectDuration) || beat.effectDuration <= 0f) beat.effectDuration = .28f;
                    if (beat.characterStaging == null)
                        beat.characterStaging = new List<VnSceneComposerBeatCharacterStaging>();
                    for (int r = 0; r < beat.characterStaging.Count; r++)
                    {
                        VnSceneComposerBeatCharacterStaging staging = beat.characterStaging[r];
                        if (staging == null)
                        {
                            beat.characterStaging[r] = staging =
                                new VnSceneComposerBeatCharacterStaging();
                        }
                        if (staging.stagingId == null) staging.stagingId = string.Empty;
                        if (staging.characterId == null) staging.characterId = string.Empty;
                        if (staging.stateId == null) staging.stateId = string.Empty;
                        if (!Enum.IsDefined(typeof(VnSceneComposerBeatCharacterVisibility), staging.visibility))
                            staging.visibility = VnSceneComposerBeatCharacterVisibility.KeepPrevious;
                        if (!Enum.IsDefined(typeof(VnSceneComposerBeatCharacterPosition), staging.position))
                            staging.position = VnSceneComposerBeatCharacterPosition.KeepPrevious;
                        if (!Enum.IsDefined(typeof(VnSceneComposerBeatEffect), staging.effect))
                            staging.effect = VnSceneComposerBeatEffect.None;
                        if (!IsFinite(staging.delaySeconds) || staging.delaySeconds < 0f)
                            staging.delaySeconds = 0f;
                        if (!IsFinite(staging.customPositionOffset.x) ||
                            !IsFinite(staging.customPositionOffset.y))
                            staging.customPositionOffset = Vector2.zero;
                        if (!IsFinite(staging.effectStrength) || staging.effectStrength < 0f)
                            staging.effectStrength = 18f;
                        if (!IsFinite(staging.effectDuration) || staging.effectDuration <= 0f)
                            staging.effectDuration = .28f;
                    }
                }
                if (scene.media == null) scene.media = new VnSceneComposerMediaReference();
                if (scene.music == null) scene.music = new VnSceneComposerMusic();
                if (scene.music.assetGuid == null) scene.music.assetGuid = string.Empty;
                if (scene.music.displayName == null) scene.music.displayName = string.Empty;
                if (!Enum.IsDefined(typeof(VnSceneComposerMusicMode), scene.music.mode))
                    scene.music.mode = VnSceneComposerMusicMode.Silence;
                if (!IsFinite(scene.music.volume)) scene.music.volume = 1f;
                scene.music.volume = Mathf.Clamp01(scene.music.volume);
                if (!IsFinite(scene.music.fadeInSeconds) || scene.music.fadeInSeconds < 0f)
                    scene.music.fadeInSeconds = 0f;
                if (!IsFinite(scene.music.fadeOutSeconds) || scene.music.fadeOutSeconds < 0f)
                    scene.music.fadeOutSeconds = 0f;
                if (scene.additionalAudioCues == null)
                    scene.additionalAudioCues = new List<VnSceneComposerAdditionalAudioCue>();
                for (int a = 0; a < scene.additionalAudioCues.Count; a++)
                {
                    VnSceneComposerAdditionalAudioCue cue = scene.additionalAudioCues[a];
                    if (cue == null)
                    {
                        scene.additionalAudioCues[a] = cue = new VnSceneComposerAdditionalAudioCue();
                    }
                    if (cue.cueId == null) cue.cueId = string.Empty;
                    if (cue.displayName == null) cue.displayName = string.Empty;
                    if (cue.assetGuid == null) cue.assetGuid = string.Empty;
                    if (cue.startBeatId == null) cue.startBeatId = string.Empty;
                    if (cue.stopBeatId == null) cue.stopBeatId = string.Empty;
                    if (!Enum.IsDefined(typeof(VnSceneComposerAudioCategory), cue.category))
                        cue.category = VnSceneComposerAudioCategory.Sfx;
                    if (!Enum.IsDefined(typeof(VnSceneComposerAudioTrigger), cue.trigger))
                        cue.trigger = VnSceneComposerAudioTrigger.SceneStart;
                    if (!Enum.IsDefined(typeof(VnSceneComposerAudioStopMode), cue.stopMode))
                        cue.stopMode = VnSceneComposerAudioStopMode.Natural;
                    if (!IsFinite(cue.volume)) cue.volume = 1f;
                    cue.volume = Mathf.Clamp01(cue.volume);
                    if (!IsFinite(cue.startDelaySeconds) || cue.startDelaySeconds < 0f)
                        cue.startDelaySeconds = 0f;
                    if (!IsFinite(cue.fadeInSeconds) || cue.fadeInSeconds < 0f)
                        cue.fadeInSeconds = 0f;
                    if (!IsFinite(cue.fadeOutSeconds) || cue.fadeOutSeconds < 0f)
                        cue.fadeOutSeconds = 0f;
                }
                if (scene.characters == null) scene.characters = new List<VnSceneComposerCharacter>();
                if (scene.decorations == null) scene.decorations = new List<VnSceneComposerDecoration>();
                for (int d = 0; d < scene.decorations.Count; d++)
                {
                    VnSceneComposerDecoration decoration = scene.decorations[d];
                    if (decoration == null) continue;
                    if (decoration.decorationId == null) decoration.decorationId = string.Empty;
                    if (decoration.assetGuid == null) decoration.assetGuid = string.Empty;
                    if (decoration.displayName == null) decoration.displayName = string.Empty;
                }
                if (scene.textElements == null) scene.textElements = new List<VnSceneComposerTextElement>();
                for (int t = 0; t < scene.textElements.Count; t++)
                {
                    VnSceneComposerTextElement textElement = scene.textElements[t];
                    if (textElement == null) continue;
                    if (textElement.textElementId == null) textElement.textElementId = string.Empty;
                    if (textElement.text == null) textElement.text = string.Empty;
                    if (textElement.fontAssetGuid == null) textElement.fontAssetGuid = string.Empty;
                    if (textElement.fontDisplayName == null) textElement.fontDisplayName = string.Empty;
                }
                if (scene.presentationOverrides == null) scene.presentationOverrides = new VnPresentationWorkshopPreset();
                if (!Enum.IsDefined(typeof(VnSceneComposerTextGeometryScope), scene.textGeometryScope))
                    scene.textGeometryScope = VnSceneComposerTextGeometryScope.AllScenes;
                if (scene.dialogueBodyStyleOverride == null)
                    scene.dialogueBodyStyleOverride = new VnSceneComposerTextVisualStyleOverride();
                NormalizeTextVisualStyle(scene.dialogueBodyStyleOverride);
                if (scene.transition == null) scene.transition = new VnSceneComposerTransition();
                if (!Enum.IsDefined(typeof(VnSceneComposerSceneTransitionType), scene.transition.sceneTransitionType))
                    scene.transition.sceneTransitionType = VnSceneComposerSceneTransitionType.None;
                if (!Enum.IsDefined(typeof(VnSceneComposerSceneTransitionDirection), scene.transition.sceneTransitionDirection))
                    scene.transition.sceneTransitionDirection = VnSceneComposerSceneTransitionDirection.LeftToRight;
                if (!IsFinite(scene.transition.sceneTransitionDuration) || scene.transition.sceneTransitionDuration < 0f)
                    scene.transition.sceneTransitionDuration = 0f;
                scene.transition.sceneTransitionDuration =
                    Mathf.Clamp(scene.transition.sceneTransitionDuration, 0f, MaxSceneTransitionDuration);
                if (scene.timing == null) scene.timing = new VnSceneComposerTiming();
                if (scene.media.reference == null) scene.media.reference = string.Empty;
                if (scene.media.displayName == null) scene.media.displayName = string.Empty;
                if (scene.media.contentHash == null) scene.media.contentHash = string.Empty;
                for (int c = 0; c < scene.characters.Count; c++)
                {
                    VnSceneComposerCharacter character = scene.characters[c];
                    if (character == null) continue;
                    if (character.characterId == null) character.characterId = string.Empty;
                    if (character.stateId == null) character.stateId = string.Empty;
                }
            }
        }

        private static void PromoteLegacySharedSpeakerTypography(VnSceneComposerProject project)
        {
            if (project == null) return;
            if (project.defaultPresentation == null)
                project.defaultPresentation = new VnPresentationWorkshopPreset();
            if (project.defaultPresentation.typography == null)
                project.defaultPresentation.typography = new VnWorkshopTypographyOverride();

            VnWorkshopTypographyOverride shared = project.defaultPresentation.typography;
            bool hasCharacterColor = false;

            if (project.speakerStyleOverrides != null)
            {
                for (int i = 0; i < project.speakerStyleOverrides.Count; i++)
                {
                    VnSceneComposerSpeakerStyleOverride entry = project.speakerStyleOverrides[i];
                    VnSceneComposerTextVisualStyleOverride style = entry != null ? entry.style : null;
                    if (style == null) continue;
                    if (style.hasColor) hasCharacterColor = true;

                    // The previous revision could persist a complete character style. The new
                    // contract keeps only color character-specific, so promote the first authored
                    // font/size/alignment values into the shared speaker style when shared values
                    // are otherwise absent. The serialized character payload is preserved.
                    if (!shared.hasSpeakerFontPreset && style.hasFontPreset)
                    {
                        shared.hasSpeakerFontPreset = true;
                        shared.speakerFontPreset = style.fontPreset;
                    }
                    if (!shared.hasSpeakerFontAssetGuid && style.hasFontAssetGuid)
                    {
                        shared.hasSpeakerFontAssetGuid = true;
                        shared.speakerFontAssetGuid = style.fontAssetGuid ?? string.Empty;
                    }
                    if (!shared.hasSpeakerFontSize && style.hasFontSize)
                    {
                        shared.hasSpeakerFontSize = true;
                        shared.speakerFontSize = style.fontSize;
                    }
                    if (!shared.hasSpeakerAlignment && style.hasAlignment)
                    {
                        shared.hasSpeakerAlignment = true;
                        shared.speakerAlignment = style.alignment;
                    }
                    if (!shared.hasSpeakerCharacterSpacing && style.hasCharacterSpacing)
                    {
                        shared.hasSpeakerCharacterSpacing = true;
                        shared.speakerCharacterSpacing = style.characterSpacing;
                    }
                }
            }

            if (project.scenes == null) return;
            for (int i = 0; i < project.scenes.Count; i++)
            {
                VnSceneComposerScene scene = project.scenes[i];
                VnWorkshopTypographyOverride legacy =
                    scene != null && scene.presentationOverrides != null
                        ? scene.presentationOverrides.typography
                        : null;
                if (legacy == null) continue;

                if (!shared.hasSpeakerFontPreset && legacy.hasSpeakerFontPreset)
                {
                    shared.hasSpeakerFontPreset = true;
                    shared.speakerFontPreset = legacy.speakerFontPreset;
                }
                if (!shared.hasSpeakerFontAssetGuid && legacy.hasSpeakerFontAssetGuid)
                {
                    shared.hasSpeakerFontAssetGuid = true;
                    shared.speakerFontAssetGuid = legacy.speakerFontAssetGuid ?? string.Empty;
                }
                if (!shared.hasSpeakerFontSize && legacy.hasSpeakerFontSize)
                {
                    shared.hasSpeakerFontSize = true;
                    shared.speakerFontSize = legacy.speakerFontSize;
                }
                if (!shared.hasSpeakerAlignment && legacy.hasSpeakerAlignment)
                {
                    shared.hasSpeakerAlignment = true;
                    shared.speakerAlignment = legacy.speakerAlignment;
                }
                if (!shared.hasSpeakerCharacterSpacing && legacy.hasSpeakerCharacterSpacing)
                {
                    shared.hasSpeakerCharacterSpacing = true;
                    shared.speakerCharacterSpacing = legacy.speakerCharacterSpacing;
                }
                if (!shared.hasSpeakerColor && !hasCharacterColor && legacy.hasSpeakerColor)
                {
                    shared.hasSpeakerColor = true;
                    shared.speakerColor = legacy.speakerColor;
                }
            }
        }

        private static void PromoteLegacySharedPlaqueGeometry(VnSceneComposerProject project)
        {
            if (project == null) return;
            if (project.defaultPresentation == null)
                project.defaultPresentation = new VnPresentationWorkshopPreset();
            VnWorkshopElementOverride shared = project.defaultPresentation.dialoguePanel;
            if (shared == null) return;

            if (project.scenes != null)
            {
                for (int i = 0; i < project.scenes.Count; i++)
                {
                    VnSceneComposerScene scene = project.scenes[i];
                    VnWorkshopElementOverride local =
                        scene != null && scene.presentationOverrides != null
                            ? scene.presentationOverrides.dialoguePanel
                            : null;
                    if (local == null) continue;

                    if (!shared.hasPositionDelta && local.hasPositionDelta)
                    {
                        shared.hasPositionDelta = true;
                        shared.positionDelta = local.positionDelta;
                    }
                    if (!shared.hasSizeDelta && local.hasSizeDelta)
                    {
                        shared.hasSizeDelta = true;
                        shared.sizeDelta = local.sizeDelta;
                    }
                    if (!shared.hasScaleMultiplier && local.hasScaleMultiplier)
                    {
                        shared.hasScaleMultiplier = true;
                        shared.scaleMultiplier = local.scaleMultiplier;
                    }
                }

                // Physical plaque geometry has one owner after migration. Scene-local PNG
                // dialoguePanelVisual data is separate and intentionally remains untouched.
                for (int i = 0; i < project.scenes.Count; i++)
                {
                    VnSceneComposerScene scene = project.scenes[i];
                    if (scene != null && scene.presentationOverrides != null)
                        scene.presentationOverrides.ResetElement(VnWorkshopElement.DialoguePanel);
                }
            }
        }

        private static void NormalizeTextVisualStyle(VnSceneComposerTextVisualStyleOverride style)
        {
            if (style == null) return;
            if (style.fontAssetGuid == null) style.fontAssetGuid = string.Empty;
            if (!Enum.IsDefined(typeof(VnWorkshopFontPreset), style.fontPreset))
                style.fontPreset = VnWorkshopFontPreset.ProjectSans;
            if (!Enum.IsDefined(typeof(VnWorkshopTextAlignment), style.alignment))
                style.alignment = VnWorkshopTextAlignment.Left;
            if (!IsFinite(style.fontSize) || style.fontSize < 0f) style.fontSize = 0f;
            if (!IsFinite(style.characterSpacing)) style.characterSpacing = 0f;
            if (!IsFinite(style.lineSpacing)) style.lineSpacing = 0f;
            if (!IsFinite(style.paragraphSpacing)) style.paragraphSpacing = 0f;
            if (!IsFinite(style.color.r) || !IsFinite(style.color.g) ||
                !IsFinite(style.color.b) || !IsFinite(style.color.a))
                style.color = Color.white;
        }

        private static void MakeExternalReferencesPortable(VnSceneComposerProject project)
        {
            for (int i = 0; i < project.scenes.Count; i++)
            {
                VnSceneComposerScene scene = project.scenes[i];
                if (scene == null || scene.media == null || !IsExternalMedia(scene.media.kind)) continue;
                VnSceneComposerMediaReference media = scene.media;
                if (IsPortableExternalReference(media.reference)) continue;

                string displayName = media.displayName;
                if (string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(media.reference))
                    displayName = Path.GetFileName(media.reference);
                displayName = SafePortableSegment(displayName, "external-media");
                string hash = SafePortableSegment(media.contentHash, "unhashed");
                media.displayName = displayName;
                media.reference = ExternalReferencePrefix + hash + "/" + displayName;
                media.localPreviewDependency = true;
            }
        }

        private static string SafePortableSegment(string value, string fallback)
        {
            string source = (value ?? string.Empty).Trim();
            if (source.Length == 0) return fallback;
            source = Path.GetFileName(source);
            var chars = new char[Math.Min(source.Length, 160)];
            int count = 0;
            for (int i = 0; i < source.Length && count < chars.Length; i++)
            {
                char c = source[i];
                if (char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-') chars[count++] = c;
                else chars[count++] = '_';
            }
            string result = new string(chars, 0, count).Trim('.');
            return string.IsNullOrEmpty(result) ? fallback : result;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
