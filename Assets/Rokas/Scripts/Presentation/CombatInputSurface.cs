using System;
using Rokas.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Rokas.Presentation
{
    // One pointer owner prevents Button click + held release from producing two attacks.
    public sealed class CombatInputSurface : Button, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        private GameSession session;
        private Func<bool> blocked;
        private RectTransform[] points;
        private bool held;
        public void Configure(GameSession value, Func<bool> isBlocked, RectTransform[] ritualPoints)
        { session = value; blocked = isBlocked; points = ritualPoints; transition = Transition.None; navigation = new Navigation { mode = Navigation.Mode.None }; }
        private bool CanInput { get { return session != null && IsInteractable() && isActiveAndEnabled && !blocked(); } }
        public override void OnPointerDown(PointerEventData e)
        {
            if (!CanInput) { CancelGesture(); return; }
            if (e.button == PointerEventData.InputButton.Right) { session.Dodge(); return; }
            if (e.button != PointerEventData.InputButton.Left) return;
            held = session.BeginAttack();
            if (held) Trace(e);
        }
        public override void OnPointerUp(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || !held) return;
            if (!CanInput) { CancelGesture(); return; }
            Trace(e);
            held = false;
            session.ReleaseAttack();
        }
        public override void OnPointerExit(PointerEventData e)
        {
            base.OnPointerExit(e);
            CancelGesture();
        }
        public override void OnPointerClick(PointerEventData e) { }
        public override void OnSubmit(BaseEventData e) { }
        public void OnBeginDrag(PointerEventData e) { OnDrag(e); }
        public void OnDrag(PointerEventData e)
        {
            if (!held) return;
            if (!CanInput) { CancelGesture(); return; }
            Trace(e);
        }
        public void OnEndDrag(PointerEventData e) { OnPointerUp(e); }
        private void Trace(PointerEventData e)
        {
            if (session.Combat.Stage != CombatStage.Ritual || points == null) return;
            for (int i = 0; i < points.Length; i++)
                if (points[i] && RectTransformUtility.RectangleContainsScreenPoint(points[i], e.position, e.pressEventCamera))
                { session.TraceRitualPoint(i); return; }
        }
        public void CancelGesture() { held = false; if (session != null) session.CancelCombatInput(); }
        protected override void OnDisable() { CancelGesture(); base.OnDisable(); }
    }
}
