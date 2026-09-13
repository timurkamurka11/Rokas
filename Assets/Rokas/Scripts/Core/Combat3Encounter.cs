using System;
using System.Collections.Generic;

namespace Rokas.Core
{
    public enum Combat3Practice { Heavy, LowWave, Projectile }

    public sealed class Combat3Attack
    {
        private double contactDelay;
        private double activeRemaining = .18;
        private readonly double approachDuration;
        public int Id { get; private set; }
        public int Lane { get; private set; }
        public string KindName { get; private set; }
        public string StateName { get; internal set; }
        public bool IsDeflectable { get { return KindName == "Projectile"; } }
        public float ContactDelayRemaining { get { return (float)Math.Max(0, contactDelay); } }
        public float ActiveRemaining { get { return StateName == "Active" ? (float)Math.Max(0, activeRemaining) : 0; } }
        public float ApproachProgress { get { return (float)(1 - Math.Max(0, contactDelay) / approachDuration); } }

        internal Combat3Attack(int id, int lane, string kind, double delay)
        {
            Id = id; Lane = lane; KindName = kind;
            StateName = "Telegraph"; contactDelay = approachDuration = delay;
        }

        internal void Advance(double step)
        {
            if (StateName == "Telegraph")
            {
                contactDelay -= step;
                if (contactDelay <= .00000001) { contactDelay = 0; StateName = "Active"; }
            }
            else if (StateName == "Active")
            {
                activeRemaining -= step;
                if (activeRemaining <= .00000001) { activeRemaining = 0; StateName = "Resolved"; }
            }
        }
    }

    // Owns lane, attack lifetime and input timing only. HP, gauges and results remain in CombatService.
    public sealed class Combat3Encounter
    {
        private const double StepSeconds = 1.0 / 60.0;
        private readonly CombatService combat;
        private readonly Combat3Practice? practice;
        private readonly List<Combat3Attack> attacks = new List<Combat3Attack>(2);
        private int nextAttackId;
        private int patternIndex;
        private double accumulator;
        private double stageRemaining;
        private double moveElapsed;
        private double repeatElapsed;
        private double readDelay;
        private float moveOrigin;
        private int heldDirection;
        private int pendingDirection;
        private bool attackHeld;
        private bool attackArmed = true;
        private bool counterCommand;
        private bool counterUsed;
        private bool dodgeCommand;
        private int dodgeDirection;
        private double dodgeElapsed = .18;
        private bool perfectUsed;
        private double perfectFeedback;
        private bool deflectCommand;
        private double deflectElapsed = .14;
        private double deflectFeedback;

        public int Lane { get; private set; }
        public float LanePosition { get; private set; }
        public bool IsMoving { get; private set; }
        public int QueuedDirection { get; private set; }
        public string StageName { get; private set; }
        public IReadOnlyList<Combat3Attack> Attacks { get; private set; }
        public int AttackId { get { return attacks[0].Id; } }
        public int AttackLane { get { return attacks[0].Lane; } }
        public string AttackKindName { get { return attacks[0].KindName; } }
        public string AttackStateName { get { return attacks[0].StateName; } private set { attacks[0].StateName = value; } }
        public float DodgeRemaining { get { return dodgeCommand ? .18f : (float)Math.Max(0, .18 - dodgeElapsed); } }
        public float DeflectRemaining { get { return deflectCommand ? .14f : (float)Math.Max(0, .14 - deflectElapsed); } }
        public float DeflectFeedbackRemaining { get { return (float)Math.Max(0, deflectFeedback); } }
        public int LastDeflectedAttackId { get; private set; }
        public int LastDeflectedLane { get; private set; }
        public bool LastPerfect { get; private set; }
        public float PerfectFeedbackRemaining { get { return (float)Math.Max(0, perfectFeedback); } }
        public float StageRemaining { get { return (float)Math.Max(0, stageRemaining); } }
        public float CounterWindowRemaining { get { return StageName == "CounterWindow" ? StageRemaining : 0; } }
        public bool CounterAvailable { get { return StageName == "CounterWindow" && !counterUsed; } }
        public bool Paused { get; private set; }
        public bool Focused { get; private set; } = true;
        public float ReadDelayRemaining { get { return (float)Math.Max(0, readDelay); } }
        public bool SuspendedForBacklog { get; private set; }

