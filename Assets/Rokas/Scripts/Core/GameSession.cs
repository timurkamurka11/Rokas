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
        public MessageService Messages { get; private set; }

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
            Messages = new MessageService(state, contract, contracts);
            Messages.Changed += NotifyChanged;
        }

        public bool AcceptContract()
        {
            return NotifyIf(contracts.Accept(State, Contract));
        }

        public bool EnsureGuildContractOffer()
        {
            if (Contract == null || string.IsNullOrEmpty(Contract.id))
            {
                return false;
            }

            return Messages.DeliverIncoming(
                "guild-contract-offer:" + Contract.id,
                "guild",
                "Новый контракт доступен для принятия.",
                new MessageAttachment
                {
                    kind = MessageAttachmentKind.Contract,
                    id = "contract_" + Contract.id + "_attachment",
                    title = Contract.title ?? string.Empty,
                    body = Contract.location ?? string.Empty,
                    targetId = Contract.id
                });
        }

        public bool PrepareFood(string foodId)
        {
            return NotifyIf(food.Prepare(State, foodId));
        }

        public FoodConsumeBlockReason GetFoodConsumeBlockReason(string foodId)
        {
            return food.GetConsumeBlockReason(State, foodId);
        }

        public bool ConsumeFood(string foodId)
        {
            return NotifyIf(food.Consume(State, foodId));
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
            if (!economy.ClaimPayment(State, Contract))
            {
                return false;
            }

            bool delivered = Messages.DeliverIncoming(
                "guild-contract-completed:" + Contract.id + ":" + State.completedRuns,
                "guild",
                "Контракт закрыт. Награда перечислена. Выполнение №" + State.completedRuns + ".");
            if (!delivered)
            {
                NotifyChanged();
            }
            return true;
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
