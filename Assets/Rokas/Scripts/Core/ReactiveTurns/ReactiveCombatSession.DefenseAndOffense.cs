using System;
using System.Collections.Generic;

namespace Rokas.Core.ReactiveTurns
{
    public sealed class DefenseIntent
    {
        public string InputId { get; private set; }
        public long Epoch { get; private set; }
        public DefenseKind Kind { get; private set; }
        public long CombatUs { get; private set; }

        public DefenseIntent(string inputId, long epoch, DefenseKind kind, long combatUs)
        {
            InputId = inputId; Epoch = epoch; Kind = kind; CombatUs = combatUs;
        }
    }

    public sealed class CounterAttempt
    {
        public bool Accepted { get; internal set; }
        public string Reason { get; internal set; }
        public int Damage { get; internal set; }
    }

    public sealed class CommandImpactPreview
    {
        public int Damage { get; internal set; }
        public int SealDamage { get; internal set; }
        public int ApCost { get; internal set; }
        public int ApGain { get; internal set; }
        public int ProjectedAp { get; internal set; }
        public bool WouldBreak { get; internal set; }
        public bool CanAfford { get; internal set; }
    }

    public sealed partial class ReactiveCombatSession
    {
        private readonly Dictionary<string, SealLedger> _sealLedgers =
            new Dictionary<string, SealLedger>(StringComparer.Ordinal);
        private readonly HashSet<string> _counterInputIds = new HashSet<string>(StringComparer.Ordinal);
        private ActionPointLedger _points;
        private DefenseSequenceLedger _defenseLedger;
        private bool _activeImpactApplied;
        private bool _counterConfirmed;

        public bool EnemyBroken
        {
            get
            {
                CombatActorState enemy = FirstEnemy;
                SealLedger seal;
                return enemy != null && _sealLedgers.TryGetValue(enemy.Id, out seal) && seal.IsBroken;
            }
        }

        public CommandImpactPreview PreviewCommandImpact(CommandKind kind, string skillId, string targetId)
        {
            if (kind != CommandKind.Basic && kind != CommandKind.Skill && kind != CommandKind.Defend)
                return null;
            SkillDefinition skill = kind == CommandKind.Skill ? _definitions.FindSkill(skillId) : null;
            if (kind == CommandKind.Skill && skill == null) return null;
            if (kind == CommandKind.Defend)
                return new CommandImpactPreview { ProjectedAp = Hunter.Ap, CanAfford = true };
            CombatActorState target;
            if (string.IsNullOrEmpty(targetId) || !_actors.TryGetValue(targetId, out target) ||
                !target.Alive || _definitions.FindActor(targetId).IsHunter) return null;
            SealLedger seal;
            _sealLedgers.TryGetValue(targetId, out seal);
            int cost = skill == null ? 0 : skill.ApCost;
            int gain = kind == CommandKind.Basic ? Math.Min(2, 6 - Hunter.Ap) : 0;
            int sealDamage = skill == null || seal == null || seal.IsBroken || seal.RefractoryActionsRemaining > 0 ?
                0 : Math.Min(skill.SealDamage, seal.Remaining);
            return new CommandImpactPreview {
                Damage = ComputeHunterDamage(skill, seal), SealDamage = sealDamage,
                ApCost = cost, ApGain = gain, ProjectedAp = Math.Max(0, Math.Min(6, Hunter.Ap - cost + gain)),
                CanAfford = Hunter.Ap >= cost,
                WouldBreak = seal != null && sealDamage > 0 && sealDamage >= seal.Remaining
            };
        }

        public DefenseAttempt SubmitDefense(DefenseIntent intent)
        {
            if (intent == null) return new DefenseAttempt { Outcome = DefenseOutcome.Ignored };
            IReadOnlyList<DefenseAttempt> attempts = SubmitDefenses(new[] { intent });
            return attempts[0];
        }

        public IReadOnlyList<DefenseAttempt> SubmitDefenses(IEnumerable<DefenseIntent> intents)
        {
            if (intents == null) throw new ArgumentNullException("intents");
            var presses = new List<DefensePress>();
            foreach (DefenseIntent intent in intents)
            {
                if (intent == null) throw new ArgumentException("Null defense intent.", "intents");
                presses.Add(new DefensePress(intent.InputId, intent.Epoch, intent.Kind, intent.CombatUs));
            }
            if (_phase != ReactivePhase.EnemyExecution || _defenseLedger == null)
            {
                var ignored = new List<DefenseAttempt>(presses.Count);
                foreach (DefensePress press in presses)
                    ignored.Add(new DefenseAttempt { InputId = press.InputId, Outcome = DefenseOutcome.Ignored });
                return ignored;
            }
            _defenseLedger.ChangeEpoch(_inputEpoch);
            return _defenseLedger.SubmitPresses(presses);
        }

