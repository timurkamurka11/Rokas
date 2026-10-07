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
                float left = lerp(292.0,303.0,saturate((p.y-810.0)/145.0));
                float right = lerp(722.0,730.0,saturate((p.y-810.0)/145.0));
                float entry = smoothstep(left-3.0,left+3.0,p.x);
                float exit = 1.0-smoothstep(right-3.0,right+3.0,p.x);
                float bottom = 1.0-smoothstep(940.0,958.0,p.y);
                float mainRoomOpening = saturate(entry*exit*bottom);
                float illumination = lerp(saturate(_HallwayOn),saturate(_MainRoomOn),mainRoomOpening);
                fixed4 onPhoto = tex2D(_MainTex,i.uv);
                fixed4 offPhoto = tex2D(_OffTex,i.uv);
                return lerp(offPhoto,onPhoto,illumination)*i.color;
            }
            ENDCG
        }
    }
}
