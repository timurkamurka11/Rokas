using System;
using System.Collections.Generic;

namespace Rokas.Core.ReactiveTurns
{
    // Pure Core authority. Presentation consumes events; it never advances HP or the queue itself.
    public sealed partial class ReactiveCombatSession
    {
        private readonly CombatDefinitions _definitions;
        private readonly TurnScheduler _scheduler = new TurnScheduler();
        private readonly Dictionary<string, CombatActorState> _actors = new Dictionary<string, CombatActorState>(StringComparer.Ordinal);
        private readonly Dictionary<string, CommandResult> _commandResults = new Dictionary<string, CommandResult>(StringComparer.Ordinal);
        private readonly List<CombatEvent> _events = new List<CombatEvent>();
        private QueueState _queue;
        private ReactivePhase _phase;
        private ReactivePhase _suspendedPhase;
        private long _revision;
        private long _lastCombatUs;
        private long _inputEpoch;
        private long _nextActionOrdinal;
        private long _seed;
        private string _economicRunId;
        private long _attemptId;
        private BattleCheckpoint _stableCheckpoint;
        private readonly string _checkpointError;
        private CombatOutcome? _terminal;
        private string _currentActionId;
        private string _currentAttackerId;
        private AttackSequenceDefinition _currentAttack;
        private CommandIntent _activeCommand;
        private long _actionStartUs;
        private long _actionEndUs;

        public ReactivePhase Phase { get { return _phase; } }
        public long Revision { get { return _revision; } }
        public long CurrentCombatUs { get { return _lastCombatUs; } }
        public long InputEpoch { get { return _inputEpoch; } }
        public CombatOutcome? TerminalResult { get { return _terminal; } }
        public string CurrentActionId { get { return _currentActionId; } }
        public string ActiveActorId { get { return _currentAttackerId; } }
        public AttackSequenceDefinition CurrentAttack { get { return _currentAttack; } }
        public long CurrentActionStartUs { get { return _actionStartUs; } }
        public QueueState Queue { get { return _queue.Clone(); } }
        public int HunterHp { get { return Hunter == null ? 0 : Hunter.Hp; } }
        public int HunterAp { get { return Hunter == null ? 0 : Hunter.Ap; } }
        public int EnemyHp { get { CombatActorState e = FirstEnemy; return e == null ? 0 : e.Hp; } }
        public int EnemySeal { get { CombatActorState e = FirstEnemy; return e == null ? 0 : e.Seal; } }
        public int EnemySealMax
        {
            get
            {
                foreach (ActorDefinition actor in _definitions.Actors)
                    if (!actor.IsHunter) return actor.MaxSeal;
                return 0;
            }
        }

        private CombatActorState Hunter
        {
            get
            {
                foreach (ActorDefinition actor in _definitions.Actors)
                    if (actor.IsHunter) return _actors[actor.Id];
                return null;
            }
        }

        private CombatActorState FirstEnemy
        {
            get
            {
                foreach (ActorDefinition actor in _definitions.Actors)
                    if (!actor.IsHunter) return _actors[actor.Id];
                return null;
            }
        }

