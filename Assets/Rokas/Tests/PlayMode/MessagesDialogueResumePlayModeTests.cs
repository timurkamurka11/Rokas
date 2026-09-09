using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class MessagesDialogueResumePlayModeTests
    {
        private GameObject root;
        private string directory;

        [UnityTest]
        public IEnumerator PartiallyProgressedDialogueResumesAtNextChoiceAfterReload()
        {
            Initialize();
            yield return null;

            Press("LaptopHotspot");
            Press("LaptopMessages");
            Press("MessagesContact_kaito");
            yield return WaitForButton("MessagesChoice_kaito_choice_details", 180);
            Press("MessagesChoice_kaito_choice_details");
            yield return WaitForButton("MessagesChoice_kaito_choice_guild", 180);
            Bootstrap().SaveNow();

            yield return DestroyAndRecreate();
            Press("LaptopHotspot");
            Press("LaptopMessages");
            yield return null;
            Press("MessagesContact_kaito");
            yield return WaitForButton("MessagesChoice_kaito_choice_guild", 180);

            Assert.That(FindButton("MessagesChoice_kaito_choice_details"), Is.Null,
                "persisted first choice must be replayed internally, not exposed again");
            Assert.That(FindButton("MessagesChoice_kaito_choice_skeptic"), Is.Null,
                "reload must resume at the next unresolved authored choice set");
            Assert.That(FindButton("MessagesChoice_kaito_choice_direct"), Is.Not.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RepeatedInitializeIsIdempotentForMessagesState()
        {
            Initialize();
            yield return null;

            RokasBootstrap boot = Bootstrap();
            Assert.That(boot.Session.Messages.DeliverIncoming(
                "repeat-bootstrap-mika-1", "mika", "Один раз."), Is.True);
            GameSession original = boot.Session;
            boot.Initialize(directory);

            Assert.That(boot.Session, Is.SameAs(original));
            ConversationState mika = boot.Session.Messages.GetConversation("mika");
            Assert.That(mika, Is.Not.Null);
            Assert.That(mika.entries.Count, Is.EqualTo(1));
            Assert.That(mika.unreadCount, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        private void Initialize()
        {
            directory = Path.Combine(Path.GetTempPath(), "rokas-messages-resume-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("MessagesDialogueResumeFixture");
            root.AddComponent<RokasBootstrap>().Initialize(directory);
        }

        private RokasBootstrap Bootstrap()
        {
            return root.GetComponent<RokasBootstrap>();
        }

        private IEnumerator DestroyAndRecreate()
        {
            UnityEngine.Object.Destroy(root);
            root = null;
            yield return null;
            root = new GameObject("MessagesDialogueResumeFixtureReloaded");
            root.AddComponent<RokasBootstrap>().Initialize(directory);
            yield return null;
        }

        private IEnumerator WaitForButton(string name, int frames)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                if (FindButton(name) != null) yield break;
                yield return null;
            }
            Assert.Fail("Timed out waiting for active interaction: " + name);
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
            if (root == null) return null;
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                if (button.name == name && button.gameObject.activeInHierarchy) return button;
            return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            yield return null;
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
