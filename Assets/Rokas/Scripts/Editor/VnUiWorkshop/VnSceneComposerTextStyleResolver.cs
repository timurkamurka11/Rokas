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

            if (scene.speakerColorScope == VnSceneComposerSpeakerColorScope.ThisScene)
            {
                Color local = scene.speakerColor;
                // Speaker opacity remains shared/global. Scene-local color contributes RGB only.
                local.a = values.SpeakerColor.a;
                values.SpeakerColor = local;
            }

            return values;
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
