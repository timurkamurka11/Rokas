using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerLayeredAudioTests
    {
        private const string TrackAPath = "Assets/Rokas/Audio/05. Restaurant Prep.mp3";
        private const string TrackAGuid = "7540bd61684b4a768cad4691dbc54972";
        private const string TrackBPath = "Assets/Rokas/Audio/HomeNocturne.wav";
        private const string TrackBGuid = "e56430b08183503bb580307d23f0d2f5";
        private const string MissingGuid = "22222222222222222222222222222222";

        [Test]
        public void MSfx_01_ZeroAdditionalCuesKeepsSceneValid()
        {
            var project = ProjectWithBeats(1);
            IList cues = CueList(project.scenes[0]);
            Assert.That(cues, Is.Not.Null);
            Assert.That(cues.Count, Is.EqualTo(0));
            Assert.DoesNotThrow(() => RoundTrip(project));
        }

        [Test]
        public void MSfx_02_OneShotCueModelExists()
        {
            var project = ProjectWithBeats(1);
            object cue = AddCue(project.scenes[0], TrackAGuid, false, "SceneStart", "", "Natural", "");
            Assert.That((bool)Field(cue, "loop"), Is.False);
            Assert.That(Field(cue, "trigger").ToString(), Is.EqualTo("SceneStart"));
        }

        [Test]
        public void MSfx_03_LoopingCueModelExists()
        {
            var project = ProjectWithBeats(1);
            object cue = AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");
            Assert.That((bool)Field(cue, "loop"), Is.True);
            Assert.That(Field(cue, "stopMode").ToString(), Is.EqualTo("SceneEnd"));
        }

        [Test]
        public void MSfx_04_MultipleCuesExistIndependently()
        {
            var project = ProjectWithBeats(1);
            object a = AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");
            object b = AddCue(project.scenes[0], TrackBGuid, false, "SceneStart", "", "Natural", "");
            Assert.That(CueList(project.scenes[0]).Count, Is.EqualTo(2));
            Assert.That(Field(a, "cueId"), Is.Not.EqualTo(Field(b, "cueId")));
        }

        [Test]
        public void MSfx_05_StableIndependentCueIds()
        {
            var project = ProjectWithBeats(1);
            object a = AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");
            object b = AddCue(project.scenes[0], TrackBGuid, true, "SceneStart", "", "SceneEnd", "");
            Assert.That(((string)Field(a, "cueId")).Length, Is.EqualTo(32));
            Assert.That((string)Field(a, "cueId"), Is.Not.EqualTo((string)Field(b, "cueId")));
        }

        [Test]
        public void MSfx_06_AssetGuidRoundTrip()
        {
            var project = ProjectWithBeats(1);
            AddCue(project.scenes[0], TrackAGuid, false, "SceneStart", "", "Natural", "");
            var loaded = RoundTrip(project);
            Assert.That((string)Field(CueList(loaded.scenes[0])[0], "assetGuid"), Is.EqualTo(TrackAGuid));
        }

        [Test]
        public void MSfx_07_VolumeRoundTrip()
        {
            var project = ProjectWithBeats(1);
            object cue = AddCue(project.scenes[0], TrackAGuid, false, "SceneStart", "", "Natural", "");
            SetField(cue, "volume", .37f);
            var loaded = RoundTrip(project);
            Assert.That((float)Field(CueList(loaded.scenes[0])[0], "volume"), Is.EqualTo(.37f).Within(.0001f));
        }

        [Test]
        public void MSfx_08_LoopRoundTrip()
        {
            var project = ProjectWithBeats(1);
            AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");
            var loaded = RoundTrip(project);
            Assert.That((bool)Field(CueList(loaded.scenes[0])[0], "loop"), Is.True);
        }

        [Test]
        public void MSfx_09_FadesRoundTrip()
        {
            var project = ProjectWithBeats(1);
            object cue = AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");
            SetField(cue, "fadeInSeconds", .25f);
            SetField(cue, "fadeOutSeconds", .7f);
            var loaded = RoundTrip(project);
            object copy = CueList(loaded.scenes[0])[0];
            Assert.That((float)Field(copy, "fadeInSeconds"), Is.EqualTo(.25f).Within(.0001f));
            Assert.That((float)Field(copy, "fadeOutSeconds"), Is.EqualTo(.7f).Within(.0001f));
        }

        [Test]
        public void MSfx_10_SceneStartTriggerStartsCue()
        {
            var project = ProjectWithBeats(1);
            object cue = AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");
            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                AssertCueActive(playback, (string)Field(cue, "cueId"), true);
            }
        }

        [Test]
        public void MSfx_11_BeatStartTriggerStartsOnSelectedBeat()
        {
            var project = ProjectWithBeats(3);
            string beat = project.scenes[0].dialogueBeats[1].beatId;
            object cue = AddCue(project.scenes[0], TrackAGuid, true, "BeatStart", beat, "SceneEnd", "");
            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                AssertCueActive(playback, (string)Field(cue, "cueId"), false);
                playback.AdvanceDialogue();
                AssertCueActive(playback, (string)Field(cue, "cueId"), true);
            }
        }

        [Test]
        public void MSfx_12_OptionalDelayDefersStart()
        {
            var project = ProjectWithBeats(1);
            object cue = AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");
            SetField(cue, "startDelaySeconds", .8f);
            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                AssertCueActive(playback, (string)Field(cue, "cueId"), false);
                playback.Advance(.79f);
                AssertCueActive(playback, (string)Field(cue, "cueId"), false);
                playback.Advance(.02f);
                AssertCueActive(playback, (string)Field(cue, "cueId"), true);
            }
        }

        [Test]
        public void MSfx_13_StaleDelayedBeatTriggerCancelsAfterBeatChange()
        {
            var project = ProjectWithBeats(3);
            string beat1 = project.scenes[0].dialogueBeats[0].beatId;
            object cue = AddCue(project.scenes[0], TrackAGuid, true, "BeatStart", beat1, "SceneEnd", "");
            SetField(cue, "startDelaySeconds", 1f);
            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                playback.Advance(.2f);
                playback.AdvanceDialogue();
                playback.Advance(1.2f);
                AssertCueActive(playback, (string)Field(cue, "cueId"), false);
            }
        }

        [Test]
        public void MSfx_14_StopBeatStopsActiveLoop()
        {
            var project = ProjectWithBeats(3);
            string start = project.scenes[0].dialogueBeats[0].beatId;
            string stop = project.scenes[0].dialogueBeats[2].beatId;
            object cue = AddCue(project.scenes[0], TrackAGuid, true, "BeatStart", start, "BeatStart", stop);
            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                AssertCueActive(playback, (string)Field(cue, "cueId"), true);
                playback.AdvanceDialogue();
                AssertCueActive(playback, (string)Field(cue, "cueId"), true);
                playback.AdvanceDialogue();
                AssertCueActive(playback, (string)Field(cue, "cueId"), false);
            }
        }

        [Test]
        public void MSfx_15_OneShotCompletesDeterministically()
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(TrackAPath);
            Assert.That(clip, Is.Not.Null);
            var project = ProjectWithBeats(1);
            object cue = AddCue(project.scenes[0], TrackAGuid, false, "SceneStart", "", "Natural", "");
            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                AssertCueActive(playback, (string)Field(cue, "cueId"), true);
                playback.Advance(clip.length + .1f);
                AssertCueActive(playback, (string)Field(cue, "cueId"), false);
            }
        }

        [Test]
        public void MSfx_16_MultipleCuesPlaySimultaneously()
        {
            var project = ProjectWithBeats(1);
            AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");
            AddCue(project.scenes[0], TrackBGuid, true, "SceneStart", "", "SceneEnd", "");
            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                Assert.That(IntProperty(playback, "ActiveAdditionalAudioSourceCount"), Is.EqualTo(2));
            }
        }

        [Test]
        public void MSfx_17_PrimaryBgmAndSfxPlaySimultaneously()
        {
            var project = ProjectWithBeats(1);
            SetMusic(project.scenes[0], TrackBGuid);
            AddCue(project.scenes[0], TrackAGuid, false, "SceneStart", "", "Natural", "", "Sfx");
            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                Assert.That(playback.CurrentMusicAssetGuid, Is.EqualTo(TrackBGuid));
                Assert.That(playback.ActiveMusicSourceCount, Is.EqualTo(1));
                Assert.That(IntProperty(playback, "ActiveAdditionalAudioSourceCount"), Is.EqualTo(1));
            }
        }

        [Test]
        public void MSfx_18_PrimaryBgmAndAmbiencePlaySimultaneously()
        {
            var project = ProjectWithBeats(1);
            SetMusic(project.scenes[0], TrackBGuid);
            AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "", "Ambience");
            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                Assert.That(playback.ActiveMusicSourceCount, Is.EqualTo(1));
                Assert.That(IntProperty(playback, "ActiveAdditionalAudioSourceCount"), Is.EqualTo(1));
            }
        }

        [Test]
        public void MSfx_19_PrimaryBgmAndMusicLayerPlaySimultaneously()
        {
            var project = ProjectWithBeats(1);
            SetMusic(project.scenes[0], TrackBGuid);
            AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "", "MusicLayer");
            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                Assert.That(playback.CurrentMusicAssetGuid, Is.EqualTo(TrackBGuid));
                Assert.That(IntProperty(playback, "ActiveAdditionalAudioSourceCount"), Is.EqualTo(1));
            }
        }

        [Test]
        public void MSfx_20_BeatAdvanceDoesNotRestartActiveLoop()
        {
            var project = ProjectWithBeats(3);
            object cue = AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");
            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                int starts = CueStartCount(playback, (string)Field(cue, "cueId"));
                playback.AdvanceDialogue();
                playback.AdvanceDialogue();
                Assert.That(CueStartCount(playback, (string)Field(cue, "cueId")), Is.EqualTo(starts));
            }
        }

        [Test]
        public void MSfx_21_StoppingCueDoesNotRestartOrStopBgm()
        {
            var project = ProjectWithBeats(2);
            SetMusic(project.scenes[0], TrackBGuid);
            string stop = project.scenes[0].dialogueBeats[1].beatId;
            AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "BeatStart", stop);
            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                int musicStarts = playback.MusicStartCount;
                playback.AdvanceDialogue();
                Assert.That(playback.CurrentMusicAssetGuid, Is.EqualTo(TrackBGuid));
                Assert.That(playback.ActiveMusicSourceCount, Is.EqualTo(1));
                Assert.That(playback.MusicStartCount, Is.EqualTo(musicStarts));
            }
        }

        [Test]
        public void MSfx_22_BgmRefreshDoesNotDestroyValidCue()
        {
            var project = ProjectWithBeats(1);
            SetMusic(project.scenes[0], TrackBGuid);
            object cue = AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");
            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                AssertCueActive(playback, (string)Field(cue, "cueId"), true);
                project.scenes[0].music.volume = .4f;
                playback.RefreshCurrentMusic();
                AssertCueActive(playback, (string)Field(cue, "cueId"), true);
            }
        }

        [Test]
        public void MSfx_23_SceneExitRemovesOutgoingLoops()
        {
            var project = ProjectWithBeats(1);
            var second = NewSceneWithBeats(1);
            project.scenes.Add(second);
            object cue = AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");
            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayFromHere(0);
                AssertCueActive(playback, (string)Field(cue, "cueId"), true);
                playback.Next();
                playback.Advance(2f);
                AssertCueActive(playback, (string)Field(cue, "cueId"), false);
            }
        }

        [Test]
        public void MSfx_24_AnimatedTransitionDoesNotLeakOutgoingLoop()
        {
            var project = ProjectWithBeats(1);
            var second = NewSceneWithBeats(1);
            second.transition.sceneTransitionType = VnSceneComposerSceneTransitionType.DarkCurtain;
            second.transition.sceneTransitionDuration = .4f;
            project.scenes.Add(second);
            object cue = AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");
            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayFromHere(0);
                playback.Next();
                playback.Advance(.5f);
                AssertCueActive(playback, (string)Field(cue, "cueId"), false);
            }
        }

        [Test]
        public void MSfx_25_MissingAssetIsSafeAndDoesNotBreakBgm()
        {
            var project = ProjectWithBeats(1);
            SetMusic(project.scenes[0], TrackBGuid);
            AddCue(project.scenes[0], MissingGuid, true, "SceneStart", "", "SceneEnd", "");
            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                Assert.DoesNotThrow(() => playback.PlayScene(0));
                Assert.That(playback.CurrentMusicAssetGuid, Is.EqualTo(TrackBGuid));
                Assert.That(playback.ActiveMusicSourceCount, Is.EqualTo(1));
                Assert.That(IntProperty(playback, "ActiveAdditionalAudioSourceCount"), Is.EqualTo(0));
            }
        }

        [Test]
        public void MSfx_26_SaveReopenPreservesCueConfiguration()
        {
            var project = ProjectWithBeats(2);
            object cue = AddCue(project.scenes[0], TrackAGuid, true, "BeatStart",
                project.scenes[0].dialogueBeats[0].beatId, "BeatStart",
                project.scenes[0].dialogueBeats[1].beatId, "Ambience");
            SetField(cue, "displayName", "Rain");
            SetField(cue, "enabled", false);
            SetField(cue, "volume", .66f);
            SetField(cue, "startDelaySeconds", .8f);
            SetField(cue, "fadeInSeconds", .2f);
            SetField(cue, "fadeOutSeconds", .5f);

            var loaded = RoundTrip(project);
            object copy = CueList(loaded.scenes[0])[0];
            Assert.That((string)Field(copy, "displayName"), Is.EqualTo("Rain"));
            Assert.That((bool)Field(copy, "enabled"), Is.False);
            Assert.That((float)Field(copy, "volume"), Is.EqualTo(.66f).Within(.0001f));
            Assert.That((float)Field(copy, "startDelaySeconds"), Is.EqualTo(.8f).Within(.0001f));
            Assert.That(Field(copy, "category").ToString(), Is.EqualTo("Ambience"));
        }

        [Test]
        public void MSfx_27_DuplicateSceneRemapsCueAndBeatIdsIndependently()
        {
            var project = ProjectWithBeats(2);
            var source = project.scenes[0];
            object cue = AddCue(source, TrackAGuid, true, "BeatStart",
                source.dialogueBeats[0].beatId, "BeatStart", source.dialogueBeats[1].beatId);
            string sourceCueId = (string)Field(cue, "cueId");

            VnSceneComposerScene copy = VnSceneComposerEditing.DuplicateScene(project, source.sceneId);
            object copiedCue = CueList(copy)[0];

            Assert.That((string)Field(copiedCue, "cueId"), Is.Not.EqualTo(sourceCueId));
            Assert.That((string)Field(copiedCue, "startBeatId"), Is.EqualTo(copy.dialogueBeats[0].beatId));
            Assert.That((string)Field(copiedCue, "stopBeatId"), Is.EqualTo(copy.dialogueBeats[1].beatId));
            SetField(copiedCue, "volume", .1f);
            Assert.That((float)Field(cue, "volume"), Is.EqualTo(1f).Within(.0001f));
        }

        [Test]
        public void MSfx_28_UndoRestoresAddAndCueEdit()
        {
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                Undo.ClearAll();
                Invoke(window, "ComposerAddAdditionalAudioCue");
                Undo.FlushUndoRecordObjects();
                Assert.That(CueList(Project(window).scenes[0]).Count, Is.EqualTo(1));
                Undo.PerformUndo();
                Assert.That(CueList(Project(window).scenes[0]).Count, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MSfx_29_PlaySceneRestartHasNoGhostDuplicateCue()
        {
            var project = ProjectWithBeats(1);
            AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");
            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                playback.Restart();
                Assert.That(IntProperty(playback, "ActiveAdditionalAudioSourceCount"), Is.EqualTo(1));
            }
        }

        [Test]
        public void MSfx_30_PlayAllRepeatedSessionCleanupHasNoGhostCue()
        {
            var project = ProjectWithBeats(1);
            AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");
            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayAll();
                playback.PlayAll();
                Assert.That(IntProperty(playback, "ActiveAdditionalAudioSourceCount"), Is.EqualTo(1));
            }
        }

        [Test]
        public void MSfx_31_EditorUiExposesSeparateSoundsCollectionWithoutReplacingMusic()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Rokas", "Scripts", "Editor",
                "VnUiWorkshop", "VnPresentationWorkshopWindow.SceneComposer.cs"));
            string layers = File.ReadAllText(Path.Combine(Application.dataPath, "Rokas", "Scripts", "Editor",
                "VnUiWorkshop", "VnPresentationWorkshopWindow.SceneComposerAudioLayers.cs"));
            Assert.That(source, Does.Contain("\"Звуки\"").And.Contain("\"Музыка\""));
            Assert.That(layers, Does.Contain("\"+ Добавить звук\"")
                .And.Contain("ComposerAddAdditionalAudioCue")
                .And.Contain("ComposerDuplicateSelectedAdditionalAudioCue")
                .And.Contain("ComposerDeleteSelectedAdditionalAudioCue"));
        }

        [Test]
        public void MSfx_32_AudioAssetOnboardingReusesExistingMusicAudioClipPipeline()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Rokas", "Scripts", "Editor",
                "VnUiWorkshop", "VnPresentationWorkshopWindow.SceneComposerAudioLayers.cs"));
            Assert.That(source, Does.Contain("VnSceneComposerAssetPurpose.Music")
                .And.Contain("ComposerAddExternalAdditionalAudio"));
            string library = File.ReadAllText(Path.Combine(Application.dataPath, "Rokas", "Scripts", "Editor",
                "VnUiWorkshop", "VnSceneComposerAssetLibrary.cs"));
            Assert.That(library, Does.Contain("\".mp3\"").And.Contain("\".wav\"").And.Contain("AudioClip"));
        }


        [Test]
        public void MSfxContinuity_01_DefaultSceneLocalBehaviorIsUnchanged()
        {
            var project = ProjectWithBeats(1);
            var second = NewSceneWithBeats(1);
            project.scenes.Add(second);
            object rain = AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");
            string cueId = (string)Field(rain, "cueId");

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayAll();
                AssertCueActive(playback, cueId, true);
                playback.AdvanceDialogue();
                Assert.That(playback.CurrentSceneIndex, Is.EqualTo(1));
                AssertCueActive(playback, cueId, false);
            }
        }

        [Test]
        public void MSfxContinuity_02_IncomingSceneInheritsOneActiveLoop()
        {
            var project = ProjectWithBeats(1);
            var second = NewSceneWithBeats(1);
            second.keepPreviousAdditionalAudio = true;
            project.scenes.Add(second);
            object rain = AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");
            string cueId = (string)Field(rain, "cueId");

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayAll();
                int starts = CueStartCount(playback, cueId);
                playback.AdvanceDialogue();
                AssertCueActive(playback, cueId, true);
                Assert.That(CueStartCount(playback, cueId), Is.EqualTo(starts));
            }
        }

        [Test]
        public void MSfxContinuity_03_IncomingSceneInheritsMultipleActiveLoops()
        {
            var project = ProjectWithBeats(1);
            var second = NewSceneWithBeats(1);
            second.keepPreviousAdditionalAudio = true;
            project.scenes.Add(second);
            object rain = AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "", "Ambience");
            object radio = AddCue(project.scenes[0], TrackBGuid, true, "SceneStart", "", "SceneEnd", "", "MusicLayer");

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayAll();
                playback.AdvanceDialogue();
                AssertCueActive(playback, (string)Field(rain, "cueId"), true);
                AssertCueActive(playback, (string)Field(radio, "cueId"), true);
                Assert.That(playback.ActiveAdditionalAudioSourceCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void MSfxContinuity_04_InheritedLoopKeepsPlaybackIdentityAndElapsedState()
        {
            var project = ProjectWithBeats(1);
            var second = NewSceneWithBeats(1);
            second.keepPreviousAdditionalAudio = true;
            project.scenes.Add(second);
            object rain = AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");
            string cueId = (string)Field(rain, "cueId");

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayAll();
                playback.Advance(.25f);
                int sourceId = playback.GetAdditionalAudioCueSourceInstanceId(cueId);
                float elapsed = playback.GetAdditionalAudioCueElapsedSeconds(cueId);
                Assert.That(sourceId, Is.Not.EqualTo(0));
                Assert.That(elapsed, Is.GreaterThan(0f));

                playback.AdvanceDialogue();

                Assert.That(playback.GetAdditionalAudioCueSourceInstanceId(cueId), Is.EqualTo(sourceId));
                Assert.That(playback.GetAdditionalAudioCueElapsedSeconds(cueId), Is.EqualTo(elapsed).Within(.0001f));
                playback.Advance(.1f);
                Assert.That(playback.GetAdditionalAudioCueElapsedSeconds(cueId), Is.GreaterThan(elapsed));
            }
        }

        [Test]
        public void MSfxContinuity_05_InheritedLoopDoesNotRestart()
        {
            var project = ProjectWithBeats(1);
            var second = NewSceneWithBeats(1);
            second.keepPreviousAdditionalAudio = true;
            project.scenes.Add(second);
            object rain = AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");
            string cueId = (string)Field(rain, "cueId");

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayAll();
                Assert.That(CueStartCount(playback, cueId), Is.EqualTo(1));
                playback.AdvanceDialogue();
                Assert.That(CueStartCount(playback, cueId), Is.EqualTo(1));
            }
        }

        [Test]
        public void MSfxContinuity_06_InheritedLoopDoesNotReplayFadeIn()
        {
            var project = ProjectWithBeats(1);
            var second = NewSceneWithBeats(1);
            second.keepPreviousAdditionalAudio = true;
            project.scenes.Add(second);
            object rain = AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");
            SetField(rain, "fadeInSeconds", 1f);
            string cueId = (string)Field(rain, "cueId");

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayAll();
                playback.Advance(.4f);
                float before = playback.GetAdditionalAudioCueCurrentVolume(cueId);
                Assert.That(before, Is.GreaterThan(0f).And.LessThan(1f));

                playback.AdvanceDialogue();
                float afterBoundary = playback.GetAdditionalAudioCueCurrentVolume(cueId);
                Assert.That(afterBoundary, Is.EqualTo(before).Within(.0001f));

                playback.Advance(.1f);
                Assert.That(playback.GetAdditionalAudioCueCurrentVolume(cueId), Is.GreaterThan(afterBoundary));
                Assert.That(CueStartCount(playback, cueId), Is.EqualTo(1));
            }
        }

        [Test]
        public void MSfxContinuity_07_InheritedCueDoesNotDuplicate()
        {
            var project = ProjectWithBeats(1);
            var second = NewSceneWithBeats(1);
            second.keepPreviousAdditionalAudio = true;
            project.scenes.Add(second);
            object rain = AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");
            string cueId = (string)Field(rain, "cueId");

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayAll();
                int sourceId = playback.GetAdditionalAudioCueSourceInstanceId(cueId);
                playback.AdvanceDialogue();
                Assert.That(playback.ActiveAdditionalAudioSourceCount, Is.EqualTo(1));
                Assert.That(playback.GetAdditionalAudioCueSourceInstanceId(cueId), Is.EqualTo(sourceId));
            }
        }

        [Test]
        public void MSfxContinuity_08_NewLocalCueCanStartWhileInheritedCueContinues()
        {
            var project = ProjectWithBeats(1);
            var second = NewSceneWithBeats(1);
            second.keepPreviousAdditionalAudio = true;
            project.scenes.Add(second);
            object rain = AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "", "Ambience");
            object phone = AddCue(second, TrackBGuid, true, "SceneStart", "", "SceneEnd", "", "Sfx");

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayAll();
                playback.AdvanceDialogue();
                AssertCueActive(playback, (string)Field(rain, "cueId"), true);
                AssertCueActive(playback, (string)Field(phone, "cueId"), true);
                Assert.That(playback.ActiveAdditionalAudioSourceCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void MSfxContinuity_09_InheritedOneShotIsNotRetriggered()
        {
            var project = ProjectWithBeats(1);
            var second = NewSceneWithBeats(1);
            second.keepPreviousAdditionalAudio = true;
            project.scenes.Add(second);
            object cue = AddCue(project.scenes[0], TrackAGuid, false, "SceneStart", "", "Natural", "");
            string cueId = (string)Field(cue, "cueId");

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayAll();
                int sourceId = playback.GetAdditionalAudioCueSourceInstanceId(cueId);
                playback.AdvanceDialogue();
                Assert.That(CueStartCount(playback, cueId), Is.EqualTo(1));
                Assert.That(playback.GetAdditionalAudioCueSourceInstanceId(cueId), Is.EqualTo(sourceId));
            }
        }

        [Test]
        public void MSfxContinuity_10_SceneWithoutInheritanceUsesExistingFadeCleanup()
        {
            var project = ProjectWithBeats(1);
            var second = NewSceneWithBeats(1);
            project.scenes.Add(second);
            object cue = AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");
            SetField(cue, "fadeOutSeconds", .2f);
            string cueId = (string)Field(cue, "cueId");

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayAll();
                playback.AdvanceDialogue();
                AssertCueActive(playback, cueId, true);
                playback.Advance(.21f);
                AssertCueActive(playback, cueId, false);
            }
        }

        [Test]
        public void MSfxContinuity_11_ThreeSceneInheritanceChainStaysContinuous()
        {
            var project = ProjectWithBeats(1);
            var second = NewSceneWithBeats(1);
            var third = NewSceneWithBeats(1);
            second.keepPreviousAdditionalAudio = true;
            third.keepPreviousAdditionalAudio = true;
            project.scenes.Add(second);
            project.scenes.Add(third);
            object rain = AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");
            string cueId = (string)Field(rain, "cueId");

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayAll();
                int sourceId = playback.GetAdditionalAudioCueSourceInstanceId(cueId);
                playback.AdvanceDialogue();
                playback.AdvanceDialogue();
                Assert.That(playback.CurrentSceneIndex, Is.EqualTo(2));
                AssertCueActive(playback, cueId, true);
                Assert.That(CueStartCount(playback, cueId), Is.EqualTo(1));
                Assert.That(playback.GetAdditionalAudioCueSourceInstanceId(cueId), Is.EqualTo(sourceId));
            }
        }

        [Test]
        public void MSfxContinuity_12_DisablingInheritanceEndsInheritedSceneAudio()
        {
            var project = ProjectWithBeats(1);
            var second = NewSceneWithBeats(1);
            var third = NewSceneWithBeats(1);
            second.keepPreviousAdditionalAudio = true;
            third.keepPreviousAdditionalAudio = false;
            project.scenes.Add(second);
            project.scenes.Add(third);
            object rain = AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");
            string cueId = (string)Field(rain, "cueId");

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayAll();
                playback.AdvanceDialogue();
                AssertCueActive(playback, cueId, true);
                playback.AdvanceDialogue();
                AssertCueActive(playback, cueId, false);
            }
        }

        [Test]
        public void MSfxContinuity_13_PrimaryBgmKeepPreviousRemainsUnaffected()
        {
            var project = ProjectWithBeats(1);
            var second = NewSceneWithBeats(1);
            second.keepPreviousAdditionalAudio = true;
            second.music.mode = VnSceneComposerMusicMode.KeepPrevious;
            project.scenes.Add(second);
            SetMusic(project.scenes[0], TrackBGuid);
            object rain = AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayAll();
                int musicStarts = playback.MusicStartCount;
                playback.AdvanceDialogue();
                Assert.That(playback.CurrentMusicAssetGuid, Is.EqualTo(TrackBGuid));
                Assert.That(playback.MusicStartCount, Is.EqualTo(musicStarts));
                Assert.That(playback.ActiveMusicSourceCount, Is.EqualTo(1));
                AssertCueActive(playback, (string)Field(rain, "cueId"), true);
            }
        }

        [Test]
        public void MSfxContinuity_14_AudioInheritanceFlagDoesNotChangeMediaReference()
        {
            var project = ProjectWithBeats(1);
            var second = NewSceneWithBeats(1);
            second.keepPreviousAdditionalAudio = true;
            second.media.reference = "sentinel-media-reference";
            second.media.displayName = "Sentinel Media";
            second.media.contentHash = "sentinel-hash";
            project.scenes.Add(second);

            VnSceneComposerProject loaded = RoundTrip(project);
            Assert.That(loaded.scenes[1].media.kind, Is.EqualTo(VnSceneComposerMediaKind.None));
            Assert.That(loaded.scenes[1].media.reference, Is.EqualTo("sentinel-media-reference"));
            Assert.That(loaded.scenes[1].media.displayName, Is.EqualTo("Sentinel Media"));
            Assert.That(loaded.scenes[1].media.contentHash, Is.EqualTo("sentinel-hash"));
        }

        [Test]
        public void MSfxContinuity_15_BeatNavigationDoesNotRestartInheritedLoop()
        {
            var project = ProjectWithBeats(1);
            var second = NewSceneWithBeats(3);
            second.keepPreviousAdditionalAudio = true;
            project.scenes.Add(second);
            object rain = AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");
            string cueId = (string)Field(rain, "cueId");

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayAll();
                playback.AdvanceDialogue();
                Assert.That(playback.CurrentSceneIndex, Is.EqualTo(1));
                int starts = CueStartCount(playback, cueId);
                playback.AdvanceDialogue();
                playback.PreviousDialogue();
                playback.AdvanceDialogue();
                Assert.That(CueStartCount(playback, cueId), Is.EqualTo(starts));
                Assert.That(playback.ActiveAdditionalAudioSourceCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void MSfxContinuity_16_PlayAllRestartCleansPreviousSessionState()
        {
            var project = ProjectWithBeats(1);
            var second = NewSceneWithBeats(1);
            second.keepPreviousAdditionalAudio = true;
            project.scenes.Add(second);
            object rain = AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");
            string cueId = (string)Field(rain, "cueId");

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayAll();
                playback.AdvanceDialogue();
                Assert.That(playback.ActiveAdditionalAudioSourceCount, Is.EqualTo(1));
                Assert.That(CueStartCount(playback, cueId), Is.EqualTo(1));

                playback.PlayAll();
                Assert.That(playback.CurrentSceneIndex, Is.EqualTo(0));
                Assert.That(playback.ActiveAdditionalAudioSourceCount, Is.EqualTo(1));
                Assert.That(CueStartCount(playback, cueId), Is.EqualTo(2));

                playback.AdvanceDialogue();
                Assert.That(playback.ActiveAdditionalAudioSourceCount, Is.EqualTo(1));
                Assert.That(CueStartCount(playback, cueId), Is.EqualTo(2));
            }
        }

        [Test]
        public void MSfxContinuity_17_PreviousThenForwardDoesNotAccumulateInheritedSources()
        {
            var project = ProjectWithBeats(1);
            var second = NewSceneWithBeats(1);
            second.keepPreviousAdditionalAudio = true;
            project.scenes.Add(second);
            object rain = AddCue(project.scenes[0], TrackAGuid, true, "SceneStart", "", "SceneEnd", "");
            string cueId = (string)Field(rain, "cueId");

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayAll();
                playback.AdvanceDialogue();
                Assert.That(playback.ActiveAdditionalAudioSourceCount, Is.EqualTo(1));

                playback.Previous();
                Assert.That(playback.CurrentSceneIndex, Is.EqualTo(0));
                Assert.That(playback.ActiveAdditionalAudioSourceCount, Is.EqualTo(1));
                int startsAfterPrevious = CueStartCount(playback, cueId);

                playback.Next();
                Assert.That(playback.CurrentSceneIndex, Is.EqualTo(1));
                Assert.That(playback.ActiveAdditionalAudioSourceCount, Is.EqualTo(1));
                Assert.That(CueStartCount(playback, cueId), Is.EqualTo(startsAfterPrevious));
            }
        }

        [Test]
        public void MSfxContinuity_18_SaveReopenPreservesKeepPreviousSounds()
        {
            var project = ProjectWithBeats(1);
            var second = NewSceneWithBeats(1);
            second.keepPreviousAdditionalAudio = true;
            project.scenes.Add(second);

            VnSceneComposerProject loaded = RoundTrip(project);
            Assert.That(loaded.scenes[1].keepPreviousAdditionalAudio, Is.True);
        }

        [Test]
        public void MSfxContinuity_19_DuplicateSceneCopiesKeepPreviousSoundsSetting()
        {
            var project = ProjectWithBeats(1);
            project.scenes[0].keepPreviousAdditionalAudio = true;

            VnSceneComposerScene copy =
                VnSceneComposerEditing.DuplicateScene(project, project.scenes[0].sceneId);

            Assert.That(copy, Is.Not.Null);
            Assert.That(copy.keepPreviousAdditionalAudio, Is.True);
        }

        [Test]
        public void MSfxContinuity_20_UndoRestoresKeepPreviousSoundsSetting()
        {
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                Assert.That(Project(window).scenes[0].keepPreviousAdditionalAudio, Is.False);
                Undo.ClearAll();

                Invoke(window, "ComposerSetSelectedSceneKeepPreviousAdditionalAudio", true);
                Undo.FlushUndoRecordObjects();
                Assert.That(Project(window).scenes[0].keepPreviousAdditionalAudio, Is.True);

                Undo.PerformUndo();
                Assert.That(Project(window).scenes[0].keepPreviousAdditionalAudio, Is.False);
            }
            finally
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MSfxContinuity_21_UiExposesRussianKeepPreviousSoundsOption()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath, "Rokas", "Scripts", "Editor", "VnUiWorkshop",
                "VnPresentationWorkshopWindow.SceneComposerAudioLayers.cs"));
            Assert.That(source, Does.Contain(""Оставить предыдущие звуки"")
                .And.Contain("ComposerSetSelectedSceneKeepPreviousAdditionalAudio"));
        }

        private static VnSceneComposerProject ProjectWithBeats(int count)
        {
            var project = new VnSceneComposerProject();
            project.scenes.Add(NewSceneWithBeats(count));
            return project;
        }

        private static VnSceneComposerScene NewSceneWithBeats(int count)
        {
            var scene = new VnSceneComposerScene();
            scene.dialogueBeats.Clear();
            for (int i = 0; i < Math.Max(1, count); i++)
                scene.dialogueBeats.Add(new VnSceneComposerDialogueBeat
                {
                    speaker = "TEST",
                    text = "Beat " + (i + 1)
                });
            return scene;
        }

        private static object AddCue(
            VnSceneComposerScene scene, string guid, bool loop, string trigger, string startBeatId,
            string stopMode, string stopBeatId, string category = "Sfx")
        {
            Type cueType = RequireType("VnSceneComposerAdditionalAudioCue");
            object cue = Activator.CreateInstance(cueType);
            SetField(cue, "cueId", VnSceneComposerScene.NewStableId());
            SetField(cue, "displayName", "Cue");
            SetField(cue, "assetGuid", guid);
            SetField(cue, "enabled", true);
            SetField(cue, "category", Enum.Parse(RequireType("VnSceneComposerAudioCategory"), category));
            SetField(cue, "volume", 1f);
            SetField(cue, "loop", loop);
            SetField(cue, "trigger", Enum.Parse(RequireType("VnSceneComposerAudioTrigger"), trigger));
            SetField(cue, "startBeatId", startBeatId ?? string.Empty);
            SetField(cue, "startDelaySeconds", 0f);
            SetField(cue, "fadeInSeconds", 0f);
            SetField(cue, "fadeOutSeconds", 0f);
            SetField(cue, "stopMode", Enum.Parse(RequireType("VnSceneComposerAudioStopMode"), stopMode));
            SetField(cue, "stopBeatId", stopBeatId ?? string.Empty);
            CueList(scene).Add(cue);
            return cue;
        }

        private static IList CueList(VnSceneComposerScene scene)
        {
            FieldInfo field = typeof(VnSceneComposerScene).GetField(
                "additionalAudioCues", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Scene must expose independent additionalAudioCues.");
            return (IList)field.GetValue(scene);
        }

        private static Type RequireType(string name)
        {
            Type type = typeof(VnSceneComposerScene).Assembly.GetType(
                "Rokas.EditorTools.VnUiWorkshop." + name);
            Assert.That(type, Is.Not.Null, "Missing M-SFX type: " + name);
            return type;
        }

        private static void SetMusic(VnSceneComposerScene scene, string guid)
        {
            scene.music.mode = VnSceneComposerMusicMode.Track;
            scene.music.assetGuid = guid;
            scene.music.displayName = guid == TrackAGuid ? "Track A" : "Track B";
            scene.music.volume = .5f;
            scene.music.loop = true;
        }

        private static VnSceneComposerProject RoundTrip(VnSceneComposerProject project)
        {
            string json = VnSceneComposerSerialization.SerializePortable(project);
            VnSceneComposerImportResult loaded = VnSceneComposerSerialization.DeserializePortable(json);
            Assert.That(loaded.Success, Is.True, loaded.Error);
            return loaded.Project;
        }

        private static void AssertCueActive(VnSceneComposerPlaybackController playback, string cueId, bool expected)
        {
            MethodInfo method = typeof(VnSceneComposerPlaybackController).GetMethod(
                "IsAdditionalAudioCueActive", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            Assert.That((bool)method.Invoke(playback, new object[] { cueId }), Is.EqualTo(expected));
        }

        private static int CueStartCount(VnSceneComposerPlaybackController playback, string cueId)
        {
            MethodInfo method = typeof(VnSceneComposerPlaybackController).GetMethod(
                "GetAdditionalAudioCueStartCount", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            return (int)method.Invoke(playback, new object[] { cueId });
        }

        private static int IntProperty(object instance, string name)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, "Missing property: " + name);
            return (int)property.GetValue(instance);
        }

        private static object Field(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(
                name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing field: " + name);
            return field.GetValue(instance);
        }

        private static void SetField(object instance, string name, object value)
        {
            FieldInfo field = instance.GetType().GetField(
                name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing field: " + name);
            field.SetValue(instance, value);
        }

        private static object Invoke(object instance, string name, params object[] args)
        {
            MethodInfo method = instance.GetType().GetMethod(
                name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "Missing method: " + name);
            return method.Invoke(instance, args);
        }

        private static VnSceneComposerProject Project(VnPresentationWorkshopWindow window)
        {
            return (VnSceneComposerProject)Field(window, "_sceneComposerProject");
        }
    }
}
