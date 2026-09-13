using System;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnUiWorkshopTestsRemoteEditorProof
    {
        [Test]
        public void RealEditorEntryPointBuildsAuthoredFramesAndVariantSurvivesWindowReinstantiation()
        {
            VnPresentationWorkshopWindow.Open();
            VnPresentationWorkshopWindow window = EditorWindow.GetWindow<VnPresentationWorkshopWindow>();
            Assert.That(window, Is.Not.Null, "The real ROKAS/VN UI Workshop entry point must open an EditorWindow.");
            Assert.That(window.titleContent.text, Is.EqualTo("VN UI Workshop"));
            Assert.That(window.CurrentPreset, Is.Not.Null);
            Assert.That(window.PreviewScene, Is.EqualTo(VnWorkshopPreviewScene.MinaBody));
            Assert.That(window.PreviewResolution, Is.EqualTo(VnWorkshopResolution.Reference1920x1080));
            Assert.That(window.ComparisonView, Is.EqualTo(VnWorkshopComparisonView.Current));

            string variantName = "CI_RemoteProof_" + Guid.NewGuid().ToString("N");
            VnPresentationWorkshopWindow reloaded = null;

            try
            {
                window.PreviewScene = VnWorkshopPreviewScene.PhoneMessage;
                window.PreviewResolution = VnWorkshopResolution.Wide1280x720;
                VnWorkshopPreviewFrame wide = window.BuildPreviewFrame();
                Assert.That(wide.ScreenSize, Is.EqualTo(new Vector2(1280f, 720f)));
                Assert.That(wide.BackgroundTexture, Is.Not.Null);
                Assert.That(wide.DialoguePanelTexture, Is.Not.Null);
                Assert.That(wide.Font, Is.Not.Null);
                Assert.That(wide.MinaTexture, Is.Not.Null);
                Assert.That(wide.ShowMina, Is.True);

                window.PreviewResolution = VnWorkshopResolution.FourThree1024x768;
                VnWorkshopPreviewFrame fourThree = window.BuildPreviewFrame();
                Assert.That(fourThree.ScreenSize, Is.EqualTo(new Vector2(1024f, 768f)));
                Assert.That(fourThree.BackgroundTexture, Is.SameAs(wide.BackgroundTexture));
                Assert.That(fourThree.DialoguePanelTexture, Is.SameAs(wide.DialoguePanelTexture));

                VnPresentationWorkshopEditing.SetPositionDelta(
                    window.CurrentPreset, VnWorkshopElement.MinaBody, new Vector2(14f, -6f));
                window.SaveCurrentVariant(variantName);
                Assert.That(window.ListSavedVariants(), Does.Contain(variantName));

                window.Close();
                window = null;

                reloaded = ScriptableObject.CreateInstance<VnPresentationWorkshopWindow>();
                VnWorkshopImportResult loaded = reloaded.LoadSavedVariant(variantName);
                Assert.That(loaded.Success, Is.True, loaded.Error);
                Assert.That(loaded.SourceHeadMismatch, Is.False);
                Assert.That(reloaded.CurrentPreset.minaBody.hasPositionDelta, Is.True);
                Assert.That(reloaded.CurrentPreset.minaBody.positionDelta, Is.EqualTo(new Vector2(14f, -6f)));
            }
            finally
            {
                try
                {
                    if (reloaded != null)
                        reloaded.DeleteSavedVariant(variantName);
                    else if (window != null)
                        window.DeleteSavedVariant(variantName);
                }
                catch { }

                if (reloaded != null) UnityEngine.Object.DestroyImmediate(reloaded);
                if (window != null) window.Close();
            }
        }
    }
}
