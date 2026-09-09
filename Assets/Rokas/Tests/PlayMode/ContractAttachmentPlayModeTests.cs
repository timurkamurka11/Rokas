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
    public sealed class ContractAttachmentPlayModeTests
    {
        private GameObject root;
        private string directory;

        [UnityTest]
        public IEnumerator GuildContractButtonAcceptsExistingContractOnce()
        {
            directory = Path.Combine(Path.GetTempPath(), "rokas-contract-ui-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("MessagesContractFixture");
            var boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory);
            yield return null;
            Press(FindButton("LaptopHotspot"));
            Press(FindButton("LaptopMessages"));
            Press(FindButton("MessagesContact_guild"));
            yield return null;

            var conversation = boot.Session.Messages.GetConversation("guild");
            Assert.That(conversation, Is.Not.Null, "Guild must deliver the real available contract card");
            var entry = conversation.entries.Find(item => item.attachment != null && item.attachment.kind == MessageAttachmentKind.Contract);
            Assert.That(entry, Is.Not.Null, "Guild history must contain a contract attachment");
            Assert.That(entry.attachment.targetId, Is.EqualTo(boot.Session.Contract.id));
            int count = conversation.entries.Count;
            var button = FindButton("MessagesAttachment_" + entry.attachment.id);
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Home));
            Press(button);
            Assert.That(boot.Session.State.phase, Is.EqualTo(RunPhase.Accepted), "real Button must invoke contract acceptance");
            Assert.That(boot.Session.State.activeContractId, Is.EqualTo(boot.Session.Contract.id));
            Assert.That(entry.attachment.opened, Is.True);
            // A queued second click on the same real Button must be harmless before rebuilding.
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            yield return null;
            Assert.That(conversation.entries.Count, Is.EqualTo(count + 1),
                "one successful acceptance must append exactly one Guild follow-up");
            Assert.That(FindButton("MessagesAttachment_" + entry.attachment.id).IsInteractable(), Is.False);
            Press(FindButton("MessagesContact_kaito"));
            Press(FindButton("MessagesContact_guild"));
            yield return null;
            Assert.That(conversation.entries.Count, Is.EqualTo(count + 1), "reopening Guild cannot duplicate delivery");
            Assert.That(FindButton("MessagesAttachment_" + entry.attachment.id).IsInteractable(), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        private Button FindButton(string name)
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
                if (button.name == name && button.gameObject.activeInHierarchy) return button;
            return null;
        }

        private static void Press(Button button)
        {
            Assert.That(button, Is.Not.Null, "Missing active interaction");
            Assert.That(button.IsInteractable(), Is.True);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.True);
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
