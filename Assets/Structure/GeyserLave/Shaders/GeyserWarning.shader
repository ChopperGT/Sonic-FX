Shader "Sonic FX/Geyser Impact"
{
    Properties { _Progress ("Approche de l'impact",Range(0,1))=0 }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off Offset -1,-1
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct a { float4 vertex:POSITION;float2 uv:TEXCOORD0; };
            struct v { float4 position:SV_POSITION;float2 uv:TEXCOORD0; };
            float _Progress;
            v vert(a i){v o;o.position=UnityObjectToClipPos(i.vertex);o.uv=i.uv;return o;}
            fixed4 frag(v i):SV_Target
            {
                float2 p=(i.uv-.5)*2;float r=length(p);clip(1-r);
                float ring=smoothstep(.78,.82,r)*(1-smoothstep(.94,.99,r));
                float approaching=(1-smoothstep(.02,.05,abs(r-(.9-.65*_Progress))));
                float cross=(1-smoothstep(.035,.055,min(abs(p.x),abs(p.y))))*(1-smoothstep(.25,.5,r));
                float pulse=.8+.2*sin(_Time.y*(7+_Progress*10));
                return float4(1,.025,.012,saturate((.13+.7*ring+.35*approaching+.4*cross)*pulse));
            }
            ENDCG
        }
    }
}