        internal Combat3Encounter(CombatService combat, Combat3Practice? practice = null)
        {
            this.combat = combat;
            this.practice = practice;
            Attacks = attacks.AsReadOnly();
            Lane = 2;
            LanePosition = 2;
            BeginAttack();
        }

        internal bool Dodge()
        {
            if (!Running || readDelay > 0 || dodgeCommand || combat.DefenseCooldownRemaining > CombatTuning.Epsilon) return false;
            dodgeCommand = true;
            dodgeDirection = heldDirection;
            // The direction sampled on this down belongs to the dodge, not a second ordinary step.
            pendingDirection = QueuedDirection = 0;
            repeatElapsed = 0;
            LastPerfect = false;
            perfectFeedback = 0;
            combat.Combat3BeginDodge();
            return true;
        }

        internal bool Deflect()
        {
            if (!Running || readDelay > 0 || deflectCommand || combat.DefenseCooldownRemaining > CombatTuning.Epsilon) return false;
            deflectCommand = true;
            combat.Combat3BeginDeflect();
            return true;
        }

        internal bool SetInput(bool left, bool right, bool attack)
        {
            bool freshAttack = attack && !attackHeld && attackArmed;
            attackHeld = attack;
            if (!attack) attackArmed = true;
            if (!Running || readDelay > 0) return false;
            int direction = left == right ? 0 : left ? -1 : 1;
            if (left && right) pendingDirection = QueuedDirection = 0;
            if (direction != heldDirection)
            {
                if (direction != 0 && pendingDirection == 0) pendingDirection = direction;
                repeatElapsed = 0;
            }
            heldDirection = direction;
            // Eligibility is captured at down-time, so an early down cannot cross into a window.
            if (freshAttack && StageName == "CounterWindow" && !counterUsed) counterCommand = true;
            return true;
        }

        internal bool SetPaused(bool value)
        {
            if (Paused == value) return false;
            Paused = value;
            Suspend();
            return true;
        }

        internal bool SetFocused(bool value)
        {
            if (Focused == value) return false;
            Focused = value;
            Suspend();
            return true;
        }

        internal void ClearInput()
        {
            heldDirection = pendingDirection = QueuedDirection = 0;
            repeatElapsed = 0;
            counterCommand = false;
            dodgeCommand = false;
            dodgeDirection = 0;
            dodgeElapsed = .18;
            deflectCommand = false;
            deflectElapsed = .14;
            deflectFeedback = 0;
            LastDeflectedAttackId = LastDeflectedLane = 0;
            perfectUsed = LastPerfect = false;
            perfectFeedback = 0;
            // Require a sampled release after cancellation, including focus loss with no input samples.
            attackArmed = false;
            combat.CancelCombat3Reservation();
        }

        internal void Complete()
        {
            ClearInput();
            IsMoving = false;
            StageName = "Complete";
            foreach (var attack in attacks) attack.StateName = "Cleanup";
            stageRemaining = accumulator = 0;
        }

        private bool Running { get { return !Paused && Focused && combat.Combat3Fighting; } }
        private void Suspend()
        {
            ClearInput();
            accumulator = 0;
            readDelay = .6;
        }

