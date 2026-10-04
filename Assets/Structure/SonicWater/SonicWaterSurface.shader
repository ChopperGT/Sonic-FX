Shader "Sonic FX/Eau Green Hill"
{
    Properties
    {
        _Color ("Couleur de l'eau", Color) = (0.03,0.52,0.68,0.65)
        _FoamColor ("Reflets clairs", Color) = (0.55,0.96,1,1)
        _WaveScale ("Taille du motif", Float) = 0.65
        _FlowSpeed ("Vitesse du courant", Float) = 0.45
        _Glossiness ("Brillance", Range(0,1)) = 0.75
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard alpha:fade
        #pragma target 3.0
        fixed4 _Color, _FoamColor;
        half _Glossiness;
        float _WaveScale, _FlowSpeed;
        struct Input { float3 worldPos; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 p = IN.worldPos.xz * _WaveScale;
            float t = _Time.y * _FlowSpeed;
            float a = sin(p.x * 1.8 + sin(p.y * 1.4 + t) + t);
            float b = sin(p.y * 2.2 + cos(p.x * 1.1 - t * 0.6) - t);
            float wave = a * b;
            float foam = smoothstep(0.65,0.95,wave) * 0.45;
            o.Albedo = lerp(_Color.rgb * (0.85 + 0.15 * wave), _FoamColor.rgb, foam);
            o.Emission = _FoamColor.rgb * foam * 0.06;
            o.Normal = normalize(float3(a * 0.14,b * 0.12,1));
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = saturate(_Color.a + foam * 0.15);
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
