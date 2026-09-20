using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed partial class VnSceneComposerPlaybackTests
    {
        [Test]
        public void P6_RendererAcceptsFullPlaybackFrameInsteadOfDiscardingTransitionContext()
        {
            Type rendererType = RequireType("VnPresentationWorkshopPreviewRenderer");
            Type playbackFrameType = RequireType("VnSceneComposerPlaybackFrame");
            MethodInfo draw = rendererType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method => method.Name == "Draw" &&
                    method.GetParameters().Length >= 2 &&
                    method.GetParameters()[0].ParameterType == typeof(Rect) &&
                    method.GetParameters()[1].ParameterType == playbackFrameType);
            Assert.That(draw, Is.Not.Null,
                "P6 renderer gap: Scene Composer must pass the full VnSceneComposerPlaybackFrame to the existing Workshop renderer so transition context is not discarded.");
        }

        [Test]
        public void P6_BackgroundFadeProducesDifferentComposedPixelsAtStartMidAndEnd()
        {
            object project = ProjectWithTwoBackgroundScenes("Fade", "RightToLeft");
            Type projectType = RequireType("VnSceneComposerProject");
            Type controllerType = RequirePlaybackType();
            object controller = NewController(controllerType, projectType, project);
            Texture2D start = null, middle = null, end = null;
            try
            {
                Invoke(controllerType, controller, "PlayScene", typeof(int), 1);
                start = ComposePlaybackBackground(Get(controller, "CurrentFrame"));
                Invoke(controllerType, controller, "Advance", typeof(float), .5f);
                middle = ComposePlaybackBackground(Get(controller, "CurrentFrame"));
                Invoke(controllerType, controller, "Advance", typeof(float), .5f);
                end = ComposePlaybackBackground(Get(controller, "CurrentFrame"));
                Assert.That(PixelSignature(middle), Is.Not.EqualTo(PixelSignature(start)), "Fade midpoint must visibly differ from source.");
                Assert.That(PixelSignature(end), Is.Not.EqualTo(PixelSignature(middle)), "Fade endpoint must visibly finish on target.");
                Assert.That(PixelSignature(end), Is.Not.EqualTo(PixelSignature(start)), "Fade must not remain one static background.");
            }
            finally { DestroyTexture(start); DestroyTexture(middle); DestroyTexture(end); Dispose(controller); }
        }

        [TestCase("LeftToRight")]
        [TestCase("RightToLeft")]
        public void P6_CurtainProducesVisibleProgressionAtQuarterHalfAndComplete(string direction)
        {
            object project = ProjectWithTwoBackgroundScenes("Curtain", direction);
            Type projectType = RequireType("VnSceneComposerProject");
            Type controllerType = RequirePlaybackType();
            object controller = NewController(controllerType, projectType, project);
            Texture2D quarter = null, half = null, complete = null;
            try
            {
                Invoke(controllerType, controller, "PlayScene", typeof(int), 1);
                Invoke(controllerType, controller, "Advance", typeof(float), .25f);
                quarter = ComposePlaybackBackground(Get(controller, "CurrentFrame"));
                Invoke(controllerType, controller, "Advance", typeof(float), .25f);
                half = ComposePlaybackBackground(Get(controller, "CurrentFrame"));
                Invoke(controllerType, controller, "Advance", typeof(float), .5f);
                complete = ComposePlaybackBackground(Get(controller, "CurrentFrame"));
                Assert.That(PixelSignature(quarter), Is.Not.EqualTo(PixelSignature(half)), direction + " curtain must visibly progress 25%→50%.");
                Assert.That(PixelSignature(half), Is.Not.EqualTo(PixelSignature(complete)), direction + " curtain must visibly progress 50%→100%.");
            }
            finally { DestroyTexture(quarter); DestroyTexture(half); DestroyTexture(complete); Dispose(controller); }
        }

        [Test]
        public void P6_SceneBoundaryUsesActualPreviousSceneMediaAsBackgroundSource()
        {
            Texture2D source = LoadRokasTexture("vnBusStopPhoneMessageMina");
            Texture2D target = LoadRokasTexture("vnNightSkyRain");
            object project = Activator.CreateInstance(RequireType("VnSceneComposerProject"));
            IList scenes = (IList)Get(project, "scenes");
            object first = Scene("Source", "source", "PreviewAutoDuration", 1f);
            object second = Scene("Target", "target", "PreviewAutoDuration", 1f);
            SetSceneBackground(first, source);
            SetSceneBackground(second, target);
            ConfigureBackgroundTransition(second, "Fade", 1f);
            scenes.Add(first);
            scenes.Add(second);
            Type projectType = RequireType("VnSceneComposerProject");
            Type controllerType = RequirePlaybackType();
            object controller = NewController(controllerType, projectType, project);
            try
            {
                Invoke(controllerType, controller, "PlayScene", typeof(int), 1);
                object frame = Get(controller, "CurrentFrame");
                Assert.That(Get(frame, "SourceBackground"), Is.SameAs(source),
                    "Scene 02 must transition FROM Scene 01's actual selected media, not a default/baseline background.");
                Assert.That(Get(frame, "TargetBackground"), Is.SameAs(target));
            }
            finally { Dispose(controller); }
        }

        [Test]
        public void P6_BounceChangesDrawableGeometryReturnsExactlyToBaselineAndRepeatsDeterministically()
        {
            object project = Activator.CreateInstance(RequireType("VnSceneComposerProject"));
            IList scenes = (IList)Get(project, "scenes");
            object from = Scene("From", "from", "PreviewAutoDuration", 1f);
            AddCharacter(from, "Mina", "mina_neutral", "Center");
            scenes.Add(from);
            object target = Scene("Bounce", "bounce", "PreviewAutoDuration", 1f);
            AddCharacter(target, "Mina", "mina_neutral", "Center");
            Set(Get(target, "transition"), "triggerActionBounce", true);
            ConfigureBounce(target, 72f, 1f, .12f);
            scenes.Add(target);
            Type projectType = RequireType("VnSceneComposerProject");
            Type controllerType = RequirePlaybackType();
            object controller = NewController(controllerType, projectType, project);
            try
            {
                Invoke(controllerType, controller, "PlayScene", typeof(int), 1);
                Rect baseline = FirstBody(controller);
                Invoke(controllerType, controller, "Advance", typeof(float), .5f);
                Rect midpoint = FirstBody(controller);
                Assert.That(midpoint, Is.Not.EqualTo(baseline));
                Invoke(controllerType, controller, "Restart");
                Assert.That(FirstBody(controller), Is.EqualTo(baseline), "Restart must clear bounce state.");
                Invoke(controllerType, controller, "Advance", typeof(float), .5f);
                Assert.That(FirstBody(controller), Is.EqualTo(midpoint), "Repeated playback must be deterministic.");
                Invoke(controllerType, controller, "Advance", typeof(float), .5f);
                Assert.That(FirstBody(controller), Is.EqualTo(baseline), "Bounce endpoint must return exactly to baseline.");
            }
            finally { Dispose(controller); }
        }

        [Test]
        public void P6_EnterExitAndInstantModeAffectTheDrawableCharacterState()
        {
            object project = Activator.CreateInstance(RequireType("VnSceneComposerProject"));
            IList scenes = (IList)Get(project, "scenes");
            scenes.Add(Scene("Empty", string.Empty, "PreviewAutoDuration", 1f));
            object enter = Scene("Enter", "enter", "PreviewAutoDuration", 1f);
            AddCharacter(enter, "Mina", "mina_neutral", "Center");
            ConfigureCharacterTransition(enter, "SlideAndFade", 1f, 1f, 180f);
            scenes.Add(enter);
            object exit = Scene("Exit", "exit", "PreviewAutoDuration", 1f);
            ConfigureCharacterTransition(exit, "SlideAndFade", 1f, 1f, 180f);
            scenes.Add(exit);
            object instant = Scene("Instant", "instant", "PreviewAutoDuration", 1f);
            AddCharacter(instant, "Mina", "mina_neutral", "Center");
            ConfigureCharacterTransition(instant, "Instant", 0f, 0f, 0f);
            scenes.Add(instant);
            Type projectType = RequireType("VnSceneComposerProject");
            Type controllerType = RequirePlaybackType();
            object controller = NewController(controllerType, projectType, project);
            try
            {
                Invoke(controllerType, controller, "PlayScene", typeof(int), 1);
                object enteringStart = FirstCharacter(controller);
                float enterStartAlpha = (float)Get(enteringStart, "Alpha");
                Rect enterStartBody = (Rect)Get(enteringStart, "Body");
                Invoke(controllerType, controller, "Advance", typeof(float), .5f);
                object enteringMiddle = FirstCharacter(controller);
                Assert.That((float)Get(enteringMiddle, "Alpha"), Is.GreaterThan(enterStartAlpha));
                Assert.That((Rect)Get(enteringMiddle, "Body"), Is.Not.EqualTo(enterStartBody));
                Invoke(controllerType, controller, "PlayScene", typeof(int), 2);
                float exitStartAlpha = (float)Get(FirstCharacter(controller), "Alpha");
                Invoke(controllerType, controller, "Advance", typeof(float), .5f);
                Assert.That((float)Get(FirstCharacter(controller), "Alpha"), Is.LessThan(exitStartAlpha));
                Invoke(controllerType, controller, "PlayScene", typeof(int), 3);
                Assert.That((float)Get(FirstCharacter(controller), "Alpha"), Is.EqualTo(1f).Within(.001f), "Instant mode must snap.");
            }
            finally { Dispose(controller); }
        }

        [Test]
        public void P6_OnboardedFullBodyPoseTransitionUsesDistinctTexturesAndZeroDurationSnaps()
        {
            string sourceA = null, sourceB = null, sourceC = null;
            string stableA = null, stableB = null, stableC = null;
            string assetA = null, assetB = null, assetC = null;
            string stateA = null, stateB = null, stateC = null;
            object controller = null;
            try
            {
                OnboardGeneratedPose("p6_pose_a_" + Guid.NewGuid().ToString("N"), new Color(.9f, .15f, .15f, .85f), out sourceA, out stableA, out assetA, out stateA);
                OnboardGeneratedPose("p6_pose_b_" + Guid.NewGuid().ToString("N"), new Color(.15f, .8f, .25f, .65f), out sourceB, out stableB, out assetB, out stateB);
                OnboardGeneratedPose("p6_pose_c_" + Guid.NewGuid().ToString("N"), new Color(.2f, .3f, .95f, .75f), out sourceC, out stableC, out assetC, out stateC);

                object project = Activator.CreateInstance(RequireType("VnSceneComposerProject"));
                IList scenes = (IList)Get(project, "scenes");
                object from = Scene("Pose A", "a", "PreviewAutoDuration", 1f);
                AddCharacter(from, "Mina", stateA, "Center");
                SetProperty(from, "speaker", "Mina");
                scenes.Add(from);
                object target = Scene("Pose B", "b", "PreviewAutoDuration", 1f);
                AddCharacter(target, "Mina", stateB, "Center");
                SetProperty(target, "speaker", "Mina");
                ConfigureExpression(target, 1f);
                scenes.Add(target);
                object snap = Scene("Pose C", "c", "PreviewAutoDuration", 1f);
                AddCharacter(snap, "Mina", stateC, "Center");
                SetProperty(snap, "speaker", "Mina");
                ConfigureExpression(snap, 0f);
                scenes.Add(snap);

                Type projectType = RequireType("VnSceneComposerProject");
                Type controllerType = RequirePlaybackType();
                controller = NewController(controllerType, projectType, project);
                Invoke(controllerType, controller, "PlayScene", typeof(int), 1);
                Invoke(controllerType, controller, "Advance", typeof(float), .5f);
                IList middle = (IList)Get(Get(controller, "CurrentFrame"), "ComposerCharacters");
                object poseA = middle.Cast<object>().FirstOrDefault(item => (string)Get(item, "StateId") == stateA);
                object poseB = middle.Cast<object>().FirstOrDefault(item => (string)Get(item, "StateId") == stateB);
                Assert.That(poseA, Is.Not.Null);
                Assert.That(poseB, Is.Not.Null);
                Assert.That(Get(poseA, "Texture"), Is.Not.SameAs(Get(poseB, "Texture")), "Full-body pose transition must use the two real authored textures.");
                Assert.That((Rect)Get(poseA, "Uv"), Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
                Assert.That((Rect)Get(poseB, "Uv"), Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
                Assert.That((float)Get(poseA, "Alpha"), Is.InRange(.01f, .99f));
                Assert.That((float)Get(poseB, "Alpha"), Is.InRange(.01f, .99f));

                Invoke(controllerType, controller, "PlayScene", typeof(int), 2);
                IList snapped = (IList)Get(Get(controller, "CurrentFrame"), "ComposerCharacters");
                object poseC = snapped.Cast<object>().FirstOrDefault(item => (string)Get(item, "StateId") == stateC);
                Assert.That(poseC, Is.Not.Null);
                Assert.That((float)Get(poseC, "Alpha"), Is.EqualTo(1f).Within(.001f));
                Assert.That(snapped.Cast<object>().Any(item => (string)Get(item, "StateId") == stateB && (float)Get(item, "Alpha") > .001f), Is.False,
                    "Zero-duration pose transition must snap without leaving the previous pose visible.");
            }
            finally
            {
                Dispose(controller);
                CleanupGeneratedPose(sourceA, stableA, assetA);
                CleanupGeneratedPose(sourceB, stableB, assetB);
                CleanupGeneratedPose(sourceC, stableC, assetC);
            }
        }

        [Test]
        public void P6_TypewriterStageAndFocusAreVisibleAndRestartIsDeterministic()
        {
            object project = Activator.CreateInstance(RequireType("VnSceneComposerProject"));
            IList scenes = (IList)Get(project, "scenes");
            object from = Scene("One", "A", "PreviewAutoDuration", 1f);
            AddCharacter(from, "Mina", "mina_neutral", "Center");
            SetProperty(from, "speaker", "Mina");
            scenes.Add(from);
            object target = Scene("Two", "Deterministic visible typewriter", "PreviewAutoDuration", 1f);
            AddCharacter(target, "Mina", "mina_neutral", "Left");
            AddCharacter(target, "Keiko", "keiko_neutral", "Right");
            SetProperty(target, "speaker", "Keiko");
            scenes.Add(target);
            Type projectType = RequireType("VnSceneComposerProject");
            Type controllerType = RequirePlaybackType();
            object controller = NewController(controllerType, projectType, project);
            try
            {
                Invoke(controllerType, controller, "PlayScene", typeof(int), 1);
                object startFrame = Get(controller, "CurrentFrame");
                string startText = (string)Get(startFrame, "Dialogue");
                IList startCharacters = (IList)Get(startFrame, "ComposerCharacters");
                Assert.That(startCharacters.Count, Is.EqualTo(2));
                Assert.That(((Rect)Get(startCharacters[0], "Body")).center.x, Is.Not.EqualTo(((Rect)Get(startCharacters[1], "Body")).center.x));
                Invoke(controllerType, controller, "Advance", typeof(float), .5f);
                object middleFrame = Get(controller, "CurrentFrame");
                string middleText = (string)Get(middleFrame, "Dialogue");
                IList middleCharacters = (IList)Get(middleFrame, "ComposerCharacters");
                Assert.That(middleText.Length, Is.GreaterThan(startText.Length));
                object mina = middleCharacters.Cast<object>().First(item => (string)Get(item, "CharacterId") == "Mina");
                object keiko = middleCharacters.Cast<object>().First(item => (string)Get(item, "CharacterId") == "Keiko");
                bool visiblyDifferent = !Mathf.Approximately((float)Get(mina, "Alpha"), (float)Get(keiko, "Alpha")) ||
                                         !Mathf.Approximately((float)Get(mina, "Brightness"), (float)Get(keiko, "Brightness")) ||
                                         ((Rect)Get(mina, "Body")).size != ((Rect)Get(keiko, "Body")).size;
                Assert.That(visiblyDifferent, Is.True, "Active speaker focus must visibly distinguish multiple characters.");
                string midpointText = middleText;
                Rect midpointMina = (Rect)Get(mina, "Body");
                Invoke(controllerType, controller, "Restart");
                Assert.That((string)Get(Get(controller, "CurrentFrame"), "Dialogue"), Is.EqualTo(startText));
                Invoke(controllerType, controller, "Advance", typeof(float), .5f);
                object repeatFrame = Get(controller, "CurrentFrame");
                object repeatMina = ((IList)Get(repeatFrame, "ComposerCharacters")).Cast<object>().First(item => (string)Get(item, "CharacterId") == "Mina");
                Assert.That((string)Get(repeatFrame, "Dialogue"), Is.EqualTo(midpointText));
                Assert.That((Rect)Get(repeatMina, "Body"), Is.EqualTo(midpointMina));
            }
            finally { Dispose(controller); }
        }

        [Test]
        public void P6_PlayScenePlayFromHereAndPlayAllUseTheSameSampledVisualPath()
        {
            object project = Activator.CreateInstance(RequireType("VnSceneComposerProject"));
            SetAutoPreviewSequenceGap(project, 0f);
            IList scenes = (IList)Get(project, "scenes");
            object first = Scene("First", "first", "PreviewAutoDuration", 1f);
            AddCharacter(first, "Mina", "mina_neutral", "Center");
            scenes.Add(first);
            object second = Scene("Second", "second", "PreviewAutoDuration", 1f);
            AddCharacter(second, "Mina", "mina_neutral", "Center");
            Set(Get(second, "transition"), "triggerActionBounce", true);
            ConfigureBounce(second, 50f, 1f, .08f);
            scenes.Add(second);
            Type projectType = RequireType("VnSceneComposerProject");
            Type controllerType = RequirePlaybackType();
            object controller = NewController(controllerType, projectType, project);
            try
            {
                Invoke(controllerType, controller, "PlayScene", typeof(int), 1);
                Invoke(controllerType, controller, "Advance", typeof(float), .5f);
                string playScene = DrawableSignature(Get(controller, "CurrentFrame"));
                Invoke(controllerType, controller, "PlayFromHere", typeof(int), 1);
                Invoke(controllerType, controller, "Advance", typeof(float), .5f);
                Assert.That(DrawableSignature(Get(controller, "CurrentFrame")), Is.EqualTo(playScene));
                Invoke(controllerType, controller, "PlayAll");
                Invoke(controllerType, controller, "Advance", typeof(float), 1f);
                Assert.That((int)Get(controller, "CurrentSceneIndex"), Is.EqualTo(1));
                Assert.That((float)Get(controller, "SceneElapsedSeconds"), Is.EqualTo(0f).Within(.0001f));
                Invoke(controllerType, controller, "Advance", typeof(float), .5f);
                Assert.That(DrawableSignature(Get(controller, "CurrentFrame")), Is.EqualTo(playScene),
                    "Play All must use the same scene-local sampled visual path as direct playback.");
            }
            finally { Dispose(controller); }
        }

        [Test]
        public void P6_LegacyPreviewButtonsProduceRealSceneComposerPlaybackFrames()
        {
            Type windowType = RequireType("VnPresentationWorkshopWindow");
            UnityEngine.Object window = ScriptableObject.CreateInstance(windowType);
            try
            {
                PropertyInfo realFrame = windowType.GetProperty("CurrentMotionPreviewFrame", BindingFlags.Public | BindingFlags.Instance);
                Assert.That(realFrame, Is.Not.Null,
                    "P6 legacy preview gap: Preview Bounce/Enter/Exit/Background/Focus/Expression must expose the real SC-G playback frame instead of only previewProgress.");
                string[] methods = { "PreviewBounce", "PreviewCharacterEnter", "PreviewCharacterExit", "PreviewBackgroundTransition", "PreviewSpeakerSwitch", "PreviewExpression" };
                foreach (string methodName in methods)
                {
                    MethodInfo method = windowType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                    Assert.That(method, Is.Not.Null, "Missing legacy preview method: " + methodName);
                    method.Invoke(window, null);
                    Assert.That(realFrame.GetValue(window, null), Is.Not.Null, methodName + " must route into real SC-G/SC-F playback.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }

        private static object ProjectWithTwoBackgroundScenes(string mode, string direction)
        {
            object project = Activator.CreateInstance(RequireType("VnSceneComposerProject"));
            IList scenes = (IList)Get(project, "scenes");
            object source = Scene("Source", "source", "PreviewAutoDuration", 1f);
            object target = Scene("Target", "target", "PreviewAutoDuration", 1f);
            SetSceneBackground(source, LoadRokasTexture("vnBusStopPhoneMessageMina"));
            SetSceneBackground(target, LoadRokasTexture("vnNightSkyRain"));
            ConfigureBackgroundTransition(target, mode, 1f);
            object transition = Get(Get(target, "presentationOverrides"), "backgroundTransition");
            Set(transition, "hasDirection", true);
            SetEnum(transition, "direction", direction);
            scenes.Add(source);
            scenes.Add(target);
            return project;
        }

        private static void SetSceneBackground(object scene, Texture2D texture)
        {
            Type editingType = RequireType("VnSceneComposerMediaEditing");
            Type sceneType = RequireType("VnSceneComposerScene");
            Type scaleType = RequireType("VnSceneComposerMediaScaleMode");
            MethodInfo method = RequireStatic(editingType, "SetExistingRokasAsset", sceneType, typeof(UnityEngine.Object), scaleType);
            method.Invoke(null, new object[] { scene, texture, Enum.Parse(scaleType, "Fill") });
        }

        private static Texture2D LoadRokasTexture(string fieldName)
        {
            Type rendererType = RequireType("VnPresentationWorkshopPreviewRenderer");
            object assets = RequireStatic(rendererType, "LoadAssets").Invoke(null, null);
            FieldInfo field = assets.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing authored RokasAssets texture: " + fieldName);
            Texture2D texture = field.GetValue(assets) as Texture2D;
            Assert.That(texture, Is.Not.Null, "Authored texture not loaded: " + fieldName);
            return texture;
        }

        private static Texture2D ComposePlaybackBackground(object playbackFrame)
        {
            Type rendererType = RequireType("VnPresentationWorkshopPreviewRenderer");
            Type playbackFrameType = RequireType("VnSceneComposerPlaybackFrame");
            MethodInfo compose = rendererType.GetMethod("ComposePlaybackBackground", BindingFlags.Public | BindingFlags.Static,
                null, new[] { playbackFrameType, typeof(int), typeof(int) }, null);
            Assert.That(compose, Is.Not.Null,
                "P6 renderer gap: existing Workshop renderer needs a shared playback background compositor used by Draw and rendered visual tests.");
            Texture2D texture = compose.Invoke(null, new object[] { playbackFrame, 64, 36 }) as Texture2D;
            Assert.That(texture, Is.Not.Null);
            return texture;
        }

        private static long PixelSignature(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            unchecked
            {
                long hash = 1469598103934665603L;
                for (int i = 0; i < pixels.Length; i++)
                {
                    hash ^= pixels[i].r; hash *= 1099511628211L;
                    hash ^= pixels[i].g; hash *= 1099511628211L;
                    hash ^= pixels[i].b; hash *= 1099511628211L;
                    hash ^= pixels[i].a; hash *= 1099511628211L;
                }
                return hash;
            }
        }

        private static void ConfigureBounce(object scene, float amplitude, float duration, float scaleEmphasis)
        {
            object bounce = Get(Get(scene, "presentationOverrides"), "actionBounce");
            Set(bounce, "hasAmplitude", true); Set(bounce, "amplitude", amplitude);
            Set(bounce, "hasDuration", true); Set(bounce, "duration", duration);
            Set(bounce, "hasScaleEmphasis", true); Set(bounce, "scaleEmphasis", scaleEmphasis);
        }

        private static void ConfigureExpression(object scene, float duration)
        {
            object expression = Get(Get(scene, "presentationOverrides"), "expressionTransition");
            Set(expression, "hasDuration", true);
            Set(expression, "duration", duration);
        }

        private static object FirstCharacter(object controller)
        {
            IList characters = (IList)Get(Get(controller, "CurrentFrame"), "ComposerCharacters");
            Assert.That(characters.Count, Is.GreaterThan(0));
            return characters[0];
        }

        private static Rect FirstBody(object controller) { return (Rect)Get(FirstCharacter(controller), "Body"); }

        private static string DrawableSignature(object playbackFrame)
        {
            string dialogue = (string)Get(playbackFrame, "Dialogue");
            IList characters = (IList)Get(playbackFrame, "ComposerCharacters");
            string[] parts = characters.Cast<object>().Select(item => string.Join("|",
                (string)Get(item, "CharacterId"), (string)Get(item, "StateId"),
                ((Rect)Get(item, "Body")).ToString("F3"),
                ((float)Get(item, "Alpha")).ToString("F4"),
                ((float)Get(item, "Brightness")).ToString("F4"))).ToArray();
            object bg = Get(playbackFrame, "ComposerBackgroundTransition");
            return dialogue + "::" + string.Join(";;", parts) + "::" +
                   ((float)Get(bg, "SourceAlpha")).ToString("F4") + ":" +
                   ((float)Get(bg, "TargetAlpha")).ToString("F4") + ":" +
                   ((float)Get(bg, "CurtainCoverage")).ToString("F4");
        }

        private static void OnboardGeneratedPose(string stateName, Color color,
            out string sourcePath, out string stableId, out string assetPath, out string stateId)
        {
            sourcePath = Path.Combine(Path.GetTempPath(), "rokas-p6-pose-" + Guid.NewGuid().ToString("N") + ".png");
            var texture = new Texture2D(8, 14, TextureFormat.RGBA32, false);
            Color[] pixels = Enumerable.Range(0, 8 * 14).Select(index =>
                new Color(color.r, color.g, color.b, index % 4 == 0 ? color.a * .4f : color.a)).ToArray();
            texture.SetPixels(pixels); texture.Apply();
            File.WriteAllBytes(sourcePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            Type libraryType = RequireType("VnSceneComposerAssetLibrary");
            Type purposeType = RequireType("VnSceneComposerAssetPurpose");
            MethodInfo onboard = RequireStatic(libraryType, "Onboard", typeof(string), typeof(string), purposeType,
                typeof(string), typeof(string), typeof(string));
            object result = onboard.Invoke(null, new object[]
            {
                Directory.GetParent(Application.dataPath).FullName,
                sourcePath,
                Enum.Parse(purposeType, "CharacterState"),
                stateName,
                "Mina",
                stateName
            });
            Assert.That((bool)Get(result, "Success"), Is.True, Get(result, "Error") as string);
            object entry = Get(result, "Entry");
            stableId = (string)Get(entry, "stableAssetId");
            assetPath = (string)Get(entry, "assetPath");
            stateId = (string)Get(entry, "stateId");
        }

        private static void CleanupGeneratedPose(string sourcePath, string stableId, string assetPath)
        {
            if (!string.IsNullOrWhiteSpace(stableId))
            {
                Type libraryType = RequireType("VnSceneComposerAssetLibrary");
                RequireStatic(libraryType, "Unregister", typeof(string), typeof(string)).Invoke(null,
                    new object[] { Directory.GetParent(Application.dataPath).FullName, stableId });
            }
            if (!string.IsNullOrWhiteSpace(assetPath)) AssetDatabase.DeleteAsset(assetPath);
            if (!string.IsNullOrWhiteSpace(sourcePath) && File.Exists(sourcePath)) File.Delete(sourcePath);
            AssetDatabase.Refresh();
        }

        private static void DestroyTexture(Texture2D texture)
        {
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