        internal bool Tick(float seconds)
        {
            if (!Running || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return false;
            if (seconds + accumulator > .10000001)
            {
                SuspendedForBacklog = true;
                Suspend();
                return true;
            }
            if (readDelay > 0)
            {
                readDelay = Math.Max(0, readDelay - seconds);
                if (readDelay < .000001) { readDelay = 0; SuspendedForBacklog = false; }
                return true;
            }
            accumulator += seconds;
            while (accumulator + .00000001 >= StepSeconds && Running)
            {
                accumulator -= StepSeconds;
                AdvanceStep();
            }
            return true;
        }

        private void AdvanceStep()
        {
            combat.AdvanceCombat3Timers((float)StepSeconds);
            perfectFeedback = Math.Max(0, perfectFeedback - StepSeconds);
            deflectFeedback = Math.Max(0, deflectFeedback - StepSeconds);
            if (deflectCommand) { deflectCommand = false; deflectElapsed = 0; }
            if (dodgeCommand)
            {
                dodgeCommand = false;
                dodgeElapsed = 0;
                perfectUsed = false;
                if (dodgeDirection != 0)
                {
                    int target = Math.Max(0, Math.Min(4, Lane + dodgeDirection));
                    if (target != Lane) BeginMove(target);
                }
                dodgeDirection = 0;
            }
            if (counterCommand)
            {
                counterCommand = false;
                if (StageName == "CounterWindow" && !counterUsed)
                {
                    counterUsed = true;
                    combat.Combat3Counter();
                    if (!combat.Combat3Fighting) { Complete(); return; }
                }
            }
            if (pendingDirection != 0) { RequestMove(pendingDirection); pendingDirection = 0; }
            if (heldDirection != 0 && DodgeRemaining <= 0)
            {
                repeatElapsed += StepSeconds;
                if (repeatElapsed + .00000001 >= .18)
                {
                    repeatElapsed -= .18;
                    RequestMove(heldDirection);
                }
            }
            float previousPosition = LanePosition;
            AdvanceMovement();
            // Swept lane contact includes the segment's origin: an ordinary step is not a dodge.
            foreach (var attack in attacks)
            {
                bool overlaps = attack.KindName == "LowWave" ||
                    (Math.Min(previousPosition, LanePosition) <= attack.Lane + .25f &&
                     Math.Max(previousPosition, LanePosition) >= attack.Lane - .25f);
                if (StageName != "Active" || attack.StateName != "Active" || !overlaps ||
                    combat.DamageImmunityRemaining > CombatTuning.Epsilon) continue;
                if (dodgeElapsed < .18)
                {
                    attack.StateName = "Dodged";
                    if (!perfectUsed && dodgeElapsed < .08)
                    {
                        perfectUsed = LastPerfect = true;
                        perfectFeedback = .6;
                        combat.Combat3PerfectDodge();
                    }
                }
                else if (deflectElapsed < .14 && attack.IsDeflectable)
                {
                    attack.StateName = "Deflected";
                    LastDeflectedAttackId = attack.Id;
                    LastDeflectedLane = attack.Lane;
                    deflectFeedback = .6;
                    combat.Combat3DeflectContact();
                }
                else
                {
                    LastPerfect = false;
                    if (combat.Combat3DamagePlayer()) attack.StateName = "Hit";
                }
                if (!combat.Combat3Fighting) { Complete(); return; }
            }
            dodgeElapsed += StepSeconds;
            deflectElapsed += StepSeconds;
            foreach (var attack in attacks) attack.Advance(StepSeconds);
            stageRemaining -= StepSeconds;
            if (stageRemaining > .00000001) return;
            switch (StageName)
            {
                case "Telegraph": StageName = "Active"; stageRemaining = AttackKindName == "Projectile" ? .58 : .18; break;
                case "Active":
                    if (AttackStateName == "Active") AttackStateName = "Resolved";
                    StageName = "Recovery"; stageRemaining = .25; break;
                case "Recovery":
                    StageName = "CounterWindow";
                    foreach (var attack in attacks) attack.StateName = "Cleanup";
                    stageRemaining = 1.1; break;
                case "CounterWindow": BeginAttack(); break;
            }
        }

        private void RequestMove(int direction)
        {
            if (IsMoving) { if (QueuedDirection == 0) QueuedDirection = direction; return; }
            int target = Math.Max(0, Math.Min(4, Lane + direction));
            if (target == Lane) return;
            BeginMove(target);
        }

        private void BeginMove(int target)
        {
            moveOrigin = LanePosition;
            Lane = target;
            moveElapsed = 0;
            IsMoving = true;
        }

        private void AdvanceMovement()
        {
            if (!IsMoving) return;
            moveElapsed += StepSeconds;
            LanePosition = moveOrigin + (Lane - moveOrigin) * (float)Math.Min(1, moveElapsed / .12);
            if (moveElapsed + .00000001 < .12) return;
            LanePosition = Lane;
            IsMoving = false;
            int queued = QueuedDirection;
            QueuedDirection = 0;
            if (queued != 0) RequestMove(queued);
        }

        private void BeginAttack()
        {
            string kind = (practice ?? (Combat3Practice)(patternIndex++ % 3)).ToString();
            attacks.Clear();
            attacks.Add(new Combat3Attack(++nextAttackId, Lane, kind, .9));
            if (kind == "Projectile") attacks.Add(new Combat3Attack(++nextAttackId, Lane < 4 ? Lane + 1 : Lane - 1, kind, 1.3));
            StageName = "Telegraph";
            stageRemaining = .9;
            counterUsed = false;
        }
    }
}
