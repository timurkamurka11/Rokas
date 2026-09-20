using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Rokas.Presentation;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed class VnSceneComposerCharacterMotionPreview
    {
        public string characterId = string.Empty;
        public bool entering;
        public bool exiting;
        public VnWorkshopCharacterTransitionSample sample;
    }

    public sealed class VnSceneComposerExpressionPreview
    {
        public string characterId = string.Empty;
        public VnWorkshopExpressionTransitionSample sample;
    }

    public sealed class VnSceneComposerPreviewTimingPlan
    {
        public bool usesPreviewAutoDuration;
        public float previewAutoDuration;
        public float typewriterDuration;
        public float settleDuration;
        public float breathingRoom;
        public float sequenceGap;
    }

    public sealed class VnSceneComposerDialogueRevealSample
    {
        public string FullText { get; internal set; } = string.Empty;
        public string PlainVisibleText { get; internal set; } = string.Empty;
        public string RenderText { get; internal set; } = string.Empty;
        public int VisibleGlyphCount { get; internal set; }
        public int TotalGlyphCount { get; internal set; }
        public float NewestGlyphAlpha { get; internal set; }
        public float DurationSeconds { get; internal set; }
        public bool Complete { get; internal set; }
    }

    public static class VnSceneComposerDialogueReveal
    {
        private const float GlyphFadeSeconds = .04f;

        private sealed class RevealToken
        {
            public string text = string.Empty;
            public int glyphIndex = -1;
        }

        public static VnSceneComposerDialogueRevealSample Sample(
            string text,
            float elapsedSeconds,
            VnWorkshopTypewriterValues values,
            bool forceComplete)
        {
            string authored = text ?? string.Empty;
            if (float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds))
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            elapsedSeconds = Mathf.Max(0f, elapsedSeconds);

            List<string> glyphs;
            List<RevealToken> tokens = Tokenize(authored, out glyphs);
            int total = glyphs.Count;
            float duration = CalculateDuration(glyphs, values);

            if (total == 0)
            {
                return new VnSceneComposerDialogueRevealSample
                {
                    FullText = authored,
                    PlainVisibleText = string.Empty,
                    RenderText = authored,
                    VisibleGlyphCount = 0,
                    TotalGlyphCount = 0,
                    NewestGlyphAlpha = 1f,
                    DurationSeconds = 0f,
                    Complete = true
                };
            }

            if (forceComplete || !values.Enabled)
            {
                return new VnSceneComposerDialogueRevealSample
                {
                    FullText = authored,
                    PlainVisibleText = JoinGlyphs(glyphs, total),
                    RenderText = authored,
                    VisibleGlyphCount = total,
                    TotalGlyphCount = total,
                    NewestGlyphAlpha = 1f,
                    DurationSeconds = duration,
                    Complete = true
                };
            }

            float characterDelay =
                1f / Mathf.Max(.0001f, values.CharactersPerSecond) +
                Mathf.Max(0f, values.BaseCharacterDelay);
            float clock = Mathf.Max(0f, values.LineStartDelay);
            int visible = 0;
            float newestRevealTime = -1f;

            for (int i = 0; i < total; i++)
            {
                clock += characterDelay;
                if (elapsedSeconds + .00001f < clock) break;
                visible = i + 1;
                newestRevealTime = clock;
                clock += GetPunctuationPause(glyphs, i, values);
            }

            float newestAlpha = visible <= 0
                ? 0f
                : Mathf.Clamp01((elapsedSeconds - newestRevealTime) / GlyphFadeSeconds);
            bool complete = visible >= total && elapsedSeconds + .00001f >= duration;
            if (complete) newestAlpha = 1f;

            return new VnSceneComposerDialogueRevealSample
            {
                FullText = authored,
                PlainVisibleText = JoinGlyphs(glyphs, visible),
                RenderText = BuildRenderText(tokens, visible, newestAlpha, complete),
                VisibleGlyphCount = visible,
                TotalGlyphCount = total,
                NewestGlyphAlpha = newestAlpha,
                DurationSeconds = duration,
                Complete = complete
            };
        }

        public static float CalculateDuration(
            string text,
            VnWorkshopTypewriterValues values)
        {
            List<string> glyphs;
            Tokenize(text ?? string.Empty, out glyphs);
            return CalculateDuration(glyphs, values);
        }

        private static float CalculateDuration(
            List<string> glyphs,
            VnWorkshopTypewriterValues values)
        {
            if (glyphs == null || glyphs.Count == 0 || !values.Enabled) return 0f;
            float characterDelay =
                1f / Mathf.Max(.0001f, values.CharactersPerSecond) +
                Mathf.Max(0f, values.BaseCharacterDelay);
            float duration = Mathf.Max(0f, values.LineStartDelay);
            for (int i = 0; i < glyphs.Count; i++)
                duration += characterDelay + GetPunctuationPause(glyphs, i, values);
            return duration + GlyphFadeSeconds;
        }

        private static List<RevealToken> Tokenize(
            string text,
            out List<string> glyphs)
        {
            glyphs = new List<string>();
            var tokens = new List<RevealToken>();
            int cursor = 0;

            while (cursor < text.Length)
            {
                if (text[cursor] == '<')
                {
                    int close = text.IndexOf('>', cursor + 1);
                    if (close >= 0)
                    {
                        tokens.Add(new RevealToken
                        {
                            text = text.Substring(cursor, close - cursor + 1),
                            glyphIndex = -1
                        });
                        cursor = close + 1;
                        continue;
                    }
                }

                int nextTag = text.IndexOf('<', cursor);
                int segmentEnd = nextTag >= 0 ? nextTag : text.Length;
                if (segmentEnd <= cursor) segmentEnd = cursor + 1;
                string segment = text.Substring(cursor, segmentEnd - cursor);
                int[] elementStarts = StringInfo.ParseCombiningCharacters(segment);
                for (int i = 0; i < elementStarts.Length; i++)
                {
                    int start = elementStarts[i];
                    int end = i + 1 < elementStarts.Length
                        ? elementStarts[i + 1]
                        : segment.Length;
                    string glyph = segment.Substring(start, end - start);
                    int index = glyphs.Count;
                    glyphs.Add(glyph);
                    tokens.Add(new RevealToken { text = glyph, glyphIndex = index });
                }
                cursor = segmentEnd;
            }

            return tokens;
        }

        private static string JoinGlyphs(List<string> glyphs, int count)
        {
            if (glyphs == null || glyphs.Count == 0 || count <= 0)
                return string.Empty;
            int safe = Mathf.Clamp(count, 0, glyphs.Count);
            var builder = new StringBuilder();
            for (int i = 0; i < safe; i++) builder.Append(glyphs[i]);
            return builder.ToString();
        }

        private static string BuildRenderText(
            List<RevealToken> tokens,
            int visible,
            float newestAlpha,
            bool complete)
        {
            if (tokens == null || tokens.Count == 0) return string.Empty;
            var builder = new StringBuilder();
            int newest = visible - 1;

            for (int i = 0; i < tokens.Count; i++)
            {
                RevealToken token = tokens[i];
                if (token.glyphIndex < 0)
                {
                    builder.Append(token.text);
                    continue;
                }

                if (complete || token.glyphIndex < newest)
                {
                    builder.Append(token.text);
                    continue;
                }

                float alpha = token.glyphIndex == newest
                    ? newestAlpha
                    : 0f;
                if (alpha >= .999f)
                {
                    builder.Append(token.text);
                    continue;
                }

                int alphaByte = Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255);
                builder.Append("<color=#FFFFFF");
                builder.Append(alphaByte.ToString("X2"));
                builder.Append(">");
                builder.Append(token.text);
                builder.Append("</color>");
            }

            return builder.ToString();
        }

        private static float GetPunctuationPause(
            List<string> glyphs,
            int index,
            VnWorkshopTypewriterValues values)
        {
            if (glyphs == null || index < 0 || index >= glyphs.Count) return 0f;
            string glyph = glyphs[index];
            if (glyph == ",") return Mathf.Max(0f, values.CommaPause);
            if (glyph == "…") return Mathf.Max(0f, values.EllipsisPause);
            if (glyph == "?") return Mathf.Max(0f, values.QuestionPause);
            if (glyph == "!") return Mathf.Max(0f, values.ExclamationPause);
            if (glyph != ".") return 0f;

            bool previousDot = index > 0 && glyphs[index - 1] == ".";
            bool nextDot = index + 1 < glyphs.Count && glyphs[index + 1] == ".";
            if (!previousDot && !nextDot) return Mathf.Max(0f, values.PeriodPause);
            bool terminalEllipsis =
                index >= 2 &&
                glyphs[index - 1] == "." &&
                glyphs[index - 2] == "." &&
                !nextDot;
            return terminalEllipsis ? Mathf.Max(0f, values.EllipsisPause) : 0f;
        }
    }

    public sealed class VnSceneComposerTransitionSnapshot
    {
        public VnWorkshopBackgroundTransitionSample background;
        public VnWorkshopActionBounceSample bounce;
        public string beatEffectCharacterId = string.Empty;
        public VnWorkshopActionBounceSample beatEffect;
        public VnWorkshopStageTransitionSample[] stage = Array.Empty<VnWorkshopStageTransitionSample>();
        public VnSceneComposerCharacterMotionPreview[] characterMotions = Array.Empty<VnSceneComposerCharacterMotionPreview>();
        public VnSceneComposerExpressionPreview[] expressions = Array.Empty<VnSceneComposerExpressionPreview>();
        public VnWorkshopSpeakerFocusSample[] focus = Array.Empty<VnWorkshopSpeakerFocusSample>();
        public string visibleText = string.Empty;
        public VnSceneComposerDialogueRevealSample dialogueReveal;
        public VnSceneComposerPreviewTimingPlan timing;
    }

    public static class VnSceneComposerTransitionSampler
    {
        public static VnSceneComposerTransitionSnapshot Sample(
            VnSceneComposerProject project,
            VnSceneComposerScene fromScene,
            VnSceneComposerScene toScene,
            float normalizedProgress)
        {
            return Sample(project, fromScene, toScene,
                ResolveFirstBeat(fromScene), ResolveFirstBeat(toScene), normalizedProgress);
        }

        public static VnSceneComposerTransitionSnapshot Sample(
            VnSceneComposerProject project,
            VnSceneComposerScene fromScene,
            VnSceneComposerScene toScene,
            VnSceneComposerDialogueBeat fromBeat,
            VnSceneComposerDialogueBeat toBeat,
            float normalizedProgress)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (fromScene == null) throw new ArgumentNullException(nameof(fromScene));
            if (toScene == null) throw new ArgumentNullException(nameof(toScene));
            if (fromBeat == null) throw new ArgumentNullException(nameof(fromBeat));
            if (toBeat == null) throw new ArgumentNullException(nameof(toBeat));

            float progress = Mathf.Clamp01(normalizedProgress);
            VnPresentationWorkshopPreset preset = VnSceneComposerComposition.ResolvePresentation(project, toScene);

            VnWorkshopBackgroundTransitionValues backgroundValues =
                VnPresentationWorkshopVn10Resolver.ResolveBackgroundTransition(preset);
            VnWorkshopActionBounceValues bounceValues =
                VnPresentationWorkshopVn10Resolver.ResolveActionBounce(preset);
            VnWorkshopStageLayoutValues stageValues =
                VnPresentationWorkshopVn10Resolver.ResolveStageLayout(preset);
            VnWorkshopCharacterTransitionValues characterValues =
                VnPresentationWorkshopVn10Resolver.ResolveCharacterTransition(preset);
            VnWorkshopExpressionTransitionValues expressionValues =
                VnPresentationWorkshopVn10Resolver.ResolveExpressionTransition(preset);
            VnWorkshopSpeakerFocusValues focusValues =
                VnPresentationWorkshopVn10Resolver.ResolveSpeakerFocus(preset);
            VnWorkshopTypewriterValues typewriterValues =
                VnPresentationWorkshopVn10Resolver.ResolveTypewriter(preset);

            string text = toBeat.text ?? string.Empty;
            float typewriterDuration =
                VnSceneComposerDialogueReveal.CalculateDuration(text, typewriterValues);
            VnSceneComposerDialogueRevealSample dialogueReveal =
                VnSceneComposerDialogueReveal.Sample(
                    text, typewriterDuration * progress, typewriterValues, false);

            return new VnSceneComposerTransitionSnapshot
            {
                background = VnPresentationWorkshopVn10Resolver.SampleBackgroundTransition(
                    SnapProgress(progress, backgroundValues.Duration), backgroundValues),
                bounce = VnPresentationWorkshopVn10Resolver.SampleActionBounce(
                    toScene.transition != null && toScene.transition.triggerActionBounce,
                    SnapProgress(progress, bounceValues.Duration),
                    bounceValues),
                stage = SampleStage(CountCharacters(fromScene), CountCharacters(toScene), progress, stageValues),
                characterMotions = SampleCharacterMotions(fromScene, toScene, progress, characterValues),
                expressions = SampleExpressions(fromScene, toScene, progress, expressionValues),
                focus = SampleFocus(toScene, fromBeat, toBeat, progress, focusValues),
                visibleText = dialogueReveal.PlainVisibleText,
                dialogueReveal = dialogueReveal,
                timing = ResolveTiming(project, toScene, toBeat)
            };
        }

        public static VnSceneComposerPreviewTimingPlan ResolveTiming(
            VnSceneComposerProject project,
            VnSceneComposerScene scene)
        {
            return ResolveTiming(project, scene, ResolveFirstBeat(scene));
        }

        public static VnSceneComposerPreviewTimingPlan ResolveTiming(
            VnSceneComposerProject project,
            VnSceneComposerScene scene,
            VnSceneComposerDialogueBeat beat)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            if (beat == null) throw new ArgumentNullException(nameof(beat));

            VnPresentationWorkshopPreset preset = VnSceneComposerComposition.ResolvePresentation(project, scene);
            VnWorkshopTypewriterValues typewriter = VnPresentationWorkshopVn10Resolver.ResolveTypewriter(preset);
            VnWorkshopTimingValues timing = VnPresentationWorkshopVn10Resolver.ResolveTiming(preset);
            bool auto = scene.timing != null &&
                        scene.timing.previewAdvanceMode == VnSceneComposerPreviewAdvanceMode.PreviewAutoDuration;

            return new VnSceneComposerPreviewTimingPlan
            {
                usesPreviewAutoDuration = auto,
                previewAutoDuration = auto ? Mathf.Max(0f, scene.timing.previewAutoDuration) : 0f,
                typewriterDuration = VnSceneComposerDialogueReveal.CalculateDuration(
                    beat.text ?? string.Empty, typewriter),
                settleDuration = Mathf.Max(0f, timing.MinimumBeatSettleDuration),
                breathingRoom = Mathf.Max(0f, timing.PostTransitionBreathingRoom),
                sequenceGap = Mathf.Max(0f, timing.AutoPreviewSequenceGap)
            };
        }

        private static VnWorkshopStageTransitionSample[] SampleStage(
            int fromCount,
            int toCount,
            float progress,
            VnWorkshopStageLayoutValues values)
        {
            if (fromCount == 0 && toCount == 0)
                return Array.Empty<VnWorkshopStageTransitionSample>();

            if (fromCount > 0 && toCount > 0)
                return VnPresentationWorkshopVn10Resolver.SampleStageTransition(fromCount, toCount, progress, values);

            bool snap = values.RepositionDuration <= 0f;
            float raw = snap ? 1f : Mathf.Clamp01(progress);
            int count = Math.Max(fromCount, toCount);
            VnWorkshopStageTarget[] targets = VnPresentationWorkshopVn10Resolver.ResolveStageTargets(count, values);
            var result = new VnWorkshopStageTransitionSample[count];
            bool entering = fromCount == 0;
            for (int i = 0; i < count; i++)
            {
                VnWorkshopStageTarget target = targets[i];
                result[i] = new VnWorkshopStageTransitionSample
                {
                    CharacterIndex = i,
                    Slot = target.Slot,
                    Position = target.Position,
                    Scale = target.Scale,
                    Visible = entering ? raw > 0f : raw < 1f,
                    Entering = entering,
                    Exiting = !entering,
                    Complete = snap || raw >= 1f
                };
            }
            return result;
        }

        private static VnSceneComposerCharacterMotionPreview[] SampleCharacterMotions(
            VnSceneComposerScene fromScene,
            VnSceneComposerScene toScene,
            float progress,
            VnWorkshopCharacterTransitionValues values)
        {
            var result = new List<VnSceneComposerCharacterMotionPreview>();
            List<VnSceneComposerCharacter> from = fromScene.characters ?? new List<VnSceneComposerCharacter>();
            List<VnSceneComposerCharacter> to = toScene.characters ?? new List<VnSceneComposerCharacter>();
            float sampledProgress = SnapProgress(progress, values.Duration);

            for (int i = 0; i < to.Count; i++)
            {
                VnSceneComposerCharacter character = to[i];
                string id = ResolveCharacterId(character);
                if (FindCharacter(from, id) >= 0) continue;
                result.Add(new VnSceneComposerCharacterMotionPreview
                {
                    characterId = id,
                    entering = true,
                    sample = VnPresentationWorkshopVn10Resolver.SampleCharacterEnter(sampledProgress, values)
                });
            }

            for (int i = 0; i < from.Count; i++)
            {
                VnSceneComposerCharacter character = from[i];
                string id = ResolveCharacterId(character);
                if (FindCharacter(to, id) >= 0) continue;
                result.Add(new VnSceneComposerCharacterMotionPreview
                {
                    characterId = id,
                    exiting = true,
                    sample = VnPresentationWorkshopVn10Resolver.SampleCharacterExit(sampledProgress, values)
                });
            }
            return result.ToArray();
        }

        private static VnSceneComposerExpressionPreview[] SampleExpressions(
            VnSceneComposerScene fromScene,
            VnSceneComposerScene toScene,
            float progress,
            VnWorkshopExpressionTransitionValues values)
        {
            var result = new List<VnSceneComposerExpressionPreview>();
            List<VnSceneComposerCharacter> from = fromScene.characters ?? new List<VnSceneComposerCharacter>();
            List<VnSceneComposerCharacter> to = toScene.characters ?? new List<VnSceneComposerCharacter>();
            float sampledProgress = SnapProgress(progress, values.Duration);

            for (int i = 0; i < to.Count; i++)
            {
                VnSceneComposerCharacter target = to[i];
                string id = ResolveCharacterId(target);
                int previousIndex = FindCharacter(from, id);
                if (previousIndex < 0) continue;
                VnSceneComposerCharacter previous = from[previousIndex];
                if (string.Equals(previous.stateId, target.stateId, StringComparison.Ordinal)) continue;
                result.Add(new VnSceneComposerExpressionPreview
                {
                    characterId = id,
                    sample = VnPresentationWorkshopVn10Resolver.SampleComposerExpressionTransition(
                        previous.stateId, target.stateId, sampledProgress, values)
                });
            }
            return result.ToArray();
        }

        private static VnWorkshopSpeakerFocusSample[] SampleFocus(
            VnSceneComposerScene scene,
            VnSceneComposerDialogueBeat fromBeat,
            VnSceneComposerDialogueBeat toBeat,
            float progress,
            VnWorkshopSpeakerFocusValues values)
        {
            int count = CountCharacters(scene);
            if (count == 0) return Array.Empty<VnWorkshopSpeakerFocusSample>();
            if (count == 1)
            {
                return new[]
                {
                    VnPresentationWorkshopVn10Resolver.SampleSpeakerFocus(1, 0, 0, 0, progress, values)
                };
            }

            int previous = FindSpeakerIndex(scene, EffectiveSpeaker(fromBeat));
            int active = FindSpeakerIndex(scene, EffectiveSpeaker(toBeat));
            if (previous < 0 && active < 0)
            {
                var neutral = new VnWorkshopSpeakerFocusSample[count];
                for (int i = 0; i < count; i++)
                {
                    neutral[i] = new VnWorkshopSpeakerFocusSample
                    {
                        PositionOffset = Vector2.zero,
                        Scale = 1f,
                        Brightness = 1f,
                        Alpha = 1f,
                        Complete = true
                    };
                }
                return neutral;
            }
            if (previous < 0) previous = active;
            if (active < 0) active = previous;

            var result = new VnWorkshopSpeakerFocusSample[count];
            for (int i = 0; i < count; i++)
                result[i] = VnPresentationWorkshopVn10Resolver.SampleSpeakerFocus(
                    count, previous, active, i, progress, values);
            return result;
        }

        private static int FindSpeakerIndex(VnSceneComposerScene scene, string speaker)
        {
            if (scene == null || scene.characters == null || string.IsNullOrEmpty(speaker)) return -1;
            for (int i = 0; i < scene.characters.Count; i++)
            {
                string id = ResolveCharacterId(scene.characters[i]);
                if (string.Equals(id, speaker, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        private static int FindCharacter(List<VnSceneComposerCharacter> characters, string characterId)
        {
            for (int i = 0; i < characters.Count; i++)
            {
                if (string.Equals(ResolveCharacterId(characters[i]), characterId, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private static string ResolveCharacterId(VnSceneComposerCharacter character)
        {
            if (character == null) return string.Empty;
            if (!string.IsNullOrEmpty(character.characterId)) return character.characterId;
            if (!string.IsNullOrEmpty(character.stateId) &&
                VnSceneComposerCharacterStateResolver.TryResolve(character.stateId, out VnSceneComposerResolvedCharacterState state))
                return state.Character;
            return string.Empty;
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

        private static string EffectiveSpeaker(VnSceneComposerDialogueBeat beat)
        {
            return beat == null || beat.narration ? string.Empty : beat.speaker ?? string.Empty;
        }

        private static int CountCharacters(VnSceneComposerScene scene)
        {
            int count = scene != null && scene.characters != null ? scene.characters.Count : 0;
            if (count < 0 || count > 3)
                throw new ArgumentOutOfRangeException(nameof(scene), count,
                    "Scene Composer supports zero to three visible authored characters.");
            return count;
        }

        private static float SnapProgress(float progress, float duration)
        {
            return duration <= 0f ? 1f : Mathf.Clamp01(progress);
        }
    }
}
