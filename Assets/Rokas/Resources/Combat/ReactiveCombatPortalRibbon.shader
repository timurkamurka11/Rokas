Shader "Rokas/ReactiveCombat/PortalRibbon"
{
    Properties { _Color("Energy",Color)=(1,.2,.7,1) _Opacity("Coverage",Float)=0 _Phase("Clock",Float)=0 _Seed("Strand seed",Float)=0 }
    SubShader
    {
        Tags {"Queue"="Transparent+3" "RenderType"="Transparent"}
        Blend One OneMinusSrcAlpha ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "ReactiveCombatPortalBoundary.cginc"
            float4 _Color;float _Opacity,_Phase,_Seed;
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
            v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                float edge=saturate(1-abs(i.uv.y*2-1));
                float n=portalFbm(float2(i.uv.x*19-_Phase*2.8,_Seed+i.uv.y*3));
                float alpha=pow(edge,.6)*_Opacity*(.85+.15*n)*smoothstep(0,.07,i.uv.x)*(1-smoothstep(.85,1,i.uv.x));
                float luminousEdge=pow(saturate(1-abs(edge-.26)*5),2);
                float3 c=float3(.005,.002,.011)+_Color.rgb*(luminousEdge*(2.4+2*n)+pow(edge,12)*.15);
                return fixed4(c*alpha,alpha);
            }
            ENDCG
        }
    }
}
