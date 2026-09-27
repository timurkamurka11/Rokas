using System;
using System.Collections.Generic;

namespace Rokas.Core.ReactiveTurns
{
    public sealed class DefensePress
    {
        public string InputId { get; private set; }
        public long Epoch { get; private set; }
        public DefenseKind Kind { get; private set; }
        public long CombatUs { get; private set; }

        public DefensePress(string inputId, long epoch, DefenseKind kind, long combatUs)
        {
            InputId = inputId;
            Epoch = epoch;
            Kind = kind;
            CombatUs = combatUs;
        }
    }

    public sealed class DefenseAttempt
    {
        public string InputId { get; internal set; }
        public string HitId { get; internal set; }
        public DefenseOutcome Outcome { get; internal set; }
        public bool Accepted { get; internal set; }
    }

    public sealed class DefenseHitResolution
    {
        public string EventId { get; internal set; }
        public string HitId { get; internal set; }
        public DefenseOutcome Outcome { get; internal set; }
        public int RawDamage { get; internal set; }
        public long ImpactUs { get; internal set; }
    }

    public sealed class DefenseSequenceLedger
    {
        private sealed class HitState
        {
            public HitDefinition Definition;
            public long ImpactUs;
            public DefenseAttempt Attempt;
            public bool Finalized;
        }

        private readonly string actionId;
        private readonly DefenseWindowProfile profile;
        private readonly HitState[] hits;
        private readonly HashSet<string> seenInputIds = new HashSet<string>(StringComparer.Ordinal);
        private long epoch;
        private bool dodgeHeld;
        private bool parryHeld;
        private bool dodgeNeedsRelease;
        private bool parryNeedsRelease;
        private bool nextHitNeedsRelease;
        private DefenseKind lastAttemptKind;
        private bool suffixCanceled;

        public bool LateDeliveryObserved { get; private set; }
        public bool IsSettled { get; private set; }
        public bool CounterEligible { get; private set; }
        public long CounterOpenUs { get; private set; }
        public long CounterCloseUs { get; private set; }

        public DefenseSequenceLedger(string actionId, AttackSequenceDefinition sequence, long startCombatUs,
            DefenseWindowProfile profile, long epoch)
        {
            if (string.IsNullOrWhiteSpace(actionId)) throw new ArgumentException("An action ID is required.", "actionId");
            if (sequence == null) throw new ArgumentNullException("sequence");
            if (profile == null) throw new ArgumentNullException("profile");
            if (sequence.Hits.Count == 0) throw new ArgumentException("Sequence has no hits.", "sequence");
            this.actionId = actionId;
            this.profile = profile;
            this.epoch = epoch;
            hits = new HitState[sequence.Hits.Count];
            for (int index = 0; index < hits.Length; index++)
            {
                HitDefinition definition = sequence.Hits[index];
                if (definition == null) throw new ArgumentException("Sequence contains a null hit.", "sequence");
                hits[index] = new HitState { Definition = definition, ImpactUs = checked(startCombatUs + definition.ImpactUs) };
            }
            CounterOpenUs = checked(hits[hits.Length - 1].ImpactUs + profile.AcquireLateUs + 40000);
            CounterCloseUs = checked(CounterOpenUs + 600000);
        }

        public IReadOnlyList<DefenseAttempt> SubmitPresses(IEnumerable<DefensePress> presses)
        {
            if (presses == null) throw new ArgumentNullException("presses");
            var sorted = new List<DefensePress>(presses);
            sorted.Sort(delegate(DefensePress left, DefensePress right)
            {
                if (left == null || right == null) throw new ArgumentException("Null defense press.", "presses");
                int byTime = left.CombatUs.CompareTo(right.CombatUs);
                if (byTime != 0) return byTime;
                int byKind = left.Kind.CompareTo(right.Kind); // Dodge has priority at the same timestamp.
                return byKind != 0 ? byKind : string.CompareOrdinal(left.InputId, right.InputId);
            });
            var results = new List<DefenseAttempt>(sorted.Count);
            long lastAcceptedTimestamp = long.MinValue;
            foreach (DefensePress press in sorted)
            {
                if (press == null) throw new ArgumentException("Null defense press.", "presses");
                var result = new DefenseAttempt { InputId = press.InputId, Outcome = DefenseOutcome.Ignored };
                results.Add(result);
                if (string.IsNullOrWhiteSpace(press.InputId) || !seenInputIds.Add(press.InputId)) continue;
                if (press.Epoch != epoch || (press.Kind != DefenseKind.Dodge && press.Kind != DefenseKind.Parry)) continue;
                bool isDodge = press.Kind == DefenseKind.Dodge;
                if (isDodge ? dodgeHeld || dodgeNeedsRelease : parryHeld || parryNeedsRelease) continue;
                if (isDodge) dodgeHeld = true; else parryHeld = true;
                if (nextHitNeedsRelease) continue;
                if (lastAcceptedTimestamp == press.CombatUs) continue;

                HitState target = FindTarget(press.CombatUs);
                if (target == null) continue;
                result.HitId = target.Definition.Id;
                result.Outcome = DefenseResolver.Classify(press.Kind,
                    checked(press.CombatUs - target.ImpactUs), profile, target.Definition.AllowedResponses);
                if (result.Outcome == DefenseOutcome.Ignored) continue;
                result.Accepted = true;
                target.Attempt = result;
                nextHitNeedsRelease = true;
                lastAttemptKind = press.Kind;
                lastAcceptedTimestamp = press.CombatUs;
            }
            return results;
        }

