using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Rokas.Presentation;
using UnityEditor;
using UnityEngine;


namespace Rokas.EditorTools.VnUiWorkshop
{
    public static class RokasVnRuntimeIntroExporter
    {
        public const string ProjectId = RokasVnRuntimeIntroPackage.ExpectedProjectId;
        public const string DefaultRuntimeRoot =
            "Assets/Rokas/Resources/VN/RuntimeIntro";
        public const string DefaultPackagePath =
            DefaultRuntimeRoot + "/RokasVnRuntimeIntroPackage.asset";

        private static readonly Regex AssetGuidValue = new Regex(
            @"(?i)""(?:assetGuid|[A-Za-z0-9_]*FontAssetGuid)""\s*:\s*""([0-9a-f]{32})""",
            RegexOptions.Compiled);

        [MenuItem("ROKAS/VN Scene Composer/Обновить runtime intro")]
        public static void ExportRuntimeIntroMenu()
        {
            RokasVnRuntimeIntroPackage package = ExportCurrentProjectToDefaultPackage();
            EditorGUIUtility.PingObject(package);
            EditorUtility.DisplayDialog(
                "ROKAS VN",
                "Runtime intro обновлён.\nProject ID: " + package.ProjectId +
                "\nScenes: " + package.Snapshot.sceneCount +
                "\nBeats: " + package.Snapshot.beatCount,
                "OK");
        }

        public static void ExportFromCommandLine()
        {
            RokasVnRuntimeIntroPackage package = ExportCurrentProjectToDefaultPackage();
            Debug.Log(
                "ROKAS_VN_RUNTIME_INTRO_EXPORT=PASS project=" + package.ProjectId +
                " scenes=" + package.Snapshot.sceneCount +
                " beats=" + package.Snapshot.beatCount +
                " sha256=" + package.SourceProjectSha256);
        }

        public static RokasVnRuntimeIntroPackage ExportCurrentProjectToDefaultPackage()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            VnSceneComposerImportResult loaded =
                VnSceneComposerStorage.LoadProject(projectRoot, ProjectId);
            if (!loaded.Success || loaded.Project == null)
                throw new InvalidOperationException(
                    "Authoritative VN intro project could not be loaded: " + loaded.Error);

            string portableJson =
                VnSceneComposerSerialization.SerializePortable(loaded.Project);
            string sha256 = ComputeSha256(Encoding.UTF8.GetBytes(portableJson));
            return ExportProjectOwnedPackage(
                loaded.Project, portableJson, sha256,
                DefaultPackagePath, DefaultRuntimeRoot);
        }

        public static RokasVnRuntimeIntroPackage ExportProjectOwnedPackage(
            VnSceneComposerProject project,
            string portableJson,
            string sourceProjectSha256,
            string packagePath,
            string runtimeRoot)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (!string.Equals(project.projectId, ProjectId, StringComparison.Ordinal))
                throw new ArgumentException(
                    "Runtime intro export requires the authoritative authored Project ID.",
                    nameof(project));
            if (string.IsNullOrWhiteSpace(portableJson))
                throw new ArgumentException("Portable project JSON is required.", nameof(portableJson));
            if (string.IsNullOrWhiteSpace(sourceProjectSha256))
                throw new ArgumentException("Source project SHA256 is required.", nameof(sourceProjectSha256));

            runtimeRoot = NormalizeAssetPath(runtimeRoot);
            packagePath = NormalizeAssetPath(packagePath);
            RequireRuntimeAssetPath(runtimeRoot);
            if (!packagePath.StartsWith(runtimeRoot + "/", StringComparison.Ordinal))
                throw new ArgumentException(
                    "Runtime package must live inside the runtime export root.",
                    nameof(packagePath));

            EnsureAssetFolder(runtimeRoot);
            EnsureAssetFolder(runtimeRoot + "/Assets");
            EnsureAssetFolder(runtimeRoot + "/Characters");
            EnsureAssetFolder(runtimeRoot + "/UI");
            EnsureAssetFolder(runtimeRoot + "/External");

