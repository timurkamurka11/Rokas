using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed partial class VnSceneComposerPlaybackTests
    {
        private static readonly string[] Md3DurationKinds =
        {
            "background",
            "character",
            "expression",
            "bounce",
            "stage",
            "focus"
        };

        [Test]
        public void DurationAuthoringPreservesPreciseValuesAcrossAllSixEffects()
        {
            Type windowType = Md3RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = Md3CreateWindow(windowType);
            try
            {
                Md3Invoke(window, "ComposerAddScene");
                foreach (string kind in Md3DurationKinds)
                {
                    float minimum = kind == "bounce" ? .01f : 0f;
                    foreach (float value in new[] { minimum, .25f, 10f })
                    {
                        Md3SetDuration(window, kind, value);
                        Assert.That(Md3ReadDuration(window, kind), Is.EqualTo(value).Within(.0001f), kind + " should preserve " + value + " seconds.");
                    }
                }
            }
            finally
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void FiniteOutOfRangeDurationsClampBeforeProjectMutation()
        {
            Type windowType = Md3RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = Md3CreateWindow(windowType);
            try
            {
                Md3Invoke(window, "ComposerAddScene");
                foreach (string kind in Md3DurationKinds)
                {
                    float minimum = kind == "bounce" ? .01f : 0f;
                    Md3SetDuration(window, kind, .75f);
                    Md3SetDuration(window, kind, -2f);
                    Assert.That(Md3ReadDuration(window, kind), Is.EqualTo(minimum).Within(.0001f), kind + " should clamp below its durable minimum.");

                    Md3SetDuration(window, kind, 12f);
                    Assert.That(Md3ReadDuration(window, kind), Is.EqualTo(10f).Within(.0001f), kind + " should clamp above the durable maximum.");
                }
            }
            finally
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void NonFiniteDurationsKeepPreviousValidValue()
        {
            Type windowType = Md3RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = Md3CreateWindow(windowType);
            try
            {
                Md3Invoke(window, "ComposerAddScene");
                foreach (string kind in Md3DurationKinds)
                {
                    Md3SetDuration(window, kind, .75f);
                    foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                    {
                        Md3SetDuration(window, kind, invalid);
                        Assert.That(Md3ReadDuration(window, kind), Is.EqualTo(.75f).Within(.0001f), kind + " should keep the previous valid value for non-finite input.");
                    }
                }
            }
            finally
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void BounceZeroSanitizesToDurablePointZeroOneMinimum()
        {
            Type windowType = Md3RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = Md3CreateWindow(windowType);
            try
            {
                Md3Invoke(window, "ComposerAddScene");
                Md3SetDuration(window, "bounce", 0f);
                Assert.That(Md3ReadDuration(window, "bounce"), Is.EqualTo(.01f).Within(.0001f));
            }
            finally
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void DurationEditRemainsUndoableThroughNormalComposerMutationRoute()
        {
            Type windowType = Md3RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = Md3CreateWindow(windowType);
            try
            {
                Md3Invoke(window, "ComposerAddScene");
                Md3SetDuration(window, "background", .5f);
                Undo.ClearAll();

                Md3SetDuration(window, "background", 2.5f);
                Undo.FlushUndoRecordObjects();
                Assert.That(Md3ReadDuration(window, "background"), Is.EqualTo(2.5f).Within(.0001f));

                Undo.PerformUndo();
                Assert.That(Md3ReadDuration(window, "background"), Is.EqualTo(.5f).Within(.0001f));
            }
            finally
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void SanitizedDurationProjectRemainsPortableSerializable()
        {
            Type windowType = Md3RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = Md3CreateWindow(windowType);
            try
            {
                Md3Invoke(window, "ComposerAddScene");
                Md3SetDuration(window, "background", 20f);
                Md3SetDuration(window, "bounce", 0f);

                object project = Md3GetField(window, "_sceneComposerProject");
                Type serializationType = Md3RequireType("VnSceneComposerSerialization");
                MethodInfo serialize = Md3RequireStatic(serializationType, "SerializePortable", 1);
                MethodInfo deserialize = Md3RequireStatic(serializationType, "DeserializePortable", 1);
                string json = (string)Md3InvokeMethod(null, serialize, project);
                object result = Md3InvokeMethod(null, deserialize, json);

                Assert.That(Md3GetProperty(result, "Success"), Is.True, Md3GetProperty(result, "Error") as string);
                Assert.That(Md3ReadDuration(window, "background"), Is.EqualTo(10f).Within(.0001f));
                Assert.That(Md3ReadDuration(window, "bounce"), Is.EqualTo(.01f).Within(.0001f));
            }
            finally
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static void Md3SetDuration(object window, string kind, float duration)
        {
            object easing = Md3EnumValue("VnWorkshopEasing", "EaseInOut");
            switch (kind)
            {
                case "background":
                    Md3Invoke(window, "ComposerSetBackgroundTransition",
                        Md3EnumValue("VnWorkshopBackgroundTransitionMode", "Fade"), duration, .85f,
                        Md3EnumValue("VnWorkshopCurtainDirection", "RightToLeft"), easing);
                    return;
                case "character":
                    Md3Invoke(window, "ComposerSetCharacterTransition",
                        Md3EnumValue("VnWorkshopCharacterTransitionMode", "Fade"), duration, .30f, 64f,
                        Md3EnumValue("VnWorkshopSlideDirection", "Left"), easing);
                    return;
                case "expression":
                    Md3Invoke(window, "ComposerSetExpressionTransition", duration, easing);
                    return;
                case "bounce":
                    Md3Invoke(window, "ComposerSetBounce", 18f, duration, .03f, .15f, easing);
                    return;
                case "stage":
                    Md3Invoke(window, "ComposerSetStageLayout", -360f, 0f, 360f, 0f, 1f, 1f, 1f, 280f, duration, easing);
                    return;
                case "focus":
                    Md3Invoke(window, "ComposerSetSpeakerFocus", 1f, 1f, 12f, 1f, .8f, 1f, duration, easing);
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private static float Md3ReadDuration(object window, string kind)
        {
            object preset = Md3Invoke(window, "ComposerGetActivePresentationPreset");
            string groupName;
            string fieldName;
            switch (kind)
            {
                case "background": groupName = "backgroundTransition"; fieldName = "duration"; break;
                case "character": groupName = "characterTransition"; fieldName = "duration"; break;
                case "expression": groupName = "expressionTransition"; fieldName = "duration"; break;
                case "bounce": groupName = "actionBounce"; fieldName = "duration"; break;
                case "stage": groupName = "stageLayout"; fieldName = "repositionDuration"; break;
                case "focus": groupName = "focus"; fieldName = "transitionDuration"; break;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }

            object group = Md3GetField(preset, groupName);
            return (float)Md3GetField(group, fieldName);
        }

        private static object Md3EnumValue(string shortName, string value)
        {
            return Enum.Parse(Md3RequireType(shortName), value);
        }

        private static UnityEngine.Object Md3CreateWindow(Type windowType)
        {
            UnityEngine.Object window = ScriptableObject.CreateInstance(windowType);
            Assert.That(window, Is.Not.Null);
            return window;
        }

        private static object Md3Invoke(object target, string name, params object[] args)
        {
            Type type = target.GetType();
            MethodInfo method = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SingleOrDefault(candidate => candidate.Name == name && candidate.GetParameters().Length == args.Length);
            Assert.That(method, Is.Not.Null, "Missing instance method " + name + " with " + args.Length + " parameters.");
            return Md3InvokeMethod(target, method, args);
        }

        private static object Md3InvokeMethod(object target, MethodInfo method, params object[] args)
        {
            try
            {
                return method.Invoke(target, args);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                throw exception.InnerException;
            }
        }

        private static MethodInfo Md3RequireStatic(Type type, string name, int parameterCount)
        {
            MethodInfo method = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .SingleOrDefault(candidate => candidate.Name == name && candidate.GetParameters().Length == parameterCount);
            Assert.That(method, Is.Not.Null, "Missing static method " + name + " with " + parameterCount + " parameters.");
            return method;
        }

        private static object Md3GetField(object target, string name)
        {
            Assert.That(target, Is.Not.Null, "Cannot read field " + name + " from null.");
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing field " + name + " on " + target.GetType().FullName + ".");
            return field.GetValue(target);
        }

        private static object Md3GetProperty(object target, string name)
        {
            Assert.That(target, Is.Not.Null);
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, "Missing property " + name + " on " + target.GetType().FullName + ".");
            return property.GetValue(target);
        }

        private static Type Md3RequireType(string shortName)
        {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == "Rokas.Editor");
            Assert.That(assembly, Is.Not.Null, "Missing Rokas.Editor assembly.");
            Type type = assembly.GetType("Rokas.EditorTools.VnUiWorkshop." + shortName, false);
            Assert.That(type, Is.Not.Null, "Missing type Rokas.EditorTools.VnUiWorkshop." + shortName + ".");
            return type;
        }
    }
}
