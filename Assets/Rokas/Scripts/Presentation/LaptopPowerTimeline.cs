namespace Rokas.Presentation
{
    /// <summary>
    /// Single-source truth for mechanical power contact; independent of drawing FPS.
    /// The caller owns sound playback and may only fire sound if Advance returns true.
    /// </summary>
    public sealed class LaptopPowerTimeline
    {
        public enum WakeStage { Off, Glow, DarkAwake, BootReady }
        public const float ContactTime = 3.10f;
        public const float GlowEnd = 3.35f;
        public const float DarkAwakeEnd = 3.75f;
        private bool contactFired;
        private bool cancelled;

        public bool Advance(float elapsed)
        {
            if (cancelled || contactFired || elapsed < ContactTime) return false;
            contactFired = true;
            return true;
        }

        public void Cancel() { cancelled = true; }

        public static WakeStage StageAt(float elapsed)
        {
            if (elapsed < ContactTime) return WakeStage.Off;
            if (elapsed < GlowEnd) return WakeStage.Glow;
            if (elapsed < DarkAwakeEnd) return WakeStage.DarkAwake;
            return WakeStage.BootReady;
        }
    }
}
