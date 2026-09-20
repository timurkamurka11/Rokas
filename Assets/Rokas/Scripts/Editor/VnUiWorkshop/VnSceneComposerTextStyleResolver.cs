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
            VnWorkshopTypographyValues v = VnPresentationWorkshopVn10Resolver.ResolveTypography(
                project.defaultPresentation ?? new VnPresentationWorkshopPreset());
            VnWorkshopTypographyOverride legacy =
                scene.presentationOverrides != null ? scene.presentationOverrides.typography : null;
            ApplyLegacyDialogue(legacy, ref v);
            ApplyVisual(scene.dialogueBodyStyleOverride, false, ref v);
            string id = ResolveSpeakerCharacterId(scene, beat);
            VnSceneComposerSpeakerStyleOverride entry = FindSpeakerStyle(project, id);
            if (entry != null && entry.style != null && entry.style.hasColor)
            {
                Color characterColor = entry.style.color;
                // Opacity belongs to the shared/default speaker style. A character override
                // contributes RGB only.
                characterColor.a = v.SpeakerColor.a;
                v.SpeakerColor = characterColor;
            }
            return v;
        }

        public static string ResolveSpeakerCharacterId(
            VnSceneComposerScene scene, VnSceneComposerDialogueBeat beat)
        {
            if (scene == null || beat == null || beat.narration) return string.Empty;
            string id = CanonicalCharacterId(scene, beat.targetCharacterId);
            if (!string.IsNullOrEmpty(id)) return id;
            return CanonicalCharacterId(scene, beat.speaker);
        }

        public static VnSceneComposerSpeakerStyleOverride FindSpeakerStyle(
            VnSceneComposerProject project, string characterId)
        {
            if (project == null || project.speakerStyleOverrides == null ||
                string.IsNullOrWhiteSpace(characterId)) return null;
            for (int i = 0; i < project.speakerStyleOverrides.Count; i++)
            {
                VnSceneComposerSpeakerStyleOverride e = project.speakerStyleOverrides[i];
                if (e != null && string.Equals(e.characterId ?? string.Empty, characterId,
                    StringComparison.OrdinalIgnoreCase)) return e;
            }
            return null;
        }

        internal static void ApplyResolvedTypography(
            VnSceneComposerProject project, VnSceneComposerScene scene,
            VnSceneComposerDialogueBeat beat, VnPresentationWorkshopPreset preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            VnWorkshopTypographyValues v = Resolve(project, scene, beat);
            preset.typography = new VnWorkshopTypographyOverride
            {
                hasDialogueFontPreset=true, dialogueFontPreset=v.DialogueFontPreset,
                hasDialogueFontAssetGuid=true, dialogueFontAssetGuid=v.DialogueFontAssetGuid ?? string.Empty,
                hasDialogueColor=true, dialogueColor=v.DialogueColor,
                hasDialogueFontSize=true, dialogueFontSize=v.DialogueFontSize,
                hasDialogueCharacterSpacing=true, dialogueCharacterSpacing=v.DialogueCharacterSpacing,
                hasDialogueLineSpacing=true, dialogueLineSpacing=v.DialogueLineSpacing,
                hasDialogueParagraphSpacing=true, dialogueParagraphSpacing=v.DialogueParagraphSpacing,
                hasDialogueAlignment=true, dialogueAlignment=v.DialogueAlignment,
                hasSpeakerFontPreset=true, speakerFontPreset=v.SpeakerFontPreset,
                hasSpeakerFontAssetGuid=true, speakerFontAssetGuid=v.SpeakerFontAssetGuid ?? string.Empty,
                hasSpeakerColor=true, speakerColor=v.SpeakerColor,
                hasSpeakerAlignment=true, speakerAlignment=v.SpeakerAlignment,
                hasSpeakerFontSize=true, speakerFontSize=v.SpeakerFontSize,
                hasSpeakerCharacterSpacing=true, speakerCharacterSpacing=v.SpeakerCharacterSpacing
            };
        }

        public static bool HasSpeakerColorOverride(
            VnSceneComposerProject project, string characterId)
        {
            VnSceneComposerSpeakerStyleOverride entry = FindSpeakerStyle(project, characterId);
            return entry != null && entry.style != null && entry.style.hasColor;
        }

        internal static void SetSpeakerColor(
            VnSceneComposerTextVisualStyleOverride style, Color color)
        {
            if (style == null) throw new ArgumentNullException(nameof(style));
            style.hasColor = true;
            style.color = color;
        }

        internal static void SetStyle(VnSceneComposerTextVisualStyleOverride s, string guid,
            float size, Color color, VnWorkshopTextAlignment alignment)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            s.hasFontAssetGuid=true; s.fontAssetGuid=guid ?? string.Empty;
            s.hasFontSize=true; s.fontSize=size;
            s.hasColor=true; s.color=color;
            s.hasAlignment=true; s.alignment=alignment;
        }

        private static void ApplyVisual(VnSceneComposerTextVisualStyleOverride s, bool speaker,
            ref VnWorkshopTypographyValues v)
        {
            if (s == null) return;
            if (speaker)
            {
                if(s.hasFontPreset)v.SpeakerFontPreset=s.fontPreset;
                if(s.hasFontAssetGuid)v.SpeakerFontAssetGuid=s.fontAssetGuid ?? string.Empty;
                if(s.hasFontSize)v.SpeakerFontSize=s.fontSize;
                if(s.hasColor)v.SpeakerColor=s.color;
                if(s.hasAlignment)v.SpeakerAlignment=s.alignment;
                if(s.hasCharacterSpacing)v.SpeakerCharacterSpacing=s.characterSpacing;
                return;
            }
            if(s.hasFontPreset)v.DialogueFontPreset=s.fontPreset;
            if(s.hasFontAssetGuid)v.DialogueFontAssetGuid=s.fontAssetGuid ?? string.Empty;
            if(s.hasFontSize)v.DialogueFontSize=s.fontSize;
            if(s.hasColor)v.DialogueColor=s.color;
            if(s.hasAlignment)v.DialogueAlignment=s.alignment;
            if(s.hasCharacterSpacing)v.DialogueCharacterSpacing=s.characterSpacing;
            if(s.hasLineSpacing)v.DialogueLineSpacing=s.lineSpacing;
            if(s.hasParagraphSpacing)v.DialogueParagraphSpacing=s.paragraphSpacing;
        }

        private static void ApplyLegacyDialogue(VnWorkshopTypographyOverride s, ref VnWorkshopTypographyValues v)
        {
            if(s==null)return;
            if(s.hasDialogueFontPreset)v.DialogueFontPreset=s.dialogueFontPreset;
            if(s.hasDialogueFontAssetGuid)v.DialogueFontAssetGuid=s.dialogueFontAssetGuid ?? string.Empty;
            if(s.hasDialogueColor)v.DialogueColor=s.dialogueColor;
            if(s.hasDialogueFontSize)v.DialogueFontSize=s.dialogueFontSize;
            if(s.hasDialogueCharacterSpacing)v.DialogueCharacterSpacing=s.dialogueCharacterSpacing;
            if(s.hasDialogueLineSpacing)v.DialogueLineSpacing=s.dialogueLineSpacing;
            if(s.hasDialogueParagraphSpacing)v.DialogueParagraphSpacing=s.dialogueParagraphSpacing;
            if(s.hasDialogueAlignment)v.DialogueAlignment=s.dialogueAlignment;
        }

        private static void ApplyLegacySpeaker(VnWorkshopTypographyOverride s, ref VnWorkshopTypographyValues v)
        {
            if(s==null)return;
            if(s.hasSpeakerFontPreset)v.SpeakerFontPreset=s.speakerFontPreset;
            if(s.hasSpeakerFontAssetGuid)v.SpeakerFontAssetGuid=s.speakerFontAssetGuid ?? string.Empty;
            if(s.hasSpeakerColor)v.SpeakerColor=s.speakerColor;
            if(s.hasSpeakerAlignment)v.SpeakerAlignment=s.speakerAlignment;
            if(s.hasSpeakerFontSize)v.SpeakerFontSize=s.speakerFontSize;
            if(s.hasSpeakerCharacterSpacing)v.SpeakerCharacterSpacing=s.speakerCharacterSpacing;
        }

        private static string CanonicalCharacterId(VnSceneComposerScene scene, string candidate)
        {
            if(scene==null || scene.characters==null || string.IsNullOrWhiteSpace(candidate))return string.Empty;
            for(int i=0;i<scene.characters.Count;i++)
            {
                VnSceneComposerCharacter c=scene.characters[i];
                if(c==null)continue;
                string id=VnSceneComposerBeatCharacterStateResolver.ResolveCharacterId(c);
                if(string.Equals(id,candidate,StringComparison.OrdinalIgnoreCase))return id;
            }
            return string.Empty;
        }
    }
}
