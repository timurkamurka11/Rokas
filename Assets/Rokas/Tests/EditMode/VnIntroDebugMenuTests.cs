using System.Text.RegularExpressions;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rokas.Core.Tests
{
    public sealed class VnIntroDebugMenuTests
    {
        private const string SentinelKey = "rokas.vn_intro.debug-menu.sentinel";

        [SetUp, TearDown]
        public void CleanupPlayerPrefs()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsVnIntroProgress.CompletedKey);
            PlayerPrefs.DeleteKey(PlayerPrefsVnIntroProgress.HubGuildIntroReplayKey);
            PlayerPrefs.DeleteKey(SentinelKey);
            PlayerPrefs.Save();
        }

        [TestCase("ROKAS/Debug/Reset VN Intro")]
        [TestCase("ROKAS/Debug/Replay VN Intro")]
        public void MenuItemResetsIntroAndQueuesOneHubPromptReplay(string menuPath)
        {
            PlayerPrefs.SetInt(PlayerPrefsVnIntroProgress.CompletedKey, 1);
            PlayerPrefs.SetInt(SentinelKey, 73);
            PlayerPrefs.Save();

            LogAssert.Expect(LogType.Log, new Regex("^ROKAS: VN intro"));

            bool executed = EditorApplication.ExecuteMenuItem(menuPath);

            Assert.That(executed, Is.True, $"Menu item '{menuPath}' is not registered.");
            Assert.That(PlayerPrefs.HasKey(PlayerPrefsVnIntroProgress.CompletedKey), Is.False);
            Assert.That(
                PlayerPrefsVnIntroProgress.HubGuildIntroReplayRequested,
                Is.True,
                "Reset/Replay VN Intro must make the post-VN guild prompt eligible exactly once.");
            Assert.That(PlayerPrefs.GetInt(SentinelKey, -1), Is.EqualTo(73));
        }
    }
}
