using System;
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
    public sealed class VnSceneComposerMf1Tests
    {
        private const string EditorAssembly = "Rokas.Editor";
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";

        private sealed class Mf1VideoPreview : VnSceneComposerVideoPreview
        {
            public int PrepareCalls;
            public int PlayCalls;
            public int DisposeCalls;
            private bool prepared;
            private bool playing;

            internal Mf1VideoPreview(bool loop, int serial) : base(null, 16, 16, loop)
            {
                warning = string.Empty;
                texture = new RenderTexture(32, 18, 0) { name = "ROKAS_MF1_" + serial };
                texture.Create();
            }

            public override bool IsPrepared { get { return prepared; } }
            public override bool IsPreparing { get { return false; } }
            public override bool IsPlaying { get { return playing; } }
            public override bool HasVisibleFrame { get { return prepared; } }

            public override void Prepare()
            {
                PrepareCalls++;
                prepared = true;
            }

            public override void Play()
            {
                PlayCalls++;
                if (!prepared) Prepare();
                playing = true;
            }

            public override void Pause() { playing = false; }
            public override void Restart() { playing = false; }

            public override void Dispose()
            {
                DisposeCalls++;
                playing = false;
                base.Dispose();
            }
        }

        private sealed class Mf1VideoFactory : IVnSceneComposerVideoPreviewFactory
        {
            public readonly List<Mf1VideoPreview> Created = new List<Mf1VideoPreview>();

            public int TotalPrepareCalls
            {
                get { return Created.Sum(x => x != null ? x.PrepareCalls : 0); }
            }

            public VnSceneComposerVideoPreview Open(VnSceneComposerMediaReference media, int width, int height)
            {
                var preview = new Mf1VideoPreview(media != null && media.loop, Created.Count);
                Created.Add(preview);
                return preview;
            }
        }

        [Test]
        public void MF1_NewBeatDefaultsToKeepPreviousCharacterState()
        {
            var beat = new VnSceneComposerDialogueBeat();
            Assert.That(GetString(beat, "targetCharacterId"), Is.Empty);
            Assert.That(GetBool(beat, "hasStateOverride"), Is.False);
            Assert.That(GetString(beat, "stateId"), Is.Empty);
            Assert.That(Get(beat, "effect").ToString(), Is.EqualTo("None"));
            Assert.That(GetFloat(beat, "effectStrength"), Is.GreaterThanOrEqualTo(0f));
            Assert.That(GetFloat(beat, "effectDuration"), Is.GreaterThan(0f));
        }

        [Test]
        public void MF1_ExplicitBeatStateResolvesForTargetCharacter()
        {
            VnSceneComposerScene scene = SceneWithCharacter("Mina", "mina_neutral");
            VnSceneComposerDialogueBeat beat = scene.dialogueBeats[0];
            SetBeatState(beat, "Mina", true, "mina_happy");

            Assert.That(ResolveState(scene, beat, "Mina"), Is.EqualTo("mina_happy"));
            Assert.That(scene.characters[0].stateId, Is.EqualTo("mina_neutral"),
                "Effective Beat state resolution must remain transient and must not mutate scene.characters.");
        }

        [Test]
        public void MF1_KeepPreviousCarriesLatestExplicitState()
        {
            VnSceneComposerScene scene = SceneWithCharacter("Mina", "mina_neutral");
            VnSceneComposerDialogueBeat first = scene.dialogueBeats[0];
            first.text = "One";
            SetBeatState(first, "Mina", true, "mina_happy");
            var second = new VnSceneComposerDialogueBeat { text = "Two" };
            SetBeatState(second, "Mina", false, string.Empty);
            var third = new VnSceneComposerDialogueBeat { text = "Three" };
            SetBeatState(third, "Mina", true, "mina_serious");
            scene.dialogueBeats.Add(second);
            scene.dialogueBeats.Add(third);

            Assert.That(ResolveState(scene, first, "Mina"), Is.EqualTo("mina_happy"));
            Assert.That(ResolveState(scene, second, "Mina"), Is.EqualTo("mina_happy"));
            Assert.That(ResolveState(scene, third, "Mina"), Is.EqualTo("mina_serious"));
        }

        [Test]
        public void MF1_CharacterStateCarryIsIndependentPerCharacter()
        {
            var scene = new VnSceneComposerScene();
            scene.characters.Add(Character("Mina", "mina_neutral", VnWorkshopStageSlot.Left));
            scene.characters.Add(Character("Keiko", "keiko_neutral", VnWorkshopStageSlot.Right));
            VnSceneComposerDialogueBeat first = scene.dialogueBeats[0];
            SetBeatState(first, "Mina", true, "mina_happy");
            var second = new VnSceneComposerDialogueBeat();
            SetBeatState(second, "Keiko", true, "keiko_serious");
            var third = new VnSceneComposerDialogueBeat();
            SetBeatState(third, "Mina", false, string.Empty);
            scene.dialogueBeats.Add(second);
            scene.dialogueBeats.Add(third);

            Assert.That(ResolveState(scene, third, "Mina"), Is.EqualTo("mina_happy"));
            Assert.That(ResolveState(scene, third, "Keiko"), Is.EqualTo("keiko_serious"));
        }

        [Test]
        public void MF1_BeatStateCannotResolveAnotherCharactersState()
        {
            VnSceneComposerScene scene = SceneWithCharacter("Mina", "mina_neutral");
            VnSceneComposerDialogueBeat beat = scene.dialogueBeats[0];
            SetBeatState(beat, "Mina", true, "keiko_serious");

            TargetInvocationException error = Assert.Throws<TargetInvocationException>(() =>
                ResolveStateMethod().Invoke(null, new object[] { scene, beat, "Mina" }));
            Assert.That(error.InnerException, Is.TypeOf<ArgumentException>());
            Assert.That(error.InnerException.Message, Does.Contain("Mina").And.Contain("Keiko"));
        }

        [Test]
        public void MF1_DuplicateBeatCopiesCharacterStateAndEffectWithNewBeatId()
        {
            var scene = SceneWithCharacter("Mina", "mina_neutral");
            VnSceneComposerDialogueBeat source = scene.dialogueBeats[0];
            source.speaker = "Mina";
            source.text = "Hello";
            SetBeatState(source, "Mina", true, "mina_happy");
            SetBeatEffect(source, "Accent", 47f, .42f);
            string sourceId = source.beatId;

            Type dialogue = RequireType("VnSceneComposerDialogue");
            MethodInfo duplicate = RequireStaticAnyVisibility(dialogue, "DuplicateBeat",
                typeof(VnSceneComposerScene), typeof(string));
            var copy = (VnSceneComposerDialogueBeat)duplicate.Invoke(null, new object[] { scene, sourceId });

            Assert.That(copy.beatId, Is.Not.EqualTo(sourceId));
            Assert.That(copy.speaker, Is.EqualTo(source.speaker));
            Assert.That(copy.text, Is.EqualTo(source.text));
            Assert.That(GetString(copy, "targetCharacterId"), Is.EqualTo("Mina"));
            Assert.That(GetBool(copy, "hasStateOverride"), Is.True);
            Assert.That(GetString(copy, "stateId"), Is.EqualTo("mina_happy"));
            Assert.That(Get(copy, "effect").ToString(), Is.EqualTo("Accent"));
            Assert.That(GetFloat(copy, "effectStrength"), Is.EqualTo(47f).Within(.0001f));
            Assert.That(GetFloat(copy, "effectDuration"), Is.EqualTo(.42f).Within(.0001f));
        }

        [Test]
        public void MF1_PreviousSchemaTwoMigratesToKeepPreviousWithoutVisualStateChange()
        {
            Assert.That(VnSceneComposerContract.SchemaVersion, Is.EqualTo(3),
                "Persisted Beat state/effect data requires a new canonical schema.");
            var project = new VnSceneComposerProject();
            var scene = SceneWithCharacter("Mina", "mina_neutral");
            scene.dialogueBeats[0].speaker = "Mina";
            scene.dialogueBeats[0].text = "Legacy";
            project.scenes.Add(scene);

            string current = VnSceneComposerSerialization.SerializePortable(project);
            string schemaTwo = current.Replace("\"schemaVersion\": 3", "\"schemaVersion\": 2");
            Assert.That(schemaTwo, Is.Not.EqualTo(current));
            VnSceneComposerImportResult result = VnSceneComposerSerialization.DeserializePortable(schemaTwo);

            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(result.Project.schemaVersion, Is.EqualTo(3));
            VnSceneComposerDialogueBeat migrated = result.Project.scenes[0].dialogueBeats[0];
            Assert.That(GetBool(migrated, "hasStateOverride"), Is.False);
            Assert.That(GetString(migrated, "targetCharacterId"), Is.Empty);
            Assert.That(GetString(migrated, "stateId"), Is.Empty);
            Assert.That(Get(migrated, "effect").ToString(), Is.EqualTo("None"));
            Assert.That(result.Project.scenes[0].characters[0].stateId, Is.EqualTo("mina_neutral"));
        }

        [Test]
        public void MF1_SerializationRoundTripPreservesBeatStateEffectAndStableId()
        {
            var project = new VnSceneComposerProject();
            var scene = SceneWithCharacter("Mina", "mina_neutral");
            VnSceneComposerDialogueBeat beat = scene.dialogueBeats[0];
            beat.speaker = "Mina";
            beat.text = "Round trip";
            SetBeatState(beat, "Mina", true, "mina_happy");
            SetBeatEffect(beat, "Accent", 35f, .55f);
            string beatId = beat.beatId;
            project.scenes.Add(scene);

            string json = VnSceneComposerSerialization.SerializePortable(project);
            VnSceneComposerImportResult result = VnSceneComposerSerialization.DeserializePortable(json);
            Assert.That(result.Success, Is.True, result.Error);
            VnSceneComposerDialogueBeat loaded = result.Project.scenes[0].dialogueBeats[0];

            Assert.That(loaded.beatId, Is.EqualTo(beatId));
            Assert.That(GetString(loaded, "targetCharacterId"), Is.EqualTo("Mina"));
            Assert.That(GetBool(loaded, "hasStateOverride"), Is.True);
            Assert.That(GetString(loaded, "stateId"), Is.EqualTo("mina_happy"));
            Assert.That(Get(loaded, "effect").ToString(), Is.EqualTo("Accent"));
            Assert.That(GetFloat(loaded, "effectStrength"), Is.EqualTo(35f).Within(.0001f));
            Assert.That(GetFloat(loaded, "effectDuration"), Is.EqualTo(.55f).Within(.0001f));
            Assert.That(ResolveState(result.Project.scenes[0], loaded, "Mina"), Is.EqualTo("mina_happy"),
                "Save/load must reproduce the same effective Beat character state.");
        }

        [Test]
        public void MF1_PersistenceRejectsBeatStateOwnedByAnotherCharacter()
        {
            var project = new VnSceneComposerProject();
            VnSceneComposerScene scene = SceneWithCharacter("Mina", "mina_neutral");
            VnSceneComposerDialogueBeat beat = scene.dialogueBeats[0];
            SetBeatState(beat, "Mina", true, "keiko_serious");
            project.scenes.Add(scene);

            ArgumentException error = Assert.Throws<ArgumentException>(() =>
                VnSceneComposerSerialization.SerializePortable(project));
            Assert.That(error.Message, Does.Contain("belongs").IgnoreCase
                .And.Contain("Keiko")
                .And.Contain("Mina"));
        }

        [Test]
        public void MF1_CompositionUsesCarriedStateForRendererFacingCharacters()
        {
            var project = new VnSceneComposerProject();
            var scene = SceneWithCharacter("Mina", "mina_neutral");
            VnSceneComposerDialogueBeat first = scene.dialogueBeats[0];
            SetBeatState(first, "Mina", true, "mina_happy");
            var second = new VnSceneComposerDialogueBeat();
            SetBeatState(second, "Mina", false, string.Empty);
            var third = new VnSceneComposerDialogueBeat();
            SetBeatState(third, "Mina", true, "mina_serious");
            scene.dialogueBeats.Add(second);
            scene.dialogueBeats.Add(third);
            project.scenes.Add(scene);

            Assert.That(BuildState(project, scene, first, "Mina"), Is.EqualTo("mina_happy"));
            Assert.That(BuildState(project, scene, second, "Mina"), Is.EqualTo("mina_happy"));
            Assert.That(BuildState(project, scene, third, "Mina"), Is.EqualTo("mina_serious"));
            Assert.That(scene.characters[0].stateId, Is.EqualTo("mina_neutral"));
        }

        [Test]
        public void MF1_MultiCharacterCompositionCarriesEachCharacterIndependently()
        {
            var project = new VnSceneComposerProject();
            var scene = new VnSceneComposerScene();
            scene.characters.Add(Character("Mina", "mina_neutral", VnWorkshopStageSlot.Left));
            scene.characters.Add(Character("Keiko", "keiko_neutral", VnWorkshopStageSlot.Right));
            VnSceneComposerDialogueBeat first = scene.dialogueBeats[0];
            SetBeatState(first, "Mina", true, "mina_happy");
            var second = new VnSceneComposerDialogueBeat();
            SetBeatState(second, "Keiko", true, "keiko_serious");
            scene.dialogueBeats.Add(second);
            project.scenes.Add(scene);

            VnWorkshopPreviewFrame frame = VnSceneComposerComposition.BuildFrame(
                project, scene, second, VnWorkshopResolution.Reference1920x1080, null);
            Assert.That(PreviewState(frame, "Mina"), Is.EqualTo("mina_happy"));
            Assert.That(PreviewState(frame, "Keiko"), Is.EqualTo("keiko_serious"));
        }

        [Test]
        public void MF1_BeatAdvanceChangesCharacterStateWithoutReopeningVideoResources()
        {
            var factory = new Mf1VideoFactory();
            VnSceneComposerMediaEditing.VideoPreviewFactory = factory;
            VnSceneComposerPlaybackController controller = null;
            try
            {
                var project = new VnSceneComposerProject();
                var scene = SceneWithCharacter("Mina", "mina_neutral");
                scene.media = new VnSceneComposerMediaReference
                {
                    kind = VnSceneComposerMediaKind.ExternalVideo,
                    reference = "mf1-continuity.mp4",
                    displayName = "mf1-continuity.mp4",
                    contentHash = "mf1-continuity",
                    scaleMode = VnSceneComposerMediaScaleMode.Fit,
                    loop = true
                };
                VnSceneComposerDialogueBeat first = scene.dialogueBeats[0];
                SetBeatState(first, "Mina", true, "mina_happy");
                var second = new VnSceneComposerDialogueBeat();
                SetBeatState(second, "Mina", true, "mina_serious");
                SetBeatEffect(second, "Accent", 30f, .4f);
                scene.dialogueBeats.Add(second);
                project.scenes.Add(scene);

                string authoredBeforePlayback = JsonUtility.ToJson(project);
                controller = new VnSceneComposerPlaybackController(project);
                controller.PlayAll();
                controller.Advance(.25f);
                Assert.That(factory.Created.Count, Is.EqualTo(1));
                Mf1VideoPreview preview = factory.Created[0];
                int prepares = factory.TotalPrepareCalls;
                Texture texture = controller.CurrentMediaTexture;
                float mediaTime = controller.MediaTimeSeconds;
                Assert.That(PreviewState(controller.CurrentFrame.WorkshopFrame, "Mina"), Is.EqualTo("mina_happy"));

                controller.AdvanceDialogue();

                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(0));
                Assert.That(controller.CurrentBeatIndex, Is.EqualTo(1));
                Assert.That(controller.BeatElapsedSeconds, Is.EqualTo(0f).Within(.0001f));
                Assert.That(controller.MediaTimeSeconds, Is.EqualTo(mediaTime).Within(.0001f));
                Assert.That(factory.Created.Count, Is.EqualTo(1));
                Assert.That(factory.TotalPrepareCalls, Is.EqualTo(prepares));
                Assert.That(preview.DisposeCalls, Is.EqualTo(0));
                Assert.That(controller.CurrentMediaTexture, Is.SameAs(texture));
                Assert.That(PreviewState(controller.CurrentFrame.WorkshopFrame, "Mina"), Is.EqualTo("mina_serious"));
                Assert.That(scene.characters[0].stateId, Is.EqualTo("mina_neutral"));
                Assert.That(JsonUtility.ToJson(project), Is.EqualTo(authoredBeforePlayback),
                    "Beat playback must remain read-only with respect to authored project data.");
            }
            finally
            {
                controller?.Dispose();
                VnSceneComposerMediaEditing.ResetVideoPreviewFactory();
                foreach (Mf1VideoPreview preview in factory.Created)
                    if (preview != null && preview.texture != null) preview.Dispose();
            }
        }

        [Test]
        public void MF1_AccentUsesBeatClockMovesOnlyTargetAndSettles()
        {
            var project = new VnSceneComposerProject();
            var scene = new VnSceneComposerScene();
            scene.characters.Add(Character("Mina", "mina_neutral", VnWorkshopStageSlot.Left));
            scene.characters.Add(Character("Keiko", "keiko_neutral", VnWorkshopStageSlot.Right));
            VnSceneComposerDialogueBeat beat = scene.dialogueBeats[0];
            SetBeatState(beat, "Mina", false, string.Empty);
            SetBeatEffect(beat, "Accent", 50f, .4f);
            project.scenes.Add(scene);

            VnWorkshopPreviewFrame baseline = VnSceneComposerComposition.BuildFrame(
                project, scene, beat, VnWorkshopResolution.Reference1920x1080, null);
            float minaBaseY = PreviewCharacter(baseline, "Mina").Body.center.y;
            float keikoBaseY = PreviewCharacter(baseline, "Keiko").Body.center.y;

            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayAll();
                controller.Advance(.2f);
                Assert.That(controller.BeatElapsedSeconds, Is.EqualTo(.2f).Within(.001f));
                Assert.That(PreviewCharacter(controller.CurrentFrame.WorkshopFrame, "Mina").Body.center.y,
                    Is.Not.EqualTo(minaBaseY).Within(.001f));
                Assert.That(PreviewCharacter(controller.CurrentFrame.WorkshopFrame, "Keiko").Body.center.y,
                    Is.EqualTo(keikoBaseY).Within(.001f),
                    "Beat Accent must affect only its explicit target character.");

                controller.Advance(.3f);
                Assert.That(PreviewCharacter(controller.CurrentFrame.WorkshopFrame, "Mina").Body.center.y,
                    Is.EqualTo(minaBaseY).Within(.01f),
                    "Beat Accent must settle after its authored duration rather than loop every repaint.");
            }
        }

        [Test]
        public void MF1_FinalBeatStillCreatesExactlyOneRealSceneBoundary()
        {
            var project = new VnSceneComposerProject();
            var firstScene = new VnSceneComposerScene();
            firstScene.dialogueBeats[0].text = "One";
            firstScene.dialogueBeats.Add(new VnSceneComposerDialogueBeat { text = "Two" });
            var secondScene = new VnSceneComposerScene();
            secondScene.dialogueBeats[0].text = "Next scene";
            project.scenes.Add(firstScene);
            project.scenes.Add(secondScene);

            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayAll();
                controller.AdvanceDialogue();
                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(0));
                Assert.That(controller.CurrentBeatIndex, Is.EqualTo(1));

                controller.AdvanceDialogue();
                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1));
                Assert.That(controller.CurrentBeatIndex, Is.EqualTo(0));
            }
        }

        [Test]
        public void MF1_AuthoringUiExposesContextualCreatorFacingBeatStateControls()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposer.cs");
            string inspector = ExtractMethodBody(source,
                "private void DrawSceneComposerTextInspector(VnSceneComposerScene scene)");

            Assert.That(inspector, Does.Contain("\"Персонаж реплики\""));
            Assert.That(inspector, Does.Contain("\"Эмоция / поза\""));
            Assert.That(inspector, Does.Contain("\"Оставить предыдущее\""));
            Assert.That(inspector, Does.Contain("\"Анимация реплики\""));
            Assert.That(inspector, Does.Contain("\"Без анимации\"").And.Contain("\"Акцент\""));
            Assert.That(inspector, Does.Not.Contain("TextField(\"stateId\"")
                .And.Not.Contain("TextField(\"State ID\""),
                "Raw state IDs must not become the normal Basic authoring workflow.");
        }

        [Test]
        public void MF1_AuthoringBeatSelectionChangesPreviewStateWithoutResettingMedia()
        {
            var factory = new Mf1VideoFactory();
            VnSceneComposerMediaEditing.VideoPreviewFactory = factory;
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                window.ComposerAddCharacter("Mina", "mina_neutral");
                VnSceneComposerProject project = GetProject(window);
                VnSceneComposerScene scene = project.scenes[0];
                scene.media = new VnSceneComposerMediaReference
                {
                    kind = VnSceneComposerMediaKind.ExternalVideo,
                    reference = "mf1-authoring.mp4",
                    displayName = "mf1-authoring.mp4",
                    contentHash = "mf1-authoring",
                    scaleMode = VnSceneComposerMediaScaleMode.Fit,
                    loop = true
                };
                VnSceneComposerDialogueBeat first = scene.dialogueBeats[0];
                SetBeatState(first, "Mina", true, "mina_happy");
                var second = new VnSceneComposerDialogueBeat();
                SetBeatState(second, "Mina", true, "mina_serious");
                scene.dialogueBeats.Add(second);

                Assert.That(window.ComposerPrepareSelectedVideoForAuthoring(), Is.True);
                Assert.That(factory.Created.Count, Is.EqualTo(1));
                int prepares = factory.TotalPrepareCalls;
                Mf1VideoPreview preview = factory.Created[0];

                window.ComposerSelectDialogueBeat(first.beatId);
                Assert.That(PreviewState(window.ComposerBuildSelectedPreviewFrame(), "Mina"), Is.EqualTo("mina_happy"));
                window.ComposerSelectDialogueBeat(second.beatId);
                Assert.That(PreviewState(window.ComposerBuildSelectedPreviewFrame(), "Mina"), Is.EqualTo("mina_serious"));

                Assert.That(factory.Created.Count, Is.EqualTo(1));
                Assert.That(factory.TotalPrepareCalls, Is.EqualTo(prepares));
                Assert.That(preview.DisposeCalls, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                VnSceneComposerMediaEditing.ResetVideoPreviewFactory();
                foreach (Mf1VideoPreview preview in factory.Created)
                    if (preview != null && preview.texture != null) preview.Dispose();
            }
        }

        [Test]
        public void MF1_WindowBeatStateMutationParticipatesInUndo()
        {
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                window.ComposerAddCharacter("Mina", "mina_neutral");
                MethodInfo setState = typeof(VnPresentationWorkshopWindow).GetMethod(
                    "ComposerSetSelectedDialogueBeatCharacterState",
                    BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(string), typeof(bool), typeof(string) }, null);
                Assert.That(setState, Is.Not.Null);
                setState.Invoke(window, new object[] { "Mina", true, "mina_happy" });
                VnSceneComposerDialogueBeat beat = GetProject(window).scenes[0].dialogueBeats[0];
                Assert.That(GetBool(beat, "hasStateOverride"), Is.True);
                Assert.That(GetString(beat, "stateId"), Is.EqualTo("mina_happy"));

                Undo.PerformUndo();

                // Undo restores the serialized ScriptableObject graph; reacquire the nested Beat
                // instead of asserting against the stale pre-Undo managed reference.
                beat = GetProject(window).scenes[0].dialogueBeats[0];
                Assert.That(GetBool(beat, "hasStateOverride"), Is.False);
                Assert.That(GetString(beat, "stateId"), Is.Empty);
            }
            finally
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static VnSceneComposerScene SceneWithCharacter(string characterId, string stateId)
        {
            var scene = new VnSceneComposerScene();
            scene.characters.Add(Character(characterId, stateId, VnWorkshopStageSlot.Center));
            return scene;
        }

        private static VnSceneComposerCharacter Character(
            string characterId, string stateId, VnWorkshopStageSlot slot)
        {
            return new VnSceneComposerCharacter
            {
                characterId = characterId,
                stateId = stateId,
                stageSlot = slot
            };
        }

        private static void SetBeatState(
            VnSceneComposerDialogueBeat beat, string targetCharacterId, bool hasOverride, string stateId)
        {
            Set(beat, "targetCharacterId", targetCharacterId);
            Set(beat, "hasStateOverride", hasOverride);
            Set(beat, "stateId", stateId);
        }

        private static void SetBeatEffect(
            VnSceneComposerDialogueBeat beat, string effect, float strength, float duration)
        {
            SetEnum(beat, "effect", effect);
            Set(beat, "effectStrength", strength);
            Set(beat, "effectDuration", duration);
        }

        private static string ResolveState(
            VnSceneComposerScene scene, VnSceneComposerDialogueBeat beat, string characterId)
        {
            return (string)ResolveStateMethod().Invoke(null, new object[] { scene, beat, characterId });
        }

        private static MethodInfo ResolveStateMethod()
        {
            Type resolver = RequireType("VnSceneComposerBeatCharacterStateResolver");
            return RequireStatic(resolver, "ResolveStateId",
                typeof(VnSceneComposerScene), typeof(VnSceneComposerDialogueBeat), typeof(string));
        }

        private static string BuildState(
            VnSceneComposerProject project, VnSceneComposerScene scene,
            VnSceneComposerDialogueBeat beat, string characterId)
        {
            VnWorkshopPreviewFrame frame = VnSceneComposerComposition.BuildFrame(
                project, scene, beat, VnWorkshopResolution.Reference1920x1080, null);
            return PreviewState(frame, characterId);
        }

        private static string PreviewState(VnWorkshopPreviewFrame frame, string characterId)
        {
            return PreviewCharacter(frame, characterId).StateId;
        }

        private static VnWorkshopPreviewCharacter PreviewCharacter(
            VnWorkshopPreviewFrame frame, string characterId)
        {
            Assert.That(frame, Is.Not.Null);
            VnWorkshopPreviewCharacter match = (frame.ComposerCharacters ?? Array.Empty<VnWorkshopPreviewCharacter>())
                .FirstOrDefault(c => c != null &&
                    string.Equals(c.CharacterId, characterId, StringComparison.OrdinalIgnoreCase));
            Assert.That(match, Is.Not.Null, "Missing renderer-facing character: " + characterId);
            return match;
        }

        private static VnSceneComposerProject GetProject(VnPresentationWorkshopWindow window)
        {
            FieldInfo field = typeof(VnPresentationWorkshopWindow).GetField(
                "_sceneComposerProject", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            return field.GetValue(window) as VnSceneComposerProject;
        }

        private static string ReadEditorSource(string fileName)
        {
            string path = Path.Combine(Application.dataPath, "Rokas", "Scripts", "Editor", "VnUiWorkshop", fileName);
            Assert.That(File.Exists(path), Is.True, "Missing inspected editor source: " + path);
            return File.ReadAllText(path);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(signatureIndex, Is.GreaterThanOrEqualTo(0), "Missing method signature: " + signature);
            int openBrace = source.IndexOf('{', signatureIndex);
            Assert.That(openBrace, Is.GreaterThanOrEqualTo(0));
            int depth = 0;
            for (int i = openBrace; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0) return source.Substring(openBrace, i - openBrace + 1);
                }
            }
            Assert.Fail("Unterminated method body: " + signature);
            return string.Empty;
        }

        private static Type RequireType(string shortName)
        {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                .SingleOrDefault(a => a.GetName().Name == EditorAssembly);
            Assert.That(assembly, Is.Not.Null);
            Type type = assembly.GetType(Namespace + shortName, false);
            Assert.That(type, Is.Not.Null, "Missing Scene Composer type: " + shortName);
            return type;
        }

        private static MethodInfo RequireStatic(Type type, string name, params Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static,
                null, parameters, null);
            Assert.That(method, Is.Not.Null, "Missing public static method: " + type.Name + "." + name);
            return method;
        }

        private static MethodInfo RequireStaticAnyVisibility(Type type, string name, params Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null, parameters, null);
            Assert.That(method, Is.Not.Null, "Missing static method: " + type.Name + "." + name);
            return method;
        }

        private static FieldInfo Field(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing public field: " + instance.GetType().Name + "." + name);
            return field;
        }

        private static object Get(object instance, string name) { return Field(instance, name).GetValue(instance); }
        private static string GetString(object instance, string name) { return (string)Get(instance, name); }
        private static bool GetBool(object instance, string name) { return (bool)Get(instance, name); }
        private static float GetFloat(object instance, string name) { return (float)Get(instance, name); }
        private static void Set(object instance, string name, object value) { Field(instance, name).SetValue(instance, value); }

        private static void SetEnum(object instance, string name, string value)
        {
            FieldInfo field = Field(instance, name);
            field.SetValue(instance, Enum.Parse(field.FieldType, value));
        }
    }
}
