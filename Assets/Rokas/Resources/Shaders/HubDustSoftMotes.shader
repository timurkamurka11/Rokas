Shader "ROKAS/UI/HubDustSoftMotes"
{
    Properties
    {
        _MainTex ("UI Texture", 2D) = "white" {}
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                // Atlas-independent procedural implementation of the
                // supplied three classes of organic dust: micro specks,
                // soft fibers and rare defocused near-field bokeh.
                float kind = floor(i.uv.x / 2.0 + .001);
                float2 uv = float2(i.uv.x-kind*2.0, i.uv.y)*2.0-1.0;
                float r = length(uv);
                float a = exp(-6.0*r*r);
                if (kind > .5 && kind < 1.5)
                {
                    float fiber = length(float2(uv.x*.60, uv.y*1.5));
                    a = exp(-7.0*fiber*fiber) * .74;
                }
                else if (kind > 1.5)
                {
                    float disc = exp(-2.1*r*r);
                    float rim = exp(-pow((r-.52)/.19,2.0))*.25;
                    a = (disc*.63+rim)*.52;
                }
                a *= 1.0-smoothstep(.76,.98,r);
                return fixed4(i.color.rgb, saturate(i.color.a*a));
            }
            ENDCG
        }
    }
    Fallback Off
}
