Shader "Sonic FX/Flammes animees"
{
    Properties
    {
        _MainTex("Animation de feu (4 x 4)",2D)="white"{}
        _FramesPerSecond("Images par seconde",Range(1,40))=16
        _Brightness("Luminosite",Range(.5,3))=1.25
        _PreviewTime("Preview Time",Float)=-1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            sampler2D _MainTex;float4 _MainTex_TexelSize;
            float _FramesPerSecond,_Brightness,_PreviewTime;
            v2f vert(appdata v) { v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o; }
            float4 frame(float2 uv,float index)
            {
                float2 cell=float2(fmod(index,4),3-floor(index/4));
                float2 padding=_MainTex_TexelSize.xy*1.5;
                float2 atlas=(cell+clamp(uv,padding*4,1-padding*4))*.25;
                float4 c=tex2D(_MainTex,atlas);
                return float4(c.rgb*c.a,c.a);
            }
            fixed4 frag(v2f i):SV_Target
            {
                float t=_PreviewTime>=0?_PreviewTime:_Time.y;
                float animation=t*_FramesPerSecond+i.color.r*16;
                float index=fmod(floor(animation),16);
                // Blend premultiplied frames to avoid dark fringes.
                float4 c=lerp(frame(i.uv,index),frame(i.uv,fmod(index+1,16)),frac(animation));
                c.rgb=c.rgb/max(c.a,.001);
                float edge=smoothstep(0,.035,i.uv.x)*smoothstep(0,.035,1-i.uv.x)*smoothstep(0,.025,i.uv.y)*smoothstep(0,.025,1-i.uv.y);
                return float4(c.rgb*_Brightness,c.a*i.color.a*edge);
            }
            ENDCG
        }
    }
}
