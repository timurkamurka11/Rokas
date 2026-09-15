using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public static class VnSceneComposerContract
    {
        public const int SchemaVersion = 1;
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

    [Serializable]
    public sealed class VnSceneComposerTransition
    {
        public bool triggerActionBounce;
    }

    [Serializable]
    public sealed class VnSceneComposerTiming
    {
        public VnSceneComposerPreviewAdvanceMode previewAdvanceMode = VnSceneComposerPreviewAdvanceMode.ManualBeat;
        public float previewAutoDuration = 2f;
    }

    [Serializable]
    public sealed class VnSceneComposerScene
    {
        public string sceneId = NewStableId();
        public string label = "Scene";
        public VnSceneComposerMediaReference media = new VnSceneComposerMediaReference();
        public List<VnSceneComposerCharacter> characters = new List<VnSceneComposerCharacter>();
        public string speaker = string.Empty;
        [TextArea(3, 10)] public string previewText = string.Empty;
        public bool narration;
        public VnPresentationWorkshopPreset presentationOverrides = new VnPresentationWorkshopPreset();
        public VnSceneComposerTransition transition = new VnSceneComposerTransition();
        public VnSceneComposerTiming timing = new VnSceneComposerTiming();

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
