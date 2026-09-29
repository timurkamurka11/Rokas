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
        public void BeatHopMatchesImmutableOracleLocalVerticalMotion()
        {
            RokasVnRuntimeIntroSnapshot snapshot = CreateMovementTerminalSnapshot();
            RokasVnRuntimeBeatSnapshot beat = snapshot.scenes[0].dialogueBeats[0];
            beat.movement = new RokasVnRuntimeMovementSnapshot();
            beat.effect = 2;
            beat.targetCharacterId = "Mina";
            beat.effectStrength = 18f;
            beat.effectDuration = .4f;

            var playback = new RokasVnRuntimePlaybackState(snapshot);
            playback.StartFromBeginning();
            float minaBaseY = playback.SampleCharacter("Mina").position.y;
            float keikoBaseY = playback.SampleCharacter("Keiko").position.y;

            playback.AdvanceTime(.2f);

            Assert.That(
                playback.SampleCharacter("Mina").position.y,
                Is.EqualTo(minaBaseY - 18f).Within(.001f),
                "Scene 05-style Hop must use the immutable Preview sin(pi*t) local Y motion.");
            Assert.That(
                playback.SampleCharacter("Keiko").position.y,
                Is.EqualTo(keikoBaseY).Within(.001f),
                "Hop is local to the authored target character, never a whole-scene shake.");
        }

        [Test]
        public void StagingHopHonorsDelayAndTargetsOnlyAuthoredCharacter()
        {
            RokasVnRuntimeIntroSnapshot snapshot = CreateMovementTerminalSnapshot();
            RokasVnRuntimeBeatSnapshot beat = snapshot.scenes[0].dialogueBeats[0];
            beat.movement = new RokasVnRuntimeMovementSnapshot();
            beat.characterStaging.Add(new RokasVnRuntimeStagingSnapshot
            {
                stagingId = "staging-hop",
                characterId = "Mina",
                effect = 2,
                effectStrength = 20f,
                effectDuration = .4f,
                delaySeconds = .2f
            });

            var playback = new RokasVnRuntimePlaybackState(snapshot);
            playback.StartFromBeginning();
            float baseY = playback.SampleCharacter("Mina").position.y;

            playback.AdvanceTime(.1f);
            Assert.That(
                playback.SampleCharacter("Mina").position.y,
                Is.EqualTo(baseY).Within(.001f));

            playback.AdvanceTime(.2f);
            float expected = baseY - 20f * Mathf.Sin(Mathf.PI * .25f);
            Assert.That(
                playback.SampleCharacter("Mina").position.y,
                Is.EqualTo(expected).Within(.001f),
                "Staging Hop must use elapsed time since its authored delay.");
        }

        [Test]
        public void BeatAccentAndSceneActionBounceMatchImmutableOracleCurve()
        {
            RokasVnRuntimeIntroSnapshot snapshot = CreateMovementTerminalSnapshot();
            RokasVnRuntimeSceneSnapshot scene = snapshot.scenes[0];
            RokasVnRuntimeBeatSnapshot beat = scene.dialogueBeats[0];
            beat.movement = new RokasVnRuntimeMovementSnapshot();
            beat.effect = 1;
            beat.targetCharacterId = "Mina";
            beat.effectStrength = 20f;
            beat.effectDuration = .4f;

            var accentPlayback = new RokasVnRuntimePlaybackState(snapshot);
            accentPlayback.StartFromBeginning();
            RokasVnRuntimeCharacterSample accentStart =
                accentPlayback.SampleCharacter("Mina");
            accentPlayback.AdvanceTime(.2f);
            RokasVnRuntimeCharacterSample accentMid =
                accentPlayback.SampleCharacter("Mina");
            Assert.That(
                accentMid.position.y,
                Is.EqualTo(accentStart.position.y - 20f).Within(.001f));
            Assert.That(
                accentMid.scale / accentStart.scale,
                Is.EqualTo(1.03f).Within(.001f),
                "Accent must preserve the oracle action-bounce scale emphasis.");

            RokasVnRuntimeIntroSnapshot bounceSnapshot =
                CreateMovementTerminalSnapshot();
            RokasVnRuntimeSceneSnapshot bounceScene =
                bounceSnapshot.scenes[0];
            bounceScene.dialogueBeats[0].movement =
                new RokasVnRuntimeMovementSnapshot();
            bounceScene.triggerActionBounce = true;

            var bouncePlayback =
                new RokasVnRuntimePlaybackState(bounceSnapshot);
            bouncePlayback.StartFromBeginning();
            RokasVnRuntimeCharacterSample bounceStart =
                bouncePlayback.SampleCharacter("Mina");
            bouncePlayback.AdvanceTime(.14f);
            RokasVnRuntimeCharacterSample bounceMid =
                bouncePlayback.SampleCharacter("Mina");
            Assert.That(
                bounceMid.position.y,
                Is.EqualTo(bounceStart.position.y - 18f).Within(.001f),
                "Scene action bounce must use the immutable oracle default amplitude at midpoint.");
            Assert.That(
                bounceMid.scale / bounceStart.scale,
                Is.EqualTo(1.03f).Within(.001f));
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
