using UnityEngine;

namespace Rokas.Presentation
{
    /// <summary>Power lasts for one running game process only. Never written to saves.</summary>
    public static class LaptopPowerSession
    {
        public static bool PoweredOn { get; private set; }
        public static int CompletedBoots { get; private set; }

        // Handles Unity domain-reload-disabled Enter Play Mode as well as cold starts.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnGameProcessStarted()
        {
            PoweredOn = false;
            CompletedBoots = 0;
        }

        public static void CompleteFirstBoot()
        {
            if (PoweredOn) return;
            PoweredOn = true;
            CompletedBoots++;
        }

        /// <summary>Switch OFF the physical laptop for this running session.
        /// Completed boot count is preserved; next press boots normally.</summary>
        public static bool Shutdown()
        {
            if (!PoweredOn) return false;
            PoweredOn = false;
            return true;
        }

        public static void ResetForTests()
        {
            PoweredOn = false;
            CompletedBoots = 0;
        }
    }
}
