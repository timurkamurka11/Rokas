Shader "Rokas/ReactiveCombat/PortalDistortion"
{
    Properties
    {
        _MainTex("Authoritative arena illustration",2D)="black"{}
        _Phase("Clock",Float)=0 _Opening("Aperture size",Float)=0 _Opacity("Coverage",Float)=0 _Seed("Kernel phase",Float)=0
        _PortalContact("Local crossing response",Vector)=(0,0,0,.45)
    }
    SubShader
    {
        Tags {"Queue"="Transparent-2" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "ReactiveCombatPortalBoundary.cginc"
            sampler2D _MainTex;float _Phase,_Opening,_Opacity,_Seed;
            float4 _PortalContact;
            struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 screen:TEXCOORD1;};
            v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;o.screen=ComputeScreenPos(o.pos);return o;}
            fixed4 frag(v2f i):SV_Target
            {
                float2 local=(i.uv*2-1)*1.25-float2(0,-.612*_Seed);
                float r=length(local),a=atan2(local.y,local.x);
                float edge=portalAngularBoundary(a,_Phase)*_Opening;
                float band=exp(-abs(r-edge)*35);
                float seed=exp(-r*r*65)*_Seed;
                float2 aperture=local/max(.001,_Opening);
                float2 contactDelta=aperture-_PortalContact.xy;
                float contact=exp(-dot(contactDelta,contactDelta)/max(.025,_PortalContact.w*_PortalContact.w))*saturate(_PortalContact.z);
                float coverage=(band*.30+seed*.35+contact*.14)*_Opacity;
                clip(coverage-.002);
                // Actor RT occupies the top 906 pixels of the 1080-pixel stage.
                // Sample the same arena illustration; this avoids a GrabPass of
                // the transparent actor RT, which cannot contain the background.
                float2 uv=i.screen.xy/i.screen.w;
                uv.y=uv.y*(906.0/1080.0)+(174.0/1080.0);
                float n=portalFbm(local*9+_Phase*.55);
                uv+=float2(cos(a),sin(a))*(n-.5)*(.009*band+.006*seed);
                uv+=contactDelta*contact*.005;
                return fixed4(tex2D(_MainTex,uv).rgb,coverage);
            }
            ENDCG
        }
    }
}
