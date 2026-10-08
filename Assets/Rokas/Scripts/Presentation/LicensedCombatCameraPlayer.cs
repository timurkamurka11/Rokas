using System;
using UnityEngine;

namespace Rokas.Presentation
{
    [Serializable]
    public sealed class LicensedCameraShakeSegment
    {
        public float start, duration, clipIn, speed = 1f;
        public int priority;
        public bool muted;
        public AnimationCurve x = new AnimationCurve();
        public AnimationCurve y = new AnimationCurve();
        public AnimationCurve z = new AnimationCurve();
    }

    /// <summary>Samples licensed shot data on the arena's existing camera. Owns no Core clock or contact.</summary>
    public sealed class LicensedCombatCameraPlayer
    {
        readonly Camera camera;
        LicensedCameraData source;
        Vector3 origin;
        Quaternion basis;
        float scale;
        Vector3 cameraParentPosition;
        Quaternion cameraParentRotation;
        Vector3 homePosition;
        Quaternion homeRotation;
        Transform homeParent;
        Vector3 homeLocalPosition;
        Quaternion homeLocalRotation;
        bool homeOrthographic;
        float homeSize;
        float homeFov;
        Vector3 perspectiveHome;
        float perspectiveHomeFov;
        Vector3 cancelPosition;
        Quaternion cancelRotation;
        float cancelFov, cancelElapsed, cancelDuration;
        double elapsedClock;

        public LicensedCombatCameraPlayer(Camera camera)
        {
            this.camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            CaptureTacticalState();
        }

        public bool Active => source != null;
        public bool IsCancelling => cancelDuration > 0f;
        public float Clock => (float)elapsedClock;
        public bool IsHome => !Active && camera != null &&
            camera.orthographic == homeOrthographic &&
            camera.orthographicSize == homeSize &&
            camera.fieldOfView == homeFov &&
            HomePoseMatches();

        /// <param name="stageOrigin">World-space floor origin of the source composition, not a second torso offset.</param>
        /// <param name="worldUnitsPerSourceUnit">Measured target/source actor-height ratio.</param>
        public void Begin(LicensedCameraData data, Vector3 stageOrigin, float worldUnitsPerSourceUnit, Quaternion stageBasis)
        {
            Begin(data, stageOrigin, worldUnitsPerSourceUnit, stageBasis, null, null);
        }

        /// <param name="resolvedParentPosition">Complete camera-root plus selected Offset translation; replaces the profile parent.</param>
        /// <param name="resolvedParentEuler">Selected Offset parent rotation, before the recorded camera child rotation.</param>
        public void Begin(LicensedCameraData data, Vector3 stageOrigin, float worldUnitsPerSourceUnit, Quaternion stageBasis,
            Vector3? resolvedParentPosition, Vector3? resolvedParentEuler = null)
        {
            if (Active) Cancel();
            CaptureTacticalState();
            if (data == null || data.shotDuration <= 0f) return;
            if (worldUnitsPerSourceUnit <= 0f || float.IsNaN(worldUnitsPerSourceUnit) || float.IsInfinity(worldUnitsPerSourceUnit))
                throw new ArgumentOutOfRangeException(nameof(worldUnitsPerSourceUnit));
            source = data;
            origin = stageOrigin;
            basis = stageBasis;
            scale = worldUnitsPerSourceUnit;
            cameraParentPosition = resolvedParentPosition ?? data.parentPosition;
            cameraParentRotation = Quaternion.Euler(resolvedParentEuler ?? data.parentEuler);
            elapsedClock = 0;
            perspectiveHomeFov = homeOrthographic ? Mathf.Clamp(data.baseFov, 1f, 179f) : homeFov;
            perspectiveHome = homePosition;
            if (homeOrthographic)
            {
                // Same vertical extent and centre on the actor plane, then restore exact orthographic projection at the end.
                Vector3 forward = homeRotation * Vector3.forward;
                float planeDepth = Vector3.Dot(stageOrigin - homePosition, forward);
                float perspectiveDepth = homeSize / Mathf.Tan(perspectiveHomeFov * Mathf.Deg2Rad * .5f);
                perspectiveHome += forward * (planeDepth - perspectiveDepth);
            }
            Sample();
        }

        public void Tick(float deltaTime)
        {
            if (!Active || camera == null) return;
            if (IsCancelling)
            {
                if (deltaTime > 0f && !float.IsNaN(deltaTime) && !float.IsInfinity(deltaTime)) cancelElapsed += deltaTime;
                if (cancelElapsed >= cancelDuration) { Cancel(); return; }
                float alpha = Mathf.Clamp01(cancelElapsed / cancelDuration);
                camera.orthographic = false;
                camera.fieldOfView = Mathf.Lerp(cancelFov, perspectiveHomeFov, alpha);
                camera.transform.SetPositionAndRotation(Vector3.Lerp(cancelPosition, perspectiveHome, alpha),
                    Quaternion.Slerp(cancelRotation, homeRotation, alpha));
                return;
            }
            if (deltaTime > 0f && !float.IsNaN(deltaTime) && !float.IsInfinity(deltaTime)) elapsedClock += deltaTime;
            if (elapsedClock >= source.shotDuration) { Cancel(); return; }
            Sample();
        }

