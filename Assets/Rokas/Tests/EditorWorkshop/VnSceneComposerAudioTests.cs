using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerAudioTests
    {
        private const string TrackAPath = "Assets/Rokas/Audio/05. Restaurant Prep.mp3";
        private const string TrackAGuid = "7540bd61684b4a768cad4691dbc54972";
        private const string TrackBPath = "Assets/Rokas/Audio/HomeNocturne.wav";
        private const string TrackBGuid = "e56430b08183503bb580307d23f0d2f5";
        private const string MissingGuid = "22222222222222222222222222222222";

        [Test]
        public void MAudio_ModelIsAdditiveSceneLocalAndKeepsSchemaThree()
        {
            Assert.That(VnSceneComposerContract.SchemaVersion, Is.EqualTo(3));
            Type mode = RequireType("VnSceneComposerMusicMode");
            Type music = RequireType("VnSceneComposerMusic");
            Assert.That(mode.IsEnum, Is.True);
            Assert.That(Enum.GetNames(mode), Does.Contain("Silence").And.Contain("Track").And.Contain("KeepPrevious"));
            foreach (string field in new[] { "mode", "assetGuid", "displayName", "volume", "loop", "fadeInSeconds", "fadeOutSeconds" })
                AssertField(music, field);

            FieldInfo sceneField = typeof(VnSceneComposerScene).GetField("music", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(sceneField, Is.Not.Null, "Music must belong directly to each Scene.");
            object value = sceneField.GetValue(new VnSceneComposerScene());
            Assert.That(value, Is.Not.Null);
            Assert.That(GetField(value, "mode").ToString(), Is.EqualTo("Silence"));
        }

        [Test]
        public void MAudio_OldSceneWithoutMusicNormalizesToExplicitSilence()
        {
            var project = new VnSceneComposerProject();
            var scene = new VnSceneComposerScene();
            project.scenes.Add(scene);
            FieldInfo musicField = RequireSceneMusicField();
            musicField.SetValue(scene, null);

            string json = JsonUtility.ToJson(project);
            VnSceneComposerImportResult loaded = VnSceneComposerSerialization.DeserializePortable(json);

            Assert.That(loaded.Success, Is.True, loaded.Error);
            object music = musicField.GetValue(loaded.Project.scenes[0]);
            Assert.That(music, Is.Not.Null);
            Assert.That(GetField(music, "mode").ToString(), Is.EqualTo("Silence"));
        }

        [Test]
        public void MAudio_TrackVolumeLoopAndFadesRoundTrip()
        {
            var project = new VnSceneComposerProject();
            var scene = new VnSceneComposerScene();
            project.scenes.Add(scene);
            SetTrack(scene, TrackAGuid, "Restaurant Prep", .42f, false, .35f, .8f);

            string json = VnSceneComposerSerialization.SerializePortable(project);
            VnSceneComposerImportResult loaded = VnSceneComposerSerialization.DeserializePortable(json);

            Assert.That(loaded.Success, Is.True, loaded.Error);
            object music = GetMusic(loaded.Project.scenes[0]);
            Assert.That(GetField(music, "mode").ToString(), Is.EqualTo("Track"));
            Assert.That((string)GetField(music, "assetGuid"), Is.EqualTo(TrackAGuid));
            Assert.That((string)GetField(music, "displayName"), Is.EqualTo("Restaurant Prep"));
            Assert.That((float)GetField(music, "volume"), Is.EqualTo(.42f).Within(.0001f));
            Assert.That((bool)GetField(music, "loop"), Is.False);
            Assert.That((float)GetField(music, "fadeInSeconds"), Is.EqualTo(.35f).Within(.0001f));
            Assert.That((float)GetField(music, "fadeOutSeconds"), Is.EqualTo(.8f).Within(.0001f));
        }

        [Test]
        public void MAudio_DuplicateScenePreservesMusicConfigurationIndependently()
        {
            var project = new VnSceneComposerProject();
            var scene = new VnSceneComposerScene();
            project.scenes.Add(scene);
            SetTrack(scene, TrackAGuid, "Restaurant Prep", .55f, true, .2f, .4f);

            VnSceneComposerScene copy = VnSceneComposerEditing.DuplicateScene(project, scene.sceneId);
            object sourceMusic = GetMusic(scene);
            object copyMusic = GetMusic(copy);

            Assert.That(copyMusic, Is.Not.SameAs(sourceMusic));
            Assert.That((string)GetField(copyMusic, "assetGuid"), Is.EqualTo(TrackAGuid));
            Assert.That((float)GetField(copyMusic, "volume"), Is.EqualTo(.55f).Within(.0001f));
            SetField(copyMusic, "volume", .2f);
            Assert.That((float)GetField(sourceMusic, "volume"), Is.EqualTo(.55f).Within(.0001f));
        }

        [Test]
        public void MAudio_ResolverDistinguishesSilenceTrackKeepPreviousAndMissingAsset()
        {
            AudioClip trackA = AssetDatabase.LoadAssetAtPath<AudioClip>(TrackAPath);
            Assert.That(trackA, Is.Not.Null);
            var project = new VnSceneComposerProject();
            var first = new VnSceneComposerScene();
            var second = new VnSceneComposerScene();
            var third = new VnSceneComposerScene();
            project.scenes.Add(first);
            project.scenes.Add(second);
            project.scenes.Add(third);
            SetTrack(first, TrackAGuid, "Restaurant Prep", .5f, true, .1f, .2f);
            SetMode(second, "KeepPrevious");
            SetTrack(third, MissingGuid, "Missing Song", .6f, true, .1f, .2f);

            Type resolver = RequireType("VnSceneComposerMusicResolver");
            MethodInfo resolve = resolver.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(VnSceneComposerProject), typeof(int) }, null);
            Assert.That(resolve, Is.Not.Null);

            object firstResolved = resolve.Invoke(null, new object[] { project, 0 });
            object secondResolved = resolve.Invoke(null, new object[] { project, 1 });
            object thirdResolved = resolve.Invoke(null, new object[] { project, 2 });
            Assert.That(GetProperty(firstResolved, "Clip"), Is.SameAs(trackA));
            Assert.That((string)GetProperty(firstResolved, "AssetGuid"), Is.EqualTo(TrackAGuid));
            Assert.That((string)GetProperty(secondResolved, "AssetGuid"), Is.EqualTo(TrackAGuid));
            Assert.That((bool)GetProperty(secondResolved, "Inherited"), Is.True);
            Assert.That((bool)GetProperty(thirdResolved, "MissingAsset"), Is.True);
            Assert.That(GetProperty(thirdResolved, "Clip"), Is.Null);
            Assert.That((string)GetField(GetMusic(third), "assetGuid"), Is.EqualTo(MissingGuid));
        }

        [Test]
        public void MAudio_AssetLibraryUsesMusicPurposeAndVerifiedMp3WavFormats()
        {
            AudioClip mp3 = AssetDatabase.LoadAssetAtPath<AudioClip>(TrackAPath);
            AudioClip wav = AssetDatabase.LoadAssetAtPath<AudioClip>(TrackBPath);
            Assert.That(mp3, Is.Not.Null);
            Assert.That(wav, Is.Not.Null);
            Assert.DoesNotThrow(() => Enum.Parse(typeof(VnSceneComposerAssetPurpose), "Music"));

            string path = Path.Combine(Application.dataPath, "Rokas", "Scripts", "Editor", "VnUiWorkshop",
                "VnSceneComposerAssetLibrary.cs");
            string source = File.ReadAllText(path);
            Assert.That(source, Does.Contain("\".mp3\"").And.Contain("\".wav\"").And.Contain("AudioClip"));
        }

        [Test]
        public void MAudio_WindowEditsDoNotRecreateScenePlayback()
        {
            AudioClip trackA = AssetDatabase.LoadAssetAtPath<AudioClip>(TrackAPath);
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                window.ComposerPlayScene();
                object before = GetPrivateField(window, "_sceneComposerPlayback");
                Assert.That(before, Is.Not.Null);

                InvokeWindow(window, "ComposerSetSelectedSceneMusicTrack", new[] { typeof(AudioClip) }, trackA);
                InvokeWindow(window, "ComposerSetSelectedSceneMusicVolume", new[] { typeof(float) }, .35f);
                InvokeWindow(window, "ComposerSetSelectedSceneMusicLoop", new[] { typeof(bool) }, false);
                InvokeWindow(window, "ComposerSetSelectedSceneMusicFades", new[] { typeof(float), typeof(float) }, .2f, .5f);

                object after = GetPrivateField(window, "_sceneComposerPlayback");
                Assert.That(after, Is.SameAs(before), "Music authoring must refresh only music state, not recreate scene/video playback.");
                object music = GetMusic(GetProject(window).scenes[0]);
                Assert.That((string)GetField(music, "assetGuid"), Is.EqualTo(TrackAGuid));
                Assert.That((float)GetField(music, "volume"), Is.EqualTo(.35f).Within(.0001f));
                Assert.That((bool)GetField(music, "loop"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MAudio_UndoRestoresTrackVolumeLoopFadesAndClear()
        {
            AudioClip trackA = AssetDatabase.LoadAssetAtPath<AudioClip>(TrackAPath);
            VnPresentationWorkshopWindow window = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
            try
            {
                window.ComposerAddScene();
                Undo.ClearAll();

                InvokeWindow(window, "ComposerSetSelectedSceneMusicTrack", new[] { typeof(AudioClip) }, trackA);
                Undo.FlushUndoRecordObjects();
                Assert.That(GetField(GetMusic(GetProject(window).scenes[0]), "mode").ToString(), Is.EqualTo("Track"));
                Undo.PerformUndo();
                Assert.That(GetField(GetMusic(GetProject(window).scenes[0]), "mode").ToString(), Is.EqualTo("Silence"));

                InvokeWindow(window, "ComposerSetSelectedSceneMusicTrack", new[] { typeof(AudioClip) }, trackA);
                Undo.FlushUndoRecordObjects();
                Undo.ClearAll();

                InvokeWindow(window, "ComposerSetSelectedSceneMusicVolume", new[] { typeof(float) }, .2f);
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.That((float)GetField(GetMusic(GetProject(window).scenes[0]), "volume"), Is.EqualTo(1f).Within(.0001f));

                Undo.ClearAll();
                InvokeWindow(window, "ComposerSetSelectedSceneMusicLoop", new[] { typeof(bool) }, false);
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Assert.That((bool)GetField(GetMusic(GetProject(window).scenes[0]), "loop"), Is.True);

                Undo.ClearAll();
                InvokeWindow(window, "ComposerSetSelectedSceneMusicFades", new[] { typeof(float), typeof(float) }, .4f, .8f);
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                object music = GetMusic(GetProject(window).scenes[0]);
                Assert.That((float)GetField(music, "fadeInSeconds"), Is.EqualTo(0f).Within(.0001f));
                Assert.That((float)GetField(music, "fadeOutSeconds"), Is.EqualTo(0f).Within(.0001f));

                Undo.ClearAll();
                InvokeWindow(window, "ComposerClearSelectedSceneMusic", Type.EmptyTypes);
                Undo.FlushUndoRecordObjects();
                Assert.That(GetField(GetMusic(GetProject(window).scenes[0]), "mode").ToString(), Is.EqualTo("Silence"));
                Undo.PerformUndo();
                Assert.That(GetField(GetMusic(GetProject(window).scenes[0]), "mode").ToString(), Is.EqualTo("Track"));
            }
            finally
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void MAudio_DialogueBeatAdvanceDoesNotRestartUnchangedMusic()
        {
            var project = new VnSceneComposerProject();
            var scene = new VnSceneComposerScene();
            scene.dialogueBeats.Add(new VnSceneComposerDialogueBeat());
            scene.dialogueBeats.Add(new VnSceneComposerDialogueBeat());
            project.scenes.Add(scene);
            SetTrack(scene, TrackAGuid, "Restaurant Prep", .6f, true, 0f, 0f);

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                int starts = ReadIntProperty(playback, "MusicStartCount");
                string guid = ReadStringProperty(playback, "CurrentMusicAssetGuid");
                playback.AdvanceDialogue();
                playback.AdvanceDialogue();
                Assert.That(ReadIntProperty(playback, "MusicStartCount"), Is.EqualTo(starts));
                Assert.That(ReadStringProperty(playback, "CurrentMusicAssetGuid"), Is.EqualTo(guid));
            }
        }

        [Test]
        public void MAudio_SameTrackAcrossScenesContinuesWithoutRestart()
        {
            var project = new VnSceneComposerProject();
            var a = new VnSceneComposerScene();
            var b = new VnSceneComposerScene();
            project.scenes.Add(a);
            project.scenes.Add(b);
            SetTrack(a, TrackAGuid, "Restaurant Prep", .6f, true, 0f, 0f);
            SetTrack(b, TrackAGuid, "Restaurant Prep", .6f, true, 0f, 0f);

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayAll();
                int starts = ReadIntProperty(playback, "MusicStartCount");
                playback.AdvanceDialogue();
                Assert.That(playback.CurrentSceneIndex, Is.EqualTo(1));
                Assert.That(ReadIntProperty(playback, "MusicStartCount"), Is.EqualTo(starts));
                Assert.That(ReadStringProperty(playback, "CurrentMusicAssetGuid"), Is.EqualTo(TrackAGuid));
                Assert.That(ReadIntProperty(playback, "ActiveMusicSourceCount"), Is.LessThanOrEqualTo(1));
            }
        }

        [Test]
        public void MAudio_KeepPreviousContinuesEffectiveTrack()
        {
            var project = new VnSceneComposerProject();
            var a = new VnSceneComposerScene();
            var b = new VnSceneComposerScene();
            project.scenes.Add(a);
            project.scenes.Add(b);
            SetTrack(a, TrackAGuid, "Restaurant Prep", .6f, true, 0f, 0f);
            SetMode(b, "KeepPrevious");

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayAll();
                int starts = ReadIntProperty(playback, "MusicStartCount");
                playback.AdvanceDialogue();
                Assert.That(ReadStringProperty(playback, "CurrentMusicAssetGuid"), Is.EqualTo(TrackAGuid));
                Assert.That(ReadIntProperty(playback, "MusicStartCount"), Is.EqualTo(starts));
            }
        }

        [Test]
        public void MAudio_TrackChangeSwitchesOnceAndNeverDuplicatesMusicSource()
        {
            var project = new VnSceneComposerProject();
            var a = new VnSceneComposerScene();
            var b = new VnSceneComposerScene();
            project.scenes.Add(a);
            project.scenes.Add(b);
            SetTrack(a, TrackAGuid, "Restaurant Prep", .6f, true, 0f, 0f);
            SetTrack(b, TrackBGuid, "Home Nocturne", .4f, true, 0f, 0f);

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayAll();
                int starts = ReadIntProperty(playback, "MusicStartCount");
                playback.AdvanceDialogue();
                Assert.That(ReadStringProperty(playback, "CurrentMusicAssetGuid"), Is.EqualTo(TrackBGuid));
                Assert.That(ReadIntProperty(playback, "MusicStartCount"), Is.EqualTo(starts + 1));
                Assert.That(ReadIntProperty(playback, "ActiveMusicSourceCount"), Is.EqualTo(1));
            }
        }

        [Test]
        public void MAudio_AuthoringResolverAndPlaySceneUseSameEffectiveMusic()
        {
            var project = new VnSceneComposerProject();
            var scene = new VnSceneComposerScene();
            project.scenes.Add(scene);
            SetTrack(scene, TrackAGuid, "Restaurant Prep", .33f, false, .1f, .2f);

            Type resolver = RequireType("VnSceneComposerMusicResolver");
            MethodInfo resolve = resolver.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(VnSceneComposerProject), typeof(int) }, null);
            object authored = resolve.Invoke(null, new object[] { project, 0 });

            using (var playback = new VnSceneComposerPlaybackController(project))
            {
                playback.PlayScene(0);
                Assert.That(ReadStringProperty(playback, "CurrentMusicAssetGuid"), Is.EqualTo((string)GetProperty(authored, "AssetGuid")));
                Assert.That(ReadFloatProperty(playback, "CurrentMusicVolume"), Is.EqualTo((float)GetProperty(authored, "Volume")).Within(.0001f));
            }
        }

        [Test]
        public void MAudio_BasicUiIsCompactRussianAndExposesPreviewStopAndClear()
        {
            string path = Path.Combine(Application.dataPath, "Rokas", "Scripts", "Editor", "VnUiWorkshop",
                "VnPresentationWorkshopWindow.SceneComposer.cs");
            string source = File.ReadAllText(path);
            Assert.That(source, Does.Contain("\"Музыка\"")
                .And.Contain("\"Предпрослушать\"")
                .And.Contain("\"Стоп\"")
                .And.Contain("\"Громкость\"")
                .And.Contain("\"Зациклить\"")
                .And.Contain("\"Fade In\"")
                .And.Contain("\"Fade Out\"")
                .And.Contain("\"Без музыки\"")
                .And.Contain("\"Оставить предыдущую\""));
            Assert.That(source, Does.Not.Contain("AudioMixer"));
        }

        private static object GetMusic(VnSceneComposerScene scene)
        {
            FieldInfo field = RequireSceneMusicField();
            object value = field.GetValue(scene);
            Assert.That(value, Is.Not.Null, "Scene music must be initialized.");
            return value;
        }

        private static FieldInfo RequireSceneMusicField()
        {
            FieldInfo field = typeof(VnSceneComposerScene).GetField("music", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Scene must own additive music data.");
            return field;
        }

        private static void SetTrack(VnSceneComposerScene scene, string guid, string name,
            float volume, bool loop, float fadeIn, float fadeOut)
        {
            object music = GetMusic(scene);
            SetEnumField(music, "mode", "Track");
            SetField(music, "assetGuid", guid);
            SetField(music, "displayName", name);
            SetField(music, "volume", volume);
            SetField(music, "loop", loop);
            SetField(music, "fadeInSeconds", fadeIn);
            SetField(music, "fadeOutSeconds", fadeOut);
        }

        private static void SetMode(VnSceneComposerScene scene, string mode)
        {
            object music = GetMusic(scene);
            SetEnumField(music, "mode", mode);
        }

        private static Type RequireType(string simpleName)
        {
            Type type = typeof(VnSceneComposerScene).Assembly.GetType("Rokas.EditorTools.VnUiWorkshop." + simpleName, false);
            Assert.That(type, Is.Not.Null, "Missing M-AUDIO type: " + simpleName);
            return type;
        }

        private static void AssertField(Type type, string name)
        {
            Assert.That(type.GetField(name, BindingFlags.Public | BindingFlags.Instance), Is.Not.Null,
                "Missing M-AUDIO field: " + type.Name + "." + name);
        }

        private static object GetField(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing field: " + instance.GetType().Name + "." + name);
            return field.GetValue(instance);
        }

        private static object GetProperty(object instance, string name)
        {
            PropertyInfo property = instance.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, "Missing property: " + instance.GetType().Name + "." + name);
            return property.GetValue(instance, null);
        }

        private static void SetField(object instance, string name, object value)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing field: " + instance.GetType().Name + "." + name);
            field.SetValue(instance, value);
        }

        private static void SetEnumField(object instance, string name, string value)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing enum field: " + name);
            field.SetValue(instance, Enum.Parse(field.FieldType, value));
        }

        private static object GetPrivateField(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing private field: " + name);
            return field.GetValue(instance);
        }

        private static VnSceneComposerProject GetProject(VnPresentationWorkshopWindow window)
        {
            return (VnSceneComposerProject)GetPrivateField(window, "_sceneComposerProject");
        }

        private static object InvokeWindow(VnPresentationWorkshopWindow window, string name, Type[] parameters, params object[] args)
        {
            MethodInfo method = typeof(VnPresentationWorkshopWindow).GetMethod(
                name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, parameters, null);
            Assert.That(method, Is.Not.Null, "Missing M-AUDIO authoring method: " + name);
            return method.Invoke(window, args);
        }

        private static int ReadIntProperty(object instance, string name)
        {
            return (int)GetProperty(instance, name);
        }

        private static float ReadFloatProperty(object instance, string name)
        {
            return (float)GetProperty(instance, name);
        }

        private static string ReadStringProperty(object instance, string name)
        {
            return (string)GetProperty(instance, name);
        }
    }
}
