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
            PlayerPrefs.DeleteKey(SentinelKey);
            PlayerPrefs.Save();
        }

        [TestCase("ROKAS/Debug/Reset VN Intro")]
        [TestCase("ROKAS/Debug/Replay VN Intro")]
        public void MenuItemClearsOnlyIntroCompletionFlag(string menuPath)
        {
            PlayerPrefs.SetInt(PlayerPrefsVnIntroProgress.CompletedKey, 1);
            PlayerPrefs.SetInt(SentinelKey, 73);
            PlayerPrefs.Save();

            LogAssert.Expect(LogType.Log, new Regex("^ROKAS: VN intro"));

            bool executed = EditorApplication.ExecuteMenuItem(menuPath);

            Assert.That(executed, Is.True, $"Menu item '{menuPath}' is not registered.");
            Assert.That(PlayerPrefs.HasKey(PlayerPrefsVnIntroProgress.CompletedKey), Is.False);
            Assert.That(PlayerPrefs.GetInt(SentinelKey, -1), Is.EqualTo(73));
        }
    }
}
