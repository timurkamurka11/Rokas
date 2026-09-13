using System;

namespace Rokas.Core
{
    // Owns lane, attack lifetime and input timing only. HP, gauges and results remain in CombatService.
    public sealed class Combat3Encounter
    {
        private const double StepSeconds = 1.0 / 60.0;
        private readonly CombatService combat;
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

        public int Lane { get; private set; }
        public float LanePosition { get; private set; }
        public bool IsMoving { get; private set; }
        public int QueuedDirection { get; private set; }
        public string StageName { get; private set; }
        public int AttackId { get; private set; }
        public int AttackLane { get; private set; }
        public string AttackStateName { get; private set; }
        public float StageRemaining { get { return (float)Math.Max(0, stageRemaining); } }
        public float CounterWindowRemaining { get { return StageName == "CounterWindow" ? StageRemaining : 0; } }
        public bool CounterAvailable { get { return StageName == "CounterWindow" && !counterUsed; } }
        public bool Paused { get; private set; }
        public bool Focused { get; private set; } = true;
        public float ReadDelayRemaining { get { return (float)Math.Max(0, readDelay); } }
        public bool SuspendedForBacklog { get; private set; }

        internal Combat3Encounter(CombatService combat)
        {
            this.combat = combat;
            Lane = 2;
            LanePosition = 2;
            BeginHeavy();
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
            // Require a sampled release after cancellation, including focus loss with no input samples.
            attackArmed = false;
            combat.CancelCombat3Reservation();
        }

        internal void Complete()
        {
            ClearInput();
            IsMoving = false;
            StageName = "Complete";
            AttackStateName = "Cleanup";
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
            if (heldDirection != 0)
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
            if (StageName == "Active" && AttackStateName == "Active" &&
                Math.Min(previousPosition, LanePosition) <= AttackLane + .25f &&
                Math.Max(previousPosition, LanePosition) >= AttackLane - .25f)
            {
                if (combat.Combat3DamagePlayer()) AttackStateName = "Hit";
                if (!combat.Combat3Fighting) { Complete(); return; }
            }
            stageRemaining -= StepSeconds;
            if (stageRemaining > .00000001) return;
            switch (StageName)
            {
                case "Telegraph": StageName = AttackStateName = "Active"; stageRemaining = .18; break;
                case "Active":
                    if (AttackStateName == "Active") AttackStateName = "Resolved";
                    StageName = "Recovery"; stageRemaining = .25; break;
                case "Recovery": StageName = "CounterWindow"; AttackStateName = "Cleanup"; stageRemaining = 1.1; break;
                case "CounterWindow": BeginHeavy(); break;
            }
        }

        private void RequestMove(int direction)
        {
            if (IsMoving) { if (QueuedDirection == 0) QueuedDirection = direction; return; }
            int target = Math.Max(0, Math.Min(4, Lane + direction));
            if (target == Lane) return;
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

        private void BeginHeavy()
        {
            AttackId++;
            AttackLane = Lane;
            StageName = AttackStateName = "Telegraph";
            stageRemaining = .9;
            counterUsed = false;
        }
    }
}
