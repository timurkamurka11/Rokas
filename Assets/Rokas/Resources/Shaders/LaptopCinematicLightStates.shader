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
                // V11: the alpha channel is the photographed aluminium
                // palm-rest and laptop chassis, previously unlit.
                float metalMask=mask.a*(1.0-hallwayMask);
                float4 bloomMasks=tex2D(_BloomMasksTex,i.uv);
                float noiseBreakup=lerp(.72,1.18,bloomMasks.a);

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

                // V11 - photographic light from the REAL YOMI screen.
                // The existing room/corridor and reference-image blend above
                // remains the single authoritative background.
                float night=saturate(1.0-roomOn);
                night=night*night*night;
                float screenGlow=saturate(_ScreenGlow);
                float bounceAmount=saturate(_ScreenBounceAmount);
                // A completely black LCD has CPU exposure .10 and emits no
                // bounce. Screen highlights are weighted by the estimator.
                float lcdEnergy=saturate((bounceAmount-.10)*3.25);
                float strength=night*screenGlow*lcdEnergy;

                // Preserve true YOMI chromaticity, with controlled exposure.
                // A desaturated warm frame does NOT become electric cyan.
                float3 rawTint=saturate(_ScreenBounceTint.rgb);
                float tintLuma=max(dot(rawTint,float3(.2126,.7152,.0722)),.001);
                float3 screenChroma=lerp(float3(1,1,1),
                    rawTint/tintLuma,.68);
                float3 sampledHue=saturate(lerp(rawTint,
                    screenChroma*sqrt(tintLuma),.42));

                // Full-resolution registered photographs encode the wood
                // fibres, dark crevices and glossy surface reflectance. Do
                // not replace them with a flat colour or procedural grain.
                float offLuma=dot(offPhoto.rgb,float3(.2126,.7152,.0722));
                float onLuma=dot(onPhoto.rgb,float3(.2126,.7152,.0722));
                float3 photoDelta=max(onPhoto.rgb-offPhoto.rgb,0.0);
                float3 onChroma=onPhoto.rgb/max(onLuma,.07);
                float3 woodAlbedo=clamp(
                    lerp(float3(1,1,1),onChroma,.48),
                    float3(.56,.49,.42),float3(1.23,1.16,1.05));
                // Floor of .38 keeps dark varnished boards visible, while
                // the photographic delta and grain modulate local contrast.
                float grainRelief=saturate(.38+.83*sqrt(saturate(onLuma*1.3))
                    +.24*offLuma);
                float shadowDetail=smoothstep(.006,.085,onLuma);
                float grainResponse=grainRelief*(.40+.60*shadowDetail);

                // Anisotropic emitter: x-span of the wide LCD expands
                // naturally across the desk. IMPORTANT: the falloff must
                // remain centred near photo y855 where the V5 WOOD mask is
                // nonzero (y842-910), not y738 on the keyboard.
                float forwardT=saturate((p.y-668.0)/273.0);
                float emitterHalfWidth=lerp(188.0,266.0,forwardT);
                float receiverX=max(abs(p.x-824.0)-emitterHalfWidth,0.0)
                    /335.0;
                float receiverY=(p.y-855.0)/210.0;
                float receiverDist=receiverX*receiverX+
                    receiverY*receiverY;
                float woodCone=(1.0/(1.0+.88*receiverDist))*
                    (1.0-smoothstep(.80,2.10,receiverDist));
                // A real falloff at y875-941 avoids the neon front-edge bar.
                float frontFade=1.0-.96*smoothstep(875.0,941.0,p.y);
                float woodFalloff=saturate(woodCone*frontFade);

                // Separate, subtler lobe for physical keyboard and chassis.
                float2 keyDelta=(p-float2(824.0,745.0))/
                    float2(440.0,205.0);
                float keyboardLobe=1.0-smoothstep(.42,1.65,
                    dot(keyDelta,keyDelta));
                float chassisWindow=smoothstep(665.0,689.0,p.y)*
                    (1.0-.60*smoothstep(793.0,889.0,p.y))*
                    (1.0-smoothstep(268.0,386.0,abs(p.x-824.0)));

                float lateral=saturate((p.x-490.0)/690.0);
                float sideWeight=saturate(1.0+
                    _ScreenBounceSide*(lateral*2.0-1.0));
                float lipFade=1.0-smoothstep(783.0,899.0,p.y);
                float woodZone=max(woodMask*1.12,lipMask*.13*lipFade);
                float3 learnedWood=photoDelta*screenChroma*1.32;
                float3 woodBounceTint=lerp(
                    sampledHue*woodAlbedo*grainResponse,
                    learnedWood,.26);
                float3 woodDiffuse=woodBounceTint*woodZone*
                    woodFalloff*sideWeight*1.46;

                // Light on mask.a makes the physical laptop body read as
                // adjacent to its powered LCD, without bleaching stickers.
                float chassisDetail=saturate(.30+.78*offLuma+.33*onLuma);
                float metalPass=metalMask*chassisDetail*
                    chassisWindow*keyboardLobe*.38;
                float keyRowFalloff=lerp(1.33,.50,
                    saturate((p.y-698.0)/119.0));
                float keyDetail=saturate(.34+1.03*offLuma+.44*onLuma);
                float keyPass=keyMask*keyDetail*
                    keyboardLobe*keyRowFalloff*.24;
                float3 diffuseLight=(woodDiffuse+
                    sampledHue*(metalPass+keyPass)*sideWeight)*strength;

                // Specular masks trace actual gloss on the photographed
                // wood. Energy is driven by bright YOMI icons/lantern.
                float bloomPower=saturate(_ScreenBloomAmount)*_BloomGain;
                float specPower=saturate(_ScreenSpecAmount)*_SpecularGain;
                float microGlint=smoothstep(.028,.22,onLuma)*
                    (.34+.66*smoothstep(.010,.075,offLuma));
                float surfaceReceiver=saturate(
                    woodMask+.50*metalMask+.33*keyMask+.12*lipMask*lipFade);
                float halo=bloomMasks.r*noiseBreakup*bloomPower*
                    surfaceReceiver*(.38+.62*grainRelief);
                float spec=(bloomMasks.g*.84+bloomMasks.b*.14*lipFade)*
                    noiseBreakup*specPower*sideWeight*
                    surfaceReceiver*microGlint;
                float3 specTint=sampledHue*
                    lerp(woodAlbedo,float3(1,1,1),.72);
                float localFalloff=max(woodFalloff,
                    keyboardLobe*.55*chassisWindow);
                float3 opticalLight=specTint*strength*localFalloff*
                    (halo*.57+spec*1.20);
                float3 bounced=diffuseLight+opticalLight;
                // Filmic high-end compression retains grain and texture.
                bounced=.45*(1.0-exp(-bounced/.45));
                resultColor+=bounced*(1.0-.24*resultColor);

                return fixed4(saturate(resultColor),onPhoto.a)*i.color;
            }
            ENDCG
        }
    }
    Fallback Off
}
