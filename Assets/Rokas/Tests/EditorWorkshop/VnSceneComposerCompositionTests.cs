using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerCompositionTests
    {
        private const string EditorAssembly = "Rokas.Editor";
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";

        [Test]
        public void PresentationResolutionLayersSceneOverridesOverProjectDefaultsWithoutMutatingInputs()
        {
            Type projectType = RequireType("VnSceneComposerProject");
            Type sceneType = RequireType("VnSceneComposerScene");
            Type compositionType = RequireType("VnSceneComposerComposition");
            object project = Activator.CreateInstance(projectType);
            object scene = Activator.CreateInstance(sceneType);
            object defaults = Get(project, "defaultPresentation");
            object sceneOverrides = Get(scene, "presentationOverrides");

            object defaultDialogue = Get(defaults, "dialogueText");
            Set(defaultDialogue, "hasPositionDelta", true);
            Set(defaultDialogue, "positionDelta", new Vector2(15f, 4f));
            object defaultTypography = Get(defaults, "typography");
            Set(defaultTypography, "hasDialogueFontSize", true);
            Set(defaultTypography, "dialogueFontSize", 28f);

            object sceneDialogue = Get(sceneOverrides, "dialogueText");
            Set(sceneDialogue, "hasSizeDelta", true);
            Set(sceneDialogue, "sizeDelta", new Vector2(30f, 8f));
            object sceneTypography = Get(sceneOverrides, "typography");
            Set(sceneTypography, "hasDialogueFontSize", true);
            Set(sceneTypography, "dialogueFontSize", 34f);

            MethodInfo resolve = RequireStatic(compositionType, "ResolvePresentation", projectType, sceneType);
            object resolved = resolve.Invoke(null, new[] { project, scene });

            Assert.That(resolved, Is.Not.Null.And.Not.SameAs(defaults).And.Not.SameAs(sceneOverrides));
            Assert.That((Vector2)Get(Get(resolved, "dialogueText"), "positionDelta"), Is.EqualTo(new Vector2(15f, 4f)));
            Assert.That((bool)Get(Get(resolved, "dialogueText"), "hasPositionDelta"), Is.True);
            Assert.That((Vector2)Get(Get(resolved, "dialogueText"), "sizeDelta"), Is.EqualTo(new Vector2(30f, 8f)));
            Assert.That((bool)Get(Get(resolved, "dialogueText"), "hasSizeDelta"), Is.True);
            Assert.That((float)Get(Get(resolved, "typography"), "dialogueFontSize"), Is.EqualTo(34f));
            Assert.That((float)Get(defaultTypography, "dialogueFontSize"), Is.EqualTo(28f));
            Assert.That((bool)Get(defaultDialogue, "hasSizeDelta"), Is.False);
            Assert.That((bool)Get(sceneDialogue, "hasPositionDelta"), Is.False);
        }

        [Test]
        public void BuildFrameUsesExistingWorkshopFrameAuthoredStatesSlotsAndActiveSpeaker()
        {
            Type projectType = RequireType("VnSceneComposerProject");
            Type sceneType = RequireType("VnSceneComposerScene");
            Type characterType = RequireType("VnSceneComposerCharacter");
            Type slotType = RequireType("VnWorkshopStageSlot");
            Type resolutionType = RequireType("VnWorkshopResolution");
            Type compositionType = RequireType("VnSceneComposerComposition");
            Type workshopFrameType = RequireType("VnWorkshopPreviewFrame");
            object project = Activator.CreateInstance(projectType);
            object scene = Activator.CreateInstance(sceneType);
            SetProperty(scene, "speaker", "Mina");
            SetProperty(scene, "previewText", "Line one\nLine two");

            IList characters = (IList)Get(scene, "characters");
            characters.Add(Character(characterType, slotType, "Mina", "mina_happy", "Left"));
            characters.Add(Character(characterType, slotType, "Keiko", "keiko_serious", "Right"));

            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            object resolution = Enum.Parse(resolutionType, "Reference1920x1080");
            MethodInfo build = RequireStatic(compositionType, "BuildFrame",
                projectType, sceneType, resolutionType, typeof(Texture2D));
            Assert.That(build.ReturnType, Is.EqualTo(workshopFrameType),
                "Composer must return the existing Workshop preview-frame type, not a second renderer model.");
            object frame = build.Invoke(null, new object[] { project, scene, resolution, assets.vnNightSkyRain });

            Assert.That(Get(frame, "BackgroundTexture"), Is.SameAs(assets.vnNightSkyRain));
            Assert.That((string)Get(frame, "Speaker"), Is.EqualTo("Mina"));
            Assert.That((string)Get(frame, "Dialogue"), Is.EqualTo("Line one\nLine two"));
            IList previewCharacters = (IList)Get(frame, "ComposerCharacters");
            Assert.That(previewCharacters.Count, Is.EqualTo(2));

            object mina = previewCharacters[0];
            object keiko = previewCharacters[1];
            Assert.That((string)Get(mina, "CharacterId"), Is.EqualTo("Mina"));
            Assert.That((string)Get(mina, "StateId"), Is.EqualTo("mina_happy"));
            Assert.That(Get(mina, "Slot").ToString(), Is.EqualTo("Left"));
            Assert.That((bool)Get(mina, "Active"), Is.True);
            Assert.That(Get(mina, "Texture"), Is.SameAs(assets.vnMinaCharacterSheet));
            Assert.That((float)Get(mina, "Alpha"), Is.EqualTo(1f).Within(.001f));

            Assert.That((string)Get(keiko, "CharacterId"), Is.EqualTo("Keiko"));
            Assert.That((string)Get(keiko, "StateId"), Is.EqualTo("keiko_serious"));
            Assert.That(Get(keiko, "Slot").ToString(), Is.EqualTo("Right"));
            Assert.That((bool)Get(keiko, "Active"), Is.False);
            Assert.That(Get(keiko, "Texture"), Is.SameAs(assets.vnKeikoCharacterSheet));
            Assert.That((float)Get(keiko, "Alpha"), Is.LessThan(1f));

            Assert.That(VnCharacterVisualCatalog.TryResolve("mina_happy", out VnCharacterVisualState minaState), Is.True);
            Assert.That(VnCharacterVisualCatalog.TryResolve("keiko_serious", out VnCharacterVisualState keikoState), Is.True);
            Assert.That((Rect)Get(mina, "Uv"), Is.EqualTo(minaState.BodyUv));
            Assert.That((Rect)Get(keiko, "Uv"), Is.EqualTo(keikoState.BodyUv));
            Assert.That(((Rect)Get(mina, "Body")).center.x, Is.LessThan(((Rect)Get(keiko, "Body")).center.x));
        }

        [Test]
        public void MD_BeatAwareCompositionResolvesEffectiveDialogueAndFocus()
        {
            Type projectType = RequireType("VnSceneComposerProject");
            Type sceneType = RequireType("VnSceneComposerScene");
            Type beatType = RequireType("VnSceneComposerDialogueBeat");
            Type characterType = RequireType("VnSceneComposerCharacter");
            Type slotType = RequireType("VnWorkshopStageSlot");
            Type resolutionType = RequireType("VnWorkshopResolution");
            Type compositionType = RequireType("VnSceneComposerComposition");
            object project = Activator.CreateInstance(projectType);
            object scene = Activator.CreateInstance(sceneType);

            IList beats = (IList)Get(scene, "dialogueBeats");
            Assert.That(beats, Has.Count.EqualTo(1));
            object beat0 = beats[0];
            Set(beat0, "speaker", "Mina");
            Set(beat0, "text", "First beat");

            object beat1 = Activator.CreateInstance(beatType);
            Set(beat1, "speaker", "Keiko");
            Set(beat1, "text", "Second beat");
            beats.Add(beat1);

            IList characters = (IList)Get(scene, "characters");
            characters.Add(Character(characterType, slotType, "Mina", "mina_happy", "Left"));
            characters.Add(Character(characterType, slotType, "Keiko", "keiko_serious", "Right"));

            MethodInfo build = compositionType.GetMethod("BuildFrame",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { projectType, sceneType, beatType, resolutionType, typeof(Texture2D) },
                null);
            Assert.That(build, Is.Not.Null,
                "M-DIALOGUE requires beat-aware composition instead of reading Scene-level dialogue proxies.");

            object resolution = Enum.Parse(resolutionType, "Reference1920x1080");
            object frame0 = build.Invoke(null, new[] { project, scene, beat0, resolution, null });
            object frame1 = build.Invoke(null, new[] { project, scene, beat1, resolution, null });

            Assert.That((string)Get(frame0, "Speaker"), Is.EqualTo("Mina"));
            Assert.That((string)Get(frame0, "Dialogue"), Is.EqualTo("First beat"));
            Assert.That((string)Get(frame1, "Speaker"), Is.EqualTo("Keiko"));
            Assert.That((string)Get(frame1, "Dialogue"), Is.EqualTo("Second beat"));

            IList frame0Characters = (IList)Get(frame0, "ComposerCharacters");
            IList frame1Characters = (IList)Get(frame1, "ComposerCharacters");
            Assert.That(frame0Characters.Count, Is.EqualTo(2));
            Assert.That(frame1Characters.Count, Is.EqualTo(2));

            Assert.That((string)Get(frame0Characters[0], "StateId"), Is.EqualTo("mina_happy"));
            Assert.That((string)Get(frame1Characters[0], "StateId"), Is.EqualTo("mina_happy"));
            Assert.That(Get(frame0Characters[0], "Slot").ToString(), Is.EqualTo("Left"));
            Assert.That(Get(frame1Characters[0], "Slot").ToString(), Is.EqualTo("Left"));
            Assert.That((string)Get(frame0Characters[1], "StateId"), Is.EqualTo("keiko_serious"));
            Assert.That((string)Get(frame1Characters[1], "StateId"), Is.EqualTo("keiko_serious"));
            Assert.That(Get(frame0Characters[1], "Slot").ToString(), Is.EqualTo("Right"));
            Assert.That(Get(frame1Characters[1], "Slot").ToString(), Is.EqualTo("Right"));

            Assert.That((bool)Get(frame0Characters[0], "Active"), Is.True);
            Assert.That((bool)Get(frame0Characters[1], "Active"), Is.False);
            Assert.That((bool)Get(frame1Characters[0], "Active"), Is.False);
            Assert.That((bool)Get(frame1Characters[1], "Active"), Is.True);

            Assert.That(((IList)Get(scene, "characters")).Count, Is.EqualTo(2),
                "Beat-aware composition must not rebuild or mutate Scene visual state.");
        }

        [Test]
        public void NarrationSupportsZeroCharactersAndComposerSupportsAtMostThreeVisibleCharacters()
        {
            Type projectType = RequireType("VnSceneComposerProject");
            Type sceneType = RequireType("VnSceneComposerScene");
            Type characterType = RequireType("VnSceneComposerCharacter");
            Type slotType = RequireType("VnWorkshopStageSlot");
            Type resolutionType = RequireType("VnWorkshopResolution");
            Type compositionType = RequireType("VnSceneComposerComposition");
            object project = Activator.CreateInstance(projectType);
            object scene = Activator.CreateInstance(sceneType);
            SetProperty(scene, "speaker", "Narrator");
            SetProperty(scene, "previewText", "A quiet room.\nRain taps the window.");
            SetProperty(scene, "narration", true);

            MethodInfo build = RequireStatic(compositionType, "BuildFrame",
                projectType, sceneType, resolutionType, typeof(Texture2D));
            object resolution = Enum.Parse(resolutionType, "Reference1920x1080");
            object narration = build.Invoke(null, new object[] { project, scene, resolution, null });
            Assert.That((string)Get(narration, "Speaker"), Is.Empty);
            Assert.That((string)Get(narration, "Dialogue"), Does.Contain("Rain taps"));
            Assert.That(((IList)Get(narration, "ComposerCharacters")).Count, Is.EqualTo(0));

            IList characters = (IList)Get(scene, "characters");
            characters.Add(Character(characterType, slotType, "Mina", "mina_neutral", "Left"));
            characters.Add(Character(characterType, slotType, "Keiko", "keiko_neutral", "Center"));
            characters.Add(Character(characterType, slotType, "Mina", "mina_serious", "Right"));
            object three = build.Invoke(null, new object[] { project, scene, resolution, null });
            Assert.That(((IList)Get(three, "ComposerCharacters")).Count, Is.EqualTo(3));

            characters.Add(Character(characterType, slotType, "Keiko", "keiko_surprised", "Center"));
            TargetInvocationException error = Assert.Throws<TargetInvocationException>(() =>
                build.Invoke(null, new object[] { project, scene, resolution, null }));
            Assert.That(error.InnerException, Is.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void UnknownStateIsRejectedInsteadOfCreatingAnArbitraryCharacterImporter()
        {
            Type projectType = RequireType("VnSceneComposerProject");
            Type sceneType = RequireType("VnSceneComposerScene");
            Type characterType = RequireType("VnSceneComposerCharacter");
            Type slotType = RequireType("VnWorkshopStageSlot");
            Type resolutionType = RequireType("VnWorkshopResolution");
            Type compositionType = RequireType("VnSceneComposerComposition");
            object project = Activator.CreateInstance(projectType);
            object scene = Activator.CreateInstance(sceneType);
            ((IList)Get(scene, "characters")).Add(
                Character(characterType, slotType, "Mina", "mina_not_authored", "Center"));

            MethodInfo build = RequireStatic(compositionType, "BuildFrame",
                projectType, sceneType, resolutionType, typeof(Texture2D));
            object resolution = Enum.Parse(resolutionType, "Reference1920x1080");
            TargetInvocationException error = Assert.Throws<TargetInvocationException>(() =>
                build.Invoke(null, new object[] { project, scene, resolution, null }));
            Assert.That(error.InnerException, Is.TypeOf<ArgumentException>());
            Assert.That(error.InnerException.Message, Does.Contain("authored").IgnoreCase);
        }

        [Test]
        public void OnboardedFullBodyStateRendersOwnTextureWithFullUvWithoutProductionCatalogEntry()
        {
            Type purposeType = RequireType("VnSceneComposerAssetPurpose");
            Type libraryType = RequireType("VnSceneComposerAssetLibrary");
            string projectRoot = ProjectRoot();
            string source = CreateTransparentPng(8, 14);
            string stableId = null;
            string assetPath = null;
            try
            {
                object onboard = InvokeStatic(libraryType, "Onboard",
                    new[] { typeof(string), typeof(string), purposeType, typeof(string), typeof(string), typeof(string) },
                    projectRoot, source, Enum.Parse(purposeType, "CharacterState"), "Rendered Full Body", "Mina", "tdd_render_full_body");
                Assert.That((bool)Get(onboard, "Success"), Is.True, Get(onboard, "Error") as string);
                object entry = Get(onboard, "Entry");
                stableId = (string)Get(entry, "stableAssetId");
                assetPath = (string)Get(entry, "assetPath");
                string stateId = (string)Get(entry, "stateId");
                Assert.That(VnCharacterVisualCatalog.TryResolve(stateId, out _), Is.False,
                    "Onboarded states must remain outside the production character catalog.");

                Type projectType = RequireType("VnSceneComposerProject");
                Type sceneType = RequireType("VnSceneComposerScene");
                Type characterType = RequireType("VnSceneComposerCharacter");
                Type slotType = RequireType("VnWorkshopStageSlot");
                Type resolutionType = RequireType("VnWorkshopResolution");
                Type compositionType = RequireType("VnSceneComposerComposition");
                object project = Activator.CreateInstance(projectType);
                object scene = Activator.CreateInstance(sceneType);
                SetProperty(scene, "speaker", "Mina");
                ((IList)Get(scene, "characters")).Add(Character(characterType, slotType, "Mina", stateId, "Center"));

                MethodInfo build = RequireStatic(compositionType, "BuildFrame",
                    projectType, sceneType, resolutionType, typeof(Texture2D));
                object frame = build.Invoke(null, new object[]
                {
                    project, scene, Enum.Parse(resolutionType, "Reference1920x1080"), null
                });
                object preview = ((IList)Get(frame, "ComposerCharacters"))[0];
                Texture2D expected = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                Assert.That(expected, Is.Not.Null);
                Assert.That(Get(preview, "Texture"), Is.SameAs(expected));
                Assert.That((Rect)Get(preview, "Uv"), Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
                Assert.That(((Rect)Get(preview, "Body")).width, Is.GreaterThan(0f));
                Assert.That(((Rect)Get(preview, "Body")).height, Is.GreaterThan(0f));
            }
            finally
            {
                CleanupLibraryAsset(libraryType, projectRoot, stableId, assetPath);
                if (File.Exists(source)) File.Delete(source);
            }
        }

        [Test]
        public void WindowSelectorsDiscoverOnboardedStateAndBackgroundAndAssignWithoutGuidTyping()
        {
            Type purposeType = RequireType("VnSceneComposerAssetPurpose");
            Type libraryType = RequireType("VnSceneComposerAssetLibrary");
            string projectRoot = ProjectRoot();
            string stateSource = CreateTransparentPng(8, 14);
            string backgroundSource = CreateOpaquePng(16, 9);
            string stateStableId = null;
            string stateAssetPath = null;
            string backgroundStableId = null;
            string backgroundAssetPath = null;
            UnityEngine.Object window = null;
            try
            {
                object stateResult = InvokeStatic(libraryType, "Onboard",
                    new[] { typeof(string), typeof(string), purposeType, typeof(string), typeof(string), typeof(string) },
                    projectRoot, stateSource, Enum.Parse(purposeType, "CharacterState"), "Selector State", "Mina", "tdd_selector_state");
                object stateEntry = Get(stateResult, "Entry");
                stateStableId = (string)Get(stateEntry, "stableAssetId");
                stateAssetPath = (string)Get(stateEntry, "assetPath");
                string stateId = (string)Get(stateEntry, "stateId");

                object backgroundResult = InvokeStatic(libraryType, "Onboard",
                    new[] { typeof(string), typeof(string), purposeType, typeof(string), typeof(string), typeof(string) },
                    projectRoot, backgroundSource, Enum.Parse(purposeType, "Background"), "Selector Background", string.Empty, string.Empty);
                object backgroundEntry = Get(backgroundResult, "Entry");
                backgroundStableId = (string)Get(backgroundEntry, "stableAssetId");
                backgroundAssetPath = (string)Get(backgroundEntry, "assetPath");
                string backgroundGuid = (string)Get(backgroundEntry, "assetGuid");

                Type windowType = RequireType("VnPresentationWorkshopWindow");
                window = ScriptableObject.CreateInstance(windowType);
                RequireInstance(windowType, "ComposerAddScene").Invoke(window, null);
                RequireInstance(windowType, "ComposerRefreshAssets").Invoke(window, null);

                string[] states = (string[])RequireInstance(windowType, "ComposerGetAuthoredStateIds", typeof(string))
                    .Invoke(window, new object[] { "Mina" });
                Assert.That(states, Does.Contain(stateId));
                RequireInstance(windowType, "ComposerAddCharacter", typeof(string), typeof(string))
                    .Invoke(window, new object[] { "Mina", stateId });

                string[] backgrounds = (string[])RequireInstance(windowType, "ComposerGetAvailableBackgroundAssetIds")
                    .Invoke(window, null);
                Assert.That(backgrounds, Does.Contain(backgroundStableId));
                RequireInstance(windowType, "ComposerSetLibraryBackground", typeof(string))
                    .Invoke(window, new object[] { backgroundStableId });

                IList scenes = (IList)Get(GetField(window, "_sceneComposerProject"), "scenes");
                object scene = scenes[0];
                IList characters = (IList)Get(scene, "characters");
                Assert.That(characters.Count, Is.EqualTo(1));
                Assert.That((string)Get(characters[0], "stateId"), Is.EqualTo(stateId));
                object media = Get(scene, "media");
                Assert.That(Get(media, "kind").ToString(), Is.EqualTo("ExistingRokasAsset"));
                Assert.That((string)Get(media, "reference"), Is.EqualTo(backgroundGuid));
            }
            finally
            {
                if (window != null) UnityEngine.Object.DestroyImmediate(window);
                CleanupLibraryAsset(libraryType, projectRoot, stateStableId, stateAssetPath);
                CleanupLibraryAsset(libraryType, projectRoot, backgroundStableId, backgroundAssetPath);
                if (File.Exists(stateSource)) File.Delete(stateSource);
                if (File.Exists(backgroundSource)) File.Delete(backgroundSource);
            }
        }

        private static object Character(Type characterType, Type slotType,
            string characterId, string stateId, string slot)
        {
            object character = Activator.CreateInstance(characterType);
            Set(character, "characterId", characterId);
            Set(character, "stateId", stateId);
            Set(character, "stageSlot", Enum.Parse(slotType, slot));
            return character;
        }

        private static string ProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }

        private static string CreateTransparentPng(int width, int height)
        {
            string path = Path.Combine(Path.GetTempPath(), "rokas-vn-composition-state-" + Guid.NewGuid().ToString("N") + ".png");
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = Enumerable.Range(0, width * height)
                .Select(index => new Color(.2f, .7f, .9f, index % 3 == 0 ? .35f : 1f)).ToArray();
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            return path;
        }

        private static string CreateOpaquePng(int width, int height)
        {
            string path = Path.Combine(Path.GetTempPath(), "rokas-vn-composition-bg-" + Guid.NewGuid().ToString("N") + ".png");
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(Enumerable.Repeat(new Color(.15f, .2f, .35f, 1f), width * height).ToArray());
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            return path;
        }

        private static void CleanupLibraryAsset(Type libraryType, string projectRoot, string stableId, string assetPath)
        {
            if (libraryType != null && !string.IsNullOrWhiteSpace(stableId))
            {
                MethodInfo unregister = libraryType.GetMethod("Unregister", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(string), typeof(string) }, null);
                if (unregister != null) unregister.Invoke(null, new object[] { projectRoot, stableId });
            }
            if (!string.IsNullOrWhiteSpace(assetPath)) AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.Refresh();
        }

        private static object InvokeStatic(Type type, string name, Type[] parameters, params object[] args)
        {
            MethodInfo method = RequireStatic(type, name, parameters);
            try { return method.Invoke(null, args); }
            catch (TargetInvocationException exception) { throw exception.InnerException ?? exception; }
        }

        private static MethodInfo RequireInstance(Type type, string name, params Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, parameters ?? Type.EmptyTypes, null);
            Assert.That(method, Is.Not.Null, "Missing Scene Composer window method: " + type.Name + "." + name);
            return method;
        }

        private static object GetField(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing field: " + instance.GetType().Name + "." + name);
            return field.GetValue(instance);
        }

        private static Type RequireType(string shortName)
        {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == EditorAssembly);
            Assert.That(assembly, Is.Not.Null);
            Type type = assembly.GetType(Namespace + shortName, false);
            Assert.That(type, Is.Not.Null, "Missing Scene Composer/Workshop type: " + shortName);
            return type;
        }

        private static MethodInfo RequireStatic(Type type, string name, params Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, parameters, null);
            Assert.That(method, Is.Not.Null, "Missing public static method: " + type.Name + "." + name);
            return method;
        }

        private static object Get(object instance, string name)
        {
            Type type = instance.GetType();
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null) return field.GetValue(instance);
            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, "Missing public field/property: " + type.Name + "." + name);
            return property.GetValue(instance, null);
        }

        private static void SetProperty(object instance, string name, object value)
        {
            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, "Missing public property: " + type.Name + "." + name);
            property.SetValue(instance, value, null);
        }

        private static void Set(object instance, string name, object value)
        {
            Type type = instance.GetType();
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing public field: " + type.Name + "." + name);
            field.SetValue(instance, value);
        }
    }
}