        private HitState FindTarget(long pressUs)
        {
            foreach (HitState hit in hits)
            {
                long openUs = checked(hit.ImpactUs - profile.AcquireEarlyUs);
                long closeUs = checked(hit.ImpactUs + profile.AcquireLateUs);
                if (pressUs < openUs || pressUs > closeUs) continue;
                if (hit.Finalized)
                {
                    if (hit.Attempt == null)
                    {
                        LateDeliveryObserved = true;
                        return null; // A stale press for this miss cannot migrate to the next hit.
                    }
                    continue;
                }
                if (hit.Attempt != null)
                {
                    if (pressUs <= hit.ImpactUs) return null;
                    continue;
                }
                return hit;
            }
            return null;
        }

        public void Release(DefenseKind kind)
        {
            if (kind == DefenseKind.Dodge) { dodgeHeld = false; dodgeNeedsRelease = false; }
            else if (kind == DefenseKind.Parry) { parryHeld = false; parryNeedsRelease = false; }
            else throw new ArgumentOutOfRangeException("kind");
            if (nextHitNeedsRelease && lastAttemptKind == kind) nextHitNeedsRelease = false;
        }

        public void ChangeEpoch(long newEpoch)
        {
            if (newEpoch == epoch) return;
            epoch = newEpoch;
            dodgeHeld = parryHeld = false;
            dodgeNeedsRelease = parryNeedsRelease = true;
            nextHitNeedsRelease = false;
        }

        public IReadOnlyList<DefenseHitResolution> Advance(long combatUs, long watermarkUs)
        {
            var resolved = new List<DefenseHitResolution>();
            foreach (HitState hit in hits)
            {
                if (hit.Finalized || watermarkUs < checked(hit.ImpactUs + profile.AcquireLateUs)) continue;
                hit.Finalized = true;
                resolved.Add(new DefenseHitResolution
                {
                    EventId = actionId + "/" + hit.Definition.Id,
                    HitId = hit.Definition.Id,
                    Outcome = hit.Attempt == null ? DefenseOutcome.Miss : hit.Attempt.Outcome,
                    RawDamage = hit.Definition.RawDamage,
                    ImpactUs = hit.ImpactUs
                });
            }
            IsSettled = true;
            foreach (HitState hit in hits) if (!hit.Finalized) { IsSettled = false; break; }
            if (IsSettled)
            {
                int successfulParries = 0;
                foreach (HitState hit in hits)
                    if (hit.Attempt != null && (hit.Attempt.Outcome == DefenseOutcome.Parry || hit.Attempt.Outcome == DefenseOutcome.Perfect))
                        successfulParries++;
                HitState final = hits[hits.Length - 1];
                CounterEligible = !suffixCanceled && successfulParries >= 2 && final.Attempt != null && final.Attempt.Outcome == DefenseOutcome.Perfect;
            }
            return resolved;
        }

        public void CancelSuffixAfter(string hitId)
        {
            int index = -1;
            for (int i = 0; i < hits.Length; i++) if (hits[i].Definition.Id == hitId) { index = i; break; }
            if (index < 0) throw new ArgumentException("Unknown hit ID.", "hitId");
            for (int i = index + 1; i < hits.Length; i++) hits[i].Finalized = true;
            suffixCanceled = index < hits.Length - 1;
            if (suffixCanceled) CounterEligible = false;
            IsSettled = true;
            for (int i = 0; i <= index; i++) if (!hits[i].Finalized) IsSettled = false;
        }
    }
}
