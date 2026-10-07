Shader "Sonic FX/Braises"
{
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Blend SrcAlpha One Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct input {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct output {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            output vert(input i){output o;o.pos=UnityObjectToClipPos(i.vertex);o.uv=i.uv;o.color=i.color;return o;}
            fixed4 frag(output i):SV_Target {float r=length((i.uv-.5)*2);return float4(i.color.rgb*1.8,pow(saturate(1-r),1.5)*i.color.a);}
            ENDCG
        }
    }
}
