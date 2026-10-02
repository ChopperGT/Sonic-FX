Shader "Sonic FX/Monitor/Character Screen"
{
    Properties
    {
        _IconTex ("Icone du personnage",2D)="white"{}
        _IconUVRect ("Zone du sprite",Vector)=(0,0,1,1)
        _IconAspect ("Proportions de l'icone",Float)=1
        _BackgroundColor ("Fond de l'ecran",Color)=(0.025,0.04,0.065,1)
    }
    SubShader
    {
        Tags {"RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _IconTex;
            float4 _IconUVRect;
            float _IconAspect;
            fixed4 _BackgroundColor;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                // The original monitor screen UVs are rotated by a quarter turn.
                float2 uv=(float2(1-i.uv.y,i.uv.x)-.5)/.94;
                float aspect=max(.01,_IconAspect);
                if(aspect>1)uv.y*=aspect;else uv.x/=aspect;
                uv+=.5;
                if(any(uv<0)||any(uv>1))return _BackgroundColor;
                fixed4 icon=tex2D(_IconTex,_IconUVRect.xy+uv*_IconUVRect.zw);
                return fixed4(lerp(_BackgroundColor.rgb,icon.rgb,icon.a),1);
            }
            ENDCG
        }
    }
    Fallback "Unlit/Texture"
}
