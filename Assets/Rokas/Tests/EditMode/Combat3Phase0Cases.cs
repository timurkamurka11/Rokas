using System;
using System.Reflection;
using Rokas.Core;

namespace Rokas.Core.Tests
{
    public static class Combat3Phase0Cases
    {
        public static readonly string[] Names = {
            "SeparateEntry", "ExclusiveOwner", "LaneTransition", "HeldRepeatBounds", "QueueOne", "SimultaneousNeutral",
            "HeavyLocksLane", "AvoidHeavy", "StepHasNoImmunity", "OneContact", "RecoveryWindow", "FreshCounterOnly",
            "WindowEdges", "WeaponCounter", "SealNextCounter", "ReserveSpend", "ReserveCancel", "PauseFocus",
            "Backlog", "AccumulatedBacklog", "FixedClock", "ReloadKnownState", "VictoryPayment", "DeathRetry"
        };
        public static void Run(string name) { typeof(Combat3Phase0Cases).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null); }
        private static void Need(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Near(float want, float got, string why) { Need(Math.Abs(want - got) < .003f, why + ": expected " + want + ", got " + got); }
        // Missing API is an assertion failure during RED, as in the existing shared C2 scenarios.
        private static object Call(object o, string name, params object[] args) { var m = o.GetType().GetMethod(name); Need(m != null, "Missing C3 behavior: " + name); return m.Invoke(o, args); }
        private static T Read<T>(object o, string name) { var p = o.GetType().GetProperty(name); Need(p != null, "Missing C3 state: " + name); return (T)p.GetValue(o, null); }
        private static object Arena(GameSession s) { return Read<object>(s.Combat, "Combat3"); }
        private static GameSession Fight(float enemyHealth = 10000, int weapon = 1, float damage = 8)
        {
            var s = new GameSession(new SaveData { weaponLevel = weapon }, new ContractDefinition { enemyHealth = enemyHealth, enemyDamage = damage });
            s.AcceptContract(); s.LeaveHome(); Need((bool)Call(s, "EnterCombat3Review"), "Review portal starts encounter"); return s;
        }
        private static void Input(GameSession s, bool left = false, bool right = false, bool attack = false) { Call(s, "SetCombat3Input", left, right, attack); }
        private static void Frames(GameSession s, int count) { for (int i = 0; i < count; i++) s.Tick(1f / 60); }
        private static void Until(GameSession s, string stage)
        {
            for (int i = 0; i < 600 && Read<string>(Arena(s), "StageName") != stage; i++) Frames(s, 1);
            Need(Read<string>(Arena(s), "StageName") == stage, "Reach " + stage);
        }
        private static void Evade(GameSession s) { Input(s, right: Read<int>(Arena(s), "Lane") < 4, left: Read<int>(Arena(s), "Lane") == 4); Frames(s, 8); Input(s); }
        private static float Counter(GameSession s)
        {
            Until(s, "Telegraph"); Evade(s); Until(s, "CounterWindow"); float hp = s.State.enemyHp; Input(s, attack: true); Frames(s, 1); Input(s); return hp - s.State.enemyHp;
        }
        private static void SeparateEntry()
        {
            var s = new GameSession(new SaveData(), new ContractDefinition()); Need(!(bool)Call(s, "EnterCombat3Review"), "Home cannot bypass contract");
            s.AcceptContract(); s.LeaveHome(); s.EnterPortal(); Need(Arena(s) == null, "Ordinary portal retains C2"); Need(s.ClickAttack(false), "C2 manual attack remains");
            var c3 = Fight(); Near(2, Read<float>(Arena(c3), "LanePosition"), "Start center");
        }
        private static void ExclusiveOwner()
        {
            var s = Fight(); Need(!s.ClickAttack(false) && !s.BeginAttack() && !s.Dodge() && !s.Deflect() && !s.TraceRitualPoint(0), "C2 actions cannot bypass C3");
            float enemy = s.State.enemyHp; Frames(s, 600); Near(enemy, s.State.enemyHp, "Idle has no enemy damage");
        }
        private static void LaneTransition()
        {
            var s = Fight(); Input(s, left: true); Frames(s, 1); Need(Read<bool>(Arena(s), "IsMoving"), "Adjacent movement takes time");
            Need(Read<float>(Arena(s), "LanePosition") < 2 && Read<float>(Arena(s), "LanePosition") > 1, "Interpolated first movement step");
            Frames(s, 6); Need(Read<bool>(Arena(s), "IsMoving"), "0.1167 remains in transition"); Frames(s, 1); Near(1, Read<float>(Arena(s), "LanePosition"), "0.12 transition completes");
        }
        private static void HeldRepeatBounds()
        {
            var s = Fight(); Input(s, left: true); Frames(s, 10); Near(1, Read<float>(Arena(s), "LanePosition"), "Held repeat waits 0.18"); Frames(s, 1); Need(Read<bool>(Arena(s), "IsMoving"), "Held repeat after 0.18");
            Frames(s, 70); Near(0, Read<float>(Arena(s), "LanePosition"), "Left boundary"); Input(s, right: true); Frames(s, 100); Near(4, Read<float>(Arena(s), "LanePosition"), "Right boundary");
        }
        private static void QueueOne()
        {
            var s = Fight(); Input(s, left: true); Frames(s, 1); Input(s, right: true); Frames(s, 1); Input(s, left: true); Frames(s, 1); Input(s); Frames(s, 20);
            Near(2, Read<float>(Arena(s), "LanePosition"), "One pending adjacent direction; extra commands do not overwrite or grow queue");
        }
        private static void SimultaneousNeutral()
        {
            var s = Fight(); Input(s, left: true, right: true); Frames(s, 40); Near(2, Read<float>(Arena(s), "LanePosition"), "Opposed inputs neutral");
        }
        private static void HeavyLocksLane()
        {
            var s = Fight(); int id = Read<int>(Arena(s), "AttackId"); Need(Read<int>(Arena(s), "AttackLane") == 2, "Telegraph locks starting lane"); Evade(s);
            Need(Read<int>(Arena(s), "AttackLane") == 2, "Heavy never retargets after lock"); Until(s, "CounterWindow"); Until(s, "Telegraph"); Need(Read<int>(Arena(s), "AttackId") > id, "Next heavy has unique ID");
        }
        private static void AvoidHeavy() { var s = Fight(); Evade(s); Until(s, "CounterWindow"); Near(100, s.State.playerHp, "Lane avoidance prevents heavy contact"); }
        private static void StepHasNoImmunity()
        {
            var s = Fight(); Until(s, "Active"); Input(s, left: true); Frames(s, 1); Near(92, s.State.playerHp, "Swept starting lane contact hits ordinary step");
        }
        private static void OneContact()
        {
            var s = Fight(); int hits = 0; s.Combat.Hit += h => { if (!h.targetIsEnemy) hits++; }; Until(s, "Recovery"); Near(92, s.State.playerHp, "Heavy damage once"); Need(hits == 1, "One player event per attack instance");
        }
        private static void RecoveryWindow()
        {
            var s = Fight(); Until(s, "Recovery"); Frames(s, 14); Need(Read<string>(Arena(s), "StageName") == "Recovery", "0.25 recovery protects readability"); Frames(s, 1);
            Need(Read<string>(Arena(s), "StageName") == "CounterWindow", "Counter window after recovery even after damage"); Near(1.1f, Read<float>(Arena(s), "CounterWindowRemaining"), "Full 1.1 counter duration");
        }
        private static void FreshCounterOnly()
        {
            var s = Fight(); Input(s, attack: true); Until(s, "CounterWindow"); Frames(s, 2); Near(10000, s.State.enemyHp, "Early hold never buffers");
            Input(s); Input(s, attack: true); Frames(s, 1); Near(9991, s.State.enemyHp, "Fresh down succeeds after taking damage");
            Input(s); Input(s, attack: true); Frames(s, 1); Near(9991, s.State.enemyHp, "One counter per window"); Near(75, s.Combat.Seal, "Counter Seal cost"); Near(10, s.Combat.Resonance, "Counter resonance reward");
        }
        private static void WindowEdges()
        {
            var s = Fight(); Until(s, "Recovery"); Frames(s, 14); Input(s, attack: true); Frames(s, 1); Near(10000, s.State.enemyHp, "Down just before window does not buffer across step");
            Input(s); Frames(s, 65); Input(s, attack: true); Frames(s, 1); Near(9991, s.State.enemyHp, "Down in final open step succeeds before transition");
            var late = Fight(); Until(late, "CounterWindow"); Frames(late, 66); Input(late, attack: true); Frames(late, 1); Near(10000, late.State.enemyHp, "After window closes down is rejected");
        }
        private static void WeaponCounter() { var s = Fight(10000, 3); Near(12.6f, Counter(s), "Contract click 5 times weapon level3 multiplier1.4 times1.8"); }
        private static void SealNextCounter()
        {
            var s = Fight(); for (int i = 0; i < 4; i++) Near(9, Counter(s), "Breaking counter has no retroactive bonus");
            Near(0, s.Combat.Seal, "Seal zero pending"); Need(Read<bool>(s.Combat, "SealBonusPending"), "Single next-counter token"); Near(14.4f, Counter(s), "Next counter receives 60 percent"); Near(100, s.Combat.Seal, "Consumption resets seal");
        }
        private static void ReserveSpend()
        {
            var s = Fight(); for (int i = 0; i < 14; i++) Counter(s); Near(100, s.Combat.Resonance, "Gauge caps100"); Need(Read<bool>(s.Combat, "SealBonusPending"), "Seal token aligns at14th counter");
            Need(s.ActivateResonance(), "Reserve full gauge"); Near(100, s.Combat.Resonance, "Reservation does not spend"); Need(!s.ActivateResonance(), "Reservation cannot stack");
            Near(18.9f, Counter(s), "Additive1+.6+.5 maximum2.1"); Near(10, s.Combat.Resonance, "Spend on success before +10 reward"); Need(!Read<bool>(s.Combat, "ResonanceReserved"), "Consumed reservation");
        }
        private static void ReserveCancel()
        {
            var s = Fight(); for (int i = 0; i < 10; i++) Counter(s); s.ActivateResonance(); Call(s, "SetCombat3Paused", true); Need(!Read<bool>(s.Combat, "ResonanceReserved"), "Pause cancels reservation"); Near(100, s.Combat.Resonance, "Cancel preserves gauge");
            Call(s, "SetCombat3Paused", false); Frames(s, 36); s.ActivateResonance(); Call(s, "SetCombat3Focused", false); Need(!Read<bool>(s.Combat, "ResonanceReserved"), "Focus cancels reserve");
            Call(s, "SetCombat3Focused", true); Frames(s, 36); s.ActivateResonance(); Until(s, "Telegraph"); Until(s, "Recovery"); Need(!Read<bool>(s.Combat, "ResonanceReserved"), "Damage cancels reserve"); Near(90, s.Combat.Resonance, "Damage loses10");
        }
        private static void PauseFocus()
        {
            var s = Fight(); Until(s, "CounterWindow"); Input(s, attack: true); Call(s, "SetCombat3Paused", true); float time = s.State.combatTime; Frames(s, 120); Near(time, s.State.combatTime, "Pause stops clock");
            Call(s, "SetCombat3Paused", false); Input(s, attack: true); Frames(s, 36); Near(time, s.State.combatTime, "Resume read delay0.6"); Frames(s, 1); Near(10000, s.State.enemyHp, "Held LMB across pause never fresh");
            Input(s); Input(s, attack: true); Frames(s, 1); Near(9991, s.State.enemyHp, "Release rearms after resume"); Call(s, "SetCombat3Focused", false); time = s.State.combatTime; Frames(s, 120); Near(time, s.State.combatTime, "Focus loss stops clock");
        }
        private static void Backlog()
        {
            var s = Fight(); float time = s.State.combatTime; s.Tick(.101f); Near(time, s.State.combatTime, "Unsafe backlog never fastforwards"); Need(Read<bool>(Arena(s), "SuspendedForBacklog"), "Visible backlog suspension");
            Frames(s, 36); Near(time, s.State.combatTime, "Backlog read delay"); Frames(s, 1); Need(s.State.combatTime > time, "Fresh frames resume after read delay");
        }
        private static void FixedClock()
        {
            var a = Fight(); var b = Fight(); Input(a, left: true); Input(b, left: true); for (int i = 0; i < 30; i++) a.Tick(1f / 30); Frames(b, 60);
            Near(a.State.combatTime, b.State.combatTime, "Clock partition consistent"); Near(Read<float>(Arena(a), "LanePosition"), Read<float>(Arena(b), "LanePosition"), "Movement partition consistent"); Near(a.State.playerHp, b.State.playerHp, "Collision partition consistent");
        }
        private static void AccumulatedBacklog()
        {
            var s = Fight(); s.Tick(.01f); s.Tick(.1f);
            Near(0, s.State.combatTime, "Partial-step accumulator plus new frame exceeding0.1 suspends without any fastforward");
            Need(Read<bool>(Arena(s), "SuspendedForBacklog"), "Accumulated backlog exposes suspension");
        }
        private static void ReloadKnownState()
        {
            var s = Fight(); Counter(s); int run = s.State.contractRunSequence; var reload = new GameSession(s.State, s.Contract); Need(Arena(reload) != null, "Saved review mode restored"); Near(10000, reload.State.enemyHp, "Reload restarts enemy"); Near(100, reload.State.playerHp, "Reload restarts player"); Need(run == reload.State.contractRunSequence && reload.State.completedRuns == 0 && reload.State.yen == 600, "Reload retains run without rewards");
            var c2 = new GameSession(new SaveData(), new ContractDefinition()); c2.AcceptContract(); c2.LeaveHome(); c2.EnterPortal(); c2.ClickAttack(false); var old = new GameSession(c2.State, c2.Contract); Near(175, old.State.enemyHp, "C2 save HP remains"); Need(Arena(old) == null, "Absent optional flag defaults C2");
        }
        private static void VictoryPayment()
        {
            var s = Fight(9); Counter(s); Need(s.State.phase == RunPhase.Sealed, "Counter uses existing victory"); Frames(s, 200); Need(s.ReturnHome(), "Existing result return"); Need(s.ClaimPayment(), "Existing payment"); Need(!s.ClaimPayment() && s.State.completedRuns == 1 && s.State.yen == 2400, "Exactly once reward"); Need(Arena(s) == null, "Return disposes encounter");
        }
        private static void DeathRetry()
        {
            var s = Fight(damage: 100); Until(s, "Active"); Frames(s, 1); Need(s.State.phase == RunPhase.Failed, "Shared player HP death"); Need(s.ReturnHome() && !s.ClaimPayment(), "Death never pays");
            s.AcceptContract(); s.LeaveHome(); s.EnterPortal(); Need(Arena(s) == null && s.ClickAttack(false), "Normal retry restores C2 driver");
        }
    }
}
