Shader "SonicFX/Chameleon Camouflage" {
 Properties { _MainTex("Wall texture",2D)="white"{} _Color("Tint",Color)=(1,1,1,1) _WorldScale("World scale",Float)=.1 _Triplanar("World mapping",Float)=0 }
 SubShader { Tags { "RenderType"="Opaque" } LOD 200
 CGPROGRAM
 #pragma surface surf Standard fullforwardshadows
 #pragma target 3.0
 sampler2D _MainTex; float4 _MainTex_ST,_Color,_Anchor,_AnchorUV,_U,_V,_WallNormal; float _WorldScale,_Triplanar;
 struct Input { float3 worldPos; };
 void surf(Input IN,inout SurfaceOutputStandard o) {
 float3 delta=IN.worldPos-_Anchor.xyz;
 float2 uv=float2(dot(delta,_U.xyz),dot(delta,_V.xyz))+_AnchorUV.xy;
 fixed4 c=tex2D(_MainTex,uv*_MainTex_ST.xy+_MainTex_ST.zw);
 if(_Triplanar>.5) { float x=abs(_WallNormal.x),z=abs(_WallNormal.z); c=(tex2D(_MainTex,IN.worldPos.zy*_WorldScale)*x+tex2D(_MainTex,IN.worldPos.xy*_WorldScale)*z)/max(.001,x+z); }
 o.Albedo=c.rgb*_Color.rgb; o.Metallic=0; o.Smoothness=.1; o.Alpha=1;
 }
 ENDCG
 } Fallback "Diffuse"
}
