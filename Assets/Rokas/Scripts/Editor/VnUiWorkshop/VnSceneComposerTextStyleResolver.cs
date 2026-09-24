using System;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public static class VnSceneComposerTextStyleResolver
    {
        public static VnWorkshopTypographyValues Resolve(
            VnSceneComposerProject project, VnSceneComposerScene scene, VnSceneComposerDialogueBeat beat)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (scene == null) throw new ArgumentNullException(nameof(scene));

            VnWorkshopTypographyValues values = ResolveLegacyFallback(project, scene, beat);
            ApplyProjectSpeakerProfile(
                FindProjectSpeakerProfile(project, ResolveSpeakerKey(scene, beat)),
                ref values);
            return values;
        }

        // Legacy Scene/default layers remain as compatibility fallback only.
        // ProjectSpeakerPalette is applied after historical resolution.
        internal static VnWorkshopTypographyValues ResolveLegacyFallback(
            VnSceneComposerProject project, VnSceneComposerScene scene, VnSceneComposerDialogueBeat beat)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (scene == null) throw new ArgumentNullException(nameof(scene));

            VnWorkshopTypographyValues values =
                VnPresentationWorkshopVn10Resolver.ResolveTypography(
                    project.defaultPresentation ?? new VnPresentationWorkshopPreset());

            float sharedDialogueFontSize = values.DialogueFontSize;
            VnWorkshopTypographyOverride legacy =
                scene.presentationOverrides != null
                    ? scene.presentationOverrides.typography
                    : null;

            ApplyLegacyDialogue(legacy, ref values);
            ApplyVisual(scene.dialogueBodyStyleOverride, ref values);
            values.DialogueFontSize = sharedDialogueFontSize;

            VnSceneComposerSpeakerColorOverride speakerOverride =
                FindSpeakerColorOverride(scene, ResolveSpeakerKey(scene, beat));
            if (speakerOverride != null)
            {
                Color localSpeaker = speakerOverride.color;
                localSpeaker.a = values.SpeakerColor.a;
                values.SpeakerColor = localSpeaker;

                if (speakerOverride.hasDialogueBodyColor)
                {
                    Color localBody = speakerOverride.dialogueBodyColor;
                    localBody.a = values.DialogueColor.a;
                    values.DialogueColor = localBody;
                }
            }
            else if (scene.hasSpeakerColorOverride ||
                     scene.speakerColorScope == VnSceneComposerSpeakerColorScope.ThisScene)
            {
                Color localScene = scene.speakerColor;
                localScene.a = values.SpeakerColor.a;
                values.SpeakerColor = localScene;
            }

            return values;
        }

        public static string ResolveSpeakerKey(VnSceneComposerDialogueBeat beat)
        {
            // Source-compatible legacy helper. New Scene-aware resolution below is authoritative
            // because only a character identity that actually belongs to the Scene may outrank
            // the authored speaker text.
            if (beat == null || beat.narration) return string.Empty;

            string characterId = (beat.targetCharacterId ?? string.Empty).Trim();
            if (characterId.Length > 0)
                return "character:" + characterId;

            string speaker = NormalizeSpeakerText(beat.speaker);
            return speaker.Length == 0 ? string.Empty : "speaker:" + speaker;
        }

        public static string ResolveSpeakerKey(
            VnSceneComposerScene scene, VnSceneComposerDialogueBeat beat)
        {
            if (beat == null || beat.narration) return string.Empty;

            string authoredCharacterId = (beat.targetCharacterId ?? string.Empty).Trim();
            if (authoredCharacterId.Length > 0 &&
                TryResolveLegitimateCharacterIdentity(scene, authoredCharacterId, out string stableCharacterId))
                return "character:" + stableCharacterId;

            string speaker = NormalizeSpeakerText(beat.speaker);
            return speaker.Length == 0 ? string.Empty : "speaker:" + speaker;
        }

        public static string ResolveSpeakerKeyForName(string value)
        {
            string speaker = NormalizeSpeakerText(value);
            return speaker.Length == 0 ? string.Empty : "speaker:" + speaker;
        }

        private static string NormalizeSpeakerText(string value)
        {
            // Deterministic and deliberately conservative: trim authoring whitespace only.
            // Do not case-fold or otherwise merge genuinely different authored names.
            return (value ?? string.Empty).Trim();
        }

        private static bool TryResolveLegitimateCharacterIdentity(
            VnSceneComposerScene scene, string authoredCharacterId, out string stableCharacterId)
        {
            stableCharacterId = string.Empty;
            if (scene == null || scene.characters == null ||
                string.IsNullOrWhiteSpace(authoredCharacterId))
                return false;

            string candidate = authoredCharacterId.Trim();
            for (int i = 0; i < scene.characters.Count; i++)
            {
                VnSceneComposerCharacter character = scene.characters[i];
                if (character == null) continue;
                string existingId = VnSceneComposerBeatCharacterStateResolver.ResolveCharacterId(character);
                if (!string.IsNullOrWhiteSpace(existingId) &&
                    string.Equals(existingId, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    stableCharacterId = existingId.Trim();
                    return true;
                }
            }

            return false;
        }

        public static VnSceneComposerProjectSpeakerProfile FindProjectSpeakerProfile(
            VnSceneComposerProject project, string speakerKey)
        {
            if (project == null || project.projectSpeakerPalette == null ||
                string.IsNullOrEmpty(speakerKey))
                return null;

            for (int i = 0; i < project.projectSpeakerPalette.Count; i++)
            {
                VnSceneComposerProjectSpeakerProfile profile = project.projectSpeakerPalette[i];
                if (profile != null &&
                    string.Equals(profile.speakerKey ?? string.Empty, speakerKey, StringComparison.Ordinal))
                    return profile;
            }
            return null;
        }

        public static VnWorkshopTypographyValues ResolveProjectSpeakerValues(
            VnSceneComposerProject project, string speakerKey)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            VnWorkshopTypographyValues values = ResolveProjectSpeakerFallback(project, speakerKey);
            ApplyProjectSpeakerProfile(FindProjectSpeakerProfile(project, speakerKey), ref values);
            return values;
        }

        public static void SetProjectSpeakerNameColor(
            VnSceneComposerProject project, string speakerKey, Color color)
        {
            VnSceneComposerProjectSpeakerProfile profile =
                EnsureProjectSpeakerProfile(project, speakerKey);
            color.a = 1f;
            profile.speakerNameColor = color;
            profile.hasSpeakerNameColor = true;
        }

        public static void SetProjectSpeakerDialogueBodyColor(
            VnSceneComposerProject project, string speakerKey, Color color)
        {
            VnSceneComposerProjectSpeakerProfile profile =
                EnsureProjectSpeakerProfile(project, speakerKey);
            color.a = 1f;
            profile.dialogueBodyColor = color;
            profile.hasDialogueBodyColor = true;
        }

        public static bool ClearProjectSpeakerNameColor(
            VnSceneComposerProject project, string speakerKey)
        {
            VnSceneComposerProjectSpeakerProfile profile =
                FindProjectSpeakerProfile(project, speakerKey);
            if (profile == null || !profile.hasSpeakerNameColor) return false;
            profile.hasSpeakerNameColor = false;
            RemoveEmptyProjectSpeakerProfile(project, profile);
            return true;
        }

        public static bool ClearProjectSpeakerDialogueBodyColor(
            VnSceneComposerProject project, string speakerKey)
        {
            VnSceneComposerProjectSpeakerProfile profile =
                FindProjectSpeakerProfile(project, speakerKey);
            if (profile == null || !profile.hasDialogueBodyColor) return false;
            profile.hasDialogueBodyColor = false;
            RemoveEmptyProjectSpeakerProfile(project, profile);
            return true;
        }

        private static VnSceneComposerProjectSpeakerProfile EnsureProjectSpeakerProfile(
            VnSceneComposerProject project, string speakerKey)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            speakerKey = (speakerKey ?? string.Empty).Trim();
            if (speakerKey.Length == 0)
                throw new InvalidOperationException(
                    "Project speaker palette requires a non-empty speaker identity.");

            if (project.projectSpeakerPalette == null)
                project.projectSpeakerPalette =
                    new System.Collections.Generic.List<VnSceneComposerProjectSpeakerProfile>();

            VnSceneComposerProjectSpeakerProfile existing =
                FindProjectSpeakerProfile(project, speakerKey);
            if (existing != null) return existing;

            VnWorkshopTypographyValues seed =
                ResolveProjectSpeakerFallback(project, speakerKey);
            Color name = seed.SpeakerColor;
            Color body = seed.DialogueColor;
            name.a = 1f;
            body.a = 1f;
            var profile = new VnSceneComposerProjectSpeakerProfile
            {
                speakerKey = speakerKey,
                hasSpeakerNameColor = false,
                speakerNameColor = name,
                hasDialogueBodyColor = false,
                dialogueBodyColor = body
            };
            project.projectSpeakerPalette.Add(profile);
            return profile;
        }

        private static void RemoveEmptyProjectSpeakerProfile(
            VnSceneComposerProject project, VnSceneComposerProjectSpeakerProfile profile)
        {
            if (project == null || project.projectSpeakerPalette == null || profile == null ||
                profile.hasSpeakerNameColor || profile.hasDialogueBodyColor)
                return;
            project.projectSpeakerPalette.Remove(profile);
        }

        private static VnWorkshopTypographyValues ResolveProjectSpeakerFallback(
            VnSceneComposerProject project, string speakerKey)
        {
            if (TryFindProjectSpeakerBeat(project, speakerKey,
                    out VnSceneComposerScene scene, out VnSceneComposerDialogueBeat beat))
                return ResolveLegacyFallback(project, scene, beat);

            return VnPresentationWorkshopVn10Resolver.ResolveTypography(
                project.defaultPresentation ?? new VnPresentationWorkshopPreset());
        }

        private static bool TryFindProjectSpeakerBeat(
            VnSceneComposerProject project,
            string speakerKey,
            out VnSceneComposerScene matchedScene,
            out VnSceneComposerDialogueBeat matchedBeat)
        {
            matchedScene = null;
            matchedBeat = null;
            if (project == null || project.scenes == null || string.IsNullOrEmpty(speakerKey))
                return false;

            for (int s = 0; s < project.scenes.Count; s++)
            {
                VnSceneComposerScene scene = project.scenes[s];
                if (scene == null || scene.dialogueBeats == null) continue;
                for (int b = 0; b < scene.dialogueBeats.Count; b++)
                {
                    VnSceneComposerDialogueBeat beat = scene.dialogueBeats[b];
                    if (beat != null &&
                        string.Equals(ResolveSpeakerKey(scene, beat), speakerKey, StringComparison.Ordinal))
                    {
                        matchedScene = scene;
                        matchedBeat = beat;
                        return true;
                    }
                }
            }
            return false;
        }

        private static void ApplyProjectSpeakerProfile(
            VnSceneComposerProjectSpeakerProfile profile,
            ref VnWorkshopTypographyValues values)
        {
            if (profile == null) return;
            if (profile.hasSpeakerNameColor)
            {
                Color name = profile.speakerNameColor;
                name.a = values.SpeakerColor.a;
                values.SpeakerColor = name;
            }
            if (profile.hasDialogueBodyColor)
            {
                Color body = profile.dialogueBodyColor;
                body.a = values.DialogueColor.a;
                values.DialogueColor = body;
            }
        }

        public static VnSceneComposerSpeakerColorOverride FindSpeakerColorOverride(
            VnSceneComposerScene scene, string speakerKey)
        {
            if (scene == null || scene.speakerColorOverrides == null ||
                string.IsNullOrEmpty(speakerKey))
                return null;

            for (int i = 0; i < scene.speakerColorOverrides.Count; i++)
            {
                VnSceneComposerSpeakerColorOverride entry = scene.speakerColorOverrides[i];
                if (entry != null &&
                    string.Equals(entry.speakerKey ?? string.Empty, speakerKey, StringComparison.Ordinal))
                    return entry;
            }
            return null;
        }

        public static bool HasSpeakerColorOverride(
            VnSceneComposerScene scene, VnSceneComposerDialogueBeat beat)
        {
            return FindSpeakerColorOverride(scene, ResolveSpeakerKey(scene, beat)) != null;
        }

        internal static void SetSpeakerColorOverride(
            VnSceneComposerScene scene, VnSceneComposerDialogueBeat beat, Color color)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            string key = ResolveSpeakerKey(scene, beat);
            if (key.Length == 0)
                throw new InvalidOperationException(
                    "Speaker-in-Scene color requires a non-empty speaker identity.");

            if (scene.speakerColorOverrides == null)
                scene.speakerColorOverrides = new System.Collections.Generic.List<VnSceneComposerSpeakerColorOverride>();

            VnSceneComposerSpeakerColorOverride entry = FindSpeakerColorOverride(scene, key);
            if (entry == null)
            {
                entry = new VnSceneComposerSpeakerColorOverride { speakerKey = key };
                scene.speakerColorOverrides.Add(entry);
            }
            entry.color = color;
        }

        internal static VnSceneComposerSpeakerColorOverride EnsureSpeakerPaletteEntry(
            VnSceneComposerProject project,
            VnSceneComposerScene scene,
            VnSceneComposerDialogueBeat beat)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (scene == null) throw new ArgumentNullException(nameof(scene));

            string key = ResolveSpeakerKey(scene, beat);
            if (key.Length == 0)
                throw new InvalidOperationException(
                    "Scene speaker palette requires a non-empty current speaker.");

            if (scene.speakerColorOverrides == null)
                scene.speakerColorOverrides =
                    new System.Collections.Generic.List<VnSceneComposerSpeakerColorOverride>();

            VnSceneComposerSpeakerColorOverride entry = FindSpeakerColorOverride(scene, key);
            if (entry != null)
                return entry;

            VnWorkshopTypographyValues seed = ResolveWithoutSpeakerPalette(project, scene, beat);
            entry = new VnSceneComposerSpeakerColorOverride
            {
                speakerKey = key,
                color = seed.SpeakerColor,
                // Name-only authoring must not silently freeze the body fallback.
                // The body override becomes explicit only when its own color is edited.
                hasDialogueBodyColor = false,
                dialogueBodyColor = seed.DialogueColor
            };
            scene.speakerColorOverrides.Add(entry);
            return entry;
        }

        internal static void SetSpeakerPaletteNameColor(
            VnSceneComposerProject project,
            VnSceneComposerScene scene,
            VnSceneComposerDialogueBeat beat,
            Color color)
        {
            VnSceneComposerSpeakerColorOverride entry =
                EnsureSpeakerPaletteEntry(project, scene, beat);
            VnWorkshopTypographyValues inherited = ResolveWithoutSpeakerPalette(project, scene, beat);
            color.a = inherited.SpeakerColor.a;
            entry.color = color;
        }

        internal static void SetSpeakerPaletteDialogueBodyColor(
            VnSceneComposerProject project,
            VnSceneComposerScene scene,
            VnSceneComposerDialogueBeat beat,
            Color color)
        {
            VnSceneComposerSpeakerColorOverride entry =
                EnsureSpeakerPaletteEntry(project, scene, beat);
            VnWorkshopTypographyValues inherited = ResolveWithoutSpeakerPalette(project, scene, beat);
            color.a = inherited.DialogueColor.a;
            entry.dialogueBodyColor = color;
            entry.hasDialogueBodyColor = true;
        }

        internal static bool ClearSpeakerPaletteDialogueBodyColor(
            VnSceneComposerScene scene, VnSceneComposerDialogueBeat beat)
        {
            if (scene == null || scene.speakerColorOverrides == null) return false;
            string key = ResolveSpeakerKey(scene, beat);
            if (key.Length == 0) return false;

            VnSceneComposerSpeakerColorOverride entry =
                FindSpeakerColorOverride(scene, key);
            if (entry == null || !entry.hasDialogueBodyColor) return false;

            entry.hasDialogueBodyColor = false;
            return true;
        }

        internal static bool RemoveSpeakerPaletteEntry(
            VnSceneComposerScene scene, VnSceneComposerDialogueBeat beat)
        {
            if (scene == null || scene.speakerColorOverrides == null) return false;
            string key = ResolveSpeakerKey(scene, beat);
            if (key.Length == 0) return false;

            for (int i = scene.speakerColorOverrides.Count - 1; i >= 0; i--)
            {
                VnSceneComposerSpeakerColorOverride entry = scene.speakerColorOverrides[i];
                if (entry != null &&
                    string.Equals(entry.speakerKey ?? string.Empty, key, StringComparison.Ordinal))
                {
                    scene.speakerColorOverrides.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        private static VnWorkshopTypographyValues ResolveWithoutSpeakerPalette(
            VnSceneComposerProject project,
            VnSceneComposerScene scene,
            VnSceneComposerDialogueBeat beat)
        {
            VnWorkshopTypographyValues values =
                VnPresentationWorkshopVn10Resolver.ResolveTypography(
                    project.defaultPresentation ?? new VnPresentationWorkshopPreset());

            float sharedDialogueFontSize = values.DialogueFontSize;
            VnWorkshopTypographyOverride legacy =
                scene.presentationOverrides != null
                    ? scene.presentationOverrides.typography
                    : null;
            ApplyLegacyDialogue(legacy, ref values);
            ApplyVisual(scene.dialogueBodyStyleOverride, ref values);
            values.DialogueFontSize = sharedDialogueFontSize;

            if (scene.hasSpeakerColorOverride ||
                scene.speakerColorScope == VnSceneComposerSpeakerColorScope.ThisScene)
            {
                Color localScene = scene.speakerColor;
                localScene.a = values.SpeakerColor.a;
                values.SpeakerColor = localScene;
            }

            return values;
        }

        internal static bool RemoveSpeakerColorOverride(
            VnSceneComposerScene scene, VnSceneComposerDialogueBeat beat)
        {
            if (scene == null || scene.speakerColorOverrides == null) return false;
            string key = ResolveSpeakerKey(scene, beat);
            if (key.Length == 0) return false;

            for (int i = scene.speakerColorOverrides.Count - 1; i >= 0; i--)
            {
                VnSceneComposerSpeakerColorOverride entry = scene.speakerColorOverrides[i];
                if (entry != null &&
                    string.Equals(entry.speakerKey ?? string.Empty, key, StringComparison.Ordinal))
                {
                    scene.speakerColorOverrides.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        internal static void ApplyResolvedTypography(
            VnSceneComposerProject project,
            VnSceneComposerScene scene,
            VnSceneComposerDialogueBeat beat,
            VnPresentationWorkshopPreset preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            VnWorkshopTypographyValues v = Resolve(project, scene, beat);
            preset.typography = new VnWorkshopTypographyOverride
            {
                hasDialogueFontPreset = true,
                dialogueFontPreset = v.DialogueFontPreset,
                hasDialogueFontAssetGuid = true,
                dialogueFontAssetGuid = v.DialogueFontAssetGuid ?? string.Empty,
                hasDialogueColor = true,
                dialogueColor = v.DialogueColor,
                hasDialogueFontSize = true,
                dialogueFontSize = v.DialogueFontSize,
                hasDialogueCharacterSpacing = true,
                dialogueCharacterSpacing = v.DialogueCharacterSpacing,
                hasDialogueLineSpacing = true,
                dialogueLineSpacing = v.DialogueLineSpacing,
                hasDialogueParagraphSpacing = true,
                dialogueParagraphSpacing = v.DialogueParagraphSpacing,
                hasDialogueAlignment = true,
                dialogueAlignment = v.DialogueAlignment,
                hasSpeakerFontPreset = true,
                speakerFontPreset = v.SpeakerFontPreset,
                hasSpeakerFontAssetGuid = true,
                speakerFontAssetGuid = v.SpeakerFontAssetGuid ?? string.Empty,
                hasSpeakerColor = true,
                speakerColor = v.SpeakerColor,
                hasSpeakerAlignment = true,
                speakerAlignment = v.SpeakerAlignment,
                hasSpeakerFontSize = true,
                speakerFontSize = v.SpeakerFontSize,
                hasSpeakerCharacterSpacing = true,
                speakerCharacterSpacing = v.SpeakerCharacterSpacing
            };
        }

        internal static void SetStyle(
            VnSceneComposerTextVisualStyleOverride style,
            string fontAssetGuid,
            float fontSize,
            Color color,
            VnWorkshopTextAlignment alignment)
        {
            if (style == null) throw new ArgumentNullException(nameof(style));
            style.hasFontAssetGuid = true;
            style.fontAssetGuid = fontAssetGuid ?? string.Empty;
            // Font size is sequence-global; Scene-local body style never forks it.
            style.hasFontSize = false;
            style.fontSize = 0f;
            style.hasColor = true;
            style.color = color;
            style.hasAlignment = true;
            style.alignment = alignment;
        }

        private static void ApplyVisual(
            VnSceneComposerTextVisualStyleOverride source,
            ref VnWorkshopTypographyValues values)
        {
            if (source == null) return;
            if (source.hasFontPreset) values.DialogueFontPreset = source.fontPreset;
            if (source.hasFontAssetGuid) values.DialogueFontAssetGuid = source.fontAssetGuid ?? string.Empty;
            if (source.hasFontSize) values.DialogueFontSize = source.fontSize;
            if (source.hasColor) values.DialogueColor = source.color;
            if (source.hasAlignment) values.DialogueAlignment = source.alignment;
            if (source.hasCharacterSpacing) values.DialogueCharacterSpacing = source.characterSpacing;
            if (source.hasLineSpacing) values.DialogueLineSpacing = source.lineSpacing;
            if (source.hasParagraphSpacing) values.DialogueParagraphSpacing = source.paragraphSpacing;
        }

        private static void ApplyLegacyDialogue(
            VnWorkshopTypographyOverride source,
            ref VnWorkshopTypographyValues values)
        {
            if (source == null) return;
            if (source.hasDialogueFontPreset) values.DialogueFontPreset = source.dialogueFontPreset;
            if (source.hasDialogueFontAssetGuid)
                values.DialogueFontAssetGuid = source.dialogueFontAssetGuid ?? string.Empty;
            if (source.hasDialogueColor) values.DialogueColor = source.dialogueColor;
            if (source.hasDialogueFontSize) values.DialogueFontSize = source.dialogueFontSize;
            if (source.hasDialogueCharacterSpacing)
                values.DialogueCharacterSpacing = source.dialogueCharacterSpacing;
            if (source.hasDialogueLineSpacing)
                values.DialogueLineSpacing = source.dialogueLineSpacing;
            if (source.hasDialogueParagraphSpacing)
                values.DialogueParagraphSpacing = source.dialogueParagraphSpacing;
            if (source.hasDialogueAlignment) values.DialogueAlignment = source.dialogueAlignment;
        }
    }
}
