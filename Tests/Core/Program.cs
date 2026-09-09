using System;

namespace Rokas.Core.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            try
            {
                int combatFailures = 0;
                foreach (string scenario in CombatFoundationCases.Names)
                {
                    try { CombatFoundationCases.Run(scenario); Console.WriteLine("PASS Combat2: " + scenario); }
                    catch (Exception e) { combatFailures++; Console.WriteLine("FAIL Combat2: " + scenario + " " + (e.InnerException ?? e).Message); }
                }
                if (combatFailures > 0) throw new InvalidOperationException(combatFailures + " Combat2 scenarios failed.");
                CombatFoundationRedTests.RunAll();
                DomainBehaviorTests.RunAll();
                FoodFeedbackTests.RunAll();
                MessageDomainTests.RunAll();
                CoordinateAttachmentActionTests.RunAll();
                ContractAttachmentActionTests.RunAll();
                GameSessionMessageEventTests.RunAll();
                DialogueProgressionTests.RunAll();
                HunterGuildFlowTests.RunAll();
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