        public ReactiveCombatSession(CombatDefinitions definitions, BattleCheckpoint checkpoint = null)
        {
            if (definitions == null) throw new ArgumentNullException("definitions");
            _definitions = definitions;
            BattleCheckpoint source = checkpoint == null ? BattleCheckpoint.CreateInitial(definitions, definitions.Id + ":1", 1, 1) : checkpoint.Clone();
            _checkpointError = ValidateCheckpointShape(definitions, source);
            _queue = QueueState.FromCheckpoint(source);
            _revision = source.revision;
            _seed = source.seed;
            _economicRunId = source.economicRunId;
            _attemptId = source.attemptId;
            _nextActionOrdinal = source.nextActionOrdinal <= 0 ? 1 : source.nextActionOrdinal;
            _phase = ReactivePhase.Preparing;
            if (source.actors != null) foreach (BattleActorSnapshot actor in source.actors)
                if (actor != null && !string.IsNullOrEmpty(actor.id))
                    _actors[actor.id] = new CombatActorState { Id = actor.id, DefinitionId = actor.definitionId,
                        Hp = actor.hp, Seal = actor.seal, Ap = actor.ap, NaturalTurns = actor.naturalTurns };
            foreach (ActorDefinition actor in definitions.Actors)
                if (!_actors.ContainsKey(actor.Id))
                    _actors[actor.Id] = new CombatActorState { Id = actor.Id, DefinitionId = actor.Id,
                        Hp = actor.MaxHp, Seal = actor.MaxSeal, Ap = actor.InitialAp };
            if (source.commands != null) foreach (BattleCommandSnapshot command in source.commands)
                if (command != null && !string.IsNullOrWhiteSpace(command.commandId))
                    _commandResults[command.commandId] = new CommandResult(command.accepted,
                        command.reason, command.actionId, command.newRevision);
            _stableCheckpoint = source;
            if (_checkpointError == null) OnCheckpointRestored(source);
        }

        public CombatStep Start()
        {
            if (_phase != ReactivePhase.Preparing) return Drain();
            ValidationResult validation = _definitions.Validate();
            if (!validation.IsValid)
            {
                _phase = ReactivePhase.SafeError;
                _events.Add(new CombatEvent(CombatEventKind.Error, detail: string.Join("|", validation.Errors)));
                return Drain();
            }
            if (_checkpointError != null)
            {
                _phase = ReactivePhase.SafeError;
                _events.Add(new CombatEvent(CombatEventKind.Error, detail: _checkpointError));
                return Drain();
            }
            if (_stableCheckpoint.contentHash != _definitions.ContentHash)
            {
                _phase = ReactivePhase.SafeError;
                _events.Add(new CombatEvent(CombatEventKind.Error, detail: "ContentHashMismatch"));
                return Drain();
            }
            if (_stableCheckpoint.terminalResult == CombatOutcome.Victory.ToString())
            {
                _terminal = CombatOutcome.Victory;
                _phase = ReactivePhase.Victory;
            }
            else if (_stableCheckpoint.terminalResult == CombatOutcome.Defeat.ToString())
            {
                _terminal = CombatOutcome.Defeat;
                _phase = ReactivePhase.Defeat;
            }
            else if (_stableCheckpoint.phase == ReactivePhase.PlayerCommand.ToString())
                _phase = ReactivePhase.PlayerCommand;
            else if (_stableCheckpoint.phase == ReactivePhase.Preparing.ToString() ||
                _stableCheckpoint.phase == ReactivePhase.Dispatch.ToString() ||
                _stableCheckpoint.phase == ReactivePhase.Settlement.ToString())
            {
                _phase = ReactivePhase.Dispatch;
                DispatchNext();
            }
            else
            {
                _phase = ReactivePhase.SafeError;
                _events.Add(new CombatEvent(CombatEventKind.Error, detail: "UnsupportedStablePhase"));
            }
            return Drain();
        }

