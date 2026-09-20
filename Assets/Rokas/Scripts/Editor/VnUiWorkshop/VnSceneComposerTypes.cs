using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public static class VnSceneComposerContract
    {
        public const int SchemaVersion = 3;
        public const string SourceHead = "f58f1db08fc225c2831ff59d50d42e8c07ea15ce";
    }

    public enum VnSceneComposerMediaKind
    {
        None,
        ExistingRokasAsset,
        ExternalImage,
        ExternalVideo,
        ExternalGif
    }

    public enum VnSceneComposerMediaScaleMode
    {
        Fit,
        Fill,
        Stretch
    }

    public enum VnSceneComposerPreviewAdvanceMode
    {
        ManualBeat,
        PreviewAutoDuration
    }

    public enum VnSceneComposerBeatEffect
    {
        None,
        Accent
    }

    public enum VnSceneComposerBeatCharacterVisibility
    {
        KeepPrevious,
        Show,
        Hide
    }

    public enum VnSceneComposerBeatCharacterPosition
    {
        KeepPrevious,
        Left,
        Center,
        Right,
        Custom
    }

    public enum VnSceneComposerDecorationLayer
    {
        BehindCharacters,
        FrontCharacters
    }

    public enum VnSceneComposerTextAlignment
    {
        Left,
        Center,
        Right
    }

    public enum VnSceneComposerTextLayer
    {
        BehindCharacters,
        FrontCharacters
    }

    public enum VnSceneComposerMusicMode
    {
        Silence,
        Track,
        KeepPrevious
    }

    public enum VnSceneComposerAudioCategory
    {
        Sfx,
        Ambience,
        MusicLayer
    }

    public enum VnSceneComposerAudioTrigger
    {
        SceneStart,
        BeatStart
    }

    public enum VnSceneComposerAudioStopMode
    {
        Natural,
        SceneEnd,
        BeatStart
    }

    [Serializable]
    public sealed class VnSceneComposerMusic
    {
        public VnSceneComposerMusicMode mode = VnSceneComposerMusicMode.Silence;
        public string assetGuid = string.Empty;
        public string displayName = string.Empty;
        public float volume = 1f;
        public bool loop = true;
        public float fadeInSeconds;
        public float fadeOutSeconds;
    }

    [Serializable]
    public sealed class VnSceneComposerAdditionalAudioCue
    {
        public string cueId = VnSceneComposerScene.NewStableId();
        public string displayName = "Новый звук";
        public string assetGuid = string.Empty;
        public bool enabled = true;
        public VnSceneComposerAudioCategory category = VnSceneComposerAudioCategory.Sfx;
        public float volume = 1f;
        public bool loop;
        public VnSceneComposerAudioTrigger trigger = VnSceneComposerAudioTrigger.SceneStart;
        public string startBeatId = string.Empty;
        public float startDelaySeconds;
        public float fadeInSeconds;
        public float fadeOutSeconds;
        public VnSceneComposerAudioStopMode stopMode = VnSceneComposerAudioStopMode.Natural;
        public string stopBeatId = string.Empty;
    }

    [Serializable]
    public sealed class VnSceneComposerDecoration
    {
        public string decorationId = VnSceneComposerScene.NewStableId();
        public string assetGuid = string.Empty;
        public string displayName = string.Empty;
        public Vector2 position = new Vector2(960f, 540f);
        public float scale = 1f;
        public float opacity = 1f;
        public bool visible = true;
        public VnSceneComposerDecorationLayer layer = VnSceneComposerDecorationLayer.BehindCharacters;
    }

    [Serializable]
    public sealed class VnSceneComposerTextElement
    {
        public string textElementId = VnSceneComposerScene.NewStableId();
        [TextArea(3, 10)] public string text = "Новый текст";
        public string fontAssetGuid = string.Empty;
        public string fontDisplayName = string.Empty;
        public float fontSize = 48f;
        public Vector2 position = new Vector2(960f, 360f);
        public Vector2 size = new Vector2(720f, 160f);
        public Color color = Color.white;
        public float opacity = 1f;
        public VnSceneComposerTextAlignment alignment = VnSceneComposerTextAlignment.Center;
        public bool visible = true;
        public VnSceneComposerTextLayer layer = VnSceneComposerTextLayer.FrontCharacters;
    }

    [Serializable]
    public sealed class VnSceneComposerMediaReference
    {
        public VnSceneComposerMediaKind kind = VnSceneComposerMediaKind.None;
        public string reference = string.Empty;
        public string displayName = string.Empty;
        public string contentHash = string.Empty;
        public bool localPreviewDependency;
        public VnSceneComposerMediaScaleMode scaleMode = VnSceneComposerMediaScaleMode.Fit;
        public bool loop;
    }

    [Serializable]
    public sealed class VnSceneComposerCharacter
    {
        public string characterId = string.Empty;
        public string stateId = string.Empty;
        public VnWorkshopStageSlot stageSlot = VnWorkshopStageSlot.Center;
        public bool hasPositionOffset;
        public Vector2 positionOffset;
        public bool hasScaleMultiplier;
        public float scaleMultiplier = 1f;
    }

    public enum VnSceneComposerSceneTransitionType
    {
        None,
        DarkCurtain
    }

    public enum VnSceneComposerSceneTransitionDirection
    {
        LeftToRight,
        RightToLeft
    }

    [Serializable]
    public sealed class VnSceneComposerTransition
    {
        public bool triggerActionBounce;
        // M-TRANSITION belongs to the incoming Scene. None is the serialized zero/default
        // so historical scenes continue to use the old instant boundary behavior.
        public VnSceneComposerSceneTransitionType sceneTransitionType = VnSceneComposerSceneTransitionType.None;
        public VnSceneComposerSceneTransitionDirection sceneTransitionDirection =
            VnSceneComposerSceneTransitionDirection.LeftToRight;
        public float sceneTransitionDuration = .7f;
    }

    [Serializable]
    public sealed class VnSceneComposerTiming
    {
        public VnSceneComposerPreviewAdvanceMode previewAdvanceMode = VnSceneComposerPreviewAdvanceMode.ManualBeat;
        public float previewAutoDuration = 2f;
    }

    [Serializable]
    public sealed class VnSceneComposerBeatCharacterStaging
    {
        public string stagingId = VnSceneComposerScene.NewStableId();
        public string characterId = string.Empty;
        public VnSceneComposerBeatCharacterVisibility visibility =
            VnSceneComposerBeatCharacterVisibility.KeepPrevious;
        public VnSceneComposerBeatCharacterPosition position =
            VnSceneComposerBeatCharacterPosition.KeepPrevious;
        public Vector2 customPositionOffset;
        public bool hasStateOverride;
        public string stateId = string.Empty;
        public VnSceneComposerBeatEffect effect = VnSceneComposerBeatEffect.None;
        public float effectStrength = 18f;
        public float effectDuration = .28f;
        public List<VnSceneComposerBeatCharacterStaging> characterStaging =
            new List<VnSceneComposerBeatCharacterStaging>();
        public float delaySeconds;
    }

    [Serializable]
    public sealed class VnSceneComposerDialogueBeat
    {
        public string beatId = VnSceneComposerScene.NewStableId();
        public string speaker = string.Empty;
        [TextArea(3, 10)] public string text = string.Empty;
        public bool narration;

        // M-F1: explicit target identity is independent from display speaker text.
        // hasStateOverride == false is the canonical "Keep Previous" behavior.
        public string targetCharacterId = string.Empty;
        public bool hasStateOverride;
        public string stateId = string.Empty;
        public VnSceneComposerBeatEffect effect = VnSceneComposerBeatEffect.None;
        public float effectStrength = 18f;
        public float effectDuration = .28f;
    }

    [Serializable]
    public sealed class VnSceneComposerScene
    {
        public string sceneId = NewStableId();
        public string label = "Scene";
        public VnSceneComposerMediaReference media = new VnSceneComposerMediaReference();
        public VnSceneComposerMusic music = new VnSceneComposerMusic();
        public List<VnSceneComposerAdditionalAudioCue> additionalAudioCues =
            new List<VnSceneComposerAdditionalAudioCue>();
        public List<VnSceneComposerCharacter> characters = new List<VnSceneComposerCharacter>();
        public List<VnSceneComposerDecoration> decorations = new List<VnSceneComposerDecoration>();
        public List<VnSceneComposerTextElement> textElements = new List<VnSceneComposerTextElement>();
        public List<VnSceneComposerDialogueBeat> dialogueBeats =
            new List<VnSceneComposerDialogueBeat> { new VnSceneComposerDialogueBeat() };
        public VnPresentationWorkshopPreset presentationOverrides = new VnPresentationWorkshopPreset();
        public VnSceneComposerTransition transition = new VnSceneComposerTransition();
        public VnSceneComposerTiming timing = new VnSceneComposerTiming();

        // Transitional source-compatible accessors. JsonUtility serializes fields rather than
        // properties, so the canonical persisted authority remains dialogueBeats only while the
        // existing composer call sites are migrated incrementally during M-DIALOGUE.
        public string speaker
        {
            get { return PrimaryDialogueBeat.speaker; }
            set { PrimaryDialogueBeat.speaker = value ?? string.Empty; }
        }

        public string previewText
        {
            get { return PrimaryDialogueBeat.text; }
            set { PrimaryDialogueBeat.text = value ?? string.Empty; }
        }

        public bool narration
        {
            get { return PrimaryDialogueBeat.narration; }
            set { PrimaryDialogueBeat.narration = value; }
        }

        private VnSceneComposerDialogueBeat PrimaryDialogueBeat
        {
            get
            {
                if (dialogueBeats == null) dialogueBeats = new List<VnSceneComposerDialogueBeat>();
                if (dialogueBeats.Count == 0 || dialogueBeats[0] == null)
                {
                    if (dialogueBeats.Count == 0) dialogueBeats.Add(new VnSceneComposerDialogueBeat());
                    else dialogueBeats[0] = new VnSceneComposerDialogueBeat();
                }
                return dialogueBeats[0];
            }
        }

        internal static string NewStableId()
        {
            return Guid.NewGuid().ToString("N");
        }
    }

    [Serializable]
    public sealed class VnSceneComposerProject
    {
        public int schemaVersion = VnSceneComposerContract.SchemaVersion;
        public string projectId = VnSceneComposerScene.NewStableId();
        public string title = "Untitled VN Sequence";
        public string sourceHead = VnSceneComposerContract.SourceHead;
        public VnPresentationWorkshopPreset defaultPresentation = new VnPresentationWorkshopPreset();
        public List<VnSceneComposerScene> scenes = new List<VnSceneComposerScene>();
    }
}
