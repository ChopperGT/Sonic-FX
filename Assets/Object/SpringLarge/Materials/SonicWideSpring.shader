Shader "Sonic FX/Spring Large"
{
    Properties
    {
        _Color ("Couleur", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _SpecGlossMap ("Reflets", 2D) = "white" {}
        _SpecularStrength ("Intensite des reflets", Range(0,1)) = 0.1
        _GlossMapScale ("Brillance", Range(0,1)) = 0.25
        _EmissionMap ("Emission", 2D) = "black" {}
        _EmissionColor ("Couleur emission", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf StandardSpecular fullforwardshadows addshadow
        #pragma target 3.0
        sampler2D _MainTex, _SpecGlossMap, _EmissionMap;
        fixed4 _Color, _EmissionColor;
        half _SpecularStrength, _GlossMapScale;
        struct Input { float2 uv_MainTex; float2 uv_SpecGlossMap; float2 uv_EmissionMap; };
        void surf(Input IN, inout SurfaceOutputStandardSpecular o)
        {
            fixed4 diffuse = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            fixed4 specular = tex2D(_SpecGlossMap, IN.uv_SpecGlossMap);
            o.Albedo = diffuse.rgb;
            o.Specular = specular.rgb * _SpecularStrength;
            o.Smoothness = specular.a * _GlossMapScale;
            o.Emission = tex2D(_EmissionMap, IN.uv_EmissionMap).rgb * _EmissionColor.rgb;
            o.Alpha = diffuse.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
