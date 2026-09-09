using System;

namespace Rokas.Core
{
    [Serializable]
    public sealed class CombatHit
    {
        public float damage;
        public bool targetIsEnemy;
        public bool critical;
        public CombatHit(float damage, bool targetIsEnemy, bool critical)
        { this.damage = damage; this.targetIsEnemy = targetIsEnemy; this.critical = critical; }
    }

    public enum CombatStage { Fighting, SealBreak, Ritual }
    public enum CombatAction { None, Basic, PerfectCombo, Finisher, WeakCut, Charged, PerfectCut, Dodge, Deflect, MissedDefense, Damaged, SealBreak, RitualReady, RitualSuccess, RitualFailed, Resonance }

    public sealed class CombatService
    {
        private readonly SaveData state;
        private readonly ContractDefinition contract;
        private readonly EconomyService economy;
        private float comboAge;
        private float stageTimer;
        private int attackSequence;
        private CombatAction lastAction;

        public event Action<CombatHit> Hit;
        public event Action<CombatAction> ActionResolved;
        public CombatStage Stage { get; private set; }
        public string StageName { get { return Stage.ToString(); } }
        public string LastAction { get { return lastAction.ToString(); } }
        public int ComboStep { get; private set; }
        public bool LastPerfect { get; private set; }
        public float ComboAge { get { return comboAge; } }
        public float Seal { get; private set; }
        public float Resonance { get; private set; }
        public float ResonanceRemaining { get; private set; }
        public bool ResonanceActive { get { return ResonanceRemaining > CombatTuning.Epsilon; } }
        public bool IsHolding { get; private set; }
        public float ChargeSeconds { get; private set; }
        public int RitualPoint { get; private set; }
        public float RitualRemaining { get { return Stage == CombatStage.Ritual ? stageTimer : 0; } }
        public float DefenseCooldownRemaining { get; private set; }
        // Retained for callers of the original API; the timed weak-point button is retired.
        public bool WeakPointActive { get { return false; } }
        public bool EnemyTelegraph { get { return Fighting && state.enemyTimer <= CombatTuning.TelegraphDuration; } }
        public int EnemyPhase
        {
            get
            {
                float ratio = contract.enemyHealth > 0 ? state.enemyHp / contract.enemyHealth : 0;
                return ratio <= CombatTuning.PhaseThreeHealth ? 3 : ratio <= CombatTuning.PhaseTwoHealth ? 2 : 1;
            }
        }
        public bool DelayedAttack { get { return EnemyPhase == 2 && attackSequence % 2 != 0; } }
        private bool Fighting { get { return state.phase == RunPhase.Combat && Stage == CombatStage.Fighting; } }
        private float PerfectBonus { get { return ResonanceActive ? CombatTuning.ResonanceWindowBonus : 0; } }

        public CombatService(SaveData state, ContractDefinition contract, EconomyService economy)
        {
            this.state = state ?? throw new ArgumentNullException("state");
            this.contract = contract ?? throw new ArgumentNullException("contract");
            this.economy = economy ?? throw new ArgumentNullException("economy");
            ResetEncounter();
        }

        internal void ResetEncounter()
        {
            Stage = CombatStage.Fighting;
            Seal = CombatTuning.MaxSeal;
            Resonance = ResonanceRemaining = DefenseCooldownRemaining = stageTimer = 0;
            ComboStep = RitualPoint = attackSequence = 0;
            comboAge = CombatTuning.ComboExpiry + 1;
            LastPerfect = false;
            lastAction = CombatAction.None;
            IsHolding = false;
            ChargeSeconds = 0;
        }

        internal bool BeginAttack()
        {
            if (state.phase != RunPhase.Combat || IsHolding || Stage == CombatStage.SealBreak) return false;
            if (Stage == CombatStage.Fighting && state.clickTimer > CombatTuning.Epsilon) return false;
            IsHolding = true;
            ChargeSeconds = 0;
            return true;
        }

        internal bool ReleaseAttack()
        {
            if (!IsHolding || state.phase != RunPhase.Combat) return false;
            IsHolding = false;
            if (Stage == CombatStage.Ritual) { FailRitual(); return true; }
            if (!Fighting) return false;
            float held = ChargeSeconds;
            ChargeSeconds = 0;
            if (held < CombatTuning.HoldThreshold) return ClickAttack(false);
            bool perfect = held >= CombatTuning.PerfectCutStart - PerfectBonus && held <= CombatTuning.PerfectCutEnd + PerfectBonus;
            bool charged = held >= CombatTuning.ChargeStart && held <= CombatTuning.ChargeEnd;
            float multiplier = perfect ? CombatTuning.PerfectCutMultiplier : charged ? CombatTuning.ChargedMultiplier : CombatTuning.WeakCutMultiplier;
            float sealDamage = perfect ? CombatTuning.PerfectCutSealDamage : charged ? CombatTuning.ChargedSealDamage : CombatTuning.WeakCutSealDamage;
            ComboStep = 0;
            LastPerfect = perfect;
            SetRecovery();
            if (perfect) GainResonance(CombatTuning.PerfectCutResonance);
            Resolve(perfect ? CombatAction.PerfectCut : charged ? CombatAction.Charged : CombatAction.WeakCut);
            Strike(multiplier, sealDamage, perfect);
            return true;
        }

