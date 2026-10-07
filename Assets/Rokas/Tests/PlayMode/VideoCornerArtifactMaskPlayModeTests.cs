using System.Collections;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class VideoCornerArtifactMaskPlayModeTests
    {
        private GameObject root;
        private bool previousIgnoreFailingMessages;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            root = new GameObject("VideoCornerArtifactMaskFixture");
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartupAndStoryUseOneSharedMaskBelowFunctionalHint()
        {
            var presenter = root.AddComponent<VideoSequencePresenter>();

            presenter.PlayStartup("__missing_mask_startup__.mp4", 0f, () => { });
            GameObject startupSurface = Find("StartupVideoSurface");
            VideoCornerArtifactMask startupMask = FindMask();
            GameObject hint = Find("StartupSkipHintOverlay");
            AssertMask(startupSurface, startupMask);
            Assert.That(hint, Is.Not.Null);
            Assert.That(startupSurface.transform.GetSiblingIndex(), Is.LessThan(startupMask.transform.GetSiblingIndex()));
            Assert.That(startupMask.transform.GetSiblingIndex(), Is.LessThan(hint.transform.GetSiblingIndex()),
                "Mask must sit above video pixels but below the functional skip hint.");
            presenter.Cancel();
            yield return null;

            presenter.PlayStoryIntro("__missing_mask_story__.mp4", 0f, () => { });
            GameObject storySurface = Find("StoryIntroVideoSurface");
            VideoCornerArtifactMask storyMask = FindMask();
            AssertMask(storySurface, storyMask);
            Assert.That(Find("StartupSkipHintOverlay"), Is.Null,
                "Story intro keeps its existing no-hint behavior while still masking the baked star.");
            presenter.Cancel();
            yield return null;
        }

        [UnityTest]
        public IEnumerator MainMenuUsesSameMaskBelowButtons()
        {
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            MainMenuView menu = MainMenuView.Create(root.transform, assets, 0f, null, () => { });
            yield return null;

            GameObject surface = Find("MainMenuVideoSurface");
            VideoCornerArtifactMask mask = FindMask();
            AssertMask(surface, mask);
            GameObject enter = Find("EnterWorldButton");
            Assert.That(enter, Is.Not.Null);
            Assert.That(surface.transform.GetSiblingIndex(), Is.LessThan(mask.transform.GetSiblingIndex()));
            Assert.That(mask.transform.GetSiblingIndex(), Is.LessThan(enter.transform.GetSiblingIndex()),
                "Menu controls must always render above the shared video artifact patch.");

            menu.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator GenericHostedVideoDoesNotMaskLaptopOrUnrelatedMedia()
        {
            var hostObject = new GameObject("HostedVideoRoot", typeof(RectTransform));
            hostObject.transform.SetParent(root.transform, false);
            var presenter = root.AddComponent<VideoSequencePresenter>();
            presenter.PlayInHost((RectTransform)hostObject.transform, "__missing_hosted_video__.mp4",
                "HostedVideoSurface", 1280, 720, 0f, () => { });

            Assert.That(Find("HostedVideoSurface"), Is.Not.Null);
            Assert.That(FindMask(), Is.Null,
                "Only StartupPreview/StoryIntro/MainMenuLoop should receive the fixed-position baked-artifact patch.");
            presenter.Cancel();
            yield return null;
        }

        private void AssertMask(GameObject surface, VideoCornerArtifactMask mask)
        {
            Assert.That(surface, Is.Not.Null);
            Assert.That(mask, Is.Not.Null);
            RawImage raw = mask.Image;
            Assert.That(raw, Is.Not.Null);
            Assert.That(raw.texture, Is.SameAs(surface.GetComponent<RawImage>().texture));
            Assert.That(raw.raycastTarget, Is.False);
            Assert.That(raw.material, Is.Not.Null);
            Assert.That(raw.material.shader.name, Is.EqualTo("ROKAS/UI/VideoCornerCloneMask"));

            RectTransform rect = (RectTransform)mask.transform;
            Assert.That(rect.anchorMin.x, Is.EqualTo(VideoCornerArtifactMask.NormalizedAnchor.x).Within(.0001f));
            Assert.That(rect.anchorMin.y, Is.EqualTo(VideoCornerArtifactMask.NormalizedAnchor.y).Within(.0001f));
            Assert.That(rect.anchorMax, Is.EqualTo(rect.anchorMin));
            Assert.That(rect.sizeDelta, Is.EqualTo(VideoCornerArtifactMask.ReferenceSize));
            Assert.That(mask.SampleRect.x + mask.SampleRect.z, Is.LessThan(.88f),
                "Clone source must sample left of the baked star rather than copying the star back onto itself.");
        }

        private VideoCornerArtifactMask FindMask()
        {
            if (root == null) return null;
            foreach (VideoCornerArtifactMask mask in root.GetComponentsInChildren<VideoCornerArtifactMask>(true))
                return mask;
            return null;
        }

        private GameObject Find(string name)
        {
            if (root == null) return null;
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                if (item.name == name) return item.gameObject;
            return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null;
            yield return null;
        }
    }
}
