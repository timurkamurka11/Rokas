using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnUiWorkshopTests
    {
        private const string EditorAssembly = "Rokas.Editor";
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";
        private const string ExpectedHead = "04a53a955286bb926365459ee11c6836ae490282";

        [Test]
        public void WorkshopEditorApiExistsAndPinsVerifiedSourceHead()
        {
            Type baseline = RequireType("VnPresentationWorkshopBaseline");
            FieldInfo sourceHead = baseline.GetField("SourceHead", BindingFlags.Public | BindingFlags.Static);
            Assert.That(sourceHead, Is.Not.Null, "Baseline must expose immutable SourceHead.");
            Assert.That(sourceHead.GetRawConstantValue(), Is.EqualTo(ExpectedHead));

            PropertyInfo referenceResolution = baseline.GetProperty("ReferenceResolution", BindingFlags.Public | BindingFlags.Static);
            Assert.That(referenceResolution, Is.Not.Null);
            Assert.That((Vector2)referenceResolution.GetValue(null), Is.EqualTo(new Vector2(1920f, 1080f)));

            RequireType("VnPresentationWorkshopPreset");
            RequireType("VnPresentationWorkshopResolver");
            RequireType("VnPresentationWorkshopStorage");
        }

        [Test]
        public void ResolverExposesProductionCanvasScalerMapping()
        {
            Type resolver = RequireType("VnPresentationWorkshopResolver");
            MethodInfo method = resolver.GetMethod("CalculateVirtualCanvasSize", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            Vector2 wide = (Vector2)method.Invoke(null, new object[] { 1280, 720 });
            Assert.That(wide.x, Is.EqualTo(1920f).Within(.01f));
            Assert.That(wide.y, Is.EqualTo(1080f).Within(.01f));

            Vector2 fourThree = (Vector2)method.Invoke(null, new object[] { 1024, 768 });
            float logWidth = Mathf.Log(1024f / 1920f, 2f);
            float logHeight = Mathf.Log(768f / 1080f, 2f);
            float scale = Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, .5f));
            Assert.That(fourThree.x, Is.EqualTo(1024f / scale).Within(.01f));
            Assert.That(fourThree.y, Is.EqualTo(768f / scale).Within(.01f));
        }

        [Test]
        public void BakedControlMetadataIsExplicitAndIndependentControlsStayIndependent()
        {
            Type baseline = RequireType("VnPresentationWorkshopBaseline");
            MethodInfo method = baseline.GetMethod("GetControlPresentationKind", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            Assert.That(method.Invoke(null, new object[] { "Mute" }), Is.EqualTo("Baked into panel"));
            Assert.That(method.Invoke(null, new object[] { "Pause" }), Is.EqualTo("Baked into panel"));
            Assert.That(method.Invoke(null, new object[] { "Skip" }), Is.EqualTo("Baked into panel"));
            Assert.That(method.Invoke(null, new object[] { "Back" }), Is.EqualTo("Independent"));
            Assert.That(method.Invoke(null, new object[] { "Next" }), Is.EqualTo("Independent"));
        }

        [Test]
        public void VariantsDirectoryIsProjectLocalLibraryPath()
        {
            Type storage = RequireType("VnPresentationWorkshopStorage");
            MethodInfo method = storage.GetMethod("GetVariantsDirectory", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            string path = (string)method.Invoke(null, new object[] { "/project/root" });
            string normalized = path.Replace('\\', '/');
            Assert.That(normalized, Does.EndWith("/project/root/Library/ROKAS/VnUiWorkshop/variants"));
            Assert.That(normalized, Does.Not.Contain("/Assets/"));
        }

        private static Type RequireType(string shortName)
        {
            Type type = Type.GetType(Namespace + shortName + ", " + EditorAssembly);
            Assert.That(type, Is.Not.Null, shortName + " is required by the approved Workshop design.");
            return type;
        }
    }
}
