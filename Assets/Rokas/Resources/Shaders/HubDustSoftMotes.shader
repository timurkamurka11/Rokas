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
                float shape=floor(i.uv.x*.5+.001);
                float2 uv=float2(i.uv.x-shape*2.0,i.uv.y);
                float2 p=uv*2.0-1.0;
                float r=length(p);

                // Diagnostic is available ONLY if explicitly selected
                // in HubDustCanvasGraphic Inspector.
                if (_DustMode>1.5)
                {
                    if (shape<7.5)
                    {
                        float disk=1.0-smoothstep(.78,.86,r);
                        // Premultiplied debug output.
                        return fixed4(float3(1.0,0.0,.85)*disk,disk);
                    }
                    float ring=exp(-pow((r-.70)/.09,2.0));
                    float da=saturate(.9*ring);
                    return fixed4(float3(1.0,.82,.3)*da,da);
                }

                if (shape>7.5)
                {
                    // V6 radial glow is deliberately additive and
                    // non-occluding. The old six long grey beams are gone.
                    // Equivalent to pow(smoothstep(1,0,r),2.5) but
                    // expressed with increasing smoothstep edges.
                    float radial=pow(saturate(1.0-smoothstep(0.0,1.0,r)),2.5);
                    float subtleAir= .98+.02*sin(4.1*p.x+5.3*p.y+_Time.y*.12);
                    float glow=radial*subtleAir*saturate(i.color.a);
                    // outAlpha=0: glow adds amber light without dimming
                    // any portion of the underlying photographic room.
                    return fixed4(i.color.rgb*glow,0.0);
                }

                // The eight uncompressed source-derived RGBA32 shapes
                // remain bound to CanvasRenderer and _DustAtlasTex.
                float row=floor(shape*.25);
                float col=shape-row*4.0;
                float2 atlasUv=float2(
                    (col+clamp(uv.x,.004,.996))*.25,
                    (row+clamp(uv.y,.004,.996))*.5);
                float srcAlpha=tex2D(_DustAtlasTex,atlasUv).a;
                float fallback=exp(-6.5*r*r)*
                    (1.0-smoothstep(.66,.88,r));
                float profile=lerp(fallback,srcAlpha,_UseAtlas);
                // One alpha calculation, one premultiplication, no
                // overbright specular kernel turning dust white.
                float outAlpha=saturate(i.color.a*profile);
                float3 premulRGB=i.color.rgb*outAlpha;
                return fixed4(premulRGB,outAlpha);
            }
            ENDCG
        }
    }
    Fallback Off
}