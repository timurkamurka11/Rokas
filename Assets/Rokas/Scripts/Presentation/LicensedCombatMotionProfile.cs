using System;
using UnityEngine;

namespace Rokas.Presentation
{
    [Serializable]
    public sealed class LicensedMotionSegment
    {
        public AnimationClip clip;
        public float start, duration, clipIn, speed = 1f, easeIn, easeOut;
        public float blendIn = -1f, blendOut = -1f;
        public AnimationCurve mixIn = AnimationCurve.Linear(0, 0, 1, 1);
        public AnimationCurve mixOut = AnimationCurve.Linear(0, 1, 1, 0);

        public float Weight(float time) => Weight(time,
            blendIn > 0 ? blendIn : easeIn, blendOut > 0 ? blendOut : easeOut);

        public float Weight(float time, float inDuration, float outDuration)
        {
            if (time < start || time >= start + duration || clip == null) return 0;
            float elapsed = time - start;
            float weight = 1;
            if (inDuration > 0 && elapsed < inDuration)
                weight *= Mathf.Clamp01(mixIn.Evaluate(elapsed / inDuration));
            if (outDuration > 0 && elapsed > duration - outDuration)
                weight *= Mathf.Clamp01(mixOut.Evaluate((elapsed - duration + outDuration) / outDuration));
            return weight;
        }

        public float SampleTime(float time) => Mathf.Clamp(clipIn + (time - start) * speed, 0,
            clip == null ? 0 : clip.length);
    }

    public enum LicensedWeaponKind { None, Sword, TwoHandedSword, Dagger, Pistol }

    [Serializable]
    public sealed class LicensedMotionCue
    {
        public string name;
        public float time, duration;
    }

    public enum LicensedCameraFramingSize { Medium, Short, Large }

    [Serializable]
    public sealed class LicensedCameraFramingOffset
    {
        public Vector3 medium, largeTarget, largePerformer, shortTarget, shortTargetEuler;
    }

    [Serializable]
    public sealed class LicensedCameraData
    {
        public float baseFov = 38, shotFov = 18, shotDuration = 1.3833333f, blendOut = .3666667f;
        public Vector3 parentPosition = new Vector3(0, 0, -6.4f);
        public Vector3 parentEuler;
        public Vector3 rootPosition = new Vector3(0, 0, -6f);
        public LicensedCameraFramingSize performerFramingSize;
        public LicensedCameraFramingOffset[] framingOffsets = Array.Empty<LicensedCameraFramingOffset>();

        public void ResolveParentPose(int externalTargets, LicensedCameraFramingSize targetSize,
            out Vector3 position, out Vector3 euler)
        {
            position = parentPosition;
            euler = parentEuler;
            if (framingOffsets == null || framingOffsets.Length == 0) return;
            LicensedCameraFramingOffset offset = framingOffsets[Mathf.Clamp(externalTargets, 0, framingOffsets.Length - 1)];
            Vector3 translation;
            if (performerFramingSize == LicensedCameraFramingSize.Large) translation = offset.largePerformer;
            else if (targetSize == LicensedCameraFramingSize.Short)
            {
                translation = offset.shortTarget;
                euler += offset.shortTargetEuler;
            }
            else if (targetSize == LicensedCameraFramingSize.Large) translation = offset.largeTarget;
            else translation = offset.medium;
            position = rootPosition + translation;
        }

        public Vector3 basePosition = new Vector3(0, .74f, -8.3f);
        public AnimationCurve x = AnimationCurve.Constant(0, 2, 0);
        public AnimationCurve y = AnimationCurve.Constant(0, 2, .6f);
        public AnimationCurve z = AnimationCurve.Constant(0, 2, -7.7001f);
        public AnimationCurve pitch = AnimationCurve.Constant(0, 2, -3.301f);
        public AnimationCurve yaw = AnimationCurve.Constant(0, 2, 0);
        public AnimationCurve roll = AnimationCurve.Constant(0, 2, 4.83f);
        public AnimationCurve mixOut = new AnimationCurve(new Keyframe(0, 1, -2, -2), new Keyframe(1, 0, 0, 0));
        public AnimationCurve shakeX = new AnimationCurve();
        public AnimationCurve shakeY = new AnimationCurve();
        public AnimationCurve shakeZ = new AnimationCurve();
        public float shakeDuration = .6f;
        public bool routeShakeToTimelineActiveCamera;
        public LicensedCameraShakeSegment[] shakes = Array.Empty<LicensedCameraShakeSegment>();
        public string sourceBinding, calibration;

        public float Weight(float time)
        {
            if (time < 0 || time >= shotDuration) return 0;
            if (blendOut <= 0 || time < shotDuration - blendOut) return 1;
            return Mathf.Clamp01(mixOut.Evaluate((time - shotDuration + blendOut) / blendOut));
        }
    }

    public sealed class LicensedCombatMotionProfile : ScriptableObject
    {
        public string sourceSkill, sourceTimeline, sourceCharacter;
        public string[] sourceAssetIdentifiers;
        public AnimationClip anticipation, baseIdle;
        public float idleSpeed = 1, anticipationSpeed = 1;
        public LicensedMotionSegment[] segments = Array.Empty<LicensedMotionSegment>();
        public LicensedWeaponKind weapon;
        public bool authoredWeaponSocket;
        public float sourceToTargetScale = 1f;
        public LicensedMotionCue[] cues = Array.Empty<LicensedMotionCue>();
        public LicensedCameraData camera;

        public float Duration
        {
            get
            {
                float end = 0;
                foreach (LicensedMotionSegment segment in segments)
                    if (segment != null) end = Mathf.Max(end, segment.start + segment.duration);
                return end;
            }
        }
    }
}
