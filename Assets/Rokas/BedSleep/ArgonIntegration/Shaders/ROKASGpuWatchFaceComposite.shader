Shader "ROKAS/BedSleep/GpuWatchFaceComposite"
{
    Properties
    {
        _MainTex ("Original Watch Face", 2D) = "black" {}
        _LiveText ("Live Cyrillic Overlay", 2D) = "black" {}
        _FaceIndex ("Tab Index", Float) = 0
        _Duration01 ("Duration Progress", Range(0,1)) = .55
        _AlarmMode ("Alarm Mode", Float) = 1
        _AlarmOn ("Alarm Enabled", Float) = 1
        _QuietMask ("Quiet Toggles", Float) = 7
        _PhaseMode ("Phase Planning", Float) = 0
        _MotionTime ("Animation Clock", Float) = 0
        _Focus ("Interaction Strength", Float) = 0
        _HoverActive ("Pointer Hover", Float) = 0
        _HoverUv ("Pointer Position", Vector) = (.5,.5,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        ZTest Always Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex, _LiveText;
            float _FaceIndex, _Duration01, _AlarmMode, _AlarmOn;
            float _QuietMask, _PhaseMode, _MotionTime, _Focus;
            float _HoverActive;
            float4 _HoverUv;
            float ring(float2 p,float2 center,float radius,float thickness)
            {
                float d=abs(length(p-center)-radius);
                return 1.-smoothstep(thickness,thickness+.0025,d);
            }
            float curve(float2 p,float2 center,float radius,float thickness,float progress)
            {
                float2 d=p-center;
                float phase=atan2(d.x,d.y);
                // Unwrap the ring from top clockwise.
                float u=frac(phase/6.2831853+1.);
                return ring(p,center,radius,thickness) *
                    (1.-smoothstep(progress-.006,progress+.005,u));
            }
            float halo(float2 p,float2 center,float size)
            {
                return exp(-dot(p-center,p-center)/(size*size));
            }
            float boxOutline(float2 uv,float2 lo,float2 hi,float edge)
            {
                float2 inside=smoothstep(lo,lo+.006,uv)*
                              (1.-smoothstep(hi-.006,hi,uv));
                float2 internal=smoothstep(lo+edge,lo+edge+.004,uv)*
                                (1.-smoothstep(hi-edge-.004,hi-edge,uv));
                return saturate(inside.x*inside.y-internal.x*internal.y);
            }
            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv=i.uv;
                float3 art=tex2D(_MainTex,uv).rgb;
                float2 center=float2(.5,.555);
                float3 cyan=float3(.05,.64,1.);
                float3 violet=float3(.45,.21,1.);
                float tick=_MotionTime;
                float energy=.72+.20*sin(tick*2.15);
                float3 glow=0;
                // Preserve all artwork, captions and glyphs as the
                // untouched base. Never dim or draw rectangles across
                // existing art: previous versions covered labels/cards.
                if (_FaceIndex < .5)
                {
                    // Timer. Animate a THIN inner guide along the existing
                    // round track, not a second glowing ring over the PNG.
                    float2 c = float2(.5,.537);
                    float pct = saturate(_Duration01);
                    float2 d = uv - c;
                    float ang = frac(atan2(d.x,d.y)/6.2831853+1.);
                    float radius = length(d);
                    float guide = (1.-smoothstep(.002,.007,
                        abs(radius-.306))) *
                        (1.-smoothstep(pct-.01,pct+.005,ang));
                    float2 tip=c+.306*float2(sin(pct*6.2831853),
                        cos(pct*6.2831853));
                    glow += cyan*(guide*.28+
                        halo(uv,tip,.012)*.30)*energy;
                }
                else if (_FaceIndex < 1.5)
                {
                    // Wheel. Align tracer to ACTUAL ring center in the
                    // authored 1024x1024 texture. No blur over numerals.
                    float2 c = float2(.5,.50);
                    float a=lerp(-2.36,2.36,saturate(_Duration01));
                    float2 tip=c+.307*float2(sin(a),cos(a));
                    float rim=ring(uv,c,.305,.002);
                    glow+=lerp(cyan,violet,.5+.5*sin(tick*.45))*
                        (halo(uv,tip,.013)*.32+rim*.085);
                }
                else if (_FaceIndex < 2.5)
                {
                    // Alarm cards: a narrow luminous top accent per
                    // selection, not a rectangular glow across the text.
                    float mode=floor(_AlarmMode+.5);
                    float2 selectedCenter=float2(.178+mode*.309,.366);
                    float bar=(1.-smoothstep(.003,.010,
                        abs(uv.y-selectedCenter.y)))*
                        (1.-smoothstep(.115,.142,
                        abs(uv.x-selectedCenter.x)));
                    glow+=cyan*bar*(.27+.07*sin(tick*2.));
                    float enabled=step(.5,_AlarmOn);
                    glow+=cyan*halo(uv,float2(.87,.827),.030)*
                        enabled*.16;
                }
                else if (_FaceIndex < 3.5)
                {
                    // Quiet cards are vertically HIGHER than alarm cards.
                    // Their real pixel bounds run y=.225...48 in UV.
                    for(int n=0;n<3;n++)
                    {
                        float enabled=fmod(floor(
                            _QuietMask/exp2((float)n)),2.);
                        float centerX=.195+.317*n;
                        float accent=(1.-smoothstep(.004,.013,
                            abs(uv.y-.470)))*
                            (1.-smoothstep(.11,.14,
                            abs(uv.x-centerX)));
                        glow+=cyan*accent*enabled*.25;
                    }
                    float allOn=step(.5,_QuietMask);
                    glow+=cyan*halo(uv,float2(.87,.832),.032)*
                        allOn*.14;
                }
                else
                {
                    // Time-aware planning cursor: the planned sleep window
                    // moves with duration; sweep stays inside CHART only.
                    float planned=lerp(.12,.91,saturate(_Duration01));
                    float sweep=lerp(.12,planned,frac(tick*.042));
                    float cursor=1.-smoothstep(.001,.004,
                        abs(uv.x-sweep));
                    float chartMask=smoothstep(.48,.50,uv.y)*
                        (1.-smoothstep(.73,.75,uv.y));
                    glow+=lerp(cyan,violet,.5+.5*sin(tick*.7))*
                        cursor*chartMask*.23;
                    if(_PhaseMode>.5)
                        glow+=cyan*halo(uv,float2(planned,.66),.020)*.24;
                }
                // Subtle physical-pointer glint on real OLED controls.
                // Never overlay a second static PNG or text outline.
                if(_HoverActive>.5)
                    glow+=cyan*halo(uv,_HoverUv.xy,.032)*.21;
                // Restrict art-preserving motion to the watch inner display.
                glow*=smoothstep(.035,.085,uv.x)*
                    (1.-smoothstep(.92,.965,uv.x))*
                    smoothstep(.08,.12,uv.y)*
                    (1.-smoothstep(.90,.95,uv.y));
                float4 live=tex2D(_LiveText,uv);
                return float4(lerp(saturate(art+glow),live.rgb,live.a),1.);
            }
            ENDCG
        }
    }
    Fallback Off
}
