using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rokas.Presentation
{
    // Core authorizes contact. This sampler owns only the visual sequence.
    public sealed class LicensedCombatMotionPlayer
    {
        private const int ContactHistoryLimit = 32;
        private readonly Animation animation;
        private readonly AnimationClip fallbackIdle;
        private readonly List<string> aliases = new List<string>();
        private readonly HashSet<string> consumedContacts = new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> contactHistory = new Queue<string>();
        private float[] blendIn, blendOut;
        private string actionId, anticipationAlias;
        private double clockSeconds;
        private float clock;
        private bool contacted;

        public LicensedCombatMotionProfile Profile { get; private set; }
        public bool Active => Profile != null;
        public bool Contacted => contacted && Active;
        public float Clock => clock;
        public bool StageRecoveryReached => Contacted && clockSeconds >= Profile.StageRecoveryTime;

        public bool ReachesStageRecovery(float delta, out float remainingStep)
        {
            remainingStep = 0f;
            if (!Contacted) return false;
            double until = Math.Max(0d, Profile.StageRecoveryTime - clockSeconds);
            if (delta < until) return false;
            remainingStep = Mathf.Max(0f, (float)(delta - until));
            return true;
        }

        public LicensedCombatMotionPlayer(Animation animation, AnimationClip fallbackIdle)
        {
            this.animation = animation ?? throw new ArgumentNullException(nameof(animation));
            this.fallbackIdle = fallbackIdle;
        }

        public void Begin(LicensedCombatMotionProfile profile, string id, string alias)
        {
            Cancel();
            if (profile == null) return;
            Profile = profile;
            actionId = id;
            anticipationAlias = alias;
            blendIn = new float[profile.segments.Length];
            blendOut = new float[profile.segments.Length];
            for (int i = 0; i < profile.segments.Length; i++)
            {
                LicensedMotionSegment s = profile.segments[i];
                blendIn[i] = s.blendIn > 0 ? s.blendIn : s.easeIn;
                blendOut[i] = s.blendOut > 0 ? s.blendOut : s.easeOut;
                if (i > 0 && s.blendIn < 0)
                {
                    LicensedMotionSegment previous = profile.segments[i - 1];
                    float overlap = previous.start + previous.duration - s.start;
                    if (overlap > 0) blendIn[i] = Mathf.Min(s.duration, overlap);
                }
                if (i + 1 < profile.segments.Length && s.blendOut < 0)
                {
                    float overlap = s.start + s.duration - profile.segments[i + 1].start;
                    if (overlap > 0) blendOut[i] = Mathf.Min(s.duration, overlap);
                }
            }
            Register(profile.anticipation, alias);
            Register(profile.baseIdle ?? fallbackIdle, "Licensed_BaseIdle");
            for (int i = 0; i < profile.segments.Length; i++)
                Register(profile.segments[i].clip, "Licensed_Segment_" + i);
            Sample();
        }

        // A preplayed presentation may acquire its domain ID after approach or release.
        public void BindContact(string id)
        {
            if (Active && !contacted) actionId = id;
        }

        private void Register(AnimationClip clip, string alias)
        {
            if (clip == null) return;
            if (!clip.legacy)
                throw new InvalidOperationException("Licensed combat clip must be Legacy: " + clip.name);
            animation.AddClip(clip, alias);
            animation[alias].wrapMode = WrapMode.ClampForever;
            aliases.Add(alias);
        }

        public bool ConfirmContact(string id)
        {
            if (!Active || contacted || string.IsNullOrEmpty(id) ||
                !string.Equals(id, actionId, StringComparison.Ordinal) || !consumedContacts.Add(id)) return false;
            contactHistory.Enqueue(id);
            while (contactHistory.Count > ContactHistoryLimit) consumedContacts.Remove(contactHistory.Dequeue());
            contacted = true;
            clockSeconds = 0;
            clock = 0;
            Sample();
            return true;
        }

        public void Tick(float delta)
        {
            if (!Active) return;
            if (delta > 0f && !float.IsNaN(delta) && !float.IsInfinity(delta)) clockSeconds += delta;
            clock = (float)clockSeconds;
            if (contacted && clock >= Profile.Duration) { Cancel(); return; }
            Sample();
        }

        public void Sample()
        {
            if (!Active) return;
            foreach (AnimationState state in animation) { state.enabled = false; state.weight = 0; }
            animation.enabled = false;
            if (!contacted)
            {
                Enable(anticipationAlias, Mathf.Min(clock * Profile.anticipationSpeed,
                    Profile.anticipation == null ? 0 : Profile.anticipation.length), 1);
            }
            else
            {
                float sum = 0;
                for (int i = 0; i < Profile.segments.Length; i++)
                    sum += Profile.segments[i].Weight(clock, blendIn[i], blendOut[i]);
                float normalization = Mathf.Max(1, sum);
                for (int i = 0; i < Profile.segments.Length; i++)
                {
                    LicensedMotionSegment segment = Profile.segments[i];
                    Enable("Licensed_Segment_" + i, segment.SampleTime(clock),
                        segment.Weight(clock, blendIn[i], blendOut[i]) / normalization);
                }
                AnimationClip idle = Profile.baseIdle ?? fallbackIdle;
                Enable("Licensed_BaseIdle", idle == null ? 0 :
                    Mathf.Repeat(clock * Profile.idleSpeed, Mathf.Max(.001f, idle.length)), Mathf.Max(0, 1 - sum));
            }
            animation.Sample();
        }

        private void Enable(string alias, float time, float weight)
        {
            if (weight <= 0 || string.IsNullOrEmpty(alias)) return;
            AnimationState state = animation[alias];
            if (state == null) return;
            state.enabled = true;
            state.speed = 0;
            state.weight = weight;
            state.time = time;
        }

        public void Cancel()
        {
            foreach (string alias in aliases)
            {
                AnimationState state = animation[alias];
                if (state != null) { state.enabled = false; state.weight = 0; }
            }
            aliases.Clear();
            Profile = null;
            actionId = null;
            clockSeconds = 0;
            clock = 0;
            contacted = false;
        }

        public void ResetForPool()
        {
            Cancel();
            consumedContacts.Clear();
            contactHistory.Clear();
        }
    }
}
