Shader "Rokas/ReactiveCombat/PortalRibbon"
{
    Properties { _Color("Energy",Color)=(1,.2,.7,1) _Opacity("Coverage",Float)=0 _Phase("Clock",Float)=0 _Seed("Strand seed",Float)=0 _Role("Primary secondary filament",Float)=0 }
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
            float4 _Color;float _Opacity,_Phase,_Seed,_Role;
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
            v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                float noise=portalFbm(float2(i.uv.x*(12+_Role*8)-_Phase*(1.2+_Role*.8),_Seed+i.uv.y*4));
                float across=abs(i.uv.y*2-1);
                float edge=saturate(1-across/(.82+.18*noise));
                float alpha=pow(edge,.52)*_Opacity*smoothstep(0,.055,i.uv.x)*(1-smoothstep(.87,1,i.uv.x));
                float flank=pow(saturate(1-abs(edge-.32)*2.4),2);
                float stream=.5+.5*sin(i.uv.x*23-_Phase*(5.2+_Role*2.1)+_Seed+noise*4);
                float hot=pow(stream,9)*pow(noise,2);
                float core=pow(edge,8);
                float3 c=float3(.008,.003,.016)+_Color.rgb*(flank*(.65+noise*.8)+hot*.85+edge*.15);
                c=lerp(c,float3(.015,.006,.028),core*.82);
                c+=float3(1.4,1.24,1.6)*hot*flank*.85;
                return fixed4(c*alpha,alpha);
            }
            ENDCG
        }
    }
}
