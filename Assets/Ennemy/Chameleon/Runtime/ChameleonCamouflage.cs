using UnityEngine;
namespace SonicFX.Chameleon
{
    public sealed class ChameleonCamouflage : MonoBehaviour
    {
        public Renderer[] surfaces;
        public Material camouflageTemplate;
        [Tooltip("Facultatif : materiau a copier si le mur utilise un rendu particulier.")]
        public Material wallMaterialOverride;
        [SerializeField] string textureSource;
        public string TextureSource => textureSource;
        public bool HasWallTexture { get; private set; }
        Material instance; Material[][] originals;
        void Prepare()
        {
            if (originals == null) { originals=new Material[surfaces.Length][]; for(int i=0;i<surfaces.Length;i++) originals[i]=surfaces[i].sharedMaterials; }
            if(instance==null && camouflageTemplate!=null) instance=new Material(camouflageTemplate);
        }
        public void Capture(RaycastHit hit)
        {
            Prepare(); if(instance==null) return;
            var terrain=hit.collider.GetComponentInParent<Terrain>();
            var renderer=terrain==null?FindWallRenderer(hit):null;
            Material source=wallMaterialOverride;
            if(source==null && terrain!=null) source=terrain.materialTemplate;
            if(source==null && renderer!=null) source=SurfaceMaterial(renderer,hit);
            Texture texture=null; Vector2 scale=Vector2.one, offset=Vector2.zero; Color color=Color.white;
            float worldScale=.1f; bool triplanar=source!=null && source.HasProperty("_Side");
            Vector3 u=Vector3.Cross(Vector3.up,hit.normal).normalized, v=Vector3.up;
            if(source!=null) {
                string property=triplanar?"_Side":source.HasProperty("_BaseMap")?"_BaseMap":"_MainTex";
                if(source.HasProperty(property)) { texture=source.GetTexture(property); scale=source.GetTextureScale(property); offset=source.GetTextureOffset(property); }
                if(source.HasProperty("_Color")) color=source.GetColor("_Color");
                if(triplanar && source.HasProperty("_SideScale")) worldScale=source.GetFloat("_SideScale");
            }
            Vector2 uv=hit.textureCoord;
            var mc=hit.collider as MeshCollider;
            if(!triplanar && mc!=null && mc.sharedMesh!=null && mc.sharedMesh.isReadable && hit.triangleIndex>=0) {
                var mesh=mc.sharedMesh; var indices=mesh.triangles; var vertices=mesh.vertices; var tex=mesh.uv; int t=hit.triangleIndex*3;
                if(t+2<indices.Length && tex.Length==vertices.Length) {
                    int a=indices[t],b=indices[t+1],c=indices[t+2];
                    Vector3 e1=mc.transform.TransformVector(vertices[b]-vertices[a]), e2=mc.transform.TransformVector(vertices[c]-vertices[a]);
                    Vector2 d1=tex[b]-tex[a],d2=tex[c]-tex[a]; float g11=Vector3.Dot(e1,e1),g22=Vector3.Dot(e2,e2),g12=Vector3.Dot(e1,e2),det=g11*g22-g12*g12;
                    if(Mathf.Abs(det)>.000001f) { u=(e1*(d1.x*g22-d2.x*g12)+e2*(d2.x*g11-d1.x*g12))/det; v=(e1*(d1.y*g22-d2.y*g12)+e2*(d2.y*g11-d1.y*g12))/det; }
                }
            }
            // A Terrain can render a custom world-space material without TerrainLayers.
            // Only use painted layers when the material itself did not supply a texture.
            if(texture==null && terrain!=null && terrain.terrainData!=null && terrain.terrainData.terrainLayers.Length>0) {
                var data=terrain.terrainData; Vector3 p=hit.point-terrain.transform.position;
                int x=Mathf.Clamp((int)(p.x/data.size.x*data.alphamapWidth),0,data.alphamapWidth-1),z=Mathf.Clamp((int)(p.z/data.size.z*data.alphamapHeight),0,data.alphamapHeight-1);
                var weights=data.GetAlphamaps(x,z,1,1); int best=0; for(int i=1;i<data.terrainLayers.Length;i++) if(weights[0,0,i]>weights[0,0,best]) best=i;
                var layer=data.terrainLayers[best]; if(layer!=null) {
                    texture=layer.diffuseTexture; u=Vector3.right/Mathf.Max(.01f,layer.tileSize.x);v=Vector3.forward/Mathf.Max(.01f,layer.tileSize.y);
                    uv=new Vector2(p.x/Mathf.Max(.01f,layer.tileSize.x),p.z/Mathf.Max(.01f,layer.tileSize.y)); offset=new Vector2(layer.tileOffset.x/Mathf.Max(.01f,layer.tileSize.x),layer.tileOffset.y/Mathf.Max(.01f,layer.tileSize.y)); triplanar=false;
                }
            }
            HasWallTexture=texture!=null;
            textureSource=HasWallTexture ? hit.collider.name+" / "+(source!=null?source.name:"TerrainLayer")+" / "+texture.name : "Texture introuvable sur "+hit.collider.name+" : renseigner Wall Material Override";
            if(!HasWallTexture) Debug.LogWarning("Cameleon : "+textureSource,this);
            instance.SetTexture("_MainTex",texture!=null?texture:Texture2D.whiteTexture); instance.SetTextureScale("_MainTex",scale);instance.SetTextureOffset("_MainTex",offset);
            instance.SetColor("_Color",color); instance.SetVector("_Anchor",hit.point);instance.SetVector("_AnchorUV",uv);
            instance.SetVector("_U",u);instance.SetVector("_V",v); instance.SetVector("_WallNormal",hit.normal);
            instance.SetFloat("_WorldScale",worldScale);instance.SetFloat("_Triplanar",triplanar?1:0);
        }
        static Renderer FindWallRenderer(RaycastHit hit)
        {
            var direct=hit.collider.GetComponent<Renderer>(); if(direct!=null)return direct;
            // Prefer the nearest ancestor. Limit sibling search to the first local group,
            // never to the entire level, to avoid sampling an unrelated floor.
            var parent=hit.collider.GetComponentInParent<Renderer>();if(parent!=null)return parent;
            Transform group=hit.collider.transform.parent!=null?hit.collider.transform.parent:hit.collider.transform;
            Renderer best=null;float distance=.25f;
            foreach(var candidate in group.GetComponentsInChildren<Renderer>()) {
                if(candidate is ParticleSystemRenderer || candidate is LineRenderer || candidate is TrailRenderer || !candidate.enabled)continue;
                float d=candidate.bounds.SqrDistance(hit.point);if(d<=distance){distance=d;best=candidate;}
            }
            return best;
        }
        static Material SurfaceMaterial(Renderer renderer,RaycastHit hit)
        {
            var materials=renderer.sharedMaterials;if(materials.Length==0)return null;
            var collider=hit.collider as MeshCollider;var filter=renderer.GetComponent<MeshFilter>();
            if(collider!=null && filter!=null && filter.sharedMesh==collider.sharedMesh && hit.triangleIndex>=0) {
                long index=hit.triangleIndex*3L;
                for(int i=0;i<collider.sharedMesh.subMeshCount;i++) {long count=collider.sharedMesh.GetIndexCount(i);if(index<count)return materials[Mathf.Min(i,materials.Length-1)];index-=count;}
            }
            return materials[0];
        }
        public void Apply(bool enabled)
        {
            Prepare(); for(int i=0;i<surfaces.Length;i++) if(surfaces[i]!=null) {
                if(!enabled || instance==null) surfaces[i].sharedMaterials=originals[i];
                else { var list=new Material[originals[i].Length]; for(int j=0;j<list.Length;j++) list[j]=instance;surfaces[i].sharedMaterials=list; }
            }
        }
        void OnDestroy() { if(instance!=null) { if(Application.isPlaying) Destroy(instance); else DestroyImmediate(instance); } }
    }
}
