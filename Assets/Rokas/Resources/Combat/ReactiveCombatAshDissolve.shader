Shader "Rokas/ReactiveCombat/AshDissolve"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = .2
        _GlossMapScale ("Smoothness Scale", Range(0,1)) = 1
        _Metallic ("Metallic", Range(0,1)) = 0
        _MetallicGlossMap ("Metallic", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        _EmissionColor ("Emission", Color) = (0,0,0,0)
        _EmissionMap ("Emission", 2D) = "white" {}
        _Dissolve ("Ash Progress", Range(0,1)) = 0
        _DissolveOrigin ("Noise Origin", Vector) = (0,0,0,0)
        _NoiseScale ("Fragment Scale", Float) = 14
        _EdgeWidth ("Fading Edge Width", Range(.001,.2)) = .065
        _AshColor ("Ash Edge", Color) = (.045,.038,.052,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        #pragma shader_feature_local _NORMALMAP
        #pragma shader_feature_local _METALLICGLOSSMAP
        #pragma shader_feature_local _EMISSION
        #pragma shader_feature_local _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
        #include "UnityStandardUtils.cginc"

        sampler2D _MainTex, _BumpMap, _MetallicGlossMap, _EmissionMap;
        fixed4 _Color, _EmissionColor, _AshColor;
        float _Glossiness, _GlossMapScale, _Metallic, _BumpScale;
        float _Dissolve, _NoiseScale, _EdgeWidth;
        float4 _DissolveOrigin;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_BumpMap;
            float3 worldPos;
        };

        float ashHash(float3 p)
        {
            return frac(sin(dot(p, float3(12.9898,78.233,37.719))) * 43758.5453);
        }

        float ashNoise(float3 p)
        {
            float3 cell = floor(p);
            float3 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float x00 = lerp(ashHash(cell), ashHash(cell + float3(1,0,0)), f.x);
            float x10 = lerp(ashHash(cell + float3(0,1,0)), ashHash(cell + float3(1,1,0)), f.x);
            float x01 = lerp(ashHash(cell + float3(0,0,1)), ashHash(cell + float3(1,0,1)), f.x);
            float x11 = lerp(ashHash(cell + float3(0,1,1)), ashHash(cell + float3(1,1,1)), f.x);
            return lerp(lerp(x00, x10, f.y), lerp(x01, x11, f.y), f.z);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            float3 noisePoint = (IN.worldPos - _DissolveOrigin.xyz) * _NoiseScale;
            float noise = ashNoise(noisePoint) * .65 + ashNoise(noisePoint * 1.97 + 11.3) * .35;
            // At zero the original body is intact; at one every fragment is gone.
            float remaining = noise - _Dissolve * 1.02 + .001;
            clip(remaining);
            float edge = (1.0 - smoothstep(0, _EdgeWidth, remaining)) * step(.001, _Dissolve);
            o.Albedo = lerp(albedo.rgb, _AshColor.rgb, edge * .8);
            #if defined(_METALLICGLOSSMAP)
                fixed4 metal = tex2D(_MetallicGlossMap, IN.uv_MainTex);
                o.Metallic = metal.r;
                o.Smoothness = metal.a * _GlossMapScale;
            #else
                o.Metallic = _Metallic;
                o.Smoothness = _Glossiness;
            #endif
            #if defined(_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A)
                o.Smoothness = albedo.a * _GlossMapScale;
            #endif
            #if defined(_NORMALMAP)
                o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_BumpMap), _BumpScale);
            #endif
            #if defined(_EMISSION)
                o.Emission = tex2D(_EmissionMap, IN.uv_MainTex).rgb * _EmissionColor.rgb;
            #endif
            // A dull edge, not a luminous explosion, keeps the death readable.
            o.Emission += _AshColor.rgb * edge * .12;
            o.Alpha = albedo.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