        public CommandResult SubmitCommand(CommandIntent command)
        {
            if (command == null) return Reject(null, "NullCommand");
            if (!string.IsNullOrEmpty(command.CommandId) && _commandResults.ContainsKey(command.CommandId))
                return _commandResults[command.CommandId];
            if (string.IsNullOrWhiteSpace(command.CommandId)) return Reject(command, "MissingCommandId");
            if (_phase != ReactivePhase.PlayerCommand) return Reject(command, "NotPlayerCommand");
            if (command.ExpectedRevision != _revision) return Reject(command, "StaleRevision");
            if (!Enum.IsDefined(typeof(CommandKind), command.Kind)) return Reject(command, "UnknownCommand");
            SkillDefinition skill = command.Kind == CommandKind.Skill ? _definitions.FindSkill(command.SkillId) : null;
            if (command.Kind == CommandKind.Skill && skill == null) return Reject(command, "UnknownSkill");
            int cost = skill == null ? 0 : skill.ApCost;
            if (Hunter.Ap < cost) return Reject(command, "InsufficientAp");
            if (command.Kind == CommandKind.Basic || command.Kind == CommandKind.Skill)
            {
                if (command.TargetIds.Count != 1) return Reject(command, "TargetCount");
                CombatActorState target;
                if (!_actors.TryGetValue(command.TargetIds[0], out target) || !target.Alive ||
                    _definitions.FindActor(target.Id).IsHunter) return Reject(command, "InvalidTarget");
            }

            TurnDecision turn = _scheduler.SelectNext(_queue, _definitions);
            if (turn == null || _definitions.FindActor(turn.ActorId).IsHunter == false) return Reject(command, "NoPlayerTurn");
            int delay = command.Kind == CommandKind.Defend ? 80 : skill == null ? 100 : skill.DelayTicks;
            _scheduler.CommitAction(_queue, _definitions, turn, delay);
            Hunter.Ap -= cost;
            _currentActionId = NextActionId();
            _currentAttackerId = Hunter.Id;
            _currentAttack = null;
            _activeCommand = command;
            _actionStartUs = _lastCombatUs;
            _actionEndUs = checked(_actionStartUs + 400000);
            _phase = ReactivePhase.PlayerExecution;
            _revision++;
            var result = new CommandResult(true, null, _currentActionId, _revision);
            _commandResults.Add(command.CommandId, result);
            _events.Add(new CombatEvent(CombatEventKind.CommandCommitted, Hunter.Id,
                command.TargetIds.Count == 0 ? null : command.TargetIds[0], _currentActionId,
                combatUs: _lastCombatUs, detail: command.Kind.ToString()));
            OnPlayerCommandCommitted(command);
            return result;
        }

        public TurnForecast Preview(CommandIntent candidate, int count = 6)
        {
            return _scheduler.Preview(_queue, candidate, _definitions, count);
        }

        public CombatStep Advance(long combatUs)
        {
            return Advance(combatUs, combatUs - 40000);
        }

        public CombatStep Advance(long combatUs, long watermarkUs)
        {
            if (combatUs < _lastCombatUs) throw new ArgumentOutOfRangeException("combatUs", "Combat time must be monotonic.");
            if (watermarkUs > combatUs) throw new ArgumentOutOfRangeException("watermarkUs");
            if (_phase == ReactivePhase.Suspended || _phase == ReactivePhase.SafeError || _terminal.HasValue) return Drain();
            _lastCombatUs = combatUs;
            if (_phase == ReactivePhase.PlayerExecution)
            {
                OnPlayerExecutionAdvanced(combatUs, watermarkUs);
                if (_phase == ReactivePhase.PlayerExecution && watermarkUs >= _actionEndUs) SettleCurrentAction();
            }
            else if (_phase == ReactivePhase.EnemyExecution)
            {
                OnEnemyExecutionAdvanced(combatUs, watermarkUs);
                if (_phase == ReactivePhase.EnemyExecution && watermarkUs >= _actionEndUs) SettleCurrentAction();
            }
            else if (_phase == ReactivePhase.CounterWindow)
            {
                OnCounterWindowAdvanced(combatUs, watermarkUs);
            }
            return Drain();
        }

        public void Suspend(string reason)
        {
            if (_phase == ReactivePhase.Suspended || _terminal.HasValue) return;
            _suspendedPhase = _phase;
            _phase = ReactivePhase.Suspended;
            _inputEpoch++;
            _events.Add(new CombatEvent(CombatEventKind.Suspended, combatUs: _lastCombatUs, detail: reason));
        }

        public void SetInputEpoch(long epoch)
        {
            if (epoch < _inputEpoch) throw new ArgumentOutOfRangeException("epoch", "Input epoch cannot go backward.");
            _inputEpoch = epoch;
        }

