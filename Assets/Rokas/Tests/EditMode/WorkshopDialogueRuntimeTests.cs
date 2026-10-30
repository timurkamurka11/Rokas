using System.Collections.Generic;
using NUnit.Framework;
using Rokas.Core;

namespace Rokas.Tests
{
    public sealed class WorkshopDialogueRuntimeTests
    {
        private static WorkshopDialogueRuntime Create(MessageService service)
        {
            return new WorkshopDialogueRuntime(service, new WorkshopCatalogue
            {
                contacts = new List<WorkshopContact>
                {
                    new WorkshopContact
                    {
                        contactId = "mina",
                        topics = new List<WorkshopTopic>
                        {
                            new WorkshopTopic
                            {
                                id = "greeting", title = "Hello topic", playerText = "Hello!",
                                enabled = true, repeatable = false, startNodeId = "start",
                                nodes = new List<WorkshopNode>
                                {
                                    new WorkshopNode
                                    {
                                        id = "start", text = "Hi!",
                                        choices = new List<WorkshopChoice>
                                        {
                                            new WorkshopChoice { id = "yes", text = "Yes", nextNodeId = "yesNode" },
                                            new WorkshopChoice { id = "no", text = "No", nextNodeId = "noNode" }
                                        }
                                    },
                                    new WorkshopNode { id = "yesNode", text = "Great!" },
                                    new WorkshopNode { id = "noNode", text = "Okay." }
                                }
                            }
                        }
                    },
                    new WorkshopContact { contactId = "guild" },
                    new WorkshopContact { contactId = "yumiko" }
                }
            });
        }

        [Test]
        public void EmptyCatalogueDoesNotInventGuildMessages()
        {
            var service = new MessageService(new SaveData());
            var dialogue = new WorkshopDialogueRuntime(service, new WorkshopCatalogue());
            Assert.That(dialogue.GetTopics("guild"), Is.Empty);
            Assert.That(dialogue.GetChoices("guild"), Is.Empty);
            Assert.That(service.GetConversation("guild"), Is.Null);
        }

        [Test]
        public void BranchChoicesSurviveReloadAndNonRepeatableDialogueFinishes()
        {
            var state = new SaveData();
            var service = new MessageService(state);
            var dialogue = Create(service);
            Assert.That(dialogue.StartTopic("mina", "greeting"), Is.True);
            Assert.That(dialogue.GetChoices("mina"), Has.Count.EqualTo(2));
            var resumed = Create(new MessageService(state));
            Assert.That(resumed.GetChoices("mina"), Has.Count.EqualTo(2));
            Assert.That(resumed.Choose("mina", "yes"), Is.True);
            Assert.That(resumed.GetChoices("mina"), Is.Empty);
            Assert.That(resumed.GetTopics("mina"), Is.Empty);
            Assert.That(state.messages.conversations.Find(c => c.contactId == "mina").entries, Has.Count.EqualTo(4));
        }
    }
}
