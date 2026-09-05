namespace Rokas.Core
{
    public sealed class FoodService
    {
        public const string GreenTeaId = "food_green_tea";
        public const int GreenTeaCost = 80;

        public bool Prepare(SaveData state, string foodId)
        {
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
            if (state.preparedFoodId == GreenTeaId)
            {
                return contract.autoInterval / 1.05f;
            }

            return contract.autoInterval;
        }
    }
}
