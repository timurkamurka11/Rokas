using System;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;

namespace Rokas.Tests
{
    public sealed class LaptopV111ContractEditModeTests
    {
        [Test]
        public void CameraReturnDurationRemainsFixedForMatchingFastApproach()
        {
            var t = typeof(LaptopCinematicSequence);
            var returnTime=t.GetField("ReturnDuration",
                System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            var handsStart=t.GetField("HandsStart",
                System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            Assert.That((float)returnTime.GetRawConstantValue(),Is.EqualTo(.72f));
            // Contact + all 49 art frames still use 1.8s hand-start marker.
            Assert.That((float)handsStart.GetRawConstantValue(),Is.EqualTo(1.8f));
            Assert.That(LaptopPowerTimeline.ContactTime,Is.EqualTo(3.10f));
        }

        [Test]
        public void PhysicalPowerStateRemainsSessionScopedNotPersisted()
        {
            LaptopPowerSession.ResetForTests();
            Assert.That(LaptopPowerSession.PoweredOn, Is.False);
            LaptopPowerSession.CompleteFirstBoot();
            Assert.That(LaptopPowerSession.CompletedBoots, Is.EqualTo(1));
            LaptopPowerSession.CompleteFirstBoot();
            Assert.That(LaptopPowerSession.CompletedBoots, Is.EqualTo(1));
            LaptopPowerSession.ResetForTests();
            Assert.That(LaptopPowerSession.PoweredOn, Is.False);
        }
    }
}
