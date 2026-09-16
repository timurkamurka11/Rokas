using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed partial class VnSceneComposerPlaybackTests
    {
        [Test]
        public void FocusedAnimationPreviewUsesCurrentSceneComposerDurationWithoutMutatingAuthoringState()
        {
            System.Type windowType = Md3RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = Md3CreateWindow(windowType);
            object bounce = Md3EnumValue("VnWorkshopPreviewEffect", "Bounce");
            try
            {
                Md3Invoke(window, "ComposerAddScene");
                Md3Invoke(window, "ActivateSceneComposerWorkspace");
                Md3Invoke(window, "ComposerSetPresentationScope", false);
                Md3SetDuration(window, "bounce", 2f);

                object project = Md3GetField(window, "_sceneComposerProject");
                string projectBefore = JsonUtility.ToJson(project);
                string selectedBefore = (string)Md3GetField(window, "_sceneComposerSelectedSceneId");
                int undoBefore = Undo.GetCurrentGroup();
                Assert.That(Md3GetField(window, "_sceneComposerPlayback"), Is.Null,
                    "Focused preview should be independent from the normal Scene Composer transport before it starts.");

                Md3Invoke(window, "ComposerPreviewFocusedEffect", bounce);
                object snapshot = Md3Invoke(window, "GetPreviewPlaybackSnapshot");

                Assert.That((float)Md3GetField(snapshot, "Duration"), Is.EqualTo(2f).Within(.0001f),
                    "Focused preview must use the currently authored Scene Composer duration rather than the hidden legacy Workshop preset.");
                Assert.That((float)Md3GetField(snapshot, "NormalizedProgress"), Is.EqualTo(0f).Within(.0001f),
                    "Focused preview must start from the beginning.");
                Assert.That(Md3GetProperty(window, "CurrentMotionPreviewFrame"), Is.Not.Null,
                    "Focused preview must render through the existing MotionPreviewBridge frame path.");
                Assert.That(JsonUtility.ToJson(project), Is.EqualTo(projectBefore),
                    "Starting focused preview must not mutate authored project data.");
                Assert.That((string)Md3GetField(window, "_sceneComposerSelectedSceneId"), Is.EqualTo(selectedBefore),
                    "Starting focused preview must not change the selected scene.");
                Assert.That(Md3GetField(window, "_sceneComposerPlayback"), Is.Null,
                    "Focused preview must not enter Play Scene / Play All transport state.");
                Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(undoBefore),
                    "Preview execution itself must not add an authored Undo step.");

                Md4SetField(window, "previewProgress", .65f);
                Md4SetField(window, "previewPlaying", true);
                Md3Invoke(window, "ComposerPreviewFocusedEffect", bounce);
                object repeated = Md3Invoke(window, "GetPreviewPlaybackSnapshot");
                Assert.That((float)Md3GetField(repeated, "NormalizedProgress"), Is.EqualTo(0f).Within(.0001f),
                    "Pressing focused preview again must restart from the beginning instead of retaining stale progress.");
            }
            finally
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void CompletedFocusedAnimationPreviewReleasesMotionFrameAndReturnsToAuthoring()
        {
            System.Type windowType = Md3RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = Md3CreateWindow(windowType);
            object bounce = Md3EnumValue("VnWorkshopPreviewEffect", "Bounce");
            try
            {
                Md3Invoke(window, "ComposerAddScene");
                Md3Invoke(window, "ActivateSceneComposerWorkspace");
                Md3Invoke(window, "ComposerSetPresentationScope", false);
                Md3SetDuration(window, "bounce", .5f);

                Md3Invoke(window, "ComposerPreviewFocusedEffect", bounce);
                Assert.That(Md3GetProperty(window, "CurrentMotionPreviewFrame"), Is.Not.Null,
                    "A running focused preview should own a temporary motion frame.");

                Md4SetField(window, "previewProgress", 1f);
                Md4SetField(window, "previewPlaying", false);
                Md4SetField(window, "previewPaused", false);

                Assert.That(Md3GetProperty(window, "CurrentMotionPreviewFrame"), Is.Null,
                    "Once focused preview completes, the temporary frame must be released so normal authoring becomes interactive again.");
                Assert.That(Md3GetField(window, "_sceneComposerPlayback"), Is.Null,
                    "Completing focused preview must not create or retain normal transport state.");
            }
            finally
            {
                Undo.ClearAll();
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        private static void Md4SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing field " + name + " on " + target.GetType().FullName + ".");
            field.SetValue(target, value);
        }
    }
}
