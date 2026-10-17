Shader "ROKAS/BedSleep/ArgonOLEDLiveSurface"
{
    Properties
    {
        _MainTex ("Real Watch OLED RenderTexture", 2D) = "black" {}
        _Color ("Color Tint", Color) = (1,1,1,1)
        _Contrast ("OLED Contrast", Range(1,1.4)) = 1.10
        _Brightness ("OLED Luminance", Range(.8,1.3)) = 1.06
        _EdgeFalloff ("Subtle Glass Depth", Range(0,.20)) = .055
    }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Cull Off
            ZWrite Off
            // Physical depth test: the OLED may only appear if its ORIGINAL
            // surface is in front of the skinned wrist and watch casing.
            // No through-skin HUD / painted square.
            ZTest LEqual
            Blend One Zero
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Contrast, _Brightness, _EdgeFalloff;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                // Genuine OLED gameplay RenderTexture on the authentic physical
                // UV mesh. Rich blacks, crisp low-glare color, barely perceptible
                // optical falloff near glass rim. No fake HUD, no glow quad.
                fixed4 ui = tex2D(_MainTex, i.uv);
                float edge = min(min(i.uv.x, 1.0 - i.uv.x),
                                 min(i.uv.y, 1.0 - i.uv.y));
                float glassDepth = 1.0 - _EdgeFalloff *
                    (1.0 - smoothstep(0.015, 0.135, edge));
                float3 crisp = saturate((ui.rgb - 0.045) * _Contrast + 0.045);
                crisp = saturate(crisp * _Brightness * glassDepth);
                return fixed4(crisp, 1.0) * _Color;
            }
            ENDCG
        }
    }
    Fallback Off
}
