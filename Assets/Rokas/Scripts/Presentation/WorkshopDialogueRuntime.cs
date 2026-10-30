using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rokas.Core
{
    // JSON authoring format; Unity JsonUtility-compatible (no dictionaries or polymorphism).
    [Serializable] public sealed class WorkshopCatalogue
    {
        public int version = 1;
        public List<WorkshopContact> contacts = new List<WorkshopContact>();
    }
    [Serializable] public sealed class WorkshopContact
    {
        public string contactId = "";
        public List<WorkshopTopic> topics = new List<WorkshopTopic>();
    }
    [Serializable] public sealed class WorkshopTopic
    {
        public string id = "";
        public string title = "";
        public string playerText = "";
        public bool enabled = true;
        public bool repeatable = true;
        public string startNodeId = "";
        public List<WorkshopNode> nodes = new List<WorkshopNode>();
    }
    [Serializable] public sealed class WorkshopNode
    {
        public string id = "";
        public string text = "";
        public string nextNodeId = ""; // automatic continuation if there are no choices
        public List<WorkshopChoice> choices = new List<WorkshopChoice>();
    }
    [Serializable] public sealed class WorkshopChoice
    {
        public string id = "";
        public string text = "";
        public string nextNodeId = ""; // empty means end of dialogue
    }
    public sealed class WorkshopTopicView
    {
        public readonly string id;
        public readonly string title;
        public WorkshopTopicView(string id, string title) { this.id = id; this.title = title; }
    }
    public sealed class WorkshopChoiceView
    {
        public readonly string id;
        public readonly string text;
        public WorkshopChoiceView(string id, string text) { this.id = id; this.text = text; }
    }

    // Runs authored conversation trees against the EXISTING MessageService/save system.
    // Does not modify the legacy LiveMessengerService/contract workflows.
    public sealed class WorkshopDialogueRuntime
    {
        private const string Prefix = "workshop:";
        private const string ActivePrefix = "workshop|";
        private readonly MessageService messages;
        private readonly WorkshopCatalogue catalogue;

        public WorkshopDialogueRuntime(MessageService service)
            : this(service, Load()) { }

        public WorkshopDialogueRuntime(MessageService service, WorkshopCatalogue data)
        {
            messages = service ?? throw new ArgumentNullException("service");
            catalogue = data ?? new WorkshopCatalogue();
        }

        public static WorkshopCatalogue Load()
        {
            TextAsset asset = Resources.Load<TextAsset>("Messages/Dialogue/WorkshopDialogues");
            if (asset == null || string.IsNullOrEmpty(asset.text)) return new WorkshopCatalogue();
            try { return JsonUtility.FromJson<WorkshopCatalogue>(asset.text) ?? new WorkshopCatalogue(); }
            catch (Exception ex) { Debug.LogError("ROKAS Dialogue Workshop: invalid JSON: " + ex.Message); return new WorkshopCatalogue(); }
        }

        public static bool Managed(string id)
        {
            return id == "mina" || id == "yumiko" || id == "guild";
        }

        private WorkshopContact FindContact(string id)
        {
            if (catalogue.contacts == null || !Managed(id)) return null;
            return catalogue.contacts.Find(c => c != null && c.contactId == id);
        }
        private WorkshopTopic FindTopic(string contact, string id)
        {
            WorkshopContact c = FindContact(contact);
            return c == null || c.topics == null ? null : c.topics.Find(t => t != null && t.id == id);
        }
        private static WorkshopNode FindNode(WorkshopTopic topic, string id)
        {
            return topic == null || topic.nodes == null || string.IsNullOrEmpty(id) ? null :
                topic.nodes.Find(n => n != null && n.id == id);
        }
        private static string Flow(string topic, string node) { return ActivePrefix + topic + "|" + node; }
        private static bool ParseFlow(string flow, out string topic, out string node)
        {
            topic = ""; node = "";
            if (string.IsNullOrEmpty(flow) || !flow.StartsWith(ActivePrefix, StringComparison.Ordinal)) return false;
            int split = flow.IndexOf('|', ActivePrefix.Length);
            if (split < 0) return false;
            topic = flow.Substring(ActivePrefix.Length, split - ActivePrefix.Length);
            node = flow.Substring(split + 1);
            return !string.IsNullOrEmpty(topic) && !string.IsNullOrEmpty(node);
        }
        private static string EventId() { return Prefix + Guid.NewGuid().ToString("N"); }
        private static string CompletedId(string topicId) { return Prefix + "completed:" + topicId; }

        public List<WorkshopTopicView> GetTopics(string contactId)
        {
            var result = new List<WorkshopTopicView>();
            WorkshopContact c = FindContact(contactId);
            if (c == null || c.topics == null) return result;
            ConversationState conversation = messages.GetConversation(contactId);
            for (int i = 0; i < c.topics.Count; i++)
            {
                WorkshopTopic t = c.topics[i];
                if (t == null || !t.enabled || string.IsNullOrWhiteSpace(t.id) ||
                    FindNode(t, t.startNodeId) == null) continue;
                if (!t.repeatable && conversation != null && conversation.completedDialogueIds != null &&
                    conversation.completedDialogueIds.Contains(CompletedId(t.id))) continue;
                result.Add(new WorkshopTopicView(t.id, string.IsNullOrWhiteSpace(t.title) ? t.playerText : t.title));
            }
            return result;
        }

        public List<WorkshopChoiceView> GetChoices(string contactId)
        {
            var result = new List<WorkshopChoiceView>();
            ConversationState c = messages.GetConversation(contactId);
            string topicId, nodeId;
            if (c == null || !ParseFlow(c.branchState, out topicId, out nodeId)) return result;
            WorkshopNode node = FindNode(FindTopic(contactId, topicId), nodeId);
            if (node == null || node.choices == null) return result;
            foreach (WorkshopChoice choice in node.choices)
                if (choice != null && !string.IsNullOrWhiteSpace(choice.id) && !string.IsNullOrWhiteSpace(choice.text))
                    result.Add(new WorkshopChoiceView(choice.id, choice.text));
            return result;
        }

        public bool StartTopic(string contactId, string topicId)
        {
            if (!Managed(contactId)) return false;
            WorkshopTopic topic = FindTopic(contactId, topicId);
            if (topic == null || !topic.enabled || FindNode(topic, topic.startNodeId) == null) return false;
            if (!GetTopics(contactId).Exists(t => t.id == topicId)) return false;
            if (GetChoices(contactId).Count > 0) return false; // finish current branch first
            messages.OpenConversation(contactId);
            ConversationState conversation = messages.GetConversation(contactId);
            conversation.branchState = Flow(topicId, topic.startNodeId);
            if (!messages.SendOutgoing(EventId(), contactId,
                string.IsNullOrWhiteSpace(topic.playerText) ? topic.title : topic.playerText,
                "", Prefix + topicId)) return false;
            Advance(contactId, topic, topic.startNodeId);
            return true;
        }

        public bool Choose(string contactId, string choiceId)
        {
            if (!Managed(contactId)) return false;
            ConversationState c = messages.GetConversation(contactId);
            string topicId, nodeId;
            if (c == null || !ParseFlow(c.branchState, out topicId, out nodeId)) return false;
            WorkshopTopic topic = FindTopic(contactId, topicId);
            WorkshopNode node = FindNode(topic, nodeId);
            if (node == null || node.choices == null) return false;
            WorkshopChoice choice = node.choices.Find(x => x != null && x.id == choiceId);
            if (choice == null) return false;
            if (!string.IsNullOrEmpty(choice.nextNodeId) && FindNode(topic, choice.nextNodeId) == null) return false;
            c.branchState = string.IsNullOrEmpty(choice.nextNodeId) ? "" : Flow(topicId, choice.nextNodeId);
            if (string.IsNullOrEmpty(choice.nextNodeId) && !topic.repeatable) Complete(c, topicId);
            if (!messages.SendOutgoing(EventId(), contactId, choice.text, choiceId, Prefix + topicId)) return false;
            if (!string.IsNullOrEmpty(choice.nextNodeId)) Advance(contactId, topic, choice.nextNodeId);
            return true;
        }

        private static void Complete(ConversationState c, string topicId)
        {
            if (c.completedDialogueIds == null) c.completedDialogueIds = new List<string>();
            if (!c.completedDialogueIds.Contains(CompletedId(topicId))) c.completedDialogueIds.Add(CompletedId(topicId));
        }

        private void Advance(string contactId, WorkshopTopic topic, string firstNodeId)
        {
            ConversationState c = messages.GetConversation(contactId);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            string current = firstNodeId;
            for (int depth = 0; depth < 64 && !string.IsNullOrEmpty(current); depth++)
            {
                // Protect against accidental designer-created auto loops.
                if (!visited.Add(current)) { c.branchState = ""; Debug.LogError("ROKAS Dialogue Workshop: automatic dialogue cycle"); return; }
                WorkshopNode n = FindNode(topic, current);
                if (n == null) { c.branchState = ""; return; }
                bool hasChoices = n.choices != null && n.choices.Count > 0;
                string next = hasChoices ? "" : n.nextNodeId;
                if (!string.IsNullOrEmpty(next) && FindNode(topic, next) == null) next = "";
                c.branchState = hasChoices ? Flow(topic.id, current) :
                    string.IsNullOrEmpty(next) ? "" : Flow(topic.id, next);
                if (!hasChoices && string.IsNullOrEmpty(next) && !topic.repeatable) Complete(c, topic.id);
                if (!string.IsNullOrWhiteSpace(n.text))
                    messages.DeliverIncoming(EventId(), contactId, n.text);
                if (hasChoices) return;
                current = next;
            }
            if (!string.IsNullOrEmpty(current)) { c.branchState = ""; Debug.LogError("ROKAS Dialogue Workshop: maximum chain length exceeded"); }
        }
    }
}