Shader "Sonic FX/Pierre magma"
{
    Properties
    {
        _RockColor ("Roche", Color) = (.055,.045,.05,1)
        _MagmaColor ("Magma", Color) = (1,.13,.005,1)
        _HotColor ("Fissures", Color) = (1,.68,.02,1)
        _Glow ("Incandescence", Range(0,5)) = 1.8
        _CrackWidth ("Largeur des fissures", Range(.01,.3)) = .06
        _PatternScale ("Echelle du motif", Float) = 2.5
        _PulseSpeed ("Pulsation", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        fixed4 _RockColor, _MagmaColor, _HotColor;
        float _Glow, _CrackWidth, _PatternScale, _PulseSpeed;
        struct Input { float3 worldPos; };
        float3 randomCell(float3 p)
        {
            return frac(sin(float3(dot(p,float3(127.1,311.7,74.7)),dot(p,float3(269.5,183.3,246.1)),dot(p,float3(113.5,271.9,124.6))))*43758.5453);
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 p=IN.worldPos*_PatternScale;
            p+=.1*sin(p.yzx*2.4);
            float3 cell=floor(p), localPos=frac(p); float nearest=10, second=10;
            for(int z=-1;z<=1;z++)for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
            {
                float3 neighbour=float3(x,y,z);
                float d=length(neighbour+.2+.6*randomCell(cell+neighbour)-localPos);
                if(d<nearest){second=nearest;nearest=d;}else if(d<second)second=d;
            }
            float edge=second-nearest;
            float crack=1-smoothstep(_CrackWidth*.3,_CrackWidth,edge);
            float core=1-smoothstep(0,_CrackWidth*.35,edge);
            float grain=.65+.35*randomCell(floor(p*23)).x;
            float pulse=.7+.3*sin(_Time.y*_PulseSpeed*2+p.x*.4+p.z*.6);
            float3 molten=lerp(_MagmaColor.rgb,_HotColor.rgb,core);
            o.Albedo=lerp(_RockColor.rgb*grain,molten,crack*.6);
            o.Emission=molten*crack*_Glow*pulse;
            o.Metallic=.03; o.Smoothness=.16; o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
