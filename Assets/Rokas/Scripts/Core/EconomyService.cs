namespace Rokas.Core
{
    public sealed class EconomyService
    {
        public int GetUpgradeCost(SaveData state)
        {
            return 300 * state.weaponLevel;
        }

        public float GetWeaponDamageMultiplier(SaveData state)
        {
            return 1f + .2f * (state.weaponLevel - 1);
        }

        public bool UpgradeWeapon(SaveData state)
        {
            if (state.phase != RunPhase.Home || state.weaponLevel < 1)
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
            if (state.phase != RunPhase.Payment || state.activeContractId != contract.id)
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
    }
}
