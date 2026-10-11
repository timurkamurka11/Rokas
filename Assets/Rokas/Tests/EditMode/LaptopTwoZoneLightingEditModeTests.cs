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
            return root;
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
            Texture2D approvedPhoto = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            image.texture = approvedPhoto;
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
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(approvedPhoto);
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
                Object.DestroyImmediate(root);
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
            image.texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
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
                Object.DestroyImmediate(image.texture);
                Object.DestroyImmediate(root);
            }
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
                Object.DestroyImmediate(root);
            }
        }
    }
}
