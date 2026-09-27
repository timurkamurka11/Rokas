using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Rokas.Core.ReactiveTurns
{
    public sealed class QueueEntry
    {
        public string ActorId { get; internal set; }
        public long NextTick { get; internal set; }
        public int SpawnOrdinal { get; internal set; }
        public int Speed { get; internal set; }
        public bool Alive { get; internal set; }
        public bool SkipNextTurn { get; internal set; }
        public int NaturalTurns { get; internal set; }

        internal QueueEntry Clone()
        {
            return new QueueEntry { ActorId = ActorId, NextTick = NextTick, SpawnOrdinal = SpawnOrdinal,
                Speed = Speed, Alive = Alive, SkipNextTurn = SkipNextTurn, NaturalTurns = NaturalTurns };
        }
    }

    public sealed class QueueState
    {
        private readonly List<QueueEntry> _entries;
        public long NowTick { get; internal set; }
        public int EnemyActionCount { get; internal set; }
        public int DefensiveHitCount { get; internal set; }
        public long AuthoredDurationUs { get; internal set; }
        public ReadOnlyCollection<QueueEntry> Entries { get { return _entries.AsReadOnly(); } }

        internal QueueState(List<QueueEntry> entries, long nowTick, int enemyActionCount,
            int defensiveHitCount, long authoredDurationUs)
        {
            _entries = entries; NowTick = nowTick; EnemyActionCount = enemyActionCount;
            DefensiveHitCount = defensiveHitCount; AuthoredDurationUs = authoredDurationUs;
        }

        public static QueueState CreateInitial(CombatDefinitions definitions)
        {
            if (definitions == null) throw new ArgumentNullException("definitions");
            var entries = new List<QueueEntry>();
            foreach (ActorDefinition actor in definitions.Actors)
                entries.Add(new QueueEntry { ActorId = actor.Id, NextTick = actor.InitialTick,
                    SpawnOrdinal = actor.SpawnOrdinal, Speed = actor.Speed, Alive = true });
            return new QueueState(entries, 0, 0, 0, 0);
        }

        public static QueueState FromCheckpoint(BattleCheckpoint checkpoint)
        {
            if (checkpoint == null) throw new ArgumentNullException("checkpoint");
            var entries = new List<QueueEntry>();
            if (checkpoint.queue != null) foreach (BattleQueueEntrySnapshot item in checkpoint.queue)
                if (item != null) entries.Add(new QueueEntry { ActorId = item.actorId, NextTick = item.nextTick,
                    SpawnOrdinal = item.spawnOrdinal, Speed = item.speed, Alive = item.alive,
                    SkipNextTurn = item.skipNextTurn, NaturalTurns = item.naturalTurns });
            return new QueueState(entries, checkpoint.nowTick, checkpoint.enemyActionCount,
                checkpoint.defensiveHitCount, checkpoint.authoredDurationUs);
        }

        public QueueState Clone()
        {
            var entries = new List<QueueEntry>(_entries.Count);
            foreach (QueueEntry entry in _entries) entries.Add(entry.Clone());
            return new QueueState(entries, NowTick, EnemyActionCount, DefensiveHitCount, AuthoredDurationUs);
        }

        public QueueEntry GetEntry(string actorId)
        {
            foreach (QueueEntry entry in _entries) if (entry.ActorId == actorId) return entry;
            return null;
        }

        public void MarkDead(string actorId)
        {
            QueueEntry entry = GetEntry(actorId);
            if (entry != null) entry.Alive = false;
        }

        public void AddSummon(string actorId, int spawnOrdinal, int speed, long dueTick)
        {
            if (string.IsNullOrWhiteSpace(actorId) || GetEntry(actorId) != null) throw new ArgumentException("Summon requires a new actor ID.", "actorId");
            if (speed < 80 || speed > 125) throw new ArgumentOutOfRangeException("speed");
            if (dueTick < checked(NowTick + 60)) throw new ArgumentOutOfRangeException("dueTick", "Summon needs at least 60 ticks of notice.");
            _entries.Add(new QueueEntry { ActorId = actorId, SpawnOrdinal = spawnOrdinal,
                Speed = speed, NextTick = dueTick, Alive = true });
        }
    }

    public sealed class TurnDecision
    {
        public string ActorId { get; private set; }
        public long DispatchTick { get; private set; }
        public bool ForcedResponse { get; private set; }
        public bool UncertainIntent { get; private set; }
        public string AttackSequenceId { get; private set; }

        internal TurnDecision(string actorId, long dispatchTick, bool forcedResponse, bool uncertainIntent,
            string attackSequenceId = null)
        {
            ActorId = actorId; DispatchTick = dispatchTick; ForcedResponse = forcedResponse;
            UncertainIntent = uncertainIntent; AttackSequenceId = attackSequenceId;
        }
    }

    public sealed class TurnForecast
    {
        public ReadOnlyCollection<TurnDecision> Slots { get; private set; }
        public bool HasUncertainIntent { get; private set; }

        internal TurnForecast(List<TurnDecision> slots)
        {
            Slots = slots.AsReadOnly();
            foreach (TurnDecision slot in slots) if (slot.UncertainIntent) HasUncertainIntent = true;
        }
    }

    public sealed class TurnScheduler
    {
        public TurnDecision SelectNext(QueueState state, CombatDefinitions definitions)
        {
            if (state == null) throw new ArgumentNullException("state");
            if (definitions == null) throw new ArgumentNullException("definitions");
            QueueEntry candidate = null;
            QueueEntry hunter = null;
            foreach (QueueEntry entry in state.Entries)
            {
                if (!entry.Alive) continue;
                ActorDefinition definition = definitions.FindActor(entry.ActorId);
                if (definition == null) continue;
                if (definition.IsHunter) hunter = entry;
                if (candidate == null || Compare(entry, candidate) < 0) candidate = entry;
            }
            if (candidate == null) return null;
            ActorDefinition candidateDef = definitions.FindActor(candidate.ActorId);
            if (!candidateDef.IsHunter && hunter != null && !candidate.SkipNextTurn)
            {
                string attackId = EnemyPolicy.ChooseAttack(candidateDef, candidate.NaturalTurns, 0);
                AttackSequenceDefinition attack = definitions.FindSequence(attackId);
                if (attack != null && (state.EnemyActionCount + 1 > 2 ||
                    state.DefensiveHitCount + attack.Hits.Count > 6 ||
                    state.AuthoredDurationUs > 8000000 - attack.DurationUs))
                {
                    long forcedTick = Math.Max(state.NowTick, Math.Min(hunter.NextTick, candidate.NextTick));
                    return new TurnDecision(hunter.ActorId, forcedTick, true, false);
                }
            }
            string selectedAttack = candidateDef.IsHunter ? null : EnemyPolicy.ChooseAttack(candidateDef, candidate.NaturalTurns, 0);
            return new TurnDecision(candidate.ActorId, Math.Max(state.NowTick, candidate.NextTick),
                false, !candidateDef.IsHunter && string.IsNullOrEmpty(selectedAttack), selectedAttack);
        }

        public void CommitAction(QueueState state, CombatDefinitions definitions, TurnDecision decision, int delayTicks)
        {
            if (state == null || definitions == null || decision == null) throw new ArgumentNullException("state/definitions/decision");
            QueueEntry entry = state.GetEntry(decision.ActorId);
            ActorDefinition actor = definitions.FindActor(decision.ActorId);
            if (entry == null || !entry.Alive || actor == null) throw new InvalidOperationException("Actor is unavailable.");
            if (delayTicks <= 0) throw new ArgumentOutOfRangeException("delayTicks");
            if (decision.DispatchTick < state.NowTick) throw new InvalidOperationException("Queue time cannot go backward.");
            long recovery = checked(((long)delayTicks * 100 + entry.Speed - 1) / entry.Speed);
            state.NowTick = decision.DispatchTick;
            entry.NextTick = checked(decision.DispatchTick + recovery);
            if (actor.IsHunter)
            {
                state.EnemyActionCount = 0;
                state.DefensiveHitCount = 0;
                state.AuthoredDurationUs = 0;
            }
            else
            {
                AttackSequenceDefinition attack = definitions.FindSequence(decision.AttackSequenceId);
                if (attack == null) throw new InvalidOperationException("Missing attack definition.");
                state.EnemyActionCount = checked(state.EnemyActionCount + 1);
                state.DefensiveHitCount = checked(state.DefensiveHitCount + attack.Hits.Count);
                state.AuthoredDurationUs = checked(state.AuthoredDurationUs + attack.DurationUs);
                entry.NaturalTurns = checked(entry.NaturalTurns + 1);
            }
        }

        public void SkipEnemySlot(QueueState state, CombatDefinitions definitions, TurnDecision decision)
        {
            QueueEntry entry = state.GetEntry(decision.ActorId);
            ActorDefinition actor = definitions.FindActor(decision.ActorId);
            if (entry == null || actor == null || actor.IsHunter || !entry.SkipNextTurn)
                throw new InvalidOperationException("No enemy skip token.");
            state.NowTick = Math.Max(state.NowTick, decision.DispatchTick);
            entry.NextTick = checked(state.NowTick + 100);
            entry.SkipNextTurn = false;
        }

        public TurnForecast Preview(QueueState state, CommandIntent candidate, CombatDefinitions definitions, int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException("count");
            QueueState copy = state.Clone();
            var slots = new List<TurnDecision>();
            for (int i = 0; i < count; i++)
            {
                TurnDecision decision = SelectNext(copy, definitions);
                if (decision == null) break;
                slots.Add(decision);
                ActorDefinition actor = definitions.FindActor(decision.ActorId);
                if (actor.IsHunter)
                {
                    int delay = candidate != null && candidate.Kind == CommandKind.Defend ? 80 :
                        candidate != null && candidate.Kind == CommandKind.Skill && definitions.FindSkill(candidate.SkillId) != null ?
                        definitions.FindSkill(candidate.SkillId).DelayTicks : 100;
                    CommitAction(copy, definitions, decision, delay);
                }
                else if (copy.GetEntry(decision.ActorId).SkipNextTurn)
                    SkipEnemySlot(copy, definitions, decision);
                else
                    CommitAction(copy, definitions, decision, definitions.FindSequence(decision.AttackSequenceId).DelayTicks);
            }
            return new TurnForecast(slots);
        }

        private static int Compare(QueueEntry left, QueueEntry right)
        {
            int due = left.NextTick.CompareTo(right.NextTick);
            if (due != 0) return due;
            int ordinal = left.SpawnOrdinal.CompareTo(right.SpawnOrdinal);
            return ordinal != 0 ? ordinal : string.CompareOrdinal(left.ActorId, right.ActorId);
        }
    }
}
