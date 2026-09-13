using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerTests
    {
        private const string EditorAssembly = "Rokas.Editor";
        private const string Namespace = "Rokas.EditorTools.VnUiWorkshop.";
        private const string ExpectedSourceHead = "f58f1db08fc225c2831ff59d50d42e8c07ea15ce";

        [Test]
        public void ComposerContractPinsVerifiedVn10SourceAndStartsAtSchemaOne()
        {
            Type contract = RequireType("VnSceneComposerContract");
            FieldInfo schemaVersion = RequirePublicStaticField(contract, "SchemaVersion");
            FieldInfo sourceHead = RequirePublicStaticField(contract, "SourceHead");

            Assert.That(schemaVersion.GetRawConstantValue(), Is.EqualTo(1));
            Assert.That(sourceHead.GetRawConstantValue(), Is.EqualTo(ExpectedSourceHead));
        }

        [Test]
        public void ComposerProjectAndSceneModelsExposeStableIdentityAndAuthoringData()
        {
            Type project = RequireType("VnSceneComposerProject");
            RequirePublicInstanceField(project, "schemaVersion");
            RequirePublicInstanceField(project, "projectId");
            RequirePublicInstanceField(project, "title");
            RequirePublicInstanceField(project, "sourceHead");
            RequirePublicInstanceField(project, "defaultPresentation");
            RequirePublicInstanceField(project, "scenes");

            Type scene = RequireType("VnSceneComposerScene");
            RequirePublicInstanceField(scene, "sceneId");
            RequirePublicInstanceField(scene, "label");
            RequirePublicInstanceField(scene, "media");
            RequirePublicInstanceField(scene, "characters");
            RequirePublicInstanceField(scene, "speaker");
            RequirePublicInstanceField(scene, "previewText");
            RequirePublicInstanceField(scene, "narration");
            RequirePublicInstanceField(scene, "presentationOverrides");
            RequirePublicInstanceField(scene, "transition");
            RequirePublicInstanceField(scene, "timing");
        }

        [Test]
        public void ComposerMediaKindsDistinguishRepositoryAssetsFromLocalPreviewDependencies()
        {
            Type mediaKind = RequireType("VnSceneComposerMediaKind");
            Assert.That(mediaKind.IsEnum, Is.True);
            string[] names = Enum.GetNames(mediaKind);
            Assert.That(names, Does.Contain("None"));
            Assert.That(names, Does.Contain("ExistingRokasAsset"));
            Assert.That(names, Does.Contain("ExternalImage"));
            Assert.That(names, Does.Contain("ExternalVideo"));
            Assert.That(names, Does.Contain("ExternalGif"));

            Type media = RequireType("VnSceneComposerMediaReference");
            RequirePublicInstanceField(media, "kind");
            RequirePublicInstanceField(media, "reference");
            RequirePublicInstanceField(media, "displayName");
            RequirePublicInstanceField(media, "contentHash");
            RequirePublicInstanceField(media, "localPreviewDependency");
        }

        [Test]
        public void ComposerReusesExistingVn10PresetTypeForProjectDefaultsAndSceneOverrides()
        {
            Type preset = RequireType("VnPresentationWorkshopPreset");
            Type project = RequireType("VnSceneComposerProject");
            Type scene = RequireType("VnSceneComposerScene");

            Assert.That(RequirePublicInstanceField(project, "defaultPresentation").FieldType, Is.EqualTo(preset));
            Assert.That(RequirePublicInstanceField(scene, "presentationOverrides").FieldType, Is.EqualTo(preset));
        }

        [Test]
        public void ComposerCharacterModelUsesAuthoredStateIdAndExistingStageSlot()
        {
            Type character = RequireType("VnSceneComposerCharacter");
            RequirePublicInstanceField(character, "characterId");
            RequirePublicInstanceField(character, "stateId");
            FieldInfo stageSlot = RequirePublicInstanceField(character, "stageSlot");
            Assert.That(stageSlot.FieldType, Is.EqualTo(RequireType("VnWorkshopStageSlot")));
        }

        private static Type RequireType(string shortName)
        {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                .SingleOrDefault(candidate => candidate.GetName().Name == EditorAssembly);
            Assert.That(assembly, Is.Not.Null, "Expected editor assembly '" + EditorAssembly + "'.");
            Type type = assembly.GetType(Namespace + shortName, false);
            Assert.That(type, Is.Not.Null, "Missing Scene Composer contract type: " + Namespace + shortName + ".");
            return type;
        }

        private static FieldInfo RequirePublicStaticField(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, type.Name + " must expose public static field " + name + ".");
            return field;
        }

        private static FieldInfo RequirePublicInstanceField(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, type.Name + " must expose public instance field " + name + ".");
            return field;
        }
    }
}
