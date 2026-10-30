#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Rokas.Core;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools
{
    public sealed class MessagesDialogueWorkshopWindow : EditorWindow
    {
        private const string FilePath = "Assets/Rokas/Resources/Messages/Dialogue/WorkshopDialogues.json";
        private static readonly string[] Ids = { "mina", "yumiko", "guild" };
        private static readonly string[] Labels = { "Mina", "Yumiko", "Guild / RH" };
        private WorkshopCatalogue catalog;
        private int contact, topic = -1, node = -1;
        private Vector2 leftScroll, rightScroll, previewScroll;
        private bool dirty, preview;
        private string previewNode = "";
        private readonly List<string> transcript = new List<string>();

        [MenuItem("ROKAS/Messages/Dialogue Workshop")]
        [MenuItem("Tools/ROKAS/Messages/Dialogue Workshop")]
        public static void Open()
        {
            var window = GetWindow<MessagesDialogueWorkshopWindow>("ROKAS · Dialogue Workshop");
            window.minSize = new Vector2(960, 600);
        }

        private static string PhysicalPath { get { return Path.Combine(Directory.GetCurrentDirectory(), FilePath); } }
        private void OnEnable() { Reload(); }
        private void Reload()
        {
            try { catalog = File.Exists(PhysicalPath) ?
                JsonUtility.FromJson<WorkshopCatalogue>(File.ReadAllText(PhysicalPath)) : new WorkshopCatalogue(); }
            catch (Exception e) { Debug.LogError(e); catalog = new WorkshopCatalogue(); }
            if (catalog == null) catalog = new WorkshopCatalogue();
            if (catalog.contacts == null) catalog.contacts = new List<WorkshopContact>();
            foreach (string id in Ids)
                if (catalog.contacts.Find(c => c != null && c.contactId == id) == null)
                    catalog.contacts.Add(new WorkshopContact { contactId = id });
            dirty = false; topic = -1; node = -1; transcript.Clear(); previewNode = "";
        }

        private WorkshopContact Contact
        {
            get
            {
                WorkshopContact c = catalog.contacts.Find(x => x != null && x.contactId == Ids[contact]);
                if (c.topics == null) c.topics = new List<WorkshopTopic>();
                return c;
            }
        }
        private WorkshopTopic Topic { get { return topic >= 0 && topic < Contact.topics.Count ? Contact.topics[topic] : null; } }
        private WorkshopNode Node
        {
            get { return Topic != null && Topic.nodes != null && node >= 0 && node < Topic.nodes.Count ? Topic.nodes[node] : null; }
        }
        private static string Id(string prefix) { return prefix + Guid.NewGuid().ToString("N").Substring(0, 8); }

        private void OnGUI()
        {
            if (catalog == null) Reload();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("ROKAS / DIALOGUE WORKSHOP", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (dirty) GUILayout.Label("UNSAVED", EditorStyles.miniBoldLabel);
            if (GUILayout.Button("Preview", EditorStyles.toolbarButton)) preview = !preview;
            if (GUILayout.Button("Reload", EditorStyles.toolbarButton))
            {
                if (!dirty || EditorUtility.DisplayDialog("Discard changes?", "Unsaved edits will be lost.", "Reload", "Cancel")) Reload();
            }
            if (GUILayout.Button("SAVE JSON", EditorStyles.toolbarButton)) Save();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            int next = GUILayout.Toolbar(contact, Labels, GUILayout.Height(32));
            if (next != contact) { contact = next; topic = -1; node = -1; transcript.Clear(); }
            EditorGUILayout.BeginHorizontal();
            DrawTopics();
            DrawTopicDetails();
            EditorGUILayout.EndHorizontal();
            if (preview) DrawPreview();
        }

        private void DrawTopics()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(265));
            GUILayout.Label("CONTACT DIALOGUES", EditorStyles.boldLabel);
            leftScroll = EditorGUILayout.BeginScrollView(leftScroll, EditorStyles.helpBox);
            for (int i = 0; i < Contact.topics.Count; i++)
            {
                var t = Contact.topics[i];
                if (t == null) continue;
                string label = (t.enabled ? "● " : "○ ") + t.title;
                if (GUILayout.Toggle(topic == i, label, "Button", GUILayout.Height(36)) && topic != i) { topic = i; node = -1; }
            }
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("+ New topic", GUILayout.Height(30)))
            {
                var t = new WorkshopTopic { id = Id("topic_"), title = "New dialogue", playerText = "Hello!", enabled = false };
                var first = new WorkshopNode { id = Id("node_"), text = "" };
                t.startNodeId = first.id; t.nodes.Add(first);
                Contact.topics.Add(t); topic = Contact.topics.Count - 1; node = 0; dirty = true;
            }
            if (Topic != null)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Duplicate"))
                {
                    var duplicate = JsonUtility.FromJson<WorkshopTopic>(JsonUtility.ToJson(Topic));
                    duplicate.id = Id("topic_"); duplicate.title += " (copy)"; duplicate.enabled = false;
                    Contact.topics.Add(duplicate); topic = Contact.topics.Count - 1; node = 0; dirty = true;
                }
                if (GUILayout.Button("Delete") && EditorUtility.DisplayDialog("Delete dialogue?", "Remove this topic and its nodes?", "Delete", "Cancel"))
                { Contact.topics.RemoveAt(topic); topic = -1; node = -1; dirty = true; }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.HelpBox("Publish topics individually; none are predefined.", MessageType.Info);
            EditorGUILayout.EndVertical();
        }

        private void DrawTopicDetails()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            rightScroll = EditorGUILayout.BeginScrollView(rightScroll, EditorStyles.helpBox);
            if (Topic == null)
            {
                EditorGUILayout.HelpBox("Select a contact and add a dialogue topic. Create NPC messages, player response choices, and branching destinations.", MessageType.Info);
                EditorGUILayout.EndScrollView(); EditorGUILayout.EndVertical(); return;
            }
            var t = Topic;
            EditorGUI.BeginChangeCheck();
            GUILayout.Label("DIALOGUE TOPIC", EditorStyles.boldLabel);
            t.title = EditorGUILayout.TextField("Topic title", t.title);
            GUILayout.Label("Player's opening message");
            t.playerText = EditorGUILayout.TextArea(t.playerText, GUILayout.Height(48));
            EditorGUILayout.BeginHorizontal();
            t.enabled = EditorGUILayout.ToggleLeft("Published in game", t.enabled, GUILayout.Width(170));
            t.repeatable = EditorGUILayout.ToggleLeft("Repeatable", t.repeatable, GUILayout.Width(120));
            GUILayout.FlexibleSpace(); GUILayout.Label(t.id, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            if (t.nodes == null) t.nodes = new List<WorkshopNode>();
            t.startNodeId = NodePicker("Start node", t.nodes, t.startNodeId, false);
            EditorGUILayout.Space(10);
            GUILayout.Label("CONTACT MESSAGES", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < t.nodes.Count; i++)
            {
                var n = t.nodes[i]; if (n == null) continue;
                string caption = "#" + (i + 1) + ": " + (n.text ?? "").Replace('\n',' ');
                if (caption.Length > 21) caption = caption.Substring(0, 21) + "…";
                if (GUILayout.Toggle(node == i, caption, "Button", GUILayout.Height(30)) && node != i) node = i;
            }
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("+ Add contact message", GUILayout.Height(28)))
            {
                t.nodes.Add(new WorkshopNode { id = Id("node_"), text = "New reply" });
                node = t.nodes.Count - 1; dirty = true;
            }
            if (Node != null)
            {
                var n = Node;
                EditorGUILayout.Space(8);
                GUILayout.Label("EDIT MESSAGE: " + n.id, EditorStyles.boldLabel);
                n.text = EditorGUILayout.TextArea(n.text, GUILayout.Height(70));
                if (n.choices == null) n.choices = new List<WorkshopChoice>();
                if (n.choices.Count == 0) n.nextNodeId = NodePicker("Auto next", t.nodes, n.nextNodeId, true);
                else EditorGUILayout.HelpBox("Multiple responses replace auto continuation.", MessageType.None);
                EditorGUILayout.Space(6);
                GUILayout.Label("PLAYER RESPONSE CHOICES", EditorStyles.boldLabel);
                for (int i = 0; i < n.choices.Count; i++)
                {
                    WorkshopChoice c = n.choices[i];
                    if (c == null) continue;
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Option " + (i + 1), EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Remove", GUILayout.Width(70)))
                    { n.choices.RemoveAt(i); dirty = true; EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical(); break; }
                    EditorGUILayout.EndHorizontal();
                    c.text = EditorGUILayout.TextField("Player says", c.text);
                    c.nextNodeId = NodePicker("Jump to", t.nodes, c.nextNodeId, true);
                    EditorGUILayout.EndVertical();
                }
                if (GUILayout.Button("+ Add answer option", GUILayout.Height(28)))
                {
                    n.choices.Add(new WorkshopChoice { id = Id("choice_"), text = "Response" });
                    n.nextNodeId = ""; dirty = true;
                }
                EditorGUILayout.Space();
                if (GUILayout.Button("Delete selected message") &&
                    EditorUtility.DisplayDialog("Delete node?", "Other choices may point to this message.", "Delete", "Cancel"))
                {
                    string oldId = n.id; t.nodes.RemoveAt(node);
                    if (t.startNodeId == oldId) t.startNodeId = "";
                    node = -1; dirty = true;
                }
            }
            EditorGUILayout.Space(12);
            string error = Validate(t);
            EditorGUILayout.HelpBox(error ?? "Ready. Save JSON and test the conversation in Play Mode.",
                error == null ? MessageType.Info : MessageType.Warning);
            if (EditorGUI.EndChangeCheck()) dirty = true;
            EditorGUILayout.EndScrollView(); EditorGUILayout.EndVertical();
        }

        private static string NodePicker(string caption, List<WorkshopNode> nodes, string current, bool allowEnd)
        {
            var labels = new List<string> { allowEnd ? "— END —" : "— SELECT —" };
            var ids = new List<string> { "" };
            foreach (WorkshopNode n in nodes)
            {
                if (n == null) continue;
                ids.Add(n.id);
                labels.Add((n.text ?? "").Length > 35 ? n.text.Substring(0, 35) : n.text);
            }
            int selected = Mathf.Max(0, ids.IndexOf(current ?? ""));
            return ids[EditorGUILayout.Popup(caption, selected, labels.ToArray())];
        }

        private static string Validate(WorkshopTopic t)
        {
            if (string.IsNullOrWhiteSpace(t.title) || string.IsNullOrWhiteSpace(t.playerText))
                return "Add the topic title and player's opening message.";
            if (t.nodes == null || t.nodes.Count == 0) return "Add at least one message.";
            var ids = new HashSet<string>(); var choiceIds = new HashSet<string>();
            foreach (WorkshopNode n in t.nodes)
                if (n == null || string.IsNullOrEmpty(n.id) || !ids.Add(n.id)) return "Duplicate/invalid message ID.";
            if (!ids.Contains(t.startNodeId ?? "")) return "Choose a valid start node.";
            foreach (WorkshopNode n in t.nodes)
            {
                if (!string.IsNullOrEmpty(n.nextNodeId) && !ids.Contains(n.nextNodeId)) return "Broken auto-next link: " + n.id;
                if (n.choices == null) continue;
                if (n.choices.Count > 0 && !string.IsNullOrEmpty(n.nextNodeId)) return "Choose auto-next OR player choices, not both.";
                foreach (WorkshopChoice c in n.choices)
                {
                    if (c == null || string.IsNullOrEmpty(c.id) || !choiceIds.Add(c.id) || string.IsNullOrWhiteSpace(c.text))
                        return "Player options need unique IDs and text.";
                    if (!string.IsNullOrEmpty(c.nextNodeId) && !ids.Contains(c.nextNodeId))
                        return "Broken response destination: " + c.id;
                }
            }
            return null;
        }

        private void Save()
        {
            foreach (WorkshopContact c in catalog.contacts)
            {
                if (c == null || c.topics == null) continue;
                var ids = new HashSet<string>();
                foreach (WorkshopTopic t in c.topics)
                {
                    if (t == null) continue;
                    if (string.IsNullOrEmpty(t.id) || !ids.Add(t.id))
                    { EditorUtility.DisplayDialog("Invalid dialogue", "Duplicate topic ID", "OK"); return; }
                    if (t.enabled && Validate(t) != null)
                    { EditorUtility.DisplayDialog("Invalid published topic", t.title + ": " + Validate(t), "OK"); return; }
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(PhysicalPath));
            File.WriteAllText(PhysicalPath, JsonUtility.ToJson(catalog, true), new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(FilePath, ImportAssetOptions.ForceUpdate);
            dirty = false;
            Debug.Log("ROKAS Dialogue Workshop saved " + FilePath);
        }

        private void DrawPreview()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(210));
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("BRANCH PREVIEW", EditorStyles.boldLabel);
            if (GUILayout.Button("Start", GUILayout.Width(76)) && Topic != null)
            { transcript.Clear(); transcript.Add("YOU: " + Topic.playerText); PreviewAdvance(Topic.startNodeId); }
            if (GUILayout.Button("Clear", GUILayout.Width(76))) { transcript.Clear(); previewNode = ""; }
            EditorGUILayout.EndHorizontal();
            previewScroll = EditorGUILayout.BeginScrollView(previewScroll);
            foreach (string message in transcript) EditorGUILayout.LabelField(message, EditorStyles.wordWrappedLabel);
            WorkshopNode active = Topic == null || Topic.nodes == null ? null :
                Topic.nodes.Find(n => n != null && n.id == previewNode);
            if (active != null && active.choices != null)
                foreach (WorkshopChoice answer in active.choices)
                {
                    if (answer == null) continue;
                    if (GUILayout.Button(answer.text))
                    { transcript.Add("YOU: " + answer.text); PreviewAdvance(answer.nextNodeId); break; }
                }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
        private void PreviewAdvance(string target)
        {
            previewNode = "";
            if (Topic == null) return;
            var seen = new HashSet<string>();
            for (int i = 0; i < 32 && !string.IsNullOrEmpty(target); i++)
            {
                if (!seen.Add(target)) { transcript.Add("[Auto-loop]"); return; }
                WorkshopNode n = Topic.nodes.Find(x => x != null && x.id == target);
                if (n == null) { transcript.Add("[Missing node: " + target + "]"); return; }
                transcript.Add(Labels[contact] + ": " + n.text);
                if (n.choices != null && n.choices.Count > 0) { previewNode = target; return; }
                target = n.nextNodeId;
            }
        }
    }
}
#endif