        public void Resume(long newEpoch)
        {
            if (_phase != ReactivePhase.Suspended) return;
            if (newEpoch <= _inputEpoch) throw new ArgumentOutOfRangeException("newEpoch");
            _inputEpoch = newEpoch;
            _phase = _suspendedPhase;
            _events.Add(new CombatEvent(CombatEventKind.Resumed, combatUs: _lastCombatUs));
        }

        public BattleCheckpoint GetStableCheckpoint()
        {
            return _stableCheckpoint.Clone();
        }

        private CommandResult Reject(CommandIntent command, string reason)
        {
            var result = new CommandResult(false, reason, null, _revision);
            if (command != null && !string.IsNullOrWhiteSpace(command.CommandId))
            {
                _commandResults.Add(command.CommandId, result);
                if (_phase == ReactivePhase.PlayerCommand) CaptureStable();
            }
            return result;
        }

        private string NextActionId()
        {
            return _economicRunId + ":" + _attemptId + ":" + _nextActionOrdinal++;
        }

        private void DispatchNext()
        {
            if (_terminal.HasValue) return;
            if (!Hunter.Alive) { SetTerminal(CombatOutcome.Defeat); return; }
            bool anyEnemyAlive = false;
            foreach (ActorDefinition actor in _definitions.Actors)
                if (!actor.IsHunter && _actors[actor.Id].Alive) anyEnemyAlive = true;
            if (!anyEnemyAlive) { SetTerminal(CombatOutcome.Victory); return; }
            for (int guard = 0; guard < 1024; guard++)
            {
                TurnDecision turn = _scheduler.SelectNext(_queue, _definitions);
                if (turn == null) { _phase = ReactivePhase.SafeError; return; }
                ActorDefinition actor = _definitions.FindActor(turn.ActorId);
                if (actor.IsHunter)
                {
                    _queue.NowTick = turn.DispatchTick;
                    _phase = ReactivePhase.PlayerCommand;
                    Hunter.Ap = Math.Min(6, Hunter.Ap + 1);
                    Hunter.NaturalTurns++;
                    _revision++;
                    _events.Add(new CombatEvent(turn.ForcedResponse ? CombatEventKind.ForcedResponse : CombatEventKind.TurnStarted,
                        actor.Id, combatUs: _lastCombatUs));
                    OnPlayerTurnStarted();
                    CaptureStable();
                    return;
                }
                QueueEntry entry = _queue.GetEntry(actor.Id);
                if (entry.SkipNextTurn)
                {
                    _scheduler.SkipEnemySlot(_queue, _definitions, turn);
                    continue;
                }
                _currentAttackerId = actor.Id;
                _currentAttack = _definitions.FindSequence(turn.AttackSequenceId);
                _currentActionId = NextActionId();
                _activeCommand = null;
                _actionStartUs = _lastCombatUs;
                _actionEndUs = checked(_actionStartUs + _currentAttack.DurationUs);
                _scheduler.CommitAction(_queue, _definitions, turn, _currentAttack.DelayTicks);
                _actors[actor.Id].NaturalTurns++;
                _phase = ReactivePhase.EnemyExecution;
                _revision++;
                _events.Add(new CombatEvent(CombatEventKind.AttackStarted, actor.Id, Hunter.Id,
                    _currentActionId, combatUs: _lastCombatUs, detail: _currentAttack.Id));
                OnEnemyAttackStarted(_currentAttack, _currentActionId, _actionStartUs);
                return;
            }
            _phase = ReactivePhase.SafeError;
            _events.Add(new CombatEvent(CombatEventKind.Error, detail: "DispatchLoop"));
        }

        private void SettleCurrentAction()
        {
            _phase = ReactivePhase.Settlement;
            _events.Add(new CombatEvent(CombatEventKind.ActionSettled, _currentAttackerId,
                actionId: _currentActionId, combatUs: _lastCombatUs));
            OnActionSettled();
            _currentActionId = null;
            _currentAttackerId = null;
            _currentAttack = null;
            _activeCommand = null;
            _revision++;
            CaptureStable();
            _phase = ReactivePhase.Dispatch;
            DispatchNext();
        }

