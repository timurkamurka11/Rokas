using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed partial class VnSceneComposerPlaybackTests
    {
        private const string EditorAssembly = "Rokas.Editor";
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";
        private const string TwoFrameGifBase64 =
            "R0lGODlhAgABAIEAAP8AAAAAAAAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQICgAAACwAAAAAAgABAAAIBQABAAgIACH5BAgUAAAALAAAAAACAAEAgQD/AAAAAAAAAAAAAAgFAAEACAgAOw==";

        [Test]
        public void TransportControlsAreDeterministicAndNeverMutateSceneOrderOrIds()
        {
            Type projectType = RequireType("VnSceneComposerProject");
            Type controllerType = RequirePlaybackType();
            object project = Activator.CreateInstance(projectType);
            IList scenes = (IList)Get(project, "scenes");
            scenes.Add(Scene("A", "first", "PreviewAutoDuration", .5f));
            scenes.Add(Scene("B", "second", "ManualBeat", 2f));
            scenes.Add(Scene("C", "third", "PreviewAutoDuration", .5f));
            string[] ids = scenes.Cast<object>().Select(s => (string)Get(s, "sceneId")).ToArray();

            object controller = NewController(controllerType, projectType, project);
            try
            {
                Invoke(controllerType, controller, "PlayFromHere", typeof(int), 1);
                Assert.That((int)Get(controller, "CurrentSceneIndex"), Is.EqualTo(1));
                Assert.That((bool)Get(controller, "IsPlaying"), Is.True);

                Invoke(controllerType, controller, "Pause");
                Assert.That((bool)Get(controller, "IsPlaying"), Is.False);
                Invoke(controllerType, controller, "Restart");
                Assert.That((int)Get(controller, "CurrentSceneIndex"), Is.EqualTo(1));
                Assert.That((float)Get(controller, "SceneElapsedSeconds"), Is.EqualTo(0f).Within(.0001f));
                Assert.That((float)Get(controller, "MediaTimeSeconds"), Is.EqualTo(0f).Within(.0001f));

                Invoke(controllerType, controller, "Next");
                Assert.That((int)Get(controller, "CurrentSceneIndex"), Is.EqualTo(2));
                Invoke(controllerType, controller, "Previous");
                Assert.That((int)Get(controller, "CurrentSceneIndex"), Is.EqualTo(1));
                Assert.That((float)Get(controller, "MediaTimeSeconds"), Is.EqualTo(0f).Within(.0001f));

                Invoke(controllerType, controller, "PlayAll");
                Assert.That((int)Get(controller, "CurrentSceneIndex"), Is.EqualTo(0));
                Invoke(controllerType, controller, "PlayScene", typeof(int), 2);
                Assert.That((int)Get(controller, "CurrentSceneIndex"), Is.EqualTo(2));

                CollectionAssert.AreEqual(ids, scenes.Cast<object>().Select(s => (string)Get(s, "sceneId")).ToArray());
            }
            finally { Dispose(controller); }
        }

        [Test]
        public void PreviewAutoDurationCanAdvanceStoryboardWhileManualBeatNeverAutoAdvances()
        {
            Type projectType = RequireType("VnSceneComposerProject");
            Type controllerType = RequirePlaybackType();
            object project = Activator.CreateInstance(projectType);
            SetAutoPreviewSequenceGap(project, 0f);
            IList scenes = (IList)Get(project, "scenes");
            scenes.Add(Scene("Auto", "auto", "PreviewAutoDuration", .25f));
            scenes.Add(Scene("Manual", "manual", "ManualBeat", 2f));
            scenes.Add(Scene("After", "after", "PreviewAutoDuration", .25f));

            object controller = NewController(controllerType, projectType, project);
            try
            {
                Invoke(controllerType, controller, "PlayAll");
                Invoke(controllerType, controller, "Advance", typeof(float), .3f);
                Assert.That((int)Get(controller, "CurrentSceneIndex"), Is.EqualTo(1),
                    "Authoring-only auto duration should advance the first scene.");

                Invoke(controllerType, controller, "Advance", typeof(float), 10f);
                Assert.That((int)Get(controller, "CurrentSceneIndex"), Is.EqualTo(1),
                    "ManualBeat must never change production-style one-click = one-beat semantics.");
                Invoke(controllerType, controller, "Next");
                Assert.That((int)Get(controller, "CurrentSceneIndex"), Is.EqualTo(2));
            }
            finally { Dispose(controller); }
        }

        [Test]
        public void RendererFacingFrameShowsTypewriterAndCharacterEnterAsObservableChanges()
        {
            Type projectType = RequireType("VnSceneComposerProject");
            Type controllerType = RequirePlaybackType();
            object project = Activator.CreateInstance(projectType);
            IList scenes = (IList)Get(project, "scenes");
            scenes.Add(Scene("Empty", string.Empty, "PreviewAutoDuration", 1f));
            object target = Scene("Enter", "Visible typewriter change", "PreviewAutoDuration", 1f);
            AddCharacter(target, "Mina", "mina_neutral", "Center");
            ConfigureCharacterTransition(target, "SlideAndFade", 1f, 1f, 180f);
            scenes.Add(target);

            object controller = NewController(controllerType, projectType, project);
            try
            {
                Invoke(controllerType, controller, "PlayScene", typeof(int), 1);
                object startFrame = Get(controller, "CurrentFrame");
                string startText = (string)Get(startFrame, "Dialogue");
                object startCharacter = ((IList)Get(startFrame, "ComposerCharacters"))[0];
                float startAlpha = (float)Get(startCharacter, "Alpha");
                Rect startBody = (Rect)Get(startCharacter, "Body");

                Invoke(controllerType, controller, "Advance", typeof(float), .5f);
                object middleFrame = Get(controller, "CurrentFrame");
                string middleText = (string)Get(middleFrame, "Dialogue");
                object middleCharacter = ((IList)Get(middleFrame, "ComposerCharacters"))[0];
                float middleAlpha = (float)Get(middleCharacter, "Alpha");
                Rect middleBody = (Rect)Get(middleCharacter, "Body");

                Invoke(controllerType, controller, "Advance", typeof(float), .5f);
                object endFrame = Get(controller, "CurrentFrame");
                string endText = (string)Get(endFrame, "Dialogue");
                object endCharacter = ((IList)Get(endFrame, "ComposerCharacters"))[0];
                float endAlpha = (float)Get(endCharacter, "Alpha");

                Assert.That(startText.Length, Is.LessThan(middleText.Length));
                Assert.That(middleText.Length, Is.LessThan(endText.Length));
                Assert.That(endText, Is.EqualTo("Visible typewriter change"));
                Assert.That(startAlpha, Is.LessThan(middleAlpha));
                Assert.That(middleAlpha, Is.LessThan(endAlpha));
                Assert.That(startBody.position, Is.Not.EqualTo(middleBody.position),
                    "The frame consumed by the existing renderer must visibly move the entering character.");
                Assert.That(endAlpha, Is.EqualTo(1f).Within(.001f));
            }
            finally { Dispose(controller); }
        }

        [Test]
        public void RendererFacingBounceAndCurtainChangeAtMidpointAndReturnToExactEndpoint()
        {
            Type projectType = RequireType("VnSceneComposerProject");
            Type controllerType = RequirePlaybackType();
            object project = Activator.CreateInstance(projectType);
            IList scenes = (IList)Get(project, "scenes");
            object from = Scene("From", "from", "PreviewAutoDuration", 1f);
            AddCharacter(from, "Mina", "mina_neutral", "Center");
            scenes.Add(from);
            object target = Scene("Target", "target", "PreviewAutoDuration", 1f);
            AddCharacter(target, "Mina", "mina_neutral", "Center");
            Set(Get(target, "transition"), "triggerActionBounce", true);
            ConfigureActionBounce(target, 1f);
            ConfigureBackgroundTransition(target, "Curtain", 1f);
            scenes.Add(target);

            object controller = NewController(controllerType, projectType, project);
            try
            {
                Invoke(controllerType, controller, "PlayScene", typeof(int), 1);
                object start = Get(controller, "CurrentFrame");
                Rect startBody = (Rect)Get(((IList)Get(start, "ComposerCharacters"))[0], "Body");
                object startBg = Get(start, "ComposerBackgroundTransition");

                Invoke(controllerType, controller, "Advance", typeof(float), .5f);
                object middle = Get(controller, "CurrentFrame");
                Rect middleBody = (Rect)Get(((IList)Get(middle, "ComposerCharacters"))[0], "Body");
                object middleBg = Get(middle, "ComposerBackgroundTransition");

                Invoke(controllerType, controller, "Advance", typeof(float), .5f);
                object end = Get(controller, "CurrentFrame");
                Rect endBody = (Rect)Get(((IList)Get(end, "ComposerCharacters"))[0], "Body");
                object endBg = Get(end, "ComposerBackgroundTransition");

                Assert.That(middleBody, Is.Not.EqualTo(startBody), "Bounce must alter the rectangle the renderer draws.");
                Assert.That(endBody, Is.EqualTo(startBody), "Bounce must return to the exact baseline endpoint.");
                Assert.That((float)Get(startBg, "CurtainCoverage"), Is.Not.EqualTo((float)Get(middleBg, "CurtainCoverage")));
                Assert.That((float)Get(middleBg, "CurtainCoverage"), Is.Not.EqualTo((float)Get(endBg, "CurtainCoverage")));
                Assert.That((bool)Get(endBg, "Complete"), Is.True);
            }
            finally { Dispose(controller); }
        }

        [Test]
        public void GifPlaybackUsesRealDecodedFramesAndRestartResetsToFirstFrame()
        {
            Type projectType = RequireType("VnSceneComposerProject");
            Type controllerType = RequirePlaybackType();
            Type editingType = RequireType("VnSceneComposerMediaEditing");
            Type sceneType = RequireType("VnSceneComposerScene");
            Type scaleModeType = RequireType("VnSceneComposerMediaScaleMode");
            object project = Activator.CreateInstance(projectType);
            object scene = Scene("Gif", string.Empty, "ManualBeat", 2f);
            ((IList)Get(project, "scenes")).Add(scene);
            string path = Path.Combine(Path.GetTempPath(), "rokas-vn-composer-sc-g-" + Guid.NewGuid().ToString("N") + ".gif");
            File.WriteAllBytes(path, Convert.FromBase64String(TwoFrameGifBase64));

            try
            {
                MethodInfo setGif = RequireStatic(editingType, "SetExternalGif", sceneType, typeof(string), scaleModeType, typeof(bool));
                setGif.Invoke(null, new[] { scene, path, Enum.Parse(scaleModeType, "Fill"), (object)true });
                object controller = NewController(controllerType, projectType, project);
                try
                {
                    Invoke(controllerType, controller, "PlayScene", typeof(int), 0);
                    Texture first = (Texture)Get(controller, "CurrentMediaTexture");
                    Invoke(controllerType, controller, "Advance", typeof(float), .15f);
                    Texture second = (Texture)Get(controller, "CurrentMediaTexture");
                    Assert.That(first, Is.Not.Null);
                    Assert.That(second, Is.Not.Null.And.Not.SameAs(first),
                        "SC-G proof must observe two actual decoded GIF frames, not only elapsed model state.");

                    Invoke(controllerType, controller, "Restart");
                    Assert.That((Texture)Get(controller, "CurrentMediaTexture"), Is.SameAs(first));
                    Assert.That((float)Get(controller, "MediaTimeSeconds"), Is.EqualTo(0f).Within(.0001f));
                }
                finally { Dispose(controller); }
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private static object Scene(string label, string text, string timingMode, float duration)
        {
            Type sceneType = RequireType("VnSceneComposerScene");
            object scene = Activator.CreateInstance(sceneType);
            Set(scene, "label", label);
            Set(scene, "previewText", text);
            object timing = Get(scene, "timing");
            SetEnum(timing, "previewAdvanceMode", timingMode);
            Set(timing, "previewAutoDuration", duration);
            return scene;
        }

        private static void SetAutoPreviewSequenceGap(object project, float sequenceGap)
        {
            object preset = Get(project, "defaultPresentation");
            Type resolverType = RequireType("VnPresentationWorkshopVn10Resolver");
            MethodInfo method = RequireStatic(
                resolverType,
                "SetTimingPreviewOverrides",
                preset.GetType(),
                typeof(float),
                typeof(float),
                typeof(float));
            method.Invoke(null, new object[] { preset, .05f, .08f, sequenceGap });
        }

        private static void AddCharacter(object scene, string id, string state, string slot)
        {
            Type characterType = RequireType("VnSceneComposerCharacter");
            object character = Activator.CreateInstance(characterType);
            Set(character, "characterId", id);
            Set(character, "stateId", state);
            FieldInfo slotField = characterType.GetField("stageSlot", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(slotField, Is.Not.Null);
            slotField.SetValue(character, Enum.Parse(slotField.FieldType, slot));
            ((IList)Get(scene, "characters")).Add(character);
        }

        private static void ConfigureCharacterTransition(object scene, string mode, float duration, float fade, float distance)
        {
            object transition = Get(Get(scene, "presentationOverrides"), "characterTransition");
            Set(transition, "hasMode", true);
            SetEnum(transition, "mode", mode);
            Set(transition, "hasDuration", true);
            Set(transition, "duration", duration);
            Set(transition, "hasFadeDuration", true);
            Set(transition, "fadeDuration", fade);
            Set(transition, "hasSlideDistance", true);
            Set(transition, "slideDistance", distance);
        }

        private static void ConfigureActionBounce(object scene, float duration)
        {
            object bounce = Get(Get(scene, "presentationOverrides"), "actionBounce");
            Set(bounce, "hasDuration", true);
            Set(bounce, "duration", duration);
        }

        private static void ConfigureBackgroundTransition(object scene, string mode, float duration)
        {
            object transition = Get(Get(scene, "presentationOverrides"), "backgroundTransition");
            Set(transition, "hasMode", true);
            SetEnum(transition, "mode", mode);
            Set(transition, "hasDuration", true);
            Set(transition, "duration", duration);
        }

        private static Type RequirePlaybackType()
        {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == EditorAssembly);
            Assert.That(assembly, Is.Not.Null);
            Type type = assembly.GetType(Namespace + "VnSceneComposerPlaybackController", false);
            Assert.That(type, Is.Not.Null, "Missing Scene Composer playback type: VnSceneComposerPlaybackController");
            return type;
        }

        private static Type RequireType(string shortName)
        {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == EditorAssembly);
            Assert.That(assembly, Is.Not.Null);
            Type type = assembly.GetType(Namespace + shortName, false);
            Assert.That(type, Is.Not.Null, "Missing Scene Composer type: " + shortName);
            return type;
        }

        private static object NewController(Type controllerType, Type projectType, object project)
        {
            ConstructorInfo ctor = controllerType.GetConstructor(new[] { projectType });
            Assert.That(ctor, Is.Not.Null, "Missing playback constructor accepting VnSceneComposerProject.");
            return ctor.Invoke(new[] { project });
        }

        private static void Invoke(Type type, object instance, string name)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            Assert.That(method, Is.Not.Null, "Missing playback method: " + name);
            method.Invoke(instance, null);
        }

        private static void Invoke(Type type, object instance, string name, Type parameterType, object value)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, new[] { parameterType }, null);
            Assert.That(method, Is.Not.Null, "Missing playback method: " + name);
            method.Invoke(instance, new[] { value });
        }

        private static MethodInfo RequireStatic(Type type, string name, params Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, parameters, null);
            Assert.That(method, Is.Not.Null, "Missing public static method: " + type.Name + "." + name);
            return method;
        }

        private static object Get(object instance, string name)
        {
            Assert.That(instance, Is.Not.Null, "Cannot read member '" + name + "' from null.");
            Type type = instance.GetType();
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null) return field.GetValue(instance);
            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, "Missing public field/property: " + type.Name + "." + name);
            return property.GetValue(instance, null);
        }

        private static void Set(object instance, string name, object value)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing public field: " + instance.GetType().Name + "." + name);
            field.SetValue(instance, value);
        }

        private static void SetEnum(object instance, string name, string value)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing public enum field: " + instance.GetType().Name + "." + name);
            field.SetValue(instance, Enum.Parse(field.FieldType, value));
        }

        private static void Dispose(object controller)
        {
            if (controller == null) return;
            MethodInfo dispose = controller.GetType().GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (dispose != null) dispose.Invoke(controller, null);
        }
    }
}
