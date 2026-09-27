using System;
using System.Collections.Generic;

namespace Rokas.Core.ReactiveTurns
{
    public sealed class DefenseReward
    {
        public int ApGranted { get; internal set; }
        public int SealDamage { get; internal set; }
    }

    public sealed class ActionPointLedger
    {
        private readonly HashSet<string> rewardedHitIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> basicEventIds = new HashSet<string>(StringComparer.Ordinal);
        private string currentEnemyActionId;
        private int defenseApThisInterval;
        private int defenseSealThisAction;

        public int Current { get; private set; }
        public bool DefendActive { get; private set; }
        public int DefenseApThisInterval { get { return defenseApThisInterval; } }

        public ActionPointLedger(int initialAp)
        {
            if (initialAp < 0 || initialAp > 6) throw new ArgumentOutOfRangeException("initialAp");
            Current = initialAp;
        }

        public void OnNaturalPlayerTurnStart()
        {
            DefendActive = false;
            defenseApThisInterval = 0;
            Current = Math.Min(6, Current + 1);
        }

        // The session owns the public AP field and has already applied the natural-turn grant.
        internal void SyncAfterNaturalTurn(int alreadyGrantedAp)
        {
            SyncCurrent(alreadyGrantedAp);
            DefendActive = false;
            defenseApThisInterval = 0;
        }

        internal void SyncCurrent(int ap)
        {
            if (ap < 0 || ap > 6) throw new ArgumentOutOfRangeException("ap");
            Current = ap;
        }

        internal void RestoreAccounting(int defenseAp, bool defendActive)
        {
            if (defenseAp < 0 || defenseAp > 2) throw new ArgumentOutOfRangeException("defenseAp");
            defenseApThisInterval = defenseAp;
            DefendActive = defendActive;
        }

        public bool TrySpend(int cost)
        {
            if (cost < 0) throw new ArgumentOutOfRangeException("cost");
            if (cost > Current) return false;
            Current -= cost;
            return true;
        }

        public void ActivateDefend()
        {
            DefendActive = true;
        }

        public void BeginEnemyAction(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId)) throw new ArgumentException("An action ID is required.", "actionId");
            if (currentEnemyActionId == actionId) return;
            currentEnemyActionId = actionId;
            defenseSealThisAction = 0;
        }

        public DefenseReward GrantDefense(string hitEventId, DefenseOutcome outcome)
        {
            if (string.IsNullOrWhiteSpace(hitEventId)) throw new ArgumentException("A hit event ID is required.", "hitEventId");
            var reward = new DefenseReward();
            if (!rewardedHitIds.Add(hitEventId)) return reward;
            int requestedAp = outcome == DefenseOutcome.Perfect ? 2 : outcome == DefenseOutcome.Parry ? 1 : 0;
            int requestedSeal = outcome == DefenseOutcome.Perfect ? 20 : outcome == DefenseOutcome.Parry ? 12 : 0;
            if (requestedAp == 0) return reward;
            if (currentEnemyActionId == null) throw new InvalidOperationException("A defense reward requires an enemy action.");
            reward.ApGranted = Math.Min(requestedAp, Math.Min(2 - defenseApThisInterval, 6 - Current));
            reward.SealDamage = Math.Min(requestedSeal, 40 - defenseSealThisAction);
            Current += reward.ApGranted;
            defenseApThisInterval += reward.ApGranted;
            defenseSealThisAction += reward.SealDamage;
            return reward;
        }

        public int GrantBasic(string impactEventId)
        {
            if (string.IsNullOrWhiteSpace(impactEventId)) throw new ArgumentException("An impact event ID is required.", "impactEventId");
            if (!basicEventIds.Add(impactEventId)) return 0;
            int granted = Math.Min(2, 6 - Current);
            Current += granted;
            return granted;
        }
    }
}
