Shader "ROKAS/UI/HallwayLightStates"
{
    Properties
    {
        [PerRendererData] _MainTex ("Hallway ON photo", 2D) = "white" {}
        _OffTex ("Hallway OFF photo", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _MainRoomOn ("Main Room practicals", Range(0,1)) = 1
        _HallwayOn ("Hallway practicals", Range(0,1)) = 1
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp]
            ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
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
            float _MainRoomOn;
            float _HallwayOn;
            struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uv = v.uv;
                return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                // Photo pair is independently aligned to the same 1920x1080 architecture.
                // Select the MainRoom image INSIDE the physical doorway only, not with a
                // circular/radial artificial spotlight. Coordinates are top-left authored.
                float2 p = float2(i.uv.x * 1920.0, (1.0-i.uv.y)*1080.0);
                // Pixel-aligned inner jambs of the photographed LEFT doorway.
                // The visible wood threshold slopes from (320,985) to (692,915).
                // Previous horizontal y=940..958 cutoff stranded a warm strip in
                // the MainRoom floor. Follow the threshold, not a viewport rectangle.
                float low = saturate((p.y-600.0)/385.0);
                float left = lerp(290.0,320.0,low);
                float right = lerp(695.0,692.0,low);
                float threshold = lerp(985.0,915.0,saturate((p.x-320.0)/372.0));
                float innerJamb = smoothstep(left-2.0,left+2.0,p.x) *
                                  (1.0-smoothstep(right-2.0,right+2.0,p.x));
                float sill = 1.0-smoothstep(threshold-2.0,threshold+2.0,p.y);
                float mainRoomOpening = saturate(innerJamb*sill);
                float illumination = lerp(saturate(_HallwayOn),saturate(_MainRoomOn),mainRoomOpening);
                fixed4 onPhoto = tex2D(_MainTex,i.uv);
                fixed4 offPhoto = tex2D(_OffTex,i.uv);
                return lerp(offPhoto,onPhoto,illumination)*i.color;
            }
            ENDCG
        }
    }
}
