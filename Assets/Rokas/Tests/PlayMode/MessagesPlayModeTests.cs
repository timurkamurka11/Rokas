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
    public sealed class MessagesPlayModeTests
    {
        private GameObject root;
        private string directory;

        [UnityTest]
        public IEnumerator LaptopMessagesBuildsRealConversationShell()
        {
            directory = Path.Combine(Path.GetTempPath(), "rokas-messages-ui-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("MessagesUiFixture");
            var boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory);
            yield return null;

            Press("LaptopHotspot");
            Press("LaptopMessages");

            Assert.That(FindRect("MessagesRoot"), Is.Not.Null, "Messages must replace the laptop placeholder with a dedicated root.");
            Assert.That(FindRect("MessagesSearch"), Is.Not.Null, "Messages must expose a real search control.");
            Assert.That(FindRect("MessagesContactList"), Is.Not.Null, "Messages must render the contact list pane.");
            Assert.That(FindRect("MessagesHeader"), Is.Not.Null, "Messages must render the active-contact header.");
            Assert.That(FindRect("MessagesConversationViewport"), Is.Not.Null, "Messages must render a clipped conversation viewport.");
            Assert.That(FindButton("MessagesContact_kaito"), Is.Not.Null, "Kaito must be selectable from the real contact list.");
            LogAssert.NoUnexpectedReceived();
        }

        private void Press(string name)
        {
            Button button = FindButton(name);
            Assert.That(button, Is.Not.Null, "Missing active interaction: " + name);
            Assert.That(button.IsInteractable(), Is.True, name);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.True);
        }

        private Button FindButton(string name)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>())
            {
                if (button.name == name)
                {
                    return button;
                }
            }
            return null;
        }

        private RectTransform FindRect(string name)
        {
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>())
            {
                if (rect.name == name)
                {
                    return rect;
                }
            }
            return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }
            yield return null;
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