        /// <summary>Own interruption adaptation: frozen displayed camera exits with the body's linear handoff.</summary>
        public void BeginCancel(float seconds)
        {
            if (!Active || IsCancelling || camera == null) return;
            if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) { Cancel(); return; }
            cancelPosition = camera.transform.position;
            cancelRotation = camera.transform.rotation;
            cancelFov = camera.fieldOfView;
            cancelElapsed = 0f;
            cancelDuration = seconds;
        }

        public void Cancel()
        {
            cancelDuration = cancelElapsed = 0f;
            source = null;
            elapsedClock = 0;
            if (camera == null) return;
            camera.orthographic = homeOrthographic;
            camera.orthographicSize = homeSize;
            camera.fieldOfView = homeFov;
            if (camera.transform.parent == homeParent)
                camera.transform.SetLocalPositionAndRotation(homeLocalPosition, homeLocalRotation);
            else
                camera.transform.SetPositionAndRotation(homePosition, homeRotation);
        }

        void CaptureTacticalState()
        {
            homePosition = camera.transform.position;
            homeRotation = camera.transform.rotation;
            homeParent = camera.transform.parent;
            homeLocalPosition = camera.transform.localPosition;
            homeLocalRotation = camera.transform.localRotation;
            homeOrthographic = camera.orthographic;
            homeSize = camera.orthographicSize;
            homeFov = camera.fieldOfView;
        }

        void Sample()
        {
            float weight = source.Weight(Clock);
            Vector3 local = cameraParentPosition + cameraParentRotation * new Vector3(
                Evaluate(source.x, Clock), Evaluate(source.y, Clock), Evaluate(source.z, Clock));
            Vector3 shotPosition = origin + basis * (local * scale);
            Quaternion shotRotation = basis * cameraParentRotation * Quaternion.Euler(
                Evaluate(source.pitch, Clock), Evaluate(source.yaw, Clock), Evaluate(source.roll, Clock));
            Vector3 shake = SampleShake();
            if (source.routeShakeToTimelineActiveCamera && weight < 1f)
            {
                // Native outgoing one-shot override routes shakers to fallback CamB; CameraState blends its correction by 1-weight.
                shake *= 1f - weight;
            }
            camera.orthographic = false;
            camera.fieldOfView = Mathf.Lerp(perspectiveHomeFov, source.shotFov, weight);
            camera.transform.SetPositionAndRotation(
                Vector3.Lerp(perspectiveHome, shotPosition, weight) + basis * (shake * scale),
                Quaternion.Slerp(homeRotation, shotRotation, weight));
        }

        Vector3 SampleShake()
        {
            var tracks = source.shakes;
            if (tracks == null || tracks.Length == 0)
                return Clock < source.shakeDuration
                    ? new Vector3(Evaluate(source.shakeX, Clock), Evaluate(source.shakeY, Clock), Evaluate(source.shakeZ, Clock))
                    : Vector3.zero;
            Vector3 selected = Vector3.zero;
            int selectedPriority = int.MinValue;
            bool found = false;
            foreach (var track in tracks)
            {
                if (track == null || track.muted || track.duration <= 0f || Clock < track.start || Clock >= track.start + track.duration) continue;
                float time = track.clipIn + (Clock - track.start) * track.speed;
                Vector3 offset = new Vector3(Evaluate(track.x, time), Evaluate(track.y, time), Evaluate(track.z, time));
                if (offset.sqrMagnitude == 0f || (found && track.priority <= selectedPriority)) continue;
                selected = offset;
                selectedPriority = track.priority;
                found = true;
            }
            // For equal priorities retain serialized order; source registration order is an explicit authoring limitation.
            return selected;
        }

        bool HomePoseMatches()
        {
            bool sameParent = camera.transform.parent == homeParent;
            Vector3 position = sameParent ? camera.transform.localPosition : camera.transform.position;
            Quaternion rotation = sameParent ? camera.transform.localRotation : camera.transform.rotation;
            return (position - (sameParent ? homeLocalPosition : homePosition)).sqrMagnitude <= 1e-12f &&
                RotationMatches(rotation, sameParent ? homeLocalRotation : homeRotation);
        }

        static bool RotationMatches(Quaternion actual, Quaternion expected)
        {
            // q and -q represent one orientation. Avoid float dot/acos rounding small real drift to zero.
            double dx = (double)actual.x - expected.x, dy = (double)actual.y - expected.y;
            double dz = (double)actual.z - expected.z, dw = (double)actual.w - expected.w;
            double sx = (double)actual.x + expected.x, sy = (double)actual.y + expected.y;
            double sz = (double)actual.z + expected.z, sw = (double)actual.w + expected.w;
            return Math.Min(dx * dx + dy * dy + dz * dz + dw * dw,
                sx * sx + sy * sy + sz * sz + sw * sw) <= 2.5e-13;
        }

        static float Evaluate(AnimationCurve curve, float time) => curve != null && curve.length > 0 ? curve.Evaluate(time) : 0f;
    }
}
