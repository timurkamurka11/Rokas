Shader "ROKAS/Home/CustomGlowRuntime"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Brightness ("Brightness", Range(0.25,2)) = 1
        _PulseAmount ("Pulse Amount", Range(0,0.25)) = 0
        _PulseSpeed ("Pulse Speed", Float) = 0.24
        _ShimmerAmount ("Shimmer Amount", Range(0,1.5)) = 0
        _ShimmerSpeed ("Shimmer Speed", Float) = 0.14
        _ShimmerWidth ("Shimmer Width", Range(0.01,0.5)) = 0.14
        _ShimmerAxis ("Shimmer Axis", Range(0,1)) = 0
        _OpacityMultiplier ("Opacity Multiplier", Range(0,1)) = 1
        _PhaseOffset ("Phase Offset", Range(0,1)) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata_t { float4 vertex:POSITION; float4 color:COLOR; float2 texcoord:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 texcoord:TEXCOORD0; float4 worldPosition:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex; fixed4 _Color; fixed4 _TextureSampleAdd; float4 _ClipRect;
            float _Brightness,_PulseAmount,_PulseSpeed,_ShimmerAmount,_ShimmerSpeed,_ShimmerWidth,_ShimmerAxis,_OpacityMultiplier,_PhaseOffset;
            v2f vert(appdata_t v) { v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.worldPosition=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex); o.texcoord=v.texcoord; o.color=v.color*_Color; return o; }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 source=(tex2D(_MainTex,i.texcoord)+_TextureSampleAdd)*i.color; fixed4 color=source;
                float pulsePhase=(_Time.y*_PulseSpeed+_PhaseOffset)*6.28318530718;
                float pulseWave=.5+.5*sin(pulsePhase); float pulse=lerp(1.0-_PulseAmount,1.0+_PulseAmount,pulseWave);
                float axisUv=lerp(i.texcoord.x,i.texcoord.y,saturate(_ShimmerAxis));
                float travel=frac(_Time.y*_ShimmerSpeed+_PhaseOffset); float wrapped=abs(frac(axisUv-travel+.5)-.5);
                float width=max(_ShimmerWidth,.0001); float coreBand=1.0-smoothstep(width*.12,width,wrapped); float featherBand=1.0-smoothstep(width,width*2.4,wrapped);
                float shimmer=saturate(coreBand*.78+featherBand*.22)*_ShimmerAmount;
                float coreMask=smoothstep(.42,.94,source.a); float haloMask=smoothstep(.025,.48,source.a)*(1.0-coreMask); float gate=smoothstep(.02,.28,source.a);
                half3 warm=half3(1.0,.84,.58); color.rgb*=max(0.0,pulse)*_Brightness; color.rgb+=coreMask*(.10*_Brightness); color.rgb+=haloMask*(.035*_Brightness); color.rgb+=warm*(shimmer*gate);
                color.a*=lerp(1.0,max(0.0,pulse),.16)*_OpacityMultiplier;
                #ifdef UNITY_UI_CLIP_RECT
                color.a*=UnityGet2DClipping(i.worldPosition.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a-.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}