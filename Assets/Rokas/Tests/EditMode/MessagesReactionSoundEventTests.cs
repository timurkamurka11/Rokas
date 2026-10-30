using System;
using NUnit.Framework;
using Rokas.Core;

namespace Rokas.Tests
{
    public sealed class MessagesReactionSoundEventTests
    {
        [Test]
        public void ReactingToMinaAndNpcReactionBothPublishOneSoundEvent()
        {
            MessageService messages = new MessageService(new SaveData());
            int soundCues = 0;
            string observedContact = null;
            messages.ReactionApplied += (contact, messageId) =>
            {
                soundCues++;
                observedContact = contact;
                Assert.That(messageId, Is.Not.Empty);
            };

            Assert.That(messages.DeliverIncoming("test:mina:incoming", "mina", "Hello"), Is.True);
            ConversationState mina = messages.GetConversation("mina");
            string incoming = mina.entries[0].messageId;

            Assert.That(messages.SetReaction("mina", incoming, "reaction_heart"), Is.True);
            Assert.That(soundCues, Is.EqualTo(1));
            Assert.That(observedContact, Is.EqualTo("mina"));

            Assert.That(messages.SetReaction("mina", incoming, "reaction_heart"), Is.False);
            Assert.That(soundCues, Is.EqualTo(1));

            Assert.That(messages.SendOutgoing("test:mina:outgoing", "mina", "Hi", "", ""), Is.True);
            string outgoing = mina.entries[mina.entries.Count - 1].messageId;

            Assert.That(messages.SetNpcReaction("mina", outgoing, "reaction_love"), Is.True);
            Assert.That(soundCues, Is.EqualTo(2));
            Assert.That(messages.SetNpcReaction("mina", outgoing, "reaction_love"), Is.False);
            Assert.That(soundCues, Is.EqualTo(2));

            Assert.That(messages.SetReaction("mina", incoming, ""), Is.True);
            Assert.That(soundCues, Is.EqualTo(2));
            Assert.That(messages.SetReaction("mina", incoming, "reaction_wink"), Is.True);
            Assert.That(soundCues, Is.EqualTo(3));
        }
    }
}
