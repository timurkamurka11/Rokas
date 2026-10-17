using System;
using System.Collections.Generic;
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
    [DisallowMultipleComponent]
    public sealed class ArgonRussianSleepOledPresenter : MonoBehaviour
    {
        public const int Size = 512;
        public RenderTexture OledRenderTexture { get; private set; }
        public Texture2D OledDynamicTexture { get; private set; }
        public int SelectedHalfHours { get; private set; } = 14;
        public bool IsConfirmed { get; private set; }
        public int CurrentHour { get; private set; } = 19;
        public int CurrentMinute { get; private set; } = 30;
        public int WakeMinutes => (CurrentHour * 60 + CurrentMinute +
            SelectedHalfHours * 30) % 1440;
        public string LastRenderedTextSummary { get; private set; }
        private Color32[] pixels;
        private float displaySteps = 14f, animatedSeconds;
        private float lastDrawn = -100f;
        private bool requiresRedraw = true;
        private readonly Color32 deep = new Color32(4, 8, 22, 255);
        private readonly Color32 white = new Color32(244, 248, 255, 255);
        private readonly Color32 blue = new Color32(70, 145, 255, 255);
        private readonly Color32 violet = new Color32(135, 73, 255, 255);
        private readonly Color32 muted = new Color32(154, 176, 217, 255);

        // Embedded glyph data, including Cyrillic, no OS-font dependency,
        // no external font files or placeholder Latin-transliterated labels.
        private static readonly Dictionary<char, string> Glyph = new Dictionary<char, string>
        {
            {'0',"01110/10001/10011/10101/11001/10001/01110"},
            {'1',"00100/01100/00100/00100/00100/00100/01110"},
            {'2',"01110/10001/00001/00010/00100/01000/11111"},
            {'3',"11110/00001/00001/01110/00001/00001/11110"},
            {'4',"00010/00110/01010/10010/11111/00010/00010"},
            {'5',"11111/10000/11110/00001/00001/10001/01110"},
            {'6',"00110/01000/10000/11110/10001/10001/01110"},
            {'7',"11111/00001/00010/00100/01000/01000/01000"},
            {'8',"01110/10001/10001/01110/10001/10001/01110"},
            {'9',"01110/10001/10001/01111/00001/00010/01100"},
            {':',"00000/00100/00100/00000/00100/00100/00000"},
            {' ',"00000/00000/00000/00000/00000/00000/00000"},
            {'+',"00000/00100/00100/11111/00100/00100/00000"},
            {'-',"00000/00000/00000/11111/00000/00000/00000"},
            {'А',"01110/10001/10001/11111/10001/10001/10001"},
            {'Б',"11111/10000/10000/11110/10001/10001/11110"},
            {'В',"11110/10001/10001/11110/10001/10001/11110"},
            {'Г',"11111/10000/10000/10000/10000/10000/10000"},
            {'Д',"00110/01010/01010/01010/01010/11111/10001"},
            {'Е',"11111/10000/10000/11110/10000/10000/11111"},
            {'Ё',"01010/00000/11111/10000/11110/10000/11111"},
            {'Ж',"10101/10101/01110/00100/01110/10101/10101"},
            {'З',"11110/00001/00001/01110/00001/00001/11110"},
            {'И',"10001/10011/10101/10101/11001/10001/10001"},
            {'Й',"00100/10001/10011/10101/10101/11001/10001"},
            {'К',"10001/10010/10100/11000/10100/10010/10001"},
            {'Л',"00111/01001/01001/01001/01001/01001/10001"},
            {'М',"10001/11011/10101/10101/10001/10001/10001"},
            {'Н',"10001/10001/10001/11111/10001/10001/10001"},
            {'О',"01110/10001/10001/10001/10001/10001/01110"},
            {'П',"11111/10001/10001/10001/10001/10001/10001"},
            {'Р',"11110/10001/10001/11110/10000/10000/10000"},
            {'С',"01111/10000/10000/10000/10000/10000/01111"},
            {'Т',"11111/00100/00100/00100/00100/00100/00100"},
            {'У',"10001/10001/01010/00100/00100/01000/10000"},
            {'Ф',"00100/01110/10101/10101/01110/00100/00100"},
            {'Х',"10001/10001/01010/00100/01010/10001/10001"},
            {'Ц',"10010/10010/10010/10010/10010/11111/00001"},
            {'Ч',"10001/10001/10001/01111/00001/00001/00001"},
            {'Ш',"10101/10101/10101/10101/10101/10101/11111"},
            {'Щ',"10101/10101/10101/10101/10101/11111/00001"},
            {'Ъ',"11000/01000/01110/01001/01001/01001/01110"},
            {'Ы',"10001/10001/11101/10101/10101/10101/11101"},
            {'Ь',"10000/10000/11110/10001/10001/10001/11110"},
            {'Э',"11110/00001/00001/00111/00001/00001/11110"},
            {'Ю',"10010/10101/10101/11101/10101/10101/10010"},
            {'Я',"01111/10001/10001/01111/00101/01001/10001"},
        };

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
            selectedHalfHours = Mathf.Clamp(selectedHalfHours,2,24);
            if (CurrentHour != currentHour || CurrentMinute != currentMinute ||
                SelectedHalfHours != selectedHalfHours)
                requiresRedraw = true;
            CurrentHour = currentHour;
            CurrentMinute = currentMinute;
            SelectedHalfHours = selectedHalfHours;
            LastRenderedTextSummary = "СЕЙЧАС " + CurrentHour.ToString("D2") +
                ":" + CurrentMinute.ToString("D2") + " ВРЕМЯ СНА " +
                DurationText + " ПОДЪЁМ " + WakeText +
                (IsConfirmed ? " УСТАНОВЛЕНО" : " ПОДТВЕРДИТЬ");
            if (OledDynamicTexture != null) RenderAt(animatedSeconds,true);
        }

        public void SetConfirmedState(bool confirmed)
        {
            if (IsConfirmed != confirmed) requiresRedraw = true;
            IsConfirmed = confirmed;
            if (OledDynamicTexture != null) RenderAt(animatedSeconds,true);
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
            if (IsConfirmed) PaintConfirmation();
            else PaintSelection();
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
                float stars=Hash(x/7,y/7);
                float star=(stars>.996f && ((x%7)==3) && ((y%7)==3))
                    ? (.35f+.65f*(.5f+.5f*Mathf.Sin(tt*1.2f+stars*30f))) : 0f;
                int index=y*Size+x;
                pixels[index]=new Color32(
                    (byte)Mathf.Clamp(4f+8f*blueHaze+110f*star,0,255),
                    (byte)Mathf.Clamp(7f+14f*blueHaze+135f*star,0,255),
                    (byte)Mathf.Clamp(22f+34f*blueHaze+185f*star,0,255),255);
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
            float progress=Mathf.Clamp01((displaySteps-2f)/22f);
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
            CenterText("ПОДЪЁМ "+WakeText,256,143,3,
                new Color32(157,196,252,255));
            CenterText("-30 МИН",131,92,2,muted);
            CenterText("+30 МИН",380,92,2,muted);
            CenterText("ПОДТВЕРДИТЬ",256,64,3,white);
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
            // Highly legible actual Cyrillic raster on the 512px physical face.
            int advance=scale*6, length=text.Length*advance-scale;
            int start=centerX-length/2;
            for(int i=0;i<text.Length;i++)
            {
                char ch=char.ToUpperInvariant(text[i]);
                if(!Glyph.TryGetValue(ch,out string bitmap))
                    continue;
                int cy=0,cx=0;
                for(int k=0;k<bitmap.Length;k++)
                {
                    if(bitmap[k]=='/'){cy++;cx=0;continue;}
                    if(bitmap[k]=='1')
                    {
                        int px=start+i*advance+cx*scale;
                        int py=y+(3-cy)*scale;
                        for(int dy=0;dy<scale;dy++)
                            for(int dx=0;dx<scale;dx++)
                                MixPixel(px+dx,py+dy,color,1f);
                    }
                    cx++;
                }
            }
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
