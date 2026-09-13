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
            "Backlog", "AccumulatedBacklog", "FixedClock", "ReloadKnownState", "VictoryPayment", "DeathRetry",
            "DodgeStationary", "PracticeGate", "MixedPatterns", "WaveAllLanes", "WaveStepCannotEvade",
            "DodgeDirectional", "DodgeBounds", "DodgeCooldown", "EmptyDodge", "PerfectContact",
            "PerfectBoundary", "DodgeImmunityBoundary", "DodgeOncePerAttack", "DodgeHeavyContact",
            "DodgeHeavyEmptyLane", "DodgeCancellation", "PerfectSealToken", "WaveFreshCounter", "DeflectStarts",
            "ProjectileMarked", "ProjectileAvoidance", "DeflectContact", "DeflectSibling", "DeflectBoundary",
            "DeflectWrongFamily", "EmptyDeflect", "DeflectSharedCooldown", "DeflectLateImmunity", "DeflectCancellation",
            "DeflectFeedbackCancellation", "DeflectSealToken", "ProjectileLifecycle", "ProjectileClock",
            "ProjectileEdges", "ProjectileEdgeSibling", "DeflectSecondInstance", "ProjectileCleanup"
        };
        public static void Run(string name) { typeof(Combat3Phase0Cases).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null); }
        private static void Need(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Near(float want, float got, string why) { Need(Math.Abs(want - got) < .003f, why + ": expected " + want + ", got " + got); }
        // Missing API is an assertion failure during RED, as in the existing shared C2 scenarios.
        private static object Call(object o, string name, params object[] args) { var m = o.GetType().GetMethod(name); Need(m != null, "Missing C3 behavior: " + name); return m.Invoke(o, args); }
        private static T Read<T>(object o, string name) { var p = o.GetType().GetProperty(name); Need(p != null, "Missing C3 state: " + name); return (T)p.GetValue(o, null); }
        private static object Arena(GameSession s) { return Read<object>(s.Combat, "Combat3"); }
        private static GameSession Fight(float enemyHealth = 10000, int weapon = 1, float damage = 8, bool mixed = false)
        {
            var s = new GameSession(new SaveData { weaponLevel = weapon }, new ContractDefinition { enemyHealth = enemyHealth, enemyDamage = damage });
            s.AcceptContract(); s.LeaveHome();
            Need(mixed ? s.EnterCombat3Review() : s.EnterCombat3Practice(Combat3Practice.Heavy), "Selected review practice starts encounter"); return s;
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
            var s = Fight(); Need(!s.ClickAttack(false) && !s.BeginAttack() && !s.TraceRitualPoint(0), "C2 attacks cannot bypass C3");
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

        private static GameSession Practice(string kind)
        {
            var s = new GameSession(new SaveData(), new ContractDefinition { enemyHealth = 10000, enemyDamage = 8 });
            s.AcceptContract(); s.LeaveHome();
            var method = typeof(GameSession).GetMethod("EnterCombat3Practice");
            Need(method != null, "Missing C3 practice entry");
            var selection = Enum.Parse(method.GetParameters()[0].ParameterType, kind);
            Need((bool)method.Invoke(s, new[] { selection }), "Practice enters same combat owner");
            return s;
        }
        private static void DeflectStarts()
        {
            var s = Fight(); Need(s.Deflect(), "Fresh Space starts C3 deflect through existing owner");
            Near(.55f, s.Combat.DefenseCooldownRemaining, "Deflect starts shared cooldown");
            Near(.14f, Read<float>(Arena(s), "DeflectRemaining"), "Deflect active duration");
        }
        private static object[] Attacks(GameSession s)
        {
            var values = new System.Collections.Generic.List<object>();
            foreach (object attack in Read<System.Collections.IEnumerable>(Arena(s), "Attacks")) values.Add(attack);
            return values.ToArray();
        }
        private static void ProjectileMarked()
        {
            Need(Enum.IsDefined(typeof(Combat3Practice), "Projectile"), "Projectile practice is an explicit gated family");
            var s = Practice("Projectile"); var attacks = Attacks(s);
            Need(attacks.Length == 2, "Projectile family contains two real overlapping attack lifetimes");
            Need(Read<int>(attacks[0], "Id") != Read<int>(attacks[1], "Id"), "Sibling IDs unique");
            Need(Read<bool>(attacks[0], "IsDeflectable") && Read<bool>(attacks[1], "IsDeflectable"), "Both projectiles marked");
            Need(Read<int>(attacks[0], "Lane") == 2 && Read<int>(attacks[1], "Lane") == 3, "Locked adjacent projectile lanes");
            Near(0, Read<float>(attacks[0], "ApproachProgress"), "Travel begins at origin");
            Frames(s, 54); Near(1, Read<float>(attacks[0], "ApproachProgress"), "Primary reaches contact");
            Near(.4f, Read<float>(attacks[1], "ContactDelayRemaining"), "Sibling arrival remains delayed");
            Need(Read<string>(attacks[1], "StateName") == "Telegraph", "Sibling is still approaching");
        }
        private static void ProjectileAvoidance()
        {
            var s = Practice("Projectile"); Input(s, left: true); Frames(s, 8); Input(s); Until(s, "CounterWindow");
            Near(100, s.State.playerHp, "Ordinary lane movement avoids both projectiles");
            Near(100, s.Combat.Seal, "Avoidance has no deflect reward"); Near(0, s.Combat.Resonance, "Avoidance has no resonance");
        }
        private static void DeflectContact()
        {
            var s = Practice("Projectile"); int hits = 0; s.Combat.Hit += h => hits++;
            Until(s, "Active"); int id = Read<int>(Arena(s), "AttackId"); Need(s.Deflect(), "Space accepted before contact"); Frames(s, 1);
            Near(100, s.State.playerHp, "Deflect precedes player HP damage"); Near(10000, s.State.enemyHp, "Return has no enemy damage");
            Near(88, s.Combat.Seal, "Deflect Seal minus12"); Near(12, s.Combat.Resonance, "Deflect resonance plus12");
            Need(hits == 0 && Read<int>(Arena(s), "LastDeflectedAttackId") == id, "Feedback identifies actual instance without damage event");
            Need(Read<string>(Arena(s), "AttackStateName") == "Deflected", "Primary resolves by ID");
            Until(s, "CounterWindow"); Near(12, s.Combat.Resonance, "Repeated overlap cannot duplicate deflect reward"); Near(88, s.Combat.Seal, "One Seal reward per instance");
        }
        private static void DeflectSibling()
        {
            var s = Practice("Projectile"); Until(s, "Active"); var attacks = Attacks(s); int sibling = Read<int>(attacks[1], "Id");
            s.Deflect(); Frames(s, 1);
            Need(Attacks(s).Length == 2 && Read<int>(Attacks(s)[1], "Id") == sibling && Read<string>(attacks[1], "StateName") == "Telegraph", "Resolving one instance preserves approaching sibling");
            Input(s, right: true); Frames(s, 8); Input(s); Frames(s, 15);
            Need(Read<string>(Arena(s), "StageName") == "Active" && Read<string>(attacks[1], "StateName") == "Active", "Counter waits for sibling arrival");
            Frames(s, 1); Near(92, s.State.playerHp, "Surviving sibling inflicts later real damage");
            Near(88, s.Combat.Seal, "Sibling was not globally deflected"); Need(Read<string>(attacks[1], "StateName") == "Hit", "Sibling has independent resolution");
        }
        private static void DeflectBoundary()
        {
            var inside = Practice("Projectile"); Frames(inside, 46); inside.Deflect(); Until(inside, "Recovery");
            Near(100, inside.State.playerHp, "Contact at0.1333 is inside0.14"); Near(12, inside.Combat.Resonance, "Inside boundary rewards");
            var early = Practice("Projectile"); Frames(early, 45); early.Deflect(); Until(early, "Recovery");
            Near(92, early.State.playerHp, "Contact at0.15 is outside0.14"); Near(100, early.Combat.Seal, "Early defense cannot reward");
        }
        private static void DeflectWrongFamily()
        {
            foreach (string kind in new[] { "Heavy", "LowWave" })
            {
                var s = Practice(kind); Until(s, "Active"); Need(s.Deflect(), "Space can start against wrong family"); Frames(s, 1);
                Near(92, s.State.playerHp, "Deflect cannot protect against " + kind); Near(100, s.Combat.Seal, "Wrong family grants no Seal"); Near(0, s.Combat.Resonance, "Wrong family grants no resonance");
            }
        }
        private static void EmptyDeflect()
        {
            var s = Practice("Projectile"); Need(s.Deflect(), "Empty deflect starts"); Frames(s, 8);
            Need(Read<float>(Arena(s), "DeflectRemaining") > 0, "Deflect still live at0.1333"); Frames(s, 1);
            Near(0, Read<float>(Arena(s), "DeflectRemaining"), "Deflect expired by0.15"); Near(100, s.Combat.Seal, "Empty deflect grants no Seal"); Near(0, s.Combat.Resonance, "Empty deflect grants no resonance");
            var miss = Practice("Projectile"); Input(miss, left: true); Frames(miss, 8); Input(miss); Until(miss, "Active"); miss.Deflect(); Frames(miss, 1);
            Near(0, miss.Combat.Resonance, "Deflect in empty lane grants nothing"); Near(0, Read<float>(Arena(miss), "DeflectFeedbackRemaining"), "No return for empty lane");
        }
        private static void DeflectSharedCooldown()
        {
            var s = Fight(); s.Deflect(); Need(!s.Dodge() && !s.Deflect(), "Deflect locks both defenses"); Frames(s, 32);
            Need(!s.Dodge(), "Shared cooldown blocks before0.55"); Frames(s, 1); Need(s.Dodge(), "Dodge allowed after deflect cooldown");
            Frames(s, 33); Need(s.Deflect(), "Deflect allowed after dodge cooldown");
        }
        private static void DeflectLateImmunity()
        {
            var s = Practice("Projectile"); Until(s, "Active"); Frames(s, 1); Near(92, s.State.playerHp, "Late defense follows real hit");
            Need(s.Deflect(), "Late deflect may start but cannot undo hit"); Frames(s, 1); Near(100, s.Combat.Seal, "Already-hit instance grants no deflect");
            Input(s, right: true); Frames(s, 8); Input(s); Frames(s, 24); Need(s.Combat.DamageImmunityRemaining > 0, "Sibling contact falls inside hit immunity");
            Need(s.Deflect(), "Fresh deflect still allowed after cooldown during immunity"); Frames(s, 1);
            Near(100, s.Combat.Seal, "Contact already harmless through hit immunity never rewards"); Near(0, s.Combat.Resonance, "No immunity farming"); Near(92, s.State.playerHp, "No duplicate damage");
        }
        private static void DeflectCancellation()
        {
            for (int mode = 0; mode < 4; mode++)
            {
                var s = Practice("Projectile"); Until(s, "Active"); s.Deflect();
                if (mode == 0) { s.SetCombat3Paused(true); Need(!s.Deflect(), "Paused rejects Space"); s.SetCombat3Paused(false); }
                if (mode == 1) { s.SetCombat3Focused(false); Need(!s.Deflect(), "Unfocused rejects Space"); s.SetCombat3Focused(true); }
                if (mode == 2) s.Tick(.101f);
                if (mode == 3) s.CancelCombatInput();
                Near(0, Read<float>(Arena(s), "DeflectRemaining"), "Cancellation clears queued defense");
                if (mode != 3) { Need(!s.Deflect(), "Read delay rejects Space"); Frames(s, 36); }
                Frames(s, 1); Near(92, s.State.playerHp, "Cancelled deflect cannot protect resumed contact"); Near(100, s.Combat.Seal, "Cancelled deflect grants nothing");
            }
        }
        private static void DeflectFeedbackCancellation()
        {
            for (int mode = 0; mode < 4; mode++)
            {
                var s = Practice("Projectile"); Until(s, "Active"); s.Deflect(); Frames(s, 1);
                Need(Read<float>(Arena(s), "DeflectFeedbackRemaining") > 0, "Actual deflect produces feedback");
                if (mode == 0) s.SetCombat3Paused(true);
                if (mode == 1) s.SetCombat3Focused(false);
                if (mode == 2) s.Tick(.101f);
                if (mode == 3) s.CancelCombatInput();
                Near(0, Read<float>(Arena(s), "DeflectRemaining"), "Cancellation clears active defense"); Near(0, Read<float>(Arena(s), "DeflectFeedbackRemaining"), "Cancellation clears return");
                Need(Read<int>(Arena(s), "LastDeflectedAttackId") == 0, "Cancellation clears feedback identity");
            }
        }
        private static void DeflectSealToken()
        {
            var s = Practice("Projectile");
            for (int i = 0; i < 9; i++) { Until(s, "Active"); s.Deflect(); Until(s, "CounterWindow"); if (i < 8) Until(s, "Telegraph"); }
            Near(0, s.Combat.Seal, "Deflect clamps Seal"); Near(100, s.Combat.Resonance, "Deflect caps resonance"); Need(s.Combat.SealBonusPending, "Deflect uses shared next-counter token");
            Need(s.ActivateResonance(), "Deflect-earned gauge can reserve"); s.SetCombat3Paused(true); Need(!s.Combat.ResonanceReserved, "Deflect practice suspension clears reservation"); s.SetCombat3Paused(false); Frames(s, 36);
            Input(s); Input(s, attack: true); Frames(s, 1); Near(9985.6f, s.State.enemyHp, "Counter consumes deflect-earned Seal bonus only once"); Near(100, s.Combat.Seal, "Counter resets shared Seal");
        }
        private static void ProjectileLifecycle()
        {
            var s = Practice("Projectile"); Until(s, "Active"); s.Deflect(); Frames(s, 1); var old = Arena(s);
            int run = s.State.contractRunSequence; var reload = new GameSession(s.State, s.Contract);
            Near(100, reload.State.playerHp, "Projectile reload uses known initial HP"); Need(reload.State.contractRunSequence == run && reload.State.completedRuns == 0, "Reload never creates payment");
            // Finish through the real result path while feedback/commands exist in the encounter.
            var win = new GameSession(new SaveData(), new ContractDefinition { enemyHealth = 9 }); win.AcceptContract(); win.LeaveHome(); win.EnterCombat3Practice((Combat3Practice)Enum.Parse(typeof(Combat3Practice), "Projectile"));
            Until(win, "CounterWindow"); win.Deflect(); Input(win, attack: true); Frames(win, 1); var completed = Arena(win);
            Need(win.State.phase == RunPhase.Sealed && Read<string>(completed, "StageName") == "Complete", "Projectile counter uses shared victory");
            Near(0, Read<float>(completed, "DeflectRemaining"), "Completion clears pending deflect"); Near(0, Read<float>(completed, "DeflectFeedbackRemaining"), "Completion clears return");
            foreach (var attack in Attacks(win)) Need(Read<string>(attack, "StateName") == "Cleanup", "Completion cleans every instance");
            Need(win.ReturnHome() && win.ClaimPayment() && !win.ClaimPayment(), "Projectile result pays exactly once");
        }
        private static void ProjectileClock()
        {
            var a = Practice("Projectile"); var b = Practice("Projectile"); Frames(a, 54); for (int i = 0; i < 27; i++) b.Tick(1f / 30);
            a.Deflect(); b.Deflect(); Frames(a, 36); for (int i = 0; i < 18; i++) b.Tick(1f / 30);
            Near(a.State.playerHp, b.State.playerHp, "Projectile contact partition independent"); Near(a.Combat.Seal, b.Combat.Seal, "Deflect resolution partition independent");
            Need(Read<string>(Attacks(a)[1], "StateName") == Read<string>(Attacks(b)[1], "StateName"), "Sibling lifetime partition independent");
        }
        private static void ProjectileCleanup()
        {
            var s = Practice("Projectile"); Until(s, "CounterWindow");
            foreach (var attack in Attacks(s)) Need(Read<string>(attack, "StateName") == "Cleanup", "Every projectile releases its lifetime before counter window");
        }
        private static GameSession ProjectileAtEdge(int edge)
        {
            var s = Practice("Projectile"); Input(s, left: edge == 0, right: edge == 4); Frames(s, 32); Input(s);
            Until(s, "CounterWindow"); int previousSibling = Read<int>(Attacks(s)[1], "Id"); Until(s, "Telegraph");
            var attacks = Attacks(s);
            Need(Read<int>(attacks[0], "Lane") == edge && Read<int>(attacks[1], "Lane") == (edge == 0 ? 1 : 3), "Both edge projectiles lock inside arena");
            Need(Read<int>(attacks[0], "Id") > previousSibling, "Next pattern preserves globally unique IDs");
            return s;
        }
        private static void ProjectileEdges()
        {
            foreach (int edge in new[] { 0, 4 })
            {
                var s = ProjectileAtEdge(edge); Input(s, left: edge == 0, right: edge == 4); Frames(s, 8); Input(s);
                Near(edge, Read<float>(Arena(s), "LanePosition"), "Outward ordinary movement stays in boundary");
                Input(s, left: edge == 4, right: edge == 0); Frames(s, 20); Input(s);
                Need(Read<int>(Attacks(s)[0], "Lane") == edge, "Movement never retargets locked edge projectile");
                Until(s, "CounterWindow"); Near(100, s.State.playerHp, "Ordinary inward movement avoids both edge projectiles");
                Near(0, s.Combat.Resonance, "Edge avoidance grants no defense reward");
            }
        }
        private static void ProjectileEdgeSibling()
        {
            foreach (int edge in new[] { 0, 4 })
            {
                var s = ProjectileAtEdge(edge); Until(s, "Active"); int siblingId = Read<int>(Attacks(s)[1], "Id");
                Need(s.Deflect(), "Edge deflect accepted"); Frames(s, 1); Input(s, left: edge == 4, right: edge == 0); Frames(s, 8); Input(s); Frames(s, 16);
                Near(92, s.State.playerHp, "Sibling survives primary deflect and contacts inward adjacent lane");
                Need(Read<int>(Attacks(s)[1], "Id") == siblingId && Read<string>(Attacks(s)[1], "StateName") == "Hit", "Boundary sibling retains ownership and resolves independently");
            }
        }
        private static void DeflectSecondInstance()
        {
            var s = Practice("Projectile"); Input(s, right: true); Frames(s, 8); Input(s); Until(s, "Active"); Frames(s, 24);
            var attacks = Attacks(s); int siblingId = Read<int>(attacks[1], "Id"); Need(s.Deflect(), "Deflect approaching sibling after avoiding primary"); Frames(s, 1);
            Need(Read<int>(Arena(s), "LastDeflectedAttackId") == siblingId && Read<int>(Arena(s), "LastDeflectedLane") == 3, "Return belongs to actual colliding sibling");
            Need(Read<string>(attacks[0], "StateName") == "Resolved" && Read<string>(attacks[1], "StateName") == "Deflected", "Deflect changes only colliding second instance");
            Near(100, s.State.playerHp, "Sibling deflect prevents contact"); Near(88, s.Combat.Seal, "Only actual sibling gives reward"); Near(12, s.Combat.Resonance, "One sibling reward");
        }
        private static void DodgeStationary()
        {
            var s = Fight(); Need(s.Dodge(), "Fresh RMB starts C3 stationary dodge"); Frames(s, 8);
            Near(2, Read<float>(Arena(s), "LanePosition"), "No direction leaves player in lane");
            Near(10000, s.State.enemyHp, "Dodge never causes enemy damage");
        }
        private static void PracticeGate()
        {
            var s = new GameSession(new SaveData(), new ContractDefinition());
            var method = typeof(GameSession).GetMethod("EnterCombat3Practice"); Need(method != null, "Missing C3 practice entry");
            var wave = Enum.Parse(method.GetParameters()[0].ParameterType, "LowWave");
            Need(!(bool)method.Invoke(s, new[] { wave }), "Practice cannot bypass contract from Home");
            s.AcceptContract(); s.LeaveHome();
            var invalid = Enum.ToObject(method.GetParameters()[0].ParameterType, 99);
            Need(!(bool)method.Invoke(s, new[] { invalid }), "Unknown practice rejected before starting combat");
            Need(s.State.phase == RunPhase.Portal, "Rejected selection preserves portal");
        }
        private static void MixedPatterns()
        {
            var s = Fight(mixed: true); int first = Read<int>(Arena(s), "AttackId");
            Need(Read<string>(Arena(s), "AttackKindName") == "Heavy", "Review starts readable heavy");
            Evade(s); Until(s, "CounterWindow"); Until(s, "Telegraph");
            Need(Read<string>(Arena(s), "AttackKindName") == "LowWave", "Review includes second family");
            Need(Read<int>(Arena(s), "AttackId") > first, "Mixed attack IDs remain unique");
            Until(s, "CounterWindow"); Until(s, "Telegraph");
            Need(Read<string>(Arena(s), "AttackKindName") == "Projectile", "Mixed pattern includes third projectile family after prior two");
        }
        private static void WaveAllLanes()
        {
            for (int lane = 0; lane < 5; lane++)
            {
                var s = Practice("LowWave");
                while (Read<int>(Arena(s), "Lane") != lane) { Input(s, left: lane < 2, right: lane > 2); Frames(s, 8); Input(s); }
                Until(s, "Recovery"); Near(92, s.State.playerHp, "Wave contacts lane " + lane);
            }
        }
        private static void WaveStepCannotEvade()
        {
            var s = Practice("LowWave"); Until(s, "Active"); Input(s, right: true); Frames(s, 1);
            Near(92, s.State.playerHp, "Ordinary movement offers no immunity from all-lane wave");
        }
        private static void DodgeDirectional()
        {
            foreach (bool left in new[] { true, false })
            {
                var s = Fight(); Input(s, left: left, right: !left); Need(s.Dodge(), "Directional dodge accepted"); Input(s); Frames(s, 20);
                Near(left ? 1 : 3, Read<float>(Arena(s), "LanePosition"), "Dodge consumes sampled direction once without queued extra step");
            }
            var neutral = Fight(); Input(neutral, left: true, right: true); Need(neutral.Dodge(), "Opposed direction dodge accepted"); Frames(neutral, 8);
            Near(2, Read<float>(Arena(neutral), "LanePosition"), "Opposed directions dodge stationary");
        }
        private static void DodgeBounds()
        {
            foreach (bool left in new[] { true, false })
            {
                var s = Fight(); Input(s, left: left, right: !left); Frames(s, 32); Need(s.Dodge(), "Outward dodge at boundary still grants defense"); Input(s); Frames(s, 8);
                Near(left ? 0 : 4, Read<float>(Arena(s), "LanePosition"), "Dodge clamps arena boundary");
            }
        }
        private static void DodgeCooldown()
        {
            var s = Fight(); Need(s.Dodge(), "First dodge accepted"); Near(.55f, s.Combat.DefenseCooldownRemaining, "Shared cooldown starts on down");
            Need(!s.Dodge() && !s.Deflect(), "No repeat or C2 defense bypass during cooldown"); Frames(s, 32);
            Need(!s.Dodge(), "Cooldown rejects at0.5333"); Frames(s, 1); Need(s.Dodge(), "Cooldown rearms at0.55");
        }
        private static void EmptyDodge()
        {
            var s = Fight(); Need(s.Dodge(), "Empty dodge starts"); Frames(s, 12);
            Near(0, Read<float>(Arena(s), "DodgeRemaining"), "Dodge has expired");
            Near(100, s.Combat.Seal, "Empty dodge never reduces Seal"); Near(0, s.Combat.Resonance, "Empty dodge never grants resonance");
            Need(!Read<bool>(Arena(s), "LastPerfect"), "Empty dodge is not Perfect");
        }
        private static void PerfectContact()
        {
            var s = Practice("LowWave"); int hits = 0; s.Combat.Hit += h => { if (!h.targetIsEnemy) hits++; };
            Until(s, "Active"); Need(s.Dodge(), "Same-step fresh dodge accepted before contact"); Frames(s, 1);
            Near(100, s.State.playerHp, "Dodge prevents actual wave contact"); Need(hits == 0, "Prevented contact emits no damage event");
            Near(92, s.Combat.Seal, "Perfect uses shared Seal minus8"); Near(8, s.Combat.Resonance, "Perfect uses shared Resonance plus8");
            Need(Read<bool>(Arena(s), "LastPerfect") && Read<float>(Arena(s), "PerfectFeedbackRemaining") > 0, "Actual Perfect has feedback");
        }
        private static void PerfectBoundary()
        {
            var early = Practice("LowWave"); Frames(early, 50); Need(early.Dodge(), "Dodge before contact"); Until(early, "Recovery");
            Near(100, early.State.playerHp, "Contact at0.0667 is prevented"); Near(8, early.Combat.Resonance, "First0.08 yields Perfect");
            var normal = Practice("LowWave"); Frames(normal, 49); Need(normal.Dodge(), "Earlier dodge starts"); Until(normal, "Recovery");
            Near(100, normal.State.playerHp, "Contact at0.0833 still dodged"); Near(0, normal.Combat.Resonance, "After0.08 grants no Perfect"); Near(100, normal.Combat.Seal, "Normal dodge grants no Seal reward");
        }
        private static void DodgeImmunityBoundary()
        {
            var inside = Practice("LowWave"); Frames(inside, 44); Need(inside.Dodge(), "Dodge before wave"); Until(inside, "Recovery"); Near(100, inside.State.playerHp, "Contact at0.1667 is inside0.18");
            var outside = Practice("LowWave"); Frames(outside, 43); Need(outside.Dodge(), "Early dodge before wave"); Until(outside, "Recovery"); Near(92, outside.State.playerHp, "Contact at0.1833 is outside0.18");
        }
        private static void DodgeOncePerAttack()
        {
            var s = Practice("LowWave"); Until(s, "Active"); s.Dodge(); Until(s, "CounterWindow");
            Near(8, s.Combat.Resonance, "Repeated overlap rewards once per attack/dodge"); Near(92, s.Combat.Seal, "One Seal reward");
            Until(s, "Telegraph"); Until(s, "Active"); s.Dodge(); Until(s, "CounterWindow");
            Near(16, s.Combat.Resonance, "New attack and fresh dodge can reward again"); Near(84, s.Combat.Seal, "Next instance has independent reward");
        }
        private static void DodgeHeavyContact()
        {
            var s = Fight(); Until(s, "Active"); Need(s.Dodge(), "Heavy can be dodged"); Until(s, "CounterWindow");
            Near(100, s.State.playerHp, "Heavy collision prevented"); Near(8, s.Combat.Resonance, "Actual heavy contact can be Perfect");
        }
        private static void DodgeHeavyEmptyLane()
        {
            var s = Fight(); Evade(s); Until(s, "Active"); Need(s.Dodge(), "Dodge outside heavy lane"); Until(s, "CounterWindow");
            Near(100, s.State.playerHp, "Heavy avoided by positioning"); Near(0, s.Combat.Resonance, "No contact means no Perfect even during active heavy");
        }
        private static void DodgeCancellation()
        {
            for (int mode = 0; mode < 4; mode++)
            {
                var s = Practice("LowWave"); Until(s, "Active"); Need(s.Dodge(), "Dodge accepted before interruption");
                if (mode == 0) { Call(s, "SetCombat3Paused", true); Need(!s.Dodge(), "Paused dodge rejected"); Call(s, "SetCombat3Paused", false); }
                if (mode == 1) { Call(s, "SetCombat3Focused", false); Need(!s.Dodge(), "Unfocused dodge rejected"); Call(s, "SetCombat3Focused", true); }
                if (mode == 2) s.Tick(.101f);
                if (mode == 3) s.CancelCombatInput();
                Near(0, Read<float>(Arena(s), "DodgeRemaining"), "Interruption clears active/queued defense");
                if (mode != 3) { Need(!s.Dodge(), "Read delay rejects dodge"); Frames(s, 36); }
                Frames(s, 1); Near(92, s.State.playerHp, "No stale dodge prevents resumed contact"); Near(0, s.Combat.Resonance, "No cancelled Perfect reward");
            }
        }
        private static void PerfectSealToken()
        {
            var s = Practice("LowWave");
            for (int i = 0; i < 13; i++) { Until(s, "Active"); Need(s.Dodge(), "Each wave can be dodged"); Until(s, "CounterWindow"); if (i < 12) Until(s, "Telegraph"); }
            Near(0, s.Combat.Seal, "Perfect clamps shared Seal at zero"); Near(100, s.Combat.Resonance, "Perfect clamps shared resonance at100");
            Need(s.Combat.SealBonusPending, "Perfect break creates same next-counter token");
            Input(s, attack: true); Frames(s, 1); Near(9985.6f, s.State.enemyHp, "Counter consumes Perfect-earned60percent token"); Near(100, s.Combat.Seal, "Token consumed once and Seal reset");
        }
        private static void WaveFreshCounter()
        {
            var s = Practice("LowWave"); Input(s, attack: true); Until(s, "Active"); s.Dodge(); Until(s, "CounterWindow"); Frames(s, 1);
            Near(10000, s.State.enemyHp, "Dodge cannot rearm held LMB"); Input(s); Input(s, attack: true); Frames(s, 1);
            Near(9991, s.State.enemyHp, "Fresh wave counter uses shared damage"); Near(67, s.Combat.Seal, "Perfect and counter share Seal"); Near(18, s.Combat.Resonance, "Perfect and counter share resonance");
        }
    }
}
