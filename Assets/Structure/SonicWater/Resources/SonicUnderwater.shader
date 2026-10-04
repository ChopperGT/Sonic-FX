Shader "Hidden/Sonic FX/Underwater"
{
    Properties { _MainTex ("Image", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _Tint;
            float _TintStrength, _Distortion, _Clock;
            float4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float edge = smoothstep(0, 0.05, min(min(uv.x,1-uv.x), min(uv.y,1-uv.y)));
                uv += float2(sin(uv.y * 19 - _Clock * 2.1), cos(uv.x * 15 + _Clock * 1.5) * 0.45) * _Distortion * edge;
                float2 halfTexel = abs(_MainTex_TexelSize.xy) * 0.5;
                uv = clamp(uv, halfTexel, 1 - halfTexel);
                float4 color = tex2D(_MainTex, uv);
                float3 tinted = color.rgb * _Tint.rgb + _Tint.rgb * 0.12;
                color.rgb = lerp(color.rgb, tinted, saturate(_TintStrength));
                return color;
            }
            ENDCG
        }
    }
    Fallback Off
}
