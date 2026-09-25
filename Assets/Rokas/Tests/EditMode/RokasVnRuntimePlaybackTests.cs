using System;
using System.Linq;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;

namespace Rokas.Core.Tests
{
    public sealed class RokasVnRuntimePlaybackTests
    {
        [Test]
        public void RuntimePlaybackPreservesMovementFocusContinuityAndTerminalCompletionOnce()
        {
            RokasVnRuntimeIntroSnapshot snapshot = CreateMovementTerminalSnapshot();
            var playback = new RokasVnRuntimePlaybackState(snapshot);
            int completed = 0;
            playback.VnSequenceCompleted += () => completed++;

            playback.StartFromBeginning();
            Assert.That(playback.CurrentSceneIndex, Is.EqualTo(0));
            Assert.That(playback.CurrentBeatIndex, Is.EqualTo(0));

            RokasVnRuntimeCharacterSample minaStart = playback.SampleCharacter("Mina");
            RokasVnRuntimeCharacterSample keikoStart = playback.SampleCharacter("Keiko");
            Assert.That(minaStart.position.x, Is.EqualTo(-360f).Within(.01f));
            Assert.That(keikoStart.position.x, Is.EqualTo(360f).Within(.01f));
            Assert.That(minaStart.brightness, Is.GreaterThan(keikoStart.brightness));

            playback.AdvanceTime(.5f);
            RokasVnRuntimeCharacterSample minaMid = playback.SampleCharacter("Mina");
            RokasVnRuntimeCharacterSample keikoMid = playback.SampleCharacter("Keiko");
            Assert.That(minaMid.position.x, Is.GreaterThan(-360f).And.LessThan(0f));
            Assert.That(keikoMid.position.x, Is.GreaterThan(360f));
            Assert.That(keikoMid.alpha, Is.GreaterThan(0f).And.LessThan(1f));

            playback.AdvanceTime(.6f);
            playback.AdvanceDialogue();
            Assert.That(playback.CurrentBeatIndex, Is.EqualTo(1));
            RokasVnRuntimeCharacterSample minaNext = playback.SampleCharacter("Mina");
            RokasVnRuntimeCharacterSample keikoNext = playback.SampleCharacter("Keiko");
            Assert.That(minaNext.position.x, Is.EqualTo(0f).Within(.01f));
            Assert.That(keikoNext.alpha, Is.EqualTo(0f).Within(.001f));

            playback.AdvanceDialogue();
            Assert.That(playback.IsTerminalFadeActive, Is.True);
            Assert.That(playback.IsSequenceCompleted, Is.False);

            playback.AdvanceTime(.2f);
            Assert.That(playback.TerminalFadeAlpha,
                Is.GreaterThan(0f).And.LessThan(1f));
            playback.AdvanceTime(.3f);
            Assert.That(playback.TerminalFadeAlpha, Is.EqualTo(1f).Within(.001f));
            Assert.That(playback.IsSequenceCompleted, Is.True);
            Assert.That(completed, Is.EqualTo(1));

            playback.AdvanceTime(5f);
            playback.AdvanceDialogue();
            Assert.That(completed, Is.EqualTo(1));
        }

        [Test]
        public void RequestAdvanceCompletesTypewriterBeforeAdvancingBeat()
        {
            RokasVnRuntimeIntroSnapshot snapshot = CreateMovementTerminalSnapshot();
            snapshot.scenes[0].dialogueBeats[0].text = "ABCD";
            snapshot.scenes[0].presentation.typewriterCharactersPerSecond = 2f;

            var playback = new RokasVnRuntimePlaybackState(snapshot);
            playback.StartFromBeginning();

            Assert.That(playback.VisibleDialogueCharacters, Is.Zero);
            Assert.That(playback.RequestAdvance(), Is.False);
            Assert.That(playback.CurrentBeatIndex, Is.Zero);
            Assert.That(playback.VisibleDialogueCharacters, Is.EqualTo(4));
            Assert.That(playback.RequestAdvance(), Is.True);
            Assert.That(playback.CurrentBeatIndex, Is.EqualTo(1));
        }

