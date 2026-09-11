using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class MainMenuInteractionAudioTransitionPlayModeTests
    {
        private GameObject root;
        private string directory;
        private string storyMediaPath;
        private string hiddenStoryMediaPath;
        private bool previousIgnoreFailingMessages;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            yield return null;
        }

        [UnityTest]
        public IEnumerator MainMenuButtonsReuseLaptopHoverFeedbackAndReturnSmoothly()
        {
            root = new GameObject("MainMenuHoverFixture");
            EnsureEventSystem(root.transform);
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            MainMenuView.Create(root.transform, assets, 0f, null, () => { });
            yield return null;

            foreach (string name in new[] { "EnterWorldButton", "DevelopersButton", "SupportDevelopmentButton" })
            {
                Button button = FindButton(name);
                Assert.That(button, Is.Not.Null, "Missing menu button: " + name);
                LaptopTileFeedback feedback = button.GetComponent<LaptopTileFeedback>();
                Assert.That(feedback, Is.Not.Null,
                    name + " must reuse the proven LaptopTileFeedback hover/selection easing.");

                var pointer = new PointerEventData(EventSystem.current);
                feedback.OnPointerEnter(pointer);
                yield return null;
                yield return null;
                yield return null;
                float hoveredScale = button.transform.localScale.x;
                Assert.That(hoveredScale, Is.GreaterThan(1.001f));
                Assert.That(hoveredScale, Is.LessThanOrEqualTo(1.04f));

                feedback.OnPointerExit(pointer);
                Assert.That(button.transform.localScale.x, Is.GreaterThan(1.001f),
                    "Pointer exit must not snap directly to scale 1.0.");
                for (int i = 0; i < 24; i++) yield return null;
                Assert.That(button.transform.localScale.x, Is.EqualTo(1f).Within(.01f),
                    "Menu hover must ease smoothly back to the original verified layout scale.");
            }
        }

        [UnityTest]
        public IEnumerator SecondaryButtonsUseGenericClickAndRemainNoOps()
        {
            root = new GameObject("MainMenuSecondaryAudioFixture");
            EnsureEventSystem(root.transform);
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            int genericClicks = 0;
            int entered = 0;
            MainMenuView.Create(root.transform, assets, 0f, () => genericClicks++, () => entered++);
            yield return null;

            Press(FindButton("DevelopersButton"));
            Press(FindButton("SupportDevelopmentButton"));
            yield return null;

            Assert.That(genericClicks, Is.EqualTo(2), "About and Support must use the existing generic outside-Laptop click path.");
            Assert.That(entered, Is.EqualTo(0), "Secondary buttons remain safe no-ops.");
            Assert.That(Find("RokasMainMenu"), Is.Not.Null);
        }

        [Test]
        public void GenericAndLaptopAssetsUseApprovedIndependentClips()
        {
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            Assert.That(assets.click, Is.Not.Null);
            Assert.That(assets.laptopMouseClick, Is.Not.Null);
            Assert.That(assets.click.name, Is.EqualTo("Button click"),
                "The existing generic outside-Laptop click path must be wired to the approved Button click.wav.");
            Assert.That(assets.laptopMouseClick.name, Is.EqualTo("LaptopMouseClick"),
                "Laptop must retain its pre-existing mouse click asset.");
            Assert.That(assets.laptopMouseClick, Is.Not.SameAs(assets.click),
                "Approved generic UI audio must never replace the Laptop mouse click asset.");
        }

        [UnityTest]
        public IEnumerator EnterWorldSkipsGenericClickAndConsumesRepeatedClicks()
        {
            root = new GameObject("MainMenuEnterGuardFixture");
            EnsureEventSystem(root.transform);
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            int genericClicks = 0;
            int enterRequests = 0;
            MainMenuView.Create(root.transform, assets, 0f, () => genericClicks++, () => enterRequests++);
            yield return null;

            Button enter = FindButton("EnterWorldButton");
            Assert.That(enter, Is.Not.Null);
            Press(enter);
            Press(enter);

            Assert.That(genericClicks, Is.EqualTo(0),
                "Enter World is a special override and must not stack the generic Button click with Enter game.");
            Assert.That(enterRequests, Is.EqualTo(1),
                "The transition guard must consume repeated clicks immediately.");
        }

        [UnityTest]
        public IEnumerator RealEnterWorldHandsHomeOffOnlyAtFullBlackThenFadesIn()
        {
            HideStoryMediaForDeterministicLinuxFallback();
            RokasBootstrap boot = CreateRealLaunchIgnoringHostDecoderErrors();
            yield return WaitFor("RokasMainMenu", 2f);

            Button enter = FindButton("EnterWorldButton");
            Press(enter);

            CanvasGroup curtain = FindCanvasGroup("EnterWorldCurtain");
            Assert.That(curtain, Is.Not.Null,
                "Enter World must start the proven CanvasGroup black-fade transition instead of cutting directly to Home.");
            Assert.That(Find("RokasMainMenu"), Is.Not.Null, "Menu must remain visible beneath the first fade frames.");
            Assert.That(Find("HomeTitle"), Is.Null, "Home must not flash before full black.");

            yield return new WaitForSecondsRealtime(.10f);
            Assert.That(curtain.alpha, Is.GreaterThan(.05f).And.LessThan(.95f),
                "Transition must expose a real partial-darkening frame.");
            Assert.That(Find("HomeTitle"), Is.Null, "State switch is forbidden during partial fade-to-black.");

            float deadline = Time.realtimeSinceStartup + 1f;
            while (Find("HomeTitle") == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Find("HomeTitle"), Is.Not.Null, "Home handoff did not occur.");
            Assert.That(curtain.alpha, Is.GreaterThanOrEqualTo(.99f),
                "Existing Home must be constructed while the transition is fully black.");
            Assert.That(Find("RokasMainMenu"), Is.Null,
                "Main Menu video/audio owner must be cleaned at the full-black handoff.");

            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(curtain.alpha, Is.GreaterThan(.05f).And.LessThan(.95f),
                "Home must fade in through real intermediate frames rather than appear abruptly.");

            deadline = Time.realtimeSinceStartup + 1f;
            while (Find("EnterWorldCurtain") != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Find("EnterWorldCurtain"), Is.Null);
            Assert.That(Find("HomeTitle"), Is.Not.Null, "Home must remain stable after transition cleanup.");
            Assert.That(boot.View, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator RealEnterWorldUsesApprovedPersistentSpecialAudioOnlyOnce()
        {
            HideStoryMediaForDeterministicLinuxFallback();
            RokasBootstrap boot = CreateRealLaunchIgnoringHostDecoderErrors();
            yield return WaitFor("RokasMainMenu", 2f);

            Button enter = FindButton("EnterWorldButton");
            Press(enter);
            Press(enter);
            yield return null;

            AudioSource special = FindAudioSourceByClipName("Enter game");
            Assert.That(special, Is.Not.Null,
                "Enter World must play the approved Enter game.wav through the persistent RokasAudio owner.");
            Assert.That(special.isPlaying, Is.True);
            Assert.That(CountAudioSourcesByClipName("Enter game"), Is.EqualTo(1),
                "Repeated Enter input must not stack duplicate Enter game playback.");
            Assert.That(FindAudioSourceByClipName("Button click"), Is.Null,
                "Enter World must not also play the generic Button click.wav.");

            yield return WaitFor("HomeTitle", 1f);
            Assert.That(special != null && special.isPlaying, Is.True,
                "Enter game SFX must survive Main Menu media cleanup and must not be truncated at the handoff.");
            Assert.That(Find("RokasMainMenu"), Is.Null);
            Assert.That(boot.View, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator OrdinaryHomeUiUsesApprovedGenericOutsideLaptopClick()
        {
            directory = Path.Combine(Path.GetTempPath(), "rokas-main-menu-audio-home-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("HomeGenericAudioFixture");
            EnsureEventSystem(root.transform);
            var boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory, false);
            yield return null;

            Press(FindButton("LampHotspot"));
            AudioSource generic = FindAudioSourceByClipName("Button click");
            Assert.That(generic, Is.Not.Null,
                "Ordinary Home UI outside Laptop must use the approved Button click.wav through the existing generic path.");
            Assert.That(generic.isPlaying, Is.True);
            Assert.That(FindAudioSourceByClipName("LaptopMouseClick"), Is.Null,
                "Outside-Laptop Home UI must not route through the Laptop mouse click asset.");
        }

        [UnityTest]
        public IEnumerator MainMenuAboutAndSupportPlayApprovedGenericClip()
        {
            HideStoryMediaForDeterministicLinuxFallback();
            CreateRealLaunchIgnoringHostDecoderErrors();
            yield return WaitFor("RokasMainMenu", 2f);

            Press(FindButton("DevelopersButton"));
            Assert.That(FindAudioSourceByClipName("Button click"), Is.Not.Null,
                "About must use the approved generic outside-Laptop Button click.");

            Press(FindButton("SupportDevelopmentButton"));
            Assert.That(FindAudioSourceByClipName("Button click"), Is.Not.Null,
                "Support must use the approved generic outside-Laptop Button click.");
            Assert.That(Find("RokasMainMenu"), Is.Not.Null);
            Assert.That(Find("HomeTitle"), Is.Null);
        }

        private void HideStoryMediaForDeterministicLinuxFallback()
        {
            storyMediaPath = Path.Combine(Application.streamingAssetsPath, "RokasVideo", "StoryIntro.mp4");
            Assert.That(File.Exists(storyMediaPath), Is.True);
            hiddenStoryMediaPath = Path.Combine(Path.GetTempPath(), "rokas-hidden-story-audio-transition-" + Guid.NewGuid().ToString("N") + ".mp4");
            File.Move(storyMediaPath, hiddenStoryMediaPath);
        }

        private RokasBootstrap CreateRealLaunchIgnoringHostDecoderErrors()
        {
            root = new GameObject("MainMenuInteractionAudioTransitionFixture");
            EnsureEventSystem(root.transform);
            return root.AddComponent<RokasBootstrap>();
        }

        private IEnumerator WaitFor(string objectName, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Find(objectName) == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Find(objectName), Is.Not.Null, "Timed out waiting for: " + objectName);
        }

        private static void EnsureEventSystem(Transform parent)
        {
            if (EventSystem.current) return;
            var events = new GameObject("TestEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            events.transform.SetParent(parent, false);
        }

        private void Press(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.IsInteractable(), Is.True);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.True);
        }

        private Button FindButton(string name)
        {
            if (root == null) return null;
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                if (button.name == name) return button;
            return null;
        }

        private GameObject Find(string name)
        {
            if (root == null) return null;
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                if (item.name == name) return item.gameObject;
            return null;
        }

        private CanvasGroup FindCanvasGroup(string name)
        {
            GameObject go = Find(name);
            return go ? go.GetComponent<CanvasGroup>() : null;
        }

        private AudioSource FindAudioSourceByClipName(string clipName)
        {
            if (root == null) return null;
            foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true))
                if (source.clip != null && source.clip.name == clipName && source.isPlaying) return source;
            return null;
        }

        private int CountAudioSourcesByClipName(string clipName)
        {
            int count = 0;
            if (root == null) return count;
            foreach (AudioSource source in root.GetComponentsInChildren<AudioSource>(true))
                if (source.clip != null && source.clip.name == clipName && source.isPlaying) count++;
            return count;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            if (root != null) UnityEngine.Object.Destroy(root);
            yield return null;
            if (!string.IsNullOrEmpty(hiddenStoryMediaPath) && File.Exists(hiddenStoryMediaPath) &&
                !string.IsNullOrEmpty(storyMediaPath) && !File.Exists(storyMediaPath))
                File.Move(hiddenStoryMediaPath, storyMediaPath);
            hiddenStoryMediaPath = null;
            storyMediaPath = null;
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) Directory.Delete(directory, true);
            directory = null;
        }
    }
}
