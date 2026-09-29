using System;
using System.Reflection;

namespace Rokas.Core.Tests
{
    public static class FoodFeedbackTests
    {
        public static void RunAll()
        {
            CompletedContractReportsPaymentPendingReason();
            AllowedHomeUseRemainsAllowed();
            ExistingEffectStillBlocksStacking();
        }

        private static void CompletedContractReportsPaymentPendingReason()
        {
            var state = new SaveData { phase = RunPhase.Payment };
            var service = new FoodService();
            var reason = GetReason(service, state, FoodService.RamenId);

            Equal("ContractPaymentPending", reason,
                "completed-but-unclaimed contract must expose a specific food-use block reason");
            Equal(false, service.Consume(state, FoodService.RamenId),
                "payment state must remain blocked from consuming food");
            Equal(string.Empty, state.preparedFoodId,
                "blocked consume must not mutate prepared-food state");
        }

        private static void AllowedHomeUseRemainsAllowed()
        {
            var state = new SaveData { phase = RunPhase.Home };
            var service = new FoodService();
            var reason = GetReason(service, state, FoodService.OnigiriId);

            Equal("None", reason, "home food use must report no block reason");
            Equal(true, service.Consume(state, FoodService.OnigiriId),
                "home food use must continue to work");
            Equal(FoodService.OnigiriId, state.preparedFoodId,
                "allowed consume must still populate the existing prepared-food slot");
        }

        private static void ExistingEffectStillBlocksStacking()
        {
            var state = new SaveData
            {
                phase = RunPhase.Home,
                preparedFoodId = FoodService.RamenId
            };
            var service = new FoodService();
            var reason = GetReason(service, state, FoodService.MisoId);

            Equal("EffectAlreadyActive", reason,
                "existing prepared food must expose the stacking block reason");
            Equal(false, service.Consume(state, FoodService.MisoId),
                "existing food effect must still reject stacking");
            Equal(FoodService.RamenId, state.preparedFoodId,
                "rejected stacking must preserve the active effect");
        }

        private static string GetReason(FoodService service, SaveData state, string foodId)
        {
            MethodInfo method = typeof(FoodService).GetMethod(
                "GetConsumeBlockReason",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(SaveData), typeof(string) },
                null);

            if (method == null)
                throw new Exception("FoodService.GetConsumeBlockReason(SaveData, string) is missing");

            object value = method.Invoke(service, new object[] { state, foodId });
            return value == null ? string.Empty : value.ToString();
        }

        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
                throw new Exception(message + " (expected: " + expected + ", actual: " + actual + ")");
        }
    }
}
