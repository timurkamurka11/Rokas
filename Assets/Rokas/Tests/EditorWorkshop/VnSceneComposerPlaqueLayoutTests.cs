using System;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerPlaqueLayoutTests
    {
        private static Type Ui { get { var t = typeof(VnSceneComposerProject).Assembly.GetType("Rokas.EditorTools.VnUiWorkshop.VnSceneComposerRuntimeUi"); Assert.That(t, Is.Not.Null, "Missing runtime plaque layout"); return t; } }
        private static object Static(string method, params object[] args) { var m = Ui.GetMethod(method); Assert.That(m, Is.Not.Null); return m.Invoke(null, args); }
        private static T Value<T>(object obj, string field) { return (T)obj.GetType().GetField(field).GetValue(obj); }
        private static VnWorkshopPreviewFrame Frame(VnWorkshopResolution resolution = VnWorkshopResolution.Reference1920x1080, string guid = null)
        {
            var p = new VnSceneComposerProject(); var s = new VnSceneComposerScene();
            if (guid != null) { s.presentationOverrides.dialoguePanelVisual.hasAssetGuid = true; s.presentationOverrides.dialoguePanelVisual.assetGuid = guid; }
            return VnSceneComposerComposition.BuildFrame(p, s, resolution, null);
        }
        [Test] public void DefaultResolvesApprovedBluePlaqueVariant() { Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(Frame().DialoguePanelTexture)), Is.EqualTo("08ac430bce3843b5a427ff44abe32a12")); }
        [Test] public void IdentifiedOriginalMapsToSafeCanonicalVariantWithoutRewritingProject() { Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(Frame(guid: "6bbf22755fe125c4482a7fd9e8f351ca").DialoguePanelTexture)), Is.EqualTo("08ac430bce3843b5a427ff44abe32a12")); }
        [Test] public void CustomPngStillOverridesCanonicalPlaque()
        {
            var texture = VnPresentationWorkshopPreviewRenderer.LoadAssets().vnDialoguePanelMinaLight;
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(texture));
            Assert.That(Frame(guid: guid).DialoguePanelTexture, Is.SameAs(texture));
        }
        [TestCase(VnWorkshopResolution.Reference1920x1080)]
        [TestCase(VnWorkshopResolution.Wide1280x720)]
        [TestCase(VnWorkshopResolution.FourThree1024x768)]
        public void ThreeControlsAreInsidePlaqueEquallySpacedAndClearOfText(VnWorkshopResolution resolution)
        {
            var f = Frame(resolution); object layout = Static("Layout", f);
            Rect mute=Value<Rect>(layout,"Mute"), forward=Value<Rect>(layout,"Forward"), menu=Value<Rect>(layout,"Menu"), triangle=Value<Rect>(layout,"Triangle");
            foreach (var r in new[] { mute, forward, menu, triangle })
            {
                Assert.That(f.DialoguePanel.Contains(r.min), Is.True);
                Assert.That(f.DialoguePanel.Contains(r.max - Vector2.one * .01f), Is.True);
                Assert.That(r.Overlaps(f.DialogueText), Is.False);
            }
            Assert.That(forward.center.x-mute.center.x, Is.EqualTo(menu.center.x-forward.center.x).Within(.01f));
            Assert.That(mute.Overlaps(forward) || forward.Overlaps(menu) || menu.Overlaps(triangle), Is.False);
            Assert.That(layout.GetType().GetField("Back"), Is.Null);
        }
        [Test] public void TriangleAnimationHasVisibleMovementScaleAndOpacityAndLoops()
        {
            object a=Static("SampleTriangle",0f), b=Static("SampleTriangle",.1875f), end=Static("SampleTriangle",.75f);
            Assert.That(Mathf.Abs(Value<float>(a,"OffsetY")-Value<float>(b,"OffsetY")), Is.GreaterThan(2f));
            Assert.That(Value<float>(a,"Scale"), Is.Not.EqualTo(Value<float>(b,"Scale")));
            Assert.That(Value<float>(a,"Alpha"), Is.Not.EqualTo(Value<float>(b,"Alpha")));
            Assert.That(Value<float>(a,"OffsetY"), Is.EqualTo(Value<float>(end,"OffsetY")).Within(.001f));
        }
        [Test] public void CompletionIndicatorIsBrightWhiteAndLargeEnoughToNotice()
        {
            var f = Frame();
            object layout = Static("Layout", f);
            Rect forward = Value<Rect>(layout, "Forward");
            Rect triangle = Value<Rect>(layout, "Triangle");
            Assert.That(triangle.width / forward.width, Is.GreaterThanOrEqualTo(.68f),
                "DialogueComplete indicator must be large enough to notice beside the plaque controls.");

            Color color = (Color)Static("CompletionIndicatorColor", .9f);
            Assert.That(color.r, Is.EqualTo(1f).Within(.0001f));
            Assert.That(color.g, Is.EqualTo(1f).Within(.0001f));
            Assert.That(color.b, Is.EqualTo(1f).Within(.0001f));
            Assert.That(color.a, Is.EqualTo(.9f).Within(.0001f));
        }

        [Test] public void PressAndHoverChangeVisualSampleNotAuthoredRect()
        {
            Rect original=new Rect(10,20,60,60);
            object normal=Static("SampleButton",original,false,false,true,1f);
            object hover=Static("SampleButton",original,true,false,true,1f);
            object pressed=Static("SampleButton",original,true,true,true,1f);
            object disabled=Static("SampleButton",original,false,false,false,1f);
            Assert.That(Value<float>(hover,"Brightness"), Is.GreaterThan(Value<float>(normal,"Brightness")));
            Assert.That(Value<Rect>(pressed,"Rect").width / original.width, Is.InRange(.94f,.98f));
            Assert.That(Value<float>(disabled,"Alpha"), Is.LessThan(Value<float>(normal,"Alpha")));
            Assert.That(original, Is.EqualTo(new Rect(10,20,60,60)));
        }
    }
}
