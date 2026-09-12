using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;

namespace Rokas.Core.Tests
{
    public sealed class VnIntroProgressTests
    {
        [SetUp, TearDown]
        public void ClearKey()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsVnIntroProgress.CompletedKey);
        }

        [Test]
        public void UsesVersionedKeyAndDefaultsToIncomplete()
        {
            Assert.That(PlayerPrefsVnIntroProgress.CompletedKey, Is.EqualTo("rokas.vn_intro.completed.v1"));
            IVnIntroProgress progress = new PlayerPrefsVnIntroProgress();
            Assert.That(progress.IsCompleted, Is.False);
        }

        [Test]
        public void MarkCompletedPersistsOnlyExplicitSuccess()
        {
            IVnIntroProgress progress = new PlayerPrefsVnIntroProgress();
            Assert.That(progress.IsCompleted, Is.False);
            progress.MarkCompleted();
            Assert.That(new PlayerPrefsVnIntroProgress().IsCompleted, Is.True);
        }
    }
}