        public void ReleaseDefense(DefenseKind kind, long epoch)
        {
            if (_phase != ReactivePhase.EnemyExecution || _defenseLedger == null || epoch != _inputEpoch) return;
            _defenseLedger.ChangeEpoch(_inputEpoch);
            _defenseLedger.Release(kind);
        }

        public CounterAttempt ConfirmCounter(string inputId, long epoch, long combatUs)
        {
            if (string.IsNullOrWhiteSpace(inputId)) return CounterRejected("MissingInputId");
            if (!_counterInputIds.Add(inputId)) return CounterRejected("DuplicateInput");
            if (epoch != _inputEpoch) return CounterRejected("StaleEpoch");
            // Inputs are delivered before the frame's Advance. Finalize the due hit using its
            // delivery watermark so a press at the exact open endpoint can see the Counter.
            if (_phase == ReactivePhase.EnemyExecution && combatUs >= _lastCombatUs)
            {
                _lastCombatUs = combatUs;
                OnEnemyExecutionAdvanced(combatUs, combatUs - 40000);
            }
            if (_phase != ReactivePhase.CounterWindow || _defenseLedger == null ||
                !_defenseLedger.CounterEligible) return CounterRejected("CounterUnavailable");
            if (_counterConfirmed) return CounterRejected("CounterAlreadyConfirmed");
            if (combatUs < _defenseLedger.CounterOpenUs || combatUs > _defenseLedger.CounterCloseUs)
                return CounterRejected("OutsideCounterWindow");

            CombatActorState target = _actors[_currentAttackerId];
            SealLedger seal;
            _sealLedgers.TryGetValue(target.Id, out seal);
            int damage = DamageResolver.Resolve(new DamageRequest(1, _definitions.HunterAttack, 0) {
                Broken = seal == null ? 1 : seal.DamageMultiplier
            });
            target.Hp = Math.Max(0, target.Hp - damage);
            if (!target.Alive) _queue.MarkDead(target.Id);
            _counterConfirmed = true;
            _events.Add(new CombatEvent(CombatEventKind.HitResolved, Hunter.Id, target.Id,
                _currentActionId, "counter", combatUs, damage, "Counter"));
            SettleCurrentAction();
            return new CounterAttempt { Accepted = true, Damage = damage };
        }

        private static CounterAttempt CounterRejected(string reason)
        {
            return new CounterAttempt { Accepted = false, Reason = reason };
        }

        private int ComputeHunterDamage(SkillDefinition skill, SealLedger seal)
        {
            return DamageResolver.Resolve(new DamageRequest(skill == null ? 1 : skill.DamagePower,
                _definitions.HunterAttack, 0) { Broken = seal == null ? 1 : seal.DamageMultiplier });
        }

        partial void OnEnemyAttackStarted(AttackSequenceDefinition sequence, string actionId, long startCombatUs)
        {
            _defenseLedger = new DefenseSequenceLedger(actionId, sequence, startCombatUs,
                _definitions.Window, _inputEpoch);
            _counterConfirmed = false;
            _counterInputIds.Clear();
            _points.BeginEnemyAction(actionId);
        }

