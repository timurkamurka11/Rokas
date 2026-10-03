using System;
using Rokas.Core.ReactiveTurns;

namespace Rokas.Core
{
    public static class ReactiveEightEnemyDefinitions
    {
        public const string HunterId = "P";
        public const string SweepId = "sweep";
        public const string HeavyId = "heavy";
        public const string AnchorId = "anchor";
        public const string ThrowId = "throw_blade";

        public static CombatDefinitions Create(ContractDefinition contract, SaveData profile)
        {
            if (contract == null) throw new ArgumentNullException("contract");
            if (profile == null) throw new ArgumentNullException("profile");
            if (string.IsNullOrWhiteSpace(contract.id))
                throw new ArgumentException("Contract ID is required.", "contract");
            double attack = DamageResolver.AttackFromLegacyClickDamage(contract.clickDamage,
                profile.weaponLevel);
            return Build(contract.id + ":reactive-eight-v1", attack, true);
        }

        // Existing encounters keep the exact catalog they started with until a new entry.
        public static CombatDefinitions CreatePreviousCatalog(ContractDefinition contract, SaveData profile)
        {
            if (contract == null) throw new ArgumentNullException("contract");
            if (profile == null) throw new ArgumentNullException("profile");
            if (string.IsNullOrWhiteSpace(contract.id))
                throw new ArgumentException("Contract ID is required.", "contract");
            double attack = DamageResolver.AttackFromLegacyClickDamage(contract.clickDamage,
                profile.weaponLevel);
            return Build(contract.id + ":reactive-eight-v1", attack, false);
        }

        public static CombatDefinitions CreateForTests()
        {
            return Build("reactive-eight-test-v1", 20, true);
        }

        private static CombatDefinitions Build(string id, double hunterAttack, bool includeThrow)
        {
            const DefenseResponseMask both = DefenseResponseMask.Dodge | DefenseResponseMask.Parry;
            var sequences = new[] {
                new AttackSequenceDefinition("single", 1600000,
                    new[] { new HitDefinition("h1", 1000000, 8, both) }),
                new AttackSequenceDefinition("triple", 3850000, new[] {
                    new HitDefinition("h1", 1000000, 8, both),
                    new HitDefinition("h2", 1650000, 8, both),
                    new HitDefinition("h3", 2550000, 8, both)
                }, interruptibleOnBreak: true),
                new AttackSequenceDefinition("heavy", 2200000,
                    new[] { new HitDefinition("h1", 1450000, 12, both, isHeavy: true) }),
                new AttackSequenceDefinition("final_triple", 4100000, new[] {
                    new HitDefinition("h1", 1000000, 8, both),
                    new HitDefinition("h2", 1650000, 8, both),
                    new HitDefinition("h3", 2800000, 8, both)
                }, interruptibleOnBreak: true)
            };
            var actors = new[] {
                new ActorDefinition(HunterId, true, 0, 100, 0, 100, 0, "single"),
                new ActorDefinition("E1", false, 1, 100, 60, 60, 60, "single"),
                new ActorDefinition("E2", false, 2, 100, 90, 60, 60, "triple", 0,
                    new[] { "triple", "single" }),
                new ActorDefinition("E3", false, 3, 100, 120, 60, 60, "single"),
                new ActorDefinition("E4", false, 4, 100, 60, 60, 60, "single"),
                new ActorDefinition("E5", false, 5, 100, 90, 60, 60, "single"),
                new ActorDefinition("E6", false, 6, 100, 120, 120, 100, "heavy", 0,
                    new[] { "heavy", "triple" }),
                new ActorDefinition("E7", false, 7, 100, 60, 120, 100, "final_triple", 0,
                    new[] { "final_triple", "heavy" }),
                new ActorDefinition("E8", false, 8, 100, 90, 120, 100, "final_triple", 0,
                    new[] { "final_triple", "heavy" })
            };
            var previousSkills = new[] {
                new SkillDefinition("seal_strike", 3, 115, 2.4, 35),
                new SkillDefinition(SweepId, 3, 130, 1.2, 12,
                    TargetingMode.AllActiveEnemies),
                new SkillDefinition(HeavyId, 5, 140, 3.4, 50,
                    offenseTimingBonus: 1.15, offenseTimingEarlyUs: 100000,
                    offenseTimingLateUs: 50000),
                new SkillDefinition(AnchorId, 2, 100, .8, 15,
                    delayTargetTicks: 35),
                new SkillDefinition("defend", 0, 80, 0, 0)
            };
            SkillDefinition[] skills = previousSkills;
            if (includeThrow)
            {
                skills = new SkillDefinition[previousSkills.Length + 1];
                Array.Copy(previousSkills, skills, previousSkills.Length);
                skills[previousSkills.Length] = new SkillDefinition(ThrowId, 2, 110, 1.0, 0);
            }
            var waves = new[] {
                new WaveDefinition("wave-1", new[] { "E1", "E2", "E3" }),
                new WaveDefinition("wave-2", new[] { "E4", "E5", "E6" }),
                new WaveDefinition("wave-3", new[] { "E7", "E8" })
            };
            var definitions = new CombatDefinitions(id, actors, sequences, skills,
                DefenseWindowProfile.Standard, 3, waves, hunterAttack);
            ValidationResult result = definitions.Validate();
            if (!result.IsValid)
                throw new InvalidOperationException("Invalid Reactive eight-enemy content: " +
                    string.Join("|", result.Errors));
            return definitions;
        }
    }
}
