using System.IO;
using System.Linq;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using Rokas.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnRuntimeIntroPackageTests
    {
        private const string ProjectId = "3cc2bc9ec7974ea7a5f4d074683bed61";

        [Test]
        public void ExportSnapshotPreservesIdentityMovementAndTerminalWithoutHardcodedCounts()
        {
            var project = new VnSceneComposerProject
            {
                projectId = ProjectId,
                title = "Runtime Intro Fixture"
            };
            project.scenes.Clear();

            var first = new VnSceneComposerScene { label = "Opening" };
            first.dialogueBeats.Clear();
            first.dialogueBeats.Add(new VnSceneComposerDialogueBeat
            {
                speaker = "Mina",
                text = "Первый кадр",
                movement = new VnSceneComposerBeatMovement
                {
                    primary = new VnSceneComposerCharacterMovementAction
                    {
                        characterId = "Mina",
                        action = VnSceneComposerMovementActionType.MoveCenter,
                        duration = .75f
                    },
                    secondary = new VnSceneComposerCharacterMovementAction
                    {
                        characterId = "Keiko",
                        action = VnSceneComposerMovementActionType.ExitRight,
                        duration = 1.1f
                    },
                    secondaryTiming = VnSceneComposerMovementTiming.Simultaneous
                }
            });

            var terminal = new VnSceneComposerScene
            {
                label = "Final",
                isTerminal = true,
                terminalFadeDuration = 1.25f
            };
            terminal.dialogueBeats[0].speaker = "Keiko";
            terminal.dialogueBeats[0].text = "До встречи.";

            project.scenes.Add(first);
            project.scenes.Add(terminal);

            RokasVnRuntimeIntroSnapshot snapshot =
                RokasVnRuntimeIntroExporter.BuildSnapshot(project, "fixture-sha256");

            Assert.That(snapshot.projectId, Is.EqualTo(ProjectId));
            Assert.That(snapshot.sourceProjectSha256, Is.EqualTo("fixture-sha256"));
            Assert.That(snapshot.scenes.Count, Is.EqualTo(2));
            Assert.That(snapshot.scenes[0].dialogueBeats.Count, Is.EqualTo(1));
            Assert.That(snapshot.scenes[0].dialogueBeats[0].movement.primary.action,
                Is.EqualTo((int)VnSceneComposerMovementActionType.MoveCenter));
            Assert.That(snapshot.scenes[0].dialogueBeats[0].movement.secondary.action,
                Is.EqualTo((int)VnSceneComposerMovementActionType.ExitRight));
            Assert.That(snapshot.scenes[0].dialogueBeats[0].movement.secondaryTiming,
                Is.EqualTo((int)VnSceneComposerMovementTiming.Simultaneous));
            Assert.That(snapshot.scenes[1].isTerminal, Is.True);
            Assert.That(snapshot.scenes[1].terminalFadeDuration, Is.EqualTo(1.25f).Within(.001f));
            Assert.That(snapshot.sceneCount, Is.EqualTo(2));
            Assert.That(snapshot.beatCount, Is.EqualTo(2));
        }

        [Test]
        public void SpeakerFocusSamplesMatchAuthoritativePreviewResolverAcrossSwitch()
        {
            var project = new VnSceneComposerProject
            {
                projectId = ProjectId,
                title = "Speaker Focus Parity Fixture",
                defaultPresentation = new VnPresentationWorkshopPreset()
            };
            project.scenes.Clear();

            VnPresentationWorkshopVn10Resolver.SetSpeakerFocusPreviewOverrides(
                project.defaultPresentation,
                1.08f,
                1.10f,
                14f,
                .90f,
                .65f,
                .70f,
                .50f,
                VnWorkshopEasing.Linear);

            var scene = new VnSceneComposerScene
            {
                label = "Focus"
            };
            scene.characters.Clear();
            scene.characters.Add(new VnSceneComposerCharacter
            {
                characterId = "Mina",
                stateId = "mina_neutral",
                stageSlot = VnWorkshopStageSlot.Left
            });
            scene.characters.Add(new VnSceneComposerCharacter
            {
                characterId = "Keiko",
                stateId = "keiko_neutral",
                stageSlot = VnWorkshopStageSlot.Right
            });
            scene.dialogueBeats.Clear();
            scene.dialogueBeats.Add(new VnSceneComposerDialogueBeat
            {
                speaker = "Mina",
                text = string.Empty
            });
            scene.dialogueBeats.Add(new VnSceneComposerDialogueBeat
            {
                speaker = "Keiko",
                text = string.Empty
            });
            project.scenes.Add(scene);

            VnWorkshopSpeakerFocusValues focus =
                VnPresentationWorkshopVn10Resolver.ResolveSpeakerFocus(
                    project.defaultPresentation);
            RokasVnRuntimeIntroSnapshot snapshot =
                RokasVnRuntimeIntroExporter.BuildSnapshot(
                    project,
                    "speaker-focus-parity");
            RokasVnRuntimePresentationSnapshot runtimePresentation =
                snapshot.scenes[0].presentation;

            Assert.That(runtimePresentation.speakerActiveScale,
                Is.EqualTo(focus.ActiveScale).Within(.0001f));
            Assert.That(runtimePresentation.speakerActiveBrightness,
                Is.EqualTo(focus.ActiveBrightness).Within(.0001f));
            Assert.That(runtimePresentation.speakerActiveForwardOffset,
                Is.EqualTo(focus.ActiveForwardOffset).Within(.0001f));
            Assert.That(runtimePresentation.speakerInactiveScale,
                Is.EqualTo(focus.InactiveScale).Within(.0001f));
            Assert.That(runtimePresentation.speakerInactiveBrightness,
                Is.EqualTo(focus.InactiveBrightness).Within(.0001f));
            Assert.That(runtimePresentation.speakerInactiveAlpha,
                Is.EqualTo(focus.InactiveAlpha).Within(.0001f));
            Assert.That(runtimePresentation.speakerFocusTransitionDuration,
                Is.EqualTo(focus.TransitionDuration).Within(.0001f));
            Assert.That(runtimePresentation.speakerFocusEasing,
                Is.EqualTo((int)focus.Easing));

            var playback =
                new RokasVnRuntimePlaybackState(snapshot);
            playback.StartFromBeginning();
            playback.AdvanceDialogue();
            Assert.That(playback.CurrentBeatIndex, Is.EqualTo(1));

            AssertSpeakerFocusParity(
                playback,
                runtimePresentation,
                focus,
                0f);
            playback.AdvanceTime(.25f);
            AssertSpeakerFocusParity(
                playback,
                runtimePresentation,
                focus,
                .5f);
            playback.AdvanceTime(.25f);
            AssertSpeakerFocusParity(
                playback,
                runtimePresentation,
                focus,
                1f);
        }

        private static void AssertSpeakerFocusParity(
            RokasVnRuntimePlaybackState playback,
            RokasVnRuntimePresentationSnapshot presentation,
            VnWorkshopSpeakerFocusValues focus,
            float normalizedProgress)
        {
            RokasVnRuntimeCharacterSample mina =
                playback.SampleCharacter("Mina");
            RokasVnRuntimeCharacterSample keiko =
                playback.SampleCharacter("Keiko");

            VnWorkshopSpeakerFocusSample expectedMina =
                VnPresentationWorkshopVn10Resolver.SampleSpeakerFocus(
                    2, 0, 1, 0,
                    normalizedProgress,
                    focus);
            VnWorkshopSpeakerFocusSample expectedKeiko =
                VnPresentationWorkshopVn10Resolver.SampleSpeakerFocus(
                    2, 0, 1, 1,
                    normalizedProgress,
                    focus);

            Assert.That(
                mina.scale / presentation.leftScale,
                Is.EqualTo(expectedMina.Scale).Within(.0001f));
            Assert.That(
                keiko.scale / presentation.rightScale,
                Is.EqualTo(expectedKeiko.Scale).Within(.0001f));
            Assert.That(
                mina.brightness,
                Is.EqualTo(expectedMina.Brightness).Within(.0001f));
            Assert.That(
                keiko.brightness,
                Is.EqualTo(expectedKeiko.Brightness).Within(.0001f));
            Assert.That(
                mina.alpha,
                Is.EqualTo(expectedMina.Alpha).Within(.0001f));
            Assert.That(
                keiko.alpha,
                Is.EqualTo(expectedKeiko.Alpha).Within(.0001f));
            Assert.That(
                mina.position.y - presentation.slotY,
                Is.EqualTo(expectedMina.PositionOffset.y).Within(.0001f));
            Assert.That(
                keiko.position.y - presentation.slotY,
                Is.EqualTo(expectedKeiko.PositionOffset.y).Within(.0001f));
        }

        [Test]
        public void NarrationWithTwoVisibleCharactersDoesNotInventSpeakerFocus()
        {
            var project = new VnSceneComposerProject
            {
                projectId = ProjectId,
                title = "Narration Focus Parity Fixture",
                defaultPresentation = new VnPresentationWorkshopPreset()
            };
            project.scenes.Clear();

            VnPresentationWorkshopVn10Resolver.SetSpeakerFocusPreviewOverrides(
                project.defaultPresentation,
                1.08f,
                1.10f,
                14f,
                .90f,
                .65f,
                .70f,
                .50f,
                VnWorkshopEasing.Linear);

            var scene = new VnSceneComposerScene
            {
                label = "Narration"
            };
            scene.characters.Clear();
            scene.characters.Add(new VnSceneComposerCharacter
            {
                characterId = "Mina",
                stateId = "mina_neutral",
                stageSlot = VnWorkshopStageSlot.Left
            });
            scene.characters.Add(new VnSceneComposerCharacter
            {
                characterId = "Keiko",
                stateId = "keiko_neutral",
                stageSlot = VnWorkshopStageSlot.Right
            });
            scene.dialogueBeats.Clear();
            var beat = new VnSceneComposerDialogueBeat
            {
                narration = true,
                speaker = string.Empty,
                text = string.Empty
            };
            scene.dialogueBeats.Add(beat);
            project.scenes.Add(scene);

            VnWorkshopPreviewFrame preview =
                VnSceneComposerComposition.BuildFrame(
                    project,
                    scene,
                    beat,
                    VnWorkshopResolution.Reference1920x1080,
                    null,
                    1f);
            VnWorkshopPreviewCharacter previewMina =
                preview.ComposerCharacters.First(
                    item => item != null &&
                        item.CharacterId == "Mina");
            VnWorkshopPreviewCharacter previewKeiko =
                preview.ComposerCharacters.First(
                    item => item != null &&
                        item.CharacterId == "Keiko");

            RokasVnRuntimeIntroSnapshot snapshot =
                RokasVnRuntimeIntroExporter.BuildSnapshot(
                    project,
                    "narration-focus-parity");
            var runtime =
                new RokasVnRuntimePlaybackState(snapshot);
            runtime.StartFromBeginning();

            RokasVnRuntimeCharacterSample runtimeMina =
                runtime.SampleCharacter("Mina");
            RokasVnRuntimeCharacterSample runtimeKeiko =
                runtime.SampleCharacter("Keiko");

            Assert.That(runtimeMina.brightness,
                Is.EqualTo(previewMina.Brightness).Within(.0001f));
            Assert.That(runtimeKeiko.brightness,
                Is.EqualTo(previewKeiko.Brightness).Within(.0001f));
            Assert.That(runtimeMina.alpha,
                Is.EqualTo(previewMina.Alpha).Within(.0001f));
            Assert.That(runtimeKeiko.alpha,
                Is.EqualTo(previewKeiko.Alpha).Within(.0001f));
            Assert.That(runtimeMina.brightness,
                Is.EqualTo(1f).Within(.0001f),
                "Narration must not invent an active first character.");
            Assert.That(runtimeKeiko.brightness,
                Is.EqualTo(1f).Within(.0001f),
                "Narration must leave all visible characters unfocused exactly like Preview.");
        }

        [Test]
        public void MovementAndReplicaEffectSamplesMatchAuthoritativePreviewSamplers()
        {
            VnSceneComposerMovementActionType[] actions =
            {
                VnSceneComposerMovementActionType.ExitLeft,
                VnSceneComposerMovementActionType.ExitRight,
                VnSceneComposerMovementActionType.Disappear,
                VnSceneComposerMovementActionType.MoveLeft,
                VnSceneComposerMovementActionType.MoveCenter,
                VnSceneComposerMovementActionType.MoveRight
            };

            foreach (VnSceneComposerMovementActionType action in actions)
            {
                var project = new VnSceneComposerProject
                {
                    projectId = ProjectId,
                    title = "Movement Parity Fixture",
                    defaultPresentation = new VnPresentationWorkshopPreset()
                };
                project.scenes.Clear();
                var scene = new VnSceneComposerScene();
                scene.characters.Clear();
                scene.characters.Add(new VnSceneComposerCharacter
                {
                    characterId = "Mina",
                    stateId = "mina_neutral",
                    stageSlot = VnWorkshopStageSlot.Center
                });
                scene.dialogueBeats.Clear();
                var beat = new VnSceneComposerDialogueBeat
                {
                    speaker = "Mina",
                    text = string.Empty
                };
                beat.movement.primary.characterId = "Mina";
                beat.movement.primary.action = action;
                beat.movement.primary.duration = .8f;
                scene.dialogueBeats.Add(beat);
                project.scenes.Add(scene);

                VnWorkshopStageLayoutValues stage =
                    VnPresentationWorkshopVn10Resolver.ResolveStageLayout(
                        project.defaultPresentation);
                const float elapsed = .37f;
                VnSceneComposerResolvedMovementSample expected =
                    VnSceneComposerMovementResolver.Sample(
                        scene,
                        beat,
                        "Mina",
                        elapsed,
                        stage,
                        1920f,
                        null);

                RokasVnRuntimeIntroSnapshot snapshot =
                    RokasVnRuntimeIntroExporter.BuildSnapshot(
                        project,
                        "movement-parity-" + action);
                var runtime =
                    new RokasVnRuntimePlaybackState(snapshot);
                runtime.StartFromBeginning();
                runtime.AdvanceTime(elapsed);
                RokasVnRuntimeCharacterSample actual =
                    runtime.SampleCharacter("Mina");

                Assert.That(actual.visible, Is.EqualTo(expected.Visible), action.ToString());
                Assert.That(actual.alpha, Is.EqualTo(expected.Alpha).Within(.0001f), action.ToString());
                Assert.That(actual.stageSlot, Is.EqualTo((int)expected.StageSlot), action.ToString());
                Assert.That(actual.position.x, Is.EqualTo(expected.StagePosition.x).Within(.0001f), action.ToString());
                Assert.That(actual.position.y, Is.EqualTo(expected.StagePosition.y).Within(.0001f), action.ToString());
                Assert.That(actual.scale, Is.EqualTo(expected.StageScale).Within(.0001f), action.ToString());
            }

            VnSceneComposerReplicaEffectType[] effectTypes =
            {
                VnSceneComposerReplicaEffectType.Shake,
                VnSceneComposerReplicaEffectType.Punch,
                VnSceneComposerReplicaEffectType.Flash,
                VnSceneComposerReplicaEffectType.Pulse
            };

            foreach (VnSceneComposerReplicaEffectType effectType in effectTypes)
            {
                var project = new VnSceneComposerProject
                {
                    projectId = ProjectId,
                    title = "Effect Parity Fixture"
                };
                project.scenes.Clear();
                var scene = new VnSceneComposerScene();
                scene.dialogueBeats.Clear();
                var beat = new VnSceneComposerDialogueBeat
                {
                    text = string.Empty,
                    replicaEffect = new VnSceneComposerReplicaEffect
                    {
                        type = effectType,
                        intensity = .63f,
                        duration = .8f,
                        frequency = 9f,
                        decay = 1.7f,
                        direction = new Vector2(-2f, 1f),
                        flashColor = new Color(.8f, .3f, .2f, .75f)
                    }
                };
                scene.dialogueBeats.Add(beat);
                project.scenes.Add(scene);

                const float elapsed = .31f;
                VnSceneComposerReplicaEffectSample expected =
                    VnSceneComposerReplicaEffects.Sample(
                        beat.replicaEffect,
                        elapsed);
                RokasVnRuntimeIntroSnapshot snapshot =
                    RokasVnRuntimeIntroExporter.BuildSnapshot(
                        project,
                        "effect-parity-" + effectType);
                var runtime =
                    new RokasVnRuntimePlaybackState(snapshot);
                runtime.StartFromBeginning();
                runtime.AdvanceTime(elapsed);
                RokasVnRuntimeReplicaEffectSample actual =
                    runtime.SampleReplicaEffect();

                Assert.That(actual.active, Is.EqualTo(expected.Active), effectType.ToString());
                Assert.That(actual.offset.x, Is.EqualTo(expected.Offset.x).Within(.0001f), effectType.ToString());
                Assert.That(actual.offset.y, Is.EqualTo(expected.Offset.y).Within(.0001f), effectType.ToString());
                Assert.That(actual.scale, Is.EqualTo(expected.Scale).Within(.0001f), effectType.ToString());
                Assert.That(actual.flash.r, Is.EqualTo(expected.Flash.r).Within(.0001f), effectType.ToString());
                Assert.That(actual.flash.g, Is.EqualTo(expected.Flash.g).Within(.0001f), effectType.ToString());
                Assert.That(actual.flash.b, Is.EqualTo(expected.Flash.b).Within(.0001f), effectType.ToString());
                Assert.That(actual.flash.a, Is.EqualTo(expected.Flash.a).Within(.0001f), effectType.ToString());
            }
        }

        [Test]
        public void SecondaryMovementTimingMatchesPreviewForSimultaneousAndAfterPrimary()
        {
            foreach (VnSceneComposerMovementTiming timing in new[]
                     {
                         VnSceneComposerMovementTiming.Simultaneous,
                         VnSceneComposerMovementTiming.AfterPrimary
                     })
            {
                var project = new VnSceneComposerProject
                {
                    projectId = ProjectId,
                    title = "Secondary Movement Parity Fixture",
                    defaultPresentation = new VnPresentationWorkshopPreset()
                };
                project.scenes.Clear();
                var scene = new VnSceneComposerScene();
                scene.characters.Clear();
                scene.characters.Add(new VnSceneComposerCharacter
                {
                    characterId = "Mina",
                    stateId = "mina_neutral",
                    stageSlot = VnWorkshopStageSlot.Left
                });
                scene.characters.Add(new VnSceneComposerCharacter
                {
                    characterId = "Keiko",
                    stateId = "keiko_neutral",
                    stageSlot = VnWorkshopStageSlot.Right
                });
                scene.dialogueBeats.Clear();
                var beat = new VnSceneComposerDialogueBeat
                {
                    text = string.Empty
                };
                beat.movement.primary.characterId = "Mina";
                beat.movement.primary.action =
                    VnSceneComposerMovementActionType.MoveCenter;
                beat.movement.primary.duration = .8f;
                beat.movement.secondary.characterId = "Keiko";
                beat.movement.secondary.action =
                    VnSceneComposerMovementActionType.MoveCenter;
                beat.movement.secondary.duration = .6f;
                beat.movement.secondaryTiming = timing;
                scene.dialogueBeats.Add(beat);
                project.scenes.Add(scene);

                VnWorkshopStageLayoutValues stage =
                    VnPresentationWorkshopVn10Resolver.ResolveStageLayout(
                        project.defaultPresentation);
                foreach (float elapsed in new[] { .35f, .95f, 1.45f })
                {
                    RokasVnRuntimeIntroSnapshot snapshot =
                        RokasVnRuntimeIntroExporter.BuildSnapshot(
                            project,
                            "secondary-parity-" + timing + "-" + elapsed);
                    var runtime =
                        new RokasVnRuntimePlaybackState(snapshot);
                    runtime.StartFromBeginning();
                    runtime.AdvanceTime(elapsed);

                    foreach (string id in new[] { "Mina", "Keiko" })
                    {
                        VnSceneComposerResolvedMovementSample expected =
                            VnSceneComposerMovementResolver.Sample(
                                scene,
                                beat,
                                id,
                                elapsed,
                                stage,
                                1920f,
                                null);
                        RokasVnRuntimeCharacterSample actual =
                            runtime.SampleCharacter(id);
                        Assert.That(actual.visible, Is.EqualTo(expected.Visible),
                            timing + " " + id + " @" + elapsed);
                        Assert.That(actual.alpha,
                            Is.EqualTo(expected.Alpha).Within(.0001f),
                            timing + " " + id + " @" + elapsed);
                        Assert.That(actual.position.x,
                            Is.EqualTo(expected.StagePosition.x).Within(.0001f),
                            timing + " " + id + " @" + elapsed);
                        Assert.That(actual.position.y,
                            Is.EqualTo(expected.StagePosition.y).Within(.0001f),
                            timing + " " + id + " @" + elapsed);
                        Assert.That(actual.scale,
                            Is.EqualTo(expected.StageScale).Within(.0001f),
                            timing + " " + id + " @" + elapsed);
                    }
                }
            }
        }

        [Test]
        public void ProjectOwnedExportCopiesAuthoredGuidAssetsAndRuntimeUiOutsideEditor()
        {
            const string root =
                "Assets/Rokas/Tests/EditorWorkshop/__RuntimeIntroExportFixture";
            const string sourcePath = root + "/SourceBackground.png";
            const string runtimeRoot = root + "/Runtime";
            const string packagePath = runtimeRoot + "/RokasVnRuntimeIntroPackage.asset";

            AssetDatabase.DeleteAsset(root);
            Directory.CreateDirectory(root);
            var sourceTexture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            try
            {
                Color[] pixels = new Color[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.magenta;
                sourceTexture.SetPixels(pixels);
                sourceTexture.Apply();
                File.WriteAllBytes(sourcePath, sourceTexture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(sourceTexture);
            }
            AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceSynchronousImport);

            try
            {
                string guid = AssetDatabase.AssetPathToGUID(sourcePath);
                Assert.That(guid, Is.Not.Empty);

                var project = new VnSceneComposerProject
                {
                    projectId = ProjectId,
                    title = "Asset Export Fixture"
                };
                project.scenes.Clear();
                var scene = new VnSceneComposerScene
                {
                    label = "Terminal",
                    isTerminal = true,
                    terminalFadeDuration = .4f,
                    media = new VnSceneComposerMediaReference
                    {
                        kind = VnSceneComposerMediaKind.ExistingRokasAsset,
                        reference = guid,
                        displayName = "SourceBackground",
                        scaleMode = VnSceneComposerMediaScaleMode.Fill
                    }
                };
                scene.dialogueBeats[0].text = "Готово.";
                project.scenes.Add(scene);

                RokasVnRuntimeIntroPackage package =
                    RokasVnRuntimeIntroExporter.ExportProjectOwnedPackage(
                        project,
                        JsonUtility.ToJson(project),
                        "asset-fixture-sha256",
                        packagePath,
                        runtimeRoot);

                Assert.That(package, Is.Not.Null);
                string error;
                Assert.That(package.TryValidate(out error), Is.True, error);
                Assert.That(package.TryGetAsset(
                    guid, out RokasVnRuntimeAssetBinding binding), Is.True);
                Assert.That(binding.asset, Is.Not.Null);
                string copied = AssetDatabase.GetAssetPath(binding.asset);
                Assert.That(copied, Does.StartWith(runtimeRoot + "/"));
                Assert.That(copied, Does.Not.Contain("/Editor/"));
                Assert.That(package.DialoguePlaque, Is.Not.Null);
                Assert.That(package.ControlSheet, Is.Not.Null);
                Assert.That(package.CompletionTriangle, Is.Not.Null);
                Assert.That(
                    AssetDatabase.GetAssetPath(package.DialoguePlaque),
                    Does.StartWith(runtimeRoot + "/"));
                Assert.That(
                    AssetDatabase.GetAssetPath(package.ControlSheet),
                    Does.StartWith(runtimeRoot + "/"));
                Assert.That(
                    AssetDatabase.GetAssetPath(package.CompletionTriangle),
                    Does.StartWith(runtimeRoot + "/"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
                AssetDatabase.Refresh();
            }
        }


        [Test]
        public void ProjectOwnedExternalPngPreservesNativeWidthThroughPreviewAndRuntimeExport()
        {
            const string root =
                "Assets/Rokas/Tests/EditorWorkshop/__RuntimeProjectOwnedNativeImageFixture";
            const string runtimeRoot = root + "/Runtime";
            const string packagePath =
                runtimeRoot + "/RokasVnRuntimeIntroPackage.asset";
            string externalPath = Path.Combine(
                Path.GetTempPath(),
                "RokasVnProjectOwnedNative2305-" +
                System.Guid.NewGuid().ToString("N") +
                ".png");

            AssetDatabase.DeleteAsset(root);
            Directory.CreateDirectory(root);
            var sourceTexture =
                new Texture2D(2305, 7, TextureFormat.RGBA32, false);
            string importedPath = null;
            string stableId = null;
            try
            {
                sourceTexture.SetPixel(0, 0, Color.cyan);
                sourceTexture.SetPixel(2304, 6, Color.magenta);
                sourceTexture.Apply(false, false);
                File.WriteAllBytes(
                    externalPath,
                    sourceTexture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(sourceTexture);
            }

            try
            {
                var project = new VnSceneComposerProject
                {
                    projectId = ProjectId,
                    title = "Project-Owned Native External Image Fixture"
                };
                project.scenes.Clear();
                var scene = new VnSceneComposerScene
                {
                    label = "Terminal",
                    isTerminal = true,
                    terminalFadeDuration = .4f
                };
                scene.dialogueBeats[0].text = "Native project image";
                VnSceneComposerMediaEditing.SetExternalImage(
                    scene,
                    externalPath,
                    VnSceneComposerMediaScaleMode.Fit);
                project.scenes.Add(scene);

                Assert.That(
                    scene.media.kind,
                    Is.EqualTo(
                        VnSceneComposerMediaKind.ExistingRokasAsset),
                    "Current image onboarding path must be tested, not the legacy ExternalImage path.");

                importedPath =
                    AssetDatabase.GUIDToAssetPath(
                        scene.media.reference);
                Assert.That(importedPath, Is.Not.Empty);
                stableId =
                    VnSceneComposerAssetLibrary.FindByPurpose(
                        VnSceneComposerAssetLibrary.GetDefaultProjectRoot(),
                        VnSceneComposerAssetPurpose.Background)
                    .Single(entry =>
                        entry.assetGuid ==
                        scene.media.reference)
                    .stableAssetId;

                using (VnSceneComposerImagePreview preview =
                       VnSceneComposerMediaEditing.OpenImagePreview(
                           scene.media))
                {
                    Assert.That(preview.texture, Is.Not.Null);
                    Assert.That(
                        preview.texture.width,
                        Is.EqualTo(2305),
                        "Authoritative Preview must retain source/native width for project-owned VN stills.");
                    Assert.That(
                        preview.texture.height,
                        Is.EqualTo(7));
                }

                RokasVnRuntimeIntroPackage package =
                    RokasVnRuntimeIntroExporter.ExportProjectOwnedPackage(
                        project,
                        JsonUtility.ToJson(project),
                        "project-owned-native-image-fixture",
                        packagePath,
                        runtimeRoot);

                Assert.That(
                    package.TryGetAsset(
                        scene.media.reference,
                        out RokasVnRuntimeAssetBinding binding),
                    Is.True);
                Texture2D runtimeTexture =
                    binding.asset as Texture2D;
                Assert.That(runtimeTexture, Is.Not.Null);
                Assert.That(
                    runtimeTexture.width,
                    Is.EqualTo(2305),
                    "Runtime export must preserve the same native image dimensions as Preview.");
                Assert.That(
                    runtimeTexture.height,
                    Is.EqualTo(7));

                TextureImporter runtimeImporter =
                    AssetImporter.GetAtPath(
                        AssetDatabase.GetAssetPath(
                            runtimeTexture))
                    as TextureImporter;
                Assert.That(runtimeImporter, Is.Not.Null);
                Assert.That(
                    runtimeImporter.textureCompression,
                    Is.EqualTo(
                        TextureImporterCompression.Uncompressed));
                Assert.That(
                    runtimeImporter.npotScale,
                    Is.EqualTo(
                        TextureImporterNPOTScale.None));
                Assert.That(
                    runtimeImporter.filterMode,
                    Is.EqualTo(FilterMode.Bilinear));
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
                if (!string.IsNullOrEmpty(stableId))
                    VnSceneComposerAssetLibrary.Unregister(
                        VnSceneComposerAssetLibrary.GetDefaultProjectRoot(),
                        stableId);
                if (!string.IsNullOrEmpty(importedPath))
                    AssetDatabase.DeleteAsset(importedPath);
                AssetDatabase.Refresh();
                if (File.Exists(externalPath))
                    File.Delete(externalPath);
            }
        }

        [Test]
        public void ExternalPngExportPreservesNativeWidthAboveUnityDefaultMaxSize()
        {
            const string root =
                "Assets/Rokas/Tests/EditorWorkshop/__RuntimeNativeImageFixture";
            const string runtimeRoot = root + "/Runtime";
            const string packagePath =
                runtimeRoot + "/RokasVnRuntimeIntroPackage.asset";
            string externalPath = Path.Combine(
                Path.GetTempPath(),
                "RokasVnRuntimeNative2305.png");

            AssetDatabase.DeleteAsset(root);
            Directory.CreateDirectory(root);
            if (File.Exists(externalPath)) File.Delete(externalPath);

            var sourceTexture =
                new Texture2D(2305, 7, TextureFormat.RGBA32, false);
            try
            {
                sourceTexture.SetPixel(0, 0, Color.cyan);
                sourceTexture.Apply(false, false);
                File.WriteAllBytes(
                    externalPath,
                    sourceTexture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(sourceTexture);
            }

            try
            {
                var project = new VnSceneComposerProject
                {
                    projectId = ProjectId,
                    title = "Native External Image Fixture"
                };
                project.scenes.Clear();
                var scene = new VnSceneComposerScene
                {
                    label = "Terminal",
                    isTerminal = true,
                    terminalFadeDuration = .4f,
                    media = new VnSceneComposerMediaReference
                    {
                        kind = VnSceneComposerMediaKind.ExternalImage,
                        reference = externalPath,
                        displayName = "Native2305.png",
                        scaleMode = VnSceneComposerMediaScaleMode.Fit
                    }
                };
                scene.dialogueBeats[0].text = "Native image";
                project.scenes.Add(scene);

                RokasVnRuntimeIntroPackage package =
                    RokasVnRuntimeIntroExporter.ExportProjectOwnedPackage(
                        project,
                        JsonUtility.ToJson(project),
                        "native-image-fixture-sha256",
                        packagePath,
                        runtimeRoot);

                string key = "scene-media:" + scene.sceneId;
                Assert.That(
                    package.TryGetAsset(
                        key,
                        out RokasVnRuntimeAssetBinding binding),
                    Is.True);
                Texture2D exported = binding.asset as Texture2D;
                Assert.That(exported, Is.Not.Null);
                Assert.That(
                    exported.width,
                    Is.EqualTo(2305),
                    "Runtime packaging must not silently reduce authored 2305 px PNG media to Unity's 2048 px default import limit.");
                Assert.That(exported.height, Is.EqualTo(7));
            }
            finally
            {
                AssetDatabase.DeleteAsset(root);
                AssetDatabase.Refresh();
                if (File.Exists(externalPath))
                    File.Delete(externalPath);
            }
        }

        [Test]
        public void RuntimePlayerDecodesPackagedGifBytesIntoBackgroundTexture()
        {
            const string root =
                "Assets/Rokas/Tests/EditorWorkshop/__RuntimeGifFixture";
            const string gifPath = root + "/Tiny.gif.bytes";
            AssetDatabase.DeleteAsset(root);
            Directory.CreateDirectory(root);
            File.WriteAllBytes(
                gifPath,
                System.Convert.FromBase64String(
                    "R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw=="));
            AssetDatabase.ImportAsset(
                gifPath,
                ImportAssetOptions.ForceSynchronousImport);
            TextAsset gif = AssetDatabase.LoadAssetAtPath<TextAsset>(gifPath);
            Assert.That(gif, Is.Not.Null);

            var scene = new RokasVnRuntimeSceneSnapshot
            {
                sceneId = "77777777777777777777777777777777",
                isTerminal = true,
                terminalFadeDuration = .25f,
                media = new RokasVnRuntimeMediaSnapshot
                {
                    kind = 4,
                    runtimeAssetKey =
                        "scene-media:77777777777777777777777777777777",
                    loop = true,
                    scaleMode = 1
                }
            };
            scene.dialogueBeats.Add(new RokasVnRuntimeBeatSnapshot
            {
                beatId = "88888888888888888888888888888888",
                text = string.Empty
            });
            var snapshot = new RokasVnRuntimeIntroSnapshot
            {
                projectId = ProjectId,
                sourceProjectSha256 = "gif-fixture-sha",
                sceneCount = 1,
                beatCount = 1
            };
            snapshot.scenes.Add(scene);

            var package =
                ScriptableObject.CreateInstance<RokasVnRuntimeIntroPackage>();
            var host = new GameObject("RuntimeGifHost");
            RokasVnRuntimePlayer player = null;
            try
            {
                package.Configure(
                    ProjectId,
                    "gif-fixture-sha",
                    "{}",
                    snapshot,
                    new[]
                    {
                        new RokasVnRuntimeAssetBinding
                        {
                            authoredKey =
                                "scene-media:77777777777777777777777777777777",
                            displayName = "Tiny.gif",
                            kind = RokasVnRuntimeAssetKind.Bytes,
                            asset = gif
                        }
                    },
                    null,
                    null, null, null, null);

                player =
                    RokasVnRuntimePlayer.Create(
                        host.transform,
                        package,
                        null);
                RawImage image = host
                    .GetComponentsInChildren<RawImage>(true)
                    .First(item => item.name == "VnBackground");
                Assert.That(image.texture, Is.TypeOf<Texture2D>(),
                    "Packaged GIF bytes must decode inside the player assembly rather than depend on Editor preview state.");
            }
            finally
            {
                if (player != null) player.Dispose();
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(package);
                AssetDatabase.DeleteAsset(root);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void RuntimePackageContainerAcceptsOtherProjectIdsWhenPackageAndSnapshotMatch()
        {
            const string otherProjectId = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
            var scene = new RokasVnRuntimeSceneSnapshot
            {
                sceneId = "ffffffffffffffffffffffffffffffff",
                isTerminal = true,
                terminalFadeDuration = .25f
            };
            scene.dialogueBeats.Add(new RokasVnRuntimeBeatSnapshot
            {
                beatId = "abababababababababababababababab",
                text = "Future episode"
            });
            var snapshot = new RokasVnRuntimeIntroSnapshot
            {
                projectId = otherProjectId,
                sourceProjectSha256 = "future-episode-sha",
                sceneCount = 1,
                beatCount = 1
            };
            snapshot.scenes.Add(scene);

            var package = ScriptableObject.CreateInstance<RokasVnRuntimeIntroPackage>();
            try
            {
                package.ConfigureForTests(
                    otherProjectId,
                    "future-episode-sha",
                    snapshot);
                string error;
                Assert.That(package.TryValidate(out error), Is.True, error,
                    "Runtime package validation must be reusable; only the Intro exporter should own the current Intro Project ID.");
            }
            finally
            {
                Object.DestroyImmediate(package);
            }
        }

        [Test]
        public void RuntimePackageValidationRequiresExactIntroProjectIdAndAtLeastOneTerminalScene()
        {
            var package = UnityEngine.ScriptableObject.CreateInstance<RokasVnRuntimeIntroPackage>();
            try
            {
                package.ConfigureForTests(
                    ProjectId,
                    "fixture-sha256",
                    new RokasVnRuntimeIntroSnapshot
                    {
                        projectId = ProjectId,
                        sourceProjectSha256 = "fixture-sha256"
                    });

                string error;
                Assert.That(package.TryValidate(out error), Is.False);
                Assert.That(error, Does.Contain("terminal"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(package);
            }
        }
    }
}
