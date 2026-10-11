Shader "ROKAS/UI/LaptopCinematicLightStates"
{
    Properties
    {
        [PerRendererData] _MainTex ("Original approved laptop POV", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _RoomLightOn ("Main room light", Range(0,1)) = 0
        _HallwayLightOn ("Hallway light", Range(0,1)) = 0
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
            fixed4 _Color;
            float _RoomLightOn;
            float _HallwayLightOn;

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

            // Coordinates are locked to the ORIGINAL unmodified 1672x941
            // LaptopPOV_screen_off_APPROVED_CINEMATIC_DOF photograph.
            // Polygon follows the blue corridor visible behind the left
            // jamb, BLACK CURTAIN and sloping bottom threshold. The opening
            // reaches the photo's top edge: an older y=75 cutoff would leave
            // a strip with the WRONG room illumination over its lintel.
            float Cross2(float2 a, float2 b) { return a.x*b.y - a.y*b.x; }
            float InsideEdge(float2 a,float2 b,float2 p)
            {
                float2 edge = b-a;
                return Cross2(edge,p-a) / max(length(edge),.0001);
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
                return smoothstep(-2.5,2.5,signedDistance);
            }

            float SoftEllipse(float2 p,float2 center,float2 reach)
            {
                float2 q=(p-center)/reach;
                float r=dot(q,q);
                return 1.0-smoothstep(.12,1.0,r);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 original = tex2D(_MainTex,i.uv);
                float roomOn=saturate(_RoomLightOn);
                float hallwayOn=saturate(_HallwayLightOn);
                // At both OFF the original night photograph is reproduced
                // exactly (other than the standard UI vertex tint).
                if (roomOn <= .0001 && hallwayOn <= .0001)
                    return original * i.color;

                float2 p=float2(i.uv.x*1672.0,(1.0-i.uv.y)*941.0);
                float doorway=CorridorDoorMask(p);
                float inRoom=1.0-doorway;

                // A restrained warm photographic lift on existing details:
                // preserves shelves, keyboard, walls and their image pixels.
                float roomPractical =
                    .24*SoftEllipse(p,float2(520,265),float2(340,370)) +
                    .19*SoftEllipse(p,float2(1070,302),float2(310,410)) +
                    .16*SoftEllipse(p,float2(1370,205),float2(320,390));
                float warmRoom=roomOn*inRoom;
                float3 roomColor=original.rgb*(1.0+warmRoom*(.18+roomPractical));
                roomColor += warmRoom*float3(.075,.043,.016)*(.85+roomPractical);

                // Corridor is a DIFFERENT electrical zone. Even if the main
                // lamp is ON the interior of this left doorway stays tied to
                // hallwayOn, with source photo's cool ambient still present.
                float3 hallColor=original.rgb*(1.0+hallwayOn*.54);
                hallColor += hallwayOn*float3(.12,.071,.024);
                float3 composed=lerp(roomColor,hallColor,doorway);

                // Light carried across the threshold softly touches the
                // inside jamb and nearest floor; it fades within the room.
                // Suppressed in the full night-off configuration.
                float spill=hallwayOn*(1.0-doorway);
                spill*= .40*SoftEllipse(p,float2(384,456),float2(200,155)) +
                        .19*SoftEllipse(p,float2(399,540),float2(275,134));
                composed+=spill*float3(.12,.071,.034);

                return fixed4(saturate(composed), original.a) * i.color;
            }
            ENDCG
        }
    }
    Fallback Off
}
