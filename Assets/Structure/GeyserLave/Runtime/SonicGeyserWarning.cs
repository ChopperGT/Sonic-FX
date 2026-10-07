using UnityEngine;
namespace SonicFX.Lava
{
    public sealed class SonicGeyserWarning : MonoBehaviour
    {
        Mesh generated;
        MaterialPropertyBlock properties;
        public void Initialise(SonicGeyserLandingZone zone,Vector3 point,Vector3 normal,float radius,Material material)
        {
            transform.position=point;transform.rotation=Quaternion.identity;
            const int segments=64;var vertices=new Vector3[segments+1];var uv=new Vector2[segments+1];var triangles=new int[segments*3];
            vertices[0]=normal*.045f;uv[0]=new Vector2(.5f,.5f);
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2/segments;Vector3 offset=new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);
                Vector3 world=point+offset;
                if(zone.projectOnGround && zone.ResolveGround(world,out var floor,out var n))vertices[i+1]=floor-point+n*.045f;
                else vertices[i+1]=offset+normal*.045f;
                uv[i+1]=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.5f+Vector2.one*.5f;
                triangles[i*3]=0;triangles[i*3+1]=(i+1)%segments+1;triangles[i*3+2]=i+1;
            }
            generated=new Mesh{name="Avertissement impact",hideFlags=HideFlags.DontSave};generated.vertices=vertices;generated.uv=uv;generated.triangles=triangles;generated.RecalculateBounds();generated.RecalculateNormals();
            gameObject.AddComponent<MeshFilter>().sharedMesh=generated;gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;SetProgress(0);
        }
        public void SetProgress(float progress)
        {
            if(properties==null)properties=new MaterialPropertyBlock();properties.SetFloat("_Progress",Mathf.Clamp01(progress));GetComponent<Renderer>().SetPropertyBlock(properties);
        }
        void OnDestroy(){if(generated!=null){if(Application.isPlaying)Destroy(generated);else DestroyImmediate(generated);}}
    }
}
