using System;
using Rokas.Core.ReactiveTurns;

namespace Rokas.Core
{
    public static class ReactiveDuelDefinitions
    {
        public const string HunterId = "P"; // Keiko is the sole player-controlled Hunter.
        public const string SingleId = "reactive_single";
        public const string TripleId = "reactive_triple";

        public static CombatDefinitions Create(ContractDefinition contract, SaveData profile)
        {
            if (contract == null) throw new ArgumentNullException("contract");
            if (profile == null) throw new ArgumentNullException("profile");
            if (string.IsNullOrEmpty(contract.id) || string.IsNullOrEmpty(contract.enemyId))
                throw new ArgumentException("Reactive duel needs contract and enemy IDs.", "contract");

            int enemyHp = Math.Max(1, (int)Math.Floor(contract.enemyHealth + .5f));
            int enemyDamage = Math.Max(0, (int)Math.Floor(contract.enemyDamage + .5f));
            var single = new AttackSequenceDefinition(SingleId, 1600000,
                new[] { new HitDefinition("single-h1", 1000000, enemyDamage,
                    DefenseResponseMask.Dodge | DefenseResponseMask.Parry) });
            var triple = new AttackSequenceDefinition(TripleId, 3850000,
                new[] {
                    new HitDefinition("triple-h1", 1000000, enemyDamage, DefenseResponseMask.Dodge | DefenseResponseMask.Parry),
                    new HitDefinition("triple-h2", 1650000, enemyDamage, DefenseResponseMask.Dodge | DefenseResponseMask.Parry),
                    new HitDefinition("triple-h3", 2550000, enemyDamage, DefenseResponseMask.Dodge | DefenseResponseMask.Parry)
                }, interruptibleOnBreak: true);
            var definitions = new CombatDefinitions(contract.id + ":reactive-duel-v1",
                new[] {
                    new ActorDefinition(HunterId, true, 0, 100, 0, 100, 0, SingleId),
                    new ActorDefinition(contract.enemyId, false, 1, 100, 60, enemyHp, 60, TripleId,
                        null, new[] { TripleId, SingleId })
                },
                new[] { single, triple },
                new[] {
                    new SkillDefinition("seal_strike", 3, 115, 2.4, 35),
                    new SkillDefinition("defend", 0, 80, 0, 0)
                }, DefenseWindowProfile.Standard, 1, hunterAttack:
                    DamageResolver.AttackFromLegacyClickDamage(contract.clickDamage, profile.weaponLevel));
            ValidationResult validation = definitions.Validate();
            if (!validation.IsValid)
                throw new InvalidOperationException("Reactive duel definitions are invalid: " + string.Join(", ", validation.Errors));
            return definitions;
        }
    }
}
