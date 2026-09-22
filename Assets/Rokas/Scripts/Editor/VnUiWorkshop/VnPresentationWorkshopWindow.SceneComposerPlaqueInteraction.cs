using System;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed partial class VnPresentationWorkshopWindow
    {
        private const float SceneComposerPlaqueReleaseSeconds=.12f;
        [NonSerialized] private VnSceneComposerPlaqueControl _sceneComposerPlaqueHovered;
        [NonSerialized] private VnSceneComposerPlaqueControl _sceneComposerPlaquePressed;
        [NonSerialized] private VnSceneComposerPlaqueControl _sceneComposerPlaqueReleased;
        [NonSerialized] private double _sceneComposerPlaqueReleaseStarted;

        private void ConfigureSceneComposerPlaqueUi(VnWorkshopPreviewFrame frame,bool staticAuthoringPreview)
        {
            if(frame==null||!frame.IsSceneComposer)return;
            double now=EditorApplication.timeSinceStartup;float releaseProgress=1f;
            if(_sceneComposerPlaqueReleased!=VnSceneComposerPlaqueControl.None)
            {
                releaseProgress=Mathf.Clamp01((float)((now-_sceneComposerPlaqueReleaseStarted)/SceneComposerPlaqueReleaseSeconds));
                if(releaseProgress>=1f)_sceneComposerPlaqueReleased=VnSceneComposerPlaqueControl.None;else Repaint();
            }
            VnSceneComposerPlaybackFrame pf=!staticAuthoringPreview&&_sceneComposerPlayback!=null?_sceneComposerPlayback.CurrentFrame:null;
            frame.PlaqueUi=new VnSceneComposerPlaqueUiSample{
                Muted=pf!=null&&pf.IsMuted,MenuOpen=pf!=null&&pf.IsMenuOpen,
                CompletionIndicatorVisible=pf!=null&&pf.DialogueCompleteIndicatorVisible,
                Hovered=_sceneComposerPlaqueHovered,Pressed=_sceneComposerPlaquePressed,Released=_sceneComposerPlaqueReleased,
                ReleaseProgress=releaseProgress,UnscaledTime=(float)now};
            if(frame.PlaqueUi.CompletionIndicatorVisible)Repaint();
        }

        private bool TryHandleSceneComposerPlaquePointer(Rect previewRect,VnWorkshopPreviewFrame frame,Event currentEvent,bool playbackInput)
        {
            if(frame==null||!frame.IsSceneComposer||currentEvent==null)return false;
            if(currentEvent.button!=0&&currentEvent.type!=EventType.MouseMove&&currentEvent.type!=EventType.Repaint)return false;
            bool inside=previewRect.Contains(currentEvent.mousePosition);
            if(!inside)
            {
                if(currentEvent.type==EventType.MouseMove&&_sceneComposerPlaqueHovered!=VnSceneComposerPlaqueControl.None){_sceneComposerPlaqueHovered=VnSceneComposerPlaqueControl.None;Repaint();}
                return false;
            }
            VnSceneComposerPlaqueUiSample sample=frame.PlaqueUi??new VnSceneComposerPlaqueUiSample();
            Vector2 logical=VnPresentationWorkshopPreviewRenderer.PreviewToLogical(previewRect,currentEvent.mousePosition,frame);
            VnSceneComposerPlaqueControl hit=VnSceneComposerPlaqueUi.HitTest(frame,logical,sample);
            if(currentEvent.type==EventType.MouseMove)
            {
                if(_sceneComposerPlaqueHovered!=hit){_sceneComposerPlaqueHovered=hit;Repaint();}
                return false;
            }
            if(currentEvent.type==EventType.MouseDown)
            {
                if(sample.MenuOpen)
                {
                    _sceneComposerPlaquePressed=hit;_sceneComposerPlaqueHovered=hit;currentEvent.Use();Repaint();return true;
                }
                if(hit==VnSceneComposerPlaqueControl.None)return false;
                _sceneComposerPlaquePressed=hit;_sceneComposerPlaqueHovered=hit;currentEvent.Use();Repaint();return true;
            }
            if(currentEvent.type!=EventType.MouseUp)return false;
            if(sample.MenuOpen)
            {
                VnSceneComposerPlaqueControl pressed=_sceneComposerPlaquePressed;_sceneComposerPlaquePressed=VnSceneComposerPlaqueControl.None;
                if(pressed==VnSceneComposerPlaqueControl.Continue&&hit==VnSceneComposerPlaqueControl.Continue&&playbackInput&&_sceneComposerPlayback!=null)
                {_sceneComposerPlayback.CloseMenu();FinishSceneComposerPlaqueRelease(VnSceneComposerPlaqueControl.Continue);}
                currentEvent.Use();Repaint();return true;
            }
            if(_sceneComposerPlaquePressed==VnSceneComposerPlaqueControl.None)return false;
            VnSceneComposerPlaqueControl control=_sceneComposerPlaquePressed;_sceneComposerPlaquePressed=VnSceneComposerPlaqueControl.None;
            if(control==hit&&playbackInput&&_sceneComposerPlayback!=null)
            {
                switch(control)
                {
                    case VnSceneComposerPlaqueControl.Mute:_sceneComposerPlayback.ToggleMute();break;
                    case VnSceneComposerPlaqueControl.Forward:
                    case VnSceneComposerPlaqueControl.CompletionIndicator:ComposerAdvanceDialogue();break;
                    case VnSceneComposerPlaqueControl.Menu:_sceneComposerPlayback.OpenMenu();break;
                }
            }
            FinishSceneComposerPlaqueRelease(control);currentEvent.Use();Repaint();return true;
        }

        private void FinishSceneComposerPlaqueRelease(VnSceneComposerPlaqueControl control)
        {_sceneComposerPlaqueReleased=control;_sceneComposerPlaqueReleaseStarted=EditorApplication.timeSinceStartup;}
    }
}
