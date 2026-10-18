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
    public enum ArgonRussianWatchScreen {
        Dial, Presets, Overview, Relaxation, Confirmed, FallingAsleep, Cancelled, Alarm, Phases
    }

    [DisallowMultipleComponent]
    public sealed class ArgonRussianSleepOledPresenter : MonoBehaviour
    {
        // Keep original authored 512-unit layout, draw final Cyrillic glyphs
        // at native 1024 pixels instead of magnifying blurry 512px text.
        public const int Size = 512;
        public const int DisplaySize = 1024;
        private const int OutputScale = DisplaySize / Size;
        public RenderTexture OledRenderTexture { get; private set; }
        public bool GpuFacesActive { get; private set; }
        public int AlarmMode { get; private set; } = 1;
        public bool AlarmEnabled { get; private set; } = true;
        // Bit 0: suppress notifications, 1: soft fade, 2: calm ambience.
        public int QuietMask { get; private set; } = 7;
        public bool PhasePlanningEnabled { get; private set; }
        private const string PrefPrefix = "ROKAS.Watch.Sleep.";
        private float lastGpuDraw = -100f;
        private float interactionFlashEnd;
        private void LoadWatchPreferences()
        {
            AlarmMode=Mathf.Clamp(PlayerPrefs.GetInt(PrefPrefix+"AlarmMode",1),0,2);
            AlarmEnabled=PlayerPrefs.GetInt(PrefPrefix+"AlarmEnabled",1)!=0;
            QuietMask=Mathf.Clamp(PlayerPrefs.GetInt(PrefPrefix+"QuietMask",7),0,7);
            PhasePlanningEnabled=PlayerPrefs.GetInt(PrefPrefix+"PhasePlan",0)!=0;
        }
        public void SetAlarmMode(int mode)
        {
            mode=Mathf.Clamp(mode,0,2);
            if(AlarmMode==mode) return;
            AlarmMode=mode;
            PlayerPrefs.SetInt(PrefPrefix+"AlarmMode",mode);
            MarkGpuInteraction();
        }
        public void ToggleAlarm()
        {
            AlarmEnabled=!AlarmEnabled;
            PlayerPrefs.SetInt(PrefPrefix+"AlarmEnabled",AlarmEnabled?1:0);
            MarkGpuInteraction();
        }
        public void SetQuietMask(int mask)
        {
            mask=Mathf.Clamp(mask,0,7);
            if (QuietMask==mask) return;
            QuietMask=mask;
            PlayerPrefs.SetInt(PrefPrefix+"QuietMask",QuietMask);
            MarkGpuInteraction();
        }
        public void ToggleQuietOption(int index)
        {
            if(index<0||index>2)return;
            QuietMask^=(1<<index);
            PlayerPrefs.SetInt(PrefPrefix+"QuietMask",QuietMask);
            MarkGpuInteraction();
        }
        public void TogglePhasePlanning()
        {
            PhasePlanningEnabled=!PhasePlanningEnabled;
            PlayerPrefs.SetInt(PrefPrefix+"PhasePlan",PhasePlanningEnabled?1:0);
            MarkGpuInteraction();
        }
        public void MarkGpuInteraction()
        {
            interactionFlashEnd=Time.unscaledTime+.30f;
            requiresRedraw=true;
            if(GpuFacesActive)RenderAt(Time.unscaledTime,true);
        }
        private Texture2D[] authoredFaces;
        private Texture2D gpuOverlay;
        private Color32[] liveOverlayPixels;
        private Material gpuComposite;
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
        private Color32[] highPixels;
        private struct TextRun
        {
            public string Text;
            public int X, Y;
            public float Scale;
            public Color32 Color;
        }
        private readonly List<TextRun> highText = new List<TextRun>(20);
        private Color32[] cachedSky;
        private byte[] cachedCloud;
        private byte[] cachedStars;
        public int TotalFaceDraws { get; private set; }
        // Positive evidence of nonoverlapping Cyrillic screens: no text
        // exceeds the physical safe rectangle, and labels never intersect.
        private readonly List<Rect> textBounds=new List<Rect>(16);
        public bool LastTextLayoutSafe { get; private set; } = true;
        public int LastTextRows => textBounds.Count;
        private float displaySteps = 14f, animatedSeconds;
        private float lastDrawn = -100f;
        private bool requiresRedraw = true;
        private readonly Color32 white = new Color32(244, 248, 255, 255);
        private readonly Color32 blue = new Color32(70, 145, 255, 255);
        private readonly Color32 violet = new Color32(135, 73, 255, 255);
        private readonly Color32 muted = new Color32(154, 176, 217, 255);

        public void InitializeOled()
        {
            LoadWatchPreferences();
            // Production game: use the five real, preauthored GPU textures
            // and only regenerate tiny live time glyphs when data changes.
            // EditMode/editor without supplied assets keeps legacy fallback.
            if (TryInitializeGpuFaces())
            {
                RenderAt(0f, true);
                return;
            }
            if (Application.isPlaying)
                Debug.LogWarning("[ROKAS WATCH] GPU watch faces unavailable. " +
                    "Check all five Resources/BedSleep/WatchFaces PNGs and the " +
                    "ROKAS/BedSleep/GpuWatchFaceComposite shader. " +
                    "Using throttled legacy OLED.");
            if (OledRenderTexture == null)
            {
                OledRenderTexture = new RenderTexture(DisplaySize,DisplaySize,0,RenderTextureFormat.ARGB32)
                {
                    name = "RT_ROKAS_Argon_Russian_MoonOLED_1024",
                    antiAliasing = 1,
                    filterMode = FilterMode.Bilinear
                };
                OledRenderTexture.Create();
            }
            if (OledDynamicTexture == null)
            {
                OledDynamicTexture = new Texture2D(DisplaySize,DisplaySize,TextureFormat.RGBA32,false)
                {
                    name = "ROKAS_Argon_Russian_MoonOLED",
                    wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
                };
                pixels = new Color32[Size*Size];
                highPixels = new Color32[DisplaySize*DisplaySize];
                BuildStaticSkyCache();
            }
            RenderAt(0f, true);
        }

        public void ConfigureTime(int currentHour, int currentMinute, int selectedHalfHours)
        {
            currentHour = (currentHour % 24 + 24) % 24;
            currentMinute = Mathf.Clamp(currentMinute,0,59);
            selectedHalfHours = Mathf.Clamp(selectedHalfHours,1,24);
            bool clockChanged = CurrentHour != currentHour ||
                CurrentMinute != currentMinute ||
                SelectedHalfHours != selectedHalfHours;
            if (clockChanged) requiresRedraw = true;
            CurrentHour = currentHour;
            CurrentMinute = currentMinute;
            SelectedHalfHours = selectedHalfHours;
            if (!IsConfirmed && (Screen == ArgonRussianWatchScreen.Confirmed ||
                                 Screen == ArgonRussianWatchScreen.Cancelled))
                Screen = ArgonRussianWatchScreen.Dial;
            RefreshTextSummary();
            if ((clockChanged || requiresRedraw) && (GpuFacesActive || OledDynamicTexture != null))
                RenderAt(animatedSeconds,true);
        }

        public void SetConfirmedState(bool confirmed)
        {
            bool changed = IsConfirmed != confirmed ||
                (confirmed && Screen != ArgonRussianWatchScreen.Confirmed) ||
                (!confirmed && Screen == ArgonRussianWatchScreen.Confirmed);
            if (changed) requiresRedraw = true;
            IsConfirmed = confirmed;
            if (confirmed) Screen = ArgonRussianWatchScreen.Confirmed;
            else if (Screen == ArgonRussianWatchScreen.Confirmed)
                Screen = ArgonRussianWatchScreen.Dial;
            RefreshTextSummary();
            if (changed && (GpuFacesActive || OledDynamicTexture != null))
                RenderAt(animatedSeconds,true);
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
            // Even selecting the SAME duration must repaint the physical
            // OLED when returning from the full-screen preset selector.
            if (Screen != ArgonRussianWatchScreen.Dial)
                requiresRedraw = true;
            Screen = ArgonRussianWatchScreen.Dial;
            ConfigureTime(CurrentHour,CurrentMinute,Mathf.Clamp(halfHours,1,24));
        }

        // Square-watch reference panels remain actual dynamically drawn
        // Russian OLED states on the physical source UV, not screen HUD.
        public void ShowOverview()
        {
            Screen=ArgonRussianWatchScreen.Overview;
            RefreshTextSummary();
            requiresRedraw=true;
            RenderAt(animatedSeconds,true);
        }

        public void ShowRelaxation()
        {
            Screen=ArgonRussianWatchScreen.Relaxation;
            RefreshTextSummary();
            requiresRedraw=true;
            RenderAt(animatedSeconds,true);
        }

        public void ShowAlarm()
        {
            Screen=ArgonRussianWatchScreen.Alarm;
            RefreshTextSummary();
            requiresRedraw=true;
            RenderAt(animatedSeconds,true);
        }

        public void ShowPhases()
        {
            Screen=ArgonRussianWatchScreen.Phases;
            RefreshTextSummary();
            requiresRedraw=true;
            RenderAt(animatedSeconds,true);
        }

        public void ShowDial()
        {
            Screen=ArgonRussianWatchScreen.Dial;
            RefreshTextSummary();
            requiresRedraw=true;
            RenderAt(animatedSeconds,true);
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
                    ? "БЫСТРЫЙ ВЫБОР" :
                  Screen == ArgonRussianWatchScreen.Overview
                    ? "ПЛАН СНА" :
                  Screen == ArgonRussianWatchScreen.Relaxation
                    ? "РЕЖИМ ОТДЫХА" :
                  Screen == ArgonRussianWatchScreen.Alarm
                    ? "БУДИЛЬНИК" :
                  Screen == ArgonRussianWatchScreen.Phases
                    ? "ФАЗЫ СНА" : "ПОДТВЕРДИТЬ");
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
            if (GpuFacesActive)
            {
                // Text atlas/CPU upload ONLY on selection/time changes.
                // The GPU shader animates the art; only a tiny Blit at
                // up to 30 Hz is needed, not a full pixel upload each frame.
                if (force || requiresRedraw) PaintGpuFace();
                if (force || animatedSeconds-lastGpuDraw>=1f/30f)
                {
                    CompositeGpuFace(animatedSeconds);
                    lastGpuDraw=animatedSeconds;
                }
                return;
            }
            if (OledDynamicTexture == null) return;
            displaySteps = Mathf.Lerp(displaySteps,SelectedHalfHours,
                force ? .45f : .20f);
            // Legacy fallback stays responsive to clock/menu changes.
            // If GPU resources are missing during gameplay, throttle its
            // million-pixel CPU path to 2Hz to prevent a continuous hitch
            // while preserving the watch visuals and interactions.
            // EditMode reference tests retain the original 10Hz cadence.
            float fallbackInterval = Application.isPlaying ? .50f : .10f;
            if (!force && !requiresRedraw &&
                animatedSeconds-lastDrawn < fallbackInterval)
                return;
            lastDrawn = animatedSeconds;
            requiresRedraw = false;
            TotalFaceDraws++;
            textBounds.Clear();
            highText.Clear();
            LastTextLayoutSafe=true;
            PaintBackground();
            PaintClouds();
            if(Screen==ArgonRussianWatchScreen.Dial ||
               Screen==ArgonRussianWatchScreen.Relaxation)
                PaintMoon();
            PaintSquareGlassFrame();
            if(Screen==ArgonRussianWatchScreen.Dial ||
               Screen==ArgonRussianWatchScreen.Relaxation)
                PaintRing();
            switch (Screen)
            {
                case ArgonRussianWatchScreen.Presets:
                    PaintPresets(); break;
                case ArgonRussianWatchScreen.Overview:
                    PaintOverview(); break;
                case ArgonRussianWatchScreen.Relaxation:
                    PaintRelaxation(); break;
                case ArgonRussianWatchScreen.Alarm:
                    PaintAlarm(); break;
                case ArgonRussianWatchScreen.Phases:
                    PaintPhases(); break;
                case ArgonRussianWatchScreen.Confirmed:
                    PaintConfirmation(); break;
                case ArgonRussianWatchScreen.FallingAsleep:
                    PaintFallingAsleep(); break;
                case ArgonRussianWatchScreen.Cancelled:
                    PaintCancelled(); break;
                default:
                    PaintSelection(); break;
            }
            // Smooth physical night-scene artwork is enlarged once; Cyrillic
            // is drawn AFTER that upscale directly at full display
            // resolution from the original anti-aliased Noto atlas.
            PaintHighResolutionOled();
            OledDynamicTexture.SetPixels32(highPixels);
            OledDynamicTexture.Apply(false,false);
            Graphics.Blit(OledDynamicTexture,OledRenderTexture);
        }

        /// <summary>
        /// Evaluate expensive, soft Perlin cloud layers ONCE per watch instance.
        /// The sky is reanimated with a cheap scrolling density sample; the
        /// actual output stays full 512x512 and remains subtly animated.
        /// </summary>
        private void BuildStaticSkyCache()
        {
            int length=Size*Size;
            cachedSky=new Color32[length];
            cachedCloud=new byte[length];
            cachedStars=new byte[length];
            for(int y=0;y<Size;y++)
            for(int x=0;x<Size;x++)
            {
                float dx=(x-256f)/256f,dy=(y-266f)/256f;
                float radius=Mathf.Sqrt(dx*dx+dy*dy);
                float haze=Mathf.Clamp01((1.15f-radius)*.55f);
                float wave=.5f+.5f*Mathf.Sin((dx+dy*.36f)*4f);
                float tint=haze*(.5f+.5f*wave);
                float cloudBand=Mathf.Clamp01(1f-
                    Mathf.Abs(y-333f)/112f);
                float n1=Mathf.PerlinNoise(x*.0135f,y*.026f);
                float n2=Mathf.PerlinNoise(x*.031f,y*.050f+.8f);
                float shape=Mathf.SmoothStep(0f,1f,
                    Mathf.Clamp01((n1*.69f+n2*.31f-.36f)*3f));
                int k=y*Size+x;
                cachedSky[k]=new Color32(
                    (byte)(4f+8f*tint),(byte)(7f+14f*tint),
                    (byte)(22f+34f*tint),255);
                cachedCloud[k]=(byte)(Mathf.Clamp01(shape*cloudBand)*255f);
                float star=Hash(x/7,y/7);
                cachedStars[k]=(byte)(star>.996f && x%7==3 && y%7==3
                    ? (int)(star*255) : 0);
            }
        }
        private void PaintBackground()
        {
            if(cachedSky==null) BuildStaticSkyCache();
            int drift=Mathf.RoundToInt(Mathf.Sin(animatedSeconds*.21f)*13f);
            float twinkle=.84f+.16f*Mathf.Sin(animatedSeconds*1.25f);
            for(int y=0;y<Size;y++)
            {
                int row=y*Size;
                for(int x=0;x<Size;x++)
                {
                    int k=row+x;
                    int shifted=row+((x+drift+Size)&(Size-1));
                    Color32 sky=cachedSky[k];
                    int cloud=cachedCloud[shifted];
                    int star=(int)(cachedStars[k]*twinkle);
                    pixels[k]=new Color32(
                        (byte)Mathf.Min(255,sky.r+(cloud*18>>8)+(star*110>>8)),
                        (byte)Mathf.Min(255,sky.g+(cloud*29>>8)+(star*135>>8)),
                        (byte)Mathf.Min(255,sky.b+(cloud*67>>8)+(star*185>>8)),
                        255);
                }
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
                float dx=x-cx,dy=y-cy;
                float sq=dx*dx+dy*dy;
                if(sq>(radius+1f)*(radius+1f))continue;
                float aa=sq<(radius-1f)*(radius-1f) ? 1f :
                    Mathf.Clamp01(radius+1f-Mathf.Sqrt(sq));
                if(aa>0f) MixPixel(x,y,col,alpha*aa);
            }
        }
        private void PaintClouds()
        {
            // Fine soft clouds already composited in the cached Perlin
            // density field. No visible round dabs or overlapping discs.
        }
        private void PaintMoon()
        {
            // Exactly the reference's icy crescent silhouette, no stock image.
            float drift=Mathf.Sin(animatedSeconds*.28f)*2f;
            Disc(256,352+drift,42,new Color32(50,100,255,255),.035f);
            Disc(256,352+drift,31,new Color32(90,130,255,255),.13f);
            Disc(256,352+drift,23,new Color32(205,223,255,255),.97f);
            Disc(266,363+drift,20,new Color32(7,16,39,255),1f);
        }
        private void PaintRing()
        {
            float progress=Mathf.Clamp01((displaySteps-1f)/23f);
            float centerX=256f,centerY=258f,radius=181f;
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
                float x=256f+169f*Mathf.Sin(theta);
                float y=258f+169f*Mathf.Cos(theta);
                Disc(x,y,1.75f,muted,.75f);
            }
            float lead=(-.73f+1.46f*progress)*Mathf.PI;
            float tipX=256f+radius*Mathf.Sin(lead);
            float tipY=258f+radius*Mathf.Cos(lead);
            float glow=.14f+.065f*Mathf.Sin(animatedSeconds*2.1f);
            Disc(tipX,tipY,17f,blue,glow);
            Disc(tipX,tipY,9.1f,white,.96f);
        }
        // A SQUARE watch panel with gently rounded rectangular inner edge,
        // while retaining the reference's curved luminous progress ring.
        // No new physical face mesh; still the authentic original OLED UVs.
        private void PaintSquareGlassFrame()
        {
            // The real square titanium bezel and sapphire cover already
            // frame this UV. The previous four DOTTED debug-looking lines
            // made the tiny screen busy and prototype-like. Let its actual
            // physical glass shader supply edge falloff instead.
        }

        private void PaintOverview()
        {
            // Three compact, nonoverlapping blocks, readable at game size.
            CenterText("ПЛАН СНА",256,458,3,white);
            CenterText("СЕЙЧАС",256,409,2,muted);
            CenterText(CurrentHour.ToString("D2")+":"+
                CurrentMinute.ToString("D2"),256,350,7,white);
            CenterText("ВРЕМЯ СНА",256,289,2,muted);
            CenterText(DurationText,256,227,7,white);
            CenterText("ПОДЪЁМ",256,163,2,muted);
            CenterText(WakeText,256,103,7,
                new Color32(182,215,255,255));
            CenterText("ДАЛЕЕ",256,43,2,white);
        }
        private void PaintAlarm()
        {
            CenterText("БУДИЛЬНИК",256,452,3,white);
            CenterText("СЕЙЧАС",256,372,2,muted);
            CenterText(CurrentHour.ToString("D2")+":"+
                CurrentMinute.ToString("D2"),256,310,6,white);
            CenterText("ПРОСНУТЬСЯ",256,232,3,blue);
            CenterText(WakeText,256,163,8,white);
            CenterText("СОН "+DurationText,256,95,3,muted);
            CenterText("ПОДТВЕРДИТЬ",256,39,2,white);
        }

        private void PaintPhases()
        {
            CenterText("ФАЗЫ СНА",256,450,3,white);
            // Indicative four-stage timeline, not real sleep tracking.
            for(int i=0;i<4;i++)
            {
                float centerX=92f+i*107f;
                float wave=.5f+.5f*Mathf.Sin(animatedSeconds*.9f+i*.9f);
                float high=58f+i%2*31f+wave*10f;
                for(int j=0;j<5;j++)
                    Disc(centerX+j*8f,258f+high*.35f,4f,
                        Color32.Lerp(blue,violet,i/3f),.5f+j*.08f);
            }
            CenterText("ЦИКЛЫ СНА",256,350,3,muted);
            CenterText("ЛЁГКИЙ / ГЛУБОКИЙ",256,226,2,white);
            CenterText("БЫСТРЫЙ СОН",256,176,2,muted);
            CenterText("ДЛИТЕЛЬНОСТЬ",256,120,2,muted);
            CenterText(DurationText,256,62,6,white);
        }

        private void PaintRelaxation()
        {
            CenterText("РЕЖИМ ОТДЫХА",256,455,2,white);
            float t=animatedSeconds*1.16f;
            for(int i=0;i<125;i++)
            {
                float x=100f+i*2.5f;
                float envelope=Mathf.Sin(Mathf.PI*i/124f);
                float y=287+Mathf.Sin(i*.113f+t)*16f*envelope+
                    Mathf.Sin(i*.059f+t*.65f)*8f*envelope;
                Disc(x,y,2.7f,Color32.Lerp(blue,violet,(float)i/124f),.78f);
            }
            CenterText("ТИШИНА",256,206,5,white);
            CenterText("СОН "+DurationText,256,146,3,muted);
            CenterText("ПОДЪЁМ "+WakeText,256,102,2,muted);
            CenterText("ПОДТВЕРДИТЬ",256,49,2,white);
        }
        private void PaintSelection()
        {
            // At 720p the watch is ~70 pixels high. One huge value with
            // clear sleep/wake context beats six overlapping micro-labels.
            // Two-tier typography for watch focus: large numeric readouts
            // and separated labels instead of thin 6px combined strings.
            // No overlapping atlas bounds inside the sapphire safe region.
            CenterText("СЕЙЧАС",256,462,2,muted);
            CenterText(CurrentHour.ToString("D2")+":"+
                CurrentMinute.ToString("D2"),256,407,5,white);
            CenterText("СОН",256,307,3,muted);
            CenterText(DurationText,256,240,9,white);
            CenterText("ПОДЪЁМ",256,147,3,muted);
            CenterText(WakeText,256,95,6,
                new Color32(182,215,255,255));
            CenterText("ПОДТВЕРДИТЬ",256,40,2,white);
        }
        private void PaintPresets()
        {
            CenterText("ВРЕМЯ СНА",256,455,3,white);
            string[] labels={"1 ЧАС","2 ЧАСА","4 ЧАСА","6 ЧАСОВ","8 ЧАСОВ"};
            int[] values={2,4,8,12,16};
            for(int i=0;i<labels.Length;i++)
            {
                int y=370-i*61;
                bool selected=SelectedHalfHours==values[i];
                Color32 shade=selected?blue:new Color32(22,41,79,255);
                for(int x=134;x<=378;x+=6)
                    Disc(x,y,18,shade,selected?.34f:.17f);
                CenterText(labels[i],256,y,3,selected?white:muted);
            }
            CenterText("ВЫБРАТЬ",256,46,2,muted);
        }
        private void PaintFallingAsleep()
        {
            Disc(256,274,55,blue,.08f+.06f*
                (.5f+.5f*Mathf.Sin(animatedSeconds*2.4f)));
            // Reference-style animated hourglass instead of reusing the
            // confirmation tick. A blue sand pulse falls between both
            // tapered chambers and remains legible on a square OLED.
            Color32 sand=new Color32(127,200,255,255);
            DrawLine(219,334,293,334,sand,3);
            DrawLine(224,330,288,251,sand,2);
            DrawLine(288,330,224,251,sand,2);
            DrawLine(219,248,293,248,sand,3);
            Disc(256,287,5f,white,.85f);
            float fall=Mathf.Repeat(animatedSeconds*.65f,1f);
            Disc(256,284f-19f*fall,2.5f,blue,.78f);
            CenterText(DurationText,256,198,8,white);
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
            float breathe=.5f+.5f*Mathf.Sin(animatedSeconds*2.0f);
            Disc(256,284,73,new Color32(47,97,201,255),
                .055f+.045f*breathe);
            Disc(256,284,54,blue,.13f+.06f*breathe);
            DrawLine(225,282,248,260,white,5);
            DrawLine(248,260,288,305,white,5);
            CenterText("УСТАНОВЛЕНО",256,187,4,white);
            CenterText("ПОДЪЁМ "+WakeText,256,141,3,muted);
            CenterText("СОН "+DurationText,256,95,3,muted);
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
            // Use exact atlas advances; prevent glyphs clipping past the
            // original watch's square sapphire bezel, not just the RT edges.
            float f=scale*.35f;
            float measured=ArgonRussianFontAtlas.MeasureWidth(text,f);
            if(measured>388f) f*=388f/measured;
            float halfWidth=ArgonRussianFontAtlas.MeasureWidth(text,f)*.5f;
            float halfHeight=28f*f*.5f+1f;
            var region=new Rect(centerX-halfWidth,y-halfHeight,
                2f*halfWidth,2f*halfHeight);
            if(region.xMin<54f || region.xMax>458f ||
                region.yMin<24f || region.yMax>488f)
                LastTextLayoutSafe=false;
            foreach(var other in textBounds)
                if(region.Overlaps(other))
                    LastTextLayoutSafe=false;
            textBounds.Add(region);
            highText.Add(new TextRun {
                Text = text, X = centerX, Y = y, Scale = f, Color = color
            });
        }
        private static readonly string[] GpuNames = {
            "screen_timer", "screen_wheel", "screen_alarm",
            "screen_quiet", "screen_phases"
        };

        private bool TryInitializeGpuFaces()
        {
            if (GpuFacesActive) return true;
            if (!Application.isPlaying) return false;
            if (OledDynamicTexture != null || OledRenderTexture != null)
                return false;
            Shader shader=Shader.Find("ROKAS/BedSleep/GpuWatchFaceComposite");
            if(shader==null || !shader.isSupported) return false;
            Texture2D[] faces=new Texture2D[GpuNames.Length];
            for(int i=0;i<faces.Length;i++)
            {
                faces[i]=Resources.Load<Texture2D>(
                    "BedSleep/WatchFaces/"+GpuNames[i]);
                if(faces[i]==null || faces[i].width<512 ||
                    faces[i].height<512)
                    return false;
            }
            authoredFaces=faces;
            gpuComposite=new Material(shader);
            gpuComposite.name="ROKAS_Argon_Watch_GPU_5Screens";
            OledRenderTexture=new RenderTexture(DisplaySize,DisplaySize,
                0,RenderTextureFormat.ARGB32)
            {
                name="RT_ROKAS_Argon_Authored_GPU_1024",
                antiAliasing=1,
                filterMode=FilterMode.Bilinear
            };
            OledRenderTexture.Create();
            gpuOverlay=new Texture2D(DisplaySize,DisplaySize,
                TextureFormat.RGBA32,false)
            {
                name="ROKAS_Argon_DynamicTime_OnChangeOnly",
                wrapMode=TextureWrapMode.Clamp,
                filterMode=FilterMode.Bilinear
            };
            liveOverlayPixels=new Color32[DisplaySize*DisplaySize];
            GpuFacesActive=true;
            requiresRedraw=true;
            Debug.Log("[ROKAS WATCH] GPU-authored five faces enabled. " +
                "No CPU sky/upscale loops in TimeSelection.");
            return true;
        }

        private int ActiveGpuFace()
        {
            switch(Screen)
            {
                case ArgonRussianWatchScreen.Presets: return 1;
                case ArgonRussianWatchScreen.Alarm:
                case ArgonRussianWatchScreen.Overview: return 2;
                case ArgonRussianWatchScreen.Relaxation:
                case ArgonRussianWatchScreen.FallingAsleep: return 3;
                case ArgonRussianWatchScreen.Phases: return 4;
                default: return 0;
            }
        }

        private void PaintGpuFace()
        {
            if (!GpuFacesActive) return;
            requiresRedraw=false;
            TotalFaceDraws++;
            // No CPU 1024x1024 background, clouds, smoothstep, or
            // per-frame GPU upload. Only new clock/menu events touch
            // this small writable overlay, then GPU composites PNG art.
            Array.Clear(liveOverlayPixels,0,liveOverlayPixels.Length);
            Color32 light = new Color32(226,240,255,255);
            Color32 cyan = new Color32(131,220,255,255);
            switch(ActiveGpuFace())
            {
                case 0:
                    LiveText(DurationText,256,265,3.15f,225,light);
                    LiveText(WakeText,256,372,1.35f,124,cyan);
                    LiveText(CurrentHour.ToString("D2")+":"+
                        CurrentMinute.ToString("D2"),74,409,1.55f,92,cyan);
                    int hours=SelectedHalfHours/2;
                    string detail=hours.ToString()+"Ч "+
                        ((SelectedHalfHours%2)==0?"00":"30")+" МИН";
                    LiveText(detail,435,410,.88f,126,cyan);
                    break;
                case 1:
                    LiveText(DurationText,256,265,3.25f,230,light);
                    break;
                case 2:
                    LiveText(WakeText,257,269,3.1f,198,light);
                    break;
                case 3:
                    // Authored quiet-mode illustration; no timer sample
                    // is painted or uploaded until state changes.
                    break;
                case 4:
                    // Phase chart is a planning illustration. The game
                    // must not claim real biometric measurements.
                    break;
            }
            if(Screen==ArgonRussianWatchScreen.Confirmed)
                LiveText("УСТАНОВЛЕНО",256,271,1.4f,275,cyan);
            if(Screen==ArgonRussianWatchScreen.Cancelled)
                LiveText("ОТМЕНЕНО",256,269,1.6f,285,cyan);
            gpuOverlay.SetPixels32(liveOverlayPixels);
            gpuOverlay.Apply(false,false);
            gpuComposite.SetTexture("_LiveText",gpuOverlay);
        }

        private void CompositeGpuFace(float now)
        {
            if (!GpuFacesActive || gpuComposite == null) return;
            gpuComposite.SetFloat("_FaceIndex", ActiveGpuFace());
            gpuComposite.SetFloat("_Duration01",
                Mathf.Clamp01((SelectedHalfHours-2f)/22f));
            gpuComposite.SetFloat("_AlarmMode", AlarmMode);
            gpuComposite.SetFloat("_AlarmOn", AlarmEnabled ? 1f : 0f);
            gpuComposite.SetFloat("_QuietMask", QuietMask);
            gpuComposite.SetFloat("_PhaseMode",
                PhasePlanningEnabled ? 1f : 0f);
            gpuComposite.SetFloat("_MotionTime", now);
            gpuComposite.SetFloat("_Focus",
                now<interactionFlashEnd ? 1f : 0f);
            Graphics.Blit(authoredFaces[ActiveGpuFace()],
                OledRenderTexture, gpuComposite);
        }

        private void LiveText(string value,int x,int yTop,
            float sourceScale,float maxWidth,Color32 tint)
        {
            if(string.IsNullOrEmpty(value)) return;
            float f=sourceScale*OutputScale;
            float measured=ArgonRussianFontAtlas.MeasureWidth(value,f);
            float max= maxWidth*OutputScale;
            if(measured>max && measured>0f) f*=max/measured;
            ArgonRussianFontAtlas.Draw(value,x*OutputScale,
                (Size-yTop)*OutputScale,f,tint,MixLivePixel);
        }

        private void MixLivePixel(int x,int y,Color32 color,float coverage)
        {
            if(x<0||y<0||x>=DisplaySize||y>=DisplaySize)return;
            int index=y*DisplaySize+x;
            byte alpha=(byte)Mathf.Clamp(
                Mathf.RoundToInt(coverage*255f),0,255);
            if(alpha>liveOverlayPixels[index].a)
                liveOverlayPixels[index]=new Color32(
                    color.r,color.g,color.b,alpha);
        }

        private void PaintHighResolutionOled()
        {
            // Do not scale the small 512px bitmap typography. This stage
            // supersamples ONLY the non-text sky, moon and watch decorations.
            // Each text run uses the source vector-like coverage atlas and
            // gets rasterized at full 1024x1024 native output pixels.
            for (int y = 0; y < DisplaySize; y++)
            {
                float fy = (y + .5f) / OutputScale - .5f;
                int y0 = Mathf.Clamp(Mathf.FloorToInt(fy),0,Size-1);
                int y1 = Mathf.Min(y0+1,Size-1);
                float wy = Mathf.Clamp01(fy - y0);
                int row0 = y0 * Size, row1 = y1 * Size;
                int destRow = y * DisplaySize;
                for (int x = 0; x < DisplaySize; x++)
                {
                    float fx = (x+.5f) / OutputScale - .5f;
                    int x0 = Mathf.Clamp(Mathf.FloorToInt(fx),0,Size-1);
                    int x1 = Mathf.Min(x0+1,Size-1);
                    float wx = Mathf.Clamp01(fx - x0);
                    Color32 a = Color32.Lerp(
                        pixels[row0+x0], pixels[row0+x1], wx);
                    Color32 b = Color32.Lerp(
                        pixels[row1+x0], pixels[row1+x1], wx);
                    highPixels[destRow+x] = Color32.Lerp(a,b,wy);
                }
            }
            for (int i=0; i<highText.Count; i++)
            {
                TextRun t = highText[i];
                ArgonRussianFontAtlas.Draw(t.Text,
                    t.X * OutputScale, t.Y * OutputScale,
                    t.Scale * OutputScale, t.Color, MixHighPixel);
            }
        }

        private void MixHighPixel(int x,int y,Color32 color,float alpha)
        {
            if (x<0||y<0||x>=DisplaySize||y>=DisplaySize)return;
            int i=y*DisplaySize+x;
            Color32 old=highPixels[i];
            float a=Mathf.Clamp01(alpha);
            highPixels[i]=new Color32(
                (byte)Mathf.Lerp(old.r,color.r,a),
                (byte)Mathf.Lerp(old.g,color.g,a),
                (byte)Mathf.Lerp(old.b,color.b,a),255);
        }

        private void Update()
        {
            if(Application.isPlaying) RenderAt(Time.unscaledTime);
        }
        private void OnDestroy()
        {
            if(gpuComposite!=null)
            {
                if(Application.isPlaying)Destroy(gpuComposite);
                else DestroyImmediate(gpuComposite);
            }
            if(gpuOverlay!=null)
            {
                if(Application.isPlaying)Destroy(gpuOverlay);
                else DestroyImmediate(gpuOverlay);
            }
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
