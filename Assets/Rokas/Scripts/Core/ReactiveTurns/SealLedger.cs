using System;
using System.Collections.Generic;

namespace Rokas.Core.ReactiveTurns
{
    public sealed class SealLedger
    {
        private readonly HashSet<string> appliedEventIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly int completedActionsForRefractory;
        private string breakTriggerActionId;
        private bool skipToken;
        private bool ignoreNextPlayerTurnEnd;
        private int ignoredPlayerTurns;

        public int MaxSeal { get; private set; }
        public int Remaining { get; private set; }
        public bool IsBroken { get; private set; }
        public int RefractoryActionsRemaining { get; private set; }
        internal bool SkipTokenPending { get { return skipToken; } }
        internal string BreakTriggerActionId { get { return breakTriggerActionId; } }
        internal int IgnoredPlayerTurns { get { return ignoredPlayerTurns; } }
        internal bool IgnoreNextPlayerTurnEnd { get { return ignoreNextPlayerTurnEnd; } }
        public double DamageMultiplier { get { return IsBroken ? 1.25 : 1; } }

        public SealLedger(int maxSeal, int completedActionsForRefractory)
        {
            if (maxSeal <= 0) throw new ArgumentOutOfRangeException("maxSeal");
            if (completedActionsForRefractory <= 0) throw new ArgumentOutOfRangeException("completedActionsForRefractory");
            MaxSeal = Remaining = maxSeal;
            this.completedActionsForRefractory = completedActionsForRefractory;
        }

        public int ApplyDamage(int amount, string actionId, string eventId)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException("amount");
            if (string.IsNullOrWhiteSpace(actionId)) throw new ArgumentException("An action ID is required.", "actionId");
            if (string.IsNullOrWhiteSpace(eventId)) throw new ArgumentException("An event ID is required.", "eventId");
            if (!appliedEventIds.Add(eventId) || IsBroken || RefractoryActionsRemaining > 0) return 0;
            int applied = Math.Min(Remaining, amount);
            Remaining -= applied;
            if (Remaining == 0)
            {
                IsBroken = true;
                skipToken = true;
                breakTriggerActionId = actionId;
                ignoredPlayerTurns = 0;
            }
            return applied;
        }

        public bool TryConsumeSkipToken()
        {
            if (!skipToken) return false;
            skipToken = false;
            return true;
        }

        public void CompleteOffensiveCommand(string actionId, bool targetedThisEnemy)
        {
            if (!IsBroken || !targetedThisEnemy) return;
            if (actionId == breakTriggerActionId)
            {
                ignoreNextPlayerTurnEnd = true;
                return;
            }
            ResetAfterVulnerability();
        }

        public void OnPlayerTurnEnd()
        {
            if (!IsBroken) return;
            if (ignoreNextPlayerTurnEnd) { ignoreNextPlayerTurnEnd = false; return; }
            ignoredPlayerTurns++;
            if (ignoredPlayerTurns >= 2) ResetAfterVulnerability();
        }

        public void CompleteNaturalEnemyAction()
        {
            if (RefractoryActionsRemaining > 0) RefractoryActionsRemaining--;
        }

        internal void Restore(int remaining, bool isBroken, int refractoryActionsRemaining,
            bool skipTokenPending, string triggerActionId, int ignoredTurns, bool ignoreNextTurnEnd)
        {
            if (remaining < 0 || remaining > MaxSeal || refractoryActionsRemaining < 0 || ignoredTurns < 0)
                throw new ArgumentOutOfRangeException("remaining");
            Remaining = remaining;
            IsBroken = isBroken;
            RefractoryActionsRemaining = refractoryActionsRemaining;
            skipToken = skipTokenPending;
            breakTriggerActionId = triggerActionId;
            ignoredPlayerTurns = ignoredTurns;
            ignoreNextPlayerTurnEnd = ignoreNextTurnEnd;
        }

        private void ResetAfterVulnerability()
        {
            IsBroken = false;
            Remaining = MaxSeal;
            RefractoryActionsRemaining = completedActionsForRefractory;
            breakTriggerActionId = null;
            ignoredPlayerTurns = 0;
            ignoreNextPlayerTurnEnd = false;
        }
    }
}