        internal bool ClickAttack(bool weakPoint)
        {
            if (!Fighting || IsHolding || state.clickTimer > CombatTuning.Epsilon) return false;
            bool continuation = ComboStep > 0 && ComboStep < 3 && comboAge >= CombatTuning.ComboEarliest && comboAge <= CombatTuning.ComboExpiry;
            ComboStep = continuation ? ComboStep + 1 : 1;
            LastPerfect = continuation && Math.Abs(comboAge - CombatTuning.ComboBeat) <= CombatTuning.ComboPerfectHalfWindow + PerfectBonus;
            comboAge = 0;
            SetRecovery();
            float multiplier = ComboStep == 3 ? CombatTuning.FinisherMultiplier : 1;
            if (LastPerfect) { multiplier *= CombatTuning.PerfectMultiplier; GainResonance(CombatTuning.PerfectComboResonance); }
            Resolve(ComboStep == 3 ? CombatAction.Finisher : LastPerfect ? CombatAction.PerfectCombo : CombatAction.Basic);
            Strike(multiplier, ComboStep == 3 ? CombatTuning.FinisherSealDamage : CombatTuning.BasicSealDamage, LastPerfect || ComboStep == 3);
            return true;
        }

        internal bool Dodge() { return Defend(false); }
        internal bool Deflect() { return Defend(true); }
        private bool Defend(bool deflect)
        {
            if (!Fighting || DefenseCooldownRemaining > CombatTuning.Epsilon) return false;
            CancelCombatInput();
            DefenseCooldownRemaining = CombatTuning.DefenseCooldown;
            float window = deflect ? CombatTuning.DeflectWindow : CombatTuning.DodgeWindow;
            if (state.enemyTimer > window + CombatTuning.Epsilon || state.enemyTimer <= CombatTuning.Epsilon)
            {
                Resolve(CombatAction.MissedDefense);
                return false;
            }
            ScheduleAttack();
            GainResonance(deflect ? CombatTuning.DeflectResonance : CombatTuning.DodgeResonance);
            Resolve(deflect ? CombatAction.Deflect : CombatAction.Dodge);
            if (deflect) DamageSeal(CombatTuning.DeflectSealDamage);
            return true;
        }

        internal bool TraceRitualPoint(int index)
        {
            if (state.phase != RunPhase.Combat || Stage != CombatStage.Ritual || !IsHolding) return false;
            if (index == RitualPoint - 1) return false; // Pointer may remain inside the last point for several frames.
            if (index != RitualPoint) { FailRitual(); return false; }
            RitualPoint++;
            if (RitualPoint == 3)
            {
                IsHolding = false;
                Stage = CombatStage.Fighting;
                stageTimer = 0;
                Seal = CombatTuning.MaxSeal;
                GainResonance(CombatTuning.RitualResonance);
                Resolve(CombatAction.RitualSuccess);
                DamageEnemy(contract.enemyHealth * CombatTuning.RitualDamageFraction, true);
                ScheduleAttack();
            }
            return true;
        }

        internal bool ActivateResonance()
        {
            if (!Fighting || ResonanceActive || Resonance < CombatTuning.MaxResonance) return false;
            Resonance = 0;
            ResonanceRemaining = CombatTuning.ResonanceDuration;
            Resolve(CombatAction.Resonance);
            return true;
        }

        internal bool CancelCombatInput()
        {
            bool wasHolding = IsHolding;
            bool changed = wasHolding || ComboStep != 0;
            IsHolding = false;
            ChargeSeconds = 0;
            ComboStep = 0;
            comboAge = CombatTuning.ComboExpiry + 1;
            LastPerfect = false;
            if (wasHolding && Stage == CombatStage.Ritual) FailRitual();
            return changed;
        }

