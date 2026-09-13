using NUnit.Framework;
namespace Rokas.Core.Tests
{
    public sealed class Combat3Phase0Tests
    {
        [TestCaseSource(typeof(Combat3Phase0Cases), nameof(Combat3Phase0Cases.Names))]
        public void Phase0LaneBehavior(string scenario) { Combat3Phase0Cases.Run(scenario); }
    }
}
