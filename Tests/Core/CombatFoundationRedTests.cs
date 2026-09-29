using System;
using Rokas.Core;
namespace Rokas.Core.Tests
{
    internal static class CombatFoundationRedTests
    {
        public static void RunAll()
        {
            int failed = 0;
            Check("idle attacks removed", () => { var s = Fight(); s.Tick(1.25f); Require(s.State.enemyHp == s.Contract.enemyHealth); }, ref failed);
            Check("spam blocked before combo recovery", () => { var s = Fight(); s.ClickAttack(false); s.Tick(.15f); Require(!s.ClickAttack(false)); }, ref failed);
            Check("timed third strike rewards combo", () => { var s = Fight(); s.ClickAttack(false); s.Tick(.48f); s.ClickAttack(false); s.Tick(.48f); s.ClickAttack(false); Require(s.Contract.enemyHealth - s.State.enemyHp > s.Contract.clickDamage * 3.5f); }, ref failed);
            Check("old timed seal button retired", () => { var s = Fight(); s.Tick(2.05f); Require(!s.Combat.WeakPointActive); }, ref failed);
            if (failed != 0) throw new InvalidOperationException(failed + " Combat 2.0 requirements missing.");
        }
        private static GameSession Fight() { var s = new GameSession(new SaveData(), new ContractDefinition()); s.AcceptContract(); s.LeaveHome(); s.EnterPortal(); return s; }
        private static void Require(bool ok) { if (!ok) throw new InvalidOperationException("Expected approved Combat 2.0 behavior."); }
        private static void Check(string name, Action test, ref int failed) { try { test(); Console.WriteLine("PASS: " + name); } catch (Exception e) { failed++; Console.WriteLine("RED: " + name + " - " + e.Message); } }
    }
}
