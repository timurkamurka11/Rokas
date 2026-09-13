using System;
using Rokas.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rokas.Presentation
{
    public sealed class Combat3InputSurface : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private GameSession session;
        private Func<bool> paused;
        private bool left;
        private bool right;
        private bool arenaPress;
        private bool dodgePending;
        private int rawDodgeFrame = -1;
        private int pointerDodgeFrame = -1;

        public void Initialize(GameSession value, Func<bool> isPaused)
        {
            session = value;
            paused = isPaused;
        }

        public void Sample(bool leftHeld, bool rightHeld, bool attackHeld)
        {
            left = leftHeld;
            right = rightHeld;
            if (!attackHeld) arenaPress = false;
            if (session == null) return;
            if (paused()) session.SetCombat3Paused(true);
            session.SetCombat3Input(left, right, arenaPress && attackHeld);
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left)
            {
                arenaPress = true;
                Sample(left, right, true);
            }
            else if (data.button == PointerEventData.InputButton.Right)
            {
                pointerDodgeFrame = Time.frameCount;
                if (rawDodgeFrame != Time.frameCount && session != null && !paused()) dodgePending = true;
            }
        }

        public void RequestDodge()
        {
            rawDodgeFrame = Time.frameCount;
            if (pointerDodgeFrame != Time.frameCount && session != null && !paused()) dodgePending = true;
        }

        public void FlushCommands()
        {
            // Merge pointer/raw down, after this frame's direction sample and before domain contact.
            bool dodge = dodgePending;
            dodgePending = false;
            if (dodge && session != null && !paused()) session.Dodge();
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) Sample(left, right, false);
        }

        public void CancelInput()
        {
            left = right = false;
            dodgePending = false;
            session?.CancelCombatInput();
        }

        private void OnDisable()
        {
            CancelInput();
            session?.SetCombat3Paused(true);
        }

        private void OnEnable()
        {
            if (session != null) session.SetCombat3Paused(paused());
        }
    }
}
