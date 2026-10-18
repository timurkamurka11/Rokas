using System;
using UnityEngine;

namespace Rokas.BedSleep.V10Release
{
    /// <summary>
    /// V6 Production Isolated 3D Visual Composite.
    /// Instantiates the authoritative PF_Keiko_FPS_Arm and PF_ROKAS_Smartwatch prefabs and provides
    /// 3D-to-UI projection for the dedicated Watch_OLED_Screen_Mesh surface.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BedSleepVisualComposite : MonoBehaviour
    {
        // Framing is computed from genuine imported mesh bounds, not the old
        // fixed V9 camera offset, which could leave the whole watch outside FOV.
        private bool _hasReportedCameraFraming;
        private bool _argonFullVisualActive;
        private MeshRenderer _argonOledRenderer;
        private MeshCollider _argonOledCollider;
        private RokasArgonFullHandFollower _argonHand;
        private Light _argonWarmKey, _argonSoftFill;

        public int renderWidth = 1920;
        public int renderHeight = 1080;
        public float cameraFov = 28.0f;

        [SerializeField] private Camera _presentationCamera;
        [SerializeField] private Light _warmKeyLight;
        [SerializeField] private Light _coolRimLight;
        [SerializeField] private GameObject _armPrefab;
        [SerializeField] private GameObject _watchPrefab;

        public Camera PresentationCamera => _presentationCamera;
        public RenderTexture CompositeRenderTexture { get; private set; }
        public KeikoFPSArmRigController ArmRig { get; private set; }
        public Smartwatch3DController Watch { get; private set; }
        public SmartwatchOLEDPresenter Oled { get; private set; }
        public SmartwatchInputRouter InputRouter { get; private set; }
        public BedSleepSFXController Sfx { get; private set; }

        public void InitializeComposite(RectTransform targetUiRect = null)
        {
            if (CompositeRenderTexture != null && ArmRig != null) return;

            // Close-up UI previously cropped a 1920px composite 2.1x,
            // making the watch text look soft even with 1024px OLED source.
            // Render the SAME original camera at 1.5x linear pixel density;
            // never change its FOV, pose, 3D lighting or body animation.
            renderWidth = Mathf.Max(renderWidth, 2880);
            renderHeight = Mathf.Max(renderHeight, 1620);
            transform.position = new Vector3(0f, -50f, 0f);
            transform.rotation = Quaternion.identity;

            CompositeRenderTexture = new RenderTexture(renderWidth, renderHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "RT_ROKAS_BedSleep_3DComposite_V6",
                antiAliasing = 4
            };
            CompositeRenderTexture.Create();

            if (_presentationCamera == null)
            {
                Transform camTr = transform.Find("PresentationCamera_V6");
                if (camTr != null) _presentationCamera = camTr.GetComponent<Camera>();
                if (_presentationCamera == null)
                {
                    GameObject camGo = new GameObject("PresentationCamera_V6");
                    camGo.transform.SetParent(transform, false);
                    camGo.transform.localPosition = new Vector3(0f, 0f, -0.55f);
                    camGo.transform.localRotation = Quaternion.identity;
                    _presentationCamera = camGo.AddComponent<Camera>();
                }
            }
            _presentationCamera.clearFlags = CameraClearFlags.SolidColor;
            _presentationCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _presentationCamera.orthographic = false;
            _presentationCamera.fieldOfView = cameraFov;
            _presentationCamera.nearClipPlane = 0.05f;
            _presentationCamera.farClipPlane = 4.0f;
            _presentationCamera.targetTexture = CompositeRenderTexture;
            // A presentation camera must never render the whole 2D hub or
            // alter Camera.main; render only the isolated imported hand/watch.
            _presentationCamera.cullingMask = 1 << 30;

            ArmRig = GetComponentInChildren<KeikoFPSArmRigController>();
            if (ArmRig == null)
            {
                GameObject armAsset = _armPrefab ?? Resources.Load<GameObject>("BedSleep/Prefabs/PF_Keiko_FPS_Arm");
                if (armAsset == null)
                    throw new MissingReferenceException("ROKAS BedSleep V7: real skinned arm Prefab is missing. Refusing placeholder geometry.");
                GameObject armInstance = Instantiate(armAsset, transform, false);
                AssignIsolatedLayer(armInstance.transform);
                ArmRig = armInstance.GetComponent<KeikoFPSArmRigController>();
                if (ArmRig == null || armInstance.GetComponentInChildren<SkinnedMeshRenderer>() == null)
                    throw new MissingReferenceException("ROKAS BedSleep V7: arm Prefab has no rig/SkinnedMeshRenderer.");
            }
            ArmRig.InitializeRig();

            Oled = GetComponent<SmartwatchOLEDPresenter>() ?? gameObject.AddComponent<SmartwatchOLEDPresenter>();
            Oled.InitializeOled();

            Watch = GetComponentInChildren<Smartwatch3DController>();
            if (Watch == null)
            {
                GameObject watchAsset = _watchPrefab ?? Resources.Load<GameObject>("BedSleep/Prefabs/PF_ROKAS_Smartwatch");
                if (watchAsset == null)
                    throw new MissingReferenceException("ROKAS BedSleep V7: seven-part 3D smartwatch Prefab is missing.");
                GameObject watchInstance = Instantiate(watchAsset, transform, false);
                AssignIsolatedLayer(watchInstance.transform);
                Watch = watchInstance.GetComponent<Smartwatch3DController>();
                if (Watch == null)
                    throw new MissingReferenceException("ROKAS BedSleep V7: smartwatch Prefab is missing its controller.");
            }
            Watch.InitializeWatch(ArmRig.WatchSocket, Oled.OledRenderTexture);
            NormalizeImportedPhysicalScale();
            FrameImportedArmAndWatch();
            // Imported OLED is physically recessed under the titanium case:
            // source pixels and UVs are correct but the watch shell occludes
            // the entire screen. Seat the ACTUAL mesh outside the face and
            // re-frame its new physical position; no substitute UI or watch.
            Watch.SeatOledAboveBezel(_presentationCamera);
            FrameImportedArmAndWatch();

            // The existing bed click is the only entry point. Replace the WHOLE
            // visual set with the authentic Argon hand, sleeve and premium watch,
            // not the old V10 watch with an Argon skin graft.
            Sfx = GetComponent<BedSleepSFXController>();
            if (Sfx == null) Sfx = gameObject.AddComponent<BedSleepSFXController>();
            Sfx.InitializeAudio();
            _argonFullVisualActive = RokasArgonConnectedHandRuntime.TryInstall(
                ArmRig, Watch, Oled, _presentationCamera, Sfx,
                out _argonOledRenderer, out _argonOledCollider);
            if (_argonFullVisualActive)
            {
                _argonHand = GetComponentInChildren<RokasArgonFullHandFollower>(true);
                ConfigureArgonGameLighting();
            }

            InputRouter = GetComponent<SmartwatchInputRouter>();
            if (InputRouter == null) InputRouter = gameObject.AddComponent<SmartwatchInputRouter>();
            InputRouter.ConfigureRouter(_presentationCamera,
                _argonFullVisualActive ? _argonOledCollider : Watch.OledMeshCollider,
                targetUiRect);
            if (_argonFullVisualActive)
            {
                // Existing gameplay handlers retain the timer/save state.
                // These extra listeners provide Foley only, never a second
                // gameplay input/confirmation handler.
                InputRouter.OnStepHalfHours += OnArgonCrownStep;
                InputRouter.OnSetAbsoluteHalfHours += OnArgonCrownAbsolute;
                InputRouter.OnConfirm += OnArgonWatchConfirm;
                InputRouter.UseArgonFaceNavigation = true;
                InputRouter.OnPhysicalOledTap += OnArgonPhysicalOledTap;
                InputRouter.OnCycleOledScreen += OnArgonCycleOledScreen;
            }
        }

        private void OnArgonPhysicalOledTap(Vector2 uv)
        {
            NavigatePhysicalOledTap(uv,
                InputRouter.SetHalfHoursFromPhysicalOled,
                InputRouter.ConfirmFromPhysicalOled);
        }

        // Called both by the existing native BedSleep UI and (when enabled)
        // the dedicated 3D pointer. Single gameplay owner for each event.
        public bool NavigatePhysicalOledTap(Vector2 uv,
            Action<int> applyHalfHours, Action confirm)
        {
            if (!_argonFullVisualActive || _argonHand == null) return false;
            var oled = _argonHand.WatchScreen;
            if (oled == null) return false;
            _argonHand.NotifyWatchInteraction();
            switch (oled.Screen)
            {
                case Rokas.BedSleep.Argon.Candidate.ArgonRussianWatchScreen.Presets:
                {
                    // Five physical watch rows. V10 gameplay permits 1..12h
                    // in half-hour increments, so never display a preset that
                    // the gameplay clock silently rejects.
                    int index = Mathf.Clamp(Mathf.RoundToInt(
                        (.675f - uv.y) / .117f), 0, 4);
                    int[] presets = { 2, 4, 8, 12, 16 };
                    int choice = presets[index];
                    applyHalfHours?.Invoke(choice);
                    oled.SelectPreset(choice);
                    return true;
                }
                case Rokas.BedSleep.Argon.Candidate.ArgonRussianWatchScreen.Overview:
                    if (uv.y > .76f) oled.ShowDial();
                    else if (uv.y < .20f) confirm?.Invoke();
                    else oled.ShowRelaxation();
                    return true;
                case Rokas.BedSleep.Argon.Candidate.ArgonRussianWatchScreen.Relaxation:
                    if (uv.y > .76f) oled.ShowOverview();
                    else if (uv.y < .20f) confirm?.Invoke();
                    else oled.ShowDial();
                    return true;
                case Rokas.BedSleep.Argon.Candidate.ArgonRussianWatchScreen.Confirmed:
                    oled.SetFallingAsleep();
                    return true;
                case Rokas.BedSleep.Argon.Candidate.ArgonRussianWatchScreen.Cancelled:
                    oled.ShowDial();
                    return true;
                case Rokas.BedSleep.Argon.Candidate.ArgonRussianWatchScreen.FallingAsleep:
                    return true;
            }
            // DIAL: top text opens physical watch presets; large central
            // sleep-time number opens plan screens; bottom confirms in
            // existing game flow; touch ring sets a real duration.
            if (uv.y > .76f) { oled.ShowPresets(); return true; }
            if (uv.y < .20f) { confirm?.Invoke(); return true; }
            Vector2 delta = uv - new Vector2(.5f, .5f);
            if (Mathf.Abs(delta.x) < .21f && Mathf.Abs(delta.y) < .19f)
            {
                oled.ShowOverview();
                return true;
            }
            float radius = delta.magnitude;
            if (radius >= .26f && radius <= .50f)
            {
                float angle = Mathf.Atan2(delta.x, delta.y);
                float progress = Mathf.InverseLerp(-.73f * Mathf.PI,
                    .73f * Mathf.PI, angle);
                int steps = Mathf.RoundToInt(Mathf.Lerp(2f, 24f, progress));
                applyHalfHours?.Invoke(steps);
            }
            return true;
        }

        private void OnArgonCycleOledScreen()
        {
            CyclePhysicalOledScreen();
        }

        public int GetPhysicalOledTab()
        {
            if (!_argonFullVisualActive || _argonHand == null ||
                _argonHand.WatchScreen == null) return 0;
            switch (_argonHand.WatchScreen.Screen)
            {
                case Rokas.BedSleep.Argon.Candidate.ArgonRussianWatchScreen.Presets:
                    return 1;
                case Rokas.BedSleep.Argon.Candidate.ArgonRussianWatchScreen.Alarm:
                case Rokas.BedSleep.Argon.Candidate.ArgonRussianWatchScreen.Overview:
                    return 2;
                case Rokas.BedSleep.Argon.Candidate.ArgonRussianWatchScreen.Relaxation:
                    return 3;
                case Rokas.BedSleep.Argon.Candidate.ArgonRussianWatchScreen.Phases:
                    return 4;
                default: return 0;
            }
        }

        public bool SelectPhysicalOledTab(int tab)
        {
            if (!_argonFullVisualActive || _argonHand == null) return false;
            var oled = _argonHand.WatchScreen;
            if (oled == null) return false;
            _argonHand.NotifyWatchInteraction();
            switch (tab)
            {
                case 0: oled.ShowDial(); break;
                case 1: oled.ShowPresets(); break;
                case 2: oled.ShowAlarm(); break;
                case 3: oled.ShowRelaxation(); break;
                case 4: oled.ShowPhases(); break;
                default: return false;
            }
            return true;
        }

        public bool CyclePhysicalOledScreen()
        {
            if (!_argonFullVisualActive) return false;
            return SelectPhysicalOledTab((GetPhysicalOledTab() + 1) % 5);
        }

        private void OnArgonCrownStep(int direction)
        {
            if (direction != 0) Sfx?.PlayCrownClick();
        }
        private void OnArgonCrownAbsolute(int selectedHalfHours)
        {
            Sfx?.PlayCrownClick(selectedHalfHours);
        }
        private void OnArgonWatchConfirm()
        {
            Sfx?.PlayConfirm();
        }

        private void ConfigureArgonGameLighting()
        {
            // The 3D hand is rendered on layer 30 in its own RT, while the
            // BEDROOM backdrop remains the original 2D scene. Give that 3D
            // skin a local night-room key/fill rather than the grey unlit look.
            _argonWarmKey = CreateArgonLight(
                "ROKAS_Argon_SoftWarmRoomKey", new Color(1f, .77f, .59f),
                .96f, Quaternion.Euler(21f, -36f, 0f));
            _argonSoftFill = CreateArgonLight(
                "ROKAS_Argon_NightWindowFill", new Color(.64f, .76f, 1f),
                .27f, Quaternion.Euler(-20f, 50f, 0f));
        }

        private Light CreateArgonLight(string name, Color tint,
            float intensity, Quaternion cameraRelativeRotation)
        {
            Transform existing = _presentationCamera.transform.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name);
            go.transform.SetParent(_presentationCamera.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = cameraRelativeRotation;
            Light light = go.GetComponent<Light>();
            if (light == null) light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = tint;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << 30;
            light.renderMode = LightRenderMode.ForcePixel;
            light.enabled = true;
            return light;
        }

        private void LateUpdate()
        {
            // The Argon presenter has its own fixed horizontal camera framing.
            // Do not let the legacy V10 raised-from-bottom camera override it.
            if (_argonFullVisualActive) return;
            // Animator and bone attachments settle before LateUpdate, so the
            // camera frames the actual animated wrist. This does not depend
            // on ROKAS View.Tick being called in a focused frame.
            if (Application.isPlaying && ArmRig != null && Watch != null &&
                ArmRig.CurrentState == KeikoFPSArmRigController.ArmClipState.InspectIdle)
                FrameImportedArmAndWatch();
        }

        private static void AssignIsolatedLayer(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = 30;
        }

        public Rect GetProjectedOledRect(RectTransform referenceRect)
        {
            if (_presentationCamera == null || Watch == null) return new Rect(0f, 0f, 0f, 0f);
            Transform oledTr = _argonFullVisualActive && _argonOledRenderer != null
                ? _argonOledRenderer.transform
                : (Watch.OledRenderer != null ? Watch.OledRenderer.transform : Watch.transform);

            Vector3[] localCorners = new Vector3[4]
            {
                new Vector3(-0.0155f, -0.0185f, 0f),
                new Vector3( 0.0155f, -0.0185f, 0f),
                new Vector3( 0.0155f,  0.0185f, 0f),
                new Vector3(-0.0155f,  0.0185f, 0f)
            };

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            // Return logical ROKAS UI coordinates regardless of internal render
            // target pixel density. The native pointer uses 1920x1080.
            Rect refBounds = referenceRect != null ? referenceRect.rect :
                new Rect(0f, 0f, 1920f, 1080f);

            if (_argonFullVisualActive && _argonOledRenderer != null)
            {
                // Project the actual imported face bounds: its origin and
                // dimensions differ from the hard-coded old V10 quad.
                Bounds realFace = _argonOledRenderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 point = new Vector3(
                        (i & 1) == 0 ? realFace.min.x : realFace.max.x,
                        (i & 2) == 0 ? realFace.min.y : realFace.max.y,
                        (i & 4) == 0 ? realFace.min.z : realFace.max.z);
                    Vector3 vp = _presentationCamera.WorldToViewportPoint(point);
                    if (vp.z <= 0f) continue;
                    float x = refBounds.xMin + vp.x * refBounds.width;
                    float y = refBounds.yMin + vp.y * refBounds.height;
                    minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
                }
                if (maxX > minX && maxY > minY)
                    return Rect.MinMaxRect(minX, minY, maxX, maxY);
            }

            for (int i = 0; i < 4; i++)
            {
                Vector3 wp = oledTr.TransformPoint(localCorners[i]);
                Vector3 vp = _presentationCamera.WorldToViewportPoint(wp);
                float px = refBounds.xMin + vp.x * refBounds.width;
                float py = refBounds.yMin + vp.y * refBounds.height;
                if (px < minX) minX = px;
                if (py < minY) minY = py;
                if (px > maxX) maxX = px;
                if (py > maxY) maxY = py;
            }
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        public void TickComposite(float dt)
        {
            ArmRig?.TickAnimation(dt);
            // Camera tracking happens in LateUpdate independently of the 2D
            // hub Tick/focus gate. A headless or unfocused hub must not leave
            // the visible watch stranded at the original 0.72m-wide shot.
            Watch?.TickWatch(dt);
            Sfx?.TickQueuedCrownClicks(dt);
            InputRouter?.TickRouter();
        }


        /// <summary>
        /// Native Unity imports of the supplied source FBXs currently have
        /// incompatible author units: cloud PlayMode measured 41.85 units for
        /// an arm and 434 units for the OLED face. The original fixed view
        /// at 0.55 units consequently saw an empty frame. Convert once at the
        /// prefab-instance roots (never mutate the source FBX/importer).
        /// Only apply when bounds are implausibly large for a first-person arm
        /// or a wristwatch, keeping already metric imports unchanged.
        /// </summary>
        private void NormalizeImportedPhysicalScale()
        {
            var skin = ArmRig == null ? null : ArmRig.SkinnedRenderer;
            if (skin == null || Watch == null || Watch.OledRenderer == null)
                return;

            float armLongest = LongestAxis(skin.bounds.size);
            if (armLongest > 2f && armLongest < 1000000f)
            {
                float factor = 0.42f / armLongest;
                ArmRig.transform.localScale *= factor;
                Debug.Log("[ROKAS BedSleep V10] Normalized authored arm units: " +
                    armLongest.ToString("F3") + " -> " +
                    LongestAxis(skin.bounds.size).ToString("F3") +
                    "m (factor " + factor.ToString("G5") + ")");
            }

            // Wristwatch is parented under arm's imported bone socket, so
            // remeasure *after* arm scaling instead of assuming the original
            // watch mesh uses meters. Target 40mm OLED diagonal axis.
            float oledLongest = LongestAxis(Watch.OledRenderer.bounds.size);
            if (oledLongest > 0.15f && oledLongest < 1000000f)
            {
                float factor = 0.04f / oledLongest;
                Watch.transform.localScale *= factor;
                Debug.Log("[ROKAS BedSleep V10] Normalized authored OLED units: " +
                    oledLongest.ToString("F3") + " -> " +
                    LongestAxis(Watch.OledRenderer.bounds.size).ToString("F3") +
                    "m (factor " + factor.ToString("G5") + ")");
            }
        }

        private static float LongestAxis(Vector3 size) =>
            Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));

        /// <summary>
        /// Keeps imported skin, sleeve, nails and physical seven-mesh watch in
        /// frame. The saved V9 camera faced +Z with a hard-coded 0.55m offset;
        /// its view never accounted for the FBX watch dial facing a different
        /// direction, or for imported mesh scale / animated arm positions.
        /// No placeholder meshes, camera.main changes or prefab changes.
        /// </summary>
        public void FrameImportedArmAndWatch()
        {
            if (_presentationCamera == null || ArmRig == null || Watch == null ||
                ArmRig.SkinnedRenderer == null || Watch.OledRenderer == null)
                return;

            Renderer skin = ArmRig.SkinnedRenderer;
            Renderer display = Watch.OledRenderer;
            Bounds skinBounds = skin.bounds;
            Bounds displayBounds = display.bounds;
            if (!IsValidBounds(skinBounds) || !IsValidBounds(displayBounds))
                return;

            Bounds compositionBounds = skinBounds;
            foreach (var renderer in Watch.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer != null && IsValidBounds(renderer.bounds))
                    compositionBounds.Encapsulate(renderer.bounds);
            }

            // The front of the imported OLED is defined by its actual mesh
            // normals, not a guessed global camera axis. The mesh is planar.
            Vector3 normal = display.transform.forward;
            var mf = display.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable)
            {
                Vector3[] sourceNormals = mf.sharedMesh.normals;
                Vector3 avg = Vector3.zero;
                for (int i = 0; i < sourceNormals.Length; i += Mathf.Max(1, sourceNormals.Length / 64))
                    avg += sourceNormals[i];
                if (avg.sqrMagnitude > 1e-5f)
                    normal = display.transform.TransformDirection(avg.normalized);
            }
            if (normal.sqrMagnitude < 0.01f)
                normal = Vector3.up;
            normal.Normalize();

            // Look at the real watch face with enough forearm still visible.
            // FPS composition: the shoulder/forearm enters from the lower
            // edge, the wrist/watch rises toward the centre of the picture.
            // Using world +Z as the camera's up axis made the 42cm arm appear
            // horizontal and tiny in cloud-rendered screenshots.
            Vector3 forearm = Vector3.zero;
            if (ArmRig.Bones != null && ArmRig.Bones.Length > 0 && ArmRig.Bones[0] != null)
                forearm = displayBounds.center - ArmRig.Bones[0].position;
            if (forearm.sqrMagnitude < 1e-6f)
                forearm = displayBounds.center - skinBounds.center;
            Vector3 upward = Vector3.ProjectOnPlane(forearm, normal);
            if (upward.sqrMagnitude < 1e-6f)
                upward = Vector3.ProjectOnPlane(Vector3.forward, normal);
            if (upward.sqrMagnitude < 1e-6f)
                upward = Vector3.ProjectOnPlane(Vector3.up, normal);
            upward.Normalize();
            // During TimeSelection the player is reading/adjusting a 40mm
            // OLED: move from the full raised-arm view to a close watch view.
            bool inspect = ArmRig.CurrentState ==
                KeikoFPSArmRigController.ArmClipState.InspectIdle;
            Vector3 aim = Vector3.Lerp(skinBounds.center, displayBounds.center,
                inspect ? 0.98f : 0.83f);
            Quaternion viewRotation = Quaternion.LookRotation(-normal, upward);
            Quaternion inverseView = Quaternion.Inverse(viewRotation);

            float halfWidth = 0.01f, halfHeight = 0.01f, depth = 0f;
            Vector3 min = compositionBounds.min;
            Vector3 max = compositionBounds.max;
            for (int i = 0; i < 8; i++)
            {
                Vector3 point = new Vector3((i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);
                Vector3 local = inverseView * (point - aim);
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(local.x));
                halfHeight = Mathf.Max(halfHeight, Mathf.Abs(local.y));
                depth = Mathf.Max(depth, Mathf.Abs(local.z));
            }

            float fovRadians = Mathf.Max(10f, cameraFov) * Mathf.Deg2Rad;
            float tanHalf = Mathf.Tan(fovRadians * 0.5f);
            float aspect = (float)Mathf.Max(1, renderWidth) / Mathf.Max(1, renderHeight);
            // Tight wrist-first POV: the proximal sleeve may continue beneath
            // the bottom edge, as it should for a raised first-person hand.
            // Full imported-arm bounds framing makes a 40mm dial illegible.
            float fullArmDistance = Mathf.Clamp(
                Mathf.Max(halfHeight / tanHalf, halfWidth / (tanHalf * aspect)) * 0.72f
                + depth + 0.08f, 0.42f, 0.72f);
            float distance = inspect ? Mathf.Clamp(fullArmDistance * 0.52f, 0.32f, 0.42f)
                : fullArmDistance;
            Vector3 desiredPosition = aim + normal * distance;
            if (inspect && _hasReportedCameraFraming && Application.isPlaying)
            {
                float blend = 1f - Mathf.Exp(-Mathf.Max(Time.unscaledDeltaTime, 0.016f) * 8f);
                _presentationCamera.transform.SetPositionAndRotation(
                    Vector3.Lerp(_presentationCamera.transform.position, desiredPosition, blend),
                    Quaternion.Slerp(_presentationCamera.transform.rotation, viewRotation, blend));
            }
            else
                _presentationCamera.transform.SetPositionAndRotation(
                    desiredPosition, viewRotation);
            _presentationCamera.fieldOfView = cameraFov;
            _presentationCamera.nearClipPlane = 0.02f;
            _presentationCamera.farClipPlane = Mathf.Max(8f, distance + depth * 2f + 2f);
            _presentationCamera.cullingMask = 1 << 30;
            _presentationCamera.targetTexture = CompositeRenderTexture;
            if (!_hasReportedCameraFraming)
            {
                _hasReportedCameraFraming = true;
                Debug.Log("[ROKAS BedSleep V10] Visible FBX framing: skin=" +
                    skinBounds.size + ", oled=" + displayBounds.size +
                    ", distance=" + distance.ToString("F3") +
                    ", front=" + normal + ", cullingMask=0x" +
                    _presentationCamera.cullingMask.ToString("X8"));
            }
        }

        private static bool IsValidBounds(Bounds bounds)
        {
            Vector3 size = bounds.size;
            Vector3 center = bounds.center;
            return size.sqrMagnitude > 1e-10f &&
                !float.IsNaN(size.x) && !float.IsInfinity(size.x) &&
                !float.IsNaN(size.y) && !float.IsInfinity(size.y) &&
                !float.IsNaN(size.z) && !float.IsInfinity(size.z) &&
                !float.IsNaN(center.x) && !float.IsInfinity(center.x) &&
                !float.IsNaN(center.y) && !float.IsInfinity(center.y) &&
                !float.IsNaN(center.z) && !float.IsInfinity(center.z);
        }

        private void OnDestroy()
        {
            if (InputRouter != null)
            {
                InputRouter.OnPhysicalOledTap -= OnArgonPhysicalOledTap;
                InputRouter.OnCycleOledScreen -= OnArgonCycleOledScreen;
                InputRouter.UseArgonFaceNavigation = false;
                InputRouter.OnStepHalfHours -= OnArgonCrownStep;
                InputRouter.OnSetAbsoluteHalfHours -= OnArgonCrownAbsolute;
                InputRouter.OnConfirm -= OnArgonWatchConfirm;
            }
            if (CompositeRenderTexture != null)
            {
                if (_presentationCamera != null) _presentationCamera.targetTexture = null;
                CompositeRenderTexture.Release();
                if (Application.isPlaying) Destroy(CompositeRenderTexture);
                else DestroyImmediate(CompositeRenderTexture);
                CompositeRenderTexture = null;
            }
        }
    }
}
