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
    public sealed class MessagesNotificationPlayModeTests
    {
        private GameObject root;
        private string directory;

        [UnityTest]
        public IEnumerator IncomingUnreadMessageShowsOneCompactNotification()
        {
            Initialize("rokas-message-notify-");
            yield return null;

            Assert.That(FindRect("MessagesNotification"), Is.Null,
                "notification surface must not exist before a new unread arrival");
            Assert.That(root.GetComponent<RokasBootstrap>().Session.Messages.DeliverIncoming(
                "notify-mika-1", "mika", "Проверь восточный вход."), Is.True);

            yield return null;
            Assert.That(FindRect("MessagesNotification"), Is.Not.Null,
                "a new unread message must render one compact notification");
            Assert.That(CountRects("MessagesNotification"), Is.EqualTo(1),
                "one arrival must create one notification surface");

            for (int frame = 0; frame < 5; frame++) yield return null;
            Assert.That(CountRects("MessagesNotification"), Is.EqualTo(1),
                "per-frame Tick must not duplicate the same notification");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ActiveConversationClearsUnreadWithoutNotification()
        {
            Initialize("rokas-message-notify-open-");
            yield return null;

            Press("LaptopHotspot");
            Press("LaptopMessages");
            yield return null;
            Press("MessagesContact_mika");
            yield return null;
            Assert.That(FindRect("MessagesNotification"), Is.Null);

            RokasBootstrap boot = root.GetComponent<RokasBootstrap>();
            Assert.That(boot.Session.Messages.DeliverIncoming(
                "notify-mika-open-1", "mika", "Я уже в открытом чате."), Is.True);
            yield return null;

            ConversationState mika = boot.Session.Messages.GetConversation("mika");
            Assert.That(mika, Is.Not.Null);
            Assert.That(mika.unreadCount, Is.EqualTo(0),
                "the currently open conversation must consume its incoming unread immediately");
            Assert.That(boot.Session.Messages.TotalUnread, Is.EqualTo(0),
                "active-chat arrival must not leave redundant global unread");
            Assert.That(FindRect("MessagesNotification"), Is.Null,
                "active-chat arrival must not produce a redundant notification popup");
            LogAssert.NoUnexpectedReceived();
        }

        private void Initialize(string prefix)
        {
            directory = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
            root = new GameObject("MessagesNotificationFixture");
            RokasBootstrap boot = root.AddComponent<RokasBootstrap>();
            boot.Initialize(directory);
        }

        private void Press(string name)
        {
            Button button = null;
            foreach (Button candidate in root.GetComponentsInChildren<Button>(true))
            {
                if (candidate.name == name && candidate.gameObject.activeInHierarchy)
                {
                    button = candidate;
                    break;
                }
            }
            Assert.That(button, Is.Not.Null, "Missing active interaction: " + name);
            Assert.That(button.IsInteractable(), Is.True, name);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            Assert.That(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler), Is.True);
        }

        private RectTransform FindRect(string name)
        {
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect.name == name && rect.gameObject.activeInHierarchy) return rect;
            }
            return null;
        }

        private int CountRects(string name)
        {
            int count = 0;
            foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect.name == name && rect.gameObject.activeInHierarchy) count++;
            }
            return count;
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
