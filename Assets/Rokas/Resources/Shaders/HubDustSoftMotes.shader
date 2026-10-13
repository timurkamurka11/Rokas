Shader "ROKAS/UI/HubDustSoftMotes"
{
    Properties
    {
        _MainTex ("CanvasRenderer UI Texture", 2D) = "white" {}
        _DustAtlasTex ("Full RGBA32 4x2 1024x512 Dust Atlas", 2D) = "white" {}
        _UseAtlas ("Use Eight Full-Res Original Shapes", Float) = 0
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
        // Premultiplied output: RGB is multiplied by alpha exactly ONCE.
        // No lightResponse * alpha * lightResponse shadow-crushing.
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
                float4 local : TEXCOORD1;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex=UnityObjectToClipPos(v.vertex);
                o.local=v.vertex;
                o.color=v.color;
                o.uv=v.uv;
                return o;
            }
            float SoftShaft(float shape,float2 p)
            {
                float2 q=p;
                q.x+=.10*q.y+.18*q.y*q.y;
                // A broad but strictly localized, feathered ribbon.
                float longitudinal=exp(-2.2*q.y*q.y);
                float transverse=exp(-4.0*q.x*q.x);
                float grain=.88+.12*sin(7.3*q.y+3.1*q.x);
                if (shape>8.5) {
                    longitudinal=exp(-2.9*q.y*q.y);
                    transverse=exp(-5.8*q.x*q.x);
                }
                return saturate(longitudinal*transverse*grain)*
                    (1.0-smoothstep(.78,1.0,length(p)));
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float shape=floor(i.uv.x*.5+.001);
                float2 uv=float2(i.uv.x-shape*2.0,i.uv.y);
                float2 p=uv*2.0-1.0;
                float radius=length(p);
                float edge=1.0-smoothstep(.82,.98,radius);
                float hotCore=exp(-radius*radius*18.0)*1.6;
                float profile;
                if (shape < 7.5)
                {
                    float row=floor(shape*.25);
                    float col=shape-row*4.0;
                    // 4x2 atlas origin is lower-left. Half-texel safe
                    // coordinates with border zero-alpha in every cell.
                    float2 atlasUv=float2(
                        (col+clamp(uv.x, .004,.996))*.25,
                        (row+clamp(uv.y,.004,.996))*.5);
                    float sourceAlpha=tex2D(_DustAtlasTex,atlasUv).a;
                    float fallback=exp(-6.5*radius*radius)*edge;
                    profile=lerp(fallback,sourceAlpha,_UseAtlas);
                }
                else profile=SoftShaft(shape,p);

                if (_DustMode>1.5)
                {
                    // Unmissable diagnostic: LARGE opaque magenta motes,
                    // green/gold shaft outlines. If not visible, sorting,
                    // material or screen geometry is broken.
                    if (shape < 7.5)
                    {
                        float alpha=1.0-smoothstep(.78,.86,radius);
                        return fixed4(1.0,0.0,.85,alpha);
                    }
                    float border=exp(-pow((radius-.70)/.09,2.0));
                    float alpha=saturate(.38*profile+.62*border);
                    return fixed4(.95,.83,.06,alpha);
                }

                // Photo-lit dust: CPU drives vertex alpha ONCE using
                // the existing room and hallway switches.
                float vertexAlpha=saturate(i.color.a);
                float outAlpha;
                float3 rgb;
                if (shape>7.5)
                {
                    outAlpha=saturate(vertexAlpha*profile*.94);
                    // Shaft RGB * alpha exactly once. Shafts are under
                    // particle quads, with no fullscreen wash.
                    rgb=i.color.rgb*outAlpha*.85;
                }
                else
                {
                    float softHalo=pow(saturate(profile),.86);
                    float tinyCore=saturate(hotCore);
                    float visibleProfile=saturate(
                        softHalo*.90+tinyCore*.22*edge);
                    // Alpha can never exceed cell bounds; avoid a global
                    // minimum alpha outside the silhouette.
                    outAlpha=saturate(vertexAlpha*visibleProfile);
                    // Core + Halo: visible real micro-glint as well as
                    // wide low-energy scatter. Avoid multiplying
                    // lightResponse twice and avoid fake cyan overlay.
                    float lightBoost= _DustMode>.5 ? 1.40 : 1.0;
                    float specular=tinyCore*edge*
                        (.20*vertexAlpha)*lightBoost;
                    rgb=i.color.rgb*(outAlpha*1.02+specular);
                }
                // Straight alpha is wrong with Blend One; output is
                // premultiplied RGB, transparent outside each guarded cell.
                return fixed4(min(rgb,2.0),outAlpha);
            }
            ENDCG
        }
    }
    Fallback Off
}