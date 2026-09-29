using System;
using System.Reflection;
using Rokas.Core;
namespace Rokas.Core.Tests
{
    // Shared behavioral scenarios: the console runner and Unity execute identical rules.
    public static class CombatFoundationCases
    {
        public static readonly string[] Names = { "Combo", "Spam", "ComboExpiry", "EarlyCharge", "Charged", "PerfectCut", "OverheldCharge", "Dodge", "Deflect", "DefenseCooldown", "SealBreak", "RitualSuccess", "RitualReleaseFailure", "RitualOrderFailure", "RitualTimeout", "Resonance", "ResonanceExpiry", "Phases", "CancelCharge", "RetryReset", "InactiveGuards", "TickPartition", "CancelledCombo" };
        public static void Run(string name) { typeof(CombatFoundationCases).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null); }
        private static GameSession Fight()
        {
            var s = new GameSession(new SaveData(), new ContractDefinition()); s.AcceptContract(); s.LeaveHome(); s.EnterPortal(); return s;
        }
        // Reflection makes missing public behavior fail an assertion in RED, before introducing the API.
        private static bool Do(GameSession s, string method, params object[] args)
        {
            var m = typeof(GameSession).GetMethod(method); Require(m != null, "Missing combat action: " + method); return (bool)m.Invoke(s, args);
        }
        private static T Read<T>(GameSession s, string property)
        {
            var p = typeof(CombatService).GetProperty(property); Require(p != null, "Missing combat state: " + property); return (T)p.GetValue(s.Combat, null);
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Near(float a, float b, string message) { Require(Math.Abs(a-b) < .002f, message + ": " + a + " vs " + b); }
        private static void Charge(GameSession s, float duration) { Require(Do(s,"BeginAttack"),"Begin hold"); s.Tick(duration); Require(Do(s,"ReleaseAttack"),"Release hold"); }
        private static void Parry(GameSession s) { s.Tick(Math.Max(0, s.State.enemyTimer-.10f)); Require(Do(s,"Deflect"),"Deflect telegraphed strike"); }
        private static void Break(GameSession s) { for(int i=0;i<4;i++) Parry(s); Require(Read<string>(s,"StageName")=="SealBreak","Seal enters break"); s.Tick(.36f); Require(Read<string>(s,"StageName")=="Ritual","Break opens ritual"); }
        private static void Ritual(GameSession s) { Require(Do(s,"BeginAttack"),"Hold ritual"); for(int i=0;i<3;i++) Require(Do(s,"TraceRitualPoint",i),"Trace point "+i); }
        private static void Combo() { var s=Fight(); s.ClickAttack(false); s.Tick(.48f); s.ClickAttack(false); s.Tick(.48f); s.ClickAttack(false); Require(Read<int>(s,"ComboStep")==3,"Three-hit combo"); Require(Read<bool>(s,"LastPerfect"),"Timed finisher is Perfect"); Require(s.State.enemyHp < 160,"Finisher stronger than basics"); }
        private static void Spam() { var s=Fight(); s.ClickAttack(false); s.Tick(.15f); Require(!s.ClickAttack(false),"No clicker spam"); Near(175,s.State.enemyHp,"Rejected attack does no damage"); }
        private static void ComboExpiry() { var s=Fight(); s.ClickAttack(false); s.Tick(.9f); s.ClickAttack(false); Require(Read<int>(s,"ComboStep")==1,"Late attack resets chain"); }
        private static void EarlyCharge() { var s=Fight(); Charge(s,.4f); Require(Read<string>(s,"LastAction")=="WeakCut","Early release weak"); Near(176.75f,s.State.enemyHp,"Weak damage"); }
        private static void Charged() { var s=Fight(); Charge(s,.7f); Require(Read<string>(s,"LastAction")=="Charged","Normal charge"); Near(78,Read<float>(s,"Seal"),"Charged seal damage"); }
        private static void PerfectCut() { var s=Fight(); Charge(s,.96f); Require(Read<string>(s,"LastAction")=="PerfectCut","Best charge window"); Near(68,Read<float>(s,"Seal"),"Perfect seal reward"); Near(15,Read<float>(s,"Resonance"),"Perfect resonance gain"); }
        private static void OverheldCharge() { var s=Fight(); Charge(s,1.3f); Require(Read<string>(s,"LastAction")=="WeakCut","Overholding loses best window"); }
        private static void Dodge() { var s=Fight(); s.Tick(2.2f); Require(Do(s,"Dodge"),"Wide dodge window"); Near(100,s.State.playerHp,"Avoid damage"); Near(100,Read<float>(s,"Seal"),"Dodge does not shatter seal"); Near(6,Read<float>(s,"Resonance"),"Good defense reward"); }
        private static void Deflect() { var s=Fight(); s.Tick(2.2f); Require(!Do(s,"Deflect"),"Deflect too early where dodge works"); var p=Fight(); Parry(p); Near(70,Read<float>(p,"Seal"),"High-skill seal reward"); Near(100,p.State.playerHp,"Deflect avoids hit"); }
        private static void DefenseCooldown() { var s=Fight(); s.Tick(2.2f); Require(!Do(s,"Deflect"),"Early attempt"); Require(!Do(s,"Dodge"),"Cannot hedge same strike with both defenses"); s.Tick(.41f); Require(s.State.playerHp<100,"Mistimed defense takes damage"); }
        private static void SealBreak() { var s=Fight(); Break(s); float hp=s.State.playerHp; s.Tick(1); Near(hp,s.State.playerHp,"Ritual pauses enemy pressure"); Require(!s.ClickAttack(false),"Cannot click through ritual"); }
        private static void RitualSuccess() { var s=Fight(); Break(s); Ritual(s); Near(108,s.State.enemyHp,"Ritual deals 40 percent max HP"); Near(100,Read<float>(s,"Seal"),"Fresh seal after payoff"); Require(Read<string>(s,"StageName")=="Fighting","Fight resumes"); Require(!Do(s,"TraceRitualPoint",2),"No duplicate payoff"); }
        private static void RitualReleaseFailure() { var s=Fight(); Break(s); Do(s,"BeginAttack"); Do(s,"TraceRitualPoint",0); Do(s,"ReleaseAttack"); Near(50,Read<float>(s,"Seal"),"Failed trace partially recovers seal"); Require(Read<string>(s,"LastAction")=="RitualFailed","Early release fails"); }
        private static void RitualOrderFailure() { var s=Fight(); Break(s); Require(!Do(s,"TraceRitualPoint",0),"Point requires held LMB"); Do(s,"BeginAttack"); Require(!Do(s,"TraceRitualPoint",2),"Cannot skip points"); Require(Read<string>(s,"LastAction")=="RitualFailed","Wrong order fails"); }
        private static void RitualTimeout() { var s=Fight(); Break(s); s.Tick(3.3f); Require(Read<string>(s,"StageName")=="Fighting","Timeout resumes pressure"); Near(50,Read<float>(s,"Seal"),"Timeout partial recovery"); }
        private static void Resonance() { var s=Fight(); Require(!Do(s,"ActivateResonance"),"Cannot activate empty gauge"); Break(s); Ritual(s); Near(100,Read<float>(s,"Resonance"),"Gauge capped at 100 from good play"); Require(Do(s,"ActivateResonance"),"Full gauge activates"); Require(!Do(s,"ActivateResonance"),"No repeat activation"); Near(0,Read<float>(s,"Resonance"),"Gauge consumed"); Charge(s,.875f); Require(Read<string>(s,"LastAction")=="PerfectCut","Slightly wider perfect window"); Near(52,Read<float>(s,"Seal"),"Enhanced seal damage"); }
        private static void ResonanceExpiry() { var s=Fight(); Break(s); Ritual(s); Do(s,"ActivateResonance"); s.Tick(5.6f); Require(!Read<bool>(s,"ResonanceActive"),"Resonance expires"); }
        private static void Phases() { var s=Fight(); Require(Read<int>(s,"EnemyPhase")==1,"Readable phase one"); Break(s); Ritual(s); Require(Read<int>(s,"EnemyPhase")==2,"Mid-health phase two"); Break(s); Ritual(s); Require(Read<int>(s,"EnemyPhase")==3,"Low-health phase three"); Require(s.State.enemyTimer < s.Contract.enemyInterval,"Final phase raises pressure"); }
        private static void CancelCharge() { var s=Fight(); Do(s,"BeginAttack"); s.Tick(.8f); Do(s,"CancelCombatInput"); Require(!Do(s,"ReleaseAttack"),"No buffered hit after pause/focus loss"); Near(180,s.State.enemyHp,"Cancelled hold deals no damage"); }
        private static void RetryReset() { var s=Fight(); Parry(s); s.Tick(40); Require(s.State.phase==RunPhase.Failed,"Idle hunter can fail"); s.ReturnHome(); s.AcceptContract(); s.LeaveHome(); s.EnterPortal(); Near(100,Read<float>(s,"Seal"),"New encounter resets seal"); Near(0,Read<float>(s,"Resonance"),"New encounter resets resonance"); }
        private static void InactiveGuards() { var s=new GameSession(new SaveData(),new ContractDefinition()); Require(!Do(s,"BeginAttack") && !Do(s,"Dodge") && !Do(s,"Deflect") && !Do(s,"ActivateResonance"),"Home rejects combat input"); }
        private static void CancelledCombo() { var s=Fight();s.ClickAttack(false);s.Tick(.48f);Do(s,"CancelCombatInput");s.ClickAttack(false);Require(Read<int>(s,"ComboStep")==1,"Interrupted combo must restart");Require(!Read<bool>(s,"LastPerfect"),"No stale perfect timing after interruption"); }
        private static void TickPartition() { var a=Fight(); var b=Fight(); a.Tick(5); for(int i=0;i<50;i++)b.Tick(.1f); Near(a.State.playerHp,b.State.playerHp,"Enemy timing partition independent"); Near(a.State.enemyTimer,b.State.enemyTimer,"Next attack partition independent"); }
    }
}
