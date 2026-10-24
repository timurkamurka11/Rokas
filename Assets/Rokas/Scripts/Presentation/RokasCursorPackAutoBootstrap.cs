using UnityEngine;

namespace Rokas.Presentation
{
    /// <summary>
    /// Self-register original Keiko 6-cursor presenter without editing any
    /// existing user-local RokasBootstrap.cs, game scene, input or prefabs.
    /// In builds this component is added once after the initial scene loads.
    /// </summary>
    internal static class RokasCursorPackAutoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Object.FindFirstObjectByType<RokasCursorPackRuntime>() != null)
                return;
            var root = new GameObject("ROKAS_Keiko_Six_Hand_Cursors");
            Object.DontDestroyOnLoad(root);
            root.AddComponent<RokasCursorPackRuntime>();
        }
    }
}
