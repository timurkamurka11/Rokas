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
        private AudioSource audioSource;
        private AudioClip cloth, tick, confirmSound;
        private int steps = 14;
        private bool confirmed;
        private float crownTarget;
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
            configureTime = type.GetMethod("ConfigureTime", BindingFlags.Public | BindingFlags.Instance);
            setConfirmed = type.GetMethod("SetConfirmedState", BindingFlags.Public | BindingFlags.Instance);
            if (configureTime == null || setConfirmed == null)
                throw new InvalidOperationException("Actual ROKAS Bed Sleep OLED interface unavailable");
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
            configureTime.Invoke(presenter, new object[] { 19, 30, steps });
            setConfirmed.Invoke(presenter, new object[] { confirmed });
        }

        public void SetDurationSteps(int halfHours, bool withSound = false)
        {
            int next = Mathf.Clamp(halfHours, 1, 24);
            if (next == steps) return;
            int before = steps;
            steps = next;
            confirmed = false;
            crownTarget += Mathf.Sign(next - before) * 4f;
            if (withSound) Play(ArgonFoleyCue.CrownTick);
            UpdateOled();
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
            if (clip != null) audioSource.PlayOneShot(clip);
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
#if ENABLE_LEGACY_INPUT_MANAGER
            if (viewCamera == null || oledCollider == null) return;
            Ray ray = viewCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            bool onFace = oledCollider.Raycast(ray, out hit, 15f);
            bool onCrown = crownCollider != null && crownCollider.Raycast(ray, out hit, 15f);
            if (onFace || onCrown)
            {
                float wheel = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(wheel) > .001f) Step(wheel > 0 ? 1 : -1);
                if (Input.GetMouseButtonDown(0)) Confirm();
            }
#endif
        }

        private void OnDestroy()
        {
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
