Shader "Sonic FX/Lave"
{
    Properties
    {
        _CrustColor ("Croute refroidie", Color) = (.12,.025,.016,1)
        _LavaColor ("Lave", Color) = (1,.18,.015,1)
        _HotColor ("Fissures chaudes", Color) = (1,.85,.08,1)
        _PatternScale ("Taille du motif", Float) = .65
        _FlowSpeed ("Vitesse du courant", Float) = .3
        _Glow ("Incandescence", Range(0,6)) = 2.2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard
        #pragma target 3.0
        fixed4 _CrustColor, _LavaColor, _HotColor;
        float _PatternScale, _FlowSpeed, _Glow;
        struct Input { float3 worldPos; };
        float2 hash(float2 p)
        {
            return frac(sin(float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3))))*43758.5453);
        }
        float fissures(float2 p, float t)
        {
            float2 cell=floor(p), local=frac(p);
            float first=10, second=10;
            for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
            {
                float2 neighbour=float2(x,y);
                float2 random=hash(cell+neighbour);
                float2 cellPoint=.5+.35*sin(t+6.2831*random);
                float d=length(neighbour+cellPoint-local);
                if(d<first){second=first;first=d;}else if(d<second)second=d;
            }
            return 1-smoothstep(.04,.20,second-first);
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float t=_Time.y*_FlowSpeed;
            float2 p=IN.worldPos.xz*_PatternScale+float2(t*.2,-t*.12);
            float hot=fissures(p,t*.5);
            float pulse=.82+.18*sin(t*2+p.x*.8+p.y*.7);
            float molten=.5+.5*sin(p.x*1.8+sin(p.y*1.4+t)+t);
            float3 magma=lerp(_LavaColor.rgb,_HotColor.rgb,smoothstep(.5,1,hot));
            o.Albedo=lerp(_CrustColor.rgb*(.75+molten*.25),magma,hot*.9);
            o.Emission=magma*(hot*_Glow*pulse+.12*molten);
            o.Normal=normalize(float3(sin(p.x*2+t)*.12,cos(p.y*2-t)*.12,1));
            o.Metallic=0; o.Smoothness=.25; o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
