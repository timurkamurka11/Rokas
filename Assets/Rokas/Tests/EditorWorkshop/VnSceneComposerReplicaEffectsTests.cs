using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerReplicaEffectsTests
    {
        private static VnSceneComposerProject Project(VnPresentationWorkshopWindow window)
        {
            return (VnSceneComposerProject)typeof(VnPresentationWorkshopWindow)
                .GetField("_sceneComposerProject", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(window);
        }

        [Test]
        public void OldBeatWithoutEffectDataLoadsAsNone()
        {
            var project = new VnSceneComposerProject();
            project.scenes.Add(new VnSceneComposerScene());
            project.scenes[0].dialogueBeats[0].replicaEffect = null;
            VnSceneComposerImportResult loaded = VnSceneComposerSerialization.DeserializePortable(
                VnSceneComposerSerialization.SerializePortable(project));
            Assert.That(loaded.Success, Is.True, loaded.Error);
            Assert.That(loaded.Project.scenes[0].dialogueBeats[0].replicaEffect, Is.Not.Null);
            Assert.That(loaded.Project.scenes[0].dialogueBeats[0].replicaEffect.type,
                Is.EqualTo(VnSceneComposerReplicaEffectType.None));
        }

        [Test]
        public void EffectsBelongToSelectedReplicaAndRoundTripIndependently()
        {
            var window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                window.ComposerSelectScene(0);
                VnSceneComposerProject project = Project(window);
                VnSceneComposerScene scene = project.scenes[0];
                VnSceneComposerDialogueBeat first = scene.dialogueBeats[0];
                window.ComposerSelectDialogueBeat(first.beatId);
                window.ComposerSetSelectedReplicaEffect(
                    VnSceneComposerReplicaEffectType.Shake, .6f, .8f, 11f, 2f,
                    Vector2.right, Color.white);
                window.ComposerAddDialogueBeat();
                VnSceneComposerDialogueBeat second = scene.dialogueBeats[1];
                Assert.That(second.replicaEffect.type, Is.EqualTo(VnSceneComposerReplicaEffectType.None));
                window.ComposerSetSelectedReplicaEffect(
                    VnSceneComposerReplicaEffectType.Flash, .4f, .3f, 12f, 1f,
                    Vector2.up, Color.red);
                Assert.That(first.replicaEffect.type, Is.EqualTo(VnSceneComposerReplicaEffectType.Shake));
                Assert.That(second.replicaEffect.type, Is.EqualTo(VnSceneComposerReplicaEffectType.Flash));

                VnSceneComposerImportResult loaded = VnSceneComposerSerialization.DeserializePortable(
                    VnSceneComposerSerialization.SerializePortable(project));
                Assert.That(loaded.Success, Is.True, loaded.Error);
                Assert.That(loaded.Project.scenes[0].dialogueBeats[0].replicaEffect.type,
                    Is.EqualTo(VnSceneComposerReplicaEffectType.Shake));
                Assert.That(loaded.Project.scenes[0].dialogueBeats[1].replicaEffect.type,
                    Is.EqualTo(VnSceneComposerReplicaEffectType.Flash));
            }
            finally { Object.DestroyImmediate(window); }
        }

        [Test]
        public void ShakeReturnsExactlyToBaseline()
        {
            var effect = new VnSceneComposerReplicaEffect
            {
                type = VnSceneComposerReplicaEffectType.Shake,
                intensity = .6f, duration = .5f, frequency = 9f, decay = 1.5f
            };
            Assert.That(VnSceneComposerReplicaEffects.Sample(effect, .05f).Offset.sqrMagnitude,
                Is.GreaterThan(.01f));
            VnSceneComposerReplicaEffectSample end = VnSceneComposerReplicaEffects.Sample(effect, .5f);
            Assert.That(end.Offset, Is.EqualTo(Vector2.zero));
            Assert.That(end.Scale, Is.EqualTo(1f));
            Assert.That(end.Active, Is.False);
        }

        [Test]
        public void PunchIsSingleDirectionalImpulseAndReturnsToBaseline()
        {
            var effect = new VnSceneComposerReplicaEffect
            {
                type = VnSceneComposerReplicaEffectType.Punch,
                intensity = .7f, duration = .4f, direction = Vector2.left
            };
            VnSceneComposerReplicaEffectSample middle = VnSceneComposerReplicaEffects.Sample(effect, .2f);
            Assert.That(middle.Offset.x, Is.LessThan(-1f));
            Assert.That(middle.Offset.y, Is.EqualTo(0f).Within(.001f));
            Assert.That(middle.Scale, Is.GreaterThan(1f));
            Assert.That(VnSceneComposerReplicaEffects.Sample(effect, .4f).Offset,
                Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void FlashClearsAndPulseReturnsToBaseline()
        {
            var flash = new VnSceneComposerReplicaEffect
            {
                type = VnSceneComposerReplicaEffectType.Flash,
                intensity = .4f, duration = .3f, flashColor = Color.red
            };
            Assert.That(VnSceneComposerReplicaEffects.Sample(flash, 0f).Flash.a,
                Is.EqualTo(.4f).Within(.001f));
            Assert.That(VnSceneComposerReplicaEffects.Sample(flash, .3f).Flash.a,
                Is.EqualTo(0f));

            var pulse = new VnSceneComposerReplicaEffect
            {
                type = VnSceneComposerReplicaEffectType.Pulse,
                intensity = .4f, duration = .6f
            };
            Assert.That(VnSceneComposerReplicaEffects.Sample(pulse, .3f).Scale,
                Is.GreaterThan(1f));
            Assert.That(VnSceneComposerReplicaEffects.Sample(pulse, .6f).Scale,
                Is.EqualTo(1f));
        }

        [Test]
        public void PreviewButtonUsesThePlaybackEffectSample()
        {
            var window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                window.ComposerSelectScene(0);
                VnSceneComposerDialogueBeat beat = Project(window).scenes[0].dialogueBeats[0];
                window.ComposerSelectDialogueBeat(beat.beatId);
                window.ComposerSetSelectedReplicaEffect(
                    VnSceneComposerReplicaEffectType.Pulse, .7f, .6f, 12f, 1.5f,
                    Vector2.right, Color.white);
                window.ComposerPreviewSelectedReplicaEffect();
                var playback = (VnSceneComposerPlaybackController)
                    typeof(VnPresentationWorkshopWindow).GetField(
                        "_sceneComposerPlayback", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(window);
                playback.Advance(.3f);
                VnSceneComposerReplicaEffectSample expected =
                    VnSceneComposerReplicaEffects.Sample(beat.replicaEffect, playback.BeatElapsedSeconds);
                Assert.That(playback.CurrentFrame.ReplicaEffect.Scale,
                    Is.EqualTo(expected.Scale).Within(.0001f));
                Assert.That(playback.CurrentFrame.ReplicaEffect.Active, Is.True);
            }
            finally { Object.DestroyImmediate(window); }
        }
    }
}
