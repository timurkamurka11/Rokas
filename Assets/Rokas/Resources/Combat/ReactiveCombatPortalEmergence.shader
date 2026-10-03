Shader "Rokas/ReactiveCombat/PortalEmergence"
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
        _PortalCenter ("Aperture Center", Vector) = (0,0,0,0)
        _PortalNormal ("Plane Normal", Vector) = (0,0,-1,0)
        _PortalRight ("Plane Right", Vector) = (1,0,0,0)
        _PortalRadius ("Radius and Depth", Vector) = (.9,1.9,.65,0)
        _PortalPhase ("Presentation Phase", Float) = 0
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
        #include "ReactiveCombatPortalBoundary.cginc"
        sampler2D _MainTex, _BumpMap, _MetallicGlossMap, _EmissionMap;
        fixed4 _Color, _EmissionColor;
        float _Glossiness, _GlossMapScale, _Metallic, _BumpScale;
        float4 _PortalCenter, _PortalNormal, _PortalRight, _PortalRadius;
        float _PortalPhase;
        struct Input { float2 uv_MainTex; float2 uv_BumpMap; float3 worldPos; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 p=IN.worldPos-_PortalCenter.xyz;
            float depth=dot(p,_PortalNormal.xyz);
            float2 aperture=float2(dot(p,_PortalRight.xyz)/_PortalRadius.x,p.y/_PortalRadius.y);
            if(depth<0)
            {
                // The back volume is visible only through the aperture. The moving
                // skinned mesh crosses this fixed plane continuously, fragment by fragment.
                float boundary=portalAngularBoundary(atan2(aperture.y,aperture.x),_PortalPhase);
                clip(boundary-length(aperture));
                clip(depth+_PortalRadius.z);
            }
            fixed4 albedo=tex2D(_MainTex,IN.uv_MainTex)*_Color;
            float behind=saturate(-depth/_PortalRadius.z);
            o.Albedo=albedo.rgb*lerp(1,.38,behind);
            #if defined(_METALLICGLOSSMAP)
                fixed4 metal=tex2D(_MetallicGlossMap,IN.uv_MainTex);
                o.Metallic=metal.r;
                o.Smoothness=metal.a*_GlossMapScale;
            #else
                o.Metallic=_Metallic;
                o.Smoothness=_Glossiness;
            #endif
            #if defined(_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A)
                o.Smoothness=albedo.a*_GlossMapScale;
            #endif
            #if defined(_NORMALMAP)
                o.Normal=UnpackScaleNormal(tex2D(_BumpMap,IN.uv_BumpMap),_BumpScale);
            #endif
            #if defined(_EMISSION)
                o.Emission=tex2D(_EmissionMap,IN.uv_MainTex).rgb*_EmissionColor.rgb;
            #endif
            o.Emission+=float3(.06,.03,.11)*(1-smoothstep(0,.09,abs(depth)));
            o.Alpha=albedo.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
