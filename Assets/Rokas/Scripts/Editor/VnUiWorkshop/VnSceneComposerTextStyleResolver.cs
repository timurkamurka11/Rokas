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

            VnWorkshopTypographyValues values =
                VnPresentationWorkshopVn10Resolver.ResolveTypography(
                    project.defaultPresentation ?? new VnPresentationWorkshopPreset());

            VnWorkshopTypographyOverride legacy =
                scene.presentationOverrides != null
                    ? scene.presentationOverrides.typography
                    : null;

            ApplyLegacyDialogue(legacy, ref values);
            ApplyVisual(scene.dialogueBodyStyleOverride, ref values);

            VnSceneComposerSpeakerColorOverride speakerOverride =
                FindSpeakerColorOverride(scene, ResolveSpeakerKey(beat));
            if (speakerOverride != null)
            {
                Color localSpeaker = speakerOverride.color;
                // Opacity remains shared/global. Local authorities contribute RGB only.
                localSpeaker.a = values.SpeakerColor.a;
                values.SpeakerColor = localSpeaker;
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
            if (beat == null || beat.narration) return string.Empty;

            string characterId = (beat.targetCharacterId ?? string.Empty).Trim();
            if (characterId.Length > 0)
                return "character:" + characterId;

            string speaker = (beat.speaker ?? string.Empty).Trim();
            return speaker.Length == 0 ? string.Empty : "speaker:" + speaker;
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
            return FindSpeakerColorOverride(scene, ResolveSpeakerKey(beat)) != null;
        }

        internal static void SetSpeakerColorOverride(
            VnSceneComposerScene scene, VnSceneComposerDialogueBeat beat, Color color)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            string key = ResolveSpeakerKey(beat);
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

        internal static bool RemoveSpeakerColorOverride(
            VnSceneComposerScene scene, VnSceneComposerDialogueBeat beat)
        {
            if (scene == null || scene.speakerColorOverrides == null) return false;
            string key = ResolveSpeakerKey(beat);
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
            style.hasFontSize = true;
            style.fontSize = fontSize;
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
