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

        [Test]
        public void MapsApprovedYarnBeatCommandsToStableVisualState()
        {
            VnIntroBeatState bus = VnIntroController.MapBeat("bus_stop", "Keiko", "dark", "keiko_neutral");
            Assert.That(bus.BackgroundId, Is.EqualTo("bus_stop"));
            Assert.That(bus.Speaker, Is.EqualTo("Keiko"));
            Assert.That(bus.PanelStyle, Is.EqualTo("dark"));
            Assert.That(bus.PortraitId, Is.EqualTo("keiko_neutral"));

            VnIntroBeatState sky = VnIntroController.MapBeat("night_sky", "Keiko", "dark", "keiko_neutral");
            Assert.That(sky.BackgroundId, Is.EqualTo("night_sky"));

            VnIntroBeatState phone = VnIntroController.MapBeat("phone", "Mina", "light", "mina_neutral");
            Assert.That(phone.BackgroundId, Is.EqualTo("phone"));
            Assert.That(phone.Speaker, Is.EqualTo("Mina"));
            Assert.That(phone.PanelStyle, Is.EqualTo("light"));
            Assert.That(phone.PortraitId, Is.EqualTo("mina_neutral"));
        }

        [Test]
        public void RejectsUnknownBeatIdentifiersInsteadOfGuessing()
        {
            Assert.Throws<System.ArgumentException>(() =>
                VnIntroController.MapBeat("unknown", "Keiko", "dark", "keiko_neutral"));
        }

        [Test]
        public void LineGateAllowsExactlyOneContinueAndPauseBlocksIt()
        {
            var gate = new VnIntroLineGate();
            gate.BeginLine();
            Assert.That(gate.IsAwaitingContinue, Is.True);
            Assert.That(gate.TryContinue(), Is.True);
            Assert.That(gate.TryContinue(), Is.False);

            gate.BeginLine();
            gate.SetPaused(true);
            Assert.That(gate.TryContinue(), Is.False);
            Assert.That(gate.IsAwaitingContinue, Is.True);
            gate.SetPaused(false);
            Assert.That(gate.TryContinue(), Is.True);
            Assert.That(gate.TryContinue(), Is.False);
        }
    }
}
