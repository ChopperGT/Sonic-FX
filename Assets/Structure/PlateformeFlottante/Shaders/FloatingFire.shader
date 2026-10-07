Shader "Sonic FX/Plateforme feu progressif"
{
    Properties { _Coverage("Propagation",Range(0,1))=0 _Strength("Feu",Range(0,1))=0 _Ignition("Depart",Vector)=(0,0,0,0) _MinSize("Bornes",Vector)=(-4,-3,8,6) }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Offset -1,-1
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION;float3 normal:NORMAL; };
            struct v2f { float4 vertex:SV_POSITION;float3 local:TEXCOORD0;float normalY:TEXCOORD1; };
            float _Coverage,_Strength;float4 _Ignition,_MinSize;
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex+float4(v.normal*.012,0));o.local=v.vertex.xyz;o.normalY=v.normal.y;return o;}
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            fixed4 frag(v2f i):SV_Target
            {
                clip(i.normalY-.35);clip(_Strength-.001);clip(_Coverage-.001);
                float2 p=(i.local.xz-_MinSize.xy)/_MinSize.zw;
                float farthest=max(max(distance(_Ignition.xy,float2(0,0)),distance(_Ignition.xy,float2(1,0))),max(distance(_Ignition.xy,float2(0,1)),distance(_Ignition.xy,float2(1,1))));
                float d=distance(p,_Ignition.xy)/max(.01,farthest);clip(_Coverage-d+.004);
                float flicker=.5+.5*sin(i.local.x*13+i.local.z*17+_Time.y*11+hash(floor(i.local.xz*5))*7);
                float3 color=lerp(float3(.15,.035,.008),float3(1,.18,.008),pow(flicker,2));color+=float3(1,.68,.08)*pow(flicker,12)*1.2;
                return float4(color,_Strength*.9);
            }
            ENDCG
        }
    }
}
