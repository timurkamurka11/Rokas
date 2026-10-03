using System.Collections;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Rokas.Tests
{
    public sealed class ReactiveEmberLayerPlayModeTests
    {
        private static readonly string[] RequiredAtlases = {
            "PortalVapor", "NormalMist", "HeavyBurst", "ThrowEnergy",
            "ThrowImpact", "BlockBurst", "ClawImpact"
        };

        [Test]
        public void RequiredAtlasesHaveUsableAlphaAndSafeRuntimeImportSettings()
        {
            var shader = Resources.Load<Shader>("Combat/ReactiveCombatEmberFlipbook");
            Assert.That(shader, Is.Not.Null, "The genuine Ember exports need their runtime carrier shader.");
            Assert.That(shader.isSupported, Is.True, "The carrier must render on the active Unity graphics device.");
#if UNITY_EDITOR
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, "The flipbook shader must compile without errors.");
#endif
            foreach (string source in RequiredAtlases)
            {
                var atlas = Resources.Load<Texture2D>(ReactiveCombatEmberLayer.ResourceFolder + source);
                Assert.That(atlas, Is.Not.Null, source + " must be an actual imported EmberGen export.");
                Assert.That(atlas.width, Is.EqualTo(atlas.height), source + " uses a square 8 x 8 atlas.");
                Assert.That(atlas.width % 8, Is.Zero, source + " must divide into 64 equal frame cells.");
                Assert.That(atlas.width, Is.InRange(256, 2048), source + " must retain useful frame resolution within the realtime memory budget.");
                Assert.That(atlas.mipmapCount, Is.EqualTo(1), source + " mipmaps would mix neighbouring flipbook cells.");
                Assert.That(atlas.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(atlas.filterMode, Is.EqualTo(FilterMode.Bilinear));
#if UNITY_EDITOR
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(atlas)) as TextureImporter;
                Assert.That(importer, Is.Not.Null);
                Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput), source + " must retain exported alpha.");
                Assert.That(importer.alphaIsTransparency, Is.False, source + " premultiplied RGB must not receive colour dilation.");
                Assert.That(importer.sRGBTexture, Is.True);
                var standalone = importer.GetPlatformTextureSettings("Standalone");
                Assert.That(standalone.overridden, Is.True);
                Assert.That(standalone.format, Is.EqualTo(TextureImporterFormat.BC7), source + " requires an alpha-capable texture format.");
#endif
                AssertUsableAlpha(atlas, source);
            }
        }

        [UnityTest]
        public IEnumerator OneShotUsesOnlyTheManualPresentationClockAndRetires()
        {
            var root = new GameObject("EmberManualClockTest");
            var layer = new ReactiveCombatEmberLayer(root.transform, 30, "HeavyBurst");
            try
            {
                Assert.That(layer.SourceAtlas, Is.Not.Null);
                layer.Play(new Vector3(2f, 3f, 0f), Vector2.one, Color.white, .1f);
                var renderer = root.GetComponentInChildren<MeshRenderer>(true);
                Assert.That(layer.Visible, Is.True);
                float frame = renderer.sharedMaterial.GetFloat("_Frame");

                // A paused combat presentation must not age from Unity's wall clock.
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(layer.Visible, Is.True);
                Assert.That(renderer.sharedMaterial.GetFloat("_Frame"), Is.EqualTo(frame));
                layer.Tick(0f);
                layer.Tick(-1f);
                Assert.That(renderer.sharedMaterial.GetFloat("_Frame"), Is.EqualTo(frame));

                layer.Tick(.04f);
                Assert.That(layer.Visible, Is.True);
                Assert.That(renderer.sharedMaterial.GetFloat("_Frame"), Is.GreaterThan(frame));
                layer.Tick(.07f);
                Assert.That(layer.Visible, Is.False, "The impact carrier must retire after its presentation lifetime.");
                Assert.That(renderer.enabled, Is.False);
                layer.Tick(1f);
                Assert.That(layer.Visible, Is.False, "A retired impact must not restart on later ticks.");
            }
            finally { layer.Dispose(); Object.DestroyImmediate(root); }
        }

        [Test]
        public void HeldLoopPersistsUntilStoppedAndDoesNotReactivateOnTicks()
        {
            var root = new GameObject("EmberHeldLoopTest");
            var layer = new ReactiveCombatEmberLayer(root.transform, 30, "ThrowEnergy");
            try
            {
                Assert.That(layer.SourceAtlas, Is.Not.Null);
                Vector3 hand = new Vector3(1f, 2f, 0f);
                layer.Follow(hand, new Vector2(.5f, .7f), Color.cyan, .6f);
                var renderer = root.GetComponentInChildren<MeshRenderer>(true);
                layer.Tick(30f);
                Assert.That(layer.Visible, Is.True, "Preview can be held while the player decides when to confirm.");
                Assert.That(renderer.transform.position, Is.EqualTo(hand));

                Vector3 movedHand = hand + Vector3.right;
                layer.Follow(movedHand, new Vector2(.5f, .7f), Color.cyan, .6f);
                Assert.That(renderer.transform.position, Is.EqualTo(movedHand), "Charge must follow the actual weapon/hand transform.");
                layer.Stop();
                layer.Tick(30f);
                Assert.That(layer.Visible, Is.False, "Cancelled preview must leave no held energy carrier.");
                Assert.That(renderer.enabled, Is.False);
            }
            finally { layer.Dispose(); Object.DestroyImmediate(root); }
        }

        [Test]
        public void ReplayAfterStopStartsAtFirstFrameAndNewContactPoint()
        {
            var root = new GameObject("EmberReplayTest");
            var layer = new ReactiveCombatEmberLayer(root.transform, 30, "BlockBurst");
            try
            {
                Assert.That(layer.SourceAtlas, Is.Not.Null);
                layer.Play(Vector3.zero, Vector2.one, Color.white, .3f);
                layer.Tick(.15f);
                var renderer = root.GetComponentInChildren<MeshRenderer>(true);
                Assert.That(renderer.sharedMaterial.GetFloat("_Frame"), Is.GreaterThan(0f));
                layer.Stop();

                Vector3 nextContact = new Vector3(4f, 2f, -.1f);
                layer.Play(nextContact, Vector2.one * .6f, Color.cyan, .2f);
                Assert.That(layer.Visible, Is.True);
                Assert.That(renderer.sharedMaterial.GetFloat("_Frame"), Is.Zero, "A later contact must not inherit the previous burst's age.");
                Assert.That(renderer.transform.position, Is.EqualTo(nextContact));
                Assert.That(renderer.sharedMaterial.GetTexture("_MainTex"), Is.SameAs(layer.SourceAtlas));
                layer.Tick(.21f);
                Assert.That(layer.Visible, Is.False);
            }
            finally { layer.Dispose(); Object.DestroyImmediate(root); }
        }

        [Test]
        public void NewFollowStartsAtFirstFrameWithoutResettingAnAlreadyHeldLoop()
        {
            var root = new GameObject("EmberFollowRestartTest");
            var layer = new ReactiveCombatEmberLayer(root.transform, 30, "NormalMist");
            try
            {
                Assert.That(layer.SourceAtlas, Is.Not.Null);
                var renderer = root.GetComponentInChildren<MeshRenderer>(true);
                layer.Follow(Vector3.zero, Vector2.one, Color.white, 0f);
                layer.Tick(.45f);
                layer.Follow(Vector3.zero, Vector2.one, Color.white, .5f);
                Assert.That(renderer.sharedMaterial.GetFloat("_Frame"), Is.Zero,
                    "An unrelated idle interval must not choose the next swing's flipbook frame.");
                layer.Tick(.2f);
                float heldFrame = renderer.sharedMaterial.GetFloat("_Frame");
                layer.Follow(Vector3.right, Vector2.one, Color.white, .5f);
                Assert.That(renderer.sharedMaterial.GetFloat("_Frame"), Is.EqualTo(heldFrame),
                    "Updating a held weapon's transform must not restart its animation each frame.");
                layer.Stop();
                layer.Tick(.8f);
                layer.Follow(Vector3.right, Vector2.one, Color.white, .5f);
                Assert.That(renderer.sharedMaterial.GetFloat("_Frame"), Is.Zero);
            }
            finally { layer.Dispose(); Object.DestroyImmediate(root); }
        }

        [Test]
        public void MissingExportCannotShowAnOpaqueFallbackCarrier()
        {
            var root = new GameObject("EmberMissingExportTest");
            var layer = new ReactiveCombatEmberLayer(root.transform, 30, "__MissingEmberExportForRegression__");
            try
            {
                Assert.That(layer.SourceAtlas, Is.Null);
                layer.Play(Vector3.zero, Vector2.one, Color.white, .2f);
                layer.Follow(Vector3.zero, Vector2.one, Color.white, 1f);
                layer.Tick(.05f);
                Assert.That(layer.Visible, Is.False);
                Assert.That(root.GetComponentInChildren<MeshRenderer>(true).enabled, Is.False,
                    "Absent exported alpha must fail closed instead of drawing a rectangular replacement.");
            }
            finally { layer.Dispose(); Object.DestroyImmediate(root); }
        }

        private static void AssertUsableAlpha(Texture2D atlas, string source)
        {
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(atlas.width, atlas.height, 0, RenderTextureFormat.ARGB32);
            var sample = new Texture2D(atlas.width, atlas.height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(atlas, target);
                RenderTexture.active = target;
                sample.ReadPixels(new Rect(0, 0, atlas.width, atlas.height), 0, 0);
                sample.Apply(false, false);
                Color32[] pixels = sample.GetPixels32();
                int transparent = 0, visible = 0;
                byte maximumAlpha = 0;
                foreach (Color32 pixel in pixels)
                {
                    if (pixel.a <= 1) transparent++;
                    if (pixel.a > 8) visible++;
                    if (pixel.a > maximumAlpha) maximumAlpha = pixel.a;
                }
                TestContext.WriteLine(source + " " + atlas.width + "x" + atlas.height + " " + atlas.format +
                    " alphaMax=" + maximumAlpha + " visiblePixels=" + visible + " transparentPixels=" + transparent);
                Assert.That(maximumAlpha, Is.GreaterThan(32), source + " cannot be an empty export.");
                Assert.That(visible, Is.GreaterThan(pixels.Length / 1000), source + " must contain an effect beyond an isolated export origin point.");
                Assert.That(transparent, Is.GreaterThan(pixels.Length / 100), source + " must preserve transparent space around the effect.");
                // This gate checks imported data, not the artistic quality of the smoke or animation.
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(sample);
            }
        }
    }
}
