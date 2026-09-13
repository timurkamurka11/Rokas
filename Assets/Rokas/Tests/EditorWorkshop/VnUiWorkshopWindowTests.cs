using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnUiWorkshopWindowTests
    {
        private const string EditorAssembly = "Rokas.Editor";
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";
        private const string ExpectedMenuPath = "ROKAS/VN UI Workshop";

        [Test]
        public void EditorWindowContractUsesApprovedMenuAndHasNoProductionApplyAction()
        {
            Type window = Type.GetType(Namespace + "VnPresentationWorkshopWindow, " + EditorAssembly);
            Assert.That(window, Is.Not.Null, "The approved Workshop requires a user-facing EditorWindow.");
            Assert.That(typeof(EditorWindow).IsAssignableFrom(window), Is.True,
                "The Workshop entry point must be an editor-only EditorWindow.");

            FieldInfo menuPath = window.GetField("MenuPath", BindingFlags.Public | BindingFlags.Static);
            Assert.That(menuPath, Is.Not.Null, "The window must expose the approved menu path as an immutable constant.");
            Assert.That(menuPath.GetRawConstantValue(), Is.EqualTo(ExpectedMenuPath));

            MethodInfo open = window.GetMethod("Open", BindingFlags.Public | BindingFlags.Static);
            Assert.That(open, Is.Not.Null, "The Workshop must expose a static menu entry point.");
            MenuItem menuItem = open.GetCustomAttributes(typeof(MenuItem), false).Cast<MenuItem>().SingleOrDefault();
            Assert.That(menuItem, Is.Not.Null, "The Workshop Open method must be registered with Unity's MenuItem attribute.");
            Assert.That(menuItem.menuItem, Is.EqualTo(ExpectedMenuPath));

            bool hasProductionApply = window
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                .Any(method => method.Name.IndexOf("ApplyToProduction", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.That(hasProductionApply, Is.False,
                "The Workshop must never expose an ApplyToProduction action.");
        }
    }
}
