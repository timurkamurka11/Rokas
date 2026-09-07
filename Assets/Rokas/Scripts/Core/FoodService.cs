using System;

namespace Rokas.Core
{
    public sealed class FoodService
    {
        public const string RamenId = "food_yumiko_ramen";
        public const string OnigiriId = "food_traveler_onigiri";
        public const string MisoId = "food_spicy_miso";
        public const string TempuraId = "food_hunter_tempura";
        public const string MochiId = "food_moon_mochi";
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

        // The laptop Food screen uses the already persisted prepared-food slot. Non-tea
        // effects are intentionally data-only for now; gameplay can extend by food id
        // without introducing a parallel inventory/save architecture.
        public bool Consume(SaveData state, string foodId)
        {
            RequireState(state);
            if ((state.phase != RunPhase.Home && state.phase != RunPhase.Accepted) ||
                !IsFoodScreenItem(foodId) ||
                !string.IsNullOrEmpty(state.preparedFoodId))
            {
                return false;
            }

            state.preparedFoodId = foodId;
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

        private static bool IsFoodScreenItem(string foodId)
        {
            return foodId == RamenId ||
                   foodId == OnigiriId ||
                   foodId == MisoId ||
                   foodId == TempuraId ||
                   foodId == MochiId ||
                   foodId == GreenTeaId;
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
