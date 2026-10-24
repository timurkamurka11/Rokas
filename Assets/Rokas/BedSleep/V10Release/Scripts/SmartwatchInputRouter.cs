using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Rokas.BedSleep.V10Release
{
    /// <summary>
    /// V6 Interactive Smartwatch Input Router.
    /// V6-07 Resolution: Supports both Unity New Input System (UnityEngine.InputSystem.Keyboard / Mouse)
    /// and Legacy Input Manager (UnityEngine.Input), plus direct programmatic raycast testing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SmartwatchInputRouter : MonoBehaviour
    {
        public bool Interactive { get; set; }
        public event Action<int> OnStepHalfHours;
        public event Action<int> OnSetAbsoluteHalfHours;
        public event Action OnConfirm;
        public event Action OnCancel;
        // Physical Argon OLED taps are routed to the actual Russian screen
        // controller, not interpreted as legacy V10 confirm-only clicks.
        public event Action<Vector2> OnPhysicalOledTap;
        public event Action OnCycleOledScreen;
        public bool UseArgonFaceNavigation { get; set; }
        public void ConfirmFromPhysicalOled() => OnConfirm?.Invoke();
        public void SetHalfHoursFromPhysicalOled(int halfHours) =>
            OnSetAbsoluteHalfHours?.Invoke(Mathf.Clamp(halfHours, 2, 24));

        private Camera _presentationCamera;
        private MeshCollider _oledCollider;
        private RectTransform _compositeRect;
        private bool _isDragging;
        private Vector2 _lastDragScreenPos;
        private float _dragAccumulatedPixels;
        private float _lastStepTimestamp = -10f;

        public void ConfigureRouter(Camera presentationCamera, MeshCollider oledCollider, RectTransform compositeRect)
        {
            _presentationCamera = presentationCamera;
            _oledCollider = oledCollider;
            _compositeRect = compositeRect;
        }

        public bool TryApplyStepOncePerFrame(int stepDir)
        {
            EmitSingleStep(stepDir);
            return true;
        }

        public void TickRouter()
        {
            if (!Interactive) return;

            if (WasCancelPressed()) { OnCancel?.Invoke(); return; }
            if (UseArgonFaceNavigation && WasCycleScreenPressed())
            {
                OnCycleOledScreen?.Invoke();
                return;
            }
            if (WasConfirmKeyPressed()) { OnConfirm?.Invoke(); return; }

            if (WasIncrementKeyPressed()) { EmitSingleStep(1); return; }
            if (WasDecrementKeyPressed()) { EmitSingleStep(-1); return; }

            float scroll = ReadScrollDeltaY();
            if (Mathf.Abs(scroll) >= 0.05f) { EmitSingleStep(scroll > 0f ? 1 : -1); return; }

            Vector2 pointerPos = ReadPointerPosition();
            if (WasPrimaryPointerPressedThisFrame())
            {
                if (TryRaycastOledUV(pointerPos, out Vector2 uv))
                {
                    if (UseArgonFaceNavigation && OnPhysicalOledTap != null)
                    {
                        OnPhysicalOledTap.Invoke(uv);
                        // Retain direct swipe/drag dial adjustment, but only
                        // after an actual hit on the physical watch face.
                        Vector2 c = uv - new Vector2(.5f, .52f);
                        _isDragging = c.magnitude >= .26f && c.magnitude <= .46f;
                        _lastDragScreenPos = pointerPos;
                        _dragAccumulatedPixels = 0f;
                        return;
                    }
                    if (uv.y <= 0.24f && uv.x >= 0.20f && uv.x <= 0.80f) { OnConfirm?.Invoke(); return; }
                    Vector2 centered = uv - new Vector2(0.5f, 0.52f);
                    float r = centered.magnitude;
                    if (r >= 0.26f && r <= 0.46f)
                    {
                        float angle = Mathf.Atan2(centered.y, centered.x);
                        float norm = Mathf.Clamp01(Mathf.InverseLerp(Mathf.PI * 0.75f, -Mathf.PI * 0.75f, angle));
                        int targetHalfHours = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(2f, 24f, norm)), 2, 24);
                        OnSetAbsoluteHalfHours?.Invoke(targetHalfHours);
                        return;
                    }
                    // Hit confirmed. Only a pointer that started ON the actual
                    // OLED surface is allowed to drag the dial.
                    _isDragging = true;
                    _lastDragScreenPos = pointerPos;
                    _dragAccumulatedPixels = 0f;
                }
                else
                {
                    _isDragging = false;
                    _dragAccumulatedPixels = 0f;
                }
            }
            else if (IsPrimaryPointerHeld() && _isDragging)
            {
                _dragAccumulatedPixels += (pointerPos.y - _lastDragScreenPos.y) + (pointerPos.x - _lastDragScreenPos.x) * 0.35f;
                _lastDragScreenPos = pointerPos;
                if (Mathf.Abs(_dragAccumulatedPixels) >= 18f)
                {
                    int dir = _dragAccumulatedPixels > 0f ? 1 : -1;
                    _dragAccumulatedPixels -= dir * 18f;
                    EmitSingleStep(dir);
                }
            }
            else if (WasPrimaryPointerReleasedThisFrame())
            {
                _isDragging = false;
            }
        }

        private static bool WasCycleScreenPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Tab)) return true;
