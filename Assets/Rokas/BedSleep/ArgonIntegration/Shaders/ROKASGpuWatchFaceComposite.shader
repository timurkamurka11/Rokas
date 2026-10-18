Shader "ROKAS/BedSleep/GpuWatchFaceComposite"
{
    Properties
    {
        _MainTex ("Authored 1024 watch screen", 2D) = "black" {}
        _LiveText ("Dynamic time overlay", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        ZTest Always Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            sampler2D _LiveText;
            fixed4 frag(v2f_img i) : SV_Target
            {
                // All five authored OLED faces are GPU textures.
                // Live numerals are blended in the shader, no 1M-pixel CPU
                // background regeneration, ReadPixels, or per-frame Apply.
                fixed4 art = tex2D(_MainTex, i.uv);
                fixed4 live = tex2D(_LiveText, i.uv);
                return fixed4(lerp(art.rgb, live.rgb, live.a), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
