using UnityEngine;

namespace Rokas.BedSleep.V10Release
{
    /// <summary>
    /// V6 Authoritative 3D Smartwatch Controller.
    /// P0-5 Resolution: Binds directly to the 7 imported FBX mesh nodes inside PF_ROKAS_Smartwatch.prefab
    /// (Watch_TitaniumCase, Watch_Strap_Top, Watch_Strap_Bottom, Watch_DigitalCrown_Rotary,
    /// Watch_SideConfirmButton, Watch_OLED_Screen, Watch_SapphireGlass).
    /// Procedural box mesh builders have been removed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Smartwatch3DController : MonoBehaviour
    {
        [SerializeField] private Transform _crownTransform;
        [SerializeField] private Transform _sideButtonTransform;
        [SerializeField] private MeshRenderer _oledRenderer;
        [SerializeField] private MeshCollider _oledMeshCollider;

        public Transform CrownTransform => _crownTransform;
        public Transform SideButtonTransform => _sideButtonTransform;
        public MeshRenderer OledRenderer => _oledRenderer;
        public MeshCollider OledMeshCollider => _oledMeshCollider;

        public float CurrentCrownAngle => _targetCrownAngle;
        public float TargetCrownAngleDeg => _targetCrownAngle;
        public const float DegreesPerHalfHourStep = 24.0f;

        public Material titaniumMaterial;
        public Material sapphireGlassMaterial;
        public Material oledMaterial;
        public Material strapMaterial;

        private float _targetCrownAngle;
        private float _currentCrownAngle;
        private Quaternion _crownRestLocalRotation = Quaternion.identity;
        private Material _oledRuntimeMaterial;
        private float _buttonPressTimer = -1f;
        private Vector3 _buttonRestLocalPos;

        public void InitializeWatch(Transform watchSocket, RenderTexture oledRt)
        {
            if (watchSocket != null)
            {
                transform.SetParent(watchSocket, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.Euler(0f, 0f, 14f);
                transform.localScale = Vector3.one;
            }

            BindPrefabWatchNodes(oledRt);
        }

        private Transform FindImportedPart(string name)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t == transform) continue;
                if (t.name == name || t.name == name + "_Mesh" ||
                    t.name.StartsWith(name + ".", System.StringComparison.Ordinal))
                    return t;
            }
            return null;
        }

        private void BindPrefabWatchNodes(RenderTexture oledRt)
        {
            // Blender FBX introduces a model root (and sometimes more than
            // one transform). Transform.Find only visits direct children and
            // caused the old code to clone/fabricate duplicate watch parts.
            if (_crownTransform == null)
                _crownTransform = FindImportedPart("Watch_DigitalCrown_Rotary");
            if (_sideButtonTransform == null)
                _sideButtonTransform = FindImportedPart("Watch_SideConfirmButton");

            Transform oledTr = FindImportedPart("Watch_OLED_Screen");
            if (oledTr != null)
            {
                if (_oledRenderer == null) _oledRenderer = oledTr.GetComponent<MeshRenderer>();
                if (_oledMeshCollider == null) _oledMeshCollider = oledTr.GetComponent<MeshCollider>();
            }

            if (_crownTransform == null || _crownTransform.GetComponent<MeshFilter>() == null)
                throw new MissingReferenceException("V8: imported 3D rotary crown MeshFilter is missing; no placeholder permitted.");
            if (_sideButtonTransform == null || _sideButtonTransform.GetComponent<MeshFilter>() == null)
                throw new MissingReferenceException("V8: imported 3D confirm button MeshFilter is missing.");
            if (_oledRenderer == null || _oledRenderer.GetComponent<MeshFilter>() == null)
                throw new MissingReferenceException("V8: imported 3D OLED screen MeshRenderer is missing.");
            if (_oledMeshCollider == null || _oledMeshCollider.sharedMesh == null)
                throw new MissingReferenceException("V8: OLED requires the real imported MeshCollider, not a UI rectangle.");

            // Animate around the imported author's actual local-axis rest pose.
            // Never replace the authored pivot orientation with Quaternion.Euler(0,0,0).
            _crownRestLocalRotation = _crownTransform.localRotation;
            _buttonRestLocalPos = _sideButtonTransform.localPosition;

            // Never mutate a shared imported .mat with the runtime RenderTexture.
            // One transient material instance belongs to this watch only.
            if (_oledRuntimeMaterial != null)
            {
                if (Application.isPlaying) Destroy(_oledRuntimeMaterial);
                else DestroyImmediate(_oledRuntimeMaterial);
                _oledRuntimeMaterial = null;
            }
            if (oledMaterial == null || oledMaterial.shader == null)
                throw new MissingReferenceException("V10: real 3D OLED imported material missing");
            _oledRuntimeMaterial = new Material(oledMaterial) { name = "ROKAS_V10_OLED_Runtime_Instance" };
            if (oledRt != null) _oledRuntimeMaterial.mainTexture = oledRt;
            _oledRenderer.sharedMaterial = _oledRuntimeMaterial;
        }

        private void OnDestroy()
        {
            if (_oledRuntimeMaterial == null) return;
            if (Application.isPlaying) Destroy(_oledRuntimeMaterial);
            else DestroyImmediate(_oledRuntimeMaterial);
            _oledRuntimeMaterial = null;
        }

        public void StepCrown(int deltaHalfHours)
        {
            // A ring selection can jump several half-hour slots in one click.
            // Preserve the *magnitude* of the change, not merely its sign.
            if (deltaHalfHours == 0) return;
            _targetCrownAngle += Mathf.Clamp(deltaHalfHours, -22, 22) * DegreesPerHalfHourStep;
            // Motion is smoothed by TickWatch to one final authoritative angle.
        }

        public void PressConfirmButton()
        {
            _buttonPressTimer = 0f;
        }

        public void TickWatch(float dt)
        {
            _currentCrownAngle = Mathf.Lerp(_currentCrownAngle, _targetCrownAngle,
                1f - Mathf.Exp(-Mathf.Max(0f, dt) * 16f));
            if (Mathf.Abs(_targetCrownAngle - _currentCrownAngle) < 0.02f)
                _currentCrownAngle = _targetCrownAngle;
            if (_crownTransform != null)
                _crownTransform.localRotation = _crownRestLocalRotation *
                    Quaternion.AngleAxis(_currentCrownAngle, Vector3.right);

            if (_buttonPressTimer >= 0f && _sideButtonTransform != null)
            {
                _buttonPressTimer += dt;
                float u = Mathf.Clamp01(_buttonPressTimer / 0.24f);
                float inward = Mathf.Sin(u * Mathf.PI) * 0.0012f;
                _sideButtonTransform.localPosition = _buttonRestLocalPos + new Vector3(-inward, 0f, 0f);
                if (u >= 1f)
                {
                    _sideButtonTransform.localPosition = _buttonRestLocalPos;
                    _buttonPressTimer = -1f;
                }
            }
        }
    }
}
