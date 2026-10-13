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
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            // Faithfully adapted from the user's supplied ROKAS 4x2 dust
            // atlas generator. Built-in ScreenSpaceOverlay cannot sample
            // depth-buffer soft particles; we instead render eight organic
            // profiles at atlas-equivalent UVs, no 3D prefab / asset dependency.
            float ShapeAlpha(float shape, float2 uv)
            {
                float2 p = uv * 2.0 - 1.0;
                float r = length(p);
                float theta = atan2(p.y,p.x);
                float a;
                if (shape < .5)
                {
                    a = exp(-6.5*r*r); // Cell 0: fine soft speck
                }
                else if (shape < 1.5)
                {
                    float core = saturate(1.0-pow(r/.68,4.0));
                    float rim = exp(-pow((r-.56)/.075,2.0)*.5)*.45;
                    a = saturate((core*.65+rim)*(.88+.12*cos(theta-.6)));
                }
                else if (shape < 2.5)
                {
                    float rr = r*(1.0+.16*sin(3.0*theta+.5)+.09*cos(5.0*theta-1.2));
                    a = exp(-5.2*rr*rr);
                }
                else if (shape < 3.5)
                {
                    float2 v = float2(p.x*.90-p.y*.435, p.x*.435+p.y*.90);
                    float curve = v.y-.28*v.x*v.x+.04;
                    float point = clamp(v.x,-.38,.38);
                    float dist = length(float2(v.x-point,curve*1.79));
                    float ends = saturate(1.0-pow(v.x/.44,2.0));
                    a = exp(-28.0*dist*dist)*(.4+.6*ends);
                }
                else if (shape < 4.5)
                {
                    float rr = length(float2((p.x-.06)*1.15,(p.y+.04)*.9));
                    a = .75*exp(-7.0*rr*rr)+.25*exp(-2.8*r*r);
                }
                else if (shape < 5.5)
                {
                    float rr=r*(1.0+.04*cos(6.0*theta));
                    float disc=1.0/(1.0+exp((rr-.58)*18.0));
                    float rim=exp(-pow((rr-.55)/.10,2.0));
                    a=disc*.48+rim*.23;
                }
                else if (shape < 6.5)
                {
                    float2 q = float2(p.x*.88+p.y*.48,-p.x*.48+p.y*.88);
                    float drop = length(float2(q.x*1.4,q.y*.8));
                    float tail = exp(-45.0*(q.y-.30*q.x*q.x)*(q.y-.30*q.x*q.x)) *
                        exp(-3.0*pow(q.x+.15,2.0));
                    a=exp(-7.0*drop*drop)*.80+tail*.18;
                }
                else
                {
                    a = .78*exp(-7.4*r*r)+.21*exp(-2.4*r*r);
                }
                // The supplied texture cells have transparent pixel guards
                // to prevent hard alpha squares. Apply the same here.
                float edge = 1.0 - smoothstep(.78,.98,r);
                return saturate(a*edge);
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float shape = floor(i.uv.x*.5 + .001);
                float2 localUv = float2(i.uv.x-shape*2.0,i.uv.y);
                float alpha = ShapeAlpha(shape,localUv);
                // Mild luminous core, avoids an overbright additive snowstorm.
                float core = exp(-7.0*dot(localUv*2-1,localUv*2-1));
                float3 tinted = saturate(i.color.rgb*(.85+.15*core));
                return fixed4(tinted,saturate(i.color.a*alpha));
            }
            ENDCG
        }
    }
    Fallback Off
}