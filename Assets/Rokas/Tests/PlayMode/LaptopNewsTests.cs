using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Rokas.Tests
{
    public sealed class LaptopNewsTests
    {
        private GameObject root;
        private string directory;

        [UnityTest]
        public IEnumerator NewsSectionBuildsVideoArticleAndReturnsToDesktop()
        {
            directory = Path.Combine(Path.GetTempPath(), "rokas-news-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("LaptopNewsFixture");
            var boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory);
            yield return null;

            Press("LaptopHotspot");
            Press("LaptopNews");
            yield return null;

            Assert.That(Find("NewsHeroPoster"), Is.Not.Null);
            Assert.That(FindButton("NewsPlayPause"), Is.Not.Null);
            Assert.That(Find("NewsHeadline").GetComponent<Text>().text, Does.Contain("рамэн"));
            Assert.That(Find("NewsStory1"), Is.Not.Null);
            Assert.That(Find("NewsStory4"), Is.Not.Null);

            var player = root.GetComponentInChildren<VideoPlayer>();
            Assert.That(player, Is.Not.Null);
            Assert.That(player.url, Does.EndWith("YomiRamenNews.mp4"));

            Press("LaptopBack");
            yield return null;
            Assert.That(FindButton("LaptopNews"), Is.Not.Null);
            Assert.That(root.GetComponentInChildren<VideoPlayer>(), Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        private void Press(string name)
        {
            var button = FindButton(name);
            Assert.That(button.IsInteractable(), Is.True, name);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.True);
        }

        private Button FindButton(string name)
        {
            foreach (var button in root.GetComponentsInChildren<Button>())
                if (button.name == name) return button;
            Assert.Fail("Missing active interaction: " + name);
            return null;
        }

        private GameObject Find(string name)
        {
            foreach (var item in root.GetComponentsInChildren<Transform>())
                if (item.name == name) return item.gameObject;
            return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root) UnityEngine.Object.Destroy(root);
            yield return null;
            if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
