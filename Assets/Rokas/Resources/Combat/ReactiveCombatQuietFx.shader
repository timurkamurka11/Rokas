Shader "Rokas/ReactiveCombat/QuietFx"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Opacity ("Visibility", Range(0,1)) = 1
        _SoftShape ("Soft Shape", Float) = 0
        _PortalCore ("Portal Interior", Float) = 0
        _Phase ("Presentation Phase", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        // The actor camera renders to a transparent texture used by the HUD.
        // Keep coverage alpha intact instead of squaring it on each FX pass.
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            fixed4 _Color;
            float _Opacity, _SoftShape, _PortalCore, _Phase;
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = i.color;
                color.a *= _Opacity;
                if (_SoftShape > .5 && _SoftShape < 1.5)
                {
                    float2 p = i.uv - .5;
                    color.a *= pow(saturate(1.0 - dot(p,p) * 4.0), 1.5);
                }
                else if (_SoftShape > 1.5)
                {
                    if (_SoftShape < 2.5)
                        color.a *= saturate(1.0 - abs(i.uv.y - .5) * 2.0);
                    else
                    {
                        float2 p = (i.uv - .5) * 2.0;
                        float wisps = .72 + .28 * sin(p.x * 12.0 + sin(p.y * 9.0 + _Phase) * 2.0);
                        color.a *= pow(saturate(1.0-length(p)),1.3) * wisps;
                    }
                }
                if (_PortalCore > .5)
                {
                    float2 p = (i.uv - .5) * 2.0;
                    float radius = length(p);
                    float swirl = .85 + .15 * sin(atan2(p.y,p.x) * 3.0 + radius * 10.0 - _Phase);
                    color.rgb *= swirl;
                    color.a *= 1.0 - smoothstep(.82, 1.0, radius);
                }
                clip(color.a - .001);
                return color;
            }
            ENDCG
        }
    }
}
