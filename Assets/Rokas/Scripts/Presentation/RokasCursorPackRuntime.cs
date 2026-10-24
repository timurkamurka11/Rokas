using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    /// <summary>
    /// Keiko's six authentic user-authored hand cursor images.
    /// The pack is presentation-only: never clicks, drags or changes gameplay.
    /// Uses original Unity EventSystem raycasts to respect overlays and buttons.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RokasCursorPackRuntime : MonoBehaviour
    {
        private enum HandCursor
        {
            Point = 0, Magic = 1, Press = 2,
            DragReady = 3, Dragging = 4, Forbidden = 5
        }

        private static readonly string[] ResourceNames =
        {
            "Rokas/Cursors/Keiko_Point",
            "Rokas/Cursors/Keiko_Magic",
            "Rokas/Cursors/Keiko_Press",
            "Rokas/Cursors/Keiko_OpenHand",
            "Rokas/Cursors/Keiko_Grab",
            "Rokas/Cursors/Keiko_Blocked"
        };

        // Each hotspot is located exactly at the active fingertip/contact.
        // These are normalized to the prepared 176x176 cursor PNGs.
        private static readonly Vector2[] Hotspots =
        {
            new Vector2(165, 53),
            new Vector2(165, 59),
            new Vector2(161, 62),
            new Vector2(152, 59),
            new Vector2(150, 70),
            new Vector2(159, 104)
        };

        private static RokasCursorPackRuntime owner;
        private readonly Texture2D[] textures = new Texture2D[6];
        private readonly List<RaycastResult> hits = new List<RaycastResult>(32);
        private PointerEventData pointer;
        private EventSystem boundEventSystem;
        private HandCursor current = (HandCursor)(-1);
        private bool packReady;
        private bool dragging;
        private GameObject dragTarget;
        private Vector2 pointerDownAt;
        private bool hasFocus = true;

        private void Awake()
        {
            // A previously installed Bootstrap hook may coexist with the
            // self-registering cursor pack. Keep ONE cursor owner only.
            if (owner != null && owner != this)
            {
                enabled = false;
                Destroy(this);
                return;
            }
            owner = this;
            bool foundAll = true;
            for (int i = 0; i < textures.Length; ++i)
            {
                textures[i] = Resources.Load<Texture2D>(ResourceNames[i]);
                if (textures[i] == null ||
                    textures[i].width != 176 || textures[i].height != 176)
                {
                    foundAll = false;
                    Debug.LogWarning("[ROKAS CURSOR] Missing/non-176 source: " +
                                     ResourceNames[i] + "; system cursor retained.");
                }
            }
            packReady = foundAll;
            if (packReady) Apply(HandCursor.Point);
        }

        private void Update()
        {
            if (!packReady || !hasFocus) return;
            if (EventSystem.current == null)
            {
                Apply(HandCursor.Point);
                return;
            }
            // Unity 6000.3: PointerEventData has no public eventSystem
            // property. Track the EventSystem used to construct it ourselves.
            EventSystem active = EventSystem.current;
            if (pointer == null || boundEventSystem != active)
            {
                boundEventSystem = active;
                pointer = new PointerEventData(active);
            }
            pointer.Reset();
            pointer.position = Input.mousePosition;
            hits.Clear();
            EventSystem.current.RaycastAll(pointer, hits);

            // Respect the foremost raycastable UI item. Transparent fullscreen
            // modal blockers correctly prevent using the interaction below.
            GameObject target = hits.Count > 0 ? hits[0].gameObject : null;
            Button button = target != null
                ? target.GetComponentInParent<Button>() : null;
            Selectable selectable = target != null
                ? target.GetComponentInParent<Selectable>() : null;
            GameObject draggableTarget = target != null
                ? ExecuteEvents.GetEventHandler<IDragHandler>(target) : null;
            bool isDragEnabled = draggableTarget != null;
            bool disabled = selectable != null &&
                (!selectable.IsInteractable() || !selectable.isActiveAndEnabled);

            if (Input.GetMouseButtonDown(0))
            {
                pointerDownAt = Input.mousePosition;
                dragTarget = isDragEnabled ? draggableTarget : null;
                dragging = false;
            }
            if (!Input.GetMouseButton(0))
            {
                dragging = false;
                dragTarget = null;
            }
            else if (dragTarget != null &&
                (new Vector2(Input.mousePosition.x, Input.mousePosition.y) -
                    pointerDownAt).sqrMagnitude > 36f)
            {
                dragging = true;
            }

            HandCursor next;
            if (dragging) next = HandCursor.Dragging;
            else if (disabled) next = HandCursor.Forbidden;
            else if (Input.GetMouseButton(0)) next = HandCursor.Press;
            else if (isDragEnabled) next = HandCursor.DragReady;
            else if (IsSpecialAction(target, button)) next = HandCursor.Magic;
            else next = HandCursor.Point;

            Apply(next);
        }

        // Keep specific and narrow: sleeping, entering portal, and confirming
        // are magic, while regular hover/UI uses the natural pointing hand.
        private static bool IsSpecialAction(GameObject target, Button button)
        {
            if (target == null || button == null ||
                !button.IsInteractable() || !button.isActiveAndEnabled)
                return false;
            string name = button.name;
            return Has(name, "BedInteractable") ||
                   Has(name, "Sleep") || Has(name, "Portal") ||
                   Has(name, "Confirm") || Has(name, "Submit") ||
                   Has(name, "SetAlarm") || Has(name, "PowerOn");
        }

        private static bool Has(string name, string part)
        {
            return name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void Apply(HandCursor state)
        {
            if (!packReady || state == current) return;
            int i = (int)state;
            if (i < 0 || i >= textures.Length || textures[i] == null) return;
            try
            {
                Cursor.SetCursor(textures[i], Hotspots[i], CursorMode.Auto);
                current = state;
            }
            catch (Exception ex)
            {
                // A faulty import never blocks input; restore system pointer.
                Debug.LogError("[ROKAS CURSOR] Unity cursor import failed: " + ex.Message);
                packReady = false;
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (owner != this) return;
            hasFocus = focused;
            if (!focused)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                current = (HandCursor)(-1);
            }
        }
        private void OnDisable()
        {
            if (owner != this) return;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            current = (HandCursor)(-1);
        }
        private void OnDestroy()
        {
            if (owner != this) return;
            owner = null;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }
}
