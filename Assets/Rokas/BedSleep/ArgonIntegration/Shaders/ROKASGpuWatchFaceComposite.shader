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
                // Suppress highlights baked into the reference PNG when
                // its new live controls are deselected. Keep typography.
                if(_FaceIndex>1.5 && _FaceIndex<2.5)
                {
                    float2 lo=float2(.035,.165),hi=float2(.32,.359);
                    float2 bounds=smoothstep(lo,lo+.012,uv)*
                        (1.-smoothstep(hi-.012,hi,uv));
                    art*=1.-bounds.x*bounds.y*
                        (step(.5,_AlarmMode)*.30);
                    if(_AlarmOn<.5 && uv.x>.72 && uv.y>.73 &&
                        uv.x<.96 && uv.y<.88)
                        art*=.53;
                }
                if(_FaceIndex>2.5 && _FaceIndex<3.5)
                {
                    for(int n=0;n<3;n++)
                    {
                        float bit=fmod(floor(_QuietMask/exp2((float)n)),2.);
                        float2 lo=float2(.033+(float)n*.31,.16);
                        float2 hi=lo+float2(.29,.19);
                        float2 bounds=smoothstep(lo,lo+.012,uv)*
                            (1.-smoothstep(hi-.012,hi,uv));
                        art*=1.-bounds.x*bounds.y*(1.-bit)*.53;
                    }
                    if(_QuietMask<.5 && uv.x>.62 && uv.y>.76 &&
                        uv.x<.94 && uv.y<.87)
                        art*=.47;
                }
                if(_FaceIndex<.5)
                {
                    // Timer: real selected duration drives the ring length.
                    // Static artwork is preserved below the new live tracer.
                    float arc=curve(uv,center,.335,.0035,
                        saturate(_Duration01)*.95);
                    float ang=_Duration01*6.2831853;
                    float2 tip=center+.335*float2(sin(ang),cos(ang));
                    glow+=cyan*(arc*.7*energy+halo(uv,tip,.017)*1.1);
                }
                else if(_FaceIndex<1.5)
                {
                    // Dial: an animated selection tracer travels to the
                    // chosen duration, with a soft moving tick when hovered.
                    float angle=lerp(-2.36,2.36,saturate(_Duration01));
                    float2 tip=center+.317*float2(sin(angle),cos(angle));
                    glow+=(lerp(cyan,violet,.5+.5*sin(tick*.8))) *
                        (halo(uv,tip,.018)*.9+
                        ring(uv,center,.315,.0025)*.11*energy);
                }
                else if(_FaceIndex<2.5)
                {
                    // Alarm: selected one of 3 physical buttons; the
                    // highlight is composited over the authored button art.
                    float mode=floor(_AlarmMode+.5);
                    float2 lo=float2(.035+mode*.31,.165);
                    float2 hi=lo+float2(.285,.194);
                    float selected=boxOutline(uv,lo,hi,.009);
                    glow+=cyan*(selected*1.35+energy*selected*.2);
                    float on=step(.5,_AlarmOn);
                    // Real enabled state over the image's upper switch.
                    float2 toggle=float2(.84,.825);
                    glow+=lerp(float3(.12,.06,.02),cyan,
                        on)*halo(uv,toggle,.047)*(.12+.15*on);
                }
                else if(_FaceIndex<3.5)
                {
                    // Quiet mode: three toggles have individual fade and
                    // hover response; static icon artwork is left untouched.
                    for(int k=0;k<3;k++)
                    {
                        float bit=fmod(floor(_QuietMask/exp2((float)k)),2.);
                        float2 lo=float2(.033+(float)k*.31,.16);
                        float2 hi=lo+float2(.29,.19);
                        float outl=boxOutline(uv,lo,hi,.008);
                        glow+=lerp(float3(.045,.035,.015),cyan,
                            bit)*outl*(bit*.7+.1);
                    }
                    glow+=cyan*halo(uv,float2(.79,.815),.036)*
                        (.12+.06*sin(tick*1.8));
                }
                else
                {
                    // Planning graph: sweeping line and glints animate
                    // without misrepresenting authored static phase curves
                    // as live biometric samples.
                    float wave=frac(tick*.0625);
                    float x=lerp(.10,.91,wave);
                    float line=(1.-smoothstep(.001,.006,abs(uv.x-x)))*
                        smoothstep(.37,.44,uv.y)*
                        (1.-smoothstep(.73,.79,uv.y));
                    glow+=lerp(cyan,violet,.5+.5*sin(tick))*
                        (line*.48+
                        halo(uv,float2(x,.55+.04*sin(tick*1.7)),.015)*.5);
                    if(_PhaseMode>.5)
                        glow+=cyan*halo(uv,float2(.54,.56),.043)*.12;
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