        internal bool Tick(float seconds)
        {
            if (state.phase != RunPhase.Combat || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return false;
            float remaining = seconds;
            while (remaining > CombatTuning.Epsilon && state.phase == RunPhase.Combat)
            {
                float due = Stage == CombatStage.Fighting ? state.enemyTimer : stageTimer;
                float step = Math.Min(remaining, Math.Max(0, due));
                AdvanceTimers(step);
                remaining -= step;
                if (Stage == CombatStage.Fighting && state.enemyTimer <= CombatTuning.Epsilon)
                {
                    float damage = contract.enemyDamage * (EnemyPhase == 3 ? CombatTuning.PhaseThreeDamage : EnemyPhase == 2 ? CombatTuning.PhaseTwoDamage : 1);
                    DamagePlayer(damage);
                    ScheduleAttack();
                }
                else if (Stage != CombatStage.Fighting && stageTimer <= CombatTuning.Epsilon)
                {
                    if (Stage == CombatStage.SealBreak)
                    {
                        Stage = CombatStage.Ritual;
                        stageTimer = CombatTuning.RitualDuration;
                        RitualPoint = 0;
                        Resolve(CombatAction.RitualReady);
                    }
                    else FailRitual();
                }
            }
            return true;
        }

        private void AdvanceTimers(float dt)
        {
            state.combatTime += dt;
            state.clickTimer = Math.Max(0, state.clickTimer - dt);
            DefenseCooldownRemaining = Math.Max(0, DefenseCooldownRemaining - dt);
            ResonanceRemaining = Math.Max(0, ResonanceRemaining - dt);
            comboAge += dt;
            if (IsHolding && Stage == CombatStage.Fighting) ChargeSeconds += dt;
            if (Stage == CombatStage.Fighting) state.enemyTimer = Math.Max(0, state.enemyTimer - dt);
            else stageTimer = Math.Max(0, stageTimer - dt);
            // autoTimer is retained in the save schema, but there is no automatic damage.
        }

        private void SetRecovery() { state.clickTimer = ResonanceActive ? CombatTuning.ResonantCooldown : CombatTuning.AttackCooldown; }
        private void ScheduleAttack()
        {
            attackSequence++;
            float tempo = EnemyPhase == 3 ? (attackSequence % 2 == 0 ? CombatTuning.PhaseThreeQuickTempo : CombatTuning.PhaseThreeHeavyTempo)
                : EnemyPhase == 2 ? (attackSequence % 2 == 0 ? CombatTuning.PhaseTwoQuickTempo : CombatTuning.PhaseTwoDelayedTempo) : 1;
            state.enemyTimer = Math.Max(CombatTuning.MinimumEnemyInterval, contract.enemyInterval * tempo);
        }
        private void Strike(float multiplier, float sealDamage, bool critical)
        {
            DamageEnemy(contract.clickDamage * economy.GetWeaponDamageMultiplier(state) * multiplier, critical);
            if (state.phase == RunPhase.Combat) DamageSeal(sealDamage);
        }
        private void DamageSeal(float amount)
        {
            Seal = Math.Max(0, Seal - amount * (ResonanceActive ? CombatTuning.ResonanceSealMultiplier : 1));
            if (Seal > CombatTuning.Epsilon) return;
            Seal = 0;
            Stage = CombatStage.SealBreak;
            stageTimer = CombatTuning.SealBreakPause;
            ComboStep = 0;
            IsHolding = false;
            ChargeSeconds = 0;
            Resolve(CombatAction.SealBreak);
        }
        private void FailRitual()
        {
            IsHolding = false;
            ChargeSeconds = 0;
            Stage = CombatStage.Fighting;
            stageTimer = 0;
            Seal = CombatTuning.MaxSeal * CombatTuning.FailedSealRecovery;
            Resonance = Math.Max(0, Resonance - CombatTuning.MistakeResonanceLoss);
            state.enemyTimer = CombatTuning.CounterDelay;
            Resolve(CombatAction.RitualFailed);
        }
        private void GainResonance(float amount)
        {
            if (!ResonanceActive) Resonance = Math.Min(CombatTuning.MaxResonance, Resonance + amount);
        }
        private void DamageEnemy(float damage, bool critical)
        {
            float applied = Math.Max(0, Math.Min(state.enemyHp, damage));
            state.enemyHp = Math.Max(0, state.enemyHp - applied);
            if (state.enemyHp <= CombatTuning.Epsilon) { state.enemyHp = 0; state.phase = RunPhase.Sealed; IsHolding = false; }
            Hit?.Invoke(new CombatHit(applied, true, critical));
        }
        private void DamagePlayer(float damage)
        {
            float applied = Math.Max(0, Math.Min(state.playerHp, damage));
            state.playerHp = Math.Max(0, state.playerHp - applied);
            IsHolding = false;
            ChargeSeconds = 0;
            ComboStep = 0;
            Resonance = Math.Max(0, Resonance - CombatTuning.MistakeResonanceLoss);
            if (state.playerHp <= CombatTuning.Epsilon) { state.playerHp = 0; state.phase = RunPhase.Failed; }
            Resolve(CombatAction.Damaged);
            Hit?.Invoke(new CombatHit(applied, false, false));
        }
        private void Resolve(CombatAction action) { lastAction = action; ActionResolved?.Invoke(action); }
    }
}
