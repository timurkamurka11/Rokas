Shader "Rokas/ReactiveCombat/PortalVolume"
{
    Properties
    {
        _Opacity("Coverage",Float)=0
        _Phase("Paused presentation clock",Float)=0
        _Opening("Aperture size",Float)=1
        _Seed("Kernel phase",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "ReactiveCombatPortalBoundary.cginc"
            float _Opacity,_Phase,_Opening,_Seed;
            struct v2f { float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 view:TEXCOORD1; };
            v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;o.view=ObjSpaceViewDir(v.vertex);return o;}
            float sectorPulse(float sector)
            {
                float cycle=frac(_Phase*(.72+sector*.13)+sector*.237+(sector==0?.14:0));
                float pulse=smoothstep(.09,.13,cycle)*(1-smoothstep(.28,.40,cycle));
                if(sector==0 || sector==2)pulse+=.72*smoothstep(.46,.50,cycle)*(1-smoothstep(.60,.69,cycle));
                return saturate(pulse);
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 p=((i.uv*2-1)*1.25-float2(0,-.612*_Seed))/max(.001,_Opening);
                float r=length(p),a=atan2(p.y,p.x),boundary=portalAngularBoundary(a,_Phase);
                float mask=1-smoothstep(boundary-.025,boundary+.015,r);clip(mask*_Opacity-.001);
                // Three view-dependent slices have different centers, radial speeds and scale.
                // Their attachment sectors share the outer discharge rhythm, not a uniform spiral.
                float3 energy=0;
                float2 parallax=i.view.xy/max(.25,abs(i.view.z));
                for(int layer=0;layer<3;layer++)
                {
                    float2 drift=float2(sin(_Phase*.47+layer*2.3),cos(_Phase*.39-layer))*float2(.055,.075);
                    float2 q=p-drift+parallax*(.14-layer*.065);
                    float radius=length(q),angle=atan2(q.y,q.x);
                    float speed=.10+layer*.145;
                    float noise=portalFbm(q*(1.1+layer*.72)+float2(_Phase*.09,-_Phase*.075)+layer*7.8);
                    float twist=angle+pow(radius,.62)*(1.50+layer*.31)-_Phase*speed
                        +sin(angle*3+radius*5-_Phase*.41)*.32+(noise-.5)*1.25;
                    float field=sin(twist*(3+layer*2)+noise*(3.5+layer))
                        +sin(twist*2-radius*8+layer)*.32;
                    float ridge=saturate(1-abs(field));
                    float thread=pow(ridge,8+layer*5);
                    float aura=pow(ridge,2+layer*3);
                    float broken=smoothstep(.28,.65,noise+.19*sin(radius*13+_Phase*(.8+layer*.6)));
                    float depth=smoothstep(.10,.29,radius)*(1-smoothstep(.85,1.09,radius));
                    float pull=0;
                    for(int sector=0;sector<4;sector++)
                    {
                        float anchor=sector==0?2.08:sector==1?1.30:sector==2?3.58:5.82;
                        pull+=pow(saturate(.5+.5*cos(angle-anchor)),24)*sectorPulse(sector);
                    }
                    float hot=pow(saturate(noise+.22*pull),3);
                    float3 tint=layer==2 ? float3(.22,.63,.86) : float3(.66,.44,1.02);
                    float haze=layer==0?.38:layer==1?.80:.18;
                    energy+=tint*(thread*(.32+layer*.05)+aura*haze)*depth*(.07+.93*broken)*(1+pull*.9);
                    energy+=float3(.96,.80,1.15)*thread*hot*depth*.65;
                    energy+=float3(.055,.036,.09)*noise*depth*(layer==0?1:.28);
                }
                float rim=exp(-abs(r-boundary)*48)*(1-_Seed*.8);
                float3 color=float3(.004,.002,.009)+energy*1.65;
                color*=smoothstep(.10,.34,r);
                color+=float3(.65,.49,1)*rim*.8;
                color*=lerp(1,.32,smoothstep(.05,.34,r)*_Seed);
                return fixed4(color,mask*_Opacity*.995);
            }
            ENDCG
        }
    }
}
