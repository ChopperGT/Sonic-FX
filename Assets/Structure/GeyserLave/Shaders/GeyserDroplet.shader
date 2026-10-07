Shader "Sonic FX/Geyser Goutte"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct a{float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct v{float4 position:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            v vert(a i){v o;o.position=UnityObjectToClipPos(i.vertex);o.uv=i.uv;o.color=i.color;return o;}
            fixed4 frag(v i):SV_Target
            {
                float r=length((i.uv-.5)*2);float alpha=1-smoothstep(.65,1,r);
                float3 c=lerp(float3(1,.07,.003),float3(2,1.4,.15),pow(saturate(1-r),.8));
                return float4(c*i.color.rgb,alpha*i.color.a);
            }
            ENDCG
        }
    }
}
