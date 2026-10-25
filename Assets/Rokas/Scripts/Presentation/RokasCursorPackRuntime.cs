using System;
using System.Collections.Generic;
using System.IO;
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
        private GameObject cursorCanvas;
        private RectTransform softwareCursorRect;
        private RawImage softwareCursorImage;
        private bool overlayAllowed;
        private bool nativeShown = true;

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
                InitializeSoftwareCursor();
                Apply(HandCursor.Point);
                Debug.Log("[ROKAS CURSOR V16] Single 176px Keiko UI pointer ready; " +
                    "no OnGUI, no hardware double cursor.");
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

        private void InitializeSoftwareCursor()
        {
            // Render ONE pointer at full 176x176 resolution in the Game view.
            // No IMGUI painting and no small Windows hand at the same time.
            cursorCanvas = new GameObject("ROKAS_Keiko_Cursor_V16_OneCanvas",
                typeof(RectTransform), typeof(Canvas));
            cursorCanvas.transform.SetParent(transform, false);
            Canvas canvas = cursorCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32760;

            GameObject display = new GameObject("Keiko_Pointer_176_NoRaycast",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            display.transform.SetParent(cursorCanvas.transform, false);
            softwareCursorRect = display.GetComponent<RectTransform>();
            softwareCursorRect.anchorMin = Vector2.zero;
            softwareCursorRect.anchorMax = Vector2.zero;
            softwareCursorRect.pivot = Vector2.zero;
            softwareCursorRect.sizeDelta = new Vector2(176f, 176f);
            softwareCursorImage = display.GetComponent<RawImage>();
            softwareCursorImage.raycastTarget = false;
            softwareCursorImage.maskable = false;
            softwareCursorImage.enabled = false;

            // Restore native ARROW outside the game; hide it ONLY when
            // the in-game software hand is actually displayed.
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            nativeShown = Cursor.visible;
        }

        private void Update()
        {
            if (!packReady || !hasFocus) return;
            // Input/raycast only; final pointer coordinates are updated in
            // LateUpdate after scene UI/camera logic, immediately pre-render.
            overlayAllowed = IsGamePointerArea();
            if (!overlayAllowed) return;
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
            current = state;
            if (softwareCursorImage != null)
                softwareCursorImage.texture = textures[i];
            // Do not call Cursor.SetCursor here: Windows can downscale
            // hardware PNGs and briefly display a second small Keiko hand.
        }

        private bool IsGamePointerArea()
        {
            Vector3 p = Input.mousePosition;
            if (p.x < 0f || p.y < 0f ||
                p.x >= Screen.width || p.y >= Screen.height)
                return false;
#if UNITY_EDITOR
            // Unity Editor can continue to receive ticks while pointer is
            // over Profiler/Hierarchy. Show the OS arrow there instead.
            var over = UnityEditor.EditorWindow.mouseOverWindow;
            if (over != null && over.GetType().Name != "GameView")
                return false;
#endif
            return true;
        }

        private void SetNativeVisibility(bool visible)
        {
            if (nativeShown != visible || Cursor.visible != visible)
            {
                Cursor.visible = visible;
                nativeShown = visible;
            }
        }

        private void LateUpdate()
        {
            bool show = owner == this && packReady && hasFocus &&
                softwareCursorImage != null && current >= HandCursor.Point &&
                IsGamePointerArea();
            if (softwareCursorImage != null &&
                softwareCursorImage.enabled != show)
                softwareCursorImage.enabled = show;
            if (!show)
            {
                SetNativeVisibility(true);
                return;
            }

            // No artificial lerp or smoothing: direct fresh pointer position
            // avoids delay and visible drifting on slower rendering frames.
            Vector3 p = Input.mousePosition;
            int i = (int)current;
            Vector2 hot = Hotspots[i];
            Vector2 size = softwareCursorRect.sizeDelta;
            float x = p.x - hot.x * size.x / 176f;
            float y = p.y - (size.y - hot.y * size.y / 176f);
            Vector2 screenPixelPosition = new Vector2(x, y);
            if ((softwareCursorRect.anchoredPosition - screenPixelPosition)
                .sqrMagnitude > .0001f)
                softwareCursorRect.anchoredPosition = screenPixelPosition;
            SetNativeVisibility(false);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (owner != this) return;
            hasFocus = focused;
            overlayAllowed = false;
            if (softwareCursorImage != null)
                softwareCursorImage.enabled = false;
            SetNativeVisibility(true);
            if (!focused)
            {
                dragging = false;
                dragTarget = null;
                current = (HandCursor)(-1);
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
            else if (packReady)
                Apply(HandCursor.Point);
        }

        private void OnDisable()
        {
            if (owner != this) return;
            if (softwareCursorImage != null)
                softwareCursorImage.enabled = false;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            SetNativeVisibility(true);
            current = (HandCursor)(-1);
        }

        private void OnDestroy()
        {
            if (owner != this) return;
            owner = null;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            SetNativeVisibility(true);
            if (cursorCanvas != null) Destroy(cursorCanvas);
        }
    }
}
