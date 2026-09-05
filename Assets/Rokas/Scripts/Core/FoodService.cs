using System;

namespace Rokas.Core
{
    public sealed class FoodService
    {
        public const string GreenTeaId = "food_green_tea";
        public const int GreenTeaCost = 80;

        public bool Prepare(SaveData state, string foodId)
        {
            RequireState(state);
            if ((state.phase != RunPhase.Home && state.phase != RunPhase.Accepted) ||
                foodId != GreenTeaId ||
                !string.IsNullOrEmpty(state.preparedFoodId) ||
                state.yen < GreenTeaCost)
            {
                return false;
            }

            state.yen -= GreenTeaCost;
            state.preparedFoodId = GreenTeaId;
            return true;
        }

        public float GetAutoInterval(SaveData state, ContractDefinition contract)
        {
            RequireState(state);
            if (contract == null)
            {
                throw new ArgumentNullException("contract");
            }
            if (state.preparedFoodId == GreenTeaId)
            {
                return contract.autoInterval / 1.05f;
            }

            return contract.autoInterval;
        }

        private static void RequireState(SaveData state)
        {
            if (state == null)
            {
                throw new ArgumentNullException("state");
            }
        }
    }
}
