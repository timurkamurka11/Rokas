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

            Sfx = GetComponent<BedSleepSFXController>() ?? gameObject.AddComponent<BedSleepSFXController>();
            Sfx.InitializeAudio();

            InputRouter = GetComponent<SmartwatchInputRouter>() ?? gameObject.AddComponent<SmartwatchInputRouter>();
            InputRouter.ConfigureRouter(_presentationCamera, Watch.OledMeshCollider, targetUiRect);
        }

        private static void AssignIsolatedLayer(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = 30;
        }

        public Rect GetProjectedOledRect(RectTransform referenceRect)
        {
            if (_presentationCamera == null || Watch == null) return new Rect(0f, 0f, 0f, 0f);
            Transform oledTr = Watch.OledRenderer != null ? Watch.OledRenderer.transform : Watch.transform;

            Vector3[] localCorners = new Vector3[4]
            {
                new Vector3(-0.0155f, -0.0185f, 0f),
                new Vector3( 0.0155f, -0.0185f, 0f),
                new Vector3( 0.0155f,  0.0185f, 0f),
                new Vector3(-0.0155f,  0.0185f, 0f)
            };

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            Rect refBounds = referenceRect != null ? referenceRect.rect : new Rect(0f, 0f, renderWidth, renderHeight);

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
            // Animator-backed V10 FBX clips continue moving the wrist after the
            // 2D fade has finished. Follow its actual render bounds during watch
            // inspection so the wrist and OLED cannot disappear off-camera.
            if (ArmRig != null && ArmRig.CurrentState == KeikoFPSArmRigController.ArmClipState.InspectIdle)
                FrameImportedArmAndWatch();
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
            Vector3 aim = Vector3.Lerp(skinBounds.center, displayBounds.center, 0.83f);
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
            float distance = Mathf.Clamp(
                Mathf.Max(halfHeight / tanHalf, halfWidth / (tanHalf * aspect)) * 0.72f
                + depth + 0.08f, 0.42f, 0.72f);

            _presentationCamera.transform.SetPositionAndRotation(
                aim + normal * distance, viewRotation);
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
