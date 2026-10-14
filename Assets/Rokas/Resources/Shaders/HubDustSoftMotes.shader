Shader "ROKAS/UI/HubDustSoftMotes"
{
    Properties
    {
        _MainTex ("CanvasRenderer UI Texture", 2D) = "white" {}
        _DustAtlasTex ("Full RGBA32 4x2 1024x512 Dust Atlas", 2D) = "white" {}
        _UseAtlas ("Use Eight Full-Res Shapes", Float) = 0
        _DustMode ("0 Normal, 1 High Contrast, 2 Diagnostic", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent"
            "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp]
            ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local __ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"

            sampler2D _DustAtlasTex;
            float _UseAtlas;
            float _DustMode;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex=UnityObjectToClipPos(v.vertex);
                o.color=v.color;
                o.uv=v.uv;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                // Pack particle class into the atlas shape id without
                // extra UI Canvas shader channels:
                // 0..7 micro, 10..17 medium, 20..27 bokeh, 30 glow.
                float packedShape=floor(i.uv.x*.5+.001);
                float kind=floor(packedShape*.1);
                float shape=packedShape-kind*10.0;
                float2 uv=float2(i.uv.x-packedShape*2.0,i.uv.y);
                float2 p=uv*2.0-1.0;
                float r=length(p);

                // Diagnostic requires explicit Inspector selection.
                if (_DustMode>1.5)
                {
                    if (kind<2.5)
                    {
                        float disk=1.0-smoothstep(.76,.86,r);
                        return fixed4(float3(1.0,0.0,.85)*disk,disk);
                    }
                    float ring=exp(-pow((r-.70)/.09,2.0));
                    float da=saturate(.9*ring);
                    return fixed4(float3(1.0,.82,.3)*da,da);
                }

                if (kind>2.5)
                {
                    // Four broad photographic lamp volumes, no strip quads.
                    // Non-occluding additive RGB, ZERO output alpha.
                    float radial=pow(saturate(1.0-smoothstep(0.0,1.0,r)),2.5);
                    float subtleAir=.98+.02*sin(4.1*p.x+5.3*p.y+_Time.y*.12);
                    float glow=radial*subtleAir*saturate(i.color.a);
                    return fixed4(i.color.rgb*glow,0.0);
                }

                // Preserve the 1024x512, eight-cell source atlas and its
                // organic edge profile (not hard opaque square sprites).
                float row=floor(shape*.25);
                float col=shape-row*4.0;
                float2 atlasUv=float2(
                    (col+clamp(uv.x,.004,.996))*.25,
                    (row+clamp(uv.y,.004,.996))*.5);
                float srcAlpha=tex2D(_DustAtlasTex,atlasUv).a;
                float fallback=exp(-6.5*r*r)*
                    (1.0-smoothstep(.66,.88,r));
                float warmHalo=lerp(fallback,srcAlpha,_UseAtlas);

                // Visible cream micro-core inside a transparent amber halo.
                // Optical bokeh must retain its hollow/ring atlas silhouette.
                float coreMask=kind>1.5 ? 0.0 : exp(-r*r*24.0)*
                    (kind>.5 ? .68 : 1.0);
                float profile=saturate(max(warmHalo*.91,coreMask*.64));
                float finalAlpha=saturate(i.color.a*profile);
                // CPU vertex alpha already includes light-zone proximity,
                // so the core's additive strength follows that field.
                float lightZoneFactor=saturate((i.color.a-.18)/.43);

                half3 creamCore=half3(1.00,.95,.82);
                half3 amberHalo=half3(1.00,.78,.42);
                // At room-light OFF, vertex tint identifies moon/city air.
                float coolShare=saturate((i.color.b-i.color.r)*4.0);
                creamCore=lerp(creamCore,half3(.82,.94,1.00),coolShare);
                amberHalo=lerp(amberHalo,half3(.72,.90,1.00),coolShare);
                half3 litRGB=lerp(amberHalo,creamCore,saturate(coreMask));
                // Mild respect for the vertex tint from photographic zones.
                litRGB=lerp(litRGB,i.color.rgb,.18);
                half3 premulRGB = litRGB * finalAlpha *
                    (1.0 + coreMask * lightZoneFactor * 1.35);
                half outAlpha = finalAlpha *
                    (1.0 - coreMask * lightZoneFactor * 0.45);
                // Blend One OneMinusSrcAlpha: additive color does not
                // darken the photographic room beneath the dust.
                return half4(premulRGB,outAlpha);
            }
            ENDCG
        }
    }
    Fallback Off
}