#endif
            return false;
        }

        private static bool WasCancelPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) return true;
#endif
            return false;
        }

        private static bool WasConfirmKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && (
                Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.numpadEnterKey.wasPressedThisFrame ||
                Keyboard.current.spaceKey.wasPressedThisFrame)) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) return true;
#endif
            return false;
        }

        private static bool WasIncrementKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame)) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.RightArrow)) return true;
#endif
            return false;
        }

        private static bool WasDecrementKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.LeftArrow)) return true;
#endif
            return false;
        }

        private static float ReadScrollDeltaY()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                float s = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(s) > 0.001f) return s;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mouseScrollDelta.y;
#else
            return 0f;
#endif
        }

        private static Vector2 ReadPointerPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) return Mouse.current.position.ReadValue();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return Vector2.zero;
#endif
        }

        private static bool WasPrimaryPointerPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0)) return true;
#endif
            return false;
        }

        private static bool IsPrimaryPointerHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButton(0)) return true;
#endif
            return false;
        }

        private static bool WasPrimaryPointerReleasedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonUp(0)) return true;
#endif
            return false;
        }

        public bool TryRaycastOledUV(Vector2 screenPos, out Vector2 hitUv)
        {
            hitUv = Vector2.zero;
            if (_presentationCamera == null || _oledCollider == null) return false;

            Vector2 vp = new Vector2(
                screenPos.x / Mathf.Max(1f, Screen.width),
                screenPos.y / Mathf.Max(1f, Screen.height));

            Canvas parentCanvas = _compositeRect != null
                ? _compositeRect.GetComponentInParent<Canvas>() : null;
            Camera eventCamera = parentCanvas != null &&
                parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? (parentCanvas.worldCamera != null ? parentCanvas.worldCamera : Camera.main)
                : null;
            if (_compositeRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _compositeRect, screenPos, eventCamera, out Vector2 localPt))
            {
                Rect r = _compositeRect.rect;
                if (r.width > 1f && r.height > 1f)
                {
                    vp = new Vector2(
                        (localPt.x - r.xMin) / r.width,
                        (localPt.y - r.yMin) / r.height);
                }
            }

            // Do not clamp offscreen cursor to edge: that could spuriously
            // hit the watch while the user clicks elsewhere in the hub.
            if (vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f)
                return false;
            Ray ray = _presentationCamera.ViewportPointToRay(new Vector3(vp.x, vp.y, 0f));
            if (_oledCollider.Raycast(ray, out RaycastHit hit, 2.5f))
            {
                hitUv = hit.textureCoord;
                return true;
            }
            return false;
        }

        private void EmitSingleStep(int stepDir)
        {
            float now = Time.unscaledTime;
            if (now - _lastStepTimestamp < 0.025f) return;
            _lastStepTimestamp = now;
            OnStepHalfHours?.Invoke(stepDir >= 0 ? 1 : -1);
        }
    }
}