        private void SetTerminal(CombatOutcome outcome)
        {
            if (_terminal.HasValue) return;
            foreach (CombatActorState actor in _actors.Values)
                if (!actor.Alive) _queue.MarkDead(actor.Id);
            _terminal = outcome;
            _phase = outcome == CombatOutcome.Victory ? ReactivePhase.Victory : ReactivePhase.Defeat;
            _revision++;
            _events.Add(new CombatEvent(outcome == CombatOutcome.Victory ? CombatEventKind.Victory : CombatEventKind.Defeat,
                combatUs: _lastCombatUs));
            CaptureStable();
        }

        private void CaptureStable()
        {
            BattleCheckpoint checkpoint = _stableCheckpoint.Clone();
            checkpoint.revision = _revision;
            checkpoint.phase = _phase.ToString();
            checkpoint.seed = _seed;
            checkpoint.nowTick = _queue.NowTick;
            checkpoint.enemyActionCount = _queue.EnemyActionCount;
            checkpoint.defensiveHitCount = _queue.DefensiveHitCount;
            checkpoint.authoredDurationUs = _queue.AuthoredDurationUs;
            checkpoint.nextActionOrdinal = _nextActionOrdinal;
            checkpoint.terminalResult = _terminal.HasValue ? _terminal.Value.ToString() : null;
            checkpoint.hunterHp = Hunter.Hp;
            checkpoint.hunterAp = Hunter.Ap;
            CombatActorState firstEnemy = FirstEnemy;
            checkpoint.enemyHp = firstEnemy == null ? 0 : firstEnemy.Hp;
            checkpoint.enemySeal = firstEnemy == null ? 0 : firstEnemy.Seal;
            checkpoint.actors = new BattleActorSnapshot[_definitions.Actors.Count];
            for (int i = 0; i < _definitions.Actors.Count; i++)
            {
                CombatActorState actor = _actors[_definitions.Actors[i].Id];
                checkpoint.actors[i] = new BattleActorSnapshot { id = actor.Id, definitionId = actor.DefinitionId,
                    hp = actor.Hp, seal = actor.Seal, ap = actor.Ap, naturalTurns = actor.NaturalTurns };
            }
            checkpoint.queue = new BattleQueueEntrySnapshot[_queue.Entries.Count];
            for (int i = 0; i < _queue.Entries.Count; i++)
            {
                QueueEntry entry = _queue.Entries[i];
                checkpoint.queue[i] = new BattleQueueEntrySnapshot { actorId = entry.ActorId,
                    nextTick = entry.NextTick, spawnOrdinal = entry.SpawnOrdinal, speed = entry.Speed,
                    alive = entry.Alive, skipNextTurn = entry.SkipNextTurn, naturalTurns = entry.NaturalTurns };
            }
            var commandIds = new List<string>(_commandResults.Keys);
            commandIds.Sort(StringComparer.Ordinal);
            checkpoint.commands = new BattleCommandSnapshot[commandIds.Count];
            for (int i = 0; i < commandIds.Count; i++)
            {
                CommandResult result = _commandResults[commandIds[i]];
                checkpoint.commands[i] = new BattleCommandSnapshot { commandId = commandIds[i],
                    accepted = result.Accepted, reason = result.Reason, actionId = result.ActionId,
                    newRevision = result.NewRevision };
            }
            OnCheckpointCaptured(checkpoint);
            _stableCheckpoint = checkpoint;
        }

        private CombatStep Drain()
        {
            var step = new CombatStep(new List<CombatEvent>(_events), _revision, _phase, _terminal);
            _events.Clear();
            return step;
        }

