using NUnit.Framework;
using Rokas.Core;
using Rokas.Presentation;

namespace Rokas.Tests
{
    public sealed class RokasBedSleepEditModeTests
    {
        [Test]
        public void SleepDurationUsesExactHalfHourSteps()
        {
            Assert.AreEqual(1f, RokasBedSleepCinematic.QuantizeDuration(-5f));
            Assert.AreEqual(12f, RokasBedSleepCinematic.QuantizeDuration(16f));
            Assert.AreEqual(7.5f, RokasBedSleepCinematic.QuantizeDuration(7.31f));
            Assert.AreEqual(.5f, RokasBedSleepCinematic.StepHours);
        }

        [Test]
        public void SleepAcrossMidnightAdvancesDayAndClock()
        {
            Assert.AreEqual(2, RokasBedSleepCinematic.WakeDay(1, 23.5f, 1.5f));
            Assert.That(RokasBedSleepCinematic.WakeHour(23.5f, 1.5f),
                Is.EqualTo(1f).Within(.00001f));
            Assert.AreEqual("01:00", RokasBedSleepCinematic.ClockText(1f));
            Assert.AreEqual("00:00", RokasBedSleepCinematic.ClockText(24f));
        }

        [Test]
        public void InvalidOrLongSleepCannotExceedTwelveHours()
        {
            Assert.AreEqual(12f, RokasBedSleepCinematic.QuantizeDuration(99f));
            Assert.AreEqual(1f, RokasBedSleepCinematic.QuantizeDuration(0f));
            Assert.AreEqual(2, RokasBedSleepCinematic.WakeDay(1, 18f, 7f));
            Assert.AreEqual(1f, RokasBedSleepCinematic.WakeHour(18f, 7f));
        }

        [Test]
        public void SaveDataRetainsBackwardCompatibleSleepFields()
        {
            var state = new SaveData();
            Assert.IsFalse(state.hubSleepInitialized);
            Assert.AreEqual(1, state.hubDay);
            Assert.AreEqual(0, state.hubSleepCount);
            Assert.AreEqual(2, SaveData.CurrentVersion);
            state.hubSleepInitialized = true;
            state.hubDay = 3;
            state.hubClockHours = 7.5f;
            state.hubSleepCount = 2;
            var clone = UnityEngine.JsonUtility.FromJson<SaveData>(
                UnityEngine.JsonUtility.ToJson(state));
            Assert.IsTrue(clone.hubSleepInitialized);
            Assert.AreEqual(3, clone.hubDay);
            Assert.That(clone.hubClockHours, Is.EqualTo(7.5f).Within(.001f));
            Assert.AreEqual(2, clone.hubSleepCount);
        }

        [Test]
        public void BedInteractionControllerHasEightRequiredPhases()
        {
            Assert.AreEqual(8, System.Enum.GetValues(
                typeof(RokasBedSleepCinematic.State)).Length);
            Assert.AreEqual("ChooseDuration",
                RokasBedSleepCinematic.State.ChooseDuration.ToString());
        }
    }
}
