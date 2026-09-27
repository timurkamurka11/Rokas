using System;

namespace Rokas.Core.ReactiveTurns
{
    [Serializable]
    public sealed class BattleSealSnapshot
    {
        public string actorId;
        public int remaining;
        public bool isBroken;
        public int refractoryActionsRemaining;
        public bool skipTokenPending;
        public string breakTriggerActionId;
        public int ignoredPlayerTurns;
        public bool ignoreNextPlayerTurnEnd;

        public BattleSealSnapshot Clone()
        {
            return (BattleSealSnapshot)MemberwiseClone();
        }
    }

    public sealed partial class BattleCheckpoint
    {
        public int defenseApThisInterval;
        public bool defendActive;
        public BattleSealSnapshot[] sealStates;

        partial void OnCheckpointCloned(BattleCheckpoint clone)
        {
            if (sealStates == null) return;
            clone.sealStates = new BattleSealSnapshot[sealStates.Length];
            for (int i = 0; i < sealStates.Length; i++)
                if (sealStates[i] != null) clone.sealStates[i] = sealStates[i].Clone();
        }
    }
}
