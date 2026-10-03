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
            struct v2f { float4 pos:SV_POSITION;float2 uv:TEXCOORD0; };
            v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                float2 p=((i.uv*2-1)*1.25-float2(0,-.612*_Seed))/max(.001,_Opening);
                float r=length(p),a=atan2(p.y,p.x),boundary=portalAngularBoundary(a,_Phase);
                float mask=1-smoothstep(boundary-.025,boundary+.015,r);clip(mask*_Opacity-.001);
                // Independently flowing warped slices converge into a black throat.
                float strands=0,cyan=0,fog=0;
                for(int layer=0;layer<2;layer++)
                {
                    float z=layer*.37, angle=a+r*(.75+z*.6)-_Phase*(.25+z*.24);
                    float2 q=float2(cos(angle),sin(angle))*(pow(r,.7)*1.6+z);
                    float n=portalFbm(q*1.75+float2(_Phase*.08,-_Phase*.06)+layer*7.8);
                    float vein=pow(saturate(1-abs(n*2-1)),18);
                    strands+=vein*(.22+layer*.11)*smoothstep(.08,.32,r)*(1-smoothstep(.88,1.06,r));
                    cyan+=pow(saturate(1-abs(n*2-1.08)),16)*.32*smoothstep(.2,.50,r);fog+=n*.035;
                }
                float rim=exp(-abs(r-boundary)*48)*(1-_Seed*.8);
                float3 color=float3(.004,.002,.009)+float3(.45,.16,.72)*strands+float3(.10,.44,.62)*cyan;
                color*=smoothstep(.10,.34,r);
                color+=float3(.05,.016,.08)*fog+float3(.65,.49,1)*rim*.8;
                color*=lerp(1,.32,smoothstep(.05,.34,r)*_Seed);
                return fixed4(color,mask*_Opacity*.995);
            }
            ENDCG
        }
    }
}
