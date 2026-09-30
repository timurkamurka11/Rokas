Shader "Rokas/ReactiveCombat/PortalVolume"
{
    Properties
    {
        _Color ("Energy", Color) = (.32,.24,.48,1)
        _Opacity ("Visibility", Range(0,1)) = 0
        _Phase ("Presentation Phase", Float) = 0
        _Opening ("Opening", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        // Preserve coverage for the transparent actor render texture.
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            fixed4 _Color;
            float _Opacity, _Phase, _Opening;
            v2f vert(appdata v)
            {
                v2f o;
                o.pos=UnityObjectToClipPos(v.vertex);
                o.uv=v.uv;
                return o;
            }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 a=floor(p), f=frac(p);
                f=f*f*(3-2*f);
                return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),
                    lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 p=(i.uv-.5)*2;
                float r=length(p);
                float theta=atan2(p.y,p.x);
                float angle=theta + r*3.8 - _Phase*.58;
                float2 flow=float2(cos(angle),sin(angle))*r*3.2;
                float clouds=noise(flow+float2(_Phase*.11,-_Phase*.08));
                clouds=clouds*.65+noise(flow*2.1+17.3)*.35;
                float strands=pow(saturate(.5+.5*sin(angle*7+r*7+clouds*5)),9);
                float rim=exp(-abs(r-.85)*37);
                float inner=exp(-abs(r-.65-clouds*.09)*14)*strands;
                float depth=(1-smoothstep(.12,.8,r))*.18;
                float3 dark=float3(.018,.011,.032)+clouds*float3(.018,.012,.035);
                float3 energy=_Color.rgb*(inner*.6+rim*1.8+strands*.1*r);
                energy+=float3(.15,.29,.31)*pow(saturate(clouds-.4),2)*r*.35;
                float alpha=(1-smoothstep(.88,1,r))*_Opacity;
                // An opaque depth core, translucent cloudy edges, no rectangular carrier.
                alpha*=lerp(.98,.55,smoothstep(.72,.94,r));
                clip(alpha-.002);
                return fixed4(dark+energy+depth*float3(.02,.01,.05),alpha);
            }
            ENDCG
        }
    }
}
