Shader "Rokas/Reactive Combat EmberGen Flipbook"
{
    Properties
    {
        _MainTex ("EmberGen premultiplied RGBA atlas", 2D) = "black" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _Opacity ("Opacity", Range(0,1)) = 1
        _Frame ("Frame (8 by 8)", Range(0,63)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+12" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize, _Tint;
            float _Opacity, _Frame;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o;
            }
            float4 sampleFrame(float2 uv, float frame)
            {
                float2 cell = float2(fmod(frame, 8), 7 - floor(frame / 8));
                float2 inset = _MainTex_TexelSize.xy * 4;
                return tex2D(_MainTex, (cell + clamp(uv, inset, 1-inset)) / 8);
            }
            float4 frag(v2f i) : SV_Target
            {
                float frame = clamp(_Frame, 0, 63);
                float4 color = lerp(sampleFrame(i.uv, floor(frame)),
                    sampleFrame(i.uv, min(63, floor(frame)+1)), frac(frame));
                // Fade inside each cell and clamp to its own texels: no neighbouring
                // frame bleed, carrier rectangle or premultiplication fringe.
                float edge = smoothstep(0, .035, min(min(i.uv.x, 1-i.uv.x), min(i.uv.y, 1-i.uv.y)));
                float opacity = _Opacity * _Tint.a * edge;
                clip(color.a * opacity - .0001);
                return float4(color.rgb * _Tint.rgb * opacity, color.a * opacity);
            }
            ENDCG
        }
    }
}
