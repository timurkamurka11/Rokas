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
        Vector3 homePosition;
        Quaternion homeRotation;
        bool homeOrthographic;
        float homeSize;
        float homeFov;
        Vector3 perspectiveHome;
        float perspectiveHomeFov;

        public LicensedCombatCameraPlayer(Camera camera)
        {
            this.camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            CaptureTacticalState();
        }

        public bool Active => source != null;
        public float Clock { get; private set; }
        public bool IsHome => !Active && camera != null &&
            camera.orthographic == homeOrthographic &&
            Mathf.Abs(camera.orthographicSize - homeSize) < .001f &&
            Mathf.Abs(camera.fieldOfView - homeFov) < .001f &&
            (camera.transform.position - homePosition).sqrMagnitude < .000001f &&
            Quaternion.Angle(camera.transform.rotation, homeRotation) < .001f;

        /// <param name="stageOrigin">World-space floor origin of the source composition, not a second torso offset.</param>
        /// <param name="worldUnitsPerSourceUnit">Measured target/source actor-height ratio.</param>
        public void Begin(LicensedCameraData data, Vector3 stageOrigin, float worldUnitsPerSourceUnit, Quaternion stageBasis)
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
            Clock = 0f;
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
            if (deltaTime > 0f && !float.IsNaN(deltaTime) && !float.IsInfinity(deltaTime)) Clock += deltaTime;
            if (Clock >= source.shotDuration) { Cancel(); return; }
            Sample();
        }

        public void Cancel()
        {
            source = null;
            Clock = 0f;
            if (camera == null) return;
            camera.orthographic = homeOrthographic;
            camera.orthographicSize = homeSize;
            camera.fieldOfView = homeFov;
            camera.transform.SetPositionAndRotation(homePosition, homeRotation);
        }

        void CaptureTacticalState()
        {
            homePosition = camera.transform.position;
            homeRotation = camera.transform.rotation;
            homeOrthographic = camera.orthographic;
            homeSize = camera.orthographicSize;
            homeFov = camera.fieldOfView;
        }

        void Sample()
        {
            float weight = source.Weight(Clock);
            Vector3 local = source.parentPosition + new Vector3(
                Evaluate(source.x, Clock), Evaluate(source.y, Clock), Evaluate(source.z, Clock));
            Vector3 shotPosition = origin + basis * (local * scale);
            Quaternion shotRotation = basis * Quaternion.Euler(
                Evaluate(source.pitch, Clock), Evaluate(source.yaw, Clock), Evaluate(source.roll, Clock));
            Vector3 shake = SampleShake();
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

        static float Evaluate(AnimationCurve curve, float time) => curve != null && curve.length > 0 ? curve.Evaluate(time) : 0f;
    }
}
