using System;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public static partial class VnPresentationWorkshopVn10Resolver
    {
        public static VnWorkshopExpressionTransitionSample SampleComposerExpressionTransition(
            string startStateId,
            string endStateId,
            float normalizedProgress,
            VnWorkshopExpressionTransitionValues values)
        {
            if (!VnSceneComposerCharacterStateResolver.TryResolve(startStateId, out VnSceneComposerResolvedCharacterState start))
                throw new ArgumentException("Unknown authored VN character visual state: " + (startStateId ?? string.Empty), nameof(startStateId));
            if (!VnSceneComposerCharacterStateResolver.TryResolve(endStateId, out VnSceneComposerResolvedCharacterState end))
                throw new ArgumentException("Unknown authored VN character visual state: " + (endStateId ?? string.Empty), nameof(endStateId));
            if (!string.Equals(start.Character, end.Character, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Expression transition states must belong to the same authored character.");
            ValidateEnum(values.Easing, nameof(values.Easing));
            float raw = Mathf.Clamp01(normalizedProgress);
            float eased = EvaluateEasing(raw, values.Easing);
            return new VnWorkshopExpressionTransitionSample
            {
                StartStateId = start.Id,
                EndStateId = end.Id,
                StartAlpha = 1f - eased,
                EndAlpha = eased,
                Complete = raw >= 1f
            };
        }
    }
}
