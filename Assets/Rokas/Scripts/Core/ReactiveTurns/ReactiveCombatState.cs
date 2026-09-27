using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Rokas.Core.ReactiveTurns
{
    public enum ReactivePhase
    {
        Preparing, Intro, Dispatch, PlayerCommand, PlayerExecution, EnemyTelegraph,
        EnemyExecution, CounterWindow, CounterExecution, Settlement, WaveTransition,
        Victory, Defeat, Suspended, SafeError
    }

    public enum CombatOutcome { Victory, Defeat }

    public enum CombatEventKind
    {
        TurnStarted, CommandCommitted, AttackStarted, HitResolved, ActionSettled,
        ForcedResponse, Victory, Defeat, Suspended, Resumed, Error
    }

    public sealed class CombatEvent
    {
        public CombatEventKind Kind { get; private set; }
        public string ActorId { get; private set; }
        public string TargetId { get; private set; }
        public string ActionId { get; private set; }
        public string HitId { get; private set; }
        public long CombatUs { get; private set; }
        public int Amount { get; private set; }
        public string Detail { get; private set; }

        public CombatEvent(CombatEventKind kind, string actorId = null, string targetId = null,
            string actionId = null, string hitId = null, long combatUs = 0, int amount = 0, string detail = null)
        {
            Kind = kind; ActorId = actorId; TargetId = targetId; ActionId = actionId;
            HitId = hitId; CombatUs = combatUs; Amount = amount; Detail = detail;
        }
    }

    public sealed class CombatStep
    {
        public ReadOnlyCollection<CombatEvent> Events { get; private set; }
        public long Revision { get; private set; }
        public ReactivePhase Phase { get; private set; }
        public CombatOutcome? TerminalResult { get; private set; }

        internal CombatStep(List<CombatEvent> events, long revision, ReactivePhase phase, CombatOutcome? terminalResult)
        {
            Events = events.AsReadOnly(); Revision = revision; Phase = phase; TerminalResult = terminalResult;
        }
    }

    public sealed class CommandIntent
    {
        public string CommandId { get; private set; }
        public long ExpectedRevision { get; private set; }
        public CommandKind Kind { get; private set; }
        public string SkillId { get; private set; }
        public ReadOnlyCollection<string> TargetIds { get; private set; }

        public CommandIntent(string commandId, long expectedRevision, CommandKind kind, string skillId, string[] targetIds)
        {
            CommandId = commandId; ExpectedRevision = expectedRevision; Kind = kind;
            SkillId = skillId; TargetIds = Array.AsReadOnly((string[])(targetIds ?? new string[0]).Clone());
        }
    }

    public sealed class CommandResult
    {
        public bool Accepted { get; private set; }
        public string Reason { get; private set; }
        public string ActionId { get; private set; }
        public long NewRevision { get; private set; }

        internal CommandResult(bool accepted, string reason, string actionId, long newRevision)
        {
            Accepted = accepted; Reason = reason; ActionId = actionId; NewRevision = newRevision;
        }
    }

    public sealed class CombatActorState
    {
        public string Id { get; internal set; }
        public string DefinitionId { get; internal set; }
        public int Hp { get; internal set; }
        public int Seal { get; internal set; }
        public int Ap { get; internal set; }
        public int NaturalTurns { get; internal set; }
        public bool Alive { get { return Hp > 0; } }

        internal CombatActorState Clone()
        {
            return new CombatActorState { Id = Id, DefinitionId = DefinitionId, Hp = Hp, Seal = Seal, Ap = Ap, NaturalTurns = NaturalTurns };
        }
    }

    // Public fields keep this stable snapshot compatible with Unity JsonUtility.
    [Serializable]
    public sealed class BattleActorSnapshot
    {
        public string id;
        public string definitionId;
        public int hp;
        public int seal;
        public int ap;
        public int naturalTurns;
    }

    [Serializable]
    public sealed class BattleQueueEntrySnapshot
    {
        public string actorId;
        public long nextTick;
        public int spawnOrdinal;
        public int speed;
        public bool alive;
        public bool skipNextTurn;
        public int naturalTurns;
    }

    [Serializable]
    public sealed class BattleCommandSnapshot
    {
        public string commandId;
        public bool accepted;
        public string reason;
        public string actionId;
        public long newRevision;
    }

    [Serializable]
    public sealed partial class BattleCheckpoint
    {
        public int schemaVersion = 2;
        public string economicRunId;
        public long attemptId;
        public long revision;
        public long seed;
        public string phase;
        public string contentHash;
        public int hunterHp;
        public int hunterAp;
        public int enemyHp;
        public int enemySeal;
        public string terminalResult;
        public long nowTick;
        public int enemyActionCount;
        public int defensiveHitCount;
        public long authoredDurationUs;
        public long nextActionOrdinal;
        public BattleActorSnapshot[] actors;
        public BattleQueueEntrySnapshot[] queue;
        public BattleCommandSnapshot[] commands;

        public static BattleCheckpoint CreateInitial(CombatDefinitions definitions, string economicRunId,
            long attemptId, long seed)
        {
            if (definitions == null) throw new ArgumentNullException("definitions");
            var checkpoint = new BattleCheckpoint {
                economicRunId = economicRunId, attemptId = attemptId, seed = seed,
                contentHash = definitions.ContentHash, phase = ReactivePhase.Preparing.ToString(),
                nextActionOrdinal = 1,
                actors = new BattleActorSnapshot[definitions.Actors.Count],
                queue = new BattleQueueEntrySnapshot[definitions.Actors.Count],
                commands = new BattleCommandSnapshot[0]
            };
            for (int i = 0; i < definitions.Actors.Count; i++)
            {
                ActorDefinition actor = definitions.Actors[i];
                checkpoint.actors[i] = new BattleActorSnapshot { id = actor.Id, definitionId = actor.Id,
                    hp = actor.MaxHp, seal = actor.MaxSeal, ap = actor.InitialAp };
                checkpoint.queue[i] = new BattleQueueEntrySnapshot { actorId = actor.Id,
                    nextTick = actor.InitialTick, spawnOrdinal = actor.SpawnOrdinal,
                    speed = actor.Speed, alive = true };
                if (actor.IsHunter) { checkpoint.hunterHp = actor.MaxHp; checkpoint.hunterAp = actor.InitialAp; }
                else if (checkpoint.enemyHp == 0) { checkpoint.enemyHp = actor.MaxHp; checkpoint.enemySeal = actor.MaxSeal; }
            }
            return checkpoint;
        }

        public BattleCheckpoint Clone()
        {
            var clone = (BattleCheckpoint)MemberwiseClone();
            if (actors != null)
            {
                clone.actors = new BattleActorSnapshot[actors.Length];
                for (int i = 0; i < actors.Length; i++) if (actors[i] != null)
                    clone.actors[i] = new BattleActorSnapshot { id = actors[i].id, definitionId = actors[i].definitionId,
                        hp = actors[i].hp, seal = actors[i].seal, ap = actors[i].ap,
                        naturalTurns = actors[i].naturalTurns };
            }
            if (queue != null)
            {
                clone.queue = new BattleQueueEntrySnapshot[queue.Length];
                for (int i = 0; i < queue.Length; i++) if (queue[i] != null)
                    clone.queue[i] = new BattleQueueEntrySnapshot { actorId = queue[i].actorId,
                        nextTick = queue[i].nextTick, spawnOrdinal = queue[i].spawnOrdinal,
                        speed = queue[i].speed, alive = queue[i].alive, skipNextTurn = queue[i].skipNextTurn,
                        naturalTurns = queue[i].naturalTurns };
            }
            if (commands != null)
            {
                clone.commands = new BattleCommandSnapshot[commands.Length];
                for (int i = 0; i < commands.Length; i++) if (commands[i] != null)
                    clone.commands[i] = new BattleCommandSnapshot { commandId = commands[i].commandId,
                        accepted = commands[i].accepted, reason = commands[i].reason,
                        actionId = commands[i].actionId, newRevision = commands[i].newRevision };
            }
            OnCheckpointCloned(clone);
            return clone;
        }

        partial void OnCheckpointCloned(BattleCheckpoint clone);
    }
}
