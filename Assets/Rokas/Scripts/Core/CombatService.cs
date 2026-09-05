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
        {
            this.damage = damage;
            this.targetIsEnemy = targetIsEnemy;
            this.critical = critical;
        }
    }

    public sealed class CombatService
    {
        private const float ClickCooldown = .15f;
        private const float WeakPointStart = 2f;
        private const float WeakPointEnd = 3.5f;
        private const float TimerEpsilon = .00001f;

        private readonly SaveData state;
        private readonly ContractDefinition contract;
        private readonly EconomyService economy;

        public event Action<CombatHit> Hit;

        public bool WeakPointActive
        {
            get
            {
                return state.phase == RunPhase.Combat &&
                       !state.weakPointClaimed &&
                       state.combatTime >= WeakPointStart &&
                       state.combatTime < WeakPointEnd;
            }
        }

        public CombatService(SaveData state, ContractDefinition contract, EconomyService economy)
        {
            if (state == null)
            {
                throw new ArgumentNullException("state");
            }

            if (contract == null)
            {
                throw new ArgumentNullException("contract");
            }

            if (economy == null)
            {
                throw new ArgumentNullException("economy");
            }

            this.state = state;
            this.contract = contract;
            this.economy = economy;
        }

        internal bool ClickAttack(bool weakPoint)
        {
            if (state.phase != RunPhase.Combat || state.clickTimer > TimerEpsilon)
            {
                return false;
            }

            bool critical = weakPoint && WeakPointActive;
            float damage = contract.clickDamage * economy.GetWeaponDamageMultiplier(state);
            if (critical)
            {
                damage *= 2f;
                state.weakPointClaimed = true;
            }

            state.clickTimer = ClickCooldown;
            DamageEnemy(damage, critical);
            return true;
        }

        internal bool Tick(float seconds)
        {
            if (state.phase != RunPhase.Combat || seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds))
            {
                return false;
            }

            float remaining = seconds;
            while (remaining > TimerEpsilon && state.phase == RunPhase.Combat)
            {
                float step = remaining;
                if (state.autoTimer < step)
                {
                    step = Math.Max(0f, state.autoTimer);
                }
                if (state.enemyTimer < step)
                {
                    step = Math.Max(0f, state.enemyTimer);
                }

                AdvanceTimers(step);
                remaining -= step;

                bool autoDue = state.autoTimer <= TimerEpsilon;
                bool enemyDue = state.enemyTimer <= TimerEpsilon;
                if (!autoDue && !enemyDue)
                {
                    continue;
                }

                if (autoDue)
                {
                    state.autoTimer += EffectiveAutoInterval();
                    DamageEnemy(contract.autoDamage * economy.GetWeaponDamageMultiplier(state), false);
                }

                if (enemyDue && state.phase == RunPhase.Combat)
                {
                    state.enemyTimer += SafeInterval(contract.enemyInterval);
                    DamagePlayer(contract.enemyDamage);
                }
            }

            return true;
        }

        private void AdvanceTimers(float seconds)
        {
            state.autoTimer -= seconds;
            state.enemyTimer -= seconds;
            state.clickTimer = Math.Max(0f, state.clickTimer - seconds);
            state.combatTime += seconds;
        }

        private float EffectiveAutoInterval()
        {
            float interval = contract.autoInterval;
            if (state.preparedFoodId == FoodService.GreenTeaId)
            {
                interval /= 1.05f;
            }
            return SafeInterval(interval);
        }

        private static float SafeInterval(float interval)
        {
            return interval > TimerEpsilon && !float.IsNaN(interval) && !float.IsInfinity(interval)
                ? interval
                : TimerEpsilon;
        }

        private void DamageEnemy(float damage, bool critical)
        {
            float applied = Math.Max(0f, Math.Min(state.enemyHp, damage));
            state.enemyHp = Math.Max(0f, state.enemyHp - applied);
            RaiseHit(new CombatHit(applied, true, critical));
            if (state.enemyHp <= TimerEpsilon)
            {
                state.enemyHp = 0f;
                state.phase = RunPhase.Sealed;
            }
        }

        private void DamagePlayer(float damage)
        {
            float applied = Math.Max(0f, Math.Min(state.playerHp, damage));
            state.playerHp = Math.Max(0f, state.playerHp - applied);
            RaiseHit(new CombatHit(applied, false, false));
            if (state.playerHp <= TimerEpsilon)
            {
                state.playerHp = 0f;
                state.phase = RunPhase.Failed;
            }
        }

        private void RaiseHit(CombatHit hit)
        {
            Action<CombatHit> handler = Hit;
            if (handler != null)
            {
                handler(hit);
            }
        }
    }
}
