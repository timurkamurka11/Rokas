using UnityEngine;

namespace Rokas.BedSleep.Argon.Candidate
{
    /// <summary>
    /// Pure, deterministic first-person bed arm gesture with separate raise
    /// and lower choreography. Sample AFTER the authored source FBX clip.
    /// No generated geometry or layout changes. At visible=1 every offset
    /// and rotation is EXACTLY zero (approved horizontal hero pose).
    /// </summary>
    public static class ArgonBedOrganicGesture
    {
        public struct Sample
        {
            // Offsets in the existing fixed Bed POV camera basis, meters.
            public float Horizontal;
            public float Vertical;
            public float Away;
            public float WristPitchDegrees;
            public float WristRollDegrees;
            public float ForearmPitchDegrees;
            public float ForearmRollDegrees;
            public float ProximalElbowDegrees;
            public float ProximalSwivelDegrees;
            public float FingerResponse;  // delayed/relaxing extra articulation
            public float Settling;
        }

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (t * (6f * t - 15f) + 10f);
        }
        private static float Window(float t, float begin, float end)
        {
            return Ease((t - begin) / (end - begin));
        }

        public static Sample Evaluate(float visibility, bool lowering)
        {
            float v = Mathf.Clamp01(visibility);
            float p = lowering ? 1f - v : v;
            // Entry: wrist leads and forearm follows *while visible*, instead
            // of spending the whole wrist gesture below the frame. Exit:
            // the wrist relaxes
            // first, then the forearm drops, with its own different arc.
            float moved = lowering
                ? Window(p, .11f, .96f)
                : Window(p, .055f, .94f);
            float hidden = lowering ? moved : 1f - moved;
            float wrist = lowering
                ? Window(p, .025f, .77f)
                : 1f - Window(p, .17f, .88f);
            float forearm = lowering
                ? Window(p, .14f, .87f)
                : 1f - Window(p, .28f, .98f);
            // A tiny damped settle AFTER most of the lift, decaying exactly
            // to zero at the final hero keyframe, with no perpetual drift.
            float arrive = lowering ? 0f :
                Mathf.Clamp01((p - .70f) / .30f);
            float settle = lowering ? 0f :
                Mathf.Sin(arrive * Mathf.PI * 2f) *
                Mathf.Pow(Mathf.Sin(arrive * Mathf.PI),2f) *
                (1f - arrive) * (1f - arrive) *
                (p > .70f ? 1f : 0f);
            // Exit has a distinct slower, forward-falling arc. No constant
            // lateral slide, no sudden reverse playback of the entry.
            float arc = Mathf.Sin(moved * Mathf.PI);
            // Elbow/forearm/wrist are ONE articulated chain, not one
            // translated plank. This raised middle-phase counter-motion
            // happens on camera, then dies away before the approved pose.
            // Gentle and deterministic: no randomized/floating arm sway.
            float reach=Window(p,.16f,.90f);
            float breathingArc=Mathf.Pow(Mathf.Sin(reach*Mathf.PI),2f);
            float wristCounter= Mathf.Pow(Mathf.Sin(
                Window(p,.21f,.94f)*Mathf.PI),2f);
            return new Sample
            {
                Horizontal = -(lowering ? .080f : .106f)*hidden+
                    arc*(lowering ? -.004f : .007f)+
                    breathingArc*(lowering ? -.003f : .004f),
                Vertical = -(lowering ? .348f : .315f)*hidden+
                    arc*(lowering ? -.008f : .017f)+
                    breathingArc*(lowering ? -.006f : .006f)+
                    settle*.0023f,
                Away = (lowering ? .116f : .080f)*hidden+
                    (lowering ? .010f : -.006f)*arc+
                    breathingArc*(lowering ? .003f : -.004f),
                WristPitchDegrees = (lowering ? -15.6f : 13.2f)*wrist+
                    wristCounter*(lowering ? -1.1f : 2.4f)+settle*.52f,
                // The hand points screen-RIGHT, so turning around camera RIGHT
                // only pronates it. Visible life comes from camera-forward
                // pitch: wrist and elbow must arc within the screen plane.
                WristRollDegrees = (lowering ? -12.0f : 16.5f)*wrist+
                    wristCounter*(lowering ? 2.1f : -4.4f),
                ForearmPitchDegrees = (lowering ? -7.8f : 6.1f)*forearm+
                    breathingArc*(lowering ? -1.3f : 2.1f),
                ForearmRollDegrees = (lowering ? -5.8f : 7.5f)*forearm+
                    breathingArc*(lowering ? -1.1f : 1.65f),
                // A distinct proximal forearm/"elbow" hinge is essential:
                // simply translating the entire prefab reproduces a wooden
                // sliding prop, even when wrist rotates nicely. Let the
                // proximal section follow after the wrist, with restrained
                // independent sweep in the camera plane.
                ProximalElbowDegrees = (lowering ? -10.2f : 10.8f)*
                    Window(forearm,0f,1f)+
                    breathingArc*(lowering ? -.8f : 1.6f),
                ProximalSwivelDegrees = (lowering ? 9.8f : -9.3f)*
                    Mathf.Sin(forearm*Mathf.PI*.5f)+
                    breathingArc*(lowering ? 2.3f : -2.7f),
                // Every finger receives its own micro-response with a small
                // phase lag in the living rig. Keep separate from source FBX
                // MCP; do not over-curl malformed pinky/ring finger roots.
                FingerResponse = lowering
                    ? (1f - Window(p, .06f, .70f))
                    : Window(p, .27f, .91f),
                Settling = settle
            };
        }
    }
}
