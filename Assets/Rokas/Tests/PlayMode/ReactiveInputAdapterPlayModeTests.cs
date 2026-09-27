using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Rokas.Tests
{
    public sealed class ReactiveInputAdapterPlayModeTests
    {
        [Test]
        public void DevicePressKeepsItsTimestampAndNeedsReleaseAfterRearm()
        {
            var priorEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            var priorBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            var received = new List<ReactiveDevicePress>();
            var adapter = new ReactiveInputAdapter();
            try
            {
                adapter.SetContext(true);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(), InputState.currentTime);
                InputSystem.Update();
                adapter.Tick();
                double firstTime = InputState.currentTime;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space), firstTime);
                InputSystem.Update();
                adapter.Flush(received.Add);
                Assert.That(received.Count, Is.EqualTo(1));
                Assert.That(received[0].Kind, Is.EqualTo(ReactivePressKind.Parry));
                Assert.That(received[0].DeviceTimeUs,
                    Is.EqualTo((long)Math.Round(firstTime * 1000000d, MidpointRounding.AwayFromZero)));

                long firstEpoch = received[0].Epoch;
                received.Clear();
                adapter.ForceRearm();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space), InputState.currentTime);
                InputSystem.Update();
                adapter.Flush(received.Add);
                Assert.That(received, Is.Empty, "A held key cannot become a fresh defense press.");

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(), InputState.currentTime);
                InputSystem.Update();
                adapter.Tick();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space), InputState.currentTime);
                InputSystem.Update();
                adapter.Flush(received.Add);
                Assert.That(received.Count, Is.EqualTo(1));
                Assert.That(received[0].Epoch, Is.GreaterThan(firstEpoch));
                Assert.That(received[0].InputId, Is.GreaterThan(1));
            }
            finally
            {
                adapter.Dispose();
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.editorInputBehaviorInPlayMode = priorEditorBehavior;
                InputSystem.settings.backgroundBehavior = priorBackgroundBehavior;
            }
        }

        [Test]
        public void DefenseInputIsOwnedOnlyByActiveArenaAndDoesNotDuplicate()
        {
            var priorEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            var priorBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            var adapter = new ReactiveInputAdapter();
            var delivered = new List<ReactiveDevicePress>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(), InputState.currentTime);
                InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space), InputState.currentTime);
                InputSystem.Update();
                adapter.Flush(delivered.Add);
                Assert.That(delivered, Is.Empty, "Pre-arena input must not reach combat.");

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(), InputState.currentTime);
                InputSystem.Update();
                adapter.SetContext(true);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space), InputState.currentTime);
                InputSystem.Update();
                adapter.Flush(delivered.Add);
                Assert.That(delivered.Count, Is.EqualTo(1), "One physical press must produce one intent.");
                InputSystem.Update();
                adapter.Flush(delivered.Add);
                Assert.That(delivered.Count, Is.EqualTo(1), "A held key must not produce a second intent.");

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(), InputState.currentTime);
                InputSystem.Update();
                adapter.SetContext(false);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space), InputState.currentTime);
                InputSystem.Update();
                adapter.Flush(delivered.Add);
                Assert.That(delivered.Count, Is.EqualTo(1), "Post-arena input must not reach old combat.");
            }
            finally
            {
                adapter.Dispose();
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.editorInputBehaviorInPlayMode = priorEditorBehavior;
                InputSystem.settings.backgroundBehavior = priorBackgroundBehavior;
            }
        }
    }
}
