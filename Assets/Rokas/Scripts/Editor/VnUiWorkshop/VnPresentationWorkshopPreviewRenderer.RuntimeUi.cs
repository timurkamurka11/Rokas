using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public static partial class VnPresentationWorkshopPreviewRenderer
    {
        private sealed class ButtonMotion
        {
            public double ChangedAt;
            public float FromScale = 1f, ToScale = 1f, FromLight = 1f, ToLight = 1f;
            public bool Held;
            public float Scale(double now) { return Mathf.Lerp(FromScale, ToScale, Ease(now)); }
            public float Light(double now) { return Mathf.Lerp(FromLight, ToLight, Ease(now)); }
            private float Ease(double now) { return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((float)(now-ChangedAt)/.09f)); }
            public void Target(bool hover, bool pressed, double now)
            {
                float scale = pressed ? .96f : 1f, light = hover ? 1.25f : 1f;
                if (scale == ToScale && light == ToLight) return;
                FromScale = Scale(now); FromLight = Light(now); ToScale = scale; ToLight = light; ChangedAt = now;
            }
        }
        private static readonly ConditionalWeakTable<VnSceneComposerPlaybackController, Dictionary<int, ButtonMotion>> ControlMotion =
            new ConditionalWeakTable<VnSceneComposerPlaybackController, Dictionary<int, ButtonMotion>>();

        private static void DrawComposerRuntimeControls(Rect canvas, VnWorkshopPreviewFrame frame)
        {
            if (!frame.IsComposerFrame) return;
            PlaybackFrames.TryGetValue(frame, out VnSceneComposerPlaybackFrame playback);
            var owner = playback != null ? playback.Owner : null;
            var layout = VnSceneComposerRuntimeUi.Layout(frame);
            bool enabled = owner != null && owner.IsPlaying && !owner.IsMenuOpen;
            DrawPlaqueButton(canvas, frame, layout.Mute, 0, "Звук", owner, enabled, owner != null && owner.IsMuted);
            DrawPlaqueButton(canvas, frame, layout.Forward, 1, "Далее", owner, enabled && !owner.IsSceneTransitionActive, false);
            DrawPlaqueButton(canvas, frame, layout.Menu, 2, "Меню", owner, enabled, false);
            if (owner == null || !owner.ShowCompletionIndicator) return;
            var sample = VnSceneComposerRuntimeUi.SampleTriangle(owner.UiElapsedSeconds);
            Rect hit = LogicalToPreview(canvas, layout.Triangle, frame);
            Rect visual = hit;
            visual.position += new Vector2(0f, sample.OffsetY * canvas.height / frame.VirtualCanvasSize.y);
            Vector2 size = visual.size * sample.Scale;
            visual = new Rect(visual.center - size * .5f, size);
            Texture2D triangle = VnSceneComposerRuntimeUi.CompletionTriangle;
            if (triangle != null)
            {
                Color before = GUI.color;
                GUI.color = VnSceneComposerRuntimeUi.CompletionIndicatorColor(sample.Alpha);
                GUI.DrawTextureWithTexCoords(visual, triangle, VnSceneComposerRuntimeUi.TriangleUv, true);
                GUI.color = before;
            }
            using (new EditorGUI.DisabledScope(!enabled))
                if (GUI.Button(hit, new GUIContent(string.Empty,"Реплика завершена — далее"), GUIStyle.none)) owner.RequestAdvance(owner.InputTick);
        }

        private static void DrawPlaqueButton(Rect canvas, VnWorkshopPreviewFrame frame, Rect logical, int index,
            string tooltip, VnSceneComposerPlaybackController owner, bool enabled, bool muted)
        {
            Rect hit = LogicalToPreview(canvas, logical, frame);
            bool hover = enabled && hit.Contains(Event.current.mousePosition);
            double now = EditorApplication.timeSinceStartup;
            ButtonMotion motion = new ButtonMotion();
            if (owner != null)
            {
                var states = ControlMotion.GetOrCreateValue(owner);
                if (!states.TryGetValue(index, out motion)) states[index] = motion = new ButtonMotion();
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && hover) motion.Held = true;
                if (Event.current.rawType == EventType.MouseUp || !enabled) motion.Held = false;
                motion.Target(hover, hover && motion.Held, now);
            }
            float scale = motion.Scale(now);
            Vector2 size = hit.size * scale;
            Rect rect = new Rect(hit.center - size * .5f, size);
            float alpha = owner == null || enabled ? (muted ? .65f : 1f) : .42f;
            Texture2D sheet = VnSceneComposerRuntimeUi.ControlSheet;
            if (sheet != null)
            {
                Color before = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTextureWithTexCoords(rect, sheet, VnSceneComposerRuntimeUi.ButtonUv(index), true);
                GUI.color = before;
            }
            if (hover)
            {
                Color before = Handles.color;
                Handles.color = new Color(.35f, .8f, 1f, .24f);
                Handles.DrawWireDisc(rect.center, Vector3.forward, rect.width * .52f);
                Handles.color = before;
            }
            using (new EditorGUI.DisabledScope(!enabled))
            {
                if (!GUI.Button(hit, new GUIContent(string.Empty, tooltip), GUIStyle.none) || owner == null) return;
                if (index == 0) owner.SetMuted(!owner.IsMuted);
                else if (index == 1) owner.RequestAdvance(owner.InputTick);
                else owner.SetMenuOpen(true);
            }
        }

        private static void DrawComposerMenu(Rect canvas, VnWorkshopPreviewFrame frame)
        {
            if (!PlaybackFrames.TryGetValue(frame, out VnSceneComposerPlaybackFrame playback) || playback.Owner == null || !playback.Owner.IsMenuOpen) return;
            var owner = playback.Owner;
            Color old = GUI.color;
            GUI.color = new Color(.012f,.024f,.045f,.88f);
            GUI.DrawTexture(canvas,Texture2D.whiteTexture);
            float width=Mathf.Min(canvas.width*.68f,440f), height=Mathf.Min(canvas.height*.84f,410f);
            Rect panel=new Rect(canvas.center.x-width*.5f,canvas.center.y-height*.5f,width,height);
            GUI.color = new Color(.035f,.085f,.13f,.98f); GUI.DrawTexture(panel,Texture2D.whiteTexture);
            GUI.color = new Color(.2f,.75f,1f,1f); DrawOutline(panel,1.5f);
            GUI.color=Color.white;
            var title=new GUIStyle(EditorStyles.boldLabel) { font=frame.DialogueFont,fontSize=Mathf.RoundToInt(Mathf.Clamp(canvas.height*.044f,16f,24f)),alignment=TextAnchor.MiddleLeft };
            title.normal.textColor=new Color(.62f,.9f,1f);
            GUI.Label(new Rect(panel.x+24,panel.y+18,panel.width-48,32),"ROKAS  /  ПАУЗА",title);
            float row=Mathf.Min(48f,(height-90f)/4f), gap=8f;
            string[] labels={"Продолжить","Настройки  ·  Скоро","Сохранение  ·  Скоро","Главное меню  ·  Скоро"};
            var style=new GUIStyle(GUI.skin.button) { font=frame.DialogueFont,fontSize=Mathf.RoundToInt(Mathf.Clamp(row*.35f,12,18)),alignment=TextAnchor.MiddleLeft,padding=new RectOffset(16,10,4,4) };
            for(int i=0;i<labels.Length;i++)
            {
                Rect button=new Rect(panel.x+24,panel.y+64+i*(row+gap),panel.width-48,row);
                using(new EditorGUI.DisabledScope(i!=0)) if(GUI.Button(button,labels[i],style) && i==0) owner.SetMenuOpen(false);
            }
            GUI.color=old;
            if(Event.current.type==EventType.KeyDown && Event.current.keyCode==KeyCode.Escape) { owner.SetMenuOpen(false); Event.current.Use(); }
            // IMGUI modal layer: consume every remaining pointer/key event before the
            // preview's dialogue/drag handler can receive it. No Stop/Restart path.
            if(Event.current.isMouse || Event.current.isKey || Event.current.type==EventType.ScrollWheel) Event.current.Use();
        }
    }
}
