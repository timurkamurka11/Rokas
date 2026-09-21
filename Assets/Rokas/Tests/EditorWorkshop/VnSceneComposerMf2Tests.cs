using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerMf2Tests
    {
        private const string DarkPanelPath = "Assets/Rokas/Art/VN/UI/Dialogue/VN_DialoguePanel_Keiko_Dark.png";
        private const string LightPanelPath = "Assets/Rokas/Art/VN/UI/Dialogue/VN_DialoguePanel_Mina_Light.png";
        private const string DarkPanelGuid = "66e03e45e4f849e4a8471bc16d6aa83d";
        private const string LightPanelGuid = "db6ce06f7a32417097fc2b2b6376c2d3";

        private sealed class Mf2VideoPreview : VnSceneComposerVideoPreview
        {
            public int PrepareCalls;
            public int RestartCalls;
            public int DisposeCalls;
            private bool prepared;

            internal Mf2VideoPreview() : base(null, 16, 16, false)
            {
                texture = new RenderTexture(32, 18, 0) { name = "ROKAS_MF2_VIDEO" };
                texture.Create();
                warning = string.Empty;
            }

            public override bool IsPrepared { get { return prepared; } }
            public override bool IsPreparing { get { return false; } }
            public override bool HasVisibleFrame { get { return prepared; } }

            public override void Prepare()
            {
                PrepareCalls++;
                prepared = true;
            }

            public override void Restart()
            {
                RestartCalls++;
            }

            public override void Dispose()
            {
                DisposeCalls++;
                base.Dispose();
            }
        }

        private sealed class Mf2VideoFactory : IVnSceneComposerVideoPreviewFactory
        {
            public Mf2VideoPreview Preview;

            public VnSceneComposerVideoPreview Open(VnSceneComposerMediaReference media, int width, int height)
            {
                Preview = new Mf2VideoPreview();
                return Preview;
            }
        }

        [Test]
        public void MF2_DefaultProjectUsesExistingDialoguePanelVisual()
        {
            VnWorkshopPreviewFrame baseline = VnPresentationWorkshopPreviewRenderer.BuildFrame(
                new VnPresentationWorkshopPreset(), VnWorkshopResolution.Reference1920x1080,
                VnWorkshopPreviewScene.BusStopKeiko);

            var project = new VnSceneComposerProject();
            var scene = new VnSceneComposerScene();
            scene.dialogueBeats[0].speaker = "Keiko";
            project.scenes.Add(scene);
            VnWorkshopPreviewFrame composed = VnSceneComposerComposition.BuildFrame(
                project, scene, VnWorkshopResolution.Reference1920x1080, Texture2D.blackTexture);

            Assert.That(composed.DialoguePanelTexture, Is.SameAs(baseline.DialoguePanelTexture),
                "M-F2 must not change projects that have no custom panel visual authored.");
        }

        [Test]
        public void MF2_CustomPanelReferenceOverridesDefaultVisual()
        {
            Texture2D custom = AssetDatabase.LoadAssetAtPath<Texture2D>(LightPanelPath);
            Assert.That(custom, Is.Not.Null);

            var preset = new VnPresentationWorkshopPreset();
            SetPanelOverride(preset, LightPanelGuid);

            VnWorkshopPreviewFrame frame = VnPresentationWorkshopPreviewRenderer.BuildFrame(
                preset, VnWorkshopResolution.Reference1920x1080, VnWorkshopPreviewScene.BusStopKeiko);

            Assert.That(frame.DialoguePanelTexture, Is.SameAs(custom),
                "The authored custom panel must resolve through the canonical DialoguePanelTexture path.");
        }

        [Test]
        public void MF2_CustomPanelAlphaIsPreservedAndGeometryDoesNotChange()
        {
            Texture2D custom = AssetDatabase.LoadAssetAtPath<Texture2D>(LightPanelPath);
            Assert.That(custom, Is.Not.Null);
            TextureImporter importer = AssetImporter.GetAtPath(LightPanelPath) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.alphaIsTransparency, Is.True);

            VnWorkshopPreviewFrame baseline = VnPresentationWorkshopPreviewRenderer.BuildFrame(
                new VnPresentationWorkshopPreset(), VnWorkshopResolution.Reference1920x1080,
                VnWorkshopPreviewScene.BusStopKeiko);
            var preset = new VnPresentationWorkshopPreset();
            SetPanelOverride(preset, LightPanelGuid);
            VnWorkshopPreviewFrame customFrame = VnPresentationWorkshopPreviewRenderer.BuildFrame(
                preset, VnWorkshopResolution.Reference1920x1080, VnWorkshopPreviewScene.BusStopKeiko);

            Assert.That(customFrame.DialoguePanelTexture, Is.SameAs(custom));
            Assert.That(customFrame.DialoguePanel, Is.EqualTo(baseline.DialoguePanel),
                "Changing the visual asset must not invent a second panel geometry representation.");
        }

        [Test]
        public void MF2_CustomPanelPersistsThroughPortableSerializationWithoutSchemaBump()
        {
            Assert.That(VnSceneComposerContract.SchemaVersion, Is.EqualTo(4),
                "The additive optional panel visual override should remain compatible with canonical schema 4.");

            var project = new VnSceneComposerProject();
            var scene = new VnSceneComposerScene();
            project.scenes.Add(scene);
            SetPanelOverride(scene.presentationOverrides, LightPanelGuid);

            string json = VnSceneComposerSerialization.SerializePortable(project);
            VnSceneComposerImportResult loaded = VnSceneComposerSerialization.DeserializePortable(json);

            Assert.That(loaded.Success, Is.True, loaded.Error);
            Assert.That(loaded.Project.schemaVersion, Is.EqualTo(4));
            Assert.That(GetPanelOverrideGuid(loaded.Project.scenes[0].presentationOverrides), Is.EqualTo(LightPanelGuid));
        }

        [Test]
        public void MF2_ResetCustomPanelReturnsToDefault()
        {
            var preset = new VnPresentationWorkshopPreset();
            VnWorkshopPreviewFrame baseline = VnPresentationWorkshopPreviewRenderer.BuildFrame(
                new VnPresentationWorkshopPreset(), VnWorkshopResolution.Reference1920x1080,
                VnWorkshopPreviewScene.BusStopKeiko);

            SetPanelOverride(preset, LightPanelGuid);
            Assert.That(VnPresentationWorkshopPreviewRenderer.BuildFrame(
                preset, VnWorkshopResolution.Reference1920x1080, VnWorkshopPreviewScene.BusStopKeiko)
                .DialoguePanelTexture, Is.Not.SameAs(baseline.DialoguePanelTexture));

            ClearPanelOverride(preset);
            Assert.That(VnPresentationWorkshopPreviewRenderer.BuildFrame(
                preset, VnWorkshopResolution.Reference1920x1080, VnWorkshopPreviewScene.BusStopKeiko)
                .DialoguePanelTexture, Is.SameAs(baseline.DialoguePanelTexture));
        }

        [Test]
        public void MF2_ScenePanelOverrideWinsOverProjectDefault()
        {
            var project = new VnSceneComposerProject();
            var scene = new VnSceneComposerScene();
            project.scenes.Add(scene);
            SetPanelOverride(project.defaultPresentation, DarkPanelGuid);
            SetPanelOverride(scene.presentationOverrides, LightPanelGuid);

            VnPresentationWorkshopPreset resolved = VnSceneComposerComposition.ResolvePresentation(project, scene);
            Assert.That(GetPanelOverrideGuid(resolved), Is.EqualTo(LightPanelGuid));

            Texture2D light = AssetDatabase.LoadAssetAtPath<Texture2D>(LightPanelPath);
            VnWorkshopPreviewFrame frame = VnPresentationWorkshopPreviewRenderer.BuildFrame(
                resolved, VnWorkshopResolution.Reference1920x1080, VnWorkshopPreviewScene.BusStopKeiko);
            Assert.That(frame.DialoguePanelTexture, Is.SameAs(light));
        }

        [Test]
        public void MF2_MissingCustomPanelFallsBackSafelyWithoutDestroyingReference()
        {
            var preset = new VnPresentationWorkshopPreset();
            const string missingGuid = "11111111111111111111111111111111";
            SetPanelOverride(preset, missingGuid);

            VnWorkshopPreviewFrame baseline = VnPresentationWorkshopPreviewRenderer.BuildFrame(
                new VnPresentationWorkshopPreset(), VnWorkshopResolution.Reference1920x1080,
                VnWorkshopPreviewScene.BusStopKeiko);
            VnWorkshopPreviewFrame frame = VnPresentationWorkshopPreviewRenderer.BuildFrame(
                preset, VnWorkshopResolution.Reference1920x1080, VnWorkshopPreviewScene.BusStopKeiko);

            Assert.That(frame.DialoguePanelTexture, Is.SameAs(baseline.DialoguePanelTexture),
                "A missing custom panel must render the established default instead of null/black.");
            Assert.That(GetPanelWarning(frame), Is.Not.Empty,
                "A missing custom panel must expose a clear editor-facing warning.");
            Assert.That(GetPanelOverrideGuid(preset), Is.EqualTo(missingGuid),
                "Fallback must not silently erase the authored reference.");
        }

        [Test]
        public void MF2_PanelMutationParticipatesInUndo()
        {
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                window.ComposerSetPresentationScope(false);
                Texture2D custom = AssetDatabase.LoadAssetAtPath<Texture2D>(LightPanelPath);
                Assert.That(custom, Is.Not.Null);

                Undo.ClearAll();
                RequireWindowMethod("ComposerSetDialoguePanelVisualAsset", typeof(Texture2D))
                    .Invoke(window, new object[] { custom });
                Undo.FlushUndoRecordObjects();

                VnSceneComposerProject project = GetProject(window);
                Assert.That(GetPanelOverrideGuid(project.scenes[0].presentationOverrides), Is.EqualTo(LightPanelGuid));

                Undo.PerformUndo();
                project = GetProject(window);
                Assert.That(GetPanelOverrideGuid(project.scenes[0].presentationOverrides), Is.Empty);
            }
            finally
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MF2_CustomPanelMutationDoesNotResetVideoResources()
        {
            var factory = new Mf2VideoFactory();
            VnSceneComposerMediaEditing.VideoPreviewFactory = factory;
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                VnSceneComposerProject project = GetProject(window);
                VnSceneComposerScene scene = project.scenes[0];
                scene.media = new VnSceneComposerMediaReference
                {
                    kind = VnSceneComposerMediaKind.ExternalVideo,
                    reference = "mf2-video.mp4",
                    displayName = "mf2-video.mp4",
                    contentHash = "mf2-video",
                    scaleMode = VnSceneComposerMediaScaleMode.Fit,
                    loop = false
                };

                Assert.That((bool)RequireWindowMethod("ComposerPrepareSelectedVideoForAuthoring")
                    .Invoke(window, null), Is.True);
                Assert.That(factory.Preview, Is.Not.Null);
                int prepareBefore = factory.Preview.PrepareCalls;
                int restartBefore = factory.Preview.RestartCalls;
                int disposeBefore = factory.Preview.DisposeCalls;

                Texture2D custom = AssetDatabase.LoadAssetAtPath<Texture2D>(LightPanelPath);
                RequireWindowMethod("ComposerSetDialoguePanelVisualAsset", typeof(Texture2D))
                    .Invoke(window, new object[] { custom });

                Assert.That(factory.Preview.PrepareCalls, Is.EqualTo(prepareBefore));
                Assert.That(factory.Preview.RestartCalls, Is.EqualTo(restartBefore));
                Assert.That(factory.Preview.DisposeCalls, Is.EqualTo(disposeBefore));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                VnSceneComposerMediaEditing.ResetVideoPreviewFactory();
                if (factory.Preview != null) factory.Preview.Dispose();
            }
        }

        [Test]
        public void MF2_CustomPanelAppearsThroughSceneComposerComposition()
        {
            var project = new VnSceneComposerProject();
            var scene = new VnSceneComposerScene();
            scene.dialogueBeats[0].speaker = "Keiko";
            project.scenes.Add(scene);
            SetPanelOverride(scene.presentationOverrides, LightPanelGuid);

            Texture2D expected = AssetDatabase.LoadAssetAtPath<Texture2D>(LightPanelPath);
            VnWorkshopPreviewFrame frame = VnSceneComposerComposition.BuildFrame(
                project, scene, VnWorkshopResolution.Reference1920x1080, Texture2D.blackTexture);

            Assert.That(frame.DialoguePanelTexture, Is.SameAs(expected),
                "Authoring/playback composition must resolve the same canonical custom panel texture.");
        }

        [Test]
        public void MF2_DuplicateScenePreservesScenePanelOverride()
        {
            var project = new VnSceneComposerProject();
            var source = new VnSceneComposerScene();
            project.scenes.Add(source);
            SetPanelOverride(source.presentationOverrides, LightPanelGuid);

            VnSceneComposerScene copy = VnSceneComposerEditing.DuplicateScene(project, source.sceneId);

            Assert.That(copy, Is.Not.Null);
            Assert.That(copy.sceneId, Is.Not.EqualTo(source.sceneId));
            Assert.That(GetPanelOverrideGuid(copy.presentationOverrides), Is.EqualTo(LightPanelGuid));
        }

        [Test]
        public void MF2_BasicUiExposesFriendlyDialoguePanelControls()
        {
            string sourcePath = Path.Combine(
                Application.dataPath, "Rokas", "Scripts", "Editor", "VnUiWorkshop",
                "VnPresentationWorkshopWindow.SceneComposer.cs");
            string source = File.ReadAllText(sourcePath);
            string signature = "private void DrawSceneComposerDialoguePanelVisualControls(VnSceneComposerScene scene)";
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            int end = source.IndexOf("private void SetSceneComposerTypewriterSpeed", start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start));
            string ui = source.Substring(start, end - start);

            Assert.That(ui, Does.Contain("\"Плашка диалога\"")
                .And.Contain("\"По умолчанию\"")
                .And.Contain("\"Своя PNG\"")
                .And.Contain("\"Только к этой сцене\"")
                .And.Contain("\"Ко всем сценам\"")
                .And.Contain("\"Выбрать PNG\"")
                .And.Contain("\"Сбросить\""));
            Assert.That(ui, Does.Not.Contain("assetGuid")
                .And.Not.Contain("contentHash")
                .And.Not.Contain("stableAssetId")
                .And.Not.Contain("Project Defaults")
                .And.Not.Contain("Scene Overrides"));
        }

        private static void SetPanelOverride(VnPresentationWorkshopPreset preset, string guid)
        {
            Assert.That(preset, Is.Not.Null);
            FieldInfo field = typeof(VnPresentationWorkshopPreset).GetField(
                "dialoguePanelVisual", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null,
                "M-F2 requires a canonical dialoguePanelVisual override inside VnPresentationWorkshopPreset.");
            object visual = field.GetValue(preset);
            Assert.That(visual, Is.Not.Null);
            SetField(visual, "hasAssetGuid", true);
            SetField(visual, "assetGuid", guid ?? string.Empty);
        }

        private static void ClearPanelOverride(VnPresentationWorkshopPreset preset)
        {
            FieldInfo field = typeof(VnPresentationWorkshopPreset).GetField(
                "dialoguePanelVisual", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            object visual = field.GetValue(preset);
            MethodInfo clear = visual.GetType().GetMethod("Clear", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(clear, Is.Not.Null);
            clear.Invoke(visual, null);
        }

        private static string GetPanelOverrideGuid(VnPresentationWorkshopPreset preset)
        {
            FieldInfo field = typeof(VnPresentationWorkshopPreset).GetField(
                "dialoguePanelVisual", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            object visual = field.GetValue(preset);
            if (visual == null) return string.Empty;
            bool has = (bool)GetField(visual, "hasAssetGuid");
            return has ? ((string)GetField(visual, "assetGuid") ?? string.Empty) : string.Empty;
        }

        private static string GetPanelWarning(VnWorkshopPreviewFrame frame)
        {
            PropertyInfo property = typeof(VnWorkshopPreviewFrame).GetProperty(
                "DialoguePanelWarning", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null,
                "The renderer frame must expose missing-panel fallback diagnostics.");
            return (string)property.GetValue(frame, null) ?? string.Empty;
        }

        private static MethodInfo RequireWindowMethod(string name, params Type[] parameters)
        {
            MethodInfo method = typeof(VnPresentationWorkshopWindow).GetMethod(
                name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, parameters ?? Type.EmptyTypes, null);
            Assert.That(method, Is.Not.Null, "Missing M-F2 authoring method: " + name);
            return method;
        }

        private static VnSceneComposerProject GetProject(VnPresentationWorkshopWindow window)
        {
            FieldInfo field = typeof(VnPresentationWorkshopWindow).GetField(
                "_sceneComposerProject", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            return (VnSceneComposerProject)field.GetValue(window);
        }

        private static object GetField(object instance, string name)
        {
            Assert.That(instance, Is.Not.Null);
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing field: " + instance.GetType().Name + "." + name);
            return field.GetValue(instance);
        }

        private static void SetField(object instance, string name, object value)
        {
            Assert.That(instance, Is.Not.Null);
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing field: " + instance.GetType().Name + "." + name);
            field.SetValue(instance, value);
        }
    }
}
