using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;

namespace Rokas.EditorTools.Tests
{
    public sealed partial class VnSceneComposerPlaybackTests
    {
        [Test]
        public void MD_CanonicalDialogueModelUsesSchemaTwoAndOneInitialBeat()
        {
            Type contractType = RequireType("VnSceneComposerContract");
            FieldInfo schema = contractType.GetField("SchemaVersion", BindingFlags.Public | BindingFlags.Static);
            Assert.That(schema, Is.Not.Null);
            Assert.That(schema.GetRawConstantValue(), Is.EqualTo(2),
                "M-DIALOGUE portable data must move to canonical schema v2.");

            Type sceneType = RequireType("VnSceneComposerScene");
            FieldInfo beatsField = sceneType.GetField("dialogueBeats", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(beatsField, Is.Not.Null,
                "A Scene must own one canonical ordered dialogueBeats list.");

            object scene = Activator.CreateInstance(sceneType);
            IList beats = beatsField.GetValue(scene) as IList;
            Assert.That(beats, Is.Not.Null.And.Count.EqualTo(1),
                "A new Scene should immediately expose one editable dialogue beat.");

            object beat = beats[0];
            Assert.That((string)Get(beat, "beatId"), Has.Length.EqualTo(32));
            Assert.That((string)Get(beat, "speaker"), Is.EqualTo(string.Empty));
            Assert.That((string)Get(beat, "text"), Is.EqualTo(string.Empty));
            Assert.That((bool)Get(beat, "narration"), Is.False);

            Assert.That(sceneType.GetField("speaker", BindingFlags.Public | BindingFlags.Instance), Is.Null,
                "Legacy single-dialogue fields must not remain independent serialized authorities in schema v2.");
            Assert.That(sceneType.GetField("previewText", BindingFlags.Public | BindingFlags.Instance), Is.Null);
            Assert.That(sceneType.GetField("narration", BindingFlags.Public | BindingFlags.Instance), Is.Null);
        }

        [Test]
        public void MD_LegacySchemaOneDialogueImportsAsExactlyOneCanonicalBeat()
        {
            Type serializationType = RequireType("VnSceneComposerSerialization");
            MethodInfo deserialize = serializationType.GetMethod(
                "DeserializePortable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(string) }, null);
            Assert.That(deserialize, Is.Not.Null);

            const string json = "{\n" +
                "  \"schemaVersion\": 1,\n" +
                "  \"sourceHead\": \"f58f1db08fc225c2831ff59d50d42e8c07ea15ce\",\n" +
                "  \"projectId\": \"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\n" +
                "  \"title\": \"Legacy\",\n" +
                "  \"scenes\": [{\n" +
                "    \"sceneId\": \"11111111111111111111111111111111\",\n" +
                "    \"label\": \"Legacy Scene\",\n" +
                "    \"speaker\": \"Aiko\",\n" +
                "    \"previewText\": \"Привет 你好 Hello\",\n" +
                "    \"narration\": false\n" +
                "  }]\n" +
                "}";

            object result = deserialize.Invoke(null, new object[] { json });
            Assert.That((bool)Get(result, "Success"), Is.True, (string)Get(result, "Error"));
            object project = Get(result, "Project");
            IList scenes = (IList)Get(project, "scenes");
            Assert.That(scenes, Has.Count.EqualTo(1));

            FieldInfo beatsField = scenes[0].GetType().GetField("dialogueBeats", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(beatsField, Is.Not.Null,
                "Legacy v1 dialogue must migrate into the canonical v2 beat list.");
            IList beats = (IList)beatsField.GetValue(scenes[0]);
            Assert.That(beats, Has.Count.EqualTo(1));
            Assert.That((string)Get(beats[0], "speaker"), Is.EqualTo("Aiko"));
            Assert.That((string)Get(beats[0], "text"), Is.EqualTo("Привет 你好 Hello"));
            Assert.That((bool)Get(beats[0], "narration"), Is.False);
        }
    }
}
