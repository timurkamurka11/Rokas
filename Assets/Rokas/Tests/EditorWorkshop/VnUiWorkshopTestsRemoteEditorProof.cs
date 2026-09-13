using System;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using Rokas.Presentation;
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

            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);

            string variantName = "CI_RemoteProof_" + Guid.NewGuid().ToString("N");
            VnPresentationWorkshopWindow reloaded = null;

            try
            {
                window.PreviewScene = VnWorkshopPreviewScene.PhoneMessage;
                window.PreviewResolution = VnWorkshopResolution.Wide1280x720;
                VnWorkshopPreviewFrame widePhone = window.BuildPreviewFrame();
                Assert.That(widePhone.ScreenSize, Is.EqualTo(new Vector2(1280f, 720f)));
                Assert.That(widePhone.BackgroundTexture, Is.SameAs(assets.vnBusStopPhoneMessageMina));
                Assert.That(widePhone.DialoguePanelTexture, Is.SameAs(assets.vnDialoguePanelMinaLight));
                Assert.That(widePhone.Font, Is.SameAs(assets.sans));
                Assert.That(widePhone.MinaTexture, Is.SameAs(assets.vnMinaCharacterSheet));
                Assert.That(widePhone.ShowMina, Is.False,
                    "PhoneMessage is an authored full-frame Mina scene and must not double-draw the character layer.");

                window.PreviewScene = VnWorkshopPreviewScene.MinaBody;
                VnWorkshopPreviewFrame wideMina = window.BuildPreviewFrame();
                Assert.That(wideMina.ShowMina, Is.True);
                Assert.That(wideMina.MinaTexture, Is.SameAs(assets.vnMinaCharacterSheet));

                window.PreviewResolution = VnWorkshopResolution.FourThree1024x768;
                VnWorkshopPreviewFrame fourThree = window.BuildPreviewFrame();
                Assert.That(fourThree.ScreenSize, Is.EqualTo(new Vector2(1024f, 768f)));
                Assert.That(fourThree.BackgroundTexture, Is.SameAs(assets.vnBusStopRainNight));
                Assert.That(fourThree.DialoguePanelTexture, Is.SameAs(assets.vnDialoguePanelMinaLight));
                Assert.That(fourThree.ShowMina, Is.True);

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
