using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rokas.Core;

namespace Rokas.Tests
{
    public sealed class MessagesThreeContactRosterTests
    {
        [Test]
        public void LaptopContactSearchShowsOnlyMinaYumikoAndGuild()
        {
            var service = new MessageService(new SaveData());
            CollectionAssert.AreEqual(
                new[] { "mina", "yumiko", "guild" },
                service.SearchContacts("").Select(contact => contact.id).ToArray());
            Assert.That(service.SearchContacts("kaito"), Is.Empty);
            Assert.That(service.SearchContacts("mika"), Is.Empty);
            Assert.That(service.SearchContacts("merchant"), Is.Empty);
            Assert.That(service.SearchContacts("unknown"), Is.Empty);
        }

        [Test]
        public void ExistingYumikoChatClearsOnlyOnceAndGuildHistorySurvives()
        {
            var state = new SaveData();
            state.messages = new MessageSaveData
            {
                rosterVersion = 0,
                deliveredEventIds = new List<string> { "old-yumiko-event" },
                conversations = new List<ConversationState>
                {
                    new ConversationState
                    {
                        contactId = "yumiko",
                        unreadCount = 2,
                        entries = new List<MessageEntry> { new MessageEntry { eventId = "old-yumiko-event", text = "Old chat" } }
                    },
                    new ConversationState
                    {
                        contactId = "guild",
                        entries = new List<MessageEntry> { new MessageEntry { eventId = "guild-event", text = "Contract" } }
                    },
                    new ConversationState
                    {
                        contactId = "mika",
                        unreadCount = 3,
                        entries = new List<MessageEntry> { new MessageEntry { eventId = "mika-event", text = "Hidden history" } }
                    }
                }
            };

            var service = new MessageService(state);
            Assert.That(state.messages.rosterVersion, Is.EqualTo(1));
            Assert.That(service.GetConversation("yumiko").entries, Is.Empty);
            Assert.That(service.GetConversation("yumiko").unreadCount, Is.Zero);
            Assert.That(service.GetConversation("guild").entries, Has.Count.EqualTo(1));
            Assert.That(service.GetConversation("mika").entries, Has.Count.EqualTo(1));
            Assert.That(service.TotalUnread, Is.Zero, "Hidden contacts must not generate notebook badges.");
            Assert.That(state.messages.deliveredEventIds, Does.Contain("old-yumiko-event"));

            Assert.That(service.DeliverIncoming("new-yumiko-event", "yumiko", "New chat"), Is.True);
            var reopened = new MessageService(state);
            Assert.That(reopened.GetConversation("yumiko").entries, Has.Count.EqualTo(1));
            Assert.That(reopened.GetConversation("yumiko").entries[0].text, Is.EqualTo("New chat"));
        }
    }
}
