Shader "ROKAS/UI/LaptopCinematicLightStates"
{
    Properties
    {
        [PerRendererData] _MainTex ("Room and hallway ON reference", 2D) = "white" {}
        [NoScaleOffset] _OffTex ("Room and hallway OFF reference", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _RoomLightOn ("Main room light", Range(0,1)) = 0
        _HallwayLightOn ("Hallway light", Range(0,1)) = 0
        _ScreenGlow ("Physical laptop screen emission", Range(0,1)) = 0
        [NoScaleOffset] _BounceMasksTex ("V5 RGBA: wood, keys, lip, metal", 2D) = "black" {}
        _ScreenBounceTint ("Actual YOMI bounce tint", Color) = (.2,.21,.22,1)
        _ScreenBounceAmount ("Actual YOMI bounce intensity", Range(0,1)) = 0
        [NoScaleOffset] _BloomMasksTex ("V6 RGBA: local halo/specular/rim/breakup", 2D) = "black" {}
        _ScreenBloomAmount ("YOMI highlight bloom strength", Range(0,1)) = 0
        _ScreenSpecAmount ("YOMI highlight specular strength", Range(0,1)) = 0
        _ScreenBounceSide ("Actual YOMI left/right luminance bias", Range(-.3,.3)) = 0
        _BloomThreshold ("YOMI bloom threshold (CPU)", Range(0,1)) = .58
        _BloomSoftKnee ("YOMI bloom soft knee (CPU)", Range(0,1)) = .22
        _BloomGain ("Localized bloom gain", Range(0,2)) = 1
        _SpecularGain ("Wood specular gain", Range(0,2)) = 1
        [HideInInspector] _OffPhotoReady ("OFF reference installed", Float) = 0
        _MaskDebug ("Mask debug: 0 off, 1 zones, 2 optical", Range(0,2)) = 0
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
            float _ScreenGlow;
            sampler2D _BounceMasksTex;
            float4 _ScreenBounceTint;
            float _ScreenBounceAmount;
            sampler2D _BloomMasksTex;
            float _ScreenBloomAmount;
            float _ScreenSpecAmount;
            float _ScreenBounceSide;
            float _BloomGain;
            float _SpecularGain;
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

            // Top-left pixel coordinates for both registered 1672x941 photos.
            // Hallway mask excludes furniture in front of the left opening,
            // follows the timber right jamb, and ends ON the raised sill.
            // The earlier (323,511)-(385,508) bottom leaked into room floor.
            float Cross2(float2 a, float2 b) { return a.x*b.y-a.y*b.x; }
            float SignedEdge(float2 a,float2 b,float2 p)
            {
                float2 edge=b-a;
                return Cross2(edge,p-a)/max(length(edge),.0001);
            }
            float DoorwayPolygon(float2 p)
            {
                // The cabinet interrupts the left jamb at y≈296. A single
                // convex half-plane test was wrong for this CONCAVE outline;
                // represent it as a union of TWO convex aligned masks instead.
                const float2 upperA=float2(287,-6);
                const float2 upperB=float2(383,-6);
                const float2 upperC=float2(393,296);
                const float2 upperD=float2(287,296);
                float upper=min(
                    min(SignedEdge(upperA,upperB,p),SignedEdge(upperB,upperC,p)),
                    min(SignedEdge(upperC,upperD,p),SignedEdge(upperD,upperA,p)));

                const float2 lowerA=float2(317,296);
                const float2 lowerB=float2(393,296);
                const float2 lowerC=float2(400,469);
                const float2 lowerD=float2(320,489);
                float lower=min(
                    min(SignedEdge(lowerA,lowerB,p),SignedEdge(lowerB,lowerC,p)),
                    min(SignedEdge(lowerC,lowerD,p),SignedEdge(lowerD,lowerA,p)));

                return max(smoothstep(-2.6,2.6,upper),
                           smoothstep(-2.6,2.6,lower));
            }
            float SoftEllipse(float2 p,float2 center,float2 reach)
            {
                float2 q=(p-center)/reach;
                return 1.0-smoothstep(.10,1.0,dot(q,q));
            }
            float ThresholdSpill(float2 p,float door)
            {
                // Small photographic spill on the jamb and FIRST interior floorboards,
                // not a second oversized flashlight covering the room floor.
                float jamb=.34*SoftEllipse(p,float2(399,426),float2(49,86));
                float sill=.22*SoftEllipse(p,float2(401,476),float2(93,36));
                return saturate((jamb+sill)*(1.0-door));
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 onPhoto=tex2D(_MainTex,i.uv);
                fixed4 offPhoto=tex2D(_OffTex,i.uv);
                float2 p=float2(i.uv.x*1672.0,(1.0-i.uv.y)*941.0);
                float hallwayMask=DoorwayPolygon(p);
                float roomMask=1.0-hallwayMask;
                float corridorSpill=ThresholdSpill(p,hallwayMask);
                // Packed material-specific light passes are generated from
                // the exact 1672x941 POV geometry (no generic radial glow).
                float4 mask=tex2D(_BounceMasksTex,i.uv);
                float woodMask=mask.r*(1.0-mask.a);
                float keyMask=mask.g;
                float lipMask=mask.b*(1.0-mask.a);
                float4 bloomMasks=tex2D(_BloomMasksTex,i.uv);
                float noiseBreakup=lerp(.75,1.12,bloomMasks.a);

                if (_MaskDebug>1.5)
                {
                    // V6 optical layers: RED halo, GREEN specular, BLUE rim.
                    return fixed4(bloomMasks.rgb,1)*i.color;
                }
                if (_MaskDebug>.5)
                {
                    // Original photographic zone diagnostics are preserved.
                    float3 zone=lerp(float3(.08,.15,.58),float3(.12,.83,.18),hallwayMask);
                    zone+=float3(corridorSpill,.0,.0)+
                        float3(0,.22,.36)*saturate(woodMask+lipMask+keyMask);
                    return fixed4(saturate(zone),1)*i.color;
                }

                // The source photo PAIR is mandatory. No brightness/grading
                // substitute for the photographed OFF room.
                float roomOn=saturate(_RoomLightOn);
                float hallwayOn=saturate(_HallwayLightOn);
                float mixOn=saturate(roomOn*roomMask+hallwayOn*hallwayMask);
                float3 resultColor=lerp(offPhoto.rgb,onPhoto.rgb,mixOn);

                // Only corridor ON + room OFF produces a localized warm spill.
                float practicalSpill=hallwayOn*(1.0-roomOn)*corridorSpill;
                resultColor+=max(onPhoto.rgb-offPhoto.rgb,0)*practicalSpill;

                // V5 Hybrid Cinematic Lighting: measured YOMI screen color
                // modulates distinct light passes painted for the wood,
                // thin tabletop lip and a *tiny* keyboard response.
                // This is reflected illumination, not an emissive UI overlay.
                // Zero bounce when the room light is fully ON or LCD is OFF.
                float night=saturate(1.0-roomOn);
                night=night*night*night;
                float strength=saturate(_ScreenGlow)*
                    saturate(_ScreenBounceAmount)*night;
                float grainResponse=saturate(.68+
                    .58*dot(resultColor,float3(.2126,.7152,.0722)));
                // V6 diffuse: restrained, wide reflected LCD emission.
                // Wood grain remains photographic, never globally graded.
                // Actual YOMI left/right bright regions steer the specular
                // reflection across the tabletop, never the laptop housing.
                float lateral=saturate((p.x-490.0)/690.0);
                float sideWeight=1.0+_ScreenBounceSide*(lateral*2.0-1.0);
                float diffusePass=(woodMask*2.22+lipMask*.83)*sideWeight;
                float keyboardPass=keyMask*.12;
                // Richer cool illumination, sourced from the real sampled
                // LCD frame. Keep wood grain and surface-level masking;
                // no flat translucent blue rectangle on top of the photo.
                float3 screenHue=saturate(_ScreenBounceTint.rgb*
                    float3(.91,1.12,1.29));
                float3 diffuseLight=screenHue*
                    (diffusePass*grainResponse+keyboardPass)*strength;

                // A second, narrower response reacts ONLY to actual bright
                // YOMI pixels. The premade radial blur already acts as a
                // localized bloom kernel (no camera post-processing or UI
                // blur of the original photograph).
                float bloomPower=saturate(_ScreenBloomAmount)*_BloomGain;
                float specPower=saturate(_ScreenSpecAmount)*_SpecularGain;
                float halo=bloomMasks.r*noiseBreakup*bloomPower;
                float spec=(bloomMasks.g*.92+bloomMasks.b*.46)*
                    noiseBreakup*specPower*sideWeight;
                float3 opticalLight=screenHue*night*
                    saturate(_ScreenGlow)*(halo*2.18+spec*2.52);
                // V8 cyan/blue desk response: dynamic YOMI, masked surfaces,
                // filmic shoulder and hard power/room-light safety gates.
                float3 bounced=diffuseLight+opticalLight;
                bounced=.56*(1.0-exp(-bounced/.56));
                resultColor+=bounced;

                return fixed4(saturate(resultColor),onPhoto.a)*i.color;
            }
            ENDCG
        }
    }
    Fallback Off
}
