using System.IO;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using Rokas.Presentation;
using UnityEditor;
using UnityEngine;

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
