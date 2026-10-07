Shader "Sonic FX/Geyser Jet Lave"
{
    Properties { _Strength ("Intensite",Range(0,1))=1 }
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
            struct a{float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct v{float4 position:SV_POSITION;float2 uv:TEXCOORD0;};float _Strength;
            v vert(a i)
            {
                v o;float t=_Time.y;
                i.vertex.x+=sin(i.uv.y*24-t*8)*.08*i.uv.y;
                i.vertex.z+=cos(i.uv.y*18-t*6)*.08*i.uv.y;
                o.position=UnityObjectToClipPos(i.vertex);o.uv=i.uv;return o;
            }
            fixed4 frag(v i):SV_Target
            {
                float y=i.uv.y;float flow=y*25-_Time.y*11;
                float veins=sin(flow+sin(i.uv.x*29+flow*.6)*1.8)*.5+.5;
                float heat=saturate(pow(veins,3)+.2*(1-y));
                float3 color=lerp(float3(.55,.035,.004),float3(2,1.25,.08),heat);
                float edge=smoothstep(0,.08,i.uv.x)*smoothstep(0,.08,1-i.uv.x);
                return float4(color,edge*(1-smoothstep(.7,1,y))*_Strength*.95);
            }
            ENDCG
        }
    }
}
