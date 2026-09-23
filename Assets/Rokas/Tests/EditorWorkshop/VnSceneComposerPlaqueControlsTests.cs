using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerPlaqueControlsTests
    {
        private static object Get(object target, string name)
        {
            var p = target.GetType().GetProperty(name);
            Assert.That(p, Is.Not.Null, "Missing runtime state: " + name);
            return p.GetValue(target);
        }
        private static object Call(object target, string name, params object[] args)
        {
            var m = target.GetType().GetMethod(name);
            Assert.That(m, Is.Not.Null, "Missing authoritative command: " + name);
            return m.Invoke(target, args);
        }
        private static VnSceneComposerProject Project()
        {
            var p = new VnSceneComposerProject();
            var s = new VnSceneComposerScene();
            s.dialogueBeats.Clear();
            for (int i = 0; i < 3; i++) s.dialogueBeats.Add(new VnSceneComposerDialogueBeat { text = new string('A', 160) });
            p.scenes.Add(s);
            p.scenes.Add(new VnSceneComposerScene());
            p.scenes[1].transition.sceneTransitionType = VnSceneComposerSceneTransitionType.DarkCurtain;
            p.scenes[1].transition.sceneTransitionDuration = 1;
            return p;
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void TriangleFollowsDialogueCompleteAcrossPlaybackModes(int mode)
        {
            using (var c = new VnSceneComposerPlaybackController(Project()))
            {
                if (mode == 0) c.PlayScene(0); else if (mode == 1) c.PlayAll(); else c.PlayFromHere(0, 1);
                int beat = c.CurrentBeatIndex;
                Assert.That(Get(c, "ShowCompletionIndicator"), Is.False);
                c.Advance(.3f);
                Assert.That(Get(c, "ShowCompletionIndicator"), Is.False);
                Call(c, "RequestAdvance", 1L);
                Assert.That(c.CurrentBeatIndex, Is.EqualTo(beat));
                Assert.That(Get(c, "ShowCompletionIndicator"), Is.True);
                Call(c, "RequestAdvance", 2L);
                Assert.That(c.CurrentBeatIndex, Is.EqualTo(beat + 1));
                Assert.That(Get(c, "ShowCompletionIndicator"), Is.False);
            }
        }
        [Test]
        public void TriangleAndForwardInSameInputTickCannotSkipTwoBeats()
        {
            using (var c = new VnSceneComposerPlaybackController(Project()))
            {
                c.PlayFromHere(0); c.Advance(20);
                Call(c, "RequestAdvance", 42L); Call(c, "RequestAdvance", 42L);
                Assert.That(c.CurrentBeatIndex, Is.EqualTo(1));
                Assert.That(c.CurrentFrame.DialogueReveal.Complete, Is.False);
            }
        }
        [Test]
        public void TriangleClearsOnPreviousAndSceneTransition()
        {
            using (var c = new VnSceneComposerPlaybackController(Project()))
            {
                c.PlayFromHere(0, 1); c.Advance(20);
                Assert.That(Get(c, "ShowCompletionIndicator"), Is.True);
                c.PreviousDialogue(); Assert.That(Get(c, "ShowCompletionIndicator"), Is.False);
                c.Advance(20); c.Next(); Assert.That(Get(c, "ShowCompletionIndicator"), Is.False);
            }
        }
        [Test]
        public void MenuFreezesDialogueAndRejectsAllAdvanceCommands()
        {
            using (var c = new VnSceneComposerPlaybackController(Project()))
            {
                c.PlayFromHere(0); c.Advance(.4f);
                var frame = c.CurrentFrame; float elapsed = c.SceneElapsedSeconds;
                Call(c, "SetMenuOpen", true); Call(c, "SetMenuOpen", true);
                c.Advance(2); c.AdvanceDialogue(); c.Next(); c.PreviousDialogue();
                Assert.That(Get(c, "IsMenuOpen"), Is.True);
                Assert.That(c.CurrentFrame, Is.SameAs(frame));
                Assert.That(c.SceneElapsedSeconds, Is.EqualTo(elapsed));
                Assert.That(c.CurrentSceneIndex, Is.EqualTo(0));
                Assert.That(c.CurrentBeatIndex, Is.EqualTo(0));
                Call(c, "SetMenuOpen", false); c.Advance(.1f);
                Assert.That(c.SceneElapsedSeconds, Is.EqualTo(elapsed + .1f).Within(.001f));
            }
        }
        [Test]
        public void MenuFreezesTransitionAndResumeDoesNotRestartIt()
        {
            using (var c = new VnSceneComposerPlaybackController(Project()))
            {
                c.PlayFromHere(0); c.Advance(20); c.Next(); c.Advance(.2f);
                var before = c.CurrentFrame; int starts = c.SceneTransitionStartCount;
                Call(c, "SetMenuOpen", true); c.Advance(5);
                Assert.That(c.CurrentFrame, Is.SameAs(before));
                Assert.That(c.SceneTransitionHasSwapped, Is.False);
                Call(c, "SetMenuOpen", false); c.Advance(.4f);
                Assert.That(c.SceneTransitionHasSwapped, Is.True);
                Assert.That(c.SceneTransitionStartCount, Is.EqualTo(starts));
            }
        }
        [Test]
        public void MenuAndMuteAreDistinctAndMasterVolumeIsRestored()
        {
            float original = AudioListener.volume;
            try
            {
                AudioListener.volume = .37f;
                using (var c = new VnSceneComposerPlaybackController(Project()))
                {
                    c.PlayFromHere(0);
                    int starts = c.MusicStartCount + c.AdditionalAudioStartCount;
                    Call(c, "SetMuted", true);
                    Assert.That(AudioListener.volume, Is.Zero);
                    Assert.That(Get(c, "IsMenuOpen"), Is.False);
                    Call(c, "SetMenuOpen", true); Call(c, "SetMenuOpen", false);
                    Assert.That(AudioListener.volume, Is.Zero);
                    Call(c, "SetMuted", false);
                    Assert.That(AudioListener.volume, Is.EqualTo(.37f));
                    Assert.That(c.MusicStartCount + c.AdditionalAudioStartCount, Is.EqualTo(starts));
                    Call(c, "SetMuted", true);
                }
                Assert.That(AudioListener.volume, Is.EqualTo(.37f));
            }
            finally { AudioListener.volume = original; }
        }
        [Test]
        public void UiClockRunsWhileMenuPausesPresentation()
        {
            using (var c = new VnSceneComposerPlaybackController(Project()))
            {
                c.PlayFromHere(0); Call(c, "SetMenuOpen", true);
                float before = (float)Get(c, "UiElapsedSeconds");
                c.Advance(.75f);
                Assert.That((float)Get(c, "UiElapsedSeconds"), Is.EqualTo(before + .75f).Within(.001f));
                Assert.That(c.SceneElapsedSeconds, Is.Zero);
            }
        }
        [Test]
        public void TransientControlsDoNotChangePortableAuthoringData()
        {
            var p = Project(); string before = VnSceneComposerSerialization.SerializePortable(p);
            using (var c = new VnSceneComposerPlaybackController(p))
            {
                c.PlayFromHere(0); c.Advance(20); Call(c, "SetMuted", true); Call(c, "SetMenuOpen", true);
                Assert.That(VnSceneComposerSerialization.SerializePortable(p), Is.EqualTo(before));
            }
        }
    }
}
