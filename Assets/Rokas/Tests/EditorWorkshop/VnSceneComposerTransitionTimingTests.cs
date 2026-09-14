using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerTransitionTimingTests
    {
        private const string EditorAssembly = "Rokas.Editor";
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";

        [Test]
        public void TransitionSamplerReusesExistingVn10BackgroundAndBounceSamplers()
        {
            Type projectType = RequireType("VnSceneComposerProject");
            Type sceneType = RequireType("VnSceneComposerScene");
            Type samplerType = RequireType("VnSceneComposerTransitionSampler");
            object project = Activator.CreateInstance(projectType);
            object from = Activator.CreateInstance(sceneType);
            object to = Activator.CreateInstance(sceneType);
            Set(Get(to, "transition"), "triggerActionBounce", true);

            object preset = Get(to, "presentationOverrides");
            object background = Get(preset, "backgroundTransition");
            Set(background, "hasMode", true);
            SetEnum(background, "mode", "Curtain");
            Set(background, "hasDuration", true);
            Set(background, "duration", .8f);

            MethodInfo sample = RequireStatic(samplerType, "Sample", projectType, sceneType, sceneType, typeof(float));
            object midpoint = sample.Invoke(null, new[] { project, from, to, (object)0.5f });
            object backgroundSample = Get(midpoint, "background");
            object bounceSample = Get(midpoint, "bounce");

            Assert.That((float)Get(backgroundSample, "CurtainCoverage"), Is.GreaterThan(.9f),
                "Composer must expose the existing VN10 curtain sample, not invent another transition engine.");
            Assert.That((float)Get(backgroundSample, "CurtainDarkness"), Is.GreaterThan(0f));
            Assert.That(((Vector2)Get(bounceSample, "PositionOffset")).y, Is.Not.EqualTo(0f));
            Assert.That((float)Get(bounceSample, "ScaleMultiplier"), Is.GreaterThan(1f));
        }

        [Test]
        public void TransitionSamplerReusesVn10CharacterEnterExitStageAndSpeakerFocus()
        {
            Type projectType = RequireType("VnSceneComposerProject");
            Type sceneType = RequireType("VnSceneComposerScene");
            Type samplerType = RequireType("VnSceneComposerTransitionSampler");
            Type characterType = RequireType("VnSceneComposerCharacter");
            Type slotType = RequireType("VnWorkshopStageSlot");
            object project = Activator.CreateInstance(projectType);
            object from = Activator.CreateInstance(sceneType);
            object to = Activator.CreateInstance(sceneType);

            ((IList)Get(from, "characters")).Add(Character(characterType, slotType, "Mina", "mina_neutral", "Center"));
            Set(from, "speaker", "Mina");
            ((IList)Get(to, "characters")).Add(Character(characterType, slotType, "Mina", "mina_neutral", "Left"));
            ((IList)Get(to, "characters")).Add(Character(characterType, slotType, "Keiko", "keiko_neutral", "Right"));
            Set(to, "speaker", "Keiko");

            MethodInfo sample = RequireStatic(samplerType, "Sample", projectType, sceneType, sceneType, typeof(float));
            object later = sample.Invoke(null, new[] { project, from, to, (object)0.75f });
            IList stage = (IList)Get(later, "stage");
            IList characterMotions = (IList)Get(later, "characterMotions");
            IList focus = (IList)Get(later, "focus");

            Assert.That(stage.Count, Is.EqualTo(2));
            Assert.That(characterMotions.Count, Is.EqualTo(1), "Only Keiko is entering in this beat.");
            Assert.That((bool)Get(characterMotions[0], "entering"), Is.True);
            Assert.That((string)Get(characterMotions[0], "characterId"), Is.EqualTo("Keiko"));
            Assert.That(focus.Count, Is.EqualTo(2));
            Assert.That((float)Get(focus[0], "Alpha"), Is.LessThan(1f));
            Assert.That((float)Get(focus[1], "Alpha"), Is.GreaterThan((float)Get(focus[0], "Alpha")));
        }

        [Test]
        public void TransitionSamplerReusesAuthoredExpressionTransitionForStateChanges()
        {
            Type projectType = RequireType("VnSceneComposerProject");
            Type sceneType = RequireType("VnSceneComposerScene");
            Type samplerType = RequireType("VnSceneComposerTransitionSampler");
            Type characterType = RequireType("VnSceneComposerCharacter");
            Type slotType = RequireType("VnWorkshopStageSlot");
            object project = Activator.CreateInstance(projectType);
            object from = Activator.CreateInstance(sceneType);
            object to = Activator.CreateInstance(sceneType);
            ((IList)Get(from, "characters")).Add(Character(characterType, slotType, "Mina", "mina_neutral", "Center"));
            ((IList)Get(to, "characters")).Add(Character(characterType, slotType, "Mina", "mina_happy", "Center"));

            MethodInfo sample = RequireStatic(samplerType, "Sample", projectType, sceneType, sceneType, typeof(float));
            object midpoint = sample.Invoke(null, new[] { project, from, to, (object)0.5f });
            IList expressions = (IList)Get(midpoint, "expressions");
            Assert.That(expressions.Count, Is.EqualTo(1));
            Assert.That((string)Get(expressions[0], "characterId"), Is.EqualTo("Mina"));
            object transition = Get(expressions[0], "sample");
            Assert.That((string)Get(transition, "StartStateId"), Is.EqualTo("mina_neutral"));
            Assert.That((string)Get(transition, "EndStateId"), Is.EqualTo("mina_happy"));
            Assert.That((float)Get(transition, "StartAlpha"), Is.InRange(.1f, .9f));
            Assert.That((float)Get(transition, "EndAlpha"), Is.InRange(.1f, .9f));
        }

        [Test]
        public void TypewriterSamplingProducesObservableTextChangeAcrossPreviewProgress()
        {
            Type projectType = RequireType("VnSceneComposerProject");
            Type sceneType = RequireType("VnSceneComposerScene");
            Type samplerType = RequireType("VnSceneComposerTransitionSampler");
            object project = Activator.CreateInstance(projectType);
            object from = Activator.CreateInstance(sceneType);
            object to = Activator.CreateInstance(sceneType);
            Set(to, "previewText", "One line, then another.");

            MethodInfo sample = RequireStatic(samplerType, "Sample", projectType, sceneType, sceneType, typeof(float));
            object start = sample.Invoke(null, new[] { project, from, to, (object)0f });
            object middle = sample.Invoke(null, new[] { project, from, to, (object)0.5f });
            object end = sample.Invoke(null, new[] { project, from, to, (object)1f });
            string startText = (string)Get(start, "visibleText");
            string middleText = (string)Get(middle, "visibleText");
            string endText = (string)Get(end, "visibleText");

            Assert.That(startText.Length, Is.LessThan(middleText.Length));
            Assert.That(middleText.Length, Is.LessThan(endText.Length));
            Assert.That(endText, Is.EqualTo("One line, then another."));
        }

        [Test]
        public void TimingPlanKeepsAuthoringAutoDurationSeparateFromManualBeatSemantics()
        {
            Type projectType = RequireType("VnSceneComposerProject");
            Type sceneType = RequireType("VnSceneComposerScene");
            Type samplerType = RequireType("VnSceneComposerTransitionSampler");
            object project = Activator.CreateInstance(projectType);
            object scene = Activator.CreateInstance(sceneType);
            Set(scene, "previewText", "Timing sample.");
            object timing = Get(scene, "timing");
            SetEnum(timing, "previewAdvanceMode", "PreviewAutoDuration");
            Set(timing, "previewAutoDuration", 4.25f);

            MethodInfo resolve = RequireStatic(samplerType, "ResolveTiming", projectType, sceneType);
            object auto = resolve.Invoke(null, new[] { project, scene });
            Assert.That((bool)Get(auto, "usesPreviewAutoDuration"), Is.True);
            Assert.That((float)Get(auto, "previewAutoDuration"), Is.EqualTo(4.25f).Within(.001f));
            Assert.That((float)Get(auto, "typewriterDuration"), Is.GreaterThan(0f));
            Assert.That((float)Get(auto, "settleDuration"), Is.GreaterThanOrEqualTo(0f));

            SetEnum(timing, "previewAdvanceMode", "ManualBeat");
            object manual = resolve.Invoke(null, new[] { project, scene });
            Assert.That((bool)Get(manual, "usesPreviewAutoDuration"), Is.False);
            Assert.That((float)Get(manual, "previewAutoDuration"), Is.EqualTo(0f));
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
            Assert.That(type, Is.Not.Null, "Missing Scene Composer transition/timing type: " + shortName);
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
    }
}