        partial void OnEnemyExecutionAdvanced(long combatUs, long watermarkUs)
        {
            if (_defenseLedger == null) return;
            _defenseLedger.ChangeEpoch(_inputEpoch);
            IReadOnlyList<DefenseHitResolution> resolved = _defenseLedger.Advance(combatUs, watermarkUs);
            bool suffixCanceled = false;
            foreach (DefenseHitResolution hit in resolved)
            {
                if (suffixCanceled) continue;
                int damage = DamageResolver.Resolve(new DamageRequest(1, hit.RawDamage, 0) {
                    DefenseOutcomeMultiplier = hit.Outcome == DefenseOutcome.Dodge ||
                        hit.Outcome == DefenseOutcome.Parry || hit.Outcome == DefenseOutcome.Perfect ? 0 : 1,
                    DefendMultiplier = _points.DefendActive ? .5 : 1
                });
                Hunter.Hp = Math.Max(0, Hunter.Hp - damage);
                DefenseReward reward = _points.GrantDefense(hit.EventId, hit.Outcome);
                Hunter.Ap = _points.Current;
                CombatActorState enemy = _actors[_currentAttackerId];
                SealLedger seal;
                if (_sealLedgers.TryGetValue(enemy.Id, out seal) && reward.SealDamage > 0)
                {
                    bool wasBroken = seal.IsBroken;
                    seal.ApplyDamage(reward.SealDamage, _currentActionId, hit.EventId);
                    enemy.Seal = seal.Remaining;
                    if (!wasBroken && seal.IsBroken)
                    {
                        if (seal.TryConsumeSkipToken()) _queue.GetEntry(enemy.Id).SkipNextTurn = true;
                        if (_currentAttack.InterruptibleOnBreak)
                        {
                            _defenseLedger.CancelSuffixAfter(hit.HitId);
                            suffixCanceled = true;
                        }
                    }
                }
                _events.Add(new CombatEvent(CombatEventKind.HitResolved, enemy.Id, Hunter.Id,
                    _currentActionId, hit.HitId, hit.ImpactUs, damage, hit.Outcome.ToString()));
            }
            if (!Hunter.Alive) { SetTerminal(CombatOutcome.Defeat); return; }
            if (_defenseLedger.CounterEligible && combatUs >= _defenseLedger.CounterOpenUs)
                _phase = ReactivePhase.CounterWindow;
        }

        partial void OnPlayerCommandCommitted(CommandIntent intent)
        {
            _points.SyncCurrent(Hunter.Ap);
            _activeImpactApplied = false;
            if (intent.Kind == CommandKind.Defend) _points.ActivateDefend();
        }

        partial void OnPlayerExecutionAdvanced(long combatUs, long watermarkUs)
        {
            if (_activeImpactApplied || watermarkUs < checked(_actionStartUs + 200000)) return;
            _activeImpactApplied = true;
            if (_activeCommand.Kind == CommandKind.Retreat)
            {
                SetTerminal(CombatOutcome.Defeat);
                return;
            }
            if (_activeCommand.Kind != CommandKind.Basic && _activeCommand.Kind != CommandKind.Skill) return;
            CombatActorState target = _actors[_activeCommand.TargetIds[0]];
            SealLedger seal;
            _sealLedgers.TryGetValue(target.Id, out seal);
            SkillDefinition skill = _activeCommand.Kind == CommandKind.Skill ?
                _definitions.FindSkill(_activeCommand.SkillId) : null;
            int damage = ComputeHunterDamage(skill, seal);
            target.Hp = Math.Max(0, target.Hp - damage);
            if (!target.Alive) _queue.MarkDead(target.Id);
            string impactId = _currentActionId + "/impact";
            if (skill != null && seal != null && skill.SealDamage > 0)
            {
                seal.ApplyDamage(skill.SealDamage, _currentActionId, impactId);
                target.Seal = seal.Remaining;
                if (seal.IsBroken && seal.TryConsumeSkipToken())
                    _queue.GetEntry(target.Id).SkipNextTurn = true;
            }
            if (_activeCommand.Kind == CommandKind.Basic)
            {
                _points.GrantBasic(impactId);
                Hunter.Ap = _points.Current;
            }
            _events.Add(new CombatEvent(CombatEventKind.HitResolved, Hunter.Id, target.Id,
                _currentActionId, "impact", checked(_actionStartUs + 200000), damage,
                _activeCommand.Kind.ToString()));
            if (!target.Alive)
            {
                bool anyEnemyAlive = false;
                foreach (ActorDefinition actor in _definitions.Actors)
                    if (!actor.IsHunter && _actors[actor.Id].Alive) { anyEnemyAlive = true; break; }
                if (!anyEnemyAlive) SetTerminal(CombatOutcome.Victory);
            }
        }

        partial void OnPlayerTurnStarted()
        {
            _points.SyncAfterNaturalTurn(Hunter.Ap);
        }

        partial void OnCounterWindowAdvanced(long combatUs, long watermarkUs)
        {
            if (_defenseLedger != null && watermarkUs >= _defenseLedger.CounterCloseUs)
                SettleCurrentAction();
        }

        partial void OnActionSettled()
        {
            if (_currentAttackerId == Hunter.Id)
            {
                if (_activeCommand != null && (_activeCommand.Kind == CommandKind.Basic ||
                    _activeCommand.Kind == CommandKind.Skill))
                {
                    string targetId = _activeCommand.TargetIds[0];
                    SealLedger seal;
                    if (_sealLedgers.TryGetValue(targetId, out seal))
                        seal.CompleteOffensiveCommand(_currentActionId, true);
                }
                foreach (KeyValuePair<string, SealLedger> item in _sealLedgers)
                {
                    item.Value.OnPlayerTurnEnd();
                    _actors[item.Key].Seal = item.Value.Remaining;
                }
            }
            else
            {
                SealLedger seal;
                if (_sealLedgers.TryGetValue(_currentAttackerId, out seal)) seal.CompleteNaturalEnemyAction();
                _defenseLedger = null;
            }
        }

