using System;
using System.Reflection;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Candidate
{
    /// <summary>
    /// Interactive Argon watch candidate adapter: mouse wheel on the genuine
    /// OLED (or physical crown), OLED state update, click confirm and Foley.
    /// Scene owner calls Initialize after instantiating original Argon prefab.
    /// Public Step and Confirm APIs also support gamepad / touch in V10 adapter.
    /// The scene's active Bed Sleep flow is NOT changed by this candidate.
    /// </summary>
    public sealed class ArgonWatchControls : MonoBehaviour
    {
        private Camera viewCamera;
        private Collider oledCollider;
        private Collider crownCollider;
        private Transform crown;
        private Quaternion crownRest;
        private Component presenter;
        private MethodInfo configureTime, setConfirmed;
        private MethodInfo showPresets, selectPreset, beginSleep, cancelSelection;
        private int currentHour=19, currentMinute=30;
        private int lastOledHour=-1, lastOledMinute=-1;
        private AudioSource audioSource;
        private AudioClip cloth, tick, confirmSound;
        private int steps = 14;
        private int minimumSteps = 2;
        private bool confirmed;
        private float crownTarget;
        private Component existingV10Router;
        private EventInfo stepEvent, absoluteEvent, confirmEvent, cancelEvent;
        private Action<int> stepHandler, absoluteHandler;
        private Action confirmHandler, cancelHandler;
        private bool oledInitialized;
        private int lastOledSteps;
        private bool lastOledConfirmed;
        public int SelectedHalfHours => steps;
        public bool IsConfirmed => confirmed;

        public void Initialize(Camera gameCamera, Component oledPresenter,
            MeshRenderer realOled, MeshRenderer realCrown)
        {
            if (gameCamera == null || oledPresenter == null || realOled == null ||
                realCrown == null)
                throw new ArgumentNullException("Argon watch needs camera, game OLED and physical watch");
            viewCamera = gameCamera;
            presenter = oledPresenter;
            var type = presenter.GetType();
            minimumSteps = presenter is ArgonRussianSleepOledPresenter ? 1 : 2;
            configureTime = type.GetMethod("ConfigureTime", BindingFlags.Public | BindingFlags.Instance);
            setConfirmed = type.GetMethod("SetConfirmedState", BindingFlags.Public | BindingFlags.Instance);
            if (configureTime == null || setConfirmed == null)
                throw new InvalidOperationException("ROKAS Bed Sleep OLED contract unavailable");
            // Additional candidate-only methods are optional; existing V10
            // production OLED remains compatible without any changes.
            showPresets=type.GetMethod("ShowPresets", BindingFlags.Public|BindingFlags.Instance);
            selectPreset=type.GetMethod("SelectPreset", BindingFlags.Public|BindingFlags.Instance);
            beginSleep=type.GetMethod("SetFallingAsleep", BindingFlags.Public|BindingFlags.Instance);
            cancelSelection=type.GetMethod("CancelSelection", BindingFlags.Public|BindingFlags.Instance);
            oledCollider = realOled.GetComponent<Collider>();
            if (oledCollider == null)
            {
                var face = realOled.GetComponent<MeshFilter>();
                var mesh = realOled.gameObject.AddComponent<MeshCollider>();
                mesh.sharedMesh = face.sharedMesh;
                oledCollider = mesh;
            }
            crownCollider = realCrown.GetComponent<Collider>();
            if (crownCollider == null)
            {
                // Readability of supplied Argon crown FBX triangles is not
                // guaranteed on all builds; a close-fitting original-mesh
                // bounds collider avoids unreliable MeshCollider imports.
                var filter = realCrown.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    throw new InvalidOperationException("Authentic watch crown mesh missing");
                var collider = realCrown.gameObject.AddComponent<BoxCollider>();
                collider.center = filter.sharedMesh.bounds.center;
                collider.size = filter.sharedMesh.bounds.size + Vector3.one * .002f;
                crownCollider = collider;
            }
            crown = realCrown.transform;
            crownRest = crown.localRotation;
            audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = .52f;
            cloth = ArgonWatchFoley.CreateClip(ArgonFoleyCue.Sleeve);
            tick = ArgonWatchFoley.CreateClip(ArgonFoleyCue.CrownTick);
            confirmSound = ArgonWatchFoley.CreateClip(ArgonFoleyCue.Confirmation);
            UpdateOled();
        }

        private void UpdateOled()
        {
            // The actual gameplay presenter redraws a full CPU 256x256 OLED
            // texture on every call. Prevent needless duplicate uploads and
            // reduce transient spikes during rapid dial scrolling.
            if (!oledInitialized || lastOledSteps != steps ||
                lastOledHour != currentHour || lastOledMinute != currentMinute)
                configureTime.Invoke(presenter, new object[] { currentHour, currentMinute, steps });
            if (!oledInitialized || lastOledConfirmed != confirmed)
                setConfirmed.Invoke(presenter, new object[] { confirmed });
            lastOledSteps = steps;
            lastOledConfirmed = confirmed;
            lastOledHour = currentHour;
            lastOledMinute = currentMinute;
            oledInitialized = true;
        }

        public void SetDurationSteps(int halfHours, bool withSound = false)
        {
            // Original V10 presenter enforces [2,24]; match the real
            // gameplay duration to avoid displaying a different selection.
            int next = Mathf.Clamp(halfHours, minimumSteps, 24);
            if (next == steps) return;
            int before = steps;
            steps = next;
            confirmed = false;
            crownTarget += Mathf.Sign(next - before) * 4f;
            if (withSound) Play(ArgonFoleyCue.CrownTick);
            UpdateOled();
        }

        /// <summary>
        /// Reuse existing V10 drag/ring, keyboard, wheel, confirm and cancel
        /// input routes rather than running a competing copy of mouse input.
        /// Bind only when integrating the candidate into the real bed flow.
        /// The V10 assembly is not referenced directly (candidate asmdef),
        /// so this remains isolated and safe against current production code.
        /// </summary>
        public void BindExistingV10InputRouter(Component router)
        {
            if (router == null) throw new ArgumentNullException(nameof(router));
            UnbindV10InputRouter();
            Type t = router.GetType();
            stepEvent = t.GetEvent("OnStepHalfHours");
            absoluteEvent = t.GetEvent("OnSetAbsoluteHalfHours");
            confirmEvent = t.GetEvent("OnConfirm");
            cancelEvent = t.GetEvent("OnCancel");
            if (stepEvent == null || absoluteEvent == null ||
                confirmEvent == null || cancelEvent == null ||
                stepEvent.EventHandlerType != typeof(Action<int>) ||
                absoluteEvent.EventHandlerType != typeof(Action<int>) ||
                confirmEvent.EventHandlerType != typeof(Action) ||
                cancelEvent.EventHandlerType != typeof(Action))
                throw new InvalidOperationException("Incompatible V10 watch input router events");
            existingV10Router = router;
            stepHandler = Step;
            absoluteHandler = value => SetDurationSteps(value, true);
            confirmHandler = Confirm;
            cancelHandler = ResetConfirmation;
            stepEvent.AddEventHandler(router, stepHandler);
            absoluteEvent.AddEventHandler(router, absoluteHandler);
            confirmEvent.AddEventHandler(router, confirmHandler);
            cancelEvent.AddEventHandler(router, cancelHandler);
        }

        public void UnbindV10InputRouter()
        {
            if (existingV10Router != null)
            {
                if (stepEvent != null && stepHandler != null)
                    stepEvent.RemoveEventHandler(existingV10Router, stepHandler);
                if (absoluteEvent != null && absoluteHandler != null)
                    absoluteEvent.RemoveEventHandler(existingV10Router, absoluteHandler);
                if (confirmEvent != null && confirmHandler != null)
                    confirmEvent.RemoveEventHandler(existingV10Router, confirmHandler);
                if (cancelEvent != null && cancelHandler != null)
                    cancelEvent.RemoveEventHandler(existingV10Router, cancelHandler);
            }
            existingV10Router = null;
            stepEvent = absoluteEvent = confirmEvent = cancelEvent = null;
            stepHandler = absoluteHandler = null;
            confirmHandler = cancelHandler = null;
        }

        /// <summary>
        /// Use actual game time when wiring this candidate to the ROKAS room.
        /// The 19:30 default above is only the deterministic preview clock.
        /// </summary>
        public void SetCurrentGameTime(int hour,int minute)
        {
            currentHour=(hour%24+24)%24;
            currentMinute=Mathf.Clamp(minute,0,59);
            UpdateOled();
        }

        public void ShowQuickPresets()
        {
            if(showPresets!=null)showPresets.Invoke(presenter,null);
        }

        public void SelectQuickPreset(int halfHours)
        {
            SetDurationSteps(halfHours,true);
            if(selectPreset!=null)
                selectPreset.Invoke(presenter,new object[]{steps});
        }

        public void BeginSleepingAnimation()
        {
            if(beginSleep!=null)beginSleep.Invoke(presenter,null);
        }

        public void CancelTimer()
        {
            confirmed=false;
            UpdateOled();
            if(cancelSelection!=null)cancelSelection.Invoke(presenter,null);
        }

        /// <summary>
        /// Physically hit-tested watch UV gestures: header opens presets,
        /// ring chooses duration, list entries choose quick durations,
        /// bottom button confirms. Touch callers can reuse this method.
        /// The watch UI is a REAL dynamic physical surface, not a static
        /// screenshot with an invisible full-screen confirmation button.
        /// </summary>
        public void ProcessWatchFaceTap(Vector2 uv)
        {
            if (!(presenter is ArgonRussianSleepOledPresenter russian))
            {
                Confirm(); // Unmodified legacy V10 behavior.
                return;
            }
            switch(russian.Screen)
            {
                case ArgonRussianWatchScreen.Presets:
                {
                    int row = Mathf.Clamp(Mathf.RoundToInt(
                        (.675f-uv.y)/.117f),0,4);
                    int[] choices={1,2,4,8,16};
                    SelectQuickPreset(choices[row]);
                    return;
                }
                case ArgonRussianWatchScreen.Confirmed:
                    BeginSleepingAnimation();return;
                case ArgonRussianWatchScreen.Cancelled:
                    SelectQuickPreset(steps);return;
                case ArgonRussianWatchScreen.FallingAsleep:
                    return;
            }
            if(uv.y>.76f){ShowQuickPresets();return;}
            if(uv.y<.20f){Confirm();return;}
            float dx=uv.x-.5f,dy=uv.y-.5f;
            float radius=Mathf.Sqrt(dx*dx+dy*dy);
            if(radius>.28f && radius<.50f)
            {
                float angle=Mathf.Atan2(dx,dy);
                float progress=Mathf.InverseLerp(-.73f*Mathf.PI,
                    .73f*Mathf.PI,angle);
                SetDurationSteps(Mathf.RoundToInt(
                    Mathf.Lerp(minimumSteps,24,progress)),true);
            }
        }

        public void Step(int direction)
        {
            if (direction == 0) return;
            int before = steps;
            SetDurationSteps(steps + Math.Sign(direction));
            if (steps != before)
            {
                crownTarget += Mathf.Sign(direction) * 11f;
                Play(ArgonFoleyCue.CrownTick);
            }
        }

        public void Confirm()
        {
            if (confirmed) return;
            confirmed = true;
            UpdateOled();
            Play(ArgonFoleyCue.Confirmation);
        }

        public void ResetConfirmation()
        {
            if (!confirmed) return;
            confirmed = false;
            UpdateOled();
        }

        public void SleeveMotion()
        {
            Play(ArgonFoleyCue.Sleeve);
        }

        private void Play(ArgonFoleyCue cue)
        {
            if (audioSource == null) return;
            var clip = cue == ArgonFoleyCue.Sleeve ? cloth :
                cue == ArgonFoleyCue.CrownTick ? tick : confirmSound;
            // EditMode QA mixes the very same samples into a WAV offline;
            // only real PlayMode drives an audible AudioSource.
            if (clip != null && Application.isPlaying) audioSource.PlayOneShot(clip);
        }

        public void AnimateCrown(float dt)
        {
            if (crown == null) return;
            crownTarget = Mathf.Lerp(crownTarget, 0f, Mathf.Clamp01(dt * 1.8f));
            crown.localRotation = Quaternion.Slerp(crown.localRotation,
                crownRest * Quaternion.AngleAxis(crownTarget, Vector3.up),
                Mathf.Clamp01(dt * 14f));
        }

        private void Update()
        {
            AnimateCrown(Time.deltaTime);
            if (existingV10Router != null) return;
#if ENABLE_LEGACY_INPUT_MANAGER
            if (viewCamera == null || oledCollider == null) return;
            Ray ray = viewCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit faceHit, crownHit;
            bool onFace = oledCollider.Raycast(ray, out faceHit, 15f);
            bool onCrown = crownCollider != null &&
                crownCollider.Raycast(ray, out crownHit, 15f);
            if (onFace || onCrown)
            {
                float wheel = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(wheel) > .001f) Step(wheel > 0 ? 1 : -1);
                if (Input.GetMouseButtonDown(0))
                {
                    if (onFace) ProcessWatchFaceTap(faceHit.textureCoord);
                    else Confirm();
                }
            }
#elif ENABLE_INPUT_SYSTEM
            // Unity 6 projects using the new Input System exclusively must
            // retain the exact same physical OLED/crown hit targets.
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (viewCamera == null || oledCollider == null || mouse == null) return;
            Ray ray = viewCamera.ScreenPointToRay(mouse.position.ReadValue());
            RaycastHit faceHit, crownHit;
            bool onFace = oledCollider.Raycast(ray, out faceHit, 15f);
            bool onCrown = crownCollider != null &&
                crownCollider.Raycast(ray, out crownHit, 15f);
            if (onFace || onCrown)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > .01f) Step(wheel > 0 ? 1 : -1);
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    if (onFace) ProcessWatchFaceTap(faceHit.textureCoord);
                    else Confirm();
                }
            }
#endif
        }

        private void OnDestroy()
        {
            UnbindV10InputRouter();
            if (Application.isPlaying)
            {
                if (cloth != null) Destroy(cloth);
                if (tick != null) Destroy(tick);
                if (confirmSound != null) Destroy(confirmSound);
            }
            else
            {
                if (cloth != null) DestroyImmediate(cloth);
                if (tick != null) DestroyImmediate(tick);
                if (confirmSound != null) DestroyImmediate(confirmSound);
            }
        }
    }
}
