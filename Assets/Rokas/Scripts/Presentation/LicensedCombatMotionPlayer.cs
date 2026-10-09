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
        private readonly List<string> registeredAliases = new List<string>();
        private readonly Dictionary<string, RegisteredClip> registeredClips =
            new Dictionary<string, RegisteredClip>(StringComparer.Ordinal);
        private readonly HashSet<string> consumedContacts = new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> contactHistory = new Queue<string>();
        private float[] blendIn = Array.Empty<float>(), blendOut = Array.Empty<float>();
        private string[] segmentAliases = Array.Empty<string>();

        private readonly struct RegisteredClip
        {
            public readonly AnimationClip source, runtime;
            public RegisteredClip(AnimationClip source, AnimationClip runtime)
            {
                this.source = source;
                this.runtime = runtime;
            }
        }
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
            RegisterProfileClips(profile, alias);
            Sample();
        }

        // Prepare only the inactive default bank. Registration does not sample or own a pose.
        public bool Prepare(LicensedCombatMotionProfile profile, string anticipationAlias)
        {
            if (Active || profile == null) return false;
            RegisterProfileClips(profile, anticipationAlias);
            // AddClip can leave a new state's default weight nonzero. Disable only
            // owned aliases, preserving contact history and all unrelated states.
            Cancel();
            return true;
        }

        private void RegisterProfileClips(LicensedCombatMotionProfile profile, string alias)
        {
            EnsureSegmentCapacity(profile.segments.Length);
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
                Register(profile.segments[i].clip, segmentAliases[i]);
            RetireUnusedRegistrations();
        }

        // A preplayed presentation may acquire its domain ID after approach or release.
        public void BindContact(string id)
        {
            if (Active && !contacted) actionId = id;
        }

        private void EnsureSegmentCapacity(int count)
        {
            if (blendIn.Length >= count) return;
            blendIn = new float[count];
            blendOut = new float[count];
            int previousCount = segmentAliases.Length;
            Array.Resize(ref segmentAliases, count);
            for (int i = previousCount; i < count; i++)
                segmentAliases[i] = "Licensed_Segment_" + i;
        }

        private void Register(AnimationClip clip, string alias)
        {
            if (clip == null) return;
            if (!clip.legacy)
                throw new InvalidOperationException("Licensed combat clip must be Legacy: " + clip.name);
            AnimationClip current = animation.GetClip(alias);
            AnimationState state = animation[alias];
            bool known = registeredClips.TryGetValue(alias, out RegisteredClip registration);
            // AddClip creates a named runtime copy. Comparing GetClip to the source
            // would rebuild that copy on every Begin instead of recognizing it.
            if (!known || registration.source != clip || current == null ||
                registration.runtime != current || state == null)
            {
                animation.AddClip(clip, alias);
                current = animation.GetClip(alias);
                state = animation[alias];
                if (current == null || state == null)
                    throw new InvalidOperationException("Licensed combat clip registration failed: " + alias);
                registeredClips[alias] = new RegisteredClip(clip, current);
                if (!known) registeredAliases.Add(alias);
            }
            // Reassert the sampling contract even if an external writer changed it.
            state.wrapMode = WrapMode.ClampForever;
            if (!aliases.Contains(alias)) aliases.Add(alias);
        }

        private void RetireUnusedRegistrations()
        {
            for (int i = registeredAliases.Count - 1; i >= 0; i--)
            {
                string alias = registeredAliases[i];
                if (aliases.Contains(alias)) continue;
                RegisteredClip registration = registeredClips[alias];
                // Only remove our own old copy; an externally replaced alias is not ours.
                if (animation.GetClip(alias) == registration.runtime)
                    animation.RemoveClip(alias);
                registeredClips.Remove(alias);
                registeredAliases.RemoveAt(i);
            }
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
                    Enable(segmentAliases[i], segment.SampleTime(clock),
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