        partial void OnCheckpointRestored(BattleCheckpoint checkpoint)
        {
            _points = new ActionPointLedger(Hunter.Ap);
            _points.RestoreAccounting(checkpoint.defenseApThisInterval, checkpoint.defendActive);
            foreach (ActorDefinition actor in _definitions.Actors)
            {
                if (actor.IsHunter || actor.MaxSeal <= 0) continue;
                var seal = new SealLedger(actor.MaxSeal, 1);
                BattleSealSnapshot saved = null;
                if (checkpoint.sealStates != null) foreach (BattleSealSnapshot item in checkpoint.sealStates)
                    if (item != null && item.actorId == actor.Id) { saved = item; break; }
                if (saved != null)
                    seal.Restore(saved.remaining, saved.isBroken, saved.refractoryActionsRemaining,
                        saved.skipTokenPending, saved.breakTriggerActionId, saved.ignoredPlayerTurns,
                        saved.ignoreNextPlayerTurnEnd);
                else if (_actors[actor.Id].Seal != actor.MaxSeal)
                    seal.Restore(_actors[actor.Id].Seal, false, 0, false, null, 0, false);
                _sealLedgers.Add(actor.Id, seal);
            }
        }

        partial void OnCheckpointCaptured(BattleCheckpoint checkpoint)
        {
            checkpoint.defenseApThisInterval = _points.DefenseApThisInterval;
            checkpoint.defendActive = _points.DefendActive;
            checkpoint.sealStates = new BattleSealSnapshot[_sealLedgers.Count];
            int index = 0;
            foreach (KeyValuePair<string, SealLedger> item in _sealLedgers)
            {
                SealLedger seal = item.Value;
                checkpoint.sealStates[index++] = new BattleSealSnapshot {
                    actorId = item.Key, remaining = seal.Remaining, isBroken = seal.IsBroken,
                    refractoryActionsRemaining = seal.RefractoryActionsRemaining,
                    skipTokenPending = seal.SkipTokenPending,
                    breakTriggerActionId = seal.BreakTriggerActionId,
                    ignoredPlayerTurns = seal.IgnoredPlayerTurns,
                    ignoreNextPlayerTurnEnd = seal.IgnoreNextPlayerTurnEnd
                };
            }
        }

        partial void ValidateCheckpointExtensions(BattleCheckpoint checkpoint, List<string> errors)
        {
            if (checkpoint.defenseApThisInterval < 0 || checkpoint.defenseApThisInterval > 2)
                errors.Add("defenseApThisInterval");
            if (checkpoint.sealStates == null)
            {
                if (checkpoint.revision > 0) errors.Add("sealStates/missing");
                return;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (BattleSealSnapshot saved in checkpoint.sealStates)
            {
                if (saved == null || string.IsNullOrWhiteSpace(saved.actorId) || !seen.Add(saved.actorId))
                {
                    errors.Add("sealStates/identity");
                    continue;
                }
                ActorDefinition definition = _definitions.FindActor(saved.actorId);
                if (definition == null || definition.IsHunter || definition.MaxSeal <= 0 ||
                    saved.remaining < 0 || saved.remaining > definition.MaxSeal ||
                    saved.refractoryActionsRemaining < 0 || saved.ignoredPlayerTurns < 0 ||
                    saved.ignoredPlayerTurns > 1 || (saved.isBroken != (saved.remaining == 0)) ||
                    (saved.isBroken && string.IsNullOrWhiteSpace(saved.breakTriggerActionId)))
                    errors.Add("sealStates/" + saved.actorId);
                if (checkpoint.actors != null) foreach (BattleActorSnapshot actor in checkpoint.actors)
                    if (actor != null && actor.id == saved.actorId && actor.seal != saved.remaining)
                        errors.Add("sealStates/actorMismatch/" + saved.actorId);
            }
            foreach (ActorDefinition actor in _definitions.Actors)
                if (!actor.IsHunter && actor.MaxSeal > 0 && !seen.Contains(actor.Id))
                    errors.Add("sealStates/missing/" + actor.Id);
        }
    }
}
