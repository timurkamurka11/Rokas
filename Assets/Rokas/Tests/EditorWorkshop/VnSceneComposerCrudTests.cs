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
