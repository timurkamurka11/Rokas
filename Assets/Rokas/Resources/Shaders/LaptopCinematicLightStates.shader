Shader "ROKAS/UI/LaptopCinematicLightStates"
{
    Properties
    {
        [PerRendererData] _MainTex ("Room and hallway ON reference", 2D) = "white" {}
        [NoScaleOffset] _OffTex ("Room and hallway OFF reference", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _RoomLightOn ("Main room light", Range(0,1)) = 0
        _HallwayLightOn ("Hallway light", Range(0,1)) = 0
        [HideInInspector] _OffPhotoReady ("OFF reference installed", Float) = 0
        _MaskDebug ("Show zone mask (diagnostic only)", Range(0,1)) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True"
               "RenderType"="Transparent" "PreviewType"="Plane" }
        Stencil
        {
            Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp]
            ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]
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
            sampler2D _OffTex;
            fixed4 _Color;
            float _RoomLightOn;
            float _HallwayLightOn;
            float _OffPhotoReady;
            float _MaskDebug;

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
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uv = v.uv;
                return o;
            }

            // TOP-LEFT coordinates of the unmodified 1672x941 approved POV.
            // The six-sided polygon covers the visible left corridor, black
            // curtain and sloped threshold; room surfaces remain outside it.
            float Cross2(float2 a, float2 b) { return a.x*b.y - a.y*b.x; }
            float InsideEdge(float2 a,float2 b,float2 p)
            {
                float2 edge=b-a;
                return Cross2(edge,p-a)/max(length(edge),.0001);
            }
            float CorridorDoorMask(float2 p)
            {
                float2 a=float2(294,-5);
                float2 b=float2(382,-5);
                float2 c=float2(388,467);
                float2 d=float2(385,508);
                float2 e=float2(323,511);
                float2 f=float2(298,484);
                float signedDistance=min(
                    min(min(InsideEdge(a,b,p),InsideEdge(b,c,p)),
                        min(InsideEdge(c,d,p),InsideEdge(d,e,p))),
                    min(InsideEdge(e,f,p),InsideEdge(f,a,p)));
                return smoothstep(-3.5,3.5,signedDistance);
            }
            float SoftEllipse(float2 p,float2 center,float2 reach)
            {
                float2 q=(p-center)/reach;
                return 1.0-smoothstep(.10,1.0,dot(q,q));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 onPhoto=tex2D(_MainTex,i.uv);
                float2 p=float2(i.uv.x*1672.0,(1.0-i.uv.y)*941.0);
                float doorway=CorridorDoorMask(p);
                float spillZone=(1.0-doorway)*
                    saturate(.47*SoftEllipse(p,float2(384,456),float2(190,155))+
                             .25*SoftEllipse(p,float2(400,535),float2(250,122)));

                if (_MaskDebug > .5)
                    return fixed4(lerp(float3(.07,.17,.72),
                         float3(.12,.85,.22),doorway)+float3(spillZone,.0,.0),1)*i.color;

                fixed4 offPhoto=tex2D(_OffTex,i.uv);
                if (_OffPhotoReady < .5)
                {
                    // Diagnostic fallback only: do not silently claim that an
                    // ON photo can reproduce true OFF practical lights.
                    // The photographic OFF reference must be installed.
                    offPhoto.rgb = saturate(onPhoto.rgb*float3(.26,.34,.47));
                    offPhoto.a=onPhoto.a;
                }

                float roomOn=saturate(_RoomLightOn);
                float hallOn=saturate(_HallwayLightOn);
                float lightMix=lerp(roomOn,hallOn,doorway);
                float3 composed=lerp(offPhoto.rgb,onPhoto.rgb,lightMix);

                // The real corridor ON photo contributes its own captured
                // illumination to the jamb and nearby floor, but ONLY when
                // the main room is OFF and the corridor switch is ON.
                float spill=hallOn*(1.0-roomOn)*spillZone;
                composed+=max(onPhoto.rgb-offPhoto.rgb,0)*spill;

                // Both OFF is the exact OFF reference. Both ON is the exact
                // original approved ON photo. No geometry, laptop LCD or
                // UI overlays are modified by this shader.
                return fixed4(saturate(composed),onPhoto.a)*i.color;
            }
            ENDCG
        }
    }
    Fallback Off
}
