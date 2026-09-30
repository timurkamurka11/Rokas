Shader "Rokas/ReactiveCombat/BladeTrail"
{
    Properties
    {
        _Color ("Blade Energy", Color) = (.75,.9,1,1)
        _Opacity ("Visibility", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            fixed4 _Color;
            float _Opacity;
            v2f vert(appdata v)
            {
                v2f o;
                o.pos=UnityObjectToClipPos(v.vertex);
                o.uv=v.uv;
                o.color=v.color*_Color;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float across=sin(saturate(i.uv.y)*3.14159265);
                float core=pow(across,3);
                float alpha=i.color.a*_Opacity*across;
                clip(alpha-.002);
                return fixed4(lerp(i.color.rgb,float3(.95,.98,1),core*.75),alpha);
            }
            ENDCG
        }
    }
}
