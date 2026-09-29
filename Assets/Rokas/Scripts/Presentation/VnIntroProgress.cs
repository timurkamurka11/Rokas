using UnityEngine;

namespace Rokas.Presentation
{
    public interface IVnIntroProgress
    {
        bool IsCompleted { get; }
        void MarkCompleted();
    }

    public sealed class PlayerPrefsVnIntroProgress : IVnIntroProgress
    {
        public const string CompletedKey = "rokas.vn_intro.completed.v1";
        public const string HubGuildIntroReplayKey =
            "rokas.vn_intro.hub_guild_intro_replay.v1";

        public bool IsCompleted => PlayerPrefs.GetInt(CompletedKey, 0) == 1;

        public static bool HubGuildIntroReplayRequested =>
            PlayerPrefs.GetInt(HubGuildIntroReplayKey, 0) == 1;

        public void MarkCompleted()
        {
            PlayerPrefs.SetInt(CompletedKey, 1);
            PlayerPrefs.Save();
        }

        public static void RequestHubGuildIntroReplay()
        {
            PlayerPrefs.SetInt(HubGuildIntroReplayKey, 1);
        }

        public static void ConsumeHubGuildIntroReplay()
        {
            PlayerPrefs.DeleteKey(HubGuildIntroReplayKey);
            PlayerPrefs.Save();
        }
    }
}
