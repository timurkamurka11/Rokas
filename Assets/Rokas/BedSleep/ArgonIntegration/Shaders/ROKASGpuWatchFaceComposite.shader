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
                    // Strong but restrained two-tone timer arc. The band
                    // remains entirely outside the numeral's center.
                    float2 c=float2(.5,.537);
                    float fraction=saturate(_Duration01);
                    float2 d=uv-c;
                    float angle=frac(atan2(d.x,d.y)/6.2831853+1.);
                    float radial=length(d);
                    float arcSpan=1.-smoothstep(fraction-.012,
                        fraction+.004,angle);
                    float thinBand=1.-smoothstep(.003,.012,
                        abs(radial-.307));
                    float outerBand=1.-smoothstep(.002,.009,
                        abs(radial-.319));
                    float3 bandColor=lerp(cyan,violet,
                        smoothstep(.40,.95,angle));
                    glow+=bandColor*(thinBand*.72+
                        outerBand*.24)*arcSpan*(.81+.09*sin(tick*1.8));
                    float a=fraction*6.2831853;
                    float2 endpoint=c+.307*float2(sin(a),cos(a));
                    glow+=bandColor*halo(uv,endpoint,.012)*.58;
                }
                else if (_FaceIndex < 1.5)
                {
                    // Dynamic precision dial: active ticks illuminate
                    // with the selected duration, not a blurred full ring.
                    float2 c=float2(.5,.50);
                    float fraction=saturate(_Duration01);
                    float a=lerp(-2.36,2.36,fraction);
                    float2 d=uv-c;
                    float2 endpoint=c+.306*float2(sin(a),cos(a));
                    float radius=length(d);
                    float phase=atan2(d.x,d.y);
                    float progress=saturate((phase+2.36)/4.72);
                    float lit=1.-smoothstep(fraction-.015,
                        fraction+.004,progress);
                    float ticks=pow(max(0.,cos(phase*48.)),28.);
                    float fine=1.-smoothstep(.005,.011,
                        abs(radius-.307));
                    float3 tint=lerp(cyan,violet,progress);
                    glow+=tint*(fine*.56*lit+
                        ticks*fine*.34*lit)*
                        (.88+.09*sin(tick*1.55));
                    glow+=lerp(cyan,violet,fraction)*
                        halo(uv,endpoint,.012)*.59;
                }
                else if (_FaceIndex < 2.5)
                {
                    // Alarm: selection markers above cards, no panels
                    // or ghost borders over the original Russian labels.
                    float mode=floor(_AlarmMode+.5);
                    float2 mark=float2(.178+mode*.309,.366);
                    float bar=(1.-smoothstep(.002,.008,
                        abs(uv.y-mark.y)))*
                        (1.-smoothstep(.11,.135,abs(uv.x-mark.x)));
                    glow+=cyan*bar*(.41+.05*sin(tick*1.8));
                    float enabled=step(.5,_AlarmOn);
                    glow+=cyan*halo(uv,
                        float2(lerp(.785,.885,enabled),.827),.019)*
                        (.12+.15*enabled);
                }
                else if (_FaceIndex < 3.5)
                {
                    // Only drift the cloud band a tiny amount. Captions,
                    // toggle, crescent and selected cards stay stationary.
                    float cloudArea=smoothstep(.465,.515,uv.y)*
                        (1.-smoothstep(.615,.677,uv.y))*
                        smoothstep(.06,.13,uv.x)*
                        (1.-smoothstep(.87,.95,uv.x));
                    float horizontal=.0028*sin(tick*.16);
                    float3 drifting=tex2D(_MainTex,
                        uv+float2(horizontal,0.)).rgb;
                    art=lerp(art,drifting,cloudArea*.66);

                    for(int n=0;n<3;n++)
                    {
                        float enabled=fmod(floor(_QuietMask/
                            exp2((float)n)),2.);
                        float cx=.195+.317*n;
                        float dash=(1.-smoothstep(.003,.011,
                            abs(uv.y-.465)))*
                            (1.-smoothstep(.11,.137,
                            abs(uv.x-cx)));
                        glow+=cyan*dash*enabled*.38;
                    }
                    float quietOn=step(.5,_QuietMask);
                    float toggleX=lerp(.77,.885,quietOn);
                    glow+=cyan*halo(uv,
                        float2(toggleX,.83),.019)*
                        (.12+.14*quietOn);
                    // Slow moon halo and tiny star glints; restrained so
                    // the authored text and moon details stay crisp.
                    float lunarPulse=.5+.5*sin(tick*1.08);
                    glow+=float3(.08,.23,.54)*
                        halo(uv,float2(.49,.715),.105)*
                        (.025+.035*lunarPulse);
                    float starA=halo(uv,float2(.704,.73),.004);
                    float starB=halo(uv,float2(.321,.792),.003);
                    float starC=halo(uv,float2(.59,.81),.003);
                    glow+=cyan*(starA*(.11+.15*sin(tick*.63)*sin(tick*.63))+
                        starB*(.06+.11*sin(tick*.48+2.)*sin(tick*.48+2.))+
                        starC*(.06+.12*sin(tick*.76+1.)*sin(tick*.76+1.)));
                }
                else
                {
                    // Light touch only: thin time-planning sweep inside
                    // the chart. The original phase graph remains intact.
                    float planned=lerp(.12,.91,saturate(_Duration01));
                    float sweep=lerp(.12,planned,frac(tick*.037));
                    float cursor=1.-smoothstep(.001,.0035,
                        abs(uv.x-sweep));
                    float region=smoothstep(.48,.51,uv.y)*
                        (1.-smoothstep(.735,.76,uv.y));
                    glow+=lerp(cyan,violet,.5+.5*sin(tick*.7))*
                        cursor*region*.30;
                    if(_PhaseMode>.5)
                        glow+=cyan*halo(uv,float2(planned,.66),.015)*.25;
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
