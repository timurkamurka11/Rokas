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
        // The emissive OLED is a PHYSICAL opaque solid panel, not a
        // late-sorted transparent decal. The genuine sapphire glass draws
        // after it. This prevents bezel/glass depth fights as the wrist turns.
        Tags { "Queue"="Geometry+20" "RenderType"="Opaque" "IgnoreProjector"="True" }
        Pass
        {
            Cull Off
            ZWrite On
            // Physical depth test: the OLED may only appear if its ORIGINAL
            // surface is in front of the skinned wrist and watch casing.
            // No through-skin HUD / painted square.
            ZTest LEqual
            // Small depth bias only on the actual OLED triangle plane.
            // No billboard or overlay follows the camera.
            Offset -0.25, -0.25
            Blend Off
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
                // Keep the high-contrast screen physically inside the actual
                // square display, with a restrained sapphire-black edge.
                // Opaque corners do not expose the moving skinned wrist behind.
                float2 uv=saturate(i.uv);
                float2 pt=abs(uv-.5);
                float2 q=max(pt-float2(.425,.425),float2(0,0));
                float dist=length(q)-.045;
                float inner=saturate(1.0 - smoothstep(-.018,.012,dist));
                float3 crisp = saturate((ui.rgb - 0.045) * _Contrast + 0.045);
                crisp *= lerp(.065,1.0,inner);
                crisp = saturate(crisp * _Brightness * glassDepth);
                // Physical sapphire-like reflection is intentionally very
                // restrained. Mostly visible on lit OLED graphics, not a
                // glowing HUD square laid over the original real watch.
                float diagonal = abs(i.uv.y - (.93 - i.uv.x*.22));
                float glassSweep = exp(-diagonal*diagonal*310.0);
                float lum = dot(crisp, float3(.2126,.7152,.0722));
                float polished = glassSweep * .026 * smoothstep(.02,.24,lum);
                crisp = saturate(crisp + float3(polished*.62,
                    polished*.77,polished));
                // Full coverage: hardware depth, not mask translucency.
                return fixed4(crisp, 1.0) * _Color;
            }
            ENDCG
        }
    }
    Fallback Off
}
