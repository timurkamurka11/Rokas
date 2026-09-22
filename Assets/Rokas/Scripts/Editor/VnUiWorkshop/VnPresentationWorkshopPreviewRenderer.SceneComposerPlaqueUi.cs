using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public static partial class VnPresentationWorkshopPreviewRenderer
    {
        private static void DrawSceneComposerPlaqueUi(Rect localCanvas, VnWorkshopPreviewFrame frame)
        {
            VnSceneComposerPlaqueUiSample sample=frame.PlaqueUi??new VnSceneComposerPlaqueUiSample();
            VnSceneComposerPlaqueLayout layout=VnSceneComposerPlaqueUi.BuildLayout(frame);
            DrawPlaqueControl(localCanvas,frame,layout.Mute,sample.Muted?"MUTED":"MUTE",VnSceneComposerPlaqueControl.Mute,sample);
            DrawPlaqueControl(localCanvas,frame,layout.Forward,"▶",VnSceneComposerPlaqueControl.Forward,sample);
            DrawPlaqueControl(localCanvas,frame,layout.Menu,"MENU",VnSceneComposerPlaqueControl.Menu,sample);
            if(sample.CompletionIndicatorVisible)
            {
                float pulse=VnSceneComposerPlaqueUi.CompletionPulse(sample.UnscaledTime);
                Rect rect=LogicalToPreview(localCanvas,ScalePlaqueRect(layout.CompletionIndicator,pulse),frame);
                Color old=GUI.color; GUI.color=new Color(.84f,.94f,1f,.96f);
                DrawText(rect,"▼",frame.DialogueFont,25,FontStyle.Bold,TextAnchor.MiddleCenter); GUI.color=old;
            }
            if(sample.MenuOpen) DrawSceneComposerMenuOverlay(localCanvas,frame,layout,sample);
        }

        private static void DrawPlaqueControl(Rect localCanvas,VnWorkshopPreviewFrame frame,Rect logical,string label,
            VnSceneComposerPlaqueControl control,VnSceneComposerPlaqueUiSample sample)
        {
            VnSceneComposerPlaqueVisualState state=VnSceneComposerPlaqueUi.ResolveVisualState(control,sample);
            float p=state==VnSceneComposerPlaqueVisualState.Release?sample.ReleaseProgress:1f;
            VnSceneComposerPlaqueFeedbackSample f=VnSceneComposerPlaqueUi.SampleFeedback(state,p);
            Rect transformed=ScalePlaqueRect(logical,f.Scale); transformed.position+=f.Offset;
            Rect rect=LogicalToPreview(localCanvas,transformed,frame); Color old=GUI.color;
            if(f.Glow>.001f){GUI.color=new Color(.56f,.82f,1f,f.Glow*.52f);GUI.DrawTexture(ExpandPlaqueRect(rect,4f),Texture2D.whiteTexture);}
            GUI.color=new Color(.07f*f.Brightness,.15f*f.Brightness,.23f*f.Brightness,.78f*f.Alpha); GUI.DrawTexture(rect,Texture2D.whiteTexture);
            GUI.color=new Color(.68f,.87f,1f,.84f*f.Alpha); DrawOutline(rect,1f);
            GUI.color=new Color(f.Brightness,f.Brightness,f.Brightness,f.Alpha);
            DrawText(rect,label,frame.DialogueFont,label.Length<=2?25:13,FontStyle.Bold,TextAnchor.MiddleCenter); GUI.color=old;
        }

        private static void DrawSceneComposerMenuOverlay(Rect localCanvas,VnWorkshopPreviewFrame frame,
            VnSceneComposerPlaqueLayout layout,VnSceneComposerPlaqueUiSample sample)
        {
            Color old=GUI.color; GUI.color=new Color(.01f,.025f,.045f,.78f);
            GUI.DrawTexture(LogicalToPreview(localCanvas,layout.MenuOverlay,frame),Texture2D.whiteTexture);
            Rect card=LogicalToPreview(localCanvas,layout.MenuCard,frame); GUI.color=new Color(.035f,.075f,.12f,.97f);
            GUI.DrawTexture(card,Texture2D.whiteTexture); GUI.color=new Color(.58f,.84f,1f,.9f); DrawOutline(card,2f);
            Rect title=new Rect(card.x+card.width*.12f,card.y+card.height*.07f,card.width*.76f,card.height*.14f);
            GUI.color=Color.white; DrawText(title,"ROKAS // MENU",frame.DialogueFont,23,FontStyle.Bold,TextAnchor.MiddleCenter);
            DrawPlaqueMenuRow(localCanvas,frame,layout.Continue,VnSceneComposerPlaqueUi.ContinueLabel,true,sample);
            DrawPlaqueMenuRow(localCanvas,frame,layout.Settings,VnSceneComposerPlaqueUi.SettingsLabel,false,sample);
            DrawPlaqueMenuRow(localCanvas,frame,layout.Save,VnSceneComposerPlaqueUi.SaveLabel,false,sample);
            DrawPlaqueMenuRow(localCanvas,frame,layout.MainMenu,VnSceneComposerPlaqueUi.MainMenuLabel,false,sample); GUI.color=old;
        }

        private static void DrawPlaqueMenuRow(Rect localCanvas,VnWorkshopPreviewFrame frame,Rect logical,string label,bool active,VnSceneComposerPlaqueUiSample sample)
        {
            Rect rect=LogicalToPreview(localCanvas,logical,frame);
            VnSceneComposerPlaqueVisualState state=active?VnSceneComposerPlaqueUi.ResolveVisualState(VnSceneComposerPlaqueControl.Continue,sample):VnSceneComposerPlaqueVisualState.Disabled;
            VnSceneComposerPlaqueFeedbackSample f=VnSceneComposerPlaqueUi.SampleFeedback(state,state==VnSceneComposerPlaqueVisualState.Release?sample.ReleaseProgress:1f);
            rect=ScalePlaqueRect(rect,f.Scale);rect.position+=f.Offset;
            GUI.color=active?new Color(.07f*f.Brightness,.15f*f.Brightness,.23f*f.Brightness,.94f):new Color(.055f,.07f,.085f,.76f);GUI.DrawTexture(rect,Texture2D.whiteTexture);
            GUI.color=active?new Color(.60f,.86f,1f,.9f):new Color(.32f,.40f,.46f,.64f);DrawOutline(rect,1f);
            GUI.color=active?new Color(1f,1f,1f,f.Alpha):new Color(.66f,.70f,.74f,.64f);DrawText(rect,label,frame.DialogueFont,17,FontStyle.Bold,TextAnchor.MiddleCenter);
        }

        private static Rect ScalePlaqueRect(Rect rect,float scale){Vector2 size=rect.size*Mathf.Max(.01f,scale);return new Rect(rect.center-size*.5f,size);}
        private static Rect ExpandPlaqueRect(Rect rect,float amount){return new Rect(rect.x-amount,rect.y-amount,rect.width+amount*2f,rect.height+amount*2f);}
    }
}
