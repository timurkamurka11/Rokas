using System;
using System.Collections.Generic;
using System.Reflection;
using Rokas.Presentation;
using UnityEditor;
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

    public sealed class VnWorkshopPreviewDecoration
    {
        public string DecorationId { get; internal set; }
        public Texture2D Texture { get; internal set; }
        public Rect Body { get; internal set; }
        public float Alpha { get; internal set; }
        public VnSceneComposerDecorationLayer Layer { get; internal set; }
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
            ApplyGeometryScopes(resolved, defaults, scene);
            return resolved;
        }

        public static VnWorkshopPreviewFrame BuildFrame(
            VnSceneComposerProject project,
            VnSceneComposerScene scene,
            VnWorkshopResolution resolution,
            Texture2D backgroundOverride)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            return BuildFrame(project, scene, ResolveFirstBeat(scene), resolution, backgroundOverride);
        }

        public static VnWorkshopPreviewFrame BuildFrame(
            VnSceneComposerProject project,
            VnSceneComposerScene scene,
            VnSceneComposerDialogueBeat beat,
            VnWorkshopResolution resolution,
            Texture2D backgroundOverride = null)
        {
            return BuildFrame(project, scene, beat, resolution, backgroundOverride, float.MaxValue, null);
        }

        public static VnWorkshopPreviewFrame BuildFrame(
            VnSceneComposerProject project,
            VnSceneComposerScene scene,
            VnSceneComposerDialogueBeat beat,
            VnWorkshopResolution resolution,
            Texture2D backgroundOverride,
            float beatElapsedSeconds)
        {
            return BuildFrame(project, scene, beat, resolution, backgroundOverride, beatElapsedSeconds, null);
        }

        internal static VnWorkshopPreviewFrame BuildFrame(
            VnSceneComposerProject project,
            VnSceneComposerScene scene,
            VnSceneComposerDialogueBeat beat,
            VnWorkshopResolution resolution,
            Texture2D backgroundOverride,
            float beatElapsedSeconds,
            ISet<string> cancelledStagingIds)
        {
            return BuildFrame(
                project, scene, beat, resolution, backgroundOverride,
                beatElapsedSeconds, cancelledStagingIds, false);
        }

        internal static VnWorkshopPreviewFrame BuildFrame(
            VnSceneComposerProject project,
            VnSceneComposerScene scene,
            VnSceneComposerDialogueBeat beat,
            VnWorkshopResolution resolution,
            Texture2D backgroundOverride,
            float beatElapsedSeconds,
            ISet<string> cancelledStagingIds,
            bool applyMovement)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            if (beat == null) throw new ArgumentNullException(nameof(beat));
            if (scene.characters == null) scene.characters = new System.Collections.Generic.List<VnSceneComposerCharacter>();
            if (scene.characters.Count > 3)
                throw new ArgumentOutOfRangeException(nameof(scene.characters), scene.characters.Count,
                    "Scene Composer supports zero to three visible authored characters.");

            VnPresentationWorkshopPreset preset = ResolvePresentation(project, scene);
            VnSceneComposerTextStyleResolver.ApplyResolvedTypography(project, scene, beat, preset);
            VnWorkshopPreviewScene baseScene = SelectBaseScene(scene, beat);
            string dialogue = beat.text ?? string.Empty;
            VnWorkshopPreviewFrame frame = VnPresentationWorkshopPreviewRenderer.BuildFrame(
                preset, resolution, baseScene, dialogue);

            VnSceneComposerRuntimeUi.ConfigureFrame(frame, preset);
            frame.BackgroundTexture = backgroundOverride != null ? backgroundOverride : Texture2D.blackTexture;
            frame.Speaker = beat.narration ? string.Empty : (beat.speaker ?? string.Empty);
            frame.Dialogue = dialogue;
            frame.ShowMina = false;
            frame.ShowKeiko = false;
            frame.ComposerCharacters = BuildCharacters(
                frame, preset, scene, beat, beatElapsedSeconds, cancelledStagingIds,
                applyMovement, out string[] characterWarnings);
            frame.ComposerCharacterWarnings = characterWarnings;
            frame.ComposerDecorations = BuildDecorations(scene, out string[] decorationWarnings);
            frame.ComposerDecorationWarnings = decorationWarnings;
            RegisterStaticExternalVideoContext(scene, frame);
            return frame;
        }

        private static VnSceneComposerDialogueBeat ResolveFirstBeat(VnSceneComposerScene scene)
        {
            if (scene != null && scene.dialogueBeats != null && scene.dialogueBeats.Count > 0 &&
                scene.dialogueBeats[0] != null)
                return scene.dialogueBeats[0];

            return new VnSceneComposerDialogueBeat
            {
                beatId = string.Empty,
                speaker = string.Empty,
                text = string.Empty,
                narration = false
            };
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

        private static VnWorkshopPreviewScene SelectBaseScene(
            VnSceneComposerScene scene,
            VnSceneComposerDialogueBeat beat)
        {
            if (!beat.narration && string.Equals(beat.speaker, "Mina", StringComparison.OrdinalIgnoreCase))
                return VnWorkshopPreviewScene.MinaBody;
            if (scene.characters != null)
            {
                for (int i = 0; i < scene.characters.Count; i++)
                {
                    VnSceneComposerCharacter character = scene.characters[i];
                    if (character != null && !string.IsNullOrEmpty(character.stateId) &&
                        VnSceneComposerCharacterStateResolver.TryResolve(character.stateId, out VnSceneComposerResolvedCharacterState state) &&
                        string.Equals(state.Character, "Mina", StringComparison.OrdinalIgnoreCase) &&
                        string.IsNullOrEmpty(beat.speaker))
                        return VnWorkshopPreviewScene.MinaBody;
                }
            }
            return VnWorkshopPreviewScene.BusStopKeiko;
        }

        private static VnWorkshopPreviewCharacter[] BuildCharacters(
            VnWorkshopPreviewFrame frame,
            VnPresentationWorkshopPreset preset,
            VnSceneComposerScene scene,
            VnSceneComposerDialogueBeat beat,
            float beatElapsedSeconds,
            ISet<string> cancelledStagingIds,
            bool applyMovement,
            out string[] warnings)
        {
            int count = scene.characters.Count;
            if (count == 0)
            {
                warnings = Array.Empty<string>();
                return Array.Empty<VnWorkshopPreviewCharacter>();
            }

            VnWorkshopStageLayoutValues stage = VnPresentationWorkshopVn10Resolver.ResolveStageLayout(preset);
            VnWorkshopSpeakerFocusValues focus = VnPresentationWorkshopVn10Resolver.ResolveSpeakerFocus(preset);
            int activeSourceIndex = FindActiveIndex(scene, beat);
            var resolvedStaging = new VnSceneComposerResolvedBeatCharacterStaging[count];
            var resolvedMovement = new VnSceneComposerResolvedMovementSample[count];
            var diagnostics = new List<string>();
            string[] authoredWarnings =
                VnSceneComposerBeatCharacterStagingResolver.CollectWarnings(scene, beat);
            for (int w = 0; w < authoredWarnings.Length; w++)
            {
                if (!string.IsNullOrWhiteSpace(authoredWarnings[w]) &&
                    !diagnostics.Contains(authoredWarnings[w]))
                    diagnostics.Add(authoredWarnings[w]);
            }
            var visibleOrder = new int[count];
            int visibleCount = 0;

            for (int i = 0; i < count; i++)
            {
                visibleOrder[i] = -1;
                VnSceneComposerCharacter source = scene.characters[i];
                if (source == null)
                    throw new ArgumentException("Scene contains a null authored character entry.", nameof(scene));

                string characterId = VnSceneComposerBeatCharacterStateResolver.ResolveCharacterId(source);
                if (string.IsNullOrWhiteSpace(characterId))
                    throw new ArgumentException("Authored Scene character identity cannot be resolved.", nameof(scene));

                VnSceneComposerResolvedBeatCharacterStaging resolved =
                    VnSceneComposerBeatCharacterStagingResolver.Resolve(
                        scene, beat, characterId, beatElapsedSeconds, cancelledStagingIds);
                resolvedStaging[i] = resolved;
                if (resolved.Warnings != null)
                {
                    for (int w = 0; w < resolved.Warnings.Length; w++)
                    {
                        string warning = resolved.Warnings[w];
                        if (!string.IsNullOrWhiteSpace(warning) && !diagnostics.Contains(warning))
                            diagnostics.Add(warning);
                    }
                }

                if (applyMovement)
                {
                    resolvedMovement[i] = VnSceneComposerMovementResolver.Sample(
                        scene, beat, characterId, beatElapsedSeconds, stage,
                        frame.VirtualCanvasSize.x, cancelledStagingIds);
                }

                bool visible = applyMovement
                    ? resolvedMovement[i].Visible && resolvedMovement[i].Alpha > .0001f
                    : resolved.Visible;
                if (visible)
                    visibleOrder[i] = visibleCount++;
            }

            int activeVisibleIndex =
                activeSourceIndex >= 0 && activeSourceIndex < visibleOrder.Length
                    ? visibleOrder[activeSourceIndex]
                    : -1;
            var result = new VnWorkshopPreviewCharacter[count];

            for (int i = 0; i < count; i++)
            {
                VnSceneComposerCharacter source = scene.characters[i];
                VnSceneComposerResolvedBeatCharacterStaging resolved = resolvedStaging[i];
                string characterId = resolved.CharacterId;

                if (!VnSceneComposerCharacterStateResolver.TryResolve(
                        resolved.StateId, out VnSceneComposerResolvedCharacterState state))
                {
                    if (!VnSceneComposerCharacterStateResolver.TryResolve(
                            source.stateId, out state))
                        throw new ArgumentException(
                            "Unknown effective VN character state: " + resolved.StateId, nameof(scene));
                }

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

                VnSceneComposerResolvedMovementSample movement = resolvedMovement[i];
                VnWorkshopStageSlot effectiveSlot = applyMovement
                    ? movement.StageSlot
                    : resolved.StageSlot;
                Vector2 center = baseline.center;
                float scale;
                float alpha;
                bool visible;
                if (applyMovement)
                {
                    center.x = frame.VirtualCanvasSize.x * .5f + movement.StagePosition.x;
                    center.y += movement.StagePosition.y;
                    scale = movement.StageScale;
                    alpha = movement.Alpha;
                    visible = movement.Visible && alpha > .0001f;
                }
                else
                {
                    ResolveSlot(resolved.StageSlot, stage, out float xOffset, out float slotScale);
                    center.x = frame.VirtualCanvasSize.x * .5f + xOffset;
                    center.y += stage.SlotY;
                    if (resolved.HasPositionOffset) center += resolved.PositionOffset;
                    scale = slotScale;
                    alpha = resolved.Visible ? 1f : 0f;
                    visible = resolved.Visible;
                }

                float brightness = 1f;
                bool active = visible && activeSourceIndex == i;

                int currentVisibleIndex = visibleOrder[i];
                if (visible && visibleCount > 1 && activeVisibleIndex >= 0 &&
                    currentVisibleIndex >= 0)
                {
                    VnWorkshopSpeakerFocusSample sample = VnPresentationWorkshopVn10Resolver.SampleSpeakerFocus(
                        visibleCount, activeVisibleIndex, activeVisibleIndex, currentVisibleIndex, 1f, focus);
                    center += sample.PositionOffset;
                    scale *= sample.Scale;
                    alpha *= sample.Alpha;
                    brightness = sample.Brightness;
                }

                if (source.hasScaleMultiplier) scale *= source.scaleMultiplier;
                scale = Mathf.Max(.01f, scale);
                Rect body = RectFromCenter(center, baseline.size * scale);

                result[i] = new VnWorkshopPreviewCharacter
                {
                    CharacterId = characterId,
                    StateId = resolved.StateId,
                    Slot = effectiveSlot,
                    Texture = texture,
                    Uv = state.BodyUv,
                    Body = body,
                    Active = active,
                    Alpha = Mathf.Clamp01(alpha),
                    Brightness = Mathf.Max(0f, brightness)
                };
            }

            warnings = diagnostics.ToArray();
            return result;
        }

        private static VnWorkshopPreviewDecoration[] BuildDecorations(
            VnSceneComposerScene scene, out string[] warnings)
        {
            var visuals = new List<VnWorkshopPreviewDecoration>();
            var diagnostics = new List<string>();
            if (scene == null || scene.decorations == null)
            {
                warnings = Array.Empty<string>();
                return Array.Empty<VnWorkshopPreviewDecoration>();
            }

            for (int i = 0; i < scene.decorations.Count; i++)
            {
                VnSceneComposerDecoration source = scene.decorations[i];
                if (source == null) continue;
                string path = AssetDatabase.GUIDToAssetPath(source.assetGuid ?? string.Empty);
                Texture2D texture = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null)
                {
                    string label = string.IsNullOrWhiteSpace(source.displayName)
                        ? source.decorationId ?? "Decoration"
                        : source.displayName;
                    diagnostics.Add("Декорация '" + label +
                                    "': файл изображения не найден. Элемент сохранён, но не отображается.");
                    continue;
                }
                if (!source.visible) continue;

                float height = 320f * Mathf.Clamp(source.scale, .01f, 10f);
                float width = texture.height > 0 ? height * ((float)texture.width / texture.height) : height;
                visuals.Add(new VnWorkshopPreviewDecoration
                {
                    DecorationId = source.decorationId ?? string.Empty,
                    Texture = texture,
                    Body = RectFromCenter(source.position, new Vector2(width, height)),
                    Alpha = Mathf.Clamp01(source.opacity),
                    Layer = source.layer
                });
            }

            warnings = diagnostics.ToArray();
            return visuals.ToArray();
        }

        private static int FindActiveIndex(VnSceneComposerScene scene, VnSceneComposerDialogueBeat beat)
        {
            if (beat == null || beat.narration || string.IsNullOrEmpty(beat.speaker)) return -1;
            for (int i = 0; i < scene.characters.Count; i++)
            {
                VnSceneComposerCharacter character = scene.characters[i];
                if (character == null) continue;
                if (string.Equals(character.characterId, beat.speaker, StringComparison.OrdinalIgnoreCase)) return i;
                if (VnSceneComposerCharacterStateResolver.TryResolve(character.stateId, out VnSceneComposerResolvedCharacterState state) &&
                    string.Equals(state.Character, beat.speaker, StringComparison.OrdinalIgnoreCase)) return i;
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

        private static void ApplyGeometryScopes(
            VnPresentationWorkshopPreset target,
            VnPresentationWorkshopPreset defaults,
            VnSceneComposerScene scene)
        {
            if (target == null) return;
            VnPresentationWorkshopPreset source = defaults ?? new VnPresentationWorkshopPreset();

            // Dialogue plaque physical geometry is always project-global. A Scene may still
            // override dialoguePanelVisual (PNG), but never the plaque rect/scale.
            CopyElementOverride(target.dialoguePanel, source.dialoguePanel);

            // Text geometry defaults to project-global. Only the explicit new ThisScene scope
            // permits the Scene speaker/body element overrides to remain in the resolved preset.
            if (scene == null || scene.textGeometryScope != VnSceneComposerTextGeometryScope.ThisScene)
            {
                CopyElementOverride(target.speakerName, source.speakerName);
                CopyElementOverride(target.dialogueText, source.dialogueText);
            }
        }

        private static void CopyElementOverride(
            VnWorkshopElementOverride target, VnWorkshopElementOverride source)
        {
            if (target == null || source == null) return;
            target.hasPositionDelta = source.hasPositionDelta;
            target.positionDelta = source.positionDelta;
            target.hasSizeDelta = source.hasSizeDelta;
            target.sizeDelta = source.sizeDelta;
            target.hasScaleMultiplier = source.hasScaleMultiplier;
            target.scaleMultiplier = source.scaleMultiplier;
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
