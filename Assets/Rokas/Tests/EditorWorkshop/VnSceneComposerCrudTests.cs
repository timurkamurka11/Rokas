using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerCrudTests
    {
        private const string EditorAssembly = "Rokas.Editor";
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";

        [Test]
        public void SceneCrudPreservesStableIdentityAndOrder()
        {
            Type projectType = RequireType("VnSceneComposerProject");
            Type editingType = RequireType("VnSceneComposerEditing");
            object project = Activator.CreateInstance(projectType);

            MethodInfo add = RequireStatic(editingType, "AddScene", projectType, typeof(string));
            MethodInfo rename = RequireStatic(editingType, "RenameScene", projectType, typeof(string), typeof(string));
            MethodInfo move = RequireStatic(editingType, "MoveScene", projectType, typeof(string), typeof(int));
            MethodInfo delete = RequireStatic(editingType, "DeleteScene", projectType, typeof(string));

            object a = add.Invoke(null, new object[] { project, "A" });
            object b = add.Invoke(null, new object[] { project, "B" });
            object c = add.Invoke(null, new object[] { project, "C" });
            string aId = GetString(a, "sceneId");
            string bId = GetString(b, "sceneId");
            string cId = GetString(c, "sceneId");

            Assert.That(aId, Is.Not.Empty);
            Assert.That(new[] { aId, bId, cId }.Distinct().Count(), Is.EqualTo(3));
            Assert.That((bool)rename.Invoke(null, new object[] { project, bId, "Renamed" }), Is.True);
            Assert.That(GetString(b, "label"), Is.EqualTo("Renamed"));
            Assert.That((bool)move.Invoke(null, new object[] { project, cId, 0 }), Is.True);
            Assert.That(SceneIdAt(project, 0), Is.EqualTo(cId));
            Assert.That(SceneIdAt(project, 1), Is.EqualTo(aId));
            Assert.That(SceneIdAt(project, 2), Is.EqualTo(bId));
            Assert.That((bool)delete.Invoke(null, new object[] { project, aId }), Is.True);
            Assert.That(SceneCount(project), Is.EqualTo(2));
            Assert.That(SceneIdAt(project, 0), Is.EqualTo(cId));
            Assert.That(SceneIdAt(project, 1), Is.EqualTo(bId));
        }

        [Test]
        public void DuplicateSceneGetsNewIdAndDeepCopiesMutableState()
        {
            Type projectType = RequireType("VnSceneComposerProject");
            Type editingType = RequireType("VnSceneComposerEditing");
            object project = Activator.CreateInstance(projectType);
            MethodInfo add = RequireStatic(editingType, "AddScene", projectType, typeof(string));
            MethodInfo duplicate = RequireStatic(editingType, "DuplicateScene", projectType, typeof(string));

            object source = add.Invoke(null, new object[] { project, "Source" });
            SetProperty(source, "speaker", "Mina");
            SetProperty(source, "previewText", "Original line");
            object sourceMedia = Get(source, "media");
            Set(sourceMedia, "displayName", "Background A");
            Set(sourceMedia, "reference", "guid:abc");
            IList characters = (IList)Get(source, "characters");
            object character = Activator.CreateInstance(RequireType("VnSceneComposerCharacter"));
            Set(character, "characterId", "mina");
            Set(character, "stateId", "mina_happy");
            characters.Add(character);

            string sourceId = GetString(source, "sceneId");
            object copy = duplicate.Invoke(null, new object[] { project, sourceId });

            Assert.That(copy, Is.Not.Null);
            Assert.That(GetString(copy, "sceneId"), Is.Not.EqualTo(sourceId));
            Assert.That(GetPropertyString(copy, "speaker"), Is.EqualTo("Mina"));
            Assert.That(Get(copy, "media"), Is.Not.SameAs(sourceMedia));
            Assert.That(Get(copy, "presentationOverrides"), Is.Not.SameAs(Get(source, "presentationOverrides")));
            Assert.That(Get(copy, "characters"), Is.Not.SameAs(characters));
            Assert.That(((IList)Get(copy, "characters")).Count, Is.EqualTo(1));

            Set(Get(copy, "media"), "displayName", "Changed");
            SetProperty(copy, "previewText", "Changed line");
            Assert.That(GetString(sourceMedia, "displayName"), Is.EqualTo("Background A"));
            Assert.That(GetPropertyString(source, "previewText"), Is.EqualTo("Original line"));
            Assert.That(SceneCount(project), Is.EqualTo(2));
            Assert.That(SceneIdAt(project, 1), Is.EqualTo(GetString(copy, "sceneId")));
        }

        [Test]
        public void MD_AddBeatInsertsEmptyBeatAfterAnchorWithUniqueId()
        {
            Type sceneType = RequireType("VnSceneComposerScene");
            Type dialogueType = RequireType("VnSceneComposerDialogue");
            object scene = Activator.CreateInstance(sceneType);
            IList beats = (IList)Get(scene, "dialogueBeats");
            object a = beats[0];
            Set(a, "speaker", "Aiko");
            Set(a, "text", "A");
            string aId = GetString(a, "beatId");

            MethodInfo add = RequireStaticAnyVisibility(dialogueType, "AddBeat", sceneType, typeof(string));
            object b = add.Invoke(null, new object[] { scene, aId });

            Assert.That(beats.Count, Is.EqualTo(2));
            Assert.That(beats[0], Is.SameAs(a));
            Assert.That(beats[1], Is.SameAs(b));
            Assert.That(GetString(b, "beatId"), Is.Not.Empty.And.Not.EqualTo(aId));
            Assert.That(GetString(b, "speaker"), Is.Empty);
            Assert.That(GetString(b, "text"), Is.Empty);
            Assert.That((bool)Get(b, "narration"), Is.False);
        }

        [Test]
        public void MD_DuplicateBeatCopiesDialogueIntoIndependentIdentity()
        {
            Type sceneType = RequireType("VnSceneComposerScene");
            Type dialogueType = RequireType("VnSceneComposerDialogue");
            object scene = Activator.CreateInstance(sceneType);
            IList beats = (IList)Get(scene, "dialogueBeats");
            object a = beats[0];
            Set(a, "speaker", "Aiko");
            Set(a, "text", "Привет");
            Set(a, "narration", true);
            string aId = GetString(a, "beatId");

            MethodInfo duplicate = RequireStaticAnyVisibility(dialogueType, "DuplicateBeat", sceneType, typeof(string));
            object copy = duplicate.Invoke(null, new object[] { scene, aId });

            Assert.That(beats.Count, Is.EqualTo(2));
            Assert.That(beats[1], Is.SameAs(copy).And.Not.SameAs(a));
            Assert.That(GetString(copy, "beatId"), Is.Not.EqualTo(aId));
            Assert.That(GetString(copy, "speaker"), Is.EqualTo("Aiko"));
            Assert.That(GetString(copy, "text"), Is.EqualTo("Привет"));
            Assert.That((bool)Get(copy, "narration"), Is.True);

            Set(copy, "text", "Changed");
            Assert.That(GetString(a, "text"), Is.EqualTo("Привет"));
        }

        [Test]
        public void MD_MoveBeatReordersExistingStableIdentity()
        {
            Type sceneType = RequireType("VnSceneComposerScene");
            Type beatType = RequireType("VnSceneComposerDialogueBeat");
            Type dialogueType = RequireType("VnSceneComposerDialogue");
            object scene = Activator.CreateInstance(sceneType);
            IList beats = (IList)Get(scene, "dialogueBeats");
            object a = beats[0];
            Set(a, "text", "A");
            object b = Activator.CreateInstance(beatType);
            Set(b, "text", "B");
            beats.Add(b);
            string aId = GetString(a, "beatId");
            string bId = GetString(b, "beatId");

            MethodInfo move = RequireStaticAnyVisibility(dialogueType, "MoveBeat", sceneType, typeof(string), typeof(int));
            bool moved = (bool)move.Invoke(null, new object[] { scene, bId, 0 });

            Assert.That(moved, Is.True);
            Assert.That(GetString(beats[0], "beatId"), Is.EqualTo(bId));
            Assert.That(GetString(beats[1], "beatId"), Is.EqualTo(aId));
            Assert.That(beats[0], Is.SameAs(b));
            Assert.That(beats[1], Is.SameAs(a));
        }

        [Test]
        public void MD_DeleteBeatSelectsNextThenPreviousAndReplacesLast()
        {
            Type sceneType = RequireType("VnSceneComposerScene");
            Type beatType = RequireType("VnSceneComposerDialogueBeat");
            Type dialogueType = RequireType("VnSceneComposerDialogue");
            object scene = Activator.CreateInstance(sceneType);
            IList beats = (IList)Get(scene, "dialogueBeats");
            object a = beats[0];
            Set(a, "text", "A");
            object b = Activator.CreateInstance(beatType);
            Set(b, "text", "B");
            object c = Activator.CreateInstance(beatType);
            Set(c, "text", "C");
            beats.Add(b);
            beats.Add(c);
            string aId = GetString(a, "beatId");
            string bId = GetString(b, "beatId");
            string cId = GetString(c, "beatId");

            MethodInfo delete = RequireStaticAnyVisibility(dialogueType, "DeleteBeat", sceneType, typeof(string));

            string afterB = (string)delete.Invoke(null, new object[] { scene, bId });
            Assert.That(afterB, Is.EqualTo(cId), "Deleting a middle beat selects the next beat.");
            Assert.That(beats.Cast<object>().Select(x => GetString(x, "beatId")).ToArray(),
                Is.EqualTo(new[] { aId, cId }));

            string afterC = (string)delete.Invoke(null, new object[] { scene, cId });
            Assert.That(afterC, Is.EqualTo(aId), "Deleting the final beat selects the previous beat.");
            Assert.That(beats.Count, Is.EqualTo(1));

            string replacementId = (string)delete.Invoke(null, new object[] { scene, aId });
            Assert.That(beats.Count, Is.EqualTo(1));
            Assert.That(replacementId, Is.EqualTo(GetString(beats[0], "beatId")));
            Assert.That(replacementId, Is.Not.Empty.And.Not.EqualTo(aId));
            Assert.That(GetString(beats[0], "speaker"), Is.Empty);
            Assert.That(GetString(beats[0], "text"), Is.Empty);
            Assert.That((bool)Get(beats[0], "narration"), Is.False);
        }

        [Test]
        public void MD_DuplicateSceneRegeneratesBeatIdsAndKeepsBeatObjectsIndependent()
        {
            Type projectType = RequireType("VnSceneComposerProject");
            Type editingType = RequireType("VnSceneComposerEditing");
            Type beatType = RequireType("VnSceneComposerDialogueBeat");
            object project = Activator.CreateInstance(projectType);
            MethodInfo addScene = RequireStatic(editingType, "AddScene", projectType, typeof(string));
            MethodInfo duplicateScene = RequireStatic(editingType, "DuplicateScene", projectType, typeof(string));
            object source = addScene.Invoke(null, new object[] { project, "Multi" });
            IList sourceBeats = (IList)Get(source, "dialogueBeats");
            object first = sourceBeats[0];
            Set(first, "speaker", "Aiko");
            Set(first, "text", "Привет");
            object second = Activator.CreateInstance(beatType);
            Set(second, "speaker", "Tim");
            Set(second, "text", "你好");
            Set(second, "narration", true);
            sourceBeats.Add(second);

            string sourceSceneId = GetString(source, "sceneId");
            string[] sourceBeatIds = sourceBeats.Cast<object>().Select(x => GetString(x, "beatId")).ToArray();
            object copy = duplicateScene.Invoke(null, new object[] { project, sourceSceneId });
            IList copyBeats = (IList)Get(copy, "dialogueBeats");

            Assert.That(GetString(copy, "sceneId"), Is.Not.EqualTo(sourceSceneId));
            Assert.That(copyBeats.Count, Is.EqualTo(sourceBeats.Count));
            for (int i = 0; i < sourceBeats.Count; i++)
            {
                Assert.That(copyBeats[i], Is.Not.SameAs(sourceBeats[i]));
                Assert.That(GetString(copyBeats[i], "beatId"), Is.Not.EqualTo(sourceBeatIds[i]),
                    "Duplicating a Scene must regenerate every canonical beat identity.");
                Assert.That(GetString(copyBeats[i], "speaker"), Is.EqualTo(GetString(sourceBeats[i], "speaker")));
                Assert.That(GetString(copyBeats[i], "text"), Is.EqualTo(GetString(sourceBeats[i], "text")));
                Assert.That((bool)Get(copyBeats[i], "narration"), Is.EqualTo((bool)Get(sourceBeats[i], "narration")));
            }

            Set(copyBeats[1], "text", "Changed clone");
            Assert.That(GetString(sourceBeats[1], "text"), Is.EqualTo("你好"));
        }

        private static MethodInfo RequireStaticAnyVisibility(Type type, string name, params Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null, parameters, null);
            Assert.That(method, Is.Not.Null, "Missing static method: " + type.Name + "." + name);
            return method;
        }

        private static Type RequireType(string shortName)
        {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a => a.GetName().Name == EditorAssembly);
            Assert.That(assembly, Is.Not.Null);
            Type type = assembly.GetType(Namespace + shortName, false);
            Assert.That(type, Is.Not.Null, "Missing Scene Composer type: " + shortName);
            return type;
        }

        private static MethodInfo RequireStatic(Type type, string name, params Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, parameters, null);
            Assert.That(method, Is.Not.Null, "Missing public static method: " + type.Name + "." + name);
            return method;
        }

        private static FieldInfo Field(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing public field: " + instance.GetType().Name + "." + name);
            return field;
        }

        private static PropertyInfo Property(object instance, string name)
        {
            PropertyInfo property = instance.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, "Missing public property: " + instance.GetType().Name + "." + name);
            return property;
        }

        private static object Get(object instance, string name) { return Field(instance, name).GetValue(instance); }
        private static string GetString(object instance, string name) { return (string)Get(instance, name); }
        private static void Set(object instance, string name, object value) { Field(instance, name).SetValue(instance, value); }
        private static object GetProperty(object instance, string name) { return Property(instance, name).GetValue(instance); }
        private static string GetPropertyString(object instance, string name) { return (string)GetProperty(instance, name); }
        private static void SetProperty(object instance, string name, object value) { Property(instance, name).SetValue(instance, value); }
        private static IList Scenes(object project) { return (IList)Get(project, "scenes"); }
        private static int SceneCount(object project) { return Scenes(project).Count; }
        private static string SceneIdAt(object project, int index) { return GetString(Scenes(project)[index], "sceneId"); }
    }
}
