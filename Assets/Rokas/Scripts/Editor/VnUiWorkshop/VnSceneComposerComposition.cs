using System;
using System.Reflection;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.Video;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed class VnWorkshopPreviewCharacter
    {
        public string CharacterId { get; internal set; }
        public string StateId { get; internal set; }
        public VnWorkshopStageSlot Slot { get; internal set; }
        public Texture2D Texture { get; internal set; }
        public Rect Uv { get; internal set; }
        public Rect Body { get; internal set; }
        public bool Active { get; internal set; }
        public float Alpha { get; internal set; }
        public float Brightness { get; internal set; }
    }

    public static class VnSceneComposerComposition
    {
        public static VnPresentationWorkshopPreset ResolvePresentation(
            VnSceneComposerProject project, VnSceneComposerScene scene)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (scene == null) throw new ArgumentNullException(nameof(scene));

            VnPresentationWorkshopPreset defaults = project.defaultPresentation ?? new VnPresentationWorkshopPreset();
            VnPresentationWorkshopPreset resolved = JsonUtility.FromJson<VnPresentationWorkshopPreset>(
                JsonUtility.ToJson(defaults));
            if (resolved == null) resolved = new VnPresentationWorkshopPreset();
            OverlayPreset(resolved, scene.presentationOverrides);
            return resolved;
        }

        public static VnWorkshopPreviewFrame BuildFrame(
            VnSceneComposerProject project,
            VnSceneComposerScene scene,
            VnWorkshopResolution resolution,
            Texture2D backgroundOverride)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            if (scene.characters == null) scene.characters = new System.Collections.Generic.List<VnSceneComposerCharacter>();
            if (scene.characters.Count > 3)
                throw new ArgumentOutOfRangeException(nameof(scene.characters), scene.characters.Count,
                    "Scene Composer supports zero to three visible authored characters.");

            VnPresentationWorkshopPreset preset = ResolvePresentation(project, scene);
            VnWorkshopPreviewScene baseScene = SelectBaseScene(scene);
            VnWorkshopPreviewFrame frame = VnPresentationWorkshopPreviewRenderer.BuildFrame(
                preset, resolution, baseScene, scene.previewText ?? string.Empty);

            frame.BackgroundTexture = backgroundOverride != null ? backgroundOverride : Texture2D.blackTexture;
            frame.Speaker = scene.narration ? string.Empty : (scene.speaker ?? string.Empty);
            frame.Dialogue = scene.previewText ?? string.Empty;
            frame.ShowMina = false;
            frame.ShowKeiko = false;
            frame.ComposerCharacters = BuildCharacters(frame, preset, scene);
            RegisterStaticExternalVideoContext(scene, frame);
            return frame;
        }

        private static void RegisterStaticExternalVideoContext(VnSceneComposerScene scene, VnWorkshopPreviewFrame frame)
        {
            if (scene == null || frame == null || scene.media == null ||
                scene.media.kind != VnSceneComposerMediaKind.ExternalVideo ||
                string.IsNullOrEmpty(scene.media.reference)) return;

            string expectedUrl;
            try { expectedUrl = new Uri(System.IO.Path.GetFullPath(scene.media.reference)).AbsoluteUri; }
            catch { expectedUrl = System.IO.Path.GetFullPath(scene.media.reference); }

            VideoPlayer[] players = Resources.FindObjectsOfTypeAll<VideoPlayer>();
            VideoPlayer match = null;
            for (int i = players.Length - 1; i >= 0; i--)
            {
                VideoPlayer candidate = players[i];
                if (candidate == null || candidate.targetTexture == null) continue;
                if (!string.Equals(candidate.url ?? string.Empty, expectedUrl, StringComparison.OrdinalIgnoreCase)) continue;
                match = candidate;
                break;
            }
            if (match == null) return;

            // Composition owns routing only. It must never probe/decode media: real first-frame
            // preparation belongs to the Scene Composer authoring-preview lifecycle. This also
            // keeps deterministic CI routing tests from decoding dummy mp4 fixtures.
            Texture videoTexture = match.targetTexture;
            if (videoTexture == null) return;
            var sample = new VnWorkshopBackgroundTransitionSample
            {
                SourceAlpha = 0f,
                TargetAlpha = 1f,
                CurtainCoverage = 0f,
                CurtainPosition = 1f,
                CurtainDarkness = 0f,
                CurtainDirection = VnWorkshopCurtainDirection.LeftToRight,
                Complete = true
            };
            new VnSceneComposerPlaybackFrame(frame, sample, videoTexture, videoTexture);
        }

        private static VnWorkshopPreviewScene SelectBaseScene(VnSceneComposerScene scene)
        {
            if (!scene.narration && string.Equals(scene.speaker, "Mina", StringComparison.OrdinalIgnoreCase))
                return VnWorkshopPreviewScene.MinaBody;
            if (scene.characters != null)
            {
                for (int i = 0; i < scene.characters.Count; i++)
                {
                    VnSceneComposerCharacter character = scene.characters[i];
                    if (character != null && !string.IsNullOrEmpty(character.stateId) &&
                        VnSceneComposerCharacterStateResolver.TryResolve(character.stateId, out VnSceneComposerResolvedCharacterState state) &&
                        string.Equals(state.Character, "Mina", StringComparison.OrdinalIgnoreCase) &&
                        string.IsNullOrEmpty(scene.speaker))
                        return VnWorkshopPreviewScene.MinaBody;
                }
            }
            return VnWorkshopPreviewScene.BusStopKeiko;
        }

        private static VnWorkshopPreviewCharacter[] BuildCharacters(
            VnWorkshopPreviewFrame frame,
            VnPresentationWorkshopPreset preset,
            VnSceneComposerScene scene)
        {
            int count = scene.characters.Count;
            if (count == 0) return Array.Empty<VnWorkshopPreviewCharacter>();

            VnWorkshopStageLayoutValues stage = VnPresentationWorkshopVn10Resolver.ResolveStageLayout(preset);
            VnWorkshopSpeakerFocusValues focus = VnPresentationWorkshopVn10Resolver.ResolveSpeakerFocus(preset);
            int activeIndex = FindActiveIndex(scene);
            var result = new VnWorkshopPreviewCharacter[count];

            for (int i = 0; i < count; i++)
            {
                VnSceneComposerCharacter source = scene.characters[i];
                if (source == null) throw new ArgumentException("Scene contains a null authored character entry.", nameof(scene));
                if (!VnSceneComposerCharacterStateResolver.TryResolve(source.stateId, out VnSceneComposerResolvedCharacterState state))
                    throw new ArgumentException("Unknown authored VN character state: " + (source.stateId ?? string.Empty), nameof(scene));
                if (!string.IsNullOrEmpty(source.characterId) &&
                    !string.Equals(source.characterId, state.Character, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Authored state '" + source.stateId + "' belongs to " + state.Character +
                        ", not " + source.characterId + ".", nameof(scene));

                Texture2D texture = state.Texture;
                Rect baseline;
                if (string.Equals(state.Character, "Mina", StringComparison.OrdinalIgnoreCase))
                    baseline = frame.MinaBody;
                else if (string.Equals(state.Character, "Keiko", StringComparison.OrdinalIgnoreCase))
                    baseline = frame.KeikoBody;
                else
                    baseline = frame.MinaBody;

                if (state.Onboarded && texture != null && texture.width > 0 && texture.height > 0)
                {
                    Vector2 baselineCenter = baseline.center;
                    float authoredHeight = baseline.height;
                    float authoredWidth = authoredHeight * ((float)texture.width / texture.height);
                    baseline = RectFromCenter(baselineCenter, new Vector2(authoredWidth, authoredHeight));
                }

                ResolveSlot(source.stageSlot, stage, out float xOffset, out float slotScale);
                Vector2 center = baseline.center;
                center.x = frame.VirtualCanvasSize.x * .5f + xOffset;
                center.y += stage.SlotY;
                float scale = slotScale;
                float alpha = 1f;
                float brightness = 1f;
                bool active = activeIndex == i;

                if (count > 1 && activeIndex >= 0)
                {
                    VnWorkshopSpeakerFocusSample sample = VnPresentationWorkshopVn10Resolver.SampleSpeakerFocus(
                        count, activeIndex, activeIndex, i, 1f, focus);
                    center += sample.PositionOffset;
                    scale *= sample.Scale;
                    alpha = sample.Alpha;
                    brightness = sample.Brightness;
                }
                if (source.hasPositionOffset) center += source.positionOffset;
                if (source.hasScaleMultiplier) scale *= source.scaleMultiplier;
                scale = Mathf.Max(.01f, scale);
                Rect body = RectFromCenter(center, baseline.size * scale);

                result[i] = new VnWorkshopPreviewCharacter
                {
                    CharacterId = string.IsNullOrEmpty(source.characterId) ? state.Character : source.characterId,
                    StateId = source.stateId ?? string.Empty,
                    Slot = source.stageSlot,
                    Texture = texture,
                    Uv = state.BodyUv,
                    Body = body,
                    Active = active,
                    Alpha = Mathf.Clamp01(alpha),
                    Brightness = Mathf.Max(0f, brightness)
                };
            }
            return result;
        }

        private static int FindActiveIndex(VnSceneComposerScene scene)
        {
            if (scene.narration || string.IsNullOrEmpty(scene.speaker)) return -1;
            for (int i = 0; i < scene.characters.Count; i++)
            {
                VnSceneComposerCharacter character = scene.characters[i];
                if (character == null) continue;
                if (string.Equals(character.characterId, scene.speaker, StringComparison.OrdinalIgnoreCase)) return i;
                if (VnSceneComposerCharacterStateResolver.TryResolve(character.stateId, out VnSceneComposerResolvedCharacterState state) &&
                    string.Equals(state.Character, scene.speaker, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        private static void ResolveSlot(VnWorkshopStageSlot slot, VnWorkshopStageLayoutValues stage,
            out float xOffset, out float scale)
        {
            switch (slot)
            {
                case VnWorkshopStageSlot.Left:
                    xOffset = stage.LeftX; scale = stage.LeftScale; return;
                case VnWorkshopStageSlot.Center:
                    xOffset = stage.CenterX; scale = stage.CenterScale; return;
                case VnWorkshopStageSlot.Right:
                    xOffset = stage.RightX; scale = stage.RightScale; return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(slot), slot, null);
            }
        }

        private static Rect RectFromCenter(Vector2 center, Vector2 size)
        {
            return new Rect(center - size * .5f, size);
        }

        private static void OverlayPreset(VnPresentationWorkshopPreset target, VnPresentationWorkshopPreset source)
        {
            if (target == null || source == null) return;
            FieldInfo[] fields = typeof(VnPresentationWorkshopPreset).GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                object sourceValue = field.GetValue(source);
                if (sourceValue == null) continue;
                object targetValue = field.GetValue(target);
                if (targetValue == null)
                {
                    targetValue = Activator.CreateInstance(field.FieldType);
                    field.SetValue(target, targetValue);
                }
                OverlayOverrideObject(targetValue, sourceValue);
            }
        }

        private static void OverlayOverrideObject(object target, object source)
        {
            Type type = source.GetType();
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo hasField = fields[i];
                if (hasField.FieldType != typeof(bool) || !hasField.Name.StartsWith("has", StringComparison.Ordinal) ||
                    !(bool)hasField.GetValue(source)) continue;

                string suffix = hasField.Name.Substring(3);
                if (suffix.Length == 0) continue;
                string valueName = char.ToLowerInvariant(suffix[0]) + suffix.Substring(1);
                FieldInfo valueField = type.GetField(valueName, BindingFlags.Public | BindingFlags.Instance);
                if (valueField == null) continue;
                hasField.SetValue(target, true);
                valueField.SetValue(target, valueField.GetValue(source));
            }
        }
    }
}
