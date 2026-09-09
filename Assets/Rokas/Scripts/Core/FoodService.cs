using System;

namespace Rokas.Core
{
    public enum FoodConsumeBlockReason
    {
        None,
        ContractPaymentPending,
        InvalidPhase,
        UnknownFood,
        EffectAlreadyActive
    }

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
            NormalizeStoredFood(state);
            bool stored = state.storedFoodCount > 0 && string.Equals(state.storedFoodId, foodId, StringComparison.Ordinal);
            if ((state.phase != RunPhase.Home && state.phase != RunPhase.Accepted) ||
                foodId != GreenTeaId ||
                !string.IsNullOrEmpty(state.preparedFoodId) ||
                (!stored && state.yen < GreenTeaCost))
            {
                return false;
            }

            if (stored)
            {
                ConsumeStoredFood(state);
            }
            else
            {
                state.yen -= GreenTeaCost;
            }
            state.preparedFoodId = GreenTeaId;
            return true;
        }

        // The laptop Food screen uses the already persisted prepared-food slot. Non-tea
        // effects are intentionally data-only for now; gameplay can extend by food id
        // without introducing a parallel inventory/save architecture.
        public FoodConsumeBlockReason GetConsumeBlockReason(SaveData state, string foodId)
        {
            RequireState(state);
            if (state.phase == RunPhase.Payment) return FoodConsumeBlockReason.ContractPaymentPending;
            if (state.phase != RunPhase.Home && state.phase != RunPhase.Accepted)
                return FoodConsumeBlockReason.InvalidPhase;
            if (!IsKnownFood(foodId)) return FoodConsumeBlockReason.UnknownFood;
            if (!string.IsNullOrEmpty(state.preparedFoodId)) return FoodConsumeBlockReason.EffectAlreadyActive;
            return FoodConsumeBlockReason.None;
        }

        public bool Consume(SaveData state, string foodId)
        {
            if (GetConsumeBlockReason(state, foodId) != FoodConsumeBlockReason.None) return false;
            NormalizeStoredFood(state);
            state.preparedFoodId = foodId;
            if (state.storedFoodCount > 0 && string.Equals(state.storedFoodId, foodId, StringComparison.Ordinal))
            {
                ConsumeStoredFood(state);
            }
            return true;
        }

        public bool GrantStoredFood(SaveData state, string foodId)
        {
            RequireState(state);
            NormalizeStoredFood(state);
            if (!IsKnownFood(foodId) || state.storedFoodCount == int.MaxValue)
            {
                return false;
            }
            if (state.storedFoodCount > 0 && !string.Equals(state.storedFoodId, foodId, StringComparison.Ordinal))
            {
                return false;
            }

            state.storedFoodId = foodId;
            state.storedFoodCount++;
            return true;
        }

        public int GetStoredFoodCount(SaveData state, string foodId)
        {
            RequireState(state);
            NormalizeStoredFood(state);
            return string.Equals(state.storedFoodId, foodId, StringComparison.Ordinal) ? state.storedFoodCount : 0;
        }

        public bool IsKnownFood(string foodId)
        {
            return foodId == RamenId ||
                   foodId == OnigiriId ||
                   foodId == MisoId ||
                   foodId == TempuraId ||
                   foodId == MochiId ||
                   foodId == GreenTeaId;
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

        private static void ConsumeStoredFood(SaveData state)
        {
            state.storedFoodCount--;
            if (state.storedFoodCount <= 0)
            {
                state.storedFoodCount = 0;
                state.storedFoodId = string.Empty;
            }
        }

        private static void NormalizeStoredFood(SaveData state)
        {
            state.storedFoodId = state.storedFoodId ?? string.Empty;
            if (state.storedFoodCount <= 0 || state.storedFoodId.Length == 0)
            {
                state.storedFoodCount = 0;
                state.storedFoodId = string.Empty;
            }
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
