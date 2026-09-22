using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rokas.EditorTools.VnUiWorkshop;
using UnityEngine;

namespace Rokas.EditorTools.Tests
{
    public sealed class VnSceneComposerPrereleasePlaqueUiTests
    {
        [Test]
        public void Transition_CoverKeepsOutgoingFrameVisibleUntilFullCover()
        {
            VnSceneComposerProject project = TwoSceneProject(
                VnSceneComposerSceneTransitionType.DarkCurtain, 1f);
            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayFromHere(0);
                controller.AdvanceDialogue();

                Assert.That(controller.IsSceneTransitionActive, Is.True);
                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(0));
                Assert.That(controller.CurrentFrame, Is.Not.Null);
                Assert.That(controller.CurrentFrame.ShowDialoguePanel, Is.True,
                    "Outgoing plaque must stay visible while the curtain is covering it.");
                Assert.That(controller.CurrentFrame.ShowCharacters, Is.True,
                    "Outgoing characters must stay visible until full cover.");
                Assert.That(controller.CurrentFrame.ShowDialogueText, Is.True,
                    "Outgoing dialogue must stay visible until full cover.");
            }
        }

        [Test]
        public void Transition_NoneProducesOneCoherentIncomingFrame()
        {
            VnSceneComposerProject project = TwoSceneProject(
                VnSceneComposerSceneTransitionType.None, 0f);
            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayFromHere(0);
                controller.AdvanceDialogue();

                Assert.That(controller.IsSceneTransitionActive, Is.False);
                Assert.That(controller.CurrentSceneIndex, Is.EqualTo(1));
                Assert.That(controller.CurrentFrame.WorkshopFrame.Speaker, Is.EqualTo("Mina"));
                Assert.That(controller.CurrentFrame.ShowDialoguePanel, Is.True);
                Assert.That(controller.CurrentFrame.ShowCharacters, Is.True,
                    "Transition=None must not reveal an intermediate frame with incoming characters hidden.");
                Assert.That(controller.CurrentFrame.ShowDialogueText, Is.True);
            }
        }

        [Test]
        public void DialogueCompleteIndicator_HiddenWhileTyping_ThenVisibleAfterCompletion()
        {
            VnSceneComposerProject project = OneSceneProject("Keiko", "ABCD");
            project.defaultPresentation.typewriter.hasEnabled = true;
            project.defaultPresentation.typewriter.enabled = true;
            project.defaultPresentation.typewriter.hasCharactersPerSecond = true;
            project.defaultPresentation.typewriter.charactersPerSecond = 1f;

            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayScene(0);
                controller.Advance(.2f);
                Assert.That(GetFrameBool(controller.CurrentFrame, "DialogueCompleteIndicatorVisible"), Is.False);

                controller.AdvanceDialogue();
                Assert.That(GetFrameBool(controller.CurrentFrame, "DialogueCompleteIndicatorVisible"), Is.True,
                    "The readiness triangle must appear immediately when the current Beat is fully revealed.");
            }
        }

        [Test]
        public void DialogueCompleteIndicator_DisappearsImmediatelyOnNextBeat()
        {
            VnSceneComposerProject project = OneSceneProject("Keiko", string.Empty);
            project.scenes[0].dialogueBeats.Add(new VnSceneComposerDialogueBeat
            {
                speaker = "Mina",
                text = "Next beat"
            });

            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayScene(0);
                controller.Advance(.3f);
                Assert.That(GetFrameBool(controller.CurrentFrame, "DialogueCompleteIndicatorVisible"), Is.True);

                controller.AdvanceDialogue();
                Assert.That(controller.CurrentBeatIndex, Is.EqualTo(1));
                Assert.That(GetFrameBool(controller.CurrentFrame, "DialogueCompleteIndicatorVisible"), Is.False);
            }
        }

        [Test]
        public void MuteToggle_IsWholePlaybackStateAndDoesNotRestartSession()
        {
            VnSceneComposerProject project = OneSceneProject("Keiko", "Text");
            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayScene(0);
                int musicStarts = controller.MusicStartCount;
                int additionalStarts = controller.AdditionalAudioStartCount;

                Invoke(controller, "ToggleMute");
                Assert.That(GetControllerBool(controller, "IsMuted"), Is.True);
                Assert.That(controller.MusicStartCount, Is.EqualTo(musicStarts));
                Assert.That(controller.AdditionalAudioStartCount, Is.EqualTo(additionalStarts));

                Invoke(controller, "ToggleMute");
                Assert.That(GetControllerBool(controller, "IsMuted"), Is.False);
                Assert.That(controller.MusicStartCount, Is.EqualTo(musicStarts));
                Assert.That(controller.AdditionalAudioStartCount, Is.EqualTo(additionalStarts));
            }
        }

        [Test]
        public void MenuPause_FreezesPresentationAndContinueResumesExactState()
        {
            VnSceneComposerProject project = OneSceneProject("Keiko", "Text");
            using (var controller = new VnSceneComposerPlaybackController(project))
            {
                controller.PlayScene(0);
                controller.Advance(.3f);
                float before = controller.BeatElapsedSeconds;

                Invoke(controller, "OpenMenu");
                Assert.That(GetControllerBool(controller, "IsMenuOpen"), Is.True);
                Assert.That(controller.IsPlaying, Is.False);

                controller.Advance(1f);
                Assert.That(controller.BeatElapsedSeconds, Is.EqualTo(before).Within(.0001f),
                    "Menu pause must freeze the VN presentation clock.");

                Invoke(controller, "CloseMenu");
                Assert.That(GetControllerBool(controller, "IsMenuOpen"), Is.False);
                Assert.That(controller.IsPlaying, Is.True);
                Assert.That(controller.BeatElapsedSeconds, Is.EqualTo(before).Within(.0001f));
            }
        }

        [Test]
        public void PlaqueModel_DefinesOnlyMuteForwardMenuAndCompletionIndicator()
        {
            string source = ReadEditorSource("VnSceneComposerPlaqueUi.cs");
            Assert.That(source, Does.Contain("Mute"));
            Assert.That(source, Does.Contain("Forward"));
            Assert.That(source, Does.Contain("Menu"));
            Assert.That(source, Does.Contain("CompletionIndicator"));
            Assert.That(source, Does.Not.Contain("VnSceneComposerPlaqueControl.Back"));
        }

        [Test]
        public void PlaqueLayout_HasDedicatedResolutionIndependentLogicalHitRegions()
        {
            string source = ReadEditorSource("VnSceneComposerPlaqueUi.cs");
            Assert.That(source, Does.Contain("BuildLayout"));
            Assert.That(source, Does.Contain("DialoguePanel"));
            Assert.That(source, Does.Contain("HitTest"));
            Assert.That(source, Does.Contain("VnSceneComposerPlaqueControl.Forward"));
            Assert.That(source, Does.Contain("VnSceneComposerPlaqueControl.Menu"));
        }

        [Test]
        public void PreviewInput_PlaqueControlsWinBeforeCharacterDrag()
        {
            string source = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposerAuthoring.cs");
            string method = ExtractMethodBody(source, "private void HandleSceneComposerPreviewInput");
            int plaque = method.IndexOf("TryHandleSceneComposerPlaquePointer", StringComparison.Ordinal);
            int drag = method.IndexOf("ComposerSelectPreviewObjectAt", StringComparison.Ordinal);

            Assert.That(plaque, Is.GreaterThanOrEqualTo(0),
                "Static authoring preview must offer the same plaque-control hit model.");
            Assert.That(drag, Is.GreaterThan(plaque),
                "Plaque controls must consume pointer input before object/character drag can begin.");
        }

        [Test]
        public void PlaybackInput_UsesPressReleaseAndConsumesPlaqueActions()
        {
            string authoring = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposerAuthoring.cs");
            string method = ExtractMethodBody(authoring, "private void HandleSceneComposerPlaybackInput");
            string interaction = ReadEditorSource("VnPresentationWorkshopWindow.SceneComposerPlaqueInteraction.cs");
            string pointer = ExtractMethodBody(interaction, "private bool TryHandleSceneComposerPlaquePointer");

            Assert.That(method, Does.Contain("TryHandleSceneComposerPlaquePointer"));
            Assert.That(pointer, Does.Contain("EventType.MouseDown"));
            Assert.That(pointer, Does.Contain("EventType.MouseUp"));
            int mouseUpGate = method.IndexOf("currentEvent.type != EventType.MouseUp", StringComparison.Ordinal);
            int genericAdvance = method.IndexOf("ComposerAdvanceDialogue();", StringComparison.Ordinal);
            Assert.That(mouseUpGate, Is.GreaterThanOrEqualTo(0));
            Assert.That(genericAdvance, Is.GreaterThan(mouseUpGate),
                "Generic panel advance is allowed only after the MouseUp gate; plaque MouseDown is consumed first.");
        }

        [Test]
        public void Renderer_DrawsStandardPlaqueControlsAndNeverDrawsRuntimeBack()
        {
            string source = ReadEditorSource("VnPresentationWorkshopPreviewRenderer.cs");
            Assert.That(source, Does.Contain("DrawSceneComposerPlaqueUi"));
            Assert.That(source, Does.Not.Contain(
                "DrawText(LogicalToPreview(localCanvas, frame.Back, frame), \"‹\""),
                "Scene Composer plaque path must not render a runtime Back control.");
        }

        [Test]
        public void DefaultPlaque_UsesCanonicalBlueRokasAssetWhileCustomOverrideRemains()
        {
            string source = ReadEditorSource("VnPresentationWorkshopPreviewRenderer.cs");
            Assert.That(source, Does.Contain("useCanonicalRokasPlaque"));
            Assert.That(source, Does.Contain("assets.vnDialoguePanelKeikoDark"));
            Assert.That(source, Does.Contain("visual.hasAssetGuid"),
                "Explicit custom PNG plaque support must remain authoritative.");
        }

        [Test]
        public void MenuOverlay_ContainsRequiredVisibleBlocks()
        {
            string source = ReadEditorSource("VnSceneComposerPlaqueUi.cs");
            Assert.That(source, Does.Contain("Продолжить"));
            Assert.That(source, Does.Contain("Настройки"));
            Assert.That(source, Does.Contain("Сохранение"));
            Assert.That(source, Does.Contain("Главное меню"));
        }

        [Test]
        public void PlaqueFeedback_UsesNormalHoverPressedReleaseStates()
        {
            string source = ReadEditorSource("VnSceneComposerPlaqueUi.cs");
            Assert.That(source, Does.Contain("Normal"));
            Assert.That(source, Does.Contain("Hover"));
            Assert.That(source, Does.Contain("Pressed"));
            Assert.That(source, Does.Contain("Release"));
            Assert.That(source, Does.Contain("unscaledProgress"));
        }

        private static VnSceneComposerProject OneSceneProject(string speaker, string text)
        {
            var project = new VnSceneComposerProject();
            project.scenes.Add(new VnSceneComposerScene
            {
                label = "Scene 1",
                dialogueBeats = new System.Collections.Generic.List<VnSceneComposerDialogueBeat>
                {
                    new VnSceneComposerDialogueBeat { speaker = speaker, text = text }
                }
            });
            return project;
        }

        private static VnSceneComposerProject TwoSceneProject(
            VnSceneComposerSceneTransitionType transitionType, float duration)
        {
            VnSceneComposerProject project = OneSceneProject("Keiko", string.Empty);
            project.scenes.Add(new VnSceneComposerScene
            {
                label = "Scene 2",
                transition = new VnSceneComposerTransition
                {
                    sceneTransitionType = transitionType,
                    sceneTransitionDuration = duration
                },
                dialogueBeats = new System.Collections.Generic.List<VnSceneComposerDialogueBeat>
                {
                    new VnSceneComposerDialogueBeat { speaker = "Mina", text = string.Empty }
                }
            });
            return project;
        }

        private static bool GetFrameBool(VnSceneComposerPlaybackFrame frame, string property)
        {
            Assert.That(frame, Is.Not.Null);
            PropertyInfo info = typeof(VnSceneComposerPlaybackFrame).GetProperty(
                property, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(info, Is.Not.Null, "Missing playback-frame property: " + property);
            return (bool)info.GetValue(frame);
        }

        private static bool GetControllerBool(VnSceneComposerPlaybackController controller, string property)
        {
            PropertyInfo info = typeof(VnSceneComposerPlaybackController).GetProperty(
                property, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(info, Is.Not.Null, "Missing playback-controller property: " + property);
            return (bool)info.GetValue(controller);
        }

        private static void Invoke(VnSceneComposerPlaybackController controller, string method)
        {
            MethodInfo info = typeof(VnSceneComposerPlaybackController).GetMethod(
                method, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(info, Is.Not.Null, "Missing playback-controller method: " + method);
            info.Invoke(controller, null);
        }

        private static string ReadEditorSource(string fileName)
        {
            string path = Path.Combine(
                Application.dataPath, "Rokas", "Scripts", "Editor", "VnUiWorkshop", fileName);
            Assert.That(File.Exists(path), Is.True, "Missing inspected editor source: " + path);
            return File.ReadAllText(path);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(signatureIndex, Is.GreaterThanOrEqualTo(0), "Missing method signature: " + signature);
            int openBrace = source.IndexOf('{', signatureIndex);
            Assert.That(openBrace, Is.GreaterThanOrEqualTo(0));
            int depth = 0;
            for (int i = openBrace; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0) return source.Substring(openBrace, i - openBrace + 1);
                }
            }

            Assert.Fail("Unterminated method body: " + signature);
            return string.Empty;
        }
    }
}
