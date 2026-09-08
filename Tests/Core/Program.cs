using System;

namespace Rokas.Core.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            try
            {
                DomainBehaviorTests.RunAll();
                FoodFeedbackTests.RunAll();
                MessageDomainTests.RunAll();
                CoordinateAttachmentActionTests.RunAll();
                ContractAttachmentActionTests.RunAll();
                GameSessionMessageEventTests.RunAll();
                DialogueProgressionTests.RunAll();
                HunterGuildFlowTests.RunAll();
                YumikoFoodIntegrationTests.RunAll();
                LiveMessengerCoreTests.RunAll();
                LiveMessengerPolishCoreTests.RunAll();
                Console.WriteLine("PASS: all Rokas.Core behavior tests");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FAIL: " + exception);
                return 1;
            }
        }
    }
}