            var bindings = new List<RokasVnRuntimeAssetBinding>();
            var boundKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string guid in CollectReferencedAssetGuids(project, portableJson))
                AddGuidBinding(guid, runtimeRoot, bindings, boundKeys);

            AddExternalMediaBindings(project, runtimeRoot, bindings, boundKeys);
            List<RokasVnRuntimeCharacterStateBinding> characterStates =
                ExportCharacterStates(project, runtimeRoot);

            Texture2D plaque = CopyTexture(
                AssetDatabase.GUIDToAssetPath(VnSceneComposerRuntimeUi.DefaultPlaqueGuid),
                runtimeRoot + "/UI/RokasDefaultPlaque.png");
            Texture2D controls = CopyTexture(
                VnSceneComposerRuntimeUi.ControlSheetPath,
                runtimeRoot + "/UI/RokasFinalControlSheet.png");
            Texture2D muted = CopyTexture(
                VnSceneComposerRuntimeUi.MutedSpeakerPath,
                runtimeRoot + "/UI/RokasMutedSpeaker.png");
            Texture2D triangle = CopyTexture(
                VnSceneComposerRuntimeUi.CompletionTrianglePath,
                runtimeRoot + "/UI/RokasCompletionArrow.png");

            RokasVnRuntimeIntroSnapshot snapshot =
                BuildSnapshot(project, sourceProjectSha256);

            RokasVnRuntimeIntroPackage package =
                AssetDatabase.LoadAssetAtPath<RokasVnRuntimeIntroPackage>(packagePath);
            if (package == null)
            {
                package = ScriptableObject.CreateInstance<RokasVnRuntimeIntroPackage>();
                AssetDatabase.CreateAsset(package, packagePath);
            }

            package.Configure(
                ProjectId,
                sourceProjectSha256,
                portableJson,
                snapshot,
                bindings,
                characterStates,
                plaque,
                controls,
                muted,
                triangle);
            EditorUtility.SetDirty(package);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(packagePath, ImportAssetOptions.ForceSynchronousImport);

            string error;
            if (!package.TryValidate(out error))
                throw new InvalidOperationException(
                    "Generated runtime intro package failed validation: " + error);
            return package;
        }


        public static RokasVnRuntimeIntroSnapshot BuildSnapshot(
            VnSceneComposerProject project,
            string sourceProjectSha256)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (!string.Equals(project.projectId, ProjectId, StringComparison.Ordinal))
                throw new ArgumentException(
                    "Runtime intro export requires the authoritative authored Project ID.",
                    nameof(project));
            if (string.IsNullOrWhiteSpace(sourceProjectSha256))
                throw new ArgumentException(
                    "Source project SHA256 is required for deterministic runtime export.",
                    nameof(sourceProjectSha256));

            var result = new RokasVnRuntimeIntroSnapshot
            {
                projectId = project.projectId ?? string.Empty,
                title = project.title ?? string.Empty,
                sourceProjectSha256 = sourceProjectSha256,
                sceneCount = 0,
                beatCount = 0
            };

            if (project.scenes == null) return result;

            for (int sceneIndex = 0; sceneIndex < project.scenes.Count; sceneIndex++)
            {
                VnSceneComposerScene scene = project.scenes[sceneIndex];
                if (scene == null) continue;

                var mapped = new RokasVnRuntimeSceneSnapshot
                {
                    sceneId = scene.sceneId ?? string.Empty,
                    label = scene.label ?? string.Empty,
                    media = MapMedia(scene.media),
                    music = MapMusic(scene.music),
                    keepPreviousAdditionalAudio = scene.keepPreviousAdditionalAudio,
                    presentation = MapPresentation(project, scene),
                    sceneTransitionType = scene.transition != null
                        ? (int)scene.transition.sceneTransitionType : 0,
                    sceneTransitionDirection = scene.transition != null
                        ? (int)scene.transition.sceneTransitionDirection : 0,
                    sceneTransitionDuration = scene.transition != null
                        ? scene.transition.sceneTransitionDuration : .7f,
                    triggerActionBounce = scene.transition != null &&
                        scene.transition.triggerActionBounce,
                    isTerminal = scene.isTerminal,
                    terminalFadeDuration = scene.terminalFadeDuration
                };
                if (scene.media != null &&
                    (scene.media.kind == VnSceneComposerMediaKind.ExternalImage ||
                     scene.media.kind == VnSceneComposerMediaKind.ExternalVideo ||
                     scene.media.kind == VnSceneComposerMediaKind.ExternalGif))
                    mapped.media.runtimeAssetKey = "scene-media:" + mapped.sceneId;

                if (scene.additionalAudioCues != null)
                {
                    for (int i = 0; i < scene.additionalAudioCues.Count; i++)
                    {
                        VnSceneComposerAdditionalAudioCue cue = scene.additionalAudioCues[i];
                        if (cue != null) mapped.additionalAudioCues.Add(MapAudioCue(cue));
                    }
                }

                if (scene.characters != null)
                {
                    for (int i = 0; i < scene.characters.Count; i++)
                    {
                        VnSceneComposerCharacter character = scene.characters[i];
                        if (character != null) mapped.characters.Add(MapCharacter(character));
                    }
                }

                if (scene.dialogueBeats != null)
                {
                    for (int i = 0; i < scene.dialogueBeats.Count; i++)
                    {
                        VnSceneComposerDialogueBeat beat = scene.dialogueBeats[i];
                        if (beat == null) continue;
                        mapped.dialogueBeats.Add(MapBeat(beat));
                        result.beatCount++;
                    }
                }

                result.scenes.Add(mapped);
                result.sceneCount++;
            }

            return result;
        }

        private static RokasVnRuntimePresentationSnapshot MapPresentation(
            VnSceneComposerProject project,
            VnSceneComposerScene scene)
        {
            VnPresentationWorkshopPreset preset =
                VnSceneComposerComposition.ResolvePresentation(project, scene);
            VnWorkshopStageLayoutValues stage =
                VnPresentationWorkshopVn10Resolver.ResolveStageLayout(preset);
            VnWorkshopSpeakerFocusValues focus =
                VnPresentationWorkshopVn10Resolver.ResolveSpeakerFocus(preset);
            VnWorkshopTypewriterValues typewriter =
                VnPresentationWorkshopVn10Resolver.ResolveTypewriter(preset);
            VnWorkshopTypographyValues typography =
                VnPresentationWorkshopVn10Resolver.ResolveTypography(preset);

            return new RokasVnRuntimePresentationSnapshot
            {
                leftX = stage.LeftX,
                centerX = stage.CenterX,
                rightX = stage.RightX,
                slotY = stage.SlotY,
                leftScale = stage.LeftScale,
                centerScale = stage.CenterScale,
                rightScale = stage.RightScale,
                stageRepositionDuration = stage.RepositionDuration,
                stageEasing = (int)stage.Easing,

                speakerActiveScale = focus.ActiveScale,
                speakerActiveBrightness = focus.ActiveBrightness,
                speakerActiveForwardOffset = focus.ActiveForwardOffset,
                speakerInactiveScale = focus.InactiveScale,
                speakerInactiveBrightness = focus.InactiveBrightness,
                speakerInactiveAlpha = focus.InactiveAlpha,
                speakerFocusTransitionDuration = focus.TransitionDuration,
                speakerFocusEasing = (int)focus.Easing,

                typewriterEnabled = typewriter.Enabled,
                typewriterCharactersPerSecond = typewriter.CharactersPerSecond,
                typewriterBaseCharacterDelay = typewriter.BaseCharacterDelay,
                typewriterCommaPause = typewriter.CommaPause,
                typewriterPeriodPause = typewriter.PeriodPause,
                typewriterEllipsisPause = typewriter.EllipsisPause,
                typewriterQuestionPause = typewriter.QuestionPause,
                typewriterExclamationPause = typewriter.ExclamationPause,
                typewriterLineStartDelay = typewriter.LineStartDelay,

                dialogueFontPreset = (int)typography.DialogueFontPreset,
                dialogueFontAssetGuid = typography.DialogueFontAssetGuid ?? string.Empty,
                dialogueColor = typography.DialogueColor,
                dialogueFontSize = typography.DialogueFontSize,
                dialogueCharacterSpacing = typography.DialogueCharacterSpacing,
                dialogueLineSpacing = typography.DialogueLineSpacing,
                dialogueParagraphSpacing = typography.DialogueParagraphSpacing,
                dialogueAlignment = (int)typography.DialogueAlignment,

                speakerFontPreset = (int)typography.SpeakerFontPreset,
                speakerFontAssetGuid = typography.SpeakerFontAssetGuid ?? string.Empty,
                speakerColor = typography.SpeakerColor,
                speakerFontSize = typography.SpeakerFontSize,
                speakerCharacterSpacing = typography.SpeakerCharacterSpacing,
                speakerAlignment = (int)typography.SpeakerAlignment
            };
        }

        private static RokasVnRuntimeMediaSnapshot MapMedia(VnSceneComposerMediaReference media)
        {
            media = media ?? new VnSceneComposerMediaReference();
            return new RokasVnRuntimeMediaSnapshot
            {
                kind = (int)media.kind,
                reference = media.reference ?? string.Empty,
                displayName = media.displayName ?? string.Empty,
                contentHash = media.contentHash ?? string.Empty,
                runtimeAssetKey = media.kind == VnSceneComposerMediaKind.ExistingRokasAsset
                    ? (media.reference ?? string.Empty)
                    : string.Empty,
                scaleMode = (int)media.scaleMode,
                loop = media.loop
            };
        }

        private static RokasVnRuntimeMusicSnapshot MapMusic(VnSceneComposerMusic music)
        {
            music = music ?? new VnSceneComposerMusic();
            return new RokasVnRuntimeMusicSnapshot
            {
                mode = (int)music.mode,
                assetGuid = music.assetGuid ?? string.Empty,
                displayName = music.displayName ?? string.Empty,
                volume = music.volume,
                loop = music.loop,
                fadeInSeconds = music.fadeInSeconds,
                fadeOutSeconds = music.fadeOutSeconds
            };
        }

        private static RokasVnRuntimeAudioCueSnapshot MapAudioCue(
            VnSceneComposerAdditionalAudioCue cue)
        {
            return new RokasVnRuntimeAudioCueSnapshot
            {
                cueId = cue.cueId ?? string.Empty,
                displayName = cue.displayName ?? string.Empty,
                assetGuid = cue.assetGuid ?? string.Empty,
                enabled = cue.enabled,
                category = (int)cue.category,
                volume = cue.volume,
                loop = cue.loop,
                trigger = (int)cue.trigger,
                startBeatId = cue.startBeatId ?? string.Empty,
                startDelaySeconds = cue.startDelaySeconds,
                fadeInSeconds = cue.fadeInSeconds,
                fadeOutSeconds = cue.fadeOutSeconds,
                stopMode = (int)cue.stopMode,
                stopBeatId = cue.stopBeatId ?? string.Empty
            };
        }

        private static RokasVnRuntimeCharacterSnapshot MapCharacter(
            VnSceneComposerCharacter character)
        {
            return new RokasVnRuntimeCharacterSnapshot
            {
                characterId = character.characterId ?? string.Empty,
                stateId = character.stateId ?? string.Empty,
                stageSlot = (int)character.stageSlot,
                hasPositionOffset = character.hasPositionOffset,
                positionOffset = character.positionOffset,
                hasScaleMultiplier = character.hasScaleMultiplier,
                scaleMultiplier = character.scaleMultiplier
            };
        }

        private static RokasVnRuntimeBeatSnapshot MapBeat(VnSceneComposerDialogueBeat beat)
        {
            var mapped = new RokasVnRuntimeBeatSnapshot
            {
                beatId = beat.beatId ?? string.Empty,
                speaker = beat.speaker ?? string.Empty,
                text = beat.text ?? string.Empty,
                narration = beat.narration,
                targetCharacterId = beat.targetCharacterId ?? string.Empty,
                hasStateOverride = beat.hasStateOverride,
                stateId = beat.stateId ?? string.Empty,
                effect = (int)beat.effect,
                effectStrength = beat.effectStrength,
                effectDuration = beat.effectDuration,
                replicaEffect = MapReplicaEffect(beat.replicaEffect),
                movement = MapMovement(beat.movement)
            };

            if (beat.characterStaging != null)
            {
                for (int i = 0; i < beat.characterStaging.Count; i++)
                {
                    VnSceneComposerBeatCharacterStaging row = beat.characterStaging[i];
                    if (row != null) mapped.characterStaging.Add(MapStaging(row));
                }
            }
            return mapped;
        }

        private static RokasVnRuntimeReplicaEffectSnapshot MapReplicaEffect(
            VnSceneComposerReplicaEffect effect)
        {
            effect = effect ?? new VnSceneComposerReplicaEffect();
            return new RokasVnRuntimeReplicaEffectSnapshot
            {
                type = (int)effect.type,
                intensity = effect.intensity,
                duration = effect.duration,
                frequency = effect.frequency,
                decay = effect.decay,
                direction = effect.direction,
                flashColor = effect.flashColor
            };
        }

        private static RokasVnRuntimeMovementSnapshot MapMovement(
            VnSceneComposerBeatMovement movement)
        {
            movement = movement ?? new VnSceneComposerBeatMovement();
            return new RokasVnRuntimeMovementSnapshot
            {
                primary = MapMovementAction(movement.primary),
                secondary = MapMovementAction(movement.secondary),
                secondaryTiming = (int)movement.secondaryTiming
            };
        }

        private static RokasVnRuntimeMovementActionSnapshot MapMovementAction(
            VnSceneComposerCharacterMovementAction action)
        {
            action = action ?? new VnSceneComposerCharacterMovementAction();
            return new RokasVnRuntimeMovementActionSnapshot
            {
                characterId = action.characterId ?? string.Empty,
                action = (int)action.action,
                duration = action.duration
            };
        }

        private static RokasVnRuntimeStagingSnapshot MapStaging(
            VnSceneComposerBeatCharacterStaging row)
        {
            return new RokasVnRuntimeStagingSnapshot
            {
                stagingId = row.stagingId ?? string.Empty,
                characterId = row.characterId ?? string.Empty,
                visibility = (int)row.visibility,
                position = (int)row.position,
                customPositionOffset = row.customPositionOffset,
                hasStateOverride = row.hasStateOverride,
                stateId = row.stateId ?? string.Empty,
                effect = (int)row.effect,
                effectStrength = row.effectStrength,
                effectDuration = row.effectDuration,
                delaySeconds = row.delaySeconds
            };
        }

        private static HashSet<string> CollectReferencedAssetGuids(
            VnSceneComposerProject project,
            string portableJson)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in AssetGuidValue.Matches(portableJson ?? string.Empty))
                result.Add(match.Groups[1].Value.ToLowerInvariant());

            if (project.scenes != null)
            {
                for (int i = 0; i < project.scenes.Count; i++)
                {
                    VnSceneComposerScene scene = project.scenes[i];
                    if (scene == null || scene.media == null) continue;
                    if (scene.media.kind == VnSceneComposerMediaKind.ExistingRokasAsset &&
                        !string.IsNullOrWhiteSpace(scene.media.reference))
                        result.Add(scene.media.reference.ToLowerInvariant());
                }
            }
            return result;
        }

        private static void AddGuidBinding(
            string guid,
            string runtimeRoot,
            List<RokasVnRuntimeAssetBinding> bindings,
            HashSet<string> boundKeys)
        {
            if (string.IsNullOrWhiteSpace(guid) || !boundKeys.Add(guid)) return;
            string sourcePath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(sourcePath))
                throw new FileNotFoundException(
                    "Authored VN asset GUID is missing from the project: " + guid);

            string extension = Path.GetExtension(sourcePath);
            if (string.IsNullOrEmpty(extension)) extension = ".asset";
            string destination =
                runtimeRoot + "/Assets/" + guid.ToLowerInvariant() + extension.ToLowerInvariant();
            UnityEngine.Object copied = CopyAssetObject(sourcePath, destination);
            if (IsManagedComposerStill(sourcePath) &&
                (copied is Texture2D || copied is Sprite))
            {
                VnSceneComposerAssetLibrary
                    .ConfigureStillImporterForNativeQuality(
                        destination);
                copied =
                    AssetDatabase.LoadMainAssetAtPath(
                        destination);
            }
            bindings.Add(new RokasVnRuntimeAssetBinding
            {
                authoredKey = guid.ToLowerInvariant(),
                displayName = copied != null ? copied.name : Path.GetFileNameWithoutExtension(sourcePath),
                contentHash = AssetDatabase.GetAssetDependencyHash(sourcePath).ToString(),
                kind = Classify(copied),
                asset = copied
            });
        }

        private static void AddExternalMediaBindings(
            VnSceneComposerProject project,
            string runtimeRoot,
            List<RokasVnRuntimeAssetBinding> bindings,
            HashSet<string> boundKeys)
        {
            if (project.scenes == null) return;
            for (int i = 0; i < project.scenes.Count; i++)
            {
                VnSceneComposerScene scene = project.scenes[i];
                if (scene == null || scene.media == null) continue;
                if (scene.media.kind != VnSceneComposerMediaKind.ExternalImage &&
                    scene.media.kind != VnSceneComposerMediaKind.ExternalVideo &&
                    scene.media.kind != VnSceneComposerMediaKind.ExternalGif)
                    continue;

                string key = "scene-media:" + (scene.sceneId ?? string.Empty);
                if (!boundKeys.Add(key)) continue;
                string sourcePath = scene.media.reference ?? string.Empty;
                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                    throw new FileNotFoundException(
                        "External authored VN media is unavailable for runtime export: " +
                        (scene.media.displayName ?? scene.sceneId), sourcePath);

                string expectedHash = scene.media.contentHash ?? string.Empty;
                if (expectedHash.Length == 64)
                {
                    string actualHash = ComputeFileSha256(sourcePath);
                    if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                        throw new IOException(
                            "External authored VN media hash does not match: " + sourcePath);
                }

                string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
                string stable = expectedHash.Length == 64
                    ? expectedHash.ToLowerInvariant()
                    : ComputeFileSha256(sourcePath);
                string destination;
                RokasVnRuntimeAssetKind kind;
                UnityEngine.Object copied;

                if (scene.media.kind == VnSceneComposerMediaKind.ExternalGif)
                {
                    destination = runtimeRoot + "/External/" + stable + ".gif.bytes";
                    ReplaceFileAsset(sourcePath, destination);
                    copied = AssetDatabase.LoadAssetAtPath<TextAsset>(destination);
                    kind = RokasVnRuntimeAssetKind.Bytes;
                }
                else
                {
                    destination = runtimeRoot + "/External/" + stable + extension;
                    ReplaceFileAsset(sourcePath, destination);
                    if (scene.media.kind == VnSceneComposerMediaKind.ExternalImage)
                        VnSceneComposerAssetLibrary
                            .ConfigureStillImporterForNativeQuality(
                                destination);
                    copied = AssetDatabase.LoadMainAssetAtPath(destination);
                    kind = scene.media.kind == VnSceneComposerMediaKind.ExternalVideo
                        ? RokasVnRuntimeAssetKind.Video
                        : RokasVnRuntimeAssetKind.Texture;
                }

                if (copied == null)
                    throw new IOException(
                        "Exported external VN media could not be imported: " + destination);

                bindings.Add(new RokasVnRuntimeAssetBinding
                {
                    authoredKey = key,
                    displayName = scene.media.displayName ?? Path.GetFileName(sourcePath),
                    contentHash = stable,
                    kind = kind,
                    asset = copied
                });
            }
        }

        private static List<RokasVnRuntimeCharacterStateBinding> ExportCharacterStates(
            VnSceneComposerProject project,
            string runtimeRoot)
        {
            var stateIds = new HashSet<string>(StringComparer.Ordinal);
            if (project.scenes != null)
            {
                for (int s = 0; s < project.scenes.Count; s++)
                {
                    VnSceneComposerScene scene = project.scenes[s];
                    if (scene == null) continue;
                    if (scene.characters != null)
                    {
                        for (int i = 0; i < scene.characters.Count; i++)
                        {
                            VnSceneComposerCharacter character = scene.characters[i];
                            if (character != null && !string.IsNullOrWhiteSpace(character.stateId))
                                stateIds.Add(character.stateId);
                        }
                    }
                    if (scene.dialogueBeats == null) continue;
                    for (int b = 0; b < scene.dialogueBeats.Count; b++)
                    {
                        VnSceneComposerDialogueBeat beat = scene.dialogueBeats[b];
                        if (beat == null) continue;
                        if (beat.hasStateOverride && !string.IsNullOrWhiteSpace(beat.stateId))
                            stateIds.Add(beat.stateId);
                        if (beat.characterStaging == null) continue;
                        for (int r = 0; r < beat.characterStaging.Count; r++)
                        {
                            VnSceneComposerBeatCharacterStaging row = beat.characterStaging[r];
                            if (row != null && row.hasStateOverride &&
                                !string.IsNullOrWhiteSpace(row.stateId))
                                stateIds.Add(row.stateId);
                        }
                    }
                }
            }

            var result = new List<RokasVnRuntimeCharacterStateBinding>();
            foreach (string stateId in stateIds)
            {
                if (!VnSceneComposerCharacterStateResolver.TryResolve(
                        stateId, out VnSceneComposerResolvedCharacterState resolved) ||
                    resolved == null || resolved.Texture == null)
                    throw new FileNotFoundException(
                        "Authored VN character state could not be resolved for runtime export: " +
                        stateId);

                string sourcePath = AssetDatabase.GetAssetPath(resolved.Texture);
                if (string.IsNullOrEmpty(sourcePath))
                    throw new FileNotFoundException(
                        "Authored VN character texture has no project asset path: " + stateId);

                string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
                string safeId = Regex.Replace(stateId, @"[^A-Za-z0-9_.-]+", "_");
                string destination =
                    runtimeRoot + "/Characters/" + safeId + extension;
                Texture2D texture = CopyTexture(sourcePath, destination);
                result.Add(new RokasVnRuntimeCharacterStateBinding
                {
                    stateId = resolved.Id ?? stateId,
                    characterId = resolved.Character ?? string.Empty,
                    texture = texture,
                    bodyUv = resolved.BodyUv
                });
            }
            return result;
        }

        private static Texture2D CopyTexture(string sourcePath, string destinationPath)
        {
            UnityEngine.Object copied = CopyAssetObject(sourcePath, destinationPath);
            Texture2D texture = copied as Texture2D;
            if (texture == null)
            {
                Sprite sprite = copied as Sprite;
                if (sprite != null) texture = sprite.texture;
            }
            if (texture == null)
                throw new IOException(
                    "Runtime VN texture copy is not a Texture2D/Sprite: " + sourcePath);
            return texture;
        }

        private static UnityEngine.Object CopyAssetObject(
            string sourcePath,
            string destinationPath)
        {
            sourcePath = NormalizeAssetPath(sourcePath);
            destinationPath = NormalizeAssetPath(destinationPath);
            if (string.IsNullOrEmpty(sourcePath) ||
                AssetDatabase.LoadMainAssetAtPath(sourcePath) == null)
                throw new FileNotFoundException("VN export source asset is missing: " + sourcePath);

            EnsureAssetFolder(
                NormalizeAssetPath(Path.GetDirectoryName(destinationPath)));
            if (AssetDatabase.LoadMainAssetAtPath(destinationPath) != null ||
                File.Exists(destinationPath))
                AssetDatabase.DeleteAsset(destinationPath);

            if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
                throw new IOException(
                    "Could not copy VN asset into runtime package: " +
                    sourcePath + " -> " + destinationPath);
            AssetDatabase.ImportAsset(
                destinationPath, ImportAssetOptions.ForceSynchronousImport);
            UnityEngine.Object copied =
                AssetDatabase.LoadMainAssetAtPath(destinationPath);
            if (copied == null)
                throw new IOException(
                    "Copied VN runtime asset could not be loaded: " + destinationPath);
            return copied;
        }

        private static bool IsManagedComposerStill(
            string assetPath)
        {
            string normalized =
                NormalizeAssetPath(assetPath);
            string managedRoot =
                NormalizeAssetPath(
                    VnSceneComposerAssetLibrary
                        .ManagedRootRelative);
            if (!normalized.StartsWith(
                    managedRoot + "/",
                    StringComparison.OrdinalIgnoreCase))
                return false;

            string extension =
                Path.GetExtension(normalized)
                    .ToLowerInvariant();
            return extension == ".png" ||
                   extension == ".jpg" ||
                   extension == ".jpeg";
        }

        private static void ReplaceFileAsset(
            string sourcePath,
            string destinationPath)
        {
            string absoluteProjectRoot =
                Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string absoluteDestination = Path.GetFullPath(
                Path.Combine(absoluteProjectRoot, destinationPath));
            string directory = Path.GetDirectoryName(absoluteDestination);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            File.Copy(sourcePath, absoluteDestination, true);
            AssetDatabase.ImportAsset(
                destinationPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static RokasVnRuntimeAssetKind Classify(UnityEngine.Object asset)
        {
            if (asset is Texture2D || asset is Sprite)
                return RokasVnRuntimeAssetKind.Texture;
            if (asset is AudioClip)
                return RokasVnRuntimeAssetKind.Audio;
            if (asset is UnityEngine.Video.VideoClip)
                return RokasVnRuntimeAssetKind.Video;
            if (asset is TextAsset)
                return RokasVnRuntimeAssetKind.Bytes;
            if (asset is Font ||
                (asset != null &&
                 string.Equals(asset.GetType().Name, "TMP_FontAsset", StringComparison.Ordinal)))
                return RokasVnRuntimeAssetKind.Font;
            return RokasVnRuntimeAssetKind.Unknown;
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            assetPath = NormalizeAssetPath(assetPath).TrimEnd('/');
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            if (!assetPath.StartsWith("Assets", StringComparison.Ordinal))
                throw new ArgumentException(
                    "Unity asset folder must be under Assets/: " + assetPath);

            string[] parts = assetPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void RequireRuntimeAssetPath(string runtimeRoot)
        {
            if (!runtimeRoot.StartsWith("Assets/", StringComparison.Ordinal) ||
                runtimeRoot.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                runtimeRoot.EndsWith("/Editor", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    "Runtime VN export root must be a build-safe Assets path.",
                    nameof(runtimeRoot));
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        }

        private static string ComputeFileSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BytesToHex(sha.ComputeHash(stream));
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return BytesToHex(sha.ComputeHash(bytes));
        }

        private static string BytesToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                builder.Append(bytes[i].ToString("x2"));
            return builder.ToString();
        }

    }
}
