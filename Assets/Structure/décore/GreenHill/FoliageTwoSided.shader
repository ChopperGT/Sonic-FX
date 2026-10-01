Shader "SonicFX/Decor Foliage Two Sided" {
 Properties { _MainTex("Feuillage",2D)="white"{} _Color("Couleur",Color)=(1,1,1,1) _Glossiness("Lissage",Range(0,1))=.08 _Metallic("Metal",Range(0,1))=0 }
 SubShader { Tags {"RenderType"="Opaque"} Cull Off
 CGPROGRAM
 #pragma surface surf Standard fullforwardshadows
 #pragma target 3.0
 sampler2D _MainTex; fixed4 _Color; half _Glossiness,_Metallic;
 struct Input { float2 uv_MainTex; float facing:VFACE; };
 void surf(Input IN,inout SurfaceOutputStandard o) {o.Albedo=tex2D(_MainTex,IN.uv_MainTex).rgb*_Color.rgb;o.Normal=float3(0,0,IN.facing>=0?1:-1);o.Smoothness=_Glossiness;o.Metallic=_Metallic;o.Alpha=1;}
 ENDCG
 } Fallback "Diffuse"
}
