using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class HomeLivingGoldLinesPlayModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator OnlyDoorAndLaptopHaveLivingGoldAndHoverDoesNotMoveTheirContours()
        {
            var rootObject = new GameObject("HomeGoldLineTest", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler));
            var parent = rootObject.GetComponent<RectTransform>();
            parent.sizeDelta = new Vector2(1920f, 1080f);
            var eventRoot = new GameObject("HomeGoldLineEventSystem", typeof(EventSystem));

            try
            {
                Transform doorHit = MakeHit(parent, "DoorHotspot");
                Transform laptopHit = MakeHit(parent, "LaptopHotspot");
                RectTransform home = HomeFinalUiPresenter.Build(parent);
                Assert.That(home, Is.Not.Null, "The committed HomeFinalIcons must load.");

                Component door = FindOutline(home, "Door");
                Component laptop = FindOutline(home, "Laptop");
                Component cat = FindOutline(home, "Cat");

                Assert.That(Living(door), Is.True);
                Assert.That(Living(laptop), Is.True);
                Assert.That(Living(cat), Is.False, "Do not alter Cat outline FX.");
                Assert.That(((Graphic)door).raycastTarget, Is.False);
                Assert.That(((Graphic)laptop).raycastTarget, Is.False);

                // Check the actual motion equation, not merely the flags:
                // color/alpha must advance smoothly with time on both outlines.
                AssertLivingColorChanges(door);
                AssertLivingColorChanges(laptop);
                AssertMovingGlint(door);
                AssertMovingGlint(laptop);

                Vector2[] doorPoints = Points(door);
                Vector2[] laptopPoints = Points(laptop);
                Assert.That(doorPoints.Length, Is.EqualTo(8));
                Assert.That(laptopPoints.Length, Is.EqualTo(8));

                var pointer = new PointerEventData(eventRoot.GetComponent<EventSystem>());
                ExecuteEvents.Execute<IPointerEnterHandler>(doorHit.gameObject, pointer,
                    ExecuteEvents.pointerEnterHandler);
                Assert.That(Hovered(door), Is.True);
                Assert.That(Hovered(laptop), Is.False);
                yield return null;

                ExecuteEvents.Execute<IPointerExitHandler>(doorHit.gameObject, pointer,
                    ExecuteEvents.pointerExitHandler);
                ExecuteEvents.Execute<IPointerEnterHandler>(laptopHit.gameObject, pointer,
                    ExecuteEvents.pointerEnterHandler);
                Assert.That(Hovered(door), Is.False);
                Assert.That(Hovered(laptop), Is.True);
                yield return null;

                ExecuteEvents.Execute<IPointerExitHandler>(laptopHit.gameObject, pointer,
                    ExecuteEvents.pointerExitHandler);
                yield return null;
                Assert.That(Hovered(laptop), Is.False);

                CollectionAssert.AreEqual(doorPoints, Points(door),
                    "Living gold must not change any authored Door contour vertex.");
                CollectionAssert.AreEqual(laptopPoints, Points(laptop),
                    "Living gold must not change any authored Laptop contour vertex.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
                UnityEngine.Object.DestroyImmediate(eventRoot);
            }
        }

        private static Transform MakeHit(Transform parent, string id)
        {
            var go = new GameObject(id, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Component FindOutline(RectTransform root, string name)
        {
            Transform t = root.Find("Outlines/Outline_" + name);
            Assert.That(t, Is.Not.Null, "Missing authored " + name + " outline.");
            Graphic graphic = t.GetComponent<Graphic>();
            Assert.That(graphic, Is.Not.Null);
            return graphic;
        }

        private static void AssertLivingColorChanges(Component outline)
        {
            MethodInfo motion = outline.GetType().GetMethod("LivingColor", Private);
            Assert.That(motion, Is.Not.Null);
            var tint = new Color(.86f, .67f, .34f, .45f);
            Color before = (Color)motion.Invoke(outline, new object[]
                { tint, .31f, 1f, 0f });
            Color after = (Color)motion.Invoke(outline, new object[]
                { tint, .31f, 1f, 2f });
            float change = Mathf.Abs(before.g - after.g) +
                Mathf.Abs(before.a - after.a);
            Assert.That(change, Is.GreaterThan(.01f),
                "Continuous shimmer/pulse must actually change the rendered color.");
        }

        private static void AssertMovingGlint(Component outline)
        {
            MethodInfo cursorMethod = outline.GetType().GetMethod("LivingCursor", Private);
            MethodInfo glintMethod = outline.GetType().GetMethod("LivingGlintColor", Private);
            Assert.That(cursorMethod, Is.Not.Null);
            Assert.That(glintMethod, Is.Not.Null);
            float start = (float)cursorMethod.Invoke(outline, new object[] { 0f });
            float later = (float)cursorMethod.Invoke(outline, new object[] { 1.5f });
            Assert.That(Mathf.Abs(start - later), Is.GreaterThan(.19f),
                "Glint must visibly progress around the perimeter over 1.5 seconds.");
            Color peak = (Color)glintMethod.Invoke(outline,
                new object[] { start, 0f, true });
            Color away = (Color)glintMethod.Invoke(outline,
                new object[] { start + .31f, 0f, true });
            Color travelled = (Color)glintMethod.Invoke(outline,
                new object[] { start, 1.5f, true });
            Assert.That(peak.a, Is.GreaterThan(.90f),
                "A bright near-white head must be visible above the saturated gold core.");
            Assert.That(peak.g, Is.GreaterThan(.95f));
            Assert.That(away.a, Is.LessThan(.001f));
            Assert.That(travelled.a, Is.LessThan(.001f),
                "The bright spot must leave the original location instead of blinking in place.");
        }

        private static bool Living(Component outline)
        {
            FieldInfo field = outline.GetType().GetField("livingGold", Private);
            Assert.That(field, Is.Not.Null);
            return (bool)field.GetValue(outline);
        }

        private static bool Hovered(Component outline)
        {
            PropertyInfo property = outline.GetType().GetProperty("Hovered",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            return (bool)property.GetValue(outline);
        }

        private static Vector2[] Points(Component outline)
        {
            FieldInfo dataField = outline.GetType().GetField("data", Private);
            Assert.That(dataField, Is.Not.Null);
            object source = dataField.GetValue(outline);
            Assert.That(source, Is.Not.Null);
            FieldInfo pointsField = source.GetType().GetField("points",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(pointsField, Is.Not.Null);
            return (Vector2[])((Vector2[])pointsField.GetValue(source)).Clone();
        }
    }
}
