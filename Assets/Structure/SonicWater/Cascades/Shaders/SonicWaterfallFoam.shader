Shader "Sonic FX/Ecume Cascade"
{
    Properties
    {
        _FoamColor ("Ecume", Color) = (.8,.98,1,1)
        _Intensity ("Intensite", Float) = 1
        _WaterSize ("Bassin", Vector) = (10000,10000,0,0)
    }
    SubShader
    {
        Tags {"Queue"="Transparent+10" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            float4 _FoamColor,_WaterSize;
            float _Intensity;
            float4x4 _WaterInverse;
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;};
            v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                float3 water=mul(_WaterInverse,float4(i.world,1)).xyz;
                clip(_WaterSize.x*.5-abs(water.x));clip(_WaterSize.y*.5-abs(water.z));
                float2 q=i.uv*2-1;float t=_Time.y;
                float wobble=sin(q.x*21+t*4)*.07+cos(q.x*39-t*2)*.025;
                float radial=abs(q.y)+wobble;
                float waves=pow(saturate(.5+.5*sin(radial*27-t*5)),5);
                float core=1-smoothstep(.05,.55,radial);
                float end=1-smoothstep(.75,1,abs(q.x));
                float alpha=saturate((core*.65+waves*.5)*(1-smoothstep(.65,1,radial))*end*_Intensity);
                return float4(_FoamColor.rgb,alpha);
            }
            ENDCG
        }
    }
}
