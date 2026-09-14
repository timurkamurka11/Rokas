using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
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
                "P6 renderer gap: Scene Composer must pass the full VnSceneComposerPlaybackFrame to the existing Workshop renderer so background/source/target transition context is not discarded.");
        }

        [Test]
        public void P6_BackgroundFadeProducesDifferentComposedPixelsAtStartMidAndEnd()
        {
            object project = ProjectWithTwoBackgroundScenes("Fade", "RightToLeft");
            Type projectType = RequireType("VnSceneComposerProject");
            Type controllerType = RequirePlaybackType();
            object controller = NewController(controllerType, projectType, project);
            Texture2D start = null;
            Texture2D middle = null;
            Texture2D end = null;
            try
            {
                Invoke(controllerType, controller, "PlayScene", typeof(int), 1);
                start = ComposePlaybackBackground(Get(controller, "CurrentFrame"));
                Invoke(controllerType, controller, "Advance", typeof(float), .5f);
                middle = ComposePlaybackBackground(Get(controller, "CurrentFrame"));
                Invoke(controllerType, controller, "Advance", typeof(float), .5f);
                end = ComposePlaybackBackground(Get(controller, "CurrentFrame"));

                long startSignature = PixelSignature(start);
                long middleSignature = PixelSignature(middle);
                long endSignature = PixelSignature(end);
                Assert.That(middleSignature, Is.Not.EqualTo(startSignature), "Fade midpoint must be visibly different from its source frame.");
                Assert.That(endSignature, Is.Not.EqualTo(middleSignature), "Fade endpoint must visibly finish on the target frame.");
                Assert.That(endSignature, Is.Not.EqualTo(startSignature), "Fade must not render the same static background for the entire transition.");
            }
            finally
            {
                DestroyTexture(start); DestroyTexture(middle); DestroyTexture(end); Dispose(controller);
            }
        }

        [TestCase("LeftToRight")]
        [TestCase("RightToLeft")]
        public void P6_CurtainProducesVisibleProgressionAtQuarterHalfAndComplete(string direction)
        {
            object project = ProjectWithTwoBackgroundScenes("Curtain", direction);
            Type projectType = RequireType("VnSceneComposerProject");
            Type controllerType = RequirePlaybackType();
            object controller = NewController(controllerType, projectType, project);
            Texture2D quarter = null;
            Texture2D half = null;
            Texture2D complete = null;
            try
            {
                Invoke(controllerType, controller, "PlayScene", typeof(int), 1);
                Invoke(controllerType, controller, "Advance", typeof(float), .25f);
                quarter = ComposePlaybackBackground(Get(controller, "CurrentFrame"));
                Invoke(controllerType, controller, "Advance", typeof(float), .25f);
                half = ComposePlaybackBackground(Get(controller, "CurrentFrame"));
                Invoke(controllerType, controller, "Advance", typeof(float), .5f);
                complete = ComposePlaybackBackground(Get(controller, "CurrentFrame"));

                Assert.That(PixelSignature(quarter), Is.Not.EqualTo(PixelSignature(half)), direction + " curtain must visibly progress from 25% to 50%.");
                Assert.That(PixelSignature(half), Is.Not.EqualTo(PixelSignature(complete)), direction + " curtain must visibly progress from 50% to 100%.");
            }
            finally
            {
                DestroyTexture(quarter); DestroyTexture(half); DestroyTexture(complete); Dispose(controller);
            }
        }

        [Test]
        public void P6_SceneBoundaryUsesActualPreviousSceneMediaAsBackgroundSource()
        {
            Texture2D source = LoadRokasTexture("vnBusStopRainNight");
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
                    "Scene 02 must transition FROM Scene 01's actual selected media, not a baseline/default background.");
                Assert.That(Get(frame, "TargetBackground"), Is.SameAs(target),
                    "Scene 02 transition target must be its actual selected media.");
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
                Rect restarted = FirstBody(controller);
                Assert.That(restarted, Is.EqualTo(baseline), "Restart must clear bounce transform/scale completely.");
                Invoke(controllerType, controller, "Advance", typeof(float), .5f);
                Rect repeatedMidpoint = FirstBody(controller);
                Assert.That(repeatedMidpoint, Is.EqualTo(midpoint), "Repeated playback must sample the exact same bounce frame.");
                Invoke(controllerType, controller, "Advance", typeof(float), .5f);
                Assert.That(FirstBody(controller), Is.EqualTo(baseline), "Bounce endpoint must be the exact baseline with no accumulation.");
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
                object exitingStart = FirstCharacter(controller);
                float exitStartAlpha = (float)Get(exitingStart, "Alpha");
                Invoke(controllerType, controller, "Advance", typeof(float), .5f);
                object exitingMiddle = FirstCharacter(controller);
                Assert.That((float)Get(exitingMiddle, "Alpha"), Is.LessThan(exitStartAlpha));

                Invoke(controllerType, controller, "PlayScene", typeof(int), 3);
                object instantCharacter = FirstCharacter(controller);
                Assert.That((float)Get(instantCharacter, "Alpha"), Is.EqualTo(1f).Within(.001f), "Instant mode must snap to the final alpha immediately.");
            }
            finally { Dispose(controller); }
        }

        [Test]
        public void P6_AuthoredPoseTransitionCrossfadesRealStatesAndZeroDurationSnaps()
        {
            object project = Activator.CreateInstance(RequireType("VnSceneComposerProject"));
            IList scenes = (IList)Get(project, "scenes");
            object from = Scene("Neutral", "neutral", "PreviewAutoDuration", 1f);
            AddCharacter(from, "Mina", "mina_neutral", "Center");
            from.GetType().GetField("speaker").SetValue(from, "Mina");
            scenes.Add(from);
            object target = Scene("Happy", "happy", "PreviewAutoDuration", 1f);
            AddCharacter(target, "Mina", "mina_happy", "Center");
            target.GetType().GetField("speaker").SetValue(target, "Mina");
            ConfigureExpression(target, 1f);
            scenes.Add(target);
            object snap = Scene("Serious", "serious", "PreviewAutoDuration", 1f);
            AddCharacter(snap, "Mina", "mina_serious", "Center");
            snap.GetType().GetField("speaker").SetValue(snap, "Mina");
            ConfigureExpression(snap, 0f);
            scenes.Add(snap);

            Type projectType = RequireType("VnSceneComposerProject");
            Type controllerType = RequirePlaybackType();
            object controller = NewController(controllerType, projectType, project);
            try
            {
                Invoke(controllerType, controller, "PlayScene", typeof(int), 1);
                Invoke(controllerType, controller, "Advance", typeof(float), .5f);
                IList middle = (IList)Get(Get(controller, "CurrentFrame"), "ComposerCharacters");
                Assert.That(middle.Count, Is.GreaterThanOrEqualTo(2), "Pose crossfade must draw both authored state images at midpoint.");
                object happy = middle.Cast<object>().FirstOrDefault(item => (string)Get(item, "StateId") == "mina_happy");
                object neutral = middle.Cast<object>().FirstOrDefault(item => (string)Get(item, "StateId") == "mina_neutral");
                Assert.That(happy, Is.Not.Null);
                Assert.That(neutral, Is.Not.Null);
                Assert.That((Rect)Get(happy, "Uv"), Is.Not.EqualTo((Rect)Get(neutral, "Uv")), "Authored states must use their actual distinct image/UV state, not skeletal morphing.");
                Assert.That((float)Get(happy, "Alpha"), Is.InRange(.01f, .99f));
                Assert.That((float)Get(neutral, "Alpha"), Is.InRange(.01f, .99f));

                Invoke(controllerType, controller, "PlayScene", typeof(int), 2);
                IList snapped = (IList)Get(Get(controller, "CurrentFrame"), "ComposerCharacters");
                object serious = snapped.Cast<object>().FirstOrDefault(item => (string)Get(item, "StateId") == "mina_serious");
                Assert.That(serious, Is.Not.Null);
                Assert.That((float)Get(serious, "Alpha"), Is.EqualTo(1f).Within(.001f));
                Assert.That(snapped.Cast<object>().Any(item => (string)Get(item, "StateId") == "mina_happy" && (float)Get(item, "Alpha") > .001f), Is.False,
                    "Zero-duration expression transition must snap without leaving the previous pose visible.");
            }
            finally { Dispose(controller); }
        }

        [Test]
        public void P6_TypewriterStageAndFocusAreVisibleAndRestartIsDeterministic()
        {
            object project = Activator.CreateInstance(RequireType("VnSceneComposerProject"));
            IList scenes = (IList)Get(project, "scenes");
            object from = Scene("One", "A", "PreviewAutoDuration", 1f);
            AddCharacter(from, "Mina", "mina_neutral", "Center");
            from.GetType().GetField("speaker").SetValue(from, "Mina");
            scenes.Add(from);
            object target = Scene("Two", "Deterministic visible typewriter", "PreviewAutoDuration", 1f);
            AddCharacter(target, "Mina", "mina_neutral", "Left");
            AddCharacter(target, "Keiko", "keiko_neutral", "Right");
            target.GetType().GetField("speaker").SetValue(target, "Keiko");
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
                Rect start0 = (Rect)Get(startCharacters[0], "Body");
                Rect start1 = (Rect)Get(startCharacters[1], "Body");
                Assert.That(start0.center.x, Is.Not.EqualTo(start1.center.x), "Two-character stage must draw characters at distinct positions.");

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
                Assert.That(visiblyDifferent, Is.True, "Active speaker focus must visibly distinguish the two characters.");

                string midpointText = middleText;
                Rect midpointMina = (Rect)Get(mina, "Body");
                Invoke(controllerType, controller, "Restart");
                Assert.That((string)Get(Get(controller, "CurrentFrame"), "Dialogue"), Is.EqualTo(startText), "Restart must reset text reveal to zero.");
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
                string playFromHere = DrawableSignature(Get(controller, "CurrentFrame"));
                Assert.That(playFromHere, Is.EqualTo(playScene));

                Invoke(controllerType, controller, "PlayAll");
                Invoke(controllerType, controller, "Advance", typeof(float), 1.5f);
                Assert.That((int)Get(controller, "CurrentSceneIndex"), Is.EqualTo(1));
                string playAll = DrawableSignature(Get(controller, "CurrentFrame"));
                Assert.That(playAll, Is.EqualTo(playScene), "Play All must render the same authored midpoint as direct scene playback.");
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
                    "P6 legacy preview gap: Preview Bounce/Enter/Exit/Background/Focus/Expression must expose the real SC-G playback frame instead of only advancing previewProgress.");

                string[] methods = { "PreviewBounce", "PreviewCharacterEnter", "PreviewCharacterExit", "PreviewBackgroundTransition", "PreviewSpeakerSwitch", "PreviewExpression" };
                foreach (string methodName in methods)
                {
                    MethodInfo method = windowType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                    Assert.That(method, Is.Not.Null, "Missing legacy preview button method: " + methodName);
                    method.Invoke(window, null);
                    Assert.That(realFrame.GetValue(window, null), Is.Not.Null, methodName + " must route into the real SC-G/SC-F playback path.");
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
            SetSceneBackground(source, LoadRokasTexture("vnBusStopRainNight"));
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
            MethodInfo load = RequireStatic(rendererType, "LoadAssets");
            object assets = load.Invoke(null, null);
            Assert.That(assets, Is.Not.Null);
            FieldInfo field = assets.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "Missing authored RokasAssets texture: " + fieldName);
            Texture2D texture = field.GetValue(assets) as Texture2D;
            Assert.That(texture, Is.Not.Null, "Authored RokasAssets texture was not loaded: " + fieldName);
            return texture;
        }

        private static Texture2D ComposePlaybackBackground(object playbackFrame)
        {
            Type rendererType = RequireType("VnPresentationWorkshopPreviewRenderer");
            Type playbackFrameType = RequireType("VnSceneComposerPlaybackFrame");
            MethodInfo compose = rendererType.GetMethod("ComposePlaybackBackground", BindingFlags.Public | BindingFlags.Static,
                null, new[] { playbackFrameType, typeof(int), typeof(int) }, null);
            Assert.That(compose, Is.Not.Null,
                "P6 renderer gap: existing Workshop renderer needs a shared playback background compositor used by both Draw and rendered visual tests.");
            Texture2D texture = compose.Invoke(null, new object[] { playbackFrame, 64, 36 }) as Texture2D;
            Assert.That(texture, Is.Not.Null);
            return texture;
        }

        private static long PixelSignature(Texture2D texture)
        {
            Assert.That(texture, Is.Not.Null);
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

        private static Rect FirstBody(object controller)
        {
            return (Rect)Get(FirstCharacter(controller), "Body");
        }

        private static string DrawableSignature(object playbackFrame)
        {
            string dialogue = (string)Get(playbackFrame, "Dialogue");
            IList characters = (IList)Get(playbackFrame, "ComposerCharacters");
            string[] parts = characters.Cast<object>()
                .Select(item => string.Join("|",
                    (string)Get(item, "CharacterId"),
                    (string)Get(item, "StateId"),
                    ((Rect)Get(item, "Body")).ToString("F3"),
                    ((float)Get(item, "Alpha")).ToString("F4"),
                    ((float)Get(item, "Brightness")).ToString("F4")))
                .ToArray();
            object bg = Get(playbackFrame, "ComposerBackgroundTransition");
            return dialogue + "::" + string.Join(";;", parts) + "::" +
                   ((float)Get(bg, "SourceAlpha")).ToString("F4") + ":" +
                   ((float)Get(bg, "TargetAlpha")).ToString("F4") + ":" +
                   ((float)Get(bg, "CurtainCoverage")).ToString("F4");
        }

        private static void DestroyTexture(Texture2D texture)
        {
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
