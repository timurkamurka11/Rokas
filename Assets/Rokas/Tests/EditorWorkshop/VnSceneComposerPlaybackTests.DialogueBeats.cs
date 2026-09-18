using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed partial class VnSceneComposerPlaybackTests
    {
        [Test]
        public void MD_CanonicalDialogueModelUsesSchemaTwoAndOneInitialBeat()
        {
            Type contractType = RequireType("VnSceneComposerContract");
            FieldInfo schema = contractType.GetField("SchemaVersion", BindingFlags.Public | BindingFlags.Static);
            Assert.That(schema, Is.Not.Null);
            Assert.That(schema.GetRawConstantValue(), Is.EqualTo(2),
                "M-DIALOGUE portable data must move to canonical schema v2.");

            Type sceneType = RequireType("VnSceneComposerScene");
            FieldInfo beatsField = sceneType.GetField("dialogueBeats", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(beatsField, Is.Not.Null,
                "A Scene must own one canonical ordered dialogueBeats list.");

            object scene = Activator.CreateInstance(sceneType);
            IList beats = beatsField.GetValue(scene) as IList;
            Assert.That(beats, Is.Not.Null.And.Count.EqualTo(1),
                "A new Scene should immediately expose one editable dialogue beat.");

            object beat = beats[0];
            Assert.That((string)Get(beat, "beatId"), Has.Length.EqualTo(32));
            Assert.That((string)Get(beat, "speaker"), Is.EqualTo(string.Empty));
            Assert.That((string)Get(beat, "text"), Is.EqualTo(string.Empty));
            Assert.That((bool)Get(beat, "narration"), Is.False);

            Assert.That(sceneType.GetField("speaker", BindingFlags.Public | BindingFlags.Instance), Is.Null,
                "Legacy single-dialogue fields must not remain independent serialized authorities in schema v2.");
            Assert.That(sceneType.GetField("previewText", BindingFlags.Public | BindingFlags.Instance), Is.Null);
            Assert.That(sceneType.GetField("narration", BindingFlags.Public | BindingFlags.Instance), Is.Null);
        }

        [Test]
        public void MD_BeatAdvanceKeepsSceneAndVideoResourceAlive()
        {
            Type controllerType = RequirePlaybackType();
            PropertyInfo beatIndex = controllerType.GetProperty(
                "CurrentBeatIndex", BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo beatElapsed = controllerType.GetProperty(
                "BeatElapsedSeconds", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo advanceDialogue = controllerType.GetMethod(
                "AdvanceDialogue", BindingFlags.Public | BindingFlags.Instance,
                null, Type.EmptyTypes, null);

            Assert.That(beatIndex, Is.Not.Null,
                "Task 4 requires controller-owned transient CurrentBeatIndex.");
            Assert.That(beatElapsed, Is.Not.Null,
                "Task 4 requires a separate transient BeatElapsedSeconds clock.");
            Assert.That(advanceDialogue, Is.Not.Null,
                "Task 4 requires AdvanceDialogue as the single same-scene beat progression authority.");

            var factory = new FakeVideoFactory();
            VnSceneComposerMediaEditing.VideoPreviewFactory = factory;
            var project = new VnSceneComposerProject();
            VnPresentationWorkshopVn10Resolver.SetTypewriterPreviewOverrides(
                project.defaultPresentation, false, 36f, 0f, .10f, .24f, .36f, .20f, .20f, .04f);

            VnSceneComposerScene scene = VideoScene("Multi Beat", "same-scene.mp4", true);
            scene.dialogueBeats[0].speaker = "Mina";
            scene.dialogueBeats[0].text = "Beat A";
            scene.dialogueBeats.Add(new VnSceneComposerDialogueBeat
            {
                speaker = "Mina",
                text = "Beat B"
            });
            AddCharacter(scene, "Mina", "mina_neutral", "Center");
            ConfigureCharacterTransition(scene, "SlideAndFade", 1f, 1f, 180f);
            project.scenes.Add(scene);

            VnSceneComposerPlaybackController controller = null;
            try
            {
                controller = new VnSceneComposerPlaybackController(project);
                controller.PlayScene(0);
                controller.Advance(1f);

                Assert.That(factory.Created.Count, Is.EqualTo(1));
                FakeVideoPreview active = factory.Created[0];
                Texture texture = controller.CurrentMediaTexture;
                int prepareCalls = active.PrepareCalls;
                int disposeCalls = active.DisposeCalls;
                int restartCalls = active.RestartCalls;
                float sceneElapsed = controller.SceneElapsedSeconds;
                float mediaTime = controller.MediaTimeSeconds;
                VnWorkshopPreviewCharacter beforeCharacter = controller.CurrentFrame.ComposerCharacters[0];
                Rect beforeBody = beforeCharacter.Body;
                float beforeAlpha = beforeCharacter.Alpha;

                advanceDialogue.Invoke(controller, null);

                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(0));
                Assert.That((int)beatIndex.GetValue(controller, null), Is.EqualTo(1));
                Assert.That((float)beatElapsed.GetValue(controller, null), Is.EqualTo(0f).Within(.0001f));
                Assert.That(controller.SceneElapsedSeconds, Is.EqualTo(sceneElapsed).Within(.0001f),
                    "Same-scene beat advance must preserve the Scene presentation clock.");
                Assert.That(controller.MediaTimeSeconds, Is.EqualTo(mediaTime).Within(.0001f),
                    "Same-scene beat advance must preserve the media timeline.");
                Assert.That(factory.Created.Count, Is.EqualTo(1),
                    "Same-scene beat advance must retain the existing VideoPreview resource.");
                Assert.That(active.PrepareCalls, Is.EqualTo(prepareCalls),
                    "Same-scene beat advance must not issue Prepare.");
                Assert.That(active.DisposeCalls, Is.EqualTo(disposeCalls),
                    "Same-scene beat advance must not dispose the active video.");
                Assert.That(active.RestartCalls, Is.EqualTo(restartCalls),
                    "Same-scene beat advance must not restart the active video.");
                Assert.That(controller.CurrentMediaTexture, Is.SameAs(texture));
                Assert.That(controller.CurrentFrame.TargetBackground, Is.SameAs(texture));
                Assert.That(controller.CurrentFrame.Dialogue, Is.EqualTo("Beat B"));

                VnWorkshopPreviewCharacter afterCharacter = controller.CurrentFrame.ComposerCharacters[0];
                Assert.That(afterCharacter.Body, Is.EqualTo(beforeBody),
                    "Beat advance must not replay CharacterEnter or Scene-stage movement.");
                Assert.That(afterCharacter.Alpha, Is.EqualTo(beforeAlpha).Within(.001f),
                    "Beat advance must preserve the completed Scene presentation state.");
            }
            finally
            {
                if (controller != null) controller.Dispose();
                VnSceneComposerMediaEditing.ResetVideoPreviewFactory();
                DisposeFakePreviews(factory);
            }
        }

        [Test]
        public void MD_FinalBeatAdvancePerformsOneRealOrderedSceneBoundary()
        {
            Type controllerType = RequirePlaybackType();
            PropertyInfo beatIndex = controllerType.GetProperty(
                "CurrentBeatIndex", BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo beatElapsed = controllerType.GetProperty(
                "BeatElapsedSeconds", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo advanceDialogue = controllerType.GetMethod(
                "AdvanceDialogue", BindingFlags.Public | BindingFlags.Instance,
                null, Type.EmptyTypes, null);

            Assert.That(beatIndex, Is.Not.Null);
            Assert.That(beatElapsed, Is.Not.Null);
            Assert.That(advanceDialogue, Is.Not.Null);

            var project = new VnSceneComposerProject();
            VnSceneComposerScene scene0 = new VnSceneComposerScene
            {
                label = "Scene 0",
                timing = new VnSceneComposerTiming
                {
                    previewAdvanceMode = VnSceneComposerPreviewAdvanceMode.ManualBeat,
                    previewAutoDuration = 2f
                }
            };
            scene0.dialogueBeats[0].speaker = "Aiko";
            scene0.dialogueBeats[0].text = "Beat A";
            scene0.dialogueBeats.Add(new VnSceneComposerDialogueBeat
            {
                speaker = "Tim",
                text = "Beat B"
            });

            VnSceneComposerScene scene1 = new VnSceneComposerScene
            {
                label = "Scene 1",
                timing = new VnSceneComposerTiming
                {
                    previewAdvanceMode = VnSceneComposerPreviewAdvanceMode.ManualBeat,
                    previewAutoDuration = 2f
                }
            };
            scene1.dialogueBeats[0].speaker = "Aiko";
            scene1.dialogueBeats[0].text = "Beat C";
            project.scenes.Add(scene0);
            project.scenes.Add(scene1);

            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayAll();
                controller.Advance(.5f);
                float sceneElapsedBeforeBeat = controller.SceneElapsedSeconds;

                advanceDialogue.Invoke(controller, null);

                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(0),
                    "A non-final Beat must not become a Scene boundary.");
                Assert.That((int)beatIndex.GetValue(controller, null), Is.EqualTo(1));
                Assert.That(controller.SceneElapsedSeconds, Is.EqualTo(sceneElapsedBeforeBeat).Within(.0001f));
                Assert.That((float)beatElapsed.GetValue(controller, null), Is.EqualTo(0f).Within(.0001f));

                advanceDialogue.Invoke(controller, null);

                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1),
                    "Only the final Beat may perform the real ordered Scene boundary.");
                Assert.That((int)beatIndex.GetValue(controller, null), Is.EqualTo(0));
                Assert.That(controller.SceneElapsedSeconds, Is.EqualTo(0f).Within(.0001f));
                Assert.That((float)beatElapsed.GetValue(controller, null), Is.EqualTo(0f).Within(.0001f));
            }
        }

        [Test]
        public void MD_PreviewAutoDurationAdvancesBeatsBeforeSceneBoundary()
        {
            var project = new VnSceneComposerProject();
            SetAutoPreviewSequenceGap(project, .25f);
            project.scenes.Add(MdScene("Auto 0", VnSceneComposerPreviewAdvanceMode.PreviewAutoDuration, .20f,
                "Beat A", "Beat B"));
            project.scenes.Add(MdScene("Auto 1", VnSceneComposerPreviewAdvanceMode.PreviewAutoDuration, .20f,
                "Beat C"));

            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayAll();
                controller.Advance(.21f);

                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(0),
                    "Auto timing must advance the active Beat before crossing the real Scene boundary.");
                Assert.That(controller.CurrentBeatIndex, Is.EqualTo(1));
                Assert.That(controller.BeatElapsedSeconds, Is.EqualTo(0f).Within(.0001f),
                    "Starting the next Beat resets only the Beat clock.");
                Assert.That(controller.SceneElapsedSeconds, Is.GreaterThan(0f),
                    "Starting the next Beat must preserve the Scene presentation clock.");

                controller.Advance(.21f);
                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(0),
                    "sequenceGap belongs only after the final Beat before a real Scene boundary.");
                Assert.That(controller.CurrentBeatIndex, Is.EqualTo(1));

                controller.Advance(.25f);
                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1));
                Assert.That(controller.CurrentBeatIndex, Is.EqualTo(0));
            }
        }

        [Test]
        public void MD_ManualBeatNeverAutoAdvancesDialogue()
        {
            var project = new VnSceneComposerProject();
            project.scenes.Add(MdScene("Manual", VnSceneComposerPreviewAdvanceMode.ManualBeat, 1f,
                "Beat A", "Beat B"));

            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayScene(0);
                controller.Advance(100f);

                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(0));
                Assert.That(controller.CurrentBeatIndex, Is.EqualTo(0),
                    "ManualBeat must never advance dialogue from elapsed time.");
                Assert.That(controller.BeatElapsedSeconds, Is.EqualTo(100f).Within(.0001f));
            }
        }

        [Test]
        public void MD_PlaySceneStopsAfterFinalBeat()
        {
            var project = new VnSceneComposerProject();
            project.scenes.Add(MdScene("Single scope", VnSceneComposerPreviewAdvanceMode.ManualBeat, 1f,
                "Beat A", "Beat B"));

            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayScene(0);
                controller.AdvanceDialogue();
                Assert.That(controller.CurrentBeatIndex, Is.EqualTo(1));
                Assert.That(controller.IsPlaying, Is.True);

                controller.AdvanceDialogue();
                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(0));
                Assert.That(controller.CurrentBeatIndex, Is.EqualTo(1));
                Assert.That(controller.IsPlaying, Is.False,
                    "Play Scene must stop after its final Beat instead of navigating to another Scene.");
            }
        }

        [Test]
        public void MD_PlayAllAdvancesAllBeatsThenNextScene()
        {
            var project = new VnSceneComposerProject();
            project.scenes.Add(MdScene("Scene 0", VnSceneComposerPreviewAdvanceMode.ManualBeat, 1f,
                "0A", "0B"));
            project.scenes.Add(MdScene("Scene 1", VnSceneComposerPreviewAdvanceMode.ManualBeat, 1f,
                "1A", "1B"));

            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayAll();
                controller.AdvanceDialogue();
                Assert.That((controller.CurrentSceneIndex, controller.CurrentBeatIndex), Is.EqualTo((0, 1)));

                controller.AdvanceDialogue();
                Assert.That((controller.CurrentSceneIndex, controller.CurrentBeatIndex), Is.EqualTo((1, 0)));

                controller.AdvanceDialogue();
                Assert.That((controller.CurrentSceneIndex, controller.CurrentBeatIndex), Is.EqualTo((1, 1)));

                controller.AdvanceDialogue();
                Assert.That((controller.CurrentSceneIndex, controller.CurrentBeatIndex), Is.EqualTo((1, 1)));
                Assert.That(controller.IsPlaying, Is.False);
            }
        }

        [Test]
        public void MD_PlayFromHereStartsSelectedSceneAtBeatZero()
        {
            var project = new VnSceneComposerProject();
            project.scenes.Add(MdScene("Scene 0", VnSceneComposerPreviewAdvanceMode.ManualBeat, 1f, "0A", "0B"));
            project.scenes.Add(MdScene("Scene 1", VnSceneComposerPreviewAdvanceMode.ManualBeat, 1f, "1A", "1B"));
            project.scenes.Add(MdScene("Scene 2", VnSceneComposerPreviewAdvanceMode.ManualBeat, 1f, "2A"));

            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayScene(0);
                controller.AdvanceDialogue();
                Assert.That(controller.CurrentBeatIndex, Is.EqualTo(1));

                controller.PlayFromHere(1);
                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1));
                Assert.That(controller.CurrentBeatIndex, Is.EqualTo(0));
                Assert.That(controller.BeatElapsedSeconds, Is.EqualTo(0f).Within(.0001f));
            }
        }

        [Test]
        public void MD_PausePreservesCurrentBeatAndClocks()
        {
            var project = new VnSceneComposerProject();
            project.scenes.Add(MdScene("Pause", VnSceneComposerPreviewAdvanceMode.ManualBeat, 1f,
                "Beat A", "Beat B"));

            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayScene(0);
                controller.Advance(.50f);
                controller.AdvanceDialogue();
                controller.Advance(.25f);

                float sceneElapsed = controller.SceneElapsedSeconds;
                float beatElapsed = controller.BeatElapsedSeconds;
                controller.Pause();
                controller.Advance(10f);

                Assert.That(controller.CurrentBeatIndex, Is.EqualTo(1));
                Assert.That(controller.SceneElapsedSeconds, Is.EqualTo(sceneElapsed).Within(.0001f));
                Assert.That(controller.BeatElapsedSeconds, Is.EqualTo(beatElapsed).Within(.0001f));
            }
        }

        [Test]
        public void MD_RestartReturnsCurrentPlaybackSceneToBeatZero()
        {
            var project = new VnSceneComposerProject();
            project.scenes.Add(MdScene("Scene 0", VnSceneComposerPreviewAdvanceMode.ManualBeat, 1f, "0A"));
            project.scenes.Add(MdScene("Scene 1", VnSceneComposerPreviewAdvanceMode.ManualBeat, 1f, "1A", "1B"));

            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayScene(1);
                controller.AdvanceDialogue();
                controller.Advance(.30f);
                Assert.That(controller.CurrentBeatIndex, Is.EqualTo(1));

                controller.Restart();

                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1));
                Assert.That(controller.CurrentBeatIndex, Is.EqualTo(0));
                Assert.That(controller.SceneElapsedSeconds, Is.EqualTo(0f).Within(.0001f));
                Assert.That(controller.BeatElapsedSeconds, Is.EqualTo(0f).Within(.0001f));
                Assert.That(controller.MediaTimeSeconds, Is.EqualTo(0f).Within(.0001f));
            }
        }

        [Test]
        public void MD_ToolbarNextPreviousRemainSceneNavigation()
        {
            var project = new VnSceneComposerProject();
            project.scenes.Add(MdScene("Scene 0", VnSceneComposerPreviewAdvanceMode.ManualBeat, 1f, "0A", "0B"));
            project.scenes.Add(MdScene("Scene 1", VnSceneComposerPreviewAdvanceMode.ManualBeat, 1f, "1A", "1B"));

            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayScene(0);
                controller.AdvanceDialogue();
                Assert.That(controller.CurrentBeatIndex, Is.EqualTo(1));

                controller.Next();
                Assert.That((controller.CurrentSceneIndex, controller.CurrentBeatIndex), Is.EqualTo((1, 0)));

                controller.AdvanceDialogue();
                Assert.That(controller.CurrentBeatIndex, Is.EqualTo(1));
                controller.Previous();
                Assert.That((controller.CurrentSceneIndex, controller.CurrentBeatIndex), Is.EqualTo((0, 0)));
            }
        }

        [Test]
        public void MD_OneBeatScenePreservesLegacyTransportBehavior()
        {
            var project = new VnSceneComposerProject();
            SetAutoPreviewSequenceGap(project, 0f);
            project.scenes.Add(MdScene("Legacy 0", VnSceneComposerPreviewAdvanceMode.PreviewAutoDuration, .10f, "A"));
            project.scenes.Add(MdScene("Legacy 1", VnSceneComposerPreviewAdvanceMode.ManualBeat, 1f, "B"));

            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayAll();
                controller.Advance(.11f);

                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1),
                    "A one-Beat Scene must preserve the historical auto Scene-boundary behavior.");
                Assert.That(controller.CurrentBeatIndex, Is.EqualTo(0));
            }
        }

        [Test]
        public void MD_LegacySchemaOneDialogueImportsAsExactlyOneCanonicalBeat()
        {
            Type serializationType = RequireType("VnSceneComposerSerialization");
            MethodInfo deserialize = serializationType.GetMethod(
                "DeserializePortable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(string) }, null);
            Assert.That(deserialize, Is.Not.Null);

            const string json = "{\n" +
                "  \"schemaVersion\": 1,\n" +
                "  \"sourceHead\": \"f58f1db08fc225c2831ff59d50d42e8c07ea15ce\",\n" +
                "  \"projectId\": \"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\n" +
                "  \"title\": \"Legacy\",\n" +
                "  \"scenes\": [{\n" +
                "    \"sceneId\": \"11111111111111111111111111111111\",\n" +
                "    \"label\": \"Legacy Scene\",\n" +
                "    \"speaker\": \"Aiko\",\n" +
                "    \"previewText\": \"Привет 你好 Hello\",\n" +
                "    \"narration\": false\n" +
                "  }]\n" +
                "}";

            object result = deserialize.Invoke(null, new object[] { json });
            Assert.That((bool)Get(result, "Success"), Is.True, (string)Get(result, "Error"));
            object project = Get(result, "Project");
            IList scenes = (IList)Get(project, "scenes");
            Assert.That(scenes, Has.Count.EqualTo(1));

            FieldInfo beatsField = scenes[0].GetType().GetField("dialogueBeats", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(beatsField, Is.Not.Null,
                "Legacy v1 dialogue must migrate into the canonical v2 beat list.");
            IList beats = (IList)beatsField.GetValue(scenes[0]);
            Assert.That(beats, Has.Count.EqualTo(1));
            Assert.That((string)Get(beats[0], "speaker"), Is.EqualTo("Aiko"));
            Assert.That((string)Get(beats[0], "text"), Is.EqualTo("Привет 你好 Hello"));
            Assert.That((bool)Get(beats[0], "narration"), Is.False);
        }
        private static VnSceneComposerScene MdScene(
            string label,
            VnSceneComposerPreviewAdvanceMode mode,
            float previewAutoDuration,
            params string[] dialogue)
        {
            var scene = new VnSceneComposerScene
            {
                label = label,
                timing = new VnSceneComposerTiming
                {
                    previewAdvanceMode = mode,
                    previewAutoDuration = previewAutoDuration
                }
            };
            scene.dialogueBeats.Clear();
            if (dialogue == null || dialogue.Length == 0)
                dialogue = new[] { string.Empty };
            for (int i = 0; i < dialogue.Length; i++)
            {
                scene.dialogueBeats.Add(new VnSceneComposerDialogueBeat
                {
                    speaker = i % 2 == 0 ? "Aiko" : "Tim",
                    text = dialogue[i] ?? string.Empty,
                    narration = false
                });
            }
            return scene;
        }

    }
}
