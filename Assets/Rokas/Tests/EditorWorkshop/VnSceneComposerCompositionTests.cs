using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rokas.Presentation;
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
            Set(scene, "speaker", "Mina");
            Set(scene, "previewText", "Line one\nLine two");

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
            Set(scene, "speaker", "Narrator");
            Set(scene, "previewText", "A quiet room.\nRain taps the window.");
            Set(scene, "narration", true);

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

        private static object Character(Type characterType, Type slotType,
            string characterId, string stateId, string slot)
        {
            object character = Activator.CreateInstance(characterType);
            Set(character, "characterId", characterId);
            Set(character, "stateId", stateId);
            Set(character, "stageSlot", Enum.Parse(slotType, slot));
            return character;
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

        private static void Set(object instance, string name, object value)
        {
            Type type = instance.GetType();
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing public field: " + type.Name + "." + name);
            field.SetValue(instance, value);
        }
    }
}
