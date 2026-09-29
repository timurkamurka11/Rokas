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
            ResetIntroState("ROKAS: VN intro completion flag reset.");
        }

        [MenuItem("ROKAS/Debug/Replay VN Intro")]
        private static void ReplayVnIntro()
        {
            ResetIntroState("ROKAS: VN intro reset; next Enter World will replay it.");
        }

        private static void ResetIntroState(string confirmation)
        {
            PlayerPrefs.DeleteKey(PlayerPrefsVnIntroProgress.CompletedKey);
            PlayerPrefsVnIntroProgress.RequestHubGuildIntroReplay();
            PlayerPrefs.Save();
            Debug.Log(confirmation + " Hub guild intro prompt will replay after VN.");
        }
    }
}
