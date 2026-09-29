using Rokas.Presentation;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools
{
    internal static class RokasVnIntroDebugMenu
    {
        [MenuItem("ROKAS/Debug/Reset VN Intro")]
        private static void ResetVnIntro()
        {
            ResetIntroFlag("ROKAS: VN intro completion flag reset.");
        }

        [MenuItem("ROKAS/Debug/Replay VN Intro")]
        private static void ReplayVnIntro()
        {
            ResetIntroFlag("ROKAS: VN intro reset; next Enter World will replay it.");
        }

        private static void ResetIntroFlag(string confirmation)
        {
            PlayerPrefs.DeleteKey(PlayerPrefsVnIntroProgress.CompletedKey);
            PlayerPrefs.Save();
            Debug.Log(confirmation);
        }
    }
}
