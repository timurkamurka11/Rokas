using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerMTransitionTests
    {
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";
        private const string TrackAGuid = "7540bd61684b4a768cad4691dbc54972";

        [Test]
        public void MT_ModelIsAdditiveIncomingSceneOwnedAndDefaultsToNone()
        {
            Type transitionType = typeof(VnSceneComposerTransition);
            Type kindType = RequireType("VnSceneComposerSceneTransitionType");
            Type directionType = RequireType("VnSceneComposerSceneTransitionDirection");
            Assert.That(kindType.IsEnum, Is.True);
            Assert.That(directionType.IsEnum, Is.True);
            Assert.That(Enum.GetNames(kindType), Does.Contain("None").And.Contain("DarkCurtain"));
            Assert.That(Enum.GetNames(directionType), Does.Contain("LeftToRight").And.Contain("RightToLeft"));
            AssertField(transitionType, "sceneTransitionType");
            AssertField(transitionType, "sceneTransitionDirection");
            AssertField(transitionType, "sceneTransitionDuration");
            object transition = new VnSceneComposerScene().transition;
            Assert.That(Get(transition, "sceneTransitionType").ToString(), Is.EqualTo("None"));
            Assert.That(VnSceneComposerContract.SchemaVersion, Is.EqualTo(4));
        }

        [Test]
        public void MT_OldTransitionJsonWithoutNewFieldsRemainsInstant()
        {
            object transition = JsonUtility.FromJson("{\"triggerActionBounce\":false}", typeof(VnSceneComposerTransition));
            Assert.That(transition, Is.Not.Null);
            Assert.That(Get(transition, "sceneTransitionType").ToString(), Is.EqualTo("None"));
        }

        [Test]
        public void MT_SettingsSerializeAndDeserialize()
        {
            var project = ProjectWithTwoScenes();
            ConfigureTransition(project.scenes[1], "DarkCurtain", "RightToLeft", .84f);
            string json = VnSceneComposerSerialization.SerializePortable(project);
            VnSceneComposerImportResult loaded = VnSceneComposerSerialization.DeserializePortable(json);
            Assert.That(loaded.Success, Is.True, loaded.Error);
            object transition = loaded.Project.scenes[1].transition;
            Assert.That(Get(transition, "sceneTransitionType").ToString(), Is.EqualTo("DarkCurtain"));
            Assert.That(Get(transition, "sceneTransitionDirection").ToString(), Is.EqualTo("RightToLeft"));
            Assert.That((float)Get(transition, "sceneTransitionDuration"), Is.EqualTo(.84f).Within(.001f));
        }

        [Test]
        public void MT_FadeSettingsRoundTripWithoutChangingLegacyCurtainValue()
        {
            Assert.That((int)VnSceneComposerSceneTransitionType.DarkCurtain, Is.EqualTo(1));
            var project = ProjectWithTwoScenes();
            ConfigureTransition(project.scenes[1], "Fade", "LeftToRight", .8f);
            VnSceneComposerImportResult loaded = VnSceneComposerSerialization.DeserializePortable(
                VnSceneComposerSerialization.SerializePortable(project));
            Assert.That(loaded.Success, Is.True, loaded.Error);
            Assert.That(loaded.Project.scenes[1].transition.sceneTransitionType,
                Is.EqualTo(VnSceneComposerSceneTransitionType.Fade));
            Assert.That(loaded.Project.scenes[1].transition.sceneTransitionDuration,
                Is.EqualTo(.8f).Within(.001f));
        }

        [Test]
        public void MT_EditorPreviewButtonUsesTheSelectedRuntimeTransitionMode()
        {
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                window.ComposerAddScene();
                window.ComposerSelectScene(1);
                window.ComposerSetPresentationScope(false);

                window.ComposerSetSelectedSceneBoundaryTransition(
                    VnSceneComposerSceneTransitionType.Fade,
                    VnSceneComposerSceneTransitionDirection.LeftToRight, 1f);
                window.ComposerPreviewSelectedSceneBoundaryTransition();
                var playback = (VnSceneComposerPlaybackController)Get(window, "_sceneComposerPlayback");
                Assert.That(playback.CurrentSceneIndex, Is.EqualTo(0));
                Assert.That(Get(window, "_sceneComposerSelectedSceneId"),
                    Is.EqualTo(((VnSceneComposerScene)Scenes(window)[1]).sceneId));
                Assert.That(GetBool(playback, "IsSceneTransitionActive"), Is.True);
                Assert.That(Get(playback.CurrentFrame.SceneTransitionOverlay, "Mode"),
                    Is.EqualTo(VnSceneComposerSceneTransitionType.Fade));
                playback.Advance(.25f);
                Assert.That(playback.CurrentFrame.SceneTransitionOverlay.Coverage,
                    Is.EqualTo(.11f).Within(.01f));

                window.ComposerSetSelectedSceneBoundaryTransition(
                    VnSceneComposerSceneTransitionType.DarkCurtain,
                    VnSceneComposerSceneTransitionDirection.RightToLeft, 1f);
                window.ComposerPreviewSelectedSceneBoundaryTransition();
                Assert.That(playback.CurrentFrame.SceneTransitionOverlay.Mode,
                    Is.EqualTo(VnSceneComposerSceneTransitionType.DarkCurtain));
                Assert.That(playback.CurrentFrame.SceneTransitionOverlay.Direction,
                    Is.EqualTo(VnSceneComposerSceneTransitionDirection.RightToLeft));

                window.ComposerSetSelectedSceneBoundaryTransition(
                    VnSceneComposerSceneTransitionType.None,
                    VnSceneComposerSceneTransitionDirection.LeftToRight, 0f);
                window.ComposerPreviewSelectedSceneBoundaryTransition();
                Assert.That(playback.CurrentSceneIndex, Is.EqualTo(1));
                Assert.That(GetBool(playback, "IsSceneTransitionActive"), Is.False);
            }
            finally
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MT_DuplicateScenePreservesIndependentTransitionConfiguration()
        {
            var project = ProjectWithTwoScenes();
            ConfigureTransition(project.scenes[1], "DarkCurtain", "LeftToRight", .7f);
            VnSceneComposerScene copy = VnSceneComposerEditing.DuplicateScene(project, project.scenes[1].sceneId);
            Assert.That(copy, Is.Not.Null);
            Assert.That(copy.transition, Is.Not.SameAs(project.scenes[1].transition));
            Assert.That(Get(copy.transition, "sceneTransitionType").ToString(), Is.EqualTo("DarkCurtain"));
            Set(copy.transition, "sceneTransitionDuration", 1.2f);
            Assert.That((float)Get(project.scenes[1].transition, "sceneTransitionDuration"), Is.EqualTo(.7f).Within(.001f));
        }

        [Test]
        public void MT_WindowAuthoringIsUndoableAndDoesNotRecreatePlaybackController()
        {
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                window.ComposerAddScene();
                window.ComposerSelectScene(1);
                window.ComposerPlayScene();
                object beforePlayback = Get(window, "_sceneComposerPlayback");
                Type type = window.GetType();
                Type kind = RequireType("VnSceneComposerSceneTransitionType");
                Type direction = RequireType("VnSceneComposerSceneTransitionDirection");
                MethodInfo set = RequireInstance(type, "ComposerSetSelectedSceneBoundaryTransition",
                    kind, direction, typeof(float));

                Undo.ClearAll();
                set.Invoke(window, new[]
                {
                    Enum.Parse(kind, "DarkCurtain"),
                    Enum.Parse(direction, "LeftToRight"),
                    (object).9f
                });
                Undo.FlushUndoRecordObjects();
                object scene = Scenes(window)[1];
                Assert.That(Get(Get(scene, "transition"), "sceneTransitionType").ToString(), Is.EqualTo("DarkCurtain"));
                Assert.That(Get(window, "_sceneComposerPlayback"), Is.SameAs(beforePlayback),
                    "Transition authoring must not rebuild the existing playback controller.");

                Undo.PerformUndo();
                scene = Scenes(window)[1];
                Assert.That(Get(Get(scene, "transition"), "sceneTransitionType").ToString(), Is.EqualTo("None"));
            }
            finally
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MT_NoneBoundarySwitchesImmediately()
        {
            var project = ProjectWithTwoScenes();
            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayFromHere(0);
                controller.Next();
                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1));
                Assert.That(GetBool(controller, "IsSceneTransitionActive"), Is.False);
            }
        }

        [Test]
        public void MT_DarkCurtainStartsWithoutEarlySceneSwap()
        {
            var project = ProjectWithTwoScenes();
            ConfigureTransition(project.scenes[1], "DarkCurtain", "LeftToRight", 1f);
            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayFromHere(0);
                controller.Next();
                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(0));
                Assert.That(GetBool(controller, "IsSceneTransitionActive"), Is.True);
                Assert.That(GetBool(controller, "SceneTransitionInputLocked"), Is.True);
                Assert.That((int)Get(controller, "SceneTransitionStartCount"), Is.EqualTo(1));
            }
        }

        [Test]
        public void MT_FadeSoftlyDimsOutgoingSceneAndCrossfadesToIncomingScene()
        {
            var project = ProjectWithTwoScenes();
            ConfigureTransition(project.scenes[1], "Fade", "RightToLeft", 1f);
            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayFromHere(0);
                controller.Next();
                controller.Advance(.25f);
                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(0));
                object cover = Get(controller.CurrentFrame, "SceneTransitionOverlay");
                Assert.That(Get(cover, "Mode").ToString(), Is.EqualTo("Fade"));
                Assert.That((float)Get(cover, "Coverage"), Is.EqualTo(.11f).Within(.01f));
                Assert.That((bool)Get(cover, "FullCover"), Is.False);
                Assert.That(controller.CurrentFrame.ForegroundAlpha, Is.EqualTo(.5f).Within(.01f));

                controller.Advance(.25f);
                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1));
                object softCover = Get(controller.CurrentFrame, "SceneTransitionOverlay");
                Assert.That((float)Get(softCover, "Coverage"), Is.EqualTo(.22f).Within(.01f));
                Assert.That((bool)Get(softCover, "FullCover"), Is.False);
                Assert.That(controller.CurrentFrame.ForegroundAlpha, Is.EqualTo(0f).Within(.01f));
                Assert.That(controller.CurrentFrame.ComposerBackgroundTransition.SourceAlpha,
                    Is.EqualTo(1f).Within(.01f));
                Assert.That(controller.CurrentFrame.ComposerBackgroundTransition.TargetAlpha,
                    Is.EqualTo(0f).Within(.01f));

                controller.Advance(.25f);
                object reveal = Get(controller.CurrentFrame, "SceneTransitionOverlay");
                Assert.That(Get(reveal, "Mode").ToString(), Is.EqualTo("Fade"));
                Assert.That(Get(reveal, "Phase").ToString(), Is.EqualTo("Reveal"));
                Assert.That((float)Get(reveal, "Coverage"), Is.EqualTo(.11f).Within(.01f));
                Assert.That(controller.CurrentFrame.ForegroundAlpha, Is.EqualTo(.5f).Within(.01f));
                Assert.That(controller.CurrentFrame.ComposerBackgroundTransition.SourceAlpha,
                    Is.EqualTo(.5f).Within(.01f));
                Assert.That(controller.CurrentFrame.ComposerBackgroundTransition.TargetAlpha,
                    Is.EqualTo(.5f).Within(.01f));

                controller.Advance(.25f);
                Assert.That(GetBool(controller, "IsSceneTransitionActive"), Is.False);
                Assert.That(controller.CurrentFrame.ForegroundAlpha, Is.EqualTo(1f).Within(.01f));
            }
        }

        [Test]
        public void MT_FadeDefersIncomingReplicaEffectUntilTheReplicaBegins()
        {
            var project = ProjectWithTwoScenes();
            ConfigureTransition(project.scenes[1], "Fade", "RightToLeft", 1f);
            project.scenes[1].dialogueBeats[0].replicaEffect = new VnSceneComposerReplicaEffect
            {
                type = VnSceneComposerReplicaEffectType.Flash,
                intensity = .5f,
                duration = .45f,
                flashColor = Color.white
            };
            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayFromHere(0);
                controller.Next();
                controller.Advance(.75f);
                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1));
                Assert.That(controller.CurrentFrame.ReplicaEffect.Active, Is.False);
                Assert.That(controller.CurrentFrame.ReplicaEffect.Flash.a, Is.EqualTo(0f));
                controller.Advance(.25f);
                Assert.That(controller.CurrentFrame.ReplicaEffect.Active, Is.False);
                controller.Advance(.17f);
                Assert.That(controller.CurrentFrame.ReplicaEffect.Active, Is.True);
                Assert.That(controller.CurrentFrame.ReplicaEffect.Flash.a, Is.GreaterThan(.1f));
            }
        }

        [Test]
        public void MT_DarkCurtainRetainsItsDistinctDirectionalOverlayMode()
        {
            var project = ProjectWithTwoScenes();
            ConfigureTransition(project.scenes[1], "DarkCurtain", "RightToLeft", 1f);
            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayFromHere(0);
                controller.Next();
                controller.Advance(.25f);
                object overlay = Get(controller.CurrentFrame, "SceneTransitionOverlay");
                Assert.That(Get(overlay, "Mode").ToString(), Is.EqualTo("DarkCurtain"));
                Assert.That(Get(overlay, "Direction").ToString(), Is.EqualTo("RightToLeft"));
            }
        }

        [Test]
        public void MT_RepeatedNextCannotStackTransition()
        {
            var project = ProjectWithTwoScenes();
            ConfigureTransition(project.scenes[1], "DarkCurtain", "LeftToRight", 1f);
            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayFromHere(0);
                controller.Next();
                controller.Next();
                controller.AdvanceDialogue();
                Assert.That((int)Get(controller, "SceneTransitionStartCount"), Is.EqualTo(1));
                Assert.That((int)Get(controller, "PendingSceneTransitionTargetIndex"), Is.EqualTo(1));
            }
        }

        [Test]
        public void MT_SceneSwapOccursAtFullCoverNotBefore()
        {
            var project = ProjectWithTwoScenes();
            ConfigureTransition(project.scenes[1], "DarkCurtain", "LeftToRight", 1f);
            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayFromHere(0);
                controller.Next();
                controller.Advance(.49f);
                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(0));
                Assert.That(GetBool(controller, "SceneTransitionHasSwapped"), Is.False);

                controller.Advance(.02f);
                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1));
                Assert.That(GetBool(controller, "SceneTransitionHasSwapped"), Is.True);
                object overlay = Get(controller.CurrentFrame, "SceneTransitionOverlay");
                Assert.That((bool)Get(overlay, "Active"), Is.True);
                Assert.That((float)Get(overlay, "Coverage"), Is.GreaterThan(.95f));
            }
        }

        [Test]
        public void MT_DialogueUiIsHiddenThroughCoverAndRevealThenRestored()
        {
            var project = ProjectWithTwoScenes();
            project.scenes[0].previewText = "old";
            project.scenes[1].previewText = "incoming";
            ConfigureTransition(project.scenes[1], "DarkCurtain", "LeftToRight", 1f);
            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayFromHere(0);
                controller.Next();
                Assert.That((bool)Get(controller.CurrentFrame, "ShowDialogueUi"), Is.False);
                controller.Advance(.55f);
                Assert.That((bool)Get(controller.CurrentFrame, "ShowDialogueUi"), Is.False);
                controller.Advance(.55f);
                Assert.That(GetBool(controller, "IsSceneTransitionActive"), Is.False);
                Assert.That((bool)Get(controller.CurrentFrame, "ShowDialogueUi"), Is.True);
            }
        }

        [Test]
        public void MT_DirectionIsDeterministicAndMatchesAuthoredValue()
        {
            var project = ProjectWithTwoScenes();
            ConfigureTransition(project.scenes[1], "DarkCurtain", "RightToLeft", 1f);
            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayFromHere(0);
                controller.Next();
                controller.Advance(.25f);
                object overlay = Get(controller.CurrentFrame, "SceneTransitionOverlay");
                Assert.That(Get(overlay, "Direction").ToString(), Is.EqualTo("RightToLeft"));
                Assert.That((float)Get(overlay, "Coverage"), Is.EqualTo(.5f).Within(.05f));
            }
        }

        [Test]
        public void MT_TransitionCompletesAndUnlocksInput()
        {
            var project = ProjectWithTwoScenes();
            ConfigureTransition(project.scenes[1], "DarkCurtain", "LeftToRight", .4f);
            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayFromHere(0);
                controller.Next();
                controller.Advance(.5f);
                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1));
                Assert.That(GetBool(controller, "IsSceneTransitionActive"), Is.False);
                Assert.That(GetBool(controller, "SceneTransitionInputLocked"), Is.False);
            }
        }

        [Test]
        public void MT_ZeroDurationCurtainResolvesAsImmediateBoundary()
        {
            var project = ProjectWithTwoScenes();
            ConfigureTransition(project.scenes[1], "DarkCurtain", "LeftToRight", 0f);
            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayFromHere(0);
                controller.Next();
                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1));
                Assert.That(GetBool(controller, "IsSceneTransitionActive"), Is.False);
            }
        }

        [Test]
        public void MT_DirectPlaySceneDoesNotAnimateIncomingTransition()
        {
            var project = ProjectWithTwoScenes();
            ConfigureTransition(project.scenes[1], "DarkCurtain", "LeftToRight", 1f);
            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayScene(1);
                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1));
                Assert.That(GetBool(controller, "IsSceneTransitionActive"), Is.False);
            }
        }

        [Test]
        public void MT_SameBgmAcrossBoundaryDoesNotRestart()
        {
            var project = ProjectWithTwoScenes();
            SetTrack(project.scenes[0], TrackAGuid);
            SetTrack(project.scenes[1], TrackAGuid);
            ConfigureTransition(project.scenes[1], "DarkCurtain", "LeftToRight", .4f);
            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayFromHere(0);
                int starts = controller.MusicStartCount;
                controller.Next();
                controller.Advance(.5f);
                Assert.That(controller.MusicStartCount, Is.EqualTo(starts));
                Assert.That(controller.ActiveMusicSourceCount, Is.LessThanOrEqualTo(1));
            }
        }

        [Test]
        public void MT_KeepPreviousBgmAcrossBoundaryDoesNotRestart()
        {
            var project = ProjectWithTwoScenes();
            SetTrack(project.scenes[0], TrackAGuid);
            project.scenes[1].music.mode = VnSceneComposerMusicMode.KeepPrevious;
            ConfigureTransition(project.scenes[1], "DarkCurtain", "LeftToRight", .4f);
            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayFromHere(0);
                int starts = controller.MusicStartCount;
                controller.Next();
                controller.Advance(.5f);
                Assert.That(controller.MusicStartCount, Is.EqualTo(starts));
            }
        }

        [Test]
        public void MT_IncomingCharacterCompositionIsResolvedUnderCover()
        {
            var project = ProjectWithTwoScenes();
            project.scenes[1].characters.Add(new VnSceneComposerCharacter
            {
                characterId = "Mina",
                stateId = "mina_neutral",
                stageSlot = VnWorkshopStageSlot.Center
            });
            ConfigureTransition(project.scenes[1], "DarkCurtain", "LeftToRight", 1f);
            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayFromHere(0);
                controller.Next();
                controller.Advance(.51f);
                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1));
                Assert.That(controller.CurrentFrame.ComposerCharacters.Length, Is.EqualTo(1));
                Assert.That(controller.CurrentFrame.ComposerCharacters[0].CharacterId, Is.EqualTo("Mina"));
                Assert.That((bool)Get(controller.CurrentFrame, "ShowDialogueUi"), Is.False);
            }
        }

        [Test]
        public void MT_IncomingDecorationsBelongToTargetAtCoveredSwap()
        {
            var project = ProjectWithTwoScenes();
            project.scenes[0].decorations.Add(new VnSceneComposerDecoration { displayName = "OUT" });
            project.scenes[1].decorations.Add(new VnSceneComposerDecoration { displayName = "IN" });
            ConfigureTransition(project.scenes[1], "DarkCurtain", "LeftToRight", 1f);
            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayFromHere(0);
                controller.Next();
                controller.Advance(.51f);
                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1));
                Assert.That(project.scenes[controller.CurrentSceneIndex].decorations[0].displayName, Is.EqualTo("IN"));
                Assert.That((float)Get(Get(controller.CurrentFrame, "SceneTransitionOverlay"), "Coverage"),
                    Is.GreaterThan(.95f));
            }
        }

        [Test]
        public void MT_VideoReadinessHoldsFullCoverWithoutDuplicatePrepare()
        {
            var factory = new TransitionVideoFactory();
            VnSceneComposerMediaEditing.VideoPreviewFactory = factory;
            var project = ProjectWithTwoScenes();
            project.scenes[1].media = new VnSceneComposerMediaReference
            {
                kind = VnSceneComposerMediaKind.ExternalVideo,
                reference = "mtransition-ready.mp4",
                displayName = "mtransition-ready.mp4",
                contentHash = "ready",
                loop = false
            };
            ConfigureTransition(project.scenes[1], "DarkCurtain", "LeftToRight", .4f);
            try
            {
                using (var controller = new VnSceneComposerPlaybackController(project))
                {
                    controller.PlayFromHere(0);
                    controller.Next();
                    controller.Advance(.21f);
                    Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1));
                    Assert.That(GetBool(controller, "IsSceneTransitionActive"), Is.True);
                    Assert.That(factory.Created, Has.Count.EqualTo(1));
                    Assert.That(factory.Created[0].PrepareCalls, Is.EqualTo(1));

                    controller.Advance(3f);
                    Assert.That(GetBool(controller, "IsSceneTransitionActive"), Is.True,
                        "Reveal must wait for a real visible video frame.");
                    Assert.That(factory.Created[0].PrepareCalls, Is.EqualTo(1),
                        "Waiting for readiness must not duplicate Prepare.");

                    factory.Created[0].Visible = true;
                    controller.Advance(.01f);
                    controller.Advance(.25f);
                    Assert.That(GetBool(controller, "IsSceneTransitionActive"), Is.False);
                }
            }
            finally
            {
                VnSceneComposerMediaEditing.ResetVideoPreviewFactory();
                foreach (TransitionVideoPreview p in factory.Created) p.Dispose();
            }
        }

        private static VnSceneComposerProject ProjectWithTwoScenes()
        {
            var project = new VnSceneComposerProject();
            project.scenes.Add(new VnSceneComposerScene { label = "A" });
            project.scenes.Add(new VnSceneComposerScene { label = "B" });
            return project;
        }

        private static void ConfigureTransition(VnSceneComposerScene scene, string kind, string direction, float duration)
        {
            Type kindType = RequireType("VnSceneComposerSceneTransitionType");
            Type directionType = RequireType("VnSceneComposerSceneTransitionDirection");
            Set(scene.transition, "sceneTransitionType", Enum.Parse(kindType, kind));
            Set(scene.transition, "sceneTransitionDirection", Enum.Parse(directionType, direction));
            Set(scene.transition, "sceneTransitionDuration", duration);
        }

        private static void SetTrack(VnSceneComposerScene scene, string guid)
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

        private static Type RequireType(string shortName)
        {
            Assembly assembly = typeof(VnSceneComposerProject).Assembly;
            Type type = assembly.GetType(Namespace + shortName, false);
            Assert.That(type, Is.Not.Null, "Missing M-TRANSITION type: " + shortName);
            return type;
        }

        private static MethodInfo RequireInstance(Type type, string name, params Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, parameters ?? Type.EmptyTypes, null);
            Assert.That(method, Is.Not.Null, "Missing method: " + type.Name + "." + name);
            return method;
        }

        private static void AssertField(Type type, string name)
        {
            Assert.That(type.GetField(name, BindingFlags.Public | BindingFlags.Instance), Is.Not.Null,
                "Missing field: " + type.Name + "." + name);
        }

        private static object Get(object instance, string name)
        {
            Assert.That(instance, Is.Not.Null);
            Type type = instance.GetType();
            PropertyInfo p = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null) return p.GetValue(instance, null);
            FieldInfo f = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(f, Is.Not.Null, "Missing member: " + type.Name + "." + name);
            return f.GetValue(instance);
        }

        private static bool GetBool(object instance, string name) { return (bool)Get(instance, name); }

        private static void Set(object instance, string name, object value)
        {
            FieldInfo f = instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(f, Is.Not.Null, "Missing field: " + instance.GetType().Name + "." + name);
            f.SetValue(instance, value);
        }

        private static IList Scenes(object window)
        {
            return (IList)Get(Get(window, "_sceneComposerProject"), "scenes");
        }

        private sealed class TransitionVideoPreview : VnSceneComposerVideoPreview
        {
            public bool Visible;
            public int PrepareCalls;
            public int PlayCalls;
            public int PauseCalls;

            public TransitionVideoPreview() : base(null, 64, 36, false)
            {
                warning = string.Empty;
                texture = new RenderTexture(64, 36, 0);
                texture.Create();
            }

            public override bool IsPrepared { get { return true; } }
            public override bool IsPreparing { get { return false; } }
            public override bool IsPlaying { get { return false; } }
            public override bool HasVisibleFrame { get { return Visible; } }
            public override void Prepare() { PrepareCalls++; }
            public override void Play() { PlayCalls++; }
            public override void Pause() { PauseCalls++; }
        }

        private sealed class TransitionVideoFactory : IVnSceneComposerVideoPreviewFactory
        {
            public readonly System.Collections.Generic.List<TransitionVideoPreview> Created =
                new System.Collections.Generic.List<TransitionVideoPreview>();

            public VnSceneComposerVideoPreview Open(VnSceneComposerMediaReference media, int width, int height)
            {
                var preview = new TransitionVideoPreview();
                Created.Add(preview);
                return preview;
            }
        }
    }
}
