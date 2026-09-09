using NUnit.Framework;
namespace Rokas.Core.Tests
{
    public sealed class CombatFoundationTests
    {
        [TestCaseSource(typeof(CombatFoundationCases), nameof(CombatFoundationCases.Names))]
        public void ApprovedCombatBehavior(string scenario) { CombatFoundationCases.Run(scenario); }
    }
}