        [Test]
        public void SecondaryAfterPrimaryWaitsAndReplicaEffectsUseAuthoredTiming()
        {
            RokasVnRuntimeIntroSnapshot snapshot = CreateMovementTerminalSnapshot();
            RokasVnRuntimeBeatSnapshot beat = snapshot.scenes[0].dialogueBeats[0];
            beat.movement.primary.action = 6;
            beat.movement.primary.duration = 1f;
            beat.movement.secondary.characterId = "Keiko";
            beat.movement.secondary.action = 6;
            beat.movement.secondary.duration = 1f;
            beat.movement.secondaryTiming = 0;
            beat.replicaEffect.type = 1;
            beat.replicaEffect.intensity = .5f;
            beat.replicaEffect.duration = 1f;
            beat.replicaEffect.frequency = 10f;

            var playback = new RokasVnRuntimePlaybackState(snapshot);
            playback.StartFromBeginning();

            float keikoStart = playback.SampleCharacter("Keiko").position.x;
            playback.AdvanceTime(.5f);
            Assert.That(playback.SampleCharacter("Keiko").position.x,
                Is.EqualTo(keikoStart).Within(.01f));

            RokasVnRuntimeReplicaEffectSample effect = playback.SampleReplicaEffect();
            Assert.That(effect.active, Is.True);
            Assert.That(effect.scale, Is.EqualTo(1f).Within(.001f));

            playback.AdvanceTime(1f);
            Assert.That(playback.SampleCharacter("Keiko").position.x,
                Is.LessThan(keikoStart));
        }

        [Test]
        public void RuntimePlaybackLivesInPlayerAssemblyWithoutUnityEditorReference()
        {
            string[] references = typeof(RokasVnRuntimePlaybackState).Assembly
                .GetReferencedAssemblies()
                .Select(item => item.Name)
                .ToArray();
            Assert.That(references, Does.Not.Contain("UnityEditor"));
        }

        private static RokasVnRuntimeIntroSnapshot CreateMovementTerminalSnapshot()
        {
            var scene = new RokasVnRuntimeSceneSnapshot
            {
                sceneId = "11111111111111111111111111111111",
                label = "Terminal",
                isTerminal = true,
                terminalFadeDuration = .4f,
                presentation = new RokasVnRuntimePresentationSnapshot
                {
                    leftX = -360f,
                    centerX = 0f,
                    rightX = 360f,
                    slotY = 0f,
                    leftScale = 1f,
                    centerScale = 1f,
                    rightScale = 1f,
                    speakerActiveScale = 1.05f,
                    speakerActiveBrightness = 1f,
                    speakerActiveForwardOffset = 12f,
                    speakerInactiveScale = .94f,
                    speakerInactiveBrightness = .76f,
                    speakerInactiveAlpha = .84f,
                    typewriterEnabled = true,
                    typewriterCharactersPerSecond = 36f
                }
            };
            scene.characters.Add(new RokasVnRuntimeCharacterSnapshot
            {
                characterId = "Mina",
                stateId = "mina_neutral",
                stageSlot = 0
            });
            scene.characters.Add(new RokasVnRuntimeCharacterSnapshot
            {
                characterId = "Keiko",
                stateId = "keiko_neutral",
                stageSlot = 2
            });

            scene.dialogueBeats.Add(new RokasVnRuntimeBeatSnapshot
            {
                beatId = "22222222222222222222222222222222",
                speaker = "Mina",
                text = string.Empty,
                movement = new RokasVnRuntimeMovementSnapshot
                {
                    primary = new RokasVnRuntimeMovementActionSnapshot
                    {
                        characterId = "Mina",
                        action = 6,
                        duration = 1f
                    },
                    secondary = new RokasVnRuntimeMovementActionSnapshot
                    {
                        characterId = "Keiko",
                        action = 3,
                        duration = 1f
                    },
                    secondaryTiming = 1
                }
            });
            scene.dialogueBeats.Add(new RokasVnRuntimeBeatSnapshot
            {
                beatId = "33333333333333333333333333333333",
                speaker = "Mina",
                text = string.Empty
            });

            return new RokasVnRuntimeIntroSnapshot
            {
                projectId = RokasVnRuntimeIntroPackage.ExpectedProjectId,
                sourceProjectSha256 = "runtime-playback-fixture",
                sceneCount = 1,
                beatCount = 2,
                scenes = { scene }
            };
        }
    }
}
