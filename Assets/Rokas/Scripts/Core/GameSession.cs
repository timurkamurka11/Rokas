using System;

namespace Rokas.Core
{
    public sealed class GameSession
    {
        private readonly ContractService contracts;
        private readonly EconomyService economy;
        private readonly FoodService food;

        public SaveData State { get; private set; }
        public ContractDefinition Contract { get; private set; }
        public CombatService Combat { get; private set; }

        public event Action Changed;

        public GameSession(SaveData state, ContractDefinition contract)
        {
            if (state == null)
            {
                throw new ArgumentNullException("state");
            }

            if (contract == null)
            {
                throw new ArgumentNullException("contract");
            }

            State = state;
            Contract = contract;
            contracts = new ContractService();
            economy = new EconomyService();
            food = new FoodService();
            Combat = new CombatService(state, contract, economy);
        }

        public bool AcceptContract()
        {
            return NotifyIf(contracts.Accept(State, Contract));
        }

        public bool PrepareFood(string foodId)
        {
            return NotifyIf(food.Prepare(State, foodId));
        }

        public bool LeaveHome()
        {
            return NotifyIf(contracts.LeaveHome(State));
        }

        public bool EnterPortal()
        {
            return NotifyIf(contracts.BeginCombat(State, Contract, food.GetAutoInterval(State, Contract)));
        }

        public bool ClickAttack(bool weakPoint)
        {
            return NotifyIf(Combat.ClickAttack(weakPoint));
        }

        public void Tick(float seconds)
        {
            NotifyIf(Combat.Tick(seconds));
        }

        public bool ReturnHome()
        {
            return NotifyIf(contracts.ReturnHome(State));
        }

        public bool ClaimPayment()
        {
            return NotifyIf(economy.ClaimPayment(State, Contract));
        }

        public bool UpgradeWeapon()
        {
            return NotifyIf(economy.UpgradeWeapon(State));
        }

        public void SetLamp(bool on)
        {
            if (State.lampOn == on)
            {
                return;
            }

            State.lampOn = on;
            NotifyChanged();
        }

        public void PetMame()
        {
            State.mameInteractions++;
            NotifyChanged();
        }

        private bool NotifyIf(bool changed)
        {
            if (changed)
            {
                NotifyChanged();
            }
            return changed;
        }

        private void NotifyChanged()
        {
            Action handler = Changed;
            if (handler != null)
            {
                handler();
            }
        }
    }
}
