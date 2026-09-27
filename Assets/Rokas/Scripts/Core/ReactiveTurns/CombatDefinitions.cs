using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Rokas.Core.ReactiveTurns
{
    public enum CommandKind { Basic, Skill, Defend, Retreat }

    [Flags]
    public enum DefenseResponseMask { None = 0, Dodge = 1, Parry = 2 }

    public sealed class DefenseWindowProfile
    {
        public static readonly DefenseWindowProfile Standard = new DefenseWindowProfile(400000, 60000, 240000, 60000, 110000, 45000, 40000, 25000);

        public long AcquireEarlyUs { get; private set; }
        public long AcquireLateUs { get; private set; }
        public long DodgeEarlyUs { get; private set; }
        public long DodgeLateUs { get; private set; }
        public long ParryEarlyUs { get; private set; }
        public long ParryLateUs { get; private set; }
        public long PerfectEarlyUs { get; private set; }
        public long PerfectLateUs { get; private set; }

        public DefenseWindowProfile(long acquireEarlyUs, long acquireLateUs, long dodgeEarlyUs, long dodgeLateUs,
            long parryEarlyUs, long parryLateUs, long perfectEarlyUs, long perfectLateUs)
        {
            AcquireEarlyUs = acquireEarlyUs;
            AcquireLateUs = acquireLateUs;
            DodgeEarlyUs = dodgeEarlyUs;
            DodgeLateUs = dodgeLateUs;
            ParryEarlyUs = parryEarlyUs;
            ParryLateUs = parryLateUs;
            PerfectEarlyUs = perfectEarlyUs;
            PerfectLateUs = perfectLateUs;
        }

        internal void Validate(List<string> errors)
        {
            if (AcquireEarlyUs < 0 || AcquireLateUs < 0) errors.Add("window/acquisition");
            if (DodgeEarlyUs < 0 || DodgeEarlyUs > AcquireEarlyUs) errors.Add("window/dodgeEarlyUs");
            if (DodgeLateUs < 0 || DodgeLateUs > AcquireLateUs) errors.Add("window/dodgeLateUs");
            if (ParryEarlyUs < 0 || ParryEarlyUs > AcquireEarlyUs) errors.Add("window/parryEarlyUs");
            if (ParryLateUs < 0 || ParryLateUs > AcquireLateUs) errors.Add("window/parryLateUs");
            if (PerfectEarlyUs < 0 || PerfectEarlyUs > ParryEarlyUs) errors.Add("window/perfectEarlyUs");
            if (PerfectLateUs < 0 || PerfectLateUs > ParryLateUs) errors.Add("window/perfectLateUs");
        }
    }

    public sealed class ActorDefinition
    {
        public string Id { get; private set; }
        public bool IsHunter { get; private set; }
        public int SpawnOrdinal { get; private set; }
        public int Speed { get; private set; }
        public long InitialTick { get; private set; }
        public int MaxHp { get; private set; }
        public int MaxSeal { get; private set; }
        public string AttackSequenceId { get; private set; }
        public ReadOnlyCollection<string> AttackSequenceIds { get; private set; }
        public int InitialAp { get; private set; }

        public ActorDefinition(string id, bool isHunter, int spawnOrdinal, int speed, long initialTick,
            int maxHp, int maxSeal, string attackSequenceId, int? initialAp = null,
            string[] attackSequenceIds = null)
        {
            Id = id;
            IsHunter = isHunter;
            SpawnOrdinal = spawnOrdinal;
            Speed = speed;
            InitialTick = initialTick;
            MaxHp = maxHp;
            MaxSeal = maxSeal;
            AttackSequenceId = attackSequenceId;
            AttackSequenceIds = Array.AsReadOnly((string[])(attackSequenceIds ??
                (attackSequenceId == null ? new string[0] : new[] { attackSequenceId })).Clone());
            InitialAp = initialAp ?? (isHunter ? 3 : 0);
        }
    }

    public sealed class HitDefinition
    {
        public string Id { get; private set; }
        public long ImpactUs { get; private set; }
        public int RawDamage { get; private set; }
        public DefenseResponseMask AllowedResponses { get; private set; }

        public HitDefinition(string id, long impactUs, int rawDamage, DefenseResponseMask allowedResponses)
        {
            Id = id;
            ImpactUs = impactUs;
            RawDamage = rawDamage;
            AllowedResponses = allowedResponses;
        }
    }

    public sealed class AttackSequenceDefinition
    {
        public string Id { get; private set; }
        public long DurationUs { get; private set; }
        public ReadOnlyCollection<HitDefinition> Hits { get; private set; }
        public int DelayTicks { get; private set; }
        public bool InterruptibleOnBreak { get; private set; }

        public AttackSequenceDefinition(string id, long durationUs, HitDefinition[] hits,
            int delayTicks = 100, bool interruptibleOnBreak = false)
        {
            Id = id;
            DurationUs = durationUs;
            Hits = Array.AsReadOnly((HitDefinition[])(hits ?? new HitDefinition[0]).Clone());
            DelayTicks = delayTicks;
            InterruptibleOnBreak = interruptibleOnBreak;
        }
    }

    public sealed class SkillDefinition
    {
        public string Id { get; private set; }
        public int ApCost { get; private set; }
        public int DelayTicks { get; private set; }
        public double DamagePower { get; private set; }
        public int SealDamage { get; private set; }

        public SkillDefinition(string id, int apCost, int delayTicks, double damagePower, int sealDamage)
        {
            Id = id;
            ApCost = apCost;
            DelayTicks = delayTicks;
            DamagePower = damagePower;
            SealDamage = sealDamage;
        }
    }

    public sealed class WaveDefinition
    {
        public string Id { get; private set; }
        public ReadOnlyCollection<string> EnemyActorIds { get; private set; }

        public WaveDefinition(string id, string[] enemyActorIds)
        {
            Id = id;
            EnemyActorIds = Array.AsReadOnly((string[])(enemyActorIds ?? new string[0]).Clone());
        }
    }

    public sealed class ValidationResult
    {
        public ReadOnlyCollection<string> Errors { get; private set; }
        public ReadOnlyCollection<string> Warnings { get; private set; }
        public bool IsValid { get { return Errors.Count == 0; } }

        internal ValidationResult(List<string> errors, List<string> warnings)
        {
            Errors = errors.AsReadOnly();
            Warnings = warnings.AsReadOnly();
        }
    }

    public sealed class CombatDefinitions
    {
        public string Id { get; private set; }
        public ReadOnlyCollection<ActorDefinition> Actors { get; private set; }
        public ReadOnlyCollection<AttackSequenceDefinition> Sequences { get; private set; }
        public ReadOnlyCollection<SkillDefinition> Skills { get; private set; }
        public ReadOnlyCollection<WaveDefinition> Waves { get; private set; }
        public DefenseWindowProfile Window { get; private set; }
        public int MaxActiveEnemies { get; private set; }
        public double HunterAttack { get; private set; }
        public string ContentHash { get { return ComputeContentHash(); } }

        public CombatDefinitions(string id, ActorDefinition[] actors, AttackSequenceDefinition[] sequences,
            SkillDefinition[] skills, DefenseWindowProfile window, int maxActiveEnemies = 4,
            WaveDefinition[] waves = null, double hunterAttack = 20)
        {
            Id = id;
            Actors = Array.AsReadOnly((ActorDefinition[])(actors ?? new ActorDefinition[0]).Clone());
            Sequences = Array.AsReadOnly((AttackSequenceDefinition[])(sequences ?? new AttackSequenceDefinition[0]).Clone());
            Skills = Array.AsReadOnly((SkillDefinition[])(skills ?? new SkillDefinition[0]).Clone());
            Waves = Array.AsReadOnly((WaveDefinition[])(waves ?? new WaveDefinition[0]).Clone());
            Window = window;
            MaxActiveEnemies = maxActiveEnemies;
            HunterAttack = hunterAttack;
        }

        public ActorDefinition FindActor(string id)
        {
            foreach (ActorDefinition actor in Actors) if (actor != null && actor.Id == id) return actor;
            return null;
        }

        public AttackSequenceDefinition FindSequence(string id)
        {
            foreach (AttackSequenceDefinition sequence in Sequences) if (sequence != null && sequence.Id == id) return sequence;
            return null;
        }

        public SkillDefinition FindSkill(string id)
        {
            foreach (SkillDefinition skill in Skills) if (skill != null && skill.Id == id) return skill;
            return null;
        }

        public ValidationResult Validate()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            if (string.IsNullOrWhiteSpace(Id)) errors.Add("encounter/id");
            if (MaxActiveEnemies < 1 || MaxActiveEnemies > 4) errors.Add("encounter/maxActiveEnemies");
            if (double.IsNaN(HunterAttack) || double.IsInfinity(HunterAttack) || HunterAttack < 0)
                errors.Add("encounter/hunterAttack");
            if (Window == null) errors.Add("window/missing");
            else Window.Validate(errors);

            var actorIds = new HashSet<string>(StringComparer.Ordinal);
            var sequenceIds = new HashSet<string>(StringComparer.Ordinal);
            var skillIds = new HashSet<string>(StringComparer.Ordinal);
            int hunterCount = 0;
            foreach (AttackSequenceDefinition sequence in Sequences)
            {
                if (sequence == null) { errors.Add("sequences/null"); continue; }
                string path = "sequences/" + sequence.Id;
                if (string.IsNullOrWhiteSpace(sequence.Id) || !sequenceIds.Add(sequence.Id)) errors.Add(path + "/id");
                if (sequence.DurationUs <= 0 || sequence.DurationUs > 8000000) errors.Add(path + "/durationUs");
                if (sequence.DelayTicks <= 0) errors.Add(path + "/delayTicks");
                if (sequence.Hits.Count == 0 || sequence.Hits.Count > 6) errors.Add(path + "/hits");
                var hitIds = new HashSet<string>(StringComparer.Ordinal);
                long previous = -1;
                foreach (HitDefinition hit in sequence.Hits)
                {
                    if (hit == null) { errors.Add(path + "/hits/null"); continue; }
                    string hitPath = path + "/hits/" + hit.Id;
                    if (string.IsNullOrWhiteSpace(hit.Id) || !hitIds.Add(hit.Id)) errors.Add(hitPath + "/id");
                    if (hit.ImpactUs < 0 || hit.ImpactUs <= previous) errors.Add(hitPath + "/impactUs");
                    if (previous >= 0 && hit.ImpactUs - previous < 350000) errors.Add(hitPath + "/gapUs");
                    if (Window != null && (hit.ImpactUs > long.MaxValue - Window.AcquireLateUs || hit.ImpactUs + Window.AcquireLateUs > sequence.DurationUs)) errors.Add(hitPath + "/lateClosure");
                    if (hit.RawDamage < 0) errors.Add(hitPath + "/rawDamage");
                    if (hit.AllowedResponses == DefenseResponseMask.None || (hit.AllowedResponses & ~(DefenseResponseMask.Dodge | DefenseResponseMask.Parry)) != 0) errors.Add(hitPath + "/allowedResponses");
                    previous = hit.ImpactUs;
                }
            }

            foreach (ActorDefinition actor in Actors)
            {
                if (actor == null) { errors.Add("actors/null"); continue; }
                string path = "actors/" + actor.Id;
                if (string.IsNullOrWhiteSpace(actor.Id) || !actorIds.Add(actor.Id)) errors.Add(path + "/id");
                if (actor.IsHunter) hunterCount++;
                if (actor.SpawnOrdinal < 0) errors.Add(path + "/spawnOrdinal");
                if (actor.Speed < 80 || actor.Speed > 125) errors.Add(path + "/speed");
                if (actor.InitialTick < 0) errors.Add(path + "/initialTick");
                if (actor.MaxHp <= 0) errors.Add(path + "/maxHp");
                if (actor.MaxSeal < 0) errors.Add(path + "/maxSeal");
                if (actor.InitialAp < 0 || actor.InitialAp > 6 || (!actor.IsHunter && actor.InitialAp != 0)) errors.Add(path + "/initialAp");
                if (!actor.IsHunter && (actor.AttackSequenceIds.Count == 0 ||
                    actor.AttackSequenceIds[0] != actor.AttackSequenceId)) errors.Add(path + "/attackSequenceId");
                foreach (string sequenceId in actor.AttackSequenceIds)
                    if (!sequenceIds.Contains(sequenceId ?? "")) errors.Add(path + "/attackSequenceId");
            }
            if (hunterCount != 1) errors.Add("encounter/hunterCount");
            if (Actors.Count - hunterCount > MaxActiveEnemies && Waves.Count == 0) errors.Add("encounter/activeEnemies");

            foreach (SkillDefinition skill in Skills)
            {
                if (skill == null) { errors.Add("skills/null"); continue; }
                string path = "skills/" + skill.Id;
                if (string.IsNullOrWhiteSpace(skill.Id) || !skillIds.Add(skill.Id)) errors.Add(path + "/id");
                if (skill.ApCost < 0 || skill.ApCost > 6) errors.Add(path + "/apCost");
                if (skill.DelayTicks <= 0) errors.Add(path + "/delayTicks");
                if (double.IsNaN(skill.DamagePower) || double.IsInfinity(skill.DamagePower) || skill.DamagePower < 0) errors.Add(path + "/damagePower");
                if (skill.SealDamage < 0) errors.Add(path + "/sealDamage");
            }

            var waveIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (WaveDefinition wave in Waves)
            {
                if (wave == null) { errors.Add("waves/null"); continue; }
                string path = "waves/" + wave.Id;
                if (string.IsNullOrWhiteSpace(wave.Id) || !waveIds.Add(wave.Id)) errors.Add(path + "/id");
                if (wave.EnemyActorIds.Count == 0 || wave.EnemyActorIds.Count > MaxActiveEnemies) errors.Add(path + "/enemyActorIds");
                foreach (string actorId in wave.EnemyActorIds)
                {
                    ActorDefinition actor = FindActor(actorId);
                    if (actor == null || actor.IsHunter) errors.Add(path + "/enemyActorIds/" + actorId);
                }
            }
            return new ValidationResult(errors, warnings);
        }

        private string ComputeContentHash()
        {
            var text = new StringBuilder();
            Add(text, Id);
            Add(text, MaxActiveEnemies);
            Add(text, HunterAttack);
            if (Window == null) Add(text, "missing-window");
            else
            {
                Add(text, Window.AcquireEarlyUs); Add(text, Window.AcquireLateUs);
                Add(text, Window.DodgeEarlyUs); Add(text, Window.DodgeLateUs);
                Add(text, Window.ParryEarlyUs); Add(text, Window.ParryLateUs);
                Add(text, Window.PerfectEarlyUs); Add(text, Window.PerfectLateUs);
            }
            Add(text, Actors.Count);
            foreach (ActorDefinition actor in Actors)
            {
                if (actor == null) { Add(text, "null-actor"); continue; }
                Add(text, actor.Id); Add(text, actor.IsHunter ? 1 : 0);
                Add(text, actor.SpawnOrdinal); Add(text, actor.Speed); Add(text, actor.InitialTick);
                Add(text, actor.MaxHp); Add(text, actor.MaxSeal); Add(text, actor.InitialAp);
                Add(text, actor.AttackSequenceId); Add(text, actor.AttackSequenceIds.Count);
                foreach (string attackId in actor.AttackSequenceIds) Add(text, attackId);
            }
            Add(text, Sequences.Count);
            foreach (AttackSequenceDefinition sequence in Sequences)
            {
                if (sequence == null) { Add(text, "null-sequence"); continue; }
                Add(text, sequence.Id); Add(text, sequence.DurationUs); Add(text, sequence.DelayTicks);
                Add(text, sequence.InterruptibleOnBreak ? 1 : 0); Add(text, sequence.Hits.Count);
                foreach (HitDefinition hit in sequence.Hits)
                {
                    if (hit == null) { Add(text, "null-hit"); continue; }
                    Add(text, hit.Id); Add(text, hit.ImpactUs); Add(text, hit.RawDamage);
                    Add(text, (int)hit.AllowedResponses);
                }
            }
            Add(text, Skills.Count);
            foreach (SkillDefinition skill in Skills)
            {
                if (skill == null) { Add(text, "null-skill"); continue; }
                Add(text, skill.Id); Add(text, skill.ApCost); Add(text, skill.DelayTicks);
                Add(text, skill.DamagePower); Add(text, skill.SealDamage);
            }
            Add(text, Waves.Count);
            foreach (WaveDefinition wave in Waves)
            {
                if (wave == null) { Add(text, "null-wave"); continue; }
                Add(text, wave.Id); Add(text, wave.EnemyActorIds.Count);
                foreach (string actorId in wave.EnemyActorIds) Add(text, actorId);
            }
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
                var result = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash) result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }

        private static void Add(StringBuilder text, string value)
        {
            if (value == null) text.Append("-1:");
            else text.Append(value.Length).Append(':').Append(value);
        }

        private static void Add(StringBuilder text, long value)
        {
            text.Append(value.ToString(CultureInfo.InvariantCulture)).Append(';');
        }

        private static void Add(StringBuilder text, int value)
        {
            Add(text, (long)value);
        }

        private static void Add(StringBuilder text, double value)
        {
            text.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append(';');
        }
    }
}
