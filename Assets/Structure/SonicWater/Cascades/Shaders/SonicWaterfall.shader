Shader "Sonic FX/Cascade"
{
    Properties
    {
        _Color ("Eau", Color) = (.04,.6,.8,.88)
        _FoamColor ("Ecume", Color) = (.8,.98,1,1)
        _FlowSpeed ("Courant", Float) = 7
        _Dimensions ("Dimensions", Vector) = (8,15,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            float4 _Color,_FoamColor,_Dimensions;
            float _FlowSpeed;
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
            v2f vert(appdata_base v)
            {
                v2f o;
                float t=_Time.y*_FlowSpeed;
                v.vertex.z += sin(v.texcoord.x*31+t*1.5+v.texcoord.y*15)*.065*sin(v.texcoord.y*3.14159);
                o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy;return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 p=i.uv*_Dimensions.xy;
                // V grows downwards: translating p.y backwards makes the pattern fall.
                float t=_Time.y*_FlowSpeed;
                float bend=sin((p.y-t)*.55+p.x*.8)*.22;
                float lanes=pow(saturate(.5+.5*sin(p.x*8+bend+sin(p.x*2.3))),6);
                float streak=lanes*(.55+.45*sin((p.y-t)*2.2+p.x*6));
                float silk=.5+.5*sin(p.x*2.5+sin((p.y-t)*.7)*.6);
                float froth=smoothstep(.78,1,i.uv.y)*(.4+.6*silk);
                float lip=(1-smoothstep(0,.07,i.uv.y))*.55;
                float bright=saturate(streak*.8+froth*.7+lip);
                float3 col=lerp(_Color.rgb*(.78+.3*silk),_FoamColor.rgb,bright);
                float edge=smoothstep(0,.025,i.uv.x)*smoothstep(0,.025,1-i.uv.x);
                return float4(col,edge*saturate(_Color.a+bright*.2));
            }
            ENDCG
        }
    }
}
