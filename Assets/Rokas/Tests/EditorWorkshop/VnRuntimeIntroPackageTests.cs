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
