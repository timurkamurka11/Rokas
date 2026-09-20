using System;

namespace Rokas.Core
{
    public sealed class EconomyService
    {
        public int GetUpgradeCost(SaveData state)
        {
            RequireState(state);
            if (state.weaponLevel < 1 || state.weaponLevel > SaveData.MaxWeaponLevel)
            {
                return int.MaxValue;
            }
            return checked(300 * state.weaponLevel);
        }

        public float GetWeaponDamageMultiplier(SaveData state)
        {
            RequireState(state);
            return 1f + .2f * (state.weaponLevel - 1);
        }

        public bool UpgradeWeapon(SaveData state)
        {
            RequireState(state);
            if (state.phase != RunPhase.Home ||
                state.weaponLevel < 1 ||
                state.weaponLevel >= SaveData.MaxWeaponLevel)
            {
                return false;
            }

            int cost = GetUpgradeCost(state);
            if (state.yen < cost)
            {
                return false;
            }

            state.yen -= cost;
            state.weaponLevel++;
            return true;
        }

        public bool ClaimPayment(SaveData state, ContractDefinition contract)
        {
            RequireState(state);
            if (contract == null)
            {
                throw new ArgumentNullException("contract");
            }
            if (state.phase != RunPhase.Payment || state.activeContractId != contract.id)
            {
                return false;
            }

            if (contract.reward < 0 || contract.reputationReward < 0 || contract.ashReward < 0 ||
                !CanAdd(state.yen, contract.reward) ||
                !CanAdd(state.reputation, contract.reputationReward) ||
                !CanAdd(state.spiritAsh, contract.ashReward) ||
                state.completedRuns == int.MaxValue)
            {
                return false;
            }

            state.yen += contract.reward;
            state.reputation += contract.reputationReward;
            state.spiritAsh += contract.ashReward;
            state.completedRuns++;
            state.phase = RunPhase.Home;
            state.activeContractId = string.Empty;
            state.preparedFoodId = string.Empty;
            ContractService.ResetCombat(state);
            return true;
        }

        private static bool CanAdd(int value, int addition)
        {
            return addition >= 0 && value <= int.MaxValue - addition;
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
