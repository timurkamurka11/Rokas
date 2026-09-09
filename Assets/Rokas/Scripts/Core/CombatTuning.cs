namespace Rokas.Core
{
    // First-slice tuning lives here; contract health/damage/tempo remain encounter data.
    public static class CombatTuning
    {
        public const float Epsilon = .00001f;
        public const float AttackCooldown = .28f;
        public const float ResonantCooldown = .22f;
        public const float ComboEarliest = .32f;
        public const float ComboBeat = .48f;
        public const float ComboPerfectHalfWindow = .08f;
        public const float ComboExpiry = .85f;
        public const float PerfectMultiplier = 1.35f;
        public const float FinisherMultiplier = 2.4f;
        public const float HoldThreshold = .22f;
        public const float ChargeStart = .65f;
        public const float ChargeEnd = 1.2f;
        public const float PerfectCutStart = .90f;
        public const float PerfectCutEnd = 1.04f;
        public const float WeakCutMultiplier = .65f;
        public const float ChargedMultiplier = 1.8f;
        public const float PerfectCutMultiplier = 3f;
        public const float DodgeWindow = .42f;
        public const float DeflectWindow = .14f;
        public const float DefenseCooldown = .65f;
        public const float MaxSeal = 100f;
        public const float BasicSealDamage = 2f;
        public const float FinisherSealDamage = 12f;
        public const float WeakCutSealDamage = 3f;
        public const float ChargedSealDamage = 22f;
        public const float PerfectCutSealDamage = 32f;
        public const float DeflectSealDamage = 30f;
        public const float SealBreakPause = .35f;
        public const float RitualDuration = 3.2f;
        public const float RitualDamageFraction = .40f;
        public const float FailedSealRecovery = .5f;
        public const float CounterDelay = .75f;
        public const float MaxResonance = 100f;
        public const float PerfectComboResonance = 8f;
        public const float PerfectCutResonance = 15f;
        public const float DeflectResonance = 20f;
        public const float DodgeResonance = 6f;
        public const float RitualResonance = 30f;
        public const float MistakeResonanceLoss = 15f;
        public const float ResonanceDuration = 5.5f;
        public const float ResonanceSealMultiplier = 1.5f;
        public const float ResonanceWindowBonus = .035f;
        public const float PhaseTwoHealth = 2f / 3f;
        public const float PhaseThreeHealth = 1f / 3f;
        public const float PhaseTwoQuickTempo = .8f;
        public const float PhaseTwoDelayedTempo = 1.15f;
        public const float PhaseThreeQuickTempo = .5f;
        public const float PhaseThreeHeavyTempo = .75f;
        public const float PhaseTwoDamage = 1.2f;
        public const float PhaseThreeDamage = 1.5f;
        public const float TelegraphDuration = .9f;
        public const float MinimumEnemyInterval = .05f;
    }
}
