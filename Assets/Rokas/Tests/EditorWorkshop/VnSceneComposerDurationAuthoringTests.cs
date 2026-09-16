using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerDurationAuthoringTests
    {
        private const string EditorAssembly = "Rokas.Editor";
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";

        private static readonly string[] DurationKinds =
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
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                Invoke(window, "ComposerAddScene");
                foreach (string kind in DurationKinds)
                {
                    float minimum = kind == "bounce" ? .01f : 0f;
                    foreach (float value in new[] { minimum, .25f, 10f })
                    {
                        SetDuration(window, kind, value);
                        Assert.That(ReadDuration(window, kind), Is.EqualTo(value).Within(.0001f), kind + " should preserve " + value + " seconds.");
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
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                Invoke(window, "ComposerAddScene");
                foreach (string kind in DurationKinds)
                {
                    float minimum = kind == "bounce" ? .01f : 0f;
                    SetDuration(window, kind, .75f);
                    SetDuration(window, kind, -2f);
                    Assert.That(ReadDuration(window, kind), Is.EqualTo(minimum).Within(.0001f), kind + " should clamp below its durable minimum.");

                    SetDuration(window, kind, 12f);
                    Assert.That(ReadDuration(window, kind), Is.EqualTo(10f).Within(.0001f), kind + " should clamp above the durable maximum.");
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
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                Invoke(window, "ComposerAddScene");
                foreach (string kind in DurationKinds)
                {
                    SetDuration(window, kind, .75f);
                    foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                    {
                        SetDuration(window, kind, invalid);
                        Assert.That(ReadDuration(window, kind), Is.EqualTo(.75f).Within(.0001f), kind + " should keep the previous valid value for non-finite input.");
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
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                Invoke(window, "ComposerAddScene");
                SetDuration(window, "bounce", 0f);
                Assert.That(ReadDuration(window, "bounce"), Is.EqualTo(.01f).Within(.0001f));
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
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                Invoke(window, "ComposerAddScene");
                SetDuration(window, "background", .5f);
                Undo.ClearAll();

                SetDuration(window, "background", 2.5f);
                Undo.FlushUndoRecordObjects();
                Assert.That(ReadDuration(window, "background"), Is.EqualTo(2.5f).Within(.0001f));

                Undo.PerformUndo();
                Assert.That(ReadDuration(window, "background"), Is.EqualTo(.5f).Within(.0001f));
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
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = CreateWindow(windowType);
            try
            {
                Invoke(window, "ComposerAddScene");
                SetDuration(window, "background", 20f);
                SetDuration(window, "bounce", 0f);

                object project = GetField(window, "_sceneComposerProject");
                Type serializationType = RequireType("VnSceneComposerSerialization");
                MethodInfo serialize = RequireStatic(serializationType, "SerializePortable", 1);
                MethodInfo deserialize = RequireStatic(serializationType, "DeserializePortable", 1);
                string json = (string)InvokeMethod(null, serialize, project);
                object result = InvokeMethod(null, deserialize, json);

                Assert.That(GetProperty(result, "Success"), Is.True, GetProperty(result, "Error") as string);
                Assert.That(ReadDuration(window, "background"), Is.EqualTo(10f).Within(.0001f));
                Assert.That(ReadDuration(window, "bounce"), Is.EqualTo(.01f).Within(.0001f));
            }
            finally
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static void SetDuration(object window, string kind, float duration)
        {
            object easing = EnumValue("VnWorkshopEasing", "EaseInOut");
            switch (kind)
            {
                case "background":
                    Invoke(window, "ComposerSetBackgroundTransition",
                        EnumValue("VnWorkshopBackgroundTransitionMode", "Fade"), duration, .85f,
                        EnumValue("VnWorkshopCurtainDirection", "RightToLeft"), easing);
                    return;
                case "character":
                    Invoke(window, "ComposerSetCharacterTransition",
                        EnumValue("VnWorkshopCharacterTransitionMode", "Fade"), duration, .30f, 64f,
                        EnumValue("VnWorkshopSlideDirection", "Left"), easing);
                    return;
                case "expression":
                    Invoke(window, "ComposerSetExpressionTransition", duration, easing);
                    return;
                case "bounce":
                    Invoke(window, "ComposerSetBounce", 18f, duration, .03f, .15f, easing);
                    return;
                case "stage":
                    Invoke(window, "ComposerSetStageLayout", -360f, 0f, 360f, 0f, 1f, 1f, 1f, 280f, duration, easing);
                    return;
                case "focus":
                    Invoke(window, "ComposerSetSpeakerFocus", 1f, 1f, 12f, 1f, .8f, 1f, duration, easing);
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private static float ReadDuration(object window, string kind)
        {
            object preset = Invoke(window, "ComposerGetActivePresentationPreset");
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

            object group = GetField(preset, groupName);
            return (float)GetField(group, fieldName);
        }

        private static object EnumValue(string shortName, string value)
        {
            return Enum.Parse(RequireType(shortName), value);
        }

        private static UnityEngine.Object CreateWindow(Type windowType)
        {
            UnityEngine.Object window = ScriptableObject.CreateInstance(windowType);
            Assert.That(window, Is.Not.Null);
            return window;
        }

        private static object Invoke(object target, string name, params object[] args)
        {
            Type type = target.GetType();
            MethodInfo method = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SingleOrDefault(candidate => candidate.Name == name && candidate.GetParameters().Length == args.Length);
            Assert.That(method, Is.Not.Null, "Missing instance method " + name + " with " + args.Length + " parameters.");
            return InvokeMethod(target, method, args);
        }

        private static object InvokeMethod(object target, MethodInfo method, params object[] args)
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

        private static MethodInfo RequireStatic(Type type, string name, int parameterCount)
        {
            MethodInfo method = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .SingleOrDefault(candidate => candidate.Name == name && candidate.GetParameters().Length == parameterCount);
            Assert.That(method, Is.Not.Null, "Missing static method " + name + " with " + parameterCount + " parameters.");
            return method;
        }

        private static object GetField(object target, string name)
        {
            Assert.That(target, Is.Not.Null, "Cannot read field " + name + " from null.");
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing field " + name + " on " + target.GetType().FullName + ".");
            return field.GetValue(target);
        }

        private static object GetProperty(object target, string name)
        {
            Assert.That(target, Is.Not.Null);
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, "Missing property " + name + " on " + target.GetType().FullName + ".");
            return property.GetValue(target);
        }

        private static Type RequireType(string shortName)
        {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == EditorAssembly);
            Assert.That(assembly, Is.Not.Null, "Missing " + EditorAssembly + " assembly.");
            Type type = assembly.GetType(Namespace + shortName, false);
            Assert.That(type, Is.Not.Null, "Missing type " + Namespace + shortName + ".");
            return type;
        }
    }
}
