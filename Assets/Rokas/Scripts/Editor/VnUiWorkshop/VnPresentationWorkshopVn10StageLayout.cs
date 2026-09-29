using System;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public enum VnWorkshopStageSlot
    {
        Left,
        Center,
        Right
    }

    [Serializable]
    public sealed class VnWorkshopStageLayoutOverride
    {
        public bool hasLeftX;
        public float leftX;
        public bool hasCenterX;
        public float centerX;
        public bool hasRightX;
        public float rightX;
        public bool hasSlotY;
        public float slotY;
        public bool hasLeftScale;
        public float leftScale;
        public bool hasCenterScale;
        public float centerScale;
        public bool hasRightScale;
        public float rightScale;
        public bool hasSpacing;
        public float spacing;
        public bool hasRepositionDuration;
        public float repositionDuration;
        public bool hasEasing;
        public VnWorkshopEasing easing;

        public bool HasAnyOverride
        {
            get
            {
                return hasLeftX || hasCenterX || hasRightX || hasSlotY || hasLeftScale || hasCenterScale ||
                       hasRightScale || hasSpacing || hasRepositionDuration || hasEasing;
            }
        }

        public void Clear()
        {
            hasLeftX = hasCenterX = hasRightX = hasSlotY = false;
            hasLeftScale = hasCenterScale = hasRightScale = false;
            hasSpacing = hasRepositionDuration = hasEasing = false;
            leftX = centerX = rightX = slotY = 0f;
            leftScale = centerScale = rightScale = 0f;
            spacing = repositionDuration = 0f;
            easing = VnWorkshopEasing.EaseInOut;
        }
    }

    public struct VnWorkshopStageLayoutValues
    {
        public float LeftX;
        public float CenterX;
        public float RightX;
        public float SlotY;
        public float LeftScale;
        public float CenterScale;
        public float RightScale;
        public float Spacing;
        public float RepositionDuration;
        public VnWorkshopEasing Easing;
    }

    public struct VnWorkshopStageTarget
    {
        public int CharacterIndex;
        public VnWorkshopStageSlot Slot;
        public Vector2 Position;
        public float Scale;
    }

    public struct VnWorkshopStageTransitionSample
    {
        public int CharacterIndex;
        public VnWorkshopStageSlot Slot;
        public Vector2 Position;
        public float Scale;
        public bool Visible;
        public bool Entering;
        public bool Exiting;
        public bool Complete;
    }

    public static partial class VnPresentationWorkshopVn10Resolver
    {
        private static readonly VnWorkshopStageLayoutValues StageLayoutBaseline = new VnWorkshopStageLayoutValues
        {
            LeftX = -360f,
            CenterX = 0f,
            RightX = 360f,
            SlotY = 0f,
            LeftScale = 1f,
            CenterScale = 1f,
            RightScale = 1f,
            Spacing = 280f,
            RepositionDuration = .35f,
            Easing = VnWorkshopEasing.EaseInOut
        };

        public static VnWorkshopStageLayoutValues ResolveStageLayout(VnPresentationWorkshopPreset preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            VnWorkshopStageLayoutOverride source = EnsureStageLayout(preset);
            VnWorkshopStageLayoutValues values = StageLayoutBaseline;
            if (source.hasLeftX) values.LeftX = source.leftX;
            if (source.hasCenterX) values.CenterX = source.centerX;
            if (source.hasRightX) values.RightX = source.rightX;
            if (source.hasSlotY) values.SlotY = source.slotY;
            if (source.hasLeftScale) values.LeftScale = source.leftScale;
            if (source.hasCenterScale) values.CenterScale = source.centerScale;
            if (source.hasRightScale) values.RightScale = source.rightScale;
            if (source.hasSpacing) values.Spacing = source.spacing;
            if (source.hasRepositionDuration) values.RepositionDuration = source.repositionDuration;
            if (source.hasEasing) values.Easing = source.easing;
            ValidateStageLayout(values);
            return values;
        }

        public static void SetStageLayoutPreviewOverrides(
            VnPresentationWorkshopPreset preset,
            float leftX,
            float centerX,
            float rightX,
            float slotY,
            float leftScale,
            float centerScale,
            float rightScale,
            float spacing,
            float repositionDuration,
            VnWorkshopEasing easing)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            var values = new VnWorkshopStageLayoutValues
            {
                LeftX = leftX,
                CenterX = centerX,
                RightX = rightX,
                SlotY = slotY,
                LeftScale = leftScale,
                CenterScale = centerScale,
                RightScale = rightScale,
                Spacing = spacing,
                RepositionDuration = repositionDuration,
                Easing = easing
            };
            ValidateStageLayout(values);

            VnWorkshopStageLayoutOverride target = EnsureStageLayout(preset);
            target.hasLeftX = !Mathf.Approximately(leftX, StageLayoutBaseline.LeftX); target.leftX = leftX;
            target.hasCenterX = !Mathf.Approximately(centerX, StageLayoutBaseline.CenterX); target.centerX = centerX;
            target.hasRightX = !Mathf.Approximately(rightX, StageLayoutBaseline.RightX); target.rightX = rightX;
            target.hasSlotY = !Mathf.Approximately(slotY, StageLayoutBaseline.SlotY); target.slotY = slotY;
            target.hasLeftScale = !Mathf.Approximately(leftScale, StageLayoutBaseline.LeftScale); target.leftScale = leftScale;
            target.hasCenterScale = !Mathf.Approximately(centerScale, StageLayoutBaseline.CenterScale); target.centerScale = centerScale;
            target.hasRightScale = !Mathf.Approximately(rightScale, StageLayoutBaseline.RightScale); target.rightScale = rightScale;
            target.hasSpacing = !Mathf.Approximately(spacing, StageLayoutBaseline.Spacing); target.spacing = spacing;
            target.hasRepositionDuration = !Mathf.Approximately(repositionDuration, StageLayoutBaseline.RepositionDuration); target.repositionDuration = repositionDuration;
            target.hasEasing = easing != StageLayoutBaseline.Easing; target.easing = easing;
        }

        public static void ResetStageLayoutPreviewOverrides(VnPresentationWorkshopPreset preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            EnsureStageLayout(preset).Clear();
        }

        public static VnWorkshopStageTarget[] ResolveStageTargets(int characterCount, VnWorkshopStageLayoutValues values)
        {
            ValidateStageCount(characterCount, nameof(characterCount));
            ValidateStageLayout(values);
            if (characterCount == 1)
                return new[] { MakeStageTarget(0, VnWorkshopStageSlot.Center, values) };
            if (characterCount == 2)
                return new[]
                {
                    MakeStageTarget(0, VnWorkshopStageSlot.Left, values),
                    MakeStageTarget(1, VnWorkshopStageSlot.Right, values)
                };
            return new[]
            {
                MakeStageTarget(0, VnWorkshopStageSlot.Left, values),
                MakeStageTarget(1, VnWorkshopStageSlot.Center, values),
                MakeStageTarget(2, VnWorkshopStageSlot.Right, values)
            };
        }

        public static VnWorkshopStageTransitionSample[] SampleStageTransition(
            int fromCharacterCount,
            int toCharacterCount,
            float normalizedProgress,
            VnWorkshopStageLayoutValues values)
        {
            ValidateStageCount(fromCharacterCount, nameof(fromCharacterCount));
            ValidateStageCount(toCharacterCount, nameof(toCharacterCount));
            ValidateStageLayout(values);
            VnWorkshopStageTarget[] from = ResolveStageTargets(fromCharacterCount, values);
            VnWorkshopStageTarget[] to = ResolveStageTargets(toCharacterCount, values);
            bool snap = values.RepositionDuration <= 0f;
            float raw = snap ? 1f : Mathf.Clamp01(normalizedProgress);
            float t = EvaluateStageEasing(raw, values.Easing);
            int count = Mathf.Max(fromCharacterCount, toCharacterCount);
            var result = new VnWorkshopStageTransitionSample[count];

            for (int i = 0; i < count; i++)
            {
                bool existed = i < fromCharacterCount;
                bool remains = i < toCharacterCount;
                VnWorkshopStageTarget start = existed ? from[i] : to[i];
                VnWorkshopStageTarget end = remains ? to[i] : from[i];
                result[i] = new VnWorkshopStageTransitionSample
                {
                    CharacterIndex = i,
                    Slot = remains ? end.Slot : start.Slot,
                    Position = Vector2.LerpUnclamped(start.Position, end.Position, t),
                    Scale = Mathf.LerpUnclamped(start.Scale, end.Scale, t),
                    Visible = existed && remains ? true : (remains ? raw > 0f : raw < 1f),
                    Entering = !existed && remains,
                    Exiting = existed && !remains,
                    Complete = snap || raw >= 1f
                };
            }
            return result;
        }

        private static VnWorkshopStageTarget MakeStageTarget(int index, VnWorkshopStageSlot slot, VnWorkshopStageLayoutValues values)
        {
            float x;
            float scale;
            switch (slot)
            {
                case VnWorkshopStageSlot.Left: x = values.LeftX; scale = values.LeftScale; break;
                case VnWorkshopStageSlot.Center: x = values.CenterX; scale = values.CenterScale; break;
                case VnWorkshopStageSlot.Right: x = values.RightX; scale = values.RightScale; break;
                default: throw new ArgumentOutOfRangeException(nameof(slot), slot, null);
            }
            return new VnWorkshopStageTarget
            {
                CharacterIndex = index,
                Slot = slot,
                Position = new Vector2(x, values.SlotY),
                Scale = scale
            };
        }

        private static VnWorkshopStageLayoutOverride EnsureStageLayout(VnPresentationWorkshopPreset preset)
        {
            if (preset.stageLayout == null)
                preset.stageLayout = new VnWorkshopStageLayoutOverride();
            return preset.stageLayout;
        }

        private static void ValidateStageLayout(VnWorkshopStageLayoutValues values)
        {
            RequireStageFiniteRange(values.LeftX, -2000f, 2000f, nameof(values.LeftX));
            RequireStageFiniteRange(values.CenterX, -2000f, 2000f, nameof(values.CenterX));
            RequireStageFiniteRange(values.RightX, -2000f, 2000f, nameof(values.RightX));
            RequireStageFiniteRange(values.SlotY, -2000f, 2000f, nameof(values.SlotY));
            RequireStageFiniteRange(values.LeftScale, .1f, 3f, nameof(values.LeftScale));
            RequireStageFiniteRange(values.CenterScale, .1f, 3f, nameof(values.CenterScale));
            RequireStageFiniteRange(values.RightScale, .1f, 3f, nameof(values.RightScale));
            RequireStageFiniteRange(values.Spacing, 0f, 2000f, nameof(values.Spacing));
            RequireStageFiniteRange(values.RepositionDuration, 0f, 10f, nameof(values.RepositionDuration));
            if (!Enum.IsDefined(typeof(VnWorkshopEasing), values.Easing))
                throw new ArgumentOutOfRangeException(nameof(values.Easing), values.Easing, null);
            if (values.CenterX - values.LeftX < values.Spacing || values.RightX - values.CenterX < values.Spacing)
                throw new ArgumentException("Stage slots must preserve configured spacing and may not overlap.");
        }

        private static void ValidateStageCount(int count, string name)
        {
            if (count < 1 || count > 3)
                throw new ArgumentOutOfRangeException(name, count, "Workshop staging supports one to three characters.");
        }

        private static void RequireStageFiniteRange(float value, float min, float max, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max)
                throw new ArgumentOutOfRangeException(name, value, null);
        }

        private static float EvaluateStageEasing(float value, VnWorkshopEasing easing)
        {
            float t = Mathf.Clamp01(value);
            switch (easing)
            {
                case VnWorkshopEasing.Linear: return t;
                case VnWorkshopEasing.EaseIn: return t * t;
                case VnWorkshopEasing.EaseOut: return 1f - ((1f - t) * (1f - t));
                case VnWorkshopEasing.EaseInOut: return t * t * (3f - (2f * t));
                default: throw new ArgumentOutOfRangeException(nameof(easing), easing, null);
            }
        }
    }
}
