using System;

namespace Rokas.Core
{
    public sealed class ContractService
    {
        public bool Accept(SaveData state, ContractDefinition contract)
        {
            RequireStateAndContract(state, contract);
            if (state.phase != RunPhase.Home || string.IsNullOrEmpty(contract.id))
            {
                return false;
            }

            state.phase = RunPhase.Accepted;
            state.activeContractId = contract.id;
            ResetCombat(state);
            return true;
        }

        public bool LeaveHome(SaveData state)
        {
            RequireState(state);
            if (state.phase != RunPhase.Accepted)
            {
                return false;
            }

            state.phase = RunPhase.Portal;
            return true;
        }

        public bool BeginCombat(SaveData state, ContractDefinition contract, float autoInterval)
        {
            RequireStateAndContract(state, contract);
            if (state.phase != RunPhase.Portal || state.activeContractId != contract.id)
            {
                return false;
            }

            state.phase = RunPhase.Combat;
            state.enemyHp = contract.enemyHealth;
            state.playerHp = 100f;
            state.enemyTimer = contract.enemyInterval;
            state.autoTimer = autoInterval;
            state.clickTimer = 0f;
            state.combatTime = 0f;
            state.weakPointClaimed = false;
            return true;
        }

        public bool ReturnHome(SaveData state)
        {
            RequireState(state);
            if (state.phase == RunPhase.Sealed)
            {
                state.phase = RunPhase.Payment;
                state.preparedFoodId = string.Empty;
                return true;
            }

            if (state.phase != RunPhase.Failed &&
                state.phase != RunPhase.Accepted &&
                state.phase != RunPhase.Portal)
            {
                return false;
            }

            state.phase = RunPhase.Home;
            state.activeContractId = string.Empty;
            state.preparedFoodId = string.Empty;
            ResetCombat(state);
            return true;
        }

        internal static void ResetCombat(SaveData state)
        {
            state.enemyHp = 0f;
            state.playerHp = 100f;
            state.enemyTimer = 0f;
            state.autoTimer = 0f;
            state.clickTimer = 0f;
            state.combatTime = 0f;
            state.weakPointClaimed = false;
        }

        private static void RequireStateAndContract(SaveData state, ContractDefinition contract)
        {
            RequireState(state);
            if (contract == null)
            {
                throw new ArgumentNullException("contract");
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
