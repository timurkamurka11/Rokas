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
            Watch?.TickWatch(dt);
            Sfx?.TickQueuedCrownClicks(dt);
            InputRouter?.TickRouter();
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
