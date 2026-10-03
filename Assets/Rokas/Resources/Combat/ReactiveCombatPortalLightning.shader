Shader "Rokas/ReactiveCombat/PortalLightning"
{
    Properties { _Color("Emissive energy",Color)=(2.8,2.6,3.4,1) _Opacity("Coverage",Float)=0 _Phase("Clock",Float)=0 _Seed("Discharge seed",Float)=0 }
    SubShader
    {
        Tags {"Queue"="Transparent+4" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "ReactiveCombatPortalBoundary.cginc"
            float4 _Color;float _Opacity,_Phase,_Seed;
            struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
            half4 frag(v2f i):SV_Target
            {
                float across=abs(i.uv.y*2-1);
                float flow=portalNoise(float2(i.uv.x*41-_Phase*7,_Seed+3.7));
                float travel=.5+.5*sin(i.uv.x*31-_Phase*11+flow*4+_Seed);
                float thickness=.36+.60*flow;
                float coverage=pow(saturate(1-across/max(.08,thickness)),.8);
                float alpha=coverage*i.color.a*_Color.a*_Opacity;
                clip(alpha-.001);
                float hotspot=pow(flow,4)*pow(travel,3)*3.6;
                return half4(i.color.rgb*_Color.rgb*(.42+hotspot),alpha);
            }
            ENDCG
        }
    }
}
