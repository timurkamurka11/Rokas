using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerVideoReliabilityTests
    {
        private const string TrackAPath = "Assets/Rokas/Audio/05. Restaurant Prep.mp3";
        private const string TrackAGuid = "7540bd61684b4a768cad4691dbc54972";
        private const string TrackBPath = "Assets/Rokas/Audio/HomeNocturne.wav";
        private const string TrackBGuid = "e56430b08183503bb580307d23f0d2f5";

        private sealed class ReliabilityVideoPreview : VnSceneComposerVideoPreview
        {
            public readonly string Reference;
            public int PrepareCalls;
            public int PlayCalls;
            public int PauseCalls;
            public int RestartCalls;
            public int StopCalls;
            public int DisposeCalls;
            public bool Prepared = true;
            public bool Preparing;
            public bool Playing;
            public bool Visible = true;
            public bool FailPrepare;

            public ReliabilityVideoPreview(string reference, int serial)
                : base(null, 16, 16, false)
            {
                Reference = reference ?? string.Empty;
                warning = string.Empty;
                texture = new RenderTexture(64, 36, 0)
                {
                    name = "ROKAS_VideoReliability_" + serial
                };
                texture.Create();
            }

            public override bool IsPrepared { get { return Prepared; } }
            public override bool IsPreparing { get { return Preparing; } }
            public override bool IsPlaying { get { return Playing; } }
            public override bool HasVisibleFrame { get { return Visible; } }

            public override void Prepare()
            {
                PrepareCalls++;
                if (FailPrepare)
                {
                    warning = "transient prepare failure";
                    Prepared = false;
                    Preparing = false;
                    Visible = false;
                    return;
                }
                Preparing = !Prepared;
            }

            public override void Play()
            {
                PlayCalls++;
                if (!string.IsNullOrEmpty(warning))
                {
                    Playing = false;
                    return;
                }
                if (!Prepared)
                {
                    Preparing = true;
                    PrepareCalls++;
                    return;
                }
                Playing = true;
            }

            public override void Pause()
            {
                PauseCalls++;
                Playing = false;
            }

            public override void Restart()
            {
                RestartCalls++;
                Playing = Prepared && string.IsNullOrEmpty(warning);
            }

            public override void Stop()
            {
                StopCalls++;
                Playing = false;
                Preparing = false;
                Prepared = false;
                Visible = false;
            }

            public override void Dispose()
            {
                DisposeCalls++;
                Playing = false;
                base.Dispose();
            }
        }

        private sealed class ReliabilityVideoFactory : IVnSceneComposerVideoPreviewFactory
        {
            public readonly List<ReliabilityVideoPreview> Created = new List<ReliabilityVideoPreview>();
            public bool NewPreviewsPrepared = true;
            public bool NewPreviewsVisible = true;
            public bool NewPreviewsFailPrepare;

            public VnSceneComposerVideoPreview Open(VnSceneComposerMediaReference media, int width, int height)
            {
                var preview = new ReliabilityVideoPreview(
                    media != null ? media.reference : string.Empty, Created.Count)
                {
                    Prepared = NewPreviewsPrepared,
                    Preparing = false,
                    Visible = NewPreviewsVisible,
                    FailPrepare = NewPreviewsFailPrepare
                };
                Created.Add(preview);
                return preview;
            }
        }

        [Test]
        public void VR_01_RequestStateChangesGenerationPerRequest()
        {
            object state = NewRequestState();
            int a = BeginRequest(state, "A");
            int b = BeginRequest(state, "A");
            Assert.That(b, Is.GreaterThan(a));
            Assert.That(GetProperty<int>(state, "Generation"), Is.EqualTo(b));
        }

        [Test]
        public void VR_02_StalePreparedCannotSatisfyCurrentRequest()
        {
            object state = NewRequestState();
            int oldGeneration = BeginRequest(state, "A");
            int currentGeneration = BeginRequest(state, "B");
            Assert.That(InvokeBool(state, "AcceptPrepared", oldGeneration, "A"), Is.False);
            Assert.That(InvokeBool(state, "AcceptPrepared", currentGeneration, "B"), Is.True);
        }

        [Test]
        public void VR_03_StaleFrameCannotSatisfyCurrentRequest()
        {
            object state = NewRequestState();
            int oldGeneration = BeginRequest(state, "A");
            int currentGeneration = BeginRequest(state, "B");
            Assert.That(InvokeBool(state, "AcceptFrame", oldGeneration, "A"), Is.False);
            Assert.That(InvokeBool(state, "AcceptFrame", currentGeneration, "B"), Is.True);
        }

        [Test]
        public void VR_04_StaleErrorCannotCorruptCurrentRequest()
        {
            object state = NewRequestState();
            int oldGeneration = BeginRequest(state, "A");
            int currentGeneration = BeginRequest(state, "B");
            Assert.That(InvokeBool(state, "AcceptError", oldGeneration, "A"), Is.False);
            Assert.That(GetProperty<bool>(state, "Failed"), Is.False);
            Assert.That(InvokeBool(state, "AcceptPrepared", currentGeneration, "B"), Is.True);
            Assert.That(GetProperty<bool>(state, "Failed"), Is.False);
        }

        [Test]
        public void VR_05_InvalidationRejectsCallbacksFromStoppedRequest()
        {
            object state = NewRequestState();
            int generation = BeginRequest(state, "A");
            Invoke(state, "Invalidate");
            Assert.That(InvokeBool(state, "AcceptPrepared", generation, "A"), Is.False);
            Assert.That(InvokeBool(state, "AcceptFrame", generation, "A"), Is.False);
            Assert.That(InvokeBool(state, "AcceptError", generation, "A"), Is.False);
        }

        [Test]
        public void VR_06_FirstEntryIntoVideoSceneStartsCurrentPreview()
        {
            WithFactory(factory =>
            {
                var project = Project(Video("B", "B.mp4"));
                using (var controller = new VnSceneComposerPlaybackController(project))
                {
                    controller.PlayScene(0);
                    Assert.That(factory.Created, Has.Count.EqualTo(1));
                    Assert.That(factory.Created[0].PlayCalls, Is.EqualTo(1));
                    Assert.That(controller.CurrentSceneIndex, Is.EqualTo(0));
                }
            });
        }

        [Test]
        public void VR_07_ReenterSameVideoSceneAfterLeavingRecovers()
        {
            WithFactory(factory =>
            {
                var project = Project(Video("B", "B.mp4"), Plain("A"));
                using (var controller = new VnSceneComposerPlaybackController(project))
                {
                    controller.PlayScene(0);
                    ReliabilityVideoPreview first = factory.Created[0];
                    controller.Next();
                    controller.Previous();
                    Assert.That(controller.CurrentSceneIndex, Is.EqualTo(0));
                    Assert.That(factory.Created.Count, Is.GreaterThanOrEqualTo(2));
                    ReliabilityVideoPreview current = factory.Created[factory.Created.Count - 1];
                    Assert.That(current.Reference, Is.EqualTo("B.mp4"));
                    Assert.That(current.PlayCalls, Is.GreaterThanOrEqualTo(1));
                    Assert.That(first.DisposeCalls, Is.EqualTo(1));
                }
            });
        }

        [Test]
        public void VR_08_PlaySceneTwiceHealthySourceReusesUsablePreview()
        {
            WithFactory(factory =>
            {
                var project = Project(Video("B", "B.mp4"));
                using (var controller = new VnSceneComposerPlaybackController(project))
                {
                    controller.PlayScene(0);
                    controller.PlayScene(0);
                    Assert.That(factory.Created, Has.Count.EqualTo(1));
                    Assert.That(factory.Created[0].PlayCalls, Is.EqualTo(2));
                }
            });
        }

        [Test]
        public void VR_09_PlaySceneFiveTimesHealthySourceDoesNotGhostDuplicate()
        {
            WithFactory(factory =>
            {
                var project = Project(Video("B", "B.mp4"));
                using (var controller = new VnSceneComposerPlaybackController(project))
                {
                    for (int i = 0; i < 5; i++) controller.PlayScene(0);
                    Assert.That(factory.Created, Has.Count.EqualTo(1));
                    Assert.That(factory.Created[0].PlayCalls, Is.EqualTo(5));
                }
            });
        }

        [Test]
        public void VR_10_PlayAllTwiceReinitializesVideoSession()
        {
            WithFactory(factory =>
            {
                var project = Project(Video("B", "B.mp4"));
                using (var controller = new VnSceneComposerPlaybackController(project))
                {
                    controller.PlayAll();
                    controller.Pause();
                    controller.PlayAll();
                    Assert.That(controller.CurrentSceneIndex, Is.EqualTo(0));
                    Assert.That(factory.Created.Count, Is.EqualTo(1));
                    Assert.That(factory.Created[0].PlayCalls, Is.EqualTo(2));
                }
            });
        }

        [Test]
        public void VR_11_PreviousThenForwardReturnsToCorrectVideo()
        {
            WithFactory(factory =>
            {
                var project = Project(Plain("A"), Video("B", "B.mp4"));
                using (var controller = new VnSceneComposerPlaybackController(project))
                {
                    controller.PlayFromHere(0);
                    controller.Next();
                    Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1));
                    controller.Previous();
                    Assert.That(controller.CurrentSceneIndex, Is.EqualTo(0));
                    controller.Next();
                    Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1));
                    Assert.That(factory.Created[factory.Created.Count - 1].Reference, Is.EqualTo("B.mp4"));
                }
            });
        }

        [Test]
        public void VR_12_PlayFromHereIntoVideoStartsCorrectPreview()
        {
            WithFactory(factory =>
            {
                var project = Project(Plain("A"), Video("B", "B.mp4"));
                using (var controller = new VnSceneComposerPlaybackController(project))
                {
                    controller.PlayFromHere(1);
                    Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1));
                    Assert.That(factory.Created[factory.Created.Count - 1].Reference, Is.EqualTo("B.mp4"));
                    Assert.That(factory.Created[factory.Created.Count - 1].PlayCalls, Is.EqualTo(1));
                }
            });
        }

        [Test]
        public void VR_13_ImageToVideoOpensVideoTarget()
        {
            WithFactory(factory =>
            {
                var project = Project(Image("A"), Video("B", "B.mp4"));
                using (var controller = new VnSceneComposerPlaybackController(project))
                {
                    controller.PlayFromHere(0);
                    controller.Next();
                    Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1));
                    Assert.That(factory.Created[factory.Created.Count - 1].Reference, Is.EqualTo("B.mp4"));
                }
            });
        }

        [Test]
        public void VR_14_VideoToImageReleasesVideoTarget()
        {
            WithFactory(factory =>
            {
                var project = Project(Video("A", "A.mp4"), Image("B"));
                using (var controller = new VnSceneComposerPlaybackController(project))
                {
                    controller.PlayFromHere(0);
                    ReliabilityVideoPreview first = factory.Created[0];
                    controller.Next();
                    Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1));
                    Assert.That(first.DisposeCalls, Is.EqualTo(1));
                }
            });
        }

        [Test]
        public void VR_15_VideoAToVideoBUsesCorrectSource()
        {
            WithFactory(factory =>
            {
                var project = Project(Video("A", "A.mp4"), Video("B", "B.mp4"));
                using (var controller = new VnSceneComposerPlaybackController(project))
                {
                    controller.PlayFromHere(0);
                    controller.Next();
                    Assert.That(factory.Created[factory.Created.Count - 1].Reference, Is.EqualTo("B.mp4"));
                }
            });
        }

        [Test]
        public void VR_16_VideoAToVideoAKeepsCorrectSourceOnLegacyBoundary()
        {
            WithFactory(factory =>
            {
                var project = Project(Video("A1", "A.mp4"), Video("A2", "A.mp4"));
                using (var controller = new VnSceneComposerPlaybackController(project))
                {
                    controller.PlayFromHere(0);
                    controller.Next();
                    Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1));
                    Assert.That(factory.Created.Count, Is.GreaterThanOrEqualTo(1));
                    Assert.That(factory.Created[factory.Created.Count - 1].Reference, Is.EqualTo("A.mp4"));
                    Assert.That(factory.Created[factory.Created.Count - 1].PlayCalls, Is.GreaterThanOrEqualTo(1));
                }
            });
        }

        [Test]
        public void VR_17_VideoImageSameVideoReopensCorrectSource()
        {
            WithFactory(factory =>
            {
                var project = Project(Video("A1", "A.mp4"), Image("I"), Video("A2", "A.mp4"));
                using (var controller = new VnSceneComposerPlaybackController(project))
                {
                    controller.PlayFromHere(0);
                    controller.Next();
                    controller.Next();
                    Assert.That(controller.CurrentSceneIndex, Is.EqualTo(2));
                    Assert.That(factory.Created[factory.Created.Count - 1].Reference, Is.EqualTo("A.mp4"));
                    Assert.That(factory.Created.Count, Is.GreaterThanOrEqualTo(2));
                }
            });
        }

        [Test]
        public void VR_18_SourceChangeInvalidatesOldPreview()
        {
            WithFactory(factory =>
            {
                var scene = Video("A", "A.mp4");
                var project = Project(scene);
                using (var controller = new VnSceneComposerPlaybackController(project))
                {
                    controller.PlayScene(0);
                    ReliabilityVideoPreview first = factory.Created[0];
                    scene.media.reference = "B.mp4";
                    scene.media.contentHash = "B";
                    controller.PlayScene(0);
                    Assert.That(factory.Created.Count, Is.EqualTo(2));
                    Assert.That(factory.Created[1].Reference, Is.EqualTo("B.mp4"));
                    Assert.That(first.DisposeCalls, Is.EqualTo(1));
                }
            });
        }

        [Test]
        public void VR_19_FailedPreviewRecoversOnNextLegitimatePlayScene()
        {
            WithFactory(factory =>
            {
                var project = Project(Video("A", "A.mp4"));
                using (var controller = new VnSceneComposerPlaybackController(project))
                {
                    controller.PlayScene(0);
                    ReliabilityVideoPreview failed = factory.Created[0];
                    failed.warning = "transient player error";
                    failed.Prepared = false;
                    failed.Visible = false;
                    failed.Playing = false;

                    controller.PlayScene(0);

                    Assert.That(factory.Created.Count, Is.EqualTo(2),
                        "An unhealthy preview must not be reused forever.");
                    Assert.That(factory.Created[1].Reference, Is.EqualTo("A.mp4"));
                    Assert.That(factory.Created[1].PlayCalls, Is.EqualTo(1));
                }
            });
        }

        [Test]
        public void VR_20_OrdinaryBeatAdvanceDoesNotReprepareUnchangedVideo()
        {
            WithFactory(factory =>
            {
                var scene = Video("A", "A.mp4");
                scene.dialogueBeats.Clear();
                scene.dialogueBeats.Add(new VnSceneComposerDialogueBeat { speaker = "A", text = "1" });
                scene.dialogueBeats.Add(new VnSceneComposerDialogueBeat { speaker = "A", text = "2" });
                scene.dialogueBeats.Add(new VnSceneComposerDialogueBeat { speaker = "A", text = "3" });
                var project = Project(scene);
                using (var controller = new VnSceneComposerPlaybackController(project))
                {
                    controller.PlayScene(0);
                    ReliabilityVideoPreview preview = factory.Created[0];
                    int prepareCalls = preview.PrepareCalls;
                    int playCalls = preview.PlayCalls;
                    controller.AdvanceDialogue();
                    controller.AdvanceDialogue();
                    Assert.That(preview.PrepareCalls, Is.EqualTo(prepareCalls));
                    Assert.That(preview.PlayCalls, Is.EqualTo(playCalls));
                    Assert.That(factory.Created.Count, Is.EqualTo(1));
                }
            });
        }

        [Test]
        public void VR_21_CinematicRevealWaitsForCurrentVisibleFrame()
        {
            WithFactory(factory =>
            {
                factory.NewPreviewsPrepared = false;
                factory.NewPreviewsVisible = false;
                var incoming = Video("B", "B.mp4");
                incoming.transition.sceneTransitionType = VnSceneComposerSceneTransitionType.DarkCurtain;
                incoming.transition.sceneTransitionDuration = .4f;
                var project = Project(Plain("A"), incoming);
                using (var controller = new VnSceneComposerPlaybackController(project))
                {
                    controller.PlayFromHere(0);
                    controller.Next();
                    controller.Advance(.21f);
                    Assert.That(controller.IsSceneTransitionActive, Is.True);
                    ReliabilityVideoPreview target = factory.Created[factory.Created.Count - 1];
                    Assert.That(target.PrepareCalls, Is.EqualTo(1));

                    controller.Advance(1f);
                    Assert.That(controller.IsSceneTransitionActive, Is.True);

                    target.Prepared = true;
                    target.Preparing = false;
                    target.Visible = true;
                    controller.Advance(.01f);
                    controller.Advance(.25f);
                    Assert.That(controller.IsSceneTransitionActive, Is.False);
                }
            });
        }

        [Test]
        public void VR_22_VideoRecoveryDoesNotRestartPrimaryBgm()
        {
            WithFactory(factory =>
            {
                var scene = Video("A", "A.mp4");
                SetMusic(scene, TrackBGuid);
                var project = Project(scene);
                using (var controller = new VnSceneComposerPlaybackController(project))
                {
                    controller.PlayScene(0);
                    int starts = controller.MusicStartCount;
                    ReliabilityVideoPreview preview = factory.Created[0];
                    preview.warning = "transient";
                    InvokeControllerRecovery(controller);
                    Assert.That(controller.MusicStartCount, Is.EqualTo(starts));
                    Assert.That(controller.CurrentMusicAssetGuid, Is.EqualTo(TrackBGuid));
                }
            });
        }

        [Test]
        public void VR_23_VideoRecoveryDoesNotKillOrRetriggerLayeredAudio()
        {
            WithFactory(factory =>
            {
                var scene = Video("A", "A.mp4");
                var cue = new VnSceneComposerAdditionalAudioCue
                {
                    displayName = "Rain",
                    assetGuid = TrackAGuid,
                    enabled = true,
                    category = VnSceneComposerAudioCategory.Ambience,
                    volume = .5f,
                    loop = true,
                    trigger = VnSceneComposerAudioTrigger.SceneStart,
                    stopMode = VnSceneComposerAudioStopMode.SceneEnd
                };
                scene.additionalAudioCues.Add(cue);
                var project = Project(scene);
                using (var controller = new VnSceneComposerPlaybackController(project))
                {
                    controller.PlayScene(0);
                    int starts = controller.GetAdditionalAudioCueStartCount(cue.cueId);
                    Assert.That(controller.IsAdditionalAudioCueActive(cue.cueId), Is.True);
                    factory.Created[0].warning = "transient";

                    InvokeControllerRecovery(controller);

                    Assert.That(controller.GetAdditionalAudioCueStartCount(cue.cueId), Is.EqualTo(starts));
                    Assert.That(controller.IsAdditionalAudioCueActive(cue.cueId), Is.True);
                }
            });
        }

        [Test]
        public void VR_24_VideoRecoveryDoesNotResetBeatCharacterOrDecorations()
        {
            WithFactory(factory =>
            {
                var scene = Video("A", "A.mp4");
                scene.dialogueBeats.Clear();
                scene.dialogueBeats.Add(new VnSceneComposerDialogueBeat { speaker = "Mina", text = "1" });
                scene.dialogueBeats.Add(new VnSceneComposerDialogueBeat { speaker = "Mina", text = "2" });
                scene.characters.Add(new VnSceneComposerCharacter
                {
                    characterId = "Mina",
                    stateId = "mina_neutral",
                    stageSlot = VnWorkshopStageSlot.Center
                });
                scene.decorations.Add(new VnSceneComposerDecoration { displayName = "Keep Me" });
                var project = Project(scene);
                using (var controller = new VnSceneComposerPlaybackController(project))
                {
                    controller.PlayScene(0);
                    controller.AdvanceDialogue();
                    Assert.That(controller.CurrentBeatIndex, Is.EqualTo(1));
                    factory.Created[0].warning = "transient";

                    InvokeControllerRecovery(controller);

                    Assert.That(controller.CurrentBeatIndex, Is.EqualTo(1));
                    Assert.That(scene.characters, Has.Count.EqualTo(1));
                    Assert.That(scene.characters[0].stateId, Is.EqualTo("mina_neutral"));
                    Assert.That(scene.decorations, Has.Count.EqualTo(1));
                    Assert.That(scene.decorations[0].displayName, Is.EqualTo("Keep Me"));
                }
            });
        }

        [Test]
        public void VR_25_StuckPrepareCannotLockCinematicTransitionForever()
        {
            WithFactory(factory =>
            {
                factory.NewPreviewsPrepared = false;
                factory.NewPreviewsVisible = false;
                var incoming = Video("B", "B.mp4");
                incoming.transition.sceneTransitionType = VnSceneComposerSceneTransitionType.DarkCurtain;
                incoming.transition.sceneTransitionDuration = .4f;
                var project = Project(Plain("A"), incoming);
                using (var controller = new VnSceneComposerPlaybackController(project))
                {
                    controller.PlayFromHere(0);
                    controller.Next();
                    controller.Advance(.21f);
                    Assert.That(controller.IsSceneTransitionActive, Is.True);
                    controller.Advance(9f);
                    Assert.That(controller.IsSceneTransitionActive, Is.False,
                        "A stuck Prepare must fail safe instead of locking the curtain forever.");
                    Assert.That(controller.SceneTransitionInputLocked, Is.False);
                    Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1));
                }
            });
        }

        [Test]
        public void VR_26_RenderTargetRecoveryUsesRealStateAndNoFakeDelay()
        {
            string path = Path.Combine(Application.dataPath, "Rokas", "Scripts", "Editor",
                "VnUiWorkshop", "VnSceneComposerMotionMediaEditing.cs");
            string source = File.ReadAllText(path);
            Assert.That(source, Does.Contain("requestGeneration"),
                "Video preview needs a request generation/token.");
            Assert.That(source, Does.Contain("sourceIdentity"),
                "Video callbacks must be tied to the requested source identity.");
            Assert.That(source, Does.Contain("texture.IsCreated()"),
                "RenderTexture validity must be checked before reuse.");
            Assert.That(source, Does.Contain("player.targetTexture = texture"),
                "Recovery must explicitly restore the VideoPlayer RenderTexture binding.");
            Assert.That(source, Does.Not.Contain("WaitForSeconds(1)")
                .And.Not.Contain("WaitForSeconds(2)"),
                "Video reliability must not be implemented with arbitrary fixed sleeps.");
        }

        private static object NewRequestState()
        {
            Type type = typeof(VnSceneComposerPlaybackController).Assembly.GetType(
                "Rokas.EditorTools.VnUiWorkshop.VnSceneComposerVideoRequestState");
            Assert.That(type, Is.Not.Null, "Missing authoritative video request state.");
            return Activator.CreateInstance(type, true);
        }

        private static int BeginRequest(object state, string source)
        {
            return (int)Invoke(state, "Begin", source);
        }

        private static void InvokeControllerRecovery(VnSceneComposerPlaybackController controller)
        {
            MethodInfo method = typeof(VnSceneComposerPlaybackController).GetMethod(
                "TryRecoverCurrentVideoPlayback",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(method, Is.Not.Null,
                "Playback controller needs narrow video-only recovery without whole-controller recreation.");
            method.Invoke(controller, null);
        }

        private static object Invoke(object instance, string name, params object[] args)
        {
            MethodInfo method = instance.GetType().GetMethod(
                name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Missing method: " + instance.GetType().Name + "." + name);
            return method.Invoke(instance, args);
        }

        private static bool InvokeBool(object instance, string name, params object[] args)
        {
            return (bool)Invoke(instance, name, args);
        }

        private static T GetProperty<T>(object instance, string name)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, "Missing property: " + instance.GetType().Name + "." + name);
            return (T)property.GetValue(instance);
        }

        private static void WithFactory(Action<ReliabilityVideoFactory> action)
        {
            var factory = new ReliabilityVideoFactory();
            VnSceneComposerMediaEditing.VideoPreviewFactory = factory;
            try { action(factory); }
            finally
            {
                VnSceneComposerMediaEditing.ResetVideoPreviewFactory();
                for (int i = 0; i < factory.Created.Count; i++)
                {
                    ReliabilityVideoPreview preview = factory.Created[i];
                    if (preview != null && preview.texture != null) preview.Dispose();
                }
            }
        }

        private static VnSceneComposerProject Project(params VnSceneComposerScene[] scenes)
        {
            var project = new VnSceneComposerProject();
            project.scenes.Clear();
            if (scenes != null) project.scenes.AddRange(scenes);
            return project;
        }

        private static VnSceneComposerScene Plain(string label)
        {
            return new VnSceneComposerScene { label = label };
        }

        private static VnSceneComposerScene Image(string label)
        {
            return new VnSceneComposerScene
            {
                label = label,
                media = new VnSceneComposerMediaReference
                {
                    kind = VnSceneComposerMediaKind.ExternalImage,
                    reference = "missing-image-for-routing.png",
                    displayName = "image.png",
                    scaleMode = VnSceneComposerMediaScaleMode.Fit
                }
            };
        }

        private static VnSceneComposerScene Video(string label, string reference)
        {
            return new VnSceneComposerScene
            {
                label = label,
                media = new VnSceneComposerMediaReference
                {
                    kind = VnSceneComposerMediaKind.ExternalVideo,
                    reference = reference,
                    displayName = reference,
                    contentHash = reference,
                    localPreviewDependency = true,
                    loop = true,
                    scaleMode = VnSceneComposerMediaScaleMode.Fit
                },
                timing = new VnSceneComposerTiming
                {
                    previewAdvanceMode = VnSceneComposerPreviewAdvanceMode.ManualBeat,
                    previewAutoDuration = 2f
                }
            };
        }

        private static void SetMusic(VnSceneComposerScene scene, string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            Assert.That(clip, Is.Not.Null, "Expected repository BGM fixture is missing.");
            scene.music.mode = VnSceneComposerMusicMode.Track;
            scene.music.assetGuid = guid;
            scene.music.displayName = clip.name;
            scene.music.volume = .5f;
            scene.music.loop = true;
            scene.music.fadeInSeconds = 0f;
            scene.music.fadeOutSeconds = 0f;
        }
    }
}
