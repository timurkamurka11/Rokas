using System;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public enum VnSceneComposerPlaqueControl
    {
        None, Mute, Forward, Menu, CompletionIndicator, Continue
    }

    public enum VnSceneComposerPlaqueVisualState
    {
        Normal, Hover, Pressed, Release, Disabled
    }

    public struct VnSceneComposerPlaqueFeedbackSample
    {
        public float Scale;
        public Vector2 Offset;
        public float Brightness;
        public float Alpha;
        public float Glow;
    }

    public sealed class VnSceneComposerPlaqueUiSample
    {
        public bool Muted;
        public bool MenuOpen;
        public bool CompletionIndicatorVisible;
        public VnSceneComposerPlaqueControl Hovered;
        public VnSceneComposerPlaqueControl Pressed;
        public VnSceneComposerPlaqueControl Released;
        public float ReleaseProgress = 1f;
        public float UnscaledTime;
    }

    public sealed class VnSceneComposerPlaqueLayout
    {
        public Rect DialoguePanel, Mute, Forward, Menu, CompletionIndicator;
        public Rect MenuOverlay, MenuCard, Continue, Settings, Save, MainMenu;

        public Rect GetRect(VnSceneComposerPlaqueControl control)
        {
            switch (control)
            {
                case VnSceneComposerPlaqueControl.Mute: return Mute;
                case VnSceneComposerPlaqueControl.Forward: return Forward;
                case VnSceneComposerPlaqueControl.Menu: return Menu;
                case VnSceneComposerPlaqueControl.CompletionIndicator: return CompletionIndicator;
                case VnSceneComposerPlaqueControl.Continue: return Continue;
                default: return Rect.zero;
            }
        }
    }

    public static class VnSceneComposerPlaqueUi
    {
        public const string ContinueLabel = "Продолжить";
        public const string SettingsLabel = "Настройки";
        public const string SaveLabel = "Сохранение";
        public const string MainMenuLabel = "Главное меню";

        public static VnSceneComposerPlaqueLayout BuildLayout(VnWorkshopPreviewFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            Rect panel = frame.DialoguePanel;
            float buttonSize = Mathf.Clamp(panel.height * .14f, 56f, 84f);
            float buttonY = panel.y + panel.height * .735f - buttonSize * .5f;
            Rect overlay = new Rect(0f, 0f, frame.VirtualCanvasSize.x, frame.VirtualCanvasSize.y);
            float cardWidth = Mathf.Min(720f, frame.VirtualCanvasSize.x * .58f);
            float cardHeight = Mathf.Min(500f, frame.VirtualCanvasSize.y * .58f);
            Rect card = new Rect((frame.VirtualCanvasSize.x-cardWidth)*.5f,
                (frame.VirtualCanvasSize.y-cardHeight)*.5f, cardWidth, cardHeight);
            float rowHeight = Mathf.Clamp(cardHeight * .145f, 52f, 76f);
            float rowWidth = cardWidth * .72f;
            float rowX = card.center.x - rowWidth * .5f;
            float firstY = card.yMax - cardHeight * .29f;
            return new VnSceneComposerPlaqueLayout
            {
                DialoguePanel=panel,
                Mute=new Rect(panel.x+panel.width*.56f-buttonSize*.5f,buttonY,buttonSize,buttonSize),
                Forward=new Rect(panel.x+panel.width*.72f-buttonSize*.5f,buttonY,buttonSize,buttonSize),
                Menu=new Rect(panel.x+panel.width*.88f-buttonSize*.5f,buttonY,buttonSize,buttonSize),
                CompletionIndicator=new Rect(panel.xMax-Mathf.Max(36f,panel.width*.035f),
                    panel.y+Mathf.Max(12f,panel.height*.055f),Mathf.Max(24f,panel.width*.024f),Mathf.Max(24f,panel.width*.024f)),
                MenuOverlay=overlay, MenuCard=card,
                Continue=new Rect(rowX,firstY,rowWidth,rowHeight),
                Settings=new Rect(rowX,firstY-rowHeight*1.22f,rowWidth,rowHeight),
                Save=new Rect(rowX,firstY-rowHeight*2.44f,rowWidth,rowHeight),
                MainMenu=new Rect(rowX,firstY-rowHeight*3.66f,rowWidth,rowHeight)
            };
        }

        public static VnSceneComposerPlaqueControl HitTest(VnWorkshopPreviewFrame frame,
            Vector2 logicalPoint, VnSceneComposerPlaqueUiSample sample)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            sample = sample ?? new VnSceneComposerPlaqueUiSample();
            VnSceneComposerPlaqueLayout layout = BuildLayout(frame);
            if (sample.MenuOpen)
                return layout.Continue.Contains(logicalPoint) ? VnSceneComposerPlaqueControl.Continue : VnSceneComposerPlaqueControl.None;
            if (sample.CompletionIndicatorVisible && layout.CompletionIndicator.Contains(logicalPoint))
                return VnSceneComposerPlaqueControl.CompletionIndicator;
            if (layout.Mute.Contains(logicalPoint)) return VnSceneComposerPlaqueControl.Mute;
            if (layout.Forward.Contains(logicalPoint)) return VnSceneComposerPlaqueControl.Forward;
            if (layout.Menu.Contains(logicalPoint)) return VnSceneComposerPlaqueControl.Menu;
            return VnSceneComposerPlaqueControl.None;
        }

        public static VnSceneComposerPlaqueVisualState ResolveVisualState(
            VnSceneComposerPlaqueControl control, VnSceneComposerPlaqueUiSample sample)
        {
            if (control == VnSceneComposerPlaqueControl.None) return VnSceneComposerPlaqueVisualState.Disabled;
            sample = sample ?? new VnSceneComposerPlaqueUiSample();
            if (sample.Pressed == control) return VnSceneComposerPlaqueVisualState.Pressed;
            if (sample.Released == control && sample.ReleaseProgress < 1f) return VnSceneComposerPlaqueVisualState.Release;
            if (sample.Hovered == control) return VnSceneComposerPlaqueVisualState.Hover;
            return VnSceneComposerPlaqueVisualState.Normal;
        }

        public static VnSceneComposerPlaqueFeedbackSample SampleFeedback(
            VnSceneComposerPlaqueVisualState state, float unscaledProgress)
        {
            float t=Mathf.Clamp01(unscaledProgress);
            float eased=1f-((1f-t)*(1f-t));
            switch(state)
            {
                case VnSceneComposerPlaqueVisualState.Hover:
                    return new VnSceneComposerPlaqueFeedbackSample{Scale=Mathf.Lerp(1f,1.045f,eased),Brightness=Mathf.Lerp(1f,1.12f,eased),Alpha=1f,Glow=Mathf.Lerp(0f,.34f,eased)};
                case VnSceneComposerPlaqueVisualState.Pressed:
                    return new VnSceneComposerPlaqueFeedbackSample{Scale=.94f,Offset=new Vector2(0f,-3f),Brightness=.90f,Alpha=1f,Glow=.46f};
                case VnSceneComposerPlaqueVisualState.Release:
                    return new VnSceneComposerPlaqueFeedbackSample{Scale=Mathf.Lerp(.94f,1f,eased),Offset=Vector2.Lerp(new Vector2(0f,-3f),Vector2.zero,eased),Brightness=Mathf.Lerp(.90f,1f,eased),Alpha=1f,Glow=Mathf.Lerp(.46f,0f,eased)};
                case VnSceneComposerPlaqueVisualState.Disabled:
                    return new VnSceneComposerPlaqueFeedbackSample{Scale=1f,Brightness=.72f,Alpha=.55f,Glow=0f};
                default:
                    return new VnSceneComposerPlaqueFeedbackSample{Scale=1f,Brightness=1f,Alpha=1f,Glow=0f};
            }
        }

        public static float CompletionPulse(float unscaledTime)
        {
            return 1f + Mathf.Sin(Mathf.Max(0f, unscaledTime) * 7.5f) * .075f;
        }
    }
}
