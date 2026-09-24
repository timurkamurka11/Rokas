using System;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed partial class VnPresentationWorkshopWindow
    {
        public VnSceneComposerPlaybackFrame CurrentMotionPreviewFrame
        {
            get
            {
                UpdatePreviewClock();
                CompleteSceneComposerFocusedPreview();
                if (previewEffect == VnWorkshopPreviewEffect.None || previewEffect == VnWorkshopPreviewEffect.UiPress)
                    return null;
                if (_sceneComposerWorkspaceActive && _sceneComposerFocusedPreviewPreset == null)
                    return null;
                VnSceneComposerProject project = BuildMotionPreviewProject();
                using (var controller = new VnSceneComposerPlaybackController(project))
                {
                    controller.PlayScene(1);
                    float duration = Mathf.Max(.01f, previewDuration);
                    if (previewProgress > 0f) controller.Advance(duration * Mathf.Clamp01(previewProgress));
                    return controller.CurrentFrame;
                }
            }
        }

        private VnSceneComposerProject BuildMotionPreviewProject()
        {
            VnPresentationWorkshopPreset preset = ResolveMotionPreviewPreset();
            var project = new VnSceneComposerProject { defaultPresentation = preset ?? new VnPresentationWorkshopPreset() };
            var from = NewMotionPreviewScene("Legacy Preview Source", string.Empty);
            var target = NewMotionPreviewScene("Legacy Preview Target", PreviewSampleText);

            switch (previewEffect)
            {
                case VnWorkshopPreviewEffect.Expression:
                    AddMotionCharacter(from, "Mina", ResolveMinaExpression(expressionFromIndex), VnWorkshopStageSlot.Center);
                    AddMotionCharacter(target, "Mina", ResolveMinaExpression(expressionToIndex), VnWorkshopStageSlot.Center);
                    from.speaker = target.speaker = "Mina";
                    break;
                case VnWorkshopPreviewEffect.CharacterEnter:
                    AddMotionCharacter(target, "Mina", "mina_neutral", VnWorkshopStageSlot.Center);
                    target.speaker = "Mina";
                    break;
                case VnWorkshopPreviewEffect.CharacterExit:
                    AddMotionCharacter(from, "Mina", "mina_neutral", VnWorkshopStageSlot.Center);
                    from.speaker = "Mina";
                    break;
                case VnWorkshopPreviewEffect.Bounce:
                    AddMotionCharacter(from, "Mina", "mina_neutral", VnWorkshopStageSlot.Center);
                    AddMotionCharacter(target, "Mina", "mina_neutral", VnWorkshopStageSlot.Center);
                    from.speaker = target.speaker = "Mina";
                    target.transition.triggerActionBounce = true;
                    break;
                case VnWorkshopPreviewEffect.BackgroundTransition:
                    SetMotionPreviewBackground(from, backgroundSource);
                    SetMotionPreviewBackground(target, backgroundTarget);
                    break;
                case VnWorkshopPreviewEffect.SpeakerSwitch:
                    AddMotionCharacter(from, "Mina", "mina_neutral", VnWorkshopStageSlot.Left);
                    AddMotionCharacter(from, "Keiko", "keiko_neutral", VnWorkshopStageSlot.Right);
                    AddMotionCharacter(target, "Mina", "mina_neutral", VnWorkshopStageSlot.Left);
                    AddMotionCharacter(target, "Keiko", "keiko_neutral", VnWorkshopStageSlot.Right);
                    from.speaker = "Mina";
                    target.speaker = "Keiko";
                    break;
                case VnWorkshopPreviewEffect.StageOneTwo:
                    AddMotionCharacter(from, "Mina", "mina_neutral", VnWorkshopStageSlot.Center);
                    AddMotionCharacter(target, "Mina", "mina_neutral", VnWorkshopStageSlot.Left);
                    AddMotionCharacter(target, "Keiko", "keiko_neutral", VnWorkshopStageSlot.Right);
                    break;
                case VnWorkshopPreviewEffect.StageTwoOne:
                    AddMotionCharacter(from, "Mina", "mina_neutral", VnWorkshopStageSlot.Left);
                    AddMotionCharacter(from, "Keiko", "keiko_neutral", VnWorkshopStageSlot.Right);
                    AddMotionCharacter(target, "Mina", "mina_neutral", VnWorkshopStageSlot.Center);
                    break;
                default:
                    AddMotionCharacter(from, "Mina", "mina_neutral", VnWorkshopStageSlot.Center);
                    AddMotionCharacter(target, "Mina", "mina_neutral", VnWorkshopStageSlot.Center);
                    from.speaker = target.speaker = "Mina";
                    break;
            }
            project.scenes.Add(from);
            project.scenes.Add(target);
            return project;
        }

        private VnSceneComposerScene NewMotionPreviewScene(string label, string text)
        {
            return new VnSceneComposerScene
            {
                label = label,
                previewText = text ?? string.Empty,
                narration = false,
                timing = new VnSceneComposerTiming
                {
                    previewAdvanceMode = VnSceneComposerPreviewAdvanceMode.PreviewAutoDuration,
                    previewAutoDuration = Mathf.Max(.01f, previewDuration)
                }
            };
        }

        private static void AddMotionCharacter(VnSceneComposerScene scene, string character, string stateId, VnWorkshopStageSlot slot)
        {
            scene.characters.Add(new VnSceneComposerCharacter
            {
                characterId = character,
                stateId = stateId,
                stageSlot = slot
            });
        }

        private static string ResolveMinaExpression(int index)
        {
            if (MinaExpressionIds == null || MinaExpressionIds.Length == 0) return "mina_neutral";
            return MinaExpressionIds[Mathf.Clamp(index, 0, MinaExpressionIds.Length - 1)];
        }

        private static void SetMotionPreviewBackground(VnSceneComposerScene scene, VnWorkshopPreviewScene previewScene)
        {
            Rokas.Presentation.RokasAssets assets = VnPresentationWorkshopPreviewRenderer.LoadAssets();
            Texture2D texture;
            switch (previewScene)
            {
                case VnWorkshopPreviewScene.NightSkyKeiko: texture = assets.vnNightSkyRain; break;
                case VnWorkshopPreviewScene.PhoneMessage: texture = assets.vnBusStopPhoneMessageMina; break;
                default: texture = assets.vnBusStopRainNight; break;
            }
            VnSceneComposerMediaEditing.SetExistingRokasAsset(scene, texture, VnSceneComposerMediaScaleMode.Fill);
        }
    }
}
