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

        public void Initialize(GameSession value, Func<bool> isPaused)
        {
            session = value;
            paused = isPaused;
        }

        public void Sample(bool leftHeld, bool rightHeld, bool attackHeld)
        {
            left = leftHeld;
            right = rightHeld;
            if (session == null) return;
            if (paused()) session.SetCombat3Paused(true);
            session.SetCombat3Input(left, right, attackHeld);
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) Sample(left, right, true);
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) Sample(left, right, false);
        }

        public void CancelInput()
        {
            left = right = false;
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
