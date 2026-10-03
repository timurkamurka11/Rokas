Shader "Rokas/ReactiveCombat/PortalFloor"
{
    Properties { _Opacity("Coverage",Float)=0 _Phase("Clock",Float)=0 _Opening("Growth",Float)=0 _Crossing("Crossing floor response",Float)=0 }
    SubShader
    {
        Tags {"Queue"="Transparent-1" "RenderType"="Transparent"}
        Blend One OneMinusSrcAlpha ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "ReactiveCombatPortalBoundary.cginc"
            float _Opacity,_Phase,_Opening,_Crossing;
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
            v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                float2 p=i.uv*2-1;float r=length(p),a=atan2(p.y,p.x),radius=lerp(.30,.86,saturate(_Opening*2));
                float noise=portalNoise(p*8+_Phase*.5);
                float ring=exp(-abs(r-radius+sin(a*9+_Phase*3)*.018)*85),inner=exp(-r*r*5)*.09;
                float2 crossingPatch=(p-float2(0,-.22))/float2(.35,.24);
                float crossing=exp(-dot(crossingPatch,crossingPatch))*saturate(_Crossing)*(.05+noise*.04);
                float alpha=(ring*(.35+noise*.4)+inner+crossing)*_Opacity*(1-smoothstep(.95,1,r));
                return fixed4(float3(.58,.30,.90)*alpha,alpha);
            }
            ENDCG
        }
    }
}
