Shader "Hidden/ROKAS/ArgonBedVideoComposite"
{
    Properties {
      _MainTex ("Actual BedCeilingPOV", 2D) = "black" {}
      _OverlayTex ("Actual Rendered Argon Hand", 2D) = "black" {}
    }
    SubShader {
      Tags { "RenderType"="Opaque" }
      Pass {
        Cull Off ZWrite Off ZTest Always
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        sampler2D _OverlayTex;
        struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
        struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
        v2f vert(appdata v) {
          v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o;
        }
        fixed4 frag(v2f i) : SV_Target {
          fixed4 bed=tex2D(_MainTex,i.uv);
          fixed4 hand=tex2D(_OverlayTex,i.uv);
          return fixed4(lerp(bed.rgb,hand.rgb,saturate(hand.a)),1);
        }
        ENDCG
      }
    }
    Fallback Off
}
