using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    /// <summary>
    /// Keiko's six authentic hand cursors with proven IMGUI rendering and a
    /// visible native arrow fallback. The cursor artwork is always 176px.
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
        private bool guiCursorAllowed;
        private int lastGuiDrawnFrame = -10000;
        private bool hardwareShown = true;

        private void Awake()
        {
            // Keep one owner per loaded scene. OnDisable releases ownership
            // so a new scene does not inherit a dead/stale cursor controller.
            if (owner != null && owner != this)
            {
                enabled = false;
                Destroy(this);
                return;
            }
            owner = this;
            bool foundAll = true;
            for (int i=0; i<textures.Length; ++i)
            {
                textures[i] = LoadAuthenticCursor(ResourceNames[i]);
                if (textures[i] == null)
                {
                    foundAll = false;
                    Debug.LogError("[ROKAS CURSOR V9] Missing authentic Keiko hand image: " + ResourceNames[i]);
                }
            }
            packReady = foundAll;
            if (packReady)
            {
                Apply(HandCursor.Point);
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                Cursor.visible = true;
                Debug.Log("[ROKAS CURSOR V16.2] 176px IMGUI Keiko hand ready; " +
                    "the Windows arrow remains a guaranteed fallback.");
            }
            else
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                Cursor.visible = true;
            }
        }

        private static Texture2D LoadAuthenticCursor(string resource)
        {
            var tex = Resources.Load<Texture2D>(resource);
            if (tex != null && tex.width > 0 && tex.height > 0) return tex;
#if UNITY_EDITOR
            string full = Path.Combine(Application.dataPath, "Resources",
                resource.Replace('/', Path.DirectorySeparatorChar) + ".png");
            if (File.Exists(full))
            {
                try {
                    var loaded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (ImageConversion.LoadImage(loaded, File.ReadAllBytes(full)))
                        return loaded;
                    UnityEngine.Object.Destroy(loaded);
                }
                catch (Exception ex) {
                    Debug.LogWarning("[ROKAS CURSOR V9] PNG load failed: " + ex.Message);
                }
            }
#endif
            return null;
        }

        private void SetNativeVisible(bool visible)
        {
            // Only change the OS cursor when the state actually changes.
            if (hardwareShown != visible || Cursor.visible != visible)
            {
                Cursor.visible = visible;
                hardwareShown = visible;
            }
        }

        private void Update()
        {
            if (!packReady || !hasFocus)
            {
                SetNativeVisible(true);
                return;
            }
            guiCursorAllowed = true;
#if UNITY_EDITOR
            var over = UnityEditor.EditorWindow.mouseOverWindow;
            if (over != null && over.GetType().Name != "GameView")
                guiCursorAllowed = false;
#endif
            // Never hide Windows until a real IMGUI repaint succeeded.
            // If the Game view stops repainting or focus is lost, return
            // immediately to a visible native pointer (no invisible cursor).
            bool painted = lastGuiDrawnFrame >= Time.frameCount - 2;
            SetNativeVisible(!(guiCursorAllowed && painted));
            if (!guiCursorAllowed) return;
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
            int index = (int)state;
            if (index < 0 || index >= textures.Length || textures[index] == null)
                return;
            current = state;
            // Rendering stays exclusively in the proven 176px OnGUI path.
            // No second, smaller hardware Keiko cursor is created.
        }

        private void OnGUI()
        {
            if (!packReady || !hasFocus || !guiCursorAllowed ||
                Event.current == null || Event.current.type != EventType.Repaint) return;
            int i = (int)current;
            if (i < 0 || i >= textures.Length || textures[i] == null) return;
            var mouse = Input.mousePosition;
            if (mouse.x < 0 || mouse.y < 0 ||
                mouse.x > Screen.width || mouse.y > Screen.height) {
                lastGuiDrawnFrame = -10000;
                SetNativeVisible(true);
                return;
            }
            GUI.depth = -10000;
            GUI.color = Color.white;
            const float cursorSize = 176f;
            GUI.DrawTexture(new Rect(mouse.x-Hotspots[i].x,
                Screen.height-mouse.y-Hotspots[i].y,
                cursorSize, cursorSize), textures[i], ScaleMode.ScaleToFit, true);
            lastGuiDrawnFrame = Time.frameCount;
            SetNativeVisible(false);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (owner != this) return;
            hasFocus = focused;
            lastGuiDrawnFrame = -10000;
            SetNativeVisible(true);
            if (!focused)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                Cursor.visible = true;
                current = (HandCursor)(-1);
            }
        }
        private void OnDisable()
        {
            if (owner != this) return;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            lastGuiDrawnFrame = -10000;
            SetNativeVisible(true);
            current = (HandCursor)(-1);
            if (owner == this) owner = null;
        }
        private void OnDestroy()
        {
            if (owner != this) return;
            owner = null;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            SetNativeVisible(true);
        }
    }
}