        private string ValidateCheckpointShape(CombatDefinitions definitions, BattleCheckpoint checkpoint)
        {
            if (checkpoint.schemaVersion != 2 || string.IsNullOrWhiteSpace(checkpoint.economicRunId) ||
                checkpoint.attemptId < 1 || checkpoint.revision < 0 || checkpoint.nextActionOrdinal < 1 ||
                checkpoint.nowTick < 0 || checkpoint.enemyActionCount < 0 || checkpoint.enemyActionCount > 2 ||
                checkpoint.defensiveHitCount < 0 || checkpoint.defensiveHitCount > 6 ||
                checkpoint.authoredDurationUs < 0 || checkpoint.authoredDurationUs > 8000000)
                return "InvalidCheckpointHeader";
            if (checkpoint.actors == null || checkpoint.actors.Length != definitions.Actors.Count ||
                checkpoint.queue == null || checkpoint.queue.Length != definitions.Actors.Count)
                return "InvalidCheckpointActorsOrQueue";
            var actors = new Dictionary<string, BattleActorSnapshot>(StringComparer.Ordinal);
            foreach (BattleActorSnapshot snapshot in checkpoint.actors)
            {
                if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.id) || actors.ContainsKey(snapshot.id))
                    return "DuplicateOrMissingCheckpointActor";
                ActorDefinition definition = definitions.FindActor(snapshot.id);
                if (definition == null || snapshot.definitionId != definition.Id || snapshot.hp < 0 ||
                    snapshot.hp > definition.MaxHp || snapshot.seal < 0 || snapshot.seal > definition.MaxSeal ||
                    snapshot.naturalTurns < 0 || snapshot.ap < 0 || snapshot.ap > 6 ||
                    (!definition.IsHunter && snapshot.ap != 0))
                    return "InvalidCheckpointActorState";
                actors.Add(snapshot.id, snapshot);
            }
            var queueIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (BattleQueueEntrySnapshot entry in checkpoint.queue)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.actorId) || !queueIds.Add(entry.actorId))
                    return "DuplicateOrMissingCheckpointQueueEntry";
                ActorDefinition definition = definitions.FindActor(entry.actorId);
                BattleActorSnapshot actor;
                if (definition == null || !actors.TryGetValue(entry.actorId, out actor) ||
                    entry.nextTick < 0 || entry.spawnOrdinal != definition.SpawnOrdinal ||
                    entry.speed < 80 || entry.speed > 125 || entry.naturalTurns < 0 ||
                    entry.alive != (actor.hp > 0) || (definition.IsHunter && entry.skipNextTurn))
                    return "InvalidCheckpointQueueState";
            }
            if (checkpoint.commands != null)
            {
                var commandIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (BattleCommandSnapshot command in checkpoint.commands)
                {
                    if (command == null || string.IsNullOrWhiteSpace(command.commandId) ||
                        !commandIds.Add(command.commandId) || command.newRevision < 0 ||
                        command.newRevision > checkpoint.revision)
                        return "InvalidCheckpointCommandLedger";
                }
            }
            var extensionErrors = new List<string>();
            ValidateCheckpointExtensions(checkpoint, extensionErrors);
            if (extensionErrors.Count > 0) return "InvalidCheckpointExtensions:" + string.Join("|", extensionErrors);
            return null;
        }

        partial void OnEnemyAttackStarted(AttackSequenceDefinition sequence, string actionId, long startCombatUs);
        partial void OnEnemyExecutionAdvanced(long combatUs, long watermarkUs);
        partial void OnPlayerCommandCommitted(CommandIntent intent);
        partial void OnPlayerExecutionAdvanced(long combatUs, long watermarkUs);
        partial void OnPlayerTurnStarted();
        partial void OnCounterWindowAdvanced(long combatUs, long watermarkUs);
        partial void OnActionSettled();
        partial void OnCheckpointRestored(BattleCheckpoint checkpoint);
        partial void OnCheckpointCaptured(BattleCheckpoint checkpoint);
        partial void ValidateCheckpointExtensions(BattleCheckpoint checkpoint, List<string> errors);
    }
}
