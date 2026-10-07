Shader "ROKAS/UI/VideoCornerCloneMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Video Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SourceRect ("Source Rect", Vector) = (0,0,1,1)
        _InnerRadius ("Inner Radius", Range(0,1)) = 0.39
        _OuterRadius ("Outer Radius", Range(0,1.5)) = 0.96
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _SourceRect;
            float _InnerRadius;
            float _OuterRadius;

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 localUv : TEXCOORD0;
            };

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _Color;
                output.localUv = input.texcoord;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 sourceUv = _SourceRect.xy + input.localUv * _SourceRect.zw;
                fixed4 color = tex2D(_MainTex, sourceUv) * input.color;

                float2 centered = (input.localUv - .5) * 2.0;
                float distanceFromCenter = length(centered);
                float feather = 1.0 - smoothstep(_InnerRadius, _OuterRadius, distanceFromCenter);
                color.a *= saturate(feather);
                return color;
            }
            ENDCG
        }
    }
}
