using NUnit.Framework;
using Rokas.Presentation;

namespace Rokas.Tests
{
    public sealed class LaptopPowerTimelineEditModeTests
    {
        [Test]
        public void NoClickBeforeContactAndExactlyOneAfterCrossing()
        {
            var timeline = new LaptopPowerTimeline();
            Assert.That(timeline.Advance(0f), Is.False);
            Assert.That(timeline.Advance(3.09f), Is.False);
            Assert.That(timeline.Advance(3.11f), Is.True);
            Assert.That(timeline.Advance(3.12f), Is.False);
            Assert.That(timeline.Advance(5f), Is.False);
        }

        [Test]
        public void SkippedBeforeContactNeverProducesDelayedClick()
        {
            var timeline = new LaptopPowerTimeline();
            timeline.Advance(2.8f);
            timeline.Cancel();
            Assert.That(timeline.Advance(4f), Is.False);
        }

        [Test]
        public void ContactEventIsFrameRateIndependent()
        {
            foreach (float fps in new[] { 24f, 30f, 60f, 120f })
            {
                var timeline = new LaptopPowerTimeline();
                int clicks = 0;
                for (int i = 0; i <= (int)(fps * 4.2f); i++)
                    if (timeline.Advance(i / fps)) clicks++;
                Assert.That(clicks, Is.EqualTo(1), "fps " + fps);
            }
        }

        [Test]
        public void WakeStagesNeverRunBeforePhysicalContact()
        {
            Assert.That(LaptopPowerTimeline.StageAt(3.09f),
                Is.EqualTo(LaptopPowerTimeline.WakeStage.Off));
            Assert.That(LaptopPowerTimeline.StageAt(3.10f),
                Is.EqualTo(LaptopPowerTimeline.WakeStage.Glow));
            Assert.That(LaptopPowerTimeline.StageAt(3.40f),
                Is.EqualTo(LaptopPowerTimeline.WakeStage.DarkAwake));
            Assert.That(LaptopPowerTimeline.StageAt(3.75f),
                Is.EqualTo(LaptopPowerTimeline.WakeStage.BootReady));
        }
    }
}
