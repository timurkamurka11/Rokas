using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed class VnSceneComposerPlaybackFrame
    {
        internal VnSceneComposerPlaybackFrame(
            VnWorkshopPreviewFrame workshopFrame,
            VnWorkshopBackgroundTransitionSample backgroundTransition,
            Texture sourceBackground,
            Texture targetBackground)
        {
            WorkshopFrame = workshopFrame;
            ComposerBackgroundTransition = backgroundTransition;
            SourceBackground = sourceBackground;
            TargetBackground = targetBackground;
            VnPresentationWorkshopPreviewRenderer.RegisterPlaybackFrame(this);
        }

        public VnWorkshopPreviewFrame WorkshopFrame { get; }
        public VnWorkshopBackgroundTransitionSample ComposerBackgroundTransition { get; }
        public Texture SourceBackground { get; }
        public Texture TargetBackground { get; }
        public string Dialogue { get { return WorkshopFrame != null ? WorkshopFrame.Dialogue : string.Empty; } }
        public VnWorkshopPreviewCharacter[] ComposerCharacters
        {
            get { return WorkshopFrame != null ? WorkshopFrame.ComposerCharacters : Array.Empty<VnWorkshopPreviewCharacter>(); }
        }
    }

    public sealed class VnSceneComposerPlaybackController : IDisposable
    {
        private enum PlaybackScope
        {
            SingleScene,
            OrderedRange
        }

        private readonly VnSceneComposerProject project;
        private PlaybackScope scope;
        private int rangeStart;
        private int rangeEnd;
        private bool disposed;
        private VnSceneComposerImagePreview imagePreview;
        private VnSceneComposerVideoPreview videoPreview;
        private VnSceneComposerGifPreview gifPreview;
        private VnSceneComposerImagePreview sourceImagePreview;
        private VnSceneComposerVideoPreview sourceVideoPreview;
        private VnSceneComposerGifPreview sourceGifPreview;
        private Texture sourceMediaTexture;
        private string openedVideoSignature = string.Empty;

        public VnSceneComposerPlaybackController(VnSceneComposerProject project)
        {
            this.project = project ?? throw new ArgumentNullException(nameof(project));
            if (this.project.scenes == null) this.project.scenes = new List<VnSceneComposerScene>();
            CurrentSceneIndex = this.project.scenes.Count > 0 ? 0 : -1;
            rangeStart = CurrentSceneIndex;
            rangeEnd = CurrentSceneIndex;
            if (CurrentSceneIndex >= 0) ResetScene(CurrentSceneIndex, false);
        }

        public int CurrentSceneIndex { get; private set; }
        public bool IsPlaying { get; private set; }
        public float SceneElapsedSeconds { get; private set; }
        public float MediaTimeSeconds { get; private set; }
        public Texture CurrentMediaTexture { get; private set; }
        public VnSceneComposerTransitionSnapshot CurrentSnapshot { get; private set; }
        public VnSceneComposerPlaybackFrame CurrentFrame { get; private set; }

        public void PlayScene(int sceneIndex)
        {
            RequireSceneIndex(sceneIndex);
            scope = PlaybackScope.SingleScene;
            rangeStart = rangeEnd = sceneIndex;
            IsPlaying = true;
            ResetScene(sceneIndex, true);
        }

        public void PlayFromHere(int sceneIndex)
        {
            RequireSceneIndex(sceneIndex);
            scope = PlaybackScope.OrderedRange;
            rangeStart = sceneIndex;
            rangeEnd = project.scenes.Count - 1;
            IsPlaying = true;
            ResetScene(sceneIndex, true);
        }

        public void PlayAll()
        {
            if (project.scenes.Count == 0)
            {
                StopEmpty();
                return;
            }
            scope = PlaybackScope.OrderedRange;
            rangeStart = 0;
            rangeEnd = project.scenes.Count - 1;
            IsPlaying = true;
            ResetScene(0, true);
        }

        public void Pause()
        {
            IsPlaying = false;
            if (videoPreview != null) videoPreview.Pause();
        }

        public void Restart()
        {
            if (CurrentSceneIndex < 0) return;
            IsPlaying = true;
            SceneElapsedSeconds = 0f;
            MediaTimeSeconds = 0f;
            if (gifPreview != null) gifPreview.Restart();
            if (videoPreview != null)
            {
                videoPreview.Restart();
                videoPreview.Play();
            }
            RefreshMediaTexture();
            RebuildFrame(0f);
        }

        public void Previous()
        {
            if (project.scenes.Count == 0) return;
            int target = Mathf.Clamp(CurrentSceneIndex - 1, 0, project.scenes.Count - 1);
            ResetScene(target, IsPlaying);
        }

        public void Next()
        {
            if (project.scenes.Count == 0) return;
            int target = Mathf.Clamp(CurrentSceneIndex + 1, 0, project.scenes.Count - 1);
            if (target == CurrentSceneIndex)
            {
                IsPlaying = false;
                if (videoPreview != null) videoPreview.Pause();
                return;
            }
            ResetScene(target, IsPlaying);
        }

        public void Advance(float deltaSeconds)
        {
            if (deltaSeconds < 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds),
                    "Scene Composer playback delta must be finite and non-negative.");
            if (!IsPlaying || CurrentSceneIndex < 0) return;

            SceneElapsedSeconds += deltaSeconds;
            MediaTimeSeconds += deltaSeconds;
            if (gifPreview != null) gifPreview.Advance(deltaSeconds);
            if (videoPreview != null && !videoPreview.IsPlaying) videoPreview.Play();
            RefreshMediaTexture();

            VnSceneComposerScene scene = project.scenes[CurrentSceneIndex];
            VnSceneComposerPreviewTimingPlan timing = VnSceneComposerTransitionSampler.ResolveTiming(project, scene);
            float duration = ResolveScenePreviewDuration(timing);
            float progress = duration <= 0f ? 1f : Mathf.Clamp01(SceneElapsedSeconds / duration);
            RebuildFrame(progress);

            if (!timing.usesPreviewAutoDuration || SceneElapsedSeconds < duration) return;

            if (scope == PlaybackScope.OrderedRange && CurrentSceneIndex < rangeEnd)
            {
                float sequenceGap = Mathf.Max(0f, timing.sequenceGap);
                if (SceneElapsedSeconds + .00001f < duration + sequenceGap)
                {
                    RebuildFrame(1f);
                    return;
                }
                ResetScene(CurrentSceneIndex + 1, true);
                return;
            }

            IsPlaying = false;
            if (videoPreview != null) videoPreview.Pause();
            RebuildFrame(1f);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            IsPlaying = false;
            ReleaseMedia();
            ReleaseSourceMedia();
            CurrentMediaTexture = null;
            CurrentSnapshot = null;
            CurrentFrame = null;
        }

        private void ResetScene(int sceneIndex, bool playMedia)
        {
            RequireSceneIndex(sceneIndex);
            VnSceneComposerScene targetScene = project.scenes[sceneIndex];
            bool reuseVideoPreview = sceneIndex == CurrentSceneIndex && CanReuseVideoPreview(targetScene);
            if (!reuseVideoPreview) ReleaseMedia();
            ReleaseSourceMedia();
            CurrentSceneIndex = sceneIndex;
            SceneElapsedSeconds = 0f;
            MediaTimeSeconds = 0f;
            OpenSourceMedia(ResolveSourceScene(sceneIndex));
            if (!reuseVideoPreview) OpenMedia(targetScene);
            if (playMedia && videoPreview != null) videoPreview.Play();
            RefreshSourceMediaTexture();
            RefreshMediaTexture();
            RebuildFrame(0f);
        }

        private void RebuildFrame(float normalizedProgress)
        {
            if (CurrentSceneIndex < 0)
            {
                CurrentSnapshot = null;
                CurrentFrame = null;
                return;
            }

            VnSceneComposerScene targetScene = project.scenes[CurrentSceneIndex];
            VnSceneComposerScene sourceScene = ResolveSourceScene(CurrentSceneIndex);
            float progress = Mathf.Clamp01(normalizedProgress);
            VnSceneComposerTransitionSnapshot sample =
                VnSceneComposerTransitionSampler.Sample(project, sourceScene, targetScene, progress);
            VnSceneComposerTransitionSnapshot endpoint =
                VnSceneComposerTransitionSampler.Sample(project, sourceScene, targetScene, 1f);

            Texture2D targetBackground = CurrentMediaTexture as Texture2D;
            VnWorkshopPreviewFrame targetFrame = VnSceneComposerComposition.BuildFrame(
                project, targetScene, VnWorkshopResolution.Reference1920x1080, targetBackground);
            VnWorkshopPreviewFrame sourceFrame = VnSceneComposerComposition.BuildFrame(
                project, sourceScene, VnWorkshopResolution.Reference1920x1080, sourceMediaTexture as Texture2D);

            ApplyRendererFacingSample(targetFrame, sourceFrame, sample, endpoint);
            Texture targetVisual = CurrentMediaTexture != null ? CurrentMediaTexture : targetFrame.BackgroundTexture;
            Texture sourceVisual = sourceMediaTexture != null ? sourceMediaTexture : sourceFrame.BackgroundTexture;

            CurrentSnapshot = sample;
            CurrentFrame = new VnSceneComposerPlaybackFrame(targetFrame, sample.background, sourceVisual, targetVisual);
        }

        private static void ApplyRendererFacingSample(
            VnWorkshopPreviewFrame targetFrame,
            VnWorkshopPreviewFrame sourceFrame,
            VnSceneComposerTransitionSnapshot sample,
            VnSceneComposerTransitionSnapshot endpoint)
        {
            targetFrame.Dialogue = sample.visibleText ?? string.Empty;
            VnWorkshopPreviewCharacter[] targetCharacters = targetFrame.ComposerCharacters ?? Array.Empty<VnWorkshopPreviewCharacter>();
            var rendered = new List<VnWorkshopPreviewCharacter>(targetCharacters.Length + 3);

            for (int i = 0; i < targetCharacters.Length; i++)
            {
                VnWorkshopPreviewCharacter character = Clone(targetCharacters[i]);
                if (character == null) continue;

                ApplyStageDelta(character, sample, endpoint, i);
                ApplyFocusDelta(character, sample, endpoint, i);

                VnSceneComposerCharacterMotionPreview motion = FindMotion(sample.characterMotions, character.CharacterId, true);
                if (motion != null)
                {
                    Rect body = character.Body;
                    body.position += motion.sample.PositionOffset;
                    character.Body = body;
                    character.Alpha *= Mathf.Clamp01(motion.sample.Alpha);
                }

                VnSceneComposerExpressionPreview expression = FindExpression(sample.expressions, character.CharacterId);
                if (expression != null)
                    character.Alpha *= Mathf.Clamp01(expression.sample.EndAlpha);

                ApplyBounce(character, sample.bounce);
                rendered.Add(character);
            }

            AppendExpressionSources(rendered, sourceFrame, sample.expressions);
            AppendExitingCharacters(rendered, sourceFrame, sample.characterMotions);
            targetFrame.ComposerCharacters = rendered.ToArray();
        }

        private static void ApplyStageDelta(
            VnWorkshopPreviewCharacter character,
            VnSceneComposerTransitionSnapshot current,
            VnSceneComposerTransitionSnapshot endpoint,
            int index)
        {
            if (current.stage == null || endpoint.stage == null ||
                index >= current.stage.Length || index >= endpoint.stage.Length) return;
            VnWorkshopStageTransitionSample now = current.stage[index];
            VnWorkshopStageTransitionSample end = endpoint.stage[index];
            Rect body = character.Body;
            body.center += now.Position - end.Position;
            if (end.Scale > .0001f)
                body.size *= Mathf.Max(.01f, now.Scale / end.Scale);
            character.Body = body;
        }

        private static void ApplyFocusDelta(
            VnWorkshopPreviewCharacter character,
            VnSceneComposerTransitionSnapshot current,
            VnSceneComposerTransitionSnapshot endpoint,
            int index)
        {
            if (current.focus == null || endpoint.focus == null ||
                index >= current.focus.Length || index >= endpoint.focus.Length) return;
            VnWorkshopSpeakerFocusSample now = current.focus[index];
            VnWorkshopSpeakerFocusSample end = endpoint.focus[index];
            Rect body = character.Body;
            body.center += now.PositionOffset - end.PositionOffset;
            if (end.Scale > .0001f) body.size *= Mathf.Max(.01f, now.Scale / end.Scale);
            character.Body = body;
            if (end.Alpha > .0001f) character.Alpha *= Mathf.Clamp01(now.Alpha / end.Alpha);
            if (end.Brightness > .0001f) character.Brightness *= Mathf.Max(0f, now.Brightness / end.Brightness);
        }

        private static void ApplyBounce(VnWorkshopPreviewCharacter character, VnWorkshopActionBounceSample bounce)
        {
            Rect body = character.Body;
            body.center += bounce.PositionOffset;
            body.size *= Mathf.Max(.01f, bounce.ScaleMultiplier);
            character.Body = body;
        }

        private static void AppendExpressionSources(
            List<VnWorkshopPreviewCharacter> rendered,
            VnWorkshopPreviewFrame sourceFrame,
            VnSceneComposerExpressionPreview[] expressions)
        {
            if (sourceFrame == null || sourceFrame.ComposerCharacters == null || expressions == null) return;
            for (int i = 0; i < expressions.Length; i++)
            {
                VnSceneComposerExpressionPreview expression = expressions[i];
                VnWorkshopPreviewCharacter source = FindCharacter(sourceFrame.ComposerCharacters, expression.characterId);
                if (source == null || expression.sample.StartAlpha <= 0f) continue;
                VnWorkshopPreviewCharacter clone = Clone(source);
                clone.Alpha *= Mathf.Clamp01(expression.sample.StartAlpha);
                rendered.Add(clone);
            }
        }

        private static void AppendExitingCharacters(
            List<VnWorkshopPreviewCharacter> rendered,
            VnWorkshopPreviewFrame sourceFrame,
            VnSceneComposerCharacterMotionPreview[] motions)
        {
            if (sourceFrame == null || sourceFrame.ComposerCharacters == null || motions == null) return;
            for (int i = 0; i < motions.Length; i++)
            {
                VnSceneComposerCharacterMotionPreview motion = motions[i];
                if (motion == null || !motion.exiting || motion.sample.Alpha <= 0f) continue;
                VnWorkshopPreviewCharacter source = FindCharacter(sourceFrame.ComposerCharacters, motion.characterId);
                if (source == null) continue;
                VnWorkshopPreviewCharacter clone = Clone(source);
                Rect body = clone.Body;
                body.position += motion.sample.PositionOffset;
                clone.Body = body;
                clone.Alpha *= Mathf.Clamp01(motion.sample.Alpha);
                rendered.Add(clone);
            }
        }

        private void OpenSourceMedia(VnSceneComposerScene scene)
        {
            if (scene == null || scene.media == null) return;
            switch (scene.media.kind)
            {
                case VnSceneComposerMediaKind.ExistingRokasAsset:
                case VnSceneComposerMediaKind.ExternalImage:
                    sourceImagePreview = VnSceneComposerMediaEditing.OpenImagePreview(scene.media);
                    break;
                case VnSceneComposerMediaKind.ExternalVideo:
                    sourceVideoPreview = VnSceneComposerMediaEditing.OpenVideoPreview(scene.media, 1280, 720);
                    break;
                case VnSceneComposerMediaKind.ExternalGif:
                    sourceGifPreview = VnSceneComposerMediaEditing.OpenGifPreview(scene.media);
                    break;
            }
        }

        private void RefreshSourceMediaTexture()
        {
            if (sourceGifPreview != null) sourceMediaTexture = sourceGifPreview.currentTexture;
            else if (sourceVideoPreview != null) sourceMediaTexture = sourceVideoPreview.texture;
            else if (sourceImagePreview != null) sourceMediaTexture = sourceImagePreview.texture;
            else sourceMediaTexture = null;
        }

        private void ReleaseSourceMedia()
        {
            if (sourceImagePreview != null) sourceImagePreview.Dispose();
            if (sourceVideoPreview != null) sourceVideoPreview.Dispose();
            if (sourceGifPreview != null) sourceGifPreview.Dispose();
            sourceImagePreview = null;
            sourceVideoPreview = null;
            sourceGifPreview = null;
            sourceMediaTexture = null;
        }

        private void OpenMedia(VnSceneComposerScene scene)
        {
            if (scene == null || scene.media == null) return;
            switch (scene.media.kind)
            {
                case VnSceneComposerMediaKind.ExistingRokasAsset:
                case VnSceneComposerMediaKind.ExternalImage:
                    imagePreview = VnSceneComposerMediaEditing.OpenImagePreview(scene.media);
                    break;
                case VnSceneComposerMediaKind.ExternalVideo:
                    videoPreview = VnSceneComposerMediaEditing.OpenVideoPreview(scene.media, 1280, 720);
                    openedVideoSignature = BuildVideoSignature(scene.media);
                    break;
                case VnSceneComposerMediaKind.ExternalGif:
                    gifPreview = VnSceneComposerMediaEditing.OpenGifPreview(scene.media);
                    break;
            }
        }

        private bool CanReuseVideoPreview(VnSceneComposerScene scene)
        {
            return scene != null && scene.media != null &&
                   scene.media.kind == VnSceneComposerMediaKind.ExternalVideo &&
                   videoPreview != null && videoPreview.texture != null &&
                   string.Equals(openedVideoSignature, BuildVideoSignature(scene.media), StringComparison.Ordinal);
        }

        private static string BuildVideoSignature(VnSceneComposerMediaReference media)
        {
            if (media == null) return string.Empty;
            return (media.reference ?? string.Empty) + "|" + (media.contentHash ?? string.Empty) + "|" + media.loop;
        }

        private void RefreshMediaTexture()
        {
            if (gifPreview != null) CurrentMediaTexture = gifPreview.currentTexture;
            else if (videoPreview != null) CurrentMediaTexture = videoPreview.texture;
            else if (imagePreview != null) CurrentMediaTexture = imagePreview.texture;
            else CurrentMediaTexture = null;
        }

        private void ReleaseMedia()
        {
            if (imagePreview != null) imagePreview.Dispose();
            if (videoPreview != null) videoPreview.Dispose();
            if (gifPreview != null) gifPreview.Dispose();
            imagePreview = null;
            videoPreview = null;
            gifPreview = null;
            openedVideoSignature = string.Empty;
        }

        private VnSceneComposerScene ResolveSourceScene(int targetIndex)
        {
            if (targetIndex > 0) return project.scenes[targetIndex - 1];
            return new VnSceneComposerScene
            {
                sceneId = "__scene_composer_preview_baseline__",
                label = "Preview Baseline",
                previewText = string.Empty,
                speaker = string.Empty,
                narration = true,
                timing = new VnSceneComposerTiming()
            };
        }

        private static float ResolveScenePreviewDuration(VnSceneComposerPreviewTimingPlan timing)
        {
            if (timing == null) return 0f;
            if (timing.usesPreviewAutoDuration) return Mathf.Max(0f, timing.previewAutoDuration);
            return Mathf.Max(.0001f,
                timing.typewriterDuration + timing.settleDuration + timing.breathingRoom);
        }

        private void RequireSceneIndex(int sceneIndex)
        {
            if (sceneIndex < 0 || sceneIndex >= project.scenes.Count)
                throw new ArgumentOutOfRangeException(nameof(sceneIndex), sceneIndex, "Scene index is outside the Composer project.");
        }

        private void StopEmpty()
        {
            IsPlaying = false;
            CurrentSceneIndex = -1;
            SceneElapsedSeconds = 0f;
            MediaTimeSeconds = 0f;
            ReleaseMedia();
            ReleaseSourceMedia();
            CurrentMediaTexture = null;
            CurrentSnapshot = null;
            CurrentFrame = null;
        }

        private static VnSceneComposerCharacterMotionPreview FindMotion(
            VnSceneComposerCharacterMotionPreview[] motions, string characterId, bool entering)
        {
            if (motions == null) return null;
            for (int i = 0; i < motions.Length; i++)
            {
                VnSceneComposerCharacterMotionPreview motion = motions[i];
                if (motion != null && motion.entering == entering &&
                    string.Equals(motion.characterId, characterId, StringComparison.OrdinalIgnoreCase)) return motion;
            }
            return null;
        }

        private static VnSceneComposerExpressionPreview FindExpression(
            VnSceneComposerExpressionPreview[] expressions, string characterId)
        {
            if (expressions == null) return null;
            for (int i = 0; i < expressions.Length; i++)
            {
                VnSceneComposerExpressionPreview expression = expressions[i];
                if (expression != null && string.Equals(expression.characterId, characterId, StringComparison.OrdinalIgnoreCase))
                    return expression;
            }
            return null;
        }

        private static VnWorkshopPreviewCharacter FindCharacter(
            VnWorkshopPreviewCharacter[] characters, string characterId)
        {
            if (characters == null) return null;
            for (int i = 0; i < characters.Length; i++)
            {
                VnWorkshopPreviewCharacter character = characters[i];
                if (character != null && string.Equals(character.CharacterId, characterId, StringComparison.OrdinalIgnoreCase))
                    return character;
            }
            return null;
        }

        private static VnWorkshopPreviewCharacter Clone(VnWorkshopPreviewCharacter source)
        {
            if (source == null) return null;
            return new VnWorkshopPreviewCharacter
            {
                CharacterId = source.CharacterId,
                StateId = source.StateId,
                Slot = source.Slot,
                Texture = source.Texture,
                Uv = source.Uv,
                Body = source.Body,
                Active = source.Active,
                Alpha = source.Alpha,
                Brightness = source.Brightness
            };
        }
    }
}
