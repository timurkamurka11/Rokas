using System;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Candidate
{
    /// <summary>
    /// ROKAS's isolated, real-time RUSSIAN sleep timer watch face.
    /// Informed by the supplied dark moon/cloud/blue-violet ring reference.
    /// Renders to the authentic watch OLED mesh (not a world-space HUD).
    /// Keeps production V10 SmartwatchOLEDPresenter completely untouched.
    /// The existing ArgonWatchControls reflection contract is preserved.
    /// </summary>
    public enum ArgonRussianWatchScreen { Dial, Presets, Confirmed, FallingAsleep, Cancelled }

    [DisallowMultipleComponent]
    public sealed class ArgonRussianSleepOledPresenter : MonoBehaviour
    {
        public const int Size = 512;
        public RenderTexture OledRenderTexture { get; private set; }
        public Texture2D OledDynamicTexture { get; private set; }
        public int SelectedHalfHours { get; private set; } = 14;
        public bool IsConfirmed { get; private set; }
        public ArgonRussianWatchScreen Screen { get; private set; } =
            ArgonRussianWatchScreen.Dial;
        public int CurrentHour { get; private set; } = 19;
        public int CurrentMinute { get; private set; } = 30;
        public int WakeMinutes => (CurrentHour * 60 + CurrentMinute +
            SelectedHalfHours * 30) % 1440;
        public string LastRenderedTextSummary { get; private set; }
        private Color32[] pixels;
        private float displaySteps = 14f, animatedSeconds;
        private float lastDrawn = -100f;
        private bool requiresRedraw = true;
        private readonly Color32 white = new Color32(244, 248, 255, 255);
        private readonly Color32 blue = new Color32(70, 145, 255, 255);
        private readonly Color32 violet = new Color32(135, 73, 255, 255);
        private readonly Color32 muted = new Color32(154, 176, 217, 255);

        public void InitializeOled()
        {
            if (OledRenderTexture == null)
            {
                OledRenderTexture = new RenderTexture(Size,Size,0,RenderTextureFormat.ARGB32)
                {
                    name = "RT_ROKAS_Argon_Russian_MoonOLED_512",
                    antiAliasing = 1,
                    filterMode = FilterMode.Bilinear
                };
                OledRenderTexture.Create();
            }
            if (OledDynamicTexture == null)
            {
                OledDynamicTexture = new Texture2D(Size,Size,TextureFormat.RGBA32,false)
                {
                    name = "ROKAS_Argon_Russian_MoonOLED",
                    wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
                };
                pixels = new Color32[Size*Size];
            }
            RenderAt(0f, true);
        }

        public void ConfigureTime(int currentHour, int currentMinute, int selectedHalfHours)
        {
            currentHour = (currentHour % 24 + 24) % 24;
            currentMinute = Mathf.Clamp(currentMinute,0,59);
            selectedHalfHours = Mathf.Clamp(selectedHalfHours,1,24);
            if (CurrentHour != currentHour || CurrentMinute != currentMinute ||
                SelectedHalfHours != selectedHalfHours)
                requiresRedraw = true;
            CurrentHour = currentHour;
            CurrentMinute = currentMinute;
            SelectedHalfHours = selectedHalfHours;
            if (!IsConfirmed && (Screen == ArgonRussianWatchScreen.Confirmed ||
                                 Screen == ArgonRussianWatchScreen.Cancelled))
                Screen = ArgonRussianWatchScreen.Dial;
            RefreshTextSummary();
            if (OledDynamicTexture != null) RenderAt(animatedSeconds,true);
        }

        public void SetConfirmedState(bool confirmed)
        {
            if (IsConfirmed != confirmed) requiresRedraw = true;
            IsConfirmed = confirmed;
            if (confirmed) Screen = ArgonRussianWatchScreen.Confirmed;
            else if (Screen == ArgonRussianWatchScreen.Confirmed)
                Screen = ArgonRussianWatchScreen.Dial;
            RefreshTextSummary();
            if (OledDynamicTexture != null) RenderAt(animatedSeconds,true);
        }

        public void ShowPresets()
        {
            Screen = ArgonRussianWatchScreen.Presets;
            RefreshTextSummary();
            requiresRedraw = true;
            RenderAt(animatedSeconds,true);
        }

        public void SelectPreset(int halfHours)
        {
            Screen = ArgonRussianWatchScreen.Dial;
            ConfigureTime(CurrentHour,CurrentMinute,Mathf.Clamp(halfHours,1,24));
        }

        public void SetFallingAsleep()
        {
            Screen = ArgonRussianWatchScreen.FallingAsleep;
            RefreshTextSummary();
            requiresRedraw = true;
            RenderAt(animatedSeconds,true);
        }

        public void CancelSelection()
        {
            IsConfirmed = false;
            Screen = ArgonRussianWatchScreen.Cancelled;
            RefreshTextSummary();
            requiresRedraw = true;
            RenderAt(animatedSeconds,true);
        }

        private void RefreshTextSummary()
        {
            LastRenderedTextSummary = "СЕЙЧАС " + CurrentHour.ToString("D2") +
                ":" + CurrentMinute.ToString("D2") + " ВРЕМЯ СНА " +
                DurationText + " ПОДЪЁМ " + WakeText + " " +
                (Screen == ArgonRussianWatchScreen.Confirmed
                    ? "УСТАНОВЛЕНО" :
                  Screen == ArgonRussianWatchScreen.FallingAsleep
                    ? "ЗАСЫПАЮ" :
                  Screen == ArgonRussianWatchScreen.Cancelled
                    ? "ОТМЕНЕНО" :
                  Screen == ArgonRussianWatchScreen.Presets
                    ? "БЫСТРЫЙ ВЫБОР" : "ПОДТВЕРДИТЬ");
        }

        public string DurationText =>
            (SelectedHalfHours/2).ToString("D2") + ":" +
            ((SelectedHalfHours%2)*30).ToString("D2");
        public string WakeText => (WakeMinutes/60).ToString("D2") + ":" +
            (WakeMinutes%60).ToString("D2");

        // Called deterministically by EditMode QA and each 15 Hz runtime tick.
        // 512x512 pixel texture is NOT uploaded 60 times per second.
        public void RenderAt(float timeSeconds, bool force = false)
        {
            animatedSeconds = Mathf.Max(0f,timeSeconds);
            if (OledDynamicTexture == null) return;
            displaySteps = Mathf.Lerp(displaySteps,SelectedHalfHours,
                force ? .45f : .20f);
            if (!force && !requiresRedraw && animatedSeconds-lastDrawn < 1f/15f)
                return;
            lastDrawn = animatedSeconds;
            requiresRedraw = false;
            PaintBackground();
            PaintClouds();
            PaintMoon();
            PaintRing();
            switch (Screen)
            {
                case ArgonRussianWatchScreen.Presets:
                    PaintPresets(); break;
                case ArgonRussianWatchScreen.Confirmed:
                    PaintConfirmation(); break;
                case ArgonRussianWatchScreen.FallingAsleep:
                    PaintFallingAsleep(); break;
                case ArgonRussianWatchScreen.Cancelled:
                    PaintCancelled(); break;
                default:
                    PaintSelection(); break;
            }
            OledDynamicTexture.SetPixels32(pixels);
            OledDynamicTexture.Apply(false,false);
            Graphics.Blit(OledDynamicTexture,OledRenderTexture);
        }

        private void PaintBackground()
        {
            float tt=animatedSeconds;
            for(int y=0;y<Size;y++)
            for(int x=0;x<Size;x++)
            {
                float dx=(x-256f)/256f, dy=(y-266f)/256f;
                float r=Mathf.Sqrt(dx*dx+dy*dy);
                float haze=Mathf.Clamp01((1.15f-r)*.55f);
                float wave=.5f+.5f*Mathf.Sin((dx+dy*.36f)*4f+tt*.22f);
                float blueHaze=haze*(.5f+.5f*wave);
                float cloudBand=Mathf.Clamp01(1f-
                    Mathf.Abs(y-(332f+Mathf.Sin(tt*.2f)*4f))/100f);
                float n1=Mathf.PerlinNoise(x*.0135f+tt*.010f,y*.026f);
                float n2=Mathf.PerlinNoise(x*.031f-tt*.007f,y*.050f+.8f);
                float cloudShape=Mathf.SmoothStep(0f,1f,
                    Mathf.Clamp01((n1*.69f+n2*.31f-.36f)*3f));
                float nebula=cloudShape*cloudBand*.65f;
                float stars=Hash(x/7,y/7);
                float star=(stars>.996f && ((x%7)==3) && ((y%7)==3))
                    ? (.35f+.65f*(.5f+.5f*Mathf.Sin(tt*1.2f+stars*30f))) : 0f;
                int index=y*Size+x;
                pixels[index]=new Color32(
                    (byte)Mathf.Clamp(4f+8f*blueHaze+18f*nebula+110f*star,0,255),
                    (byte)Mathf.Clamp(7f+14f*blueHaze+29f*nebula+135f*star,0,255),
                    (byte)Mathf.Clamp(22f+34f*blueHaze+67f*nebula+185f*star,0,255),255);
            }
        }
        private static float Hash(int x,int y)
        {
            uint n=unchecked((uint)(x*374761393+y*668265263+1274126177));
            n=unchecked((n^(n>>13))*1274126177u);
            return (n&0xFFFF)/65535f;
        }
        private void MixPixel(int x,int y,Color32 c,float alpha)
        {
            if(x<0||y<0||x>=Size||y>=Size)return;
            int k=y*Size+x; var old=pixels[k];
            float a=Mathf.Clamp01(alpha);
            pixels[k]=new Color32(
                (byte)Mathf.Lerp(old.r,c.r,a),
                (byte)Mathf.Lerp(old.g,c.g,a),
                (byte)Mathf.Lerp(old.b,c.b,a),255);
        }
        private void Disc(float cx,float cy,float radius,Color32 col,float alpha)
        {
            int xmin=Mathf.Max(0,Mathf.FloorToInt(cx-radius-2));
            int xmax=Mathf.Min(Size-1,Mathf.CeilToInt(cx+radius+2));
            int ymin=Mathf.Max(0,Mathf.FloorToInt(cy-radius-2));
            int ymax=Mathf.Min(Size-1,Mathf.CeilToInt(cy+radius+2));
            for(int y=ymin;y<=ymax;y++)
            for(int x=xmin;x<=xmax;x++)
            {
                float d=Vector2.Distance(new Vector2(x,y),new Vector2(cx,cy));
                float aa=Mathf.Clamp01(radius+1f-d);
                if(aa>0f) MixPixel(x,y,col,alpha*aa);
            }
        }
        private void PaintClouds()
        {
            float drift=Mathf.Sin(animatedSeconds*.23f)*9f;
            for(int layer=0;layer<3;layer++)
            {
                float cy=313+layer*14f;
                Color32 c=new Color32((byte)(14+layer*4),(byte)(29+layer*7),
                    (byte)(70+layer*12),255);
                for(int j=0;j<9;j++)
                {
                    float x=90+j*45f + drift*(layer%2==0?1f:-.5f);
                    float y=cy+Mathf.Sin(j*1.5f+layer*2f)*11f;
                    Disc(x,y,22+layer*2,c,.11f+layer*.045f);
                }
            }
        }
        private void PaintMoon()
        {
            // Exactly the reference's icy crescent silhouette, no stock image.
            float drift=Mathf.Sin(animatedSeconds*.28f)*2f;
            Disc(256,352+drift,29,new Color32(90,130,255,255),.12f);
            Disc(256,352+drift,23,new Color32(177,206,255,255),.97f);
            Disc(266,363+drift,20,new Color32(7,16,39,255),1f);
        }
        private void PaintRing()
        {
            float progress=Mathf.Clamp01((displaySteps-1f)/23f);
            float centerX=256f,centerY=258f,radius=200f;
            const int steps=480;
            for(int i=0;i<steps;i++)
            {
                float t=(float)i/(steps-1);
                float theta=(-.73f+t*1.46f)*Mathf.PI;
                float x=centerX+radius*Mathf.Sin(theta);
                float y=centerY+radius*Mathf.Cos(theta);
                float active=Mathf.SmoothStep(0,1,Mathf.Clamp01((progress-t)*45f));
                Color32 tint=Color32.Lerp(blue,violet,t);
                Disc(x,y,4.7f,new Color32(18,36,83,255),.95f);
                if(active>.001f)
                {
                    Disc(x,y,7.0f,tint,.21f*active);
                    Disc(x,y,3.25f,tint,active);
                }
            }
            for(int i=0;i<45;i++)
            {
                float theta=(-.73f+(float)i/44f*1.46f)*Mathf.PI;
                float x=256f+185f*Mathf.Sin(theta);
                float y=258f+185f*Mathf.Cos(theta);
                Disc(x,y,1.75f,muted,.75f);
            }
            float lead=(-.73f+1.46f*progress)*Mathf.PI;
            Disc(256f+radius*Mathf.Sin(lead),
                258f+radius*Mathf.Cos(lead),9.3f,white,.94f);
            Disc(256f+radius*Mathf.Sin(lead),
                258f+radius*Mathf.Cos(lead),15f,blue,.22f);
        }
        private void PaintSelection()
        {
            CenterText("СЕЙЧАС "+CurrentHour.ToString("D2")+":"+
                CurrentMinute.ToString("D2"),256,411,3,muted);
            CenterText(DurationText,256,247,10,white);
            CenterText("ВРЕМЯ СНА",256,185,3,muted);
            // Delicate directional markers, not additional HUD overlays.
            CenterText("-",142,251,4,muted);
            CenterText("+",374,251,4,muted);
            CenterText("ПОДЪЁМ "+WakeText,256,143,3,
                new Color32(157,196,252,255));
            DrawBedIcon(256,106);
            CenterText("-30 МИН",131,92,2,muted);
            CenterText("+30 МИН",380,92,2,muted);
            CenterText("ПОДТВЕРДИТЬ",256,64,3,white);
        }
        private void PaintPresets()
        {
            CenterText("ВРЕМЯ СНА",256,414,4,white);
            string[] labels = { "30 МИН", "1 ЧАС", "2 ЧАСА",
                "4 ЧАСА", "8 ЧАСОВ" };
            int[] values = { 1,2,4,8,16 };
            for(int i=0;i<labels.Length;i++)
            {
                int y=345-i*60;
                bool selected=SelectedHalfHours==values[i];
                Color32 shade=selected?blue:new Color32(28,45,84,255);
                for(int x=120;x<=392;x+=4)Disc(x,y,18,shade,selected ? 0.42f : 0.16f);
                CenterText(labels[i],256,y-1,3,selected?white:muted);
            }
            CenterText("ВЫБРАТЬ",256,42,3,muted);
        }
        private void PaintFallingAsleep()
        {
            Disc(256,274,55,blue,.08f+.06f*
                (.5f+.5f*Mathf.Sin(animatedSeconds*2.4f)));
            DrawLine(245,309,260,276,white,3);
            DrawLine(260,276,282,331,white,3);
            CenterText(DurationText,256,205,8,white);
            CenterText("ЗАСЫПАЮ...",256,134,3,muted);
        }
        private void PaintCancelled()
        {
            Color32 red=new Color32(255,92,108,255);
            Disc(256,278,69,red,.11f);
            DrawLine(232,255,281,303,red,5);
            DrawLine(281,255,232,303,red,5);
            CenterText("ОТМЕНЕНО",256,173,4,white);
            CenterText("НАЗАД",256,106,3,muted);
        }
        private void PaintConfirmation()
        {
            Disc(256,284,65,new Color32(47,97,201,255),.19f);
            Disc(256,284,50,blue,.16f);
            DrawLine(225,282,248,260,white,5);
            DrawLine(248,260,288,305,white,5);
            CenterText("УСТАНОВЛЕНО",256,194,3,white);
            CenterText("ПОДЪЁМ "+WakeText,256,147,4,muted);
            CenterText("ВРЕМЯ СНА "+DurationText,256,99,2,muted);
        }
        private void DrawBedIcon(int x,int y)
        {
            Color32 c=new Color32(179,202,252,255);
            DrawLine(x-15,y-6,x+15,y-6,c,2);
            DrawLine(x-15,y-7,x-15,y+9,c,2);
            DrawLine(x+15,y-7,x+15,y+2,c,2);
            DrawLine(x-14,y,x+14,y,c,2);
            Disc(x-9,y+5,3.5f,c,.95f);
            DrawLine(x-5,y+3,x+12,y+3,c,2);
        }

        private void DrawLine(int x1,int y1,int x2,int y2,Color32 c,int width)
        {
            int n=Mathf.CeilToInt(Vector2.Distance(new Vector2(x1,y1),
                new Vector2(x2,y2)));
            for(int i=0;i<=n;i++)
            {
                float t=(float)i/Mathf.Max(1,n);
                Disc(Mathf.Lerp(x1,x2,t),Mathf.Lerp(y1,y2,t),width,c,.98f);
            }
        }
        private void CenterText(string text,int centerX,int y,int scale,Color32 color)
        {
            // Antialiased, proportional Noto Sans glyph masks in Russian.
            // The earlier 5x7 prototype text was visibly too blocky at 512px.
            ArgonRussianFontAtlas.Draw(text,centerX,y,scale*.35f,
                color,MixPixel);
        }
        private void Update()
        {
            if(Application.isPlaying) RenderAt(Time.unscaledTime);
        }
        private void OnDestroy()
        {
            if(OledRenderTexture!=null)
            {
                OledRenderTexture.Release();
                if(Application.isPlaying)Destroy(OledRenderTexture);
                else DestroyImmediate(OledRenderTexture);
            }
            if(OledDynamicTexture!=null)
            {
                if(Application.isPlaying)Destroy(OledDynamicTexture);
                else DestroyImmediate(OledDynamicTexture);
            }
        }
    }
}
