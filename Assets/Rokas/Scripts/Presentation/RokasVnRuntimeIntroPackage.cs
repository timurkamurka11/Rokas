using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rokas.Presentation
{
    [Serializable]
    public sealed class RokasVnRuntimeMovementActionSnapshot
    {
        public string characterId = string.Empty;
        public int action;
        public float duration = 1.2f;
    }

    [Serializable]
    public sealed class RokasVnRuntimeMovementSnapshot
    {
        public RokasVnRuntimeMovementActionSnapshot primary =
            new RokasVnRuntimeMovementActionSnapshot();
        public RokasVnRuntimeMovementActionSnapshot secondary =
            new RokasVnRuntimeMovementActionSnapshot();
        public int secondaryTiming;
    }

    [Serializable]
    public sealed class RokasVnRuntimeReplicaEffectSnapshot
    {
        public int type;
        public float intensity = .35f;
        public float duration = .45f;
        public float frequency = 12f;
        public float decay = 1.5f;
        public Vector2 direction = Vector2.right;
        public Color flashColor = Color.white;
    }

    [Serializable]
    public sealed class RokasVnRuntimeStagingSnapshot
    {
        public string stagingId = string.Empty;
        public string characterId = string.Empty;
        public int visibility;
        public int position;
        public Vector2 customPositionOffset;
        public bool hasStateOverride;
        public string stateId = string.Empty;
        public int effect;
        public float effectStrength = 18f;
        public float effectDuration = .28f;
        public float delaySeconds;
    }

    [Serializable]
    public sealed class RokasVnRuntimeBeatSnapshot
    {
        public string beatId = string.Empty;
        public string speaker = string.Empty;
        public string text = string.Empty;
        public bool narration;
        public string targetCharacterId = string.Empty;
        public bool hasStateOverride;
        public string stateId = string.Empty;
        public int effect;
        public float effectStrength = 18f;
        public float effectDuration = .28f;
        public RokasVnRuntimeReplicaEffectSnapshot replicaEffect =
            new RokasVnRuntimeReplicaEffectSnapshot();
        public RokasVnRuntimeMovementSnapshot movement =
            new RokasVnRuntimeMovementSnapshot();
        public List<RokasVnRuntimeStagingSnapshot> characterStaging =
            new List<RokasVnRuntimeStagingSnapshot>();
    }

    [Serializable]
    public sealed class RokasVnRuntimeMediaSnapshot
    {
        public int kind;
        public string reference = string.Empty;
        public string displayName = string.Empty;
        public string contentHash = string.Empty;
        public string runtimeAssetKey = string.Empty;
        public int scaleMode;
        public bool loop;
    }

    [Serializable]
    public sealed class RokasVnRuntimeMusicSnapshot
    {
        public int mode;
        public string assetGuid = string.Empty;
        public string displayName = string.Empty;
        public float volume = 1f;
        public bool loop = true;
        public float fadeInSeconds;
        public float fadeOutSeconds;
    }

    [Serializable]
    public sealed class RokasVnRuntimeAudioCueSnapshot
    {
        public string cueId = string.Empty;
        public string displayName = string.Empty;
        public string assetGuid = string.Empty;
        public bool enabled = true;
        public int category;
        public float volume = 1f;
        public bool loop;
        public int trigger;
        public string startBeatId = string.Empty;
        public float startDelaySeconds;
        public float fadeInSeconds;
        public float fadeOutSeconds;
        public int stopMode;
        public string stopBeatId = string.Empty;
    }

    [Serializable]
    public sealed class RokasVnRuntimeCharacterSnapshot
    {
        public string characterId = string.Empty;
        public string stateId = string.Empty;
        public int stageSlot;
        public bool hasPositionOffset;
        public Vector2 positionOffset;
        public bool hasScaleMultiplier;
        public float scaleMultiplier = 1f;
    }

    [Serializable]
    public sealed class RokasVnRuntimePresentationSnapshot
    {
        public float leftX = -360f;
        public float centerX;
        public float rightX = 360f;
        public float slotY;
        public float leftScale = 1f;
        public float centerScale = 1f;
        public float rightScale = 1f;
        public float stageRepositionDuration = .35f;
        public int stageEasing = 3;

        public float speakerActiveScale = 1.05f;
        public float speakerActiveBrightness = 1f;
        public float speakerActiveForwardOffset = 12f;
        public float speakerInactiveScale = .94f;
        public float speakerInactiveBrightness = .76f;
        public float speakerInactiveAlpha = .84f;
        public float speakerFocusTransitionDuration = .22f;
        public int speakerFocusEasing = 3;

        public bool typewriterEnabled = true;
        public float typewriterCharactersPerSecond = 36f;
        public float typewriterBaseCharacterDelay;
        public float typewriterCommaPause;
        public float typewriterPeriodPause;
        public float typewriterEllipsisPause;
        public float typewriterQuestionPause;
        public float typewriterExclamationPause;
        public float typewriterLineStartDelay;

        public int dialogueFontPreset;
        public string dialogueFontAssetGuid = string.Empty;
        public Color dialogueColor = Color.white;
        public float dialogueFontSize = 22f;
        public float dialogueCharacterSpacing;
        public float dialogueLineSpacing;
        public float dialogueParagraphSpacing;
        public int dialogueAlignment;

        public int speakerFontPreset;
        public string speakerFontAssetGuid = string.Empty;
        public Color speakerColor = Color.white;
        public float speakerFontSize = 26f;
        public float speakerCharacterSpacing;
        public int speakerAlignment;
    }

    [Serializable]
    public sealed class RokasVnRuntimeSceneSnapshot
    {
        public string sceneId = string.Empty;
        public string label = string.Empty;
        public RokasVnRuntimeMediaSnapshot media = new RokasVnRuntimeMediaSnapshot();
        public RokasVnRuntimeMusicSnapshot music = new RokasVnRuntimeMusicSnapshot();
        public bool keepPreviousAdditionalAudio;
        public List<RokasVnRuntimeAudioCueSnapshot> additionalAudioCues =
            new List<RokasVnRuntimeAudioCueSnapshot>();
        public List<RokasVnRuntimeCharacterSnapshot> characters =
            new List<RokasVnRuntimeCharacterSnapshot>();
        public List<RokasVnRuntimeBeatSnapshot> dialogueBeats =
            new List<RokasVnRuntimeBeatSnapshot>();
        public RokasVnRuntimePresentationSnapshot presentation =
            new RokasVnRuntimePresentationSnapshot();
        public int sceneTransitionType;
        public int sceneTransitionDirection;
        public float sceneTransitionDuration = .7f;
        public bool triggerActionBounce;
        public bool isTerminal;
        public float terminalFadeDuration = 1.5f;
    }

    [Serializable]
    public sealed class RokasVnRuntimeIntroSnapshot
    {
        public int schemaVersion = 1;
        public string projectId = string.Empty;
        public string title = string.Empty;
        public string sourceProjectSha256 = string.Empty;
        public int sceneCount;
        public int beatCount;
        public List<RokasVnRuntimeSceneSnapshot> scenes =
            new List<RokasVnRuntimeSceneSnapshot>();
    }

    public enum RokasVnRuntimeAssetKind
    {
        Unknown,
        Texture,
        Audio,
        Video,
        Bytes,
        Font
    }

    [Serializable]
    public sealed class RokasVnRuntimeAssetBinding
    {
        public string authoredKey = string.Empty;
        public string displayName = string.Empty;
        public string contentHash = string.Empty;
        public RokasVnRuntimeAssetKind kind;
        public UnityEngine.Object asset;
        public string streamingAssetsRelativePath = string.Empty;
    }

    [Serializable]
    public sealed class RokasVnRuntimeCharacterStateBinding
    {
        public string stateId = string.Empty;
        public string characterId = string.Empty;
        public Texture2D texture;
        public Rect bodyUv = new Rect(0f, 0f, 1f, 1f);
    }

    [CreateAssetMenu(
        fileName = "RokasVnRuntimeIntroPackage",
        menuName = "ROKAS/VN Runtime Intro Package")]
    public sealed class RokasVnRuntimeIntroPackage : ScriptableObject
    {
        public const string ExpectedProjectId = "3cc2bc9ec7974ea7a5f4d074683bed61";
        public const string ResourcesPath = "VN/RuntimeIntro/RokasVnRuntimeIntroPackage";

        [SerializeField] private string projectId = string.Empty;
        [SerializeField] private string sourceProjectSha256 = string.Empty;
        [SerializeField, TextArea(4, 20)] private string portableProjectJson = string.Empty;
        [SerializeField] private RokasVnRuntimeIntroSnapshot snapshot =
            new RokasVnRuntimeIntroSnapshot();
        [SerializeField] private List<RokasVnRuntimeAssetBinding> assets =
            new List<RokasVnRuntimeAssetBinding>();
        [SerializeField] private List<RokasVnRuntimeCharacterStateBinding> characterStates =
            new List<RokasVnRuntimeCharacterStateBinding>();
        [SerializeField] private Texture2D dialoguePlaque;
        [SerializeField] private Texture2D controlSheet;
        [SerializeField] private Texture2D mutedSpeaker;
        [SerializeField] private Texture2D completionTriangle;

        public string ProjectId => projectId;
        public string SourceProjectSha256 => sourceProjectSha256;
        public string PortableProjectJson => portableProjectJson;
        public RokasVnRuntimeIntroSnapshot Snapshot => snapshot;
        public IReadOnlyList<RokasVnRuntimeAssetBinding> Assets => assets;
        public IReadOnlyList<RokasVnRuntimeCharacterStateBinding> CharacterStates => characterStates;
        public Texture2D DialoguePlaque => dialoguePlaque;
        public Texture2D ControlSheet => controlSheet;
        public Texture2D MutedSpeaker => mutedSpeaker;
        public Texture2D CompletionTriangle => completionTriangle;

        public void Configure(
            string expectedProjectId,
            string sourceSha256,
            string portableJson,
            RokasVnRuntimeIntroSnapshot runtimeSnapshot,
            IList<RokasVnRuntimeAssetBinding> runtimeAssets,
            IList<RokasVnRuntimeCharacterStateBinding> runtimeCharacterStates,
            Texture2D plaque,
            Texture2D controls,
            Texture2D muted,
            Texture2D triangle)
        {
            projectId = expectedProjectId ?? string.Empty;
            sourceProjectSha256 = sourceSha256 ?? string.Empty;
            portableProjectJson = portableJson ?? string.Empty;
            snapshot = runtimeSnapshot ?? new RokasVnRuntimeIntroSnapshot();
            assets = runtimeAssets != null
                ? new List<RokasVnRuntimeAssetBinding>(runtimeAssets)
                : new List<RokasVnRuntimeAssetBinding>();
            characterStates = runtimeCharacterStates != null
                ? new List<RokasVnRuntimeCharacterStateBinding>(runtimeCharacterStates)
                : new List<RokasVnRuntimeCharacterStateBinding>();
            dialoguePlaque = plaque;
            controlSheet = controls;
            mutedSpeaker = muted;
            completionTriangle = triangle;
        }

        public void ConfigureForTests(
            string expectedProjectId,
            string sourceSha256,
            RokasVnRuntimeIntroSnapshot runtimeSnapshot)
        {
            Configure(
                expectedProjectId, sourceSha256, string.Empty, runtimeSnapshot,
                null, null, null, null, null, null);
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(projectId))
            {
                error = "Runtime VN package Project ID is missing.";
                return false;
            }
            if (snapshot == null ||
                string.IsNullOrWhiteSpace(snapshot.projectId) ||
                !string.Equals(snapshot.projectId, projectId, StringComparison.Ordinal))
            {
                error = "Runtime VN snapshot Project ID does not match the package.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(sourceProjectSha256) ||
                !string.Equals(
                    snapshot.sourceProjectSha256,
                    sourceProjectSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                error = "Runtime intro source project hash is missing or inconsistent.";
                return false;
            }

            bool terminalFound = false;
            int beats = 0;
            if (snapshot.scenes != null)
            {
                for (int i = 0; i < snapshot.scenes.Count; i++)
                {
                    RokasVnRuntimeSceneSnapshot scene = snapshot.scenes[i];
                    if (scene == null)
                    {
                        error = "Runtime intro contains a null Scene.";
                        return false;
                    }
                    if (scene.isTerminal)
                    {
                        terminalFound = true;
                        if (scene.terminalFadeDuration <= 0f ||
                            float.IsNaN(scene.terminalFadeDuration) ||
                            float.IsInfinity(scene.terminalFadeDuration))
                        {
                            error = "Runtime intro terminal Scene has an invalid fade duration.";
                            return false;
                        }
                    }
                    beats += scene.dialogueBeats != null ? scene.dialogueBeats.Count : 0;
                }
            }

            if (!terminalFound)
            {
                error = "Runtime intro package has no terminal Scene.";
                return false;
            }

            int scenes = snapshot.scenes != null ? snapshot.scenes.Count : 0;
            if (scenes <= 0 || snapshot.sceneCount != scenes || snapshot.beatCount != beats)
            {
                error = "Runtime intro discovered Scene/Beat counts are inconsistent.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryGetAsset(string authoredKey, out RokasVnRuntimeAssetBinding binding)
        {
            binding = null;
            if (string.IsNullOrWhiteSpace(authoredKey) || assets == null) return false;
            for (int i = 0; i < assets.Count; i++)
            {
                RokasVnRuntimeAssetBinding candidate = assets[i];
                if (candidate != null &&
                    string.Equals(candidate.authoredKey, authoredKey, StringComparison.OrdinalIgnoreCase))
                {
                    binding = candidate;
                    return true;
                }
            }
            return false;
        }

        public bool TryGetCharacterState(
            string stateId,
            out RokasVnRuntimeCharacterStateBinding binding)
        {
            binding = null;
            if (string.IsNullOrWhiteSpace(stateId) || characterStates == null) return false;
            for (int i = 0; i < characterStates.Count; i++)
            {
                RokasVnRuntimeCharacterStateBinding candidate = characterStates[i];
                if (candidate != null &&
                    string.Equals(candidate.stateId, stateId, StringComparison.Ordinal))
                {
                    binding = candidate;
                    return true;
                }
            }
            return false;
        }
    }
}
