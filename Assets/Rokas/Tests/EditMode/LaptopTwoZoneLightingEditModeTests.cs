using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class LaptopTwoZoneLightingEditModeTests
    {
        private static GameObject CreateBackground(out RawImage image)
        {
            var root = new GameObject("OriginalLaptopPOV",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            image = root.GetComponent<RawImage>();
            // Bind guards require the same dimensions as the approved POV.
            image.texture = new Texture2D(
                LaptopCinematicLightPresentation.ReferenceWidth,
                LaptopCinematicLightPresentation.ReferenceHeight,
                TextureFormat.RGBA32, false);
            return root;
        }

        private static void DestroyBackground(GameObject root)
        {
            var image = root.GetComponent<RawImage>();
            if (image && image.texture) Object.DestroyImmediate(image.texture);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void ShaderExistsAndUsesTwoIndependentSavedSwitchProperties()
        {
            Shader shader = Resources.Load<Shader>(
                LaptopCinematicLightPresentation.ShaderResource);
            Assert.That(shader, Is.Not.Null,
                "Laptop lighting shader must be included in Resources, not an editor-only import.");
            Material material = new Material(shader);
            try
            {
                Assert.That(material.HasProperty("_RoomLightOn"), Is.True);
                Assert.That(material.HasProperty("_HallwayLightOn"), Is.True);
                Assert.That(material.HasProperty("_ScreenGlow"), Is.True);
                Assert.That(material.HasProperty("_ScreenBounceTint"), Is.True);
                Assert.That(material.HasProperty("_ScreenBounceAmount"), Is.True);
                Assert.That(material.HasProperty("_BounceMasksTex"), Is.True);
                Assert.That(material.HasProperty("_BloomMasksTex"), Is.True);
                Assert.That(material.HasProperty("_ScreenBloomAmount"), Is.True);
                Assert.That(material.HasProperty("_ScreenSpecAmount"), Is.True);
                Assert.That(material.HasProperty("_ScreenBounceSide"), Is.True);
                Assert.That(material.HasProperty("_BloomThreshold"), Is.True);
                Assert.That(material.HasProperty("_BloomSoftKnee"), Is.True);
                Assert.That(material.HasProperty("_OffTex"), Is.True);
                Assert.That(material.HasProperty("_OffPhotoReady"), Is.True);
                Assert.That(material.HasProperty("_MaskDebug"), Is.True);
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void AllFourLightingCombinationsBindImmediatelyWithOriginalPOVTexture()
        {
            bool roomOn = false;
            bool hallwayOn = false;
            GameObject root = CreateBackground(out RawImage image);
            Texture2D approvedPhoto = (Texture2D)image.texture;
            var presentation = new LaptopCinematicLightPresentation(
                () => roomOn, () => hallwayOn);
            try
            {
                presentation.Bind(image);
                Assert.That(image.texture, Is.SameAs(approvedPhoto));
                Assert.That(image.material, Is.SameAs(presentation.Material));
                foreach (bool room in new[] { false, true })
                    foreach (bool hall in new[] { false, true })
                    {
                        roomOn = room;
                        hallwayOn = hall;
                        presentation.Snap(); // also verifies no entry-frame flicker
                        Assert.That(presentation.RoomValue, Is.EqualTo(room ? 1f : 0f));
                        Assert.That(presentation.HallwayValue, Is.EqualTo(hall ? 1f : 0f));
                        Assert.That(presentation.Material.GetFloat("_RoomLightOn"),
                            Is.EqualTo(room ? 1f : 0f));
                        Assert.That(presentation.Material.GetFloat("_HallwayLightOn"),
                            Is.EqualTo(hall ? 1f : 0f));
                        Assert.That(image.texture, Is.SameAs(approvedPhoto),
                            "Never replace/retouch source POV PNG.");
                    }
            }
            finally
            {
                presentation.Dispose();
                DestroyBackground(root);
            }
        }

        [Test]
        public void CorridorChangesDoNotToggleRoom_AndReenterStartsFromSavedState()
        {
            bool room = false, hall = true;
            GameObject root = CreateBackground(out RawImage image);
            var presenter = new LaptopCinematicLightPresentation(() => room, () => hall);
            try
            {
                presenter.Bind(image);
                Assert.That(presenter.RoomValue, Is.EqualTo(0f));
                Assert.That(presenter.HallwayValue, Is.EqualTo(1f));

                hall = false;
                presenter.Tick(.075f);
                Assert.That(presenter.RoomValue, Is.EqualTo(0f));
                Assert.That(presenter.HallwayValue, Is.GreaterThan(0f).And.LessThan(1f));
                presenter.Tick(2f);
                Assert.That(presenter.HallwayValue, Is.EqualTo(0f).Within(.001f));

                room = true;
                presenter.Tick(2f);
                Assert.That(presenter.RoomValue, Is.EqualTo(1f).Within(.001f));
                Assert.That(presenter.HallwayValue, Is.EqualTo(0f));
                Material prior = presenter.Material;

                // Re-enter without applying a stale 'room OFF' light flash.
                presenter.Bind(image);
                Assert.That(presenter.Material, Is.Not.SameAs(prior));
                Assert.That(presenter.RoomValue, Is.EqualTo(1f));
                Assert.That(presenter.HallwayValue, Is.EqualTo(0f));
            }
            finally
            {
                presenter.Dispose();
                DestroyBackground(root);
            }
        }


        [Test]
        public void PhotographicOffReferenceIsFullResolutionAndAvailable()
        {
            Texture2D photo=Resources.Load<Texture2D>(
                LaptopCinematicLightPresentation.OffPhotoResource);
            Assert.That(photo, Is.Not.Null,
                "Install the supplied 1672x941 OFF PNG in Resources/LaptopCinematic first.");
            Assert.That(photo.width,Is.EqualTo(LaptopCinematicLightPresentation.ReferenceWidth));
            Assert.That(photo.height,Is.EqualTo(LaptopCinematicLightPresentation.ReferenceHeight));
        }

        [Test]
        public void MaterialActuallySamplesTheSeparateOffReferencePhoto()
        {
            bool room=false,hall=false;
            GameObject root=CreateBackground(out RawImage image);
            // Full-sized approved ON reference is supplied by CreateBackground().
            var presenter=new LaptopCinematicLightPresentation(()=>room,()=>hall);
            try
            {
                presenter.Bind(image);
                Assert.That(presenter.HasOffPhoto,Is.True,
                    "Missing photographic OFF asset: old one-photo shader cannot switch lights.");
                Assert.That(presenter.Material.GetFloat("_OffPhotoReady"),Is.EqualTo(1f));
                Texture2D reference=Resources.Load<Texture2D>(
                    LaptopCinematicLightPresentation.OffPhotoResource);
                Assert.That(presenter.Material.GetTexture("_OffTex"),Is.SameAs(reference));
                Assert.That(image.material,Is.SameAs(presenter.Material));
                Assert.That(presenter.Material.GetTexture("_MainTex"),Is.Not.SameAs(reference),
                    "Only the OFF texture belongs to the separate OFF channel.");
                Assert.That(image.texture,Is.Not.SameAs(reference),
                    "Approved original photo must stay the ON reference.");

                hall=true;
                presenter.Snap();
                Assert.That(presenter.Material.GetFloat("_RoomLightOn"),Is.EqualTo(0f));
                Assert.That(presenter.Material.GetFloat("_HallwayLightOn"),Is.EqualTo(1f));
            }
            finally
            {
                presenter.Dispose();
                DestroyBackground(root);
            }
        }


        [Test]
        public void LaptopScreenEmissionUsesIndependentSavedPowerState()
        {
            bool room=false, hall=false, powered=false;
            GameObject root=CreateBackground(out RawImage image);
            var presenter=new LaptopCinematicLightPresentation(
                () => room, () => hall, () => powered);
            try
            {
                presenter.Bind(image);
                Assert.That(presenter.ScreenValue,Is.EqualTo(0f));
                Assert.That(presenter.Material.GetFloat("_ScreenGlow"),Is.EqualTo(0f));
                powered=true;
                presenter.Tick(3f);
                Assert.That(presenter.ScreenValue,Is.EqualTo(1f).Within(.001f));
                Assert.That(presenter.Material.GetFloat("_ScreenGlow"),Is.EqualTo(1f).Within(.001f));
                Assert.That(presenter.RoomValue,Is.EqualTo(0f));
                Assert.That(presenter.HallwayValue,Is.EqualTo(0f));
                room=true;
                presenter.Snap();
                Assert.That(presenter.ScreenValue,Is.EqualTo(1f));
                Assert.That(presenter.RoomValue,Is.EqualTo(1f));
                powered=false;
                presenter.Snap();
                Assert.That(presenter.ScreenValue,Is.EqualTo(0f));
                Assert.That(presenter.RoomValue,Is.EqualTo(1f));
            }
            finally
            {
                presenter.Dispose();
                DestroyBackground(root);
            }
        }


        [Test]
        public void HybridMaterialLoadsPackedSurfaceMasksAndReceivesScreenColor()
        {
            GameObject root=CreateBackground(out RawImage image);
            var presenter=new LaptopCinematicLightPresentation(
                () => false, () => false, () => true);
            try
            {
                Texture2D masks=Resources.Load<Texture2D>(
                    LaptopCinematicLightPresentation.MaskResource);
                Assert.That(masks,Is.Not.Null,
                    "V5 RGBA mask texture must be committed in the Resources folder.");
                Assert.That(masks.width,Is.EqualTo(256));
                Assert.That(masks.height,Is.EqualTo(144));
                presenter.Bind(image);
                Assert.That(presenter.Material,Is.Not.Null);
                Assert.That(presenter.Material.GetTexture("_BounceMasksTex"),
                    Is.SameAs(masks));
                Color expected=new Color(.22f,.30f,.37f,1f);
                presenter.SetScreenBounce(expected,.65f,.55f,.40f,.20f);
                Assert.That(presenter.Material.GetFloat("_ScreenBounceAmount"),
                    Is.EqualTo(.65f).Within(.0001f));
                Assert.That(presenter.Material.GetFloat("_ScreenBloomAmount"),
                    Is.EqualTo(.55f).Within(.0001f));
                Assert.That(presenter.Material.GetFloat("_ScreenSpecAmount"),
                    Is.EqualTo(.40f).Within(.0001f));
                Assert.That(presenter.Material.GetFloat("_ScreenBounceSide"),
                    Is.EqualTo(.20f).Within(.0001f));
                Texture2D opticalMasks=Resources.Load<Texture2D>(
                    LaptopCinematicLightPresentation.BloomMaskResource);
                Assert.That(opticalMasks,Is.Not.Null);
                Assert.That(opticalMasks.width,Is.EqualTo(256));
                Assert.That(opticalMasks.height,Is.EqualTo(144));
                Assert.That(presenter.Material.GetTexture("_BloomMasksTex"),
                    Is.SameAs(opticalMasks));
                Color actual=presenter.Material.GetColor("_ScreenBounceTint");
                Assert.That(actual.r,Is.EqualTo(expected.r).Within(.0001f));
                Assert.That(actual.g,Is.EqualTo(expected.g).Within(.0001f));
                Assert.That(actual.b,Is.EqualTo(expected.b).Within(.0001f));
            }
            finally
            {
                presenter.Dispose();
                DestroyBackground(root);
            }
        }

        [Test]
        public void DynamicBounceTracksBrightnessWithoutNeonColorCopy()
        {
            LaptopScreenBounceEstimator.EvaluateMeanScreenColor(
                new Color(.07f,.12f,.18f,1f),
                out Color darkTint,out float darkAmount);
            LaptopScreenBounceEstimator.EvaluateMeanScreenColor(
                new Color(.75f,.82f,.89f,1f),
                out Color brightTint,out float brightAmount);
            Assert.That(brightAmount,Is.GreaterThan(darkAmount));
            Assert.That(brightAmount,Is.LessThanOrEqualTo(.96f));
            Assert.That(darkAmount, Is.InRange(.10f, .34f),
                "A dark display should not produce the old bright blue wash.");
            Assert.That(darkTint.r,Is.GreaterThan(.07f),
                "Reflection tint is desaturated/neutralized, not raw UI RGB.");
            Assert.That(brightTint.b,Is.GreaterThan(darkTint.b));
        }

        [Test]
        public void BrightPassAndHighlightResponseAreContentDriven()
        {
            float dark = LaptopScreenBounceEstimator.BrightPass(.08f);
            float mid = LaptopScreenBounceEstimator.BrightPass(.58f);
            float white = LaptopScreenBounceEstimator.BrightPass(.99f);
            Assert.That(dark,Is.EqualTo(0f));
            Assert.That(mid,Is.GreaterThan(dark));
            Assert.That(white,Is.GreaterThan(mid));
            LaptopScreenBounceEstimator.EvaluateHighlightResponse(
                .10f, .01f, out float darkBloom, out float darkSpec);
            LaptopScreenBounceEstimator.EvaluateHighlightResponse(
                .78f, .26f, out float lightBloom, out float lightSpec);
            Assert.That(lightBloom,Is.GreaterThan(darkBloom));
            Assert.That(lightSpec,Is.GreaterThan(darkSpec));
            Assert.That(lightBloom,Is.InRange(0f,1f));
        }

        [Test]
        public void ScreenLuminanceSideBiasTracksDynamicYomiPanels()
        {
            Assert.That(LaptopScreenBounceEstimator.EvaluateSideBias(1f,1f),
                Is.EqualTo(0f).Within(.0001f));
            Assert.That(LaptopScreenBounceEstimator.EvaluateSideBias(10f,100f),
                Is.GreaterThan(0f));
            Assert.That(LaptopScreenBounceEstimator.EvaluateSideBias(100f,10f),
                Is.LessThan(0f));
            Assert.That(LaptopScreenBounceEstimator.EvaluateSideBias(0f,100f),
                Is.LessThanOrEqualTo(.30f));
        }

        [Test]
        public void DisposalClearsOnlyTheOwnedMaterial()
        {
            GameObject root = CreateBackground(out RawImage image);
            var presenter = new LaptopCinematicLightPresentation(() => false, () => true);
            try
            {
                presenter.Bind(image);
                Material own = presenter.Material;
                Assert.That(own, Is.Not.Null);
                presenter.Dispose();
                Assert.That(presenter.Material, Is.Null);
                Assert.That(image.material, Is.Not.SameAs(own));
                Assert.That(image.gameObject, Is.Not.Null);
            }
            finally
            {
                presenter.Dispose();
                DestroyBackground(root);
            }
        }
    }
}
