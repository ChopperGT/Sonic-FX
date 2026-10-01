using System.Collections.Generic;
using UnityEngine;

namespace SonicFX.Structures
{
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class SonicEditableRamp : MonoBehaviour
    {
        [SerializeField, HideInInspector] Mesh sourceMesh;
        [SerializeField, HideInInspector] Vector3[] points;
        [Min(0), HideInInspector] public float wallDepth;
        public float OriginalBottomY { get; private set; }
        Mesh generated;
        bool dirty=true;
        public Mesh SourceMesh => sourceMesh;
        public Vector3[] Points => points;
        public string LastError { get; private set; }
        public static int Index(int x,int y,int z) => x+3*y+9*z;
        void OnEnable(){dirty=true;Rebuild();}
        void OnValidate(){dirty=true;}
        void Update(){if(dirty)Rebuild();}
        void OnDisable(){Release();}
        void OnDestroy(){Release();}
        public void Initialize(Mesh source){Release();sourceMesh=source;ResetShape();}
        public void ResetShape()
        {
            if(sourceMesh==null)return;
            var b=sourceMesh.bounds;points=new Vector3[27];
            for(int z=0;z<3;z++)for(int y=0;y<3;y++)for(int x=0;x<3;x++)
                points[Index(x,y,z)]=b.min+Vector3.Scale(b.size,new Vector3(x*.5f,y*.5f,z*.5f));
            dirty=true;
        }
        public Bounds ControlBounds
        {
            get {
                if(points==null || points.Length==0)return new Bounds();
                var b=new Bounds(points[0],Vector3.zero);foreach(var p in points)b.Encapsulate(p);return b;
            }
        }
        public void MovePoints(IEnumerable<int> indices,Vector3 delta)
        {
            if(points==null)return;
            var visited=new HashSet<int>();
            foreach(int i in indices)if(i>=0 && i<points.Length && visited.Add(i))points[i]+=delta;
            dirty=true;
        }
        public void Resize(Vector3 size)
        {
            var b=ControlBounds;
            if(b.size.x<=0 || b.size.y<=0 || b.size.z<=0)return;
            var f=new Vector3(Mathf.Max(.01f,size.x)/b.size.x,Mathf.Max(.01f,size.y)/b.size.y,Mathf.Max(.01f,size.z)/b.size.z);
            for(int i=0;i<points.Length;i++)points[i]=b.center+Vector3.Scale(points[i]-b.center,f);
            dirty=true;
        }
        static Vector3 Basis(float t)=>new Vector3((1-t)*(1-t),2*t*(1-t),t*t);
        static Vector3 Derivative(float t)=>new Vector3(2*t-2,2-4*t,2*t);
        void Evaluate(Vector3 t,out Vector3 p,out Vector3 dx,out Vector3 dy,out Vector3 dz)
        {
            Vector3 bx=Basis(t.x),by=Basis(t.y),bz=Basis(t.z),ax=Derivative(t.x),ay=Derivative(t.y),az=Derivative(t.z);
            p=dx=dy=dz=Vector3.zero;
            for(int z=0;z<3;z++)for(int y=0;y<3;y++)for(int x=0;x<3;x++){
                var c=points[Index(x,y,z)];p+=c*(bx[x]*by[y]*bz[z]);
                dx+=c*(ax[x]*by[y]*bz[z]);dy+=c*(bx[x]*ay[y]*bz[z]);dz+=c*(bx[x]*by[y]*az[z]);
            }
        }
        public Vector3 DeformPoint(Vector3 local)
        {
            var b=sourceMesh.bounds;var d=local-b.min;
            Evaluate(new Vector3(d.x/b.size.x,d.y/b.size.y,d.z/b.size.z),out var p,out _,out _,out _);return p;
        }
        public bool Rebuild()
        {
            dirty=false;
            if(!isActiveAndEnabled || sourceMesh==null)return false;
            if(!sourceMesh.isReadable){LastError="La geometrie source doit etre lisible. Relance l'installation de la rampe.";return false;}
            if(points==null || points.Length!=27)ResetShape();
            var b=sourceMesh.bounds;
            if(!Finite(wallDepth) || wallDepth<0){LastError="La profondeur doit etre positive ou nulle.";return false;}
            if(b.size.x<.0001f || b.size.y<.0001f || b.size.z<.0001f){LastError="La rampe source est plate ou vide.";return false;}
            foreach(var p in points)if(!Finite(p.x)||!Finite(p.y)||!Finite(p.z)){LastError="Un point a une valeur invalide.";return false;}
            // Reject folded/collapsed cages before replacing the last valid collider.
            float reference=b.size.x*b.size.y*b.size.z;
            for(int z=0;z<=4;z++)for(int y=0;y<=4;y++)for(int x=0;x<=4;x++){
                Evaluate(new Vector3(x*.25f,y*.25f,z*.25f),out _,out var dx,out var dy,out var dz);
                if(Vector3.Dot(dx,Vector3.Cross(dy,dz))<=reference*.00001f){LastError="Ces points replient ou ecrasent la rampe. Annule ou ecarte les points : la derniere forme valide est conservee.";return false;}
            }
            var vertices=sourceMesh.vertices;var normals=sourceMesh.normals;
            for(int i=0;i<vertices.Length;i++){
                var d=vertices[i]-b.min;
                Evaluate(new Vector3(d.x/b.size.x,d.y/b.size.y,d.z/b.size.z),out vertices[i],out var dx,out var dy,out var dz);
                if(normals.Length==vertices.Length){
                    dx/=b.size.x;dy/=b.size.y;dz/=b.size.z;
                    var n=normals[i];normals[i]=(Vector3.Cross(dy,dz)*n.x+Vector3.Cross(dz,dx)*n.y+Vector3.Cross(dx,dy)*n.z).normalized;
                }
            }
            OriginalBottomY=float.PositiveInfinity;foreach(var v in vertices)OriginalBottomY=Mathf.Min(OriginalBottomY,v.y);
            if(wallDepth>0 && !ExtendBase(vertices,normals,OriginalBottomY-wallDepth))return false;
            var mesh=Instantiate(sourceMesh);mesh.name="ramp_C editable";mesh.hideFlags=HideFlags.DontSave;
            mesh.vertices=vertices;
            if(normals.Length==vertices.Length)mesh.normals=normals;else mesh.RecalculateNormals();
            mesh.RecalculateBounds();if(mesh.uv.Length==vertices.Length)mesh.RecalculateTangents();
            Release();generated=mesh;GetComponent<MeshFilter>().sharedMesh=mesh;
            // Existing scene instances can have an additional MeshCollider override.
            // Keep every collider on this ramp in sync to avoid invisible old geometry.
            foreach(var collider in GetComponents<MeshCollider>()){collider.sharedMesh=null;collider.sharedMesh=mesh;}
            LastError=null;dirty=false;return true;
        }
        static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        bool ExtendBase(Vector3[] vertices,Vector3[] normals,float bottom)
        {
            // The imported ramp has a decorative overhanging rim above its main base.
            // Choose the lowest downward-facing submesh, leaving the rim and road intact.
            var original=sourceMesh.vertices;var bottomIndices=new HashSet<int>();float lowest=float.PositiveInfinity;
            for(int s=0;s<sourceMesh.subMeshCount;s++){
                var triangles=sourceMesh.GetTriangles(s);var indices=new HashSet<int>();float height=0;int count=0;
                for(int i=0;i<triangles.Length;i+=3){
                    int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                    var normal=Vector3.Cross(original[b]-original[a],original[c]-original[a]).normalized;
                    if(normal.y>=-.05f)continue;
                    indices.Add(a);indices.Add(b);indices.Add(c);height+=(original[a].y+original[b].y+original[c].y)/3;count++;
                }
                if(count>0 && height/count<lowest){lowest=height/count;bottomIndices=indices;}
            }
            if(bottomIndices.Count==0){LastError="Aucune face inferieure trouvee pour prolonger le mur.";return false;}
            // UV/normal seams duplicate positions: move all copies to keep the solid closed.
            float tolerance=sourceMesh.bounds.size.magnitude*.00001f;var moved=new bool[vertices.Length];
            for(int i=0;i<vertices.Length;i++)foreach(int j in bottomIndices)
                if((original[i]-original[j]).sqrMagnitude<=tolerance*tolerance){vertices[i].y=bottom;moved[i]=true;break;}
            // Recompute only the extended sides and underside. Preserve the road's normals.
            if(normals.Length==vertices.Length){
                var sums=new Vector3[vertices.Length];var affected=new bool[vertices.Length];var triangles=sourceMesh.triangles;
                for(int i=0;i<triangles.Length;i+=3){
                    int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                    var n=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);
                    sums[a]+=n;sums[b]+=n;sums[c]+=n;
                    if(moved[a]||moved[b]||moved[c])affected[a]=affected[b]=affected[c]=true;
                }
                for(int i=0;i<vertices.Length;i++)if(affected[i] && sums[i].sqrMagnitude>.000001f)normals[i]=sums[i].normalized;
            }
            return true;
        }
        void Release()
        {
            if(generated==null)return;
            var filter=GetComponent<MeshFilter>();
            if(filter!=null && filter.sharedMesh==generated)filter.sharedMesh=sourceMesh;
            foreach(var collider in GetComponents<MeshCollider>())if(collider.sharedMesh==generated)collider.sharedMesh=sourceMesh;
            if(Application.isPlaying)Destroy(generated);else DestroyImmediate(generated);generated=null;
        }
    }
}
