using System.Collections.Generic;
using UnityEngine;

namespace SonicFX.Structures
{
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class SonicEditableRamp : MonoBehaviour
    {
        [SerializeField, HideInInspector] Mesh sourceMesh;
        [SerializeField, HideInInspector] Vector3[] points;
        [SerializeField, HideInInspector] Vector3Int pointCounts=new Vector3Int(3,3,3);
        [SerializeField, HideInInspector] float[] knotsX,knotsY,knotsZ;
        public const int MaxPointsPerAxis=16;
        [Range(0,4), HideInInspector] public int meshSubdivisions=2;
        [Min(0), HideInInspector] public float wallDepth;
        public float OriginalBottomY { get; private set; }
        Mesh generated;
        Mesh subdividedSource,cachedSource;
        int cachedSubdivisions=-1;
        bool[] baseVertices;
        bool dirty=true;
        public Mesh SourceMesh => sourceMesh;
        public virtual string StructureName => "Rampe";
        public Vector3[] Points => points;
        public Vector3Int PointCounts { get { EnsureGrid();return pointCounts; } }
        public string LastError { get; private set; }
        public int TriangleCount { get; private set; }
        // Retained for tools referencing the original 3 x 3 x 3 cage.
        public static int Index(int x,int y,int z) => x+3*y+9*z;
        public int PointIndex(int x,int y,int z)=>x+pointCounts.x*(y+pointCounts.y*z);
        static float[] InitialKnots()=>new float[]{0,0,0,1,1,1};
        void EnsureGrid()
        {
            // Old scenes contain 27 edited points and no knots. Migrate without resetting them.
            if(knotsX==null || knotsX.Length==0){pointCounts=new Vector3Int(3,3,3);knotsX=InitialKnots();knotsY=InitialKnots();knotsZ=InitialKnots();}
        }
        float[] Knots(int axis)=>axis==0?knotsX:axis==1?knotsY:knotsZ;
        public float SuggestedInsertion(int axis,int selectedIndex=-1)
        {
            EnsureGrid();if(axis<0 || axis>2)return .5f;
            var knots=Knots(axis);float anchor=.5f;
            bool nearSelection=selectedIndex>=0 && points!=null && selectedIndex<points.Length;
            if(nearSelection){
                int coordinate=axis==0?selectedIndex%pointCounts.x:axis==1?(selectedIndex/pointCounts.x)%pointCounts.y:selectedIndex/(pointCounts.x*pointCounts.y);
                anchor=(knots[coordinate+1]+knots[coordinate+2])*.5f;
            }
            float best=float.PositiveInfinity,widest=-1,result=.5f;
            for(int i=2;i<pointCounts[axis];i++){
                if(knots[i+1]-knots[i]<.0002f)continue;
                float mid=(knots[i]+knots[i+1])*.5f;
                // Ordinary additions subdivide the widest gap, not the last added row.
                // Following the last row repeatedly would pack controls against one end.
                if(!nearSelection){
                    float width=knots[i+1]-knots[i];
                    if(width>widest){widest=width;result=mid;}
                    continue;
                }
                // Explicit "near selection" placement retains the local insertion tool.
                float distance=anchor<knots[i]?knots[i]-anchor:anchor>knots[i+1]?anchor-knots[i+1]:0;
                if(distance<best || (distance==best && mid>=anchor)){best=distance;result=mid;}
            }
            return result;
        }
        public bool CanInsertPoints(int axis,float position)
        {
            EnsureGrid();if(axis<0 || axis>2 || pointCounts[axis]>=MaxPointsPerAxis || !Finite(position) || position<=.0001f || position>=.9999f)return false;
            // Simple knots keep the deformation and surface normals continuous.
            foreach(float knot in Knots(axis))if(Mathf.Abs(position-knot)<.0001f)return false;
            return true;
        }
        public bool InsertPoints(int axis,float position,out int insertedLayer)
        {
            insertedLayer=-1;if(!CanInsertPoints(axis,position) || points==null || points.Length!=pointCounts.x*pointCounts.y*pointCounts.z)return false;
            var knots=Knots(axis);int span=FindSpan(knots,pointCounts[axis],position);
            var counts=pointCounts;counts[axis]++;
            var next=new Vector3[counts.x*counts.y*counts.z];
            for(int z=0;z<counts.z;z++)for(int y=0;y<counts.y;y++)for(int x=0;x<counts.x;x++){
                var coordinate=new Vector3Int(x,y,z);int i=coordinate[axis];Vector3 p;
                if(i<=span-2)p=points[PointIndex(x,y,z)];
                else if(i>=span+1){coordinate[axis]--;p=points[PointIndex(coordinate.x,coordinate.y,coordinate.z)];}
                else {
                    float alpha=(position-knots[i])/(knots[i+2]-knots[i]);
                    p=points[PointIndex(coordinate.x,coordinate.y,coordinate.z)]*alpha;
                    coordinate[axis]--;p+=points[PointIndex(coordinate.x,coordinate.y,coordinate.z)]*(1-alpha);
                }
                next[x+counts.x*(y+counts.y*z)]=p;
            }
            var newKnots=new float[knots.Length+1];
            System.Array.Copy(knots,0,newKnots,0,span+1);newKnots[span+1]=position;
            System.Array.Copy(knots,span+1,newKnots,span+2,knots.Length-span-1);
            if(axis==0)knotsX=newKnots;else if(axis==1)knotsY=newKnots;else knotsZ=newKnots;
            points=next;pointCounts=counts;insertedLayer=span;dirty=true;return true;
        }
        protected virtual void OnEnable(){dirty=true;Rebuild();}
        protected virtual void OnValidate(){meshSubdivisions=Mathf.Clamp(meshSubdivisions,0,4);dirty=true;}
        protected virtual void Update(){if(dirty)Rebuild();}
        protected virtual void OnDisable(){Release();ReleaseSubdivision();}
        protected virtual void OnDestroy(){Release();ReleaseSubdivision();}
        public void Initialize(Mesh source){Release();ReleaseSubdivision();sourceMesh=source;ResetShape();}
        public void ResetShape()
        {
            if(sourceMesh==null)return;
            pointCounts=new Vector3Int(3,3,3);knotsX=InitialKnots();knotsY=InitialKnots();knotsZ=InitialKnots();
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
        static int FindSpan(float[] knots,int count,float t)
        {
            if(t>=1)return count-1;
            int low=2,high=count;
            while(high-low>1){int mid=(low+high)/2;if(t<knots[mid])high=mid;else low=mid;}
            return low;
        }
        static int Basis(float[] knots,int count,float t,out Vector3 b,out Vector3 d)
        {
            t=Mathf.Clamp01(t);int span=FindSpan(knots,count,t);
            float left=t-knots[span],right=knots[span+1]-t;
            float linearLeft=right/(left+right),linearRight=left/(left+right);
            float a=linearLeft/(knots[span+1]-knots[span-1]),c=linearRight/(knots[span+2]-knots[span]);
            b=new Vector3(right*a,(t-knots[span-1])*a+(knots[span+2]-t)*c,left*c);
            d=new Vector3(-2*a,2*a-2*c,2*c);return span-2;
        }
        void Evaluate(Vector3 t,out Vector3 p,out Vector3 dx,out Vector3 dy,out Vector3 dz)
        {
            int ix=Basis(knotsX,pointCounts.x,t.x,out var bx,out var ax),iy=Basis(knotsY,pointCounts.y,t.y,out var by,out var ay),iz=Basis(knotsZ,pointCounts.z,t.z,out var bz,out var az);
            p=dx=dy=dz=Vector3.zero;
            for(int z=0;z<3;z++)for(int y=0;y<3;y++)for(int x=0;x<3;x++){
                var c=points[PointIndex(ix+x,iy+y,iz+z)];p+=c*(bx[x]*by[y]*bz[z]);
                dx+=c*(ax[x]*by[y]*bz[z]);dy+=c*(bx[x]*ay[y]*bz[z]);dz+=c*(bx[x]*by[y]*az[z]);
            }
        }
        public Vector3 DeformPoint(Vector3 local)
        {
            EnsureGrid();
            var b=sourceMesh.bounds;var d=local-b.min;
            Evaluate(new Vector3(d.x/b.size.x,d.y/b.size.y,d.z/b.size.z),out var p,out _,out _,out _);return p;
        }
        public bool Rebuild()
        {
            dirty=false;
            if(!isActiveAndEnabled || sourceMesh==null)return false;
            if(!sourceMesh.isReadable){LastError="La geometrie source doit etre lisible. Relance l'installation de cette structure.";return false;}
            EnsureGrid();if(points==null)ResetShape();
            if(points.Length!=pointCounts.x*pointCounts.y*pointCounts.z || !ValidKnots(knotsX,pointCounts.x) || !ValidKnots(knotsY,pointCounts.y) || !ValidKnots(knotsZ,pointCounts.z)){
                LastError="La grille de points est invalide. Annule la derniere modification.";return false;
            }
            var b=sourceMesh.bounds;
            if(!Finite(wallDepth) || wallDepth<0){LastError="La profondeur doit etre positive ou nulle.";return false;}
            if(b.size.x<.0001f || b.size.y<.0001f || b.size.z<.0001f){LastError="La geometrie source est plate ou vide.";return false;}
            foreach(var p in points)if(!Finite(p.x)||!Finite(p.y)||!Finite(p.z)){LastError="Un point a une valeur invalide.";return false;}
            // Reject folded/collapsed cages before replacing the last valid collider.
            float reference=b.size.x*b.size.y*b.size.z;
            // Sample every knot span, including newly added local controls.
            var samplesX=ValidationSamples(knotsX);var samplesY=ValidationSamples(knotsY);var samplesZ=ValidationSamples(knotsZ);
            foreach(float z in samplesZ)foreach(float y in samplesY)foreach(float x in samplesX){
                Evaluate(new Vector3(x,y,z),out _,out var dx,out var dy,out var dz);
                if(Vector3.Dot(dx,Vector3.Cross(dy,dz))<=reference*.00001f){LastError="Ces points replient ou ecrasent la structure. Annule ou ecarte les points : la derniere forme valide est conservee.";return false;}
            }
            var topology=PreparedSource();var vertices=topology.vertices;var normals=topology.normals;
            for(int i=0;i<vertices.Length;i++){
                var d=vertices[i]-b.min;
                Evaluate(new Vector3(d.x/b.size.x,d.y/b.size.y,d.z/b.size.z),out vertices[i],out var dx,out var dy,out var dz);
                if(normals.Length==vertices.Length){
                    dx/=b.size.x;dy/=b.size.y;dz/=b.size.z;
                    var n=normals[i];normals[i]=(Vector3.Cross(dy,dz)*n.x+Vector3.Cross(dz,dx)*n.y+Vector3.Cross(dx,dy)*n.z).normalized;
                }
            }
            OriginalBottomY=float.PositiveInfinity;foreach(var v in vertices)OriginalBottomY=Mathf.Min(OriginalBottomY,v.y);
            if(wallDepth>0 && !ExtendBase(topology,vertices,normals,OriginalBottomY-wallDepth))return false;
            var mesh=Instantiate(topology);mesh.name=StructureName+" editable";mesh.hideFlags=HideFlags.DontSave;
            mesh.vertices=vertices;
            if(normals.Length==vertices.Length)mesh.normals=normals;else mesh.RecalculateNormals();
            mesh.RecalculateBounds();if(mesh.uv.Length==vertices.Length)mesh.RecalculateTangents();
            Release();generated=mesh;GetComponent<MeshFilter>().sharedMesh=mesh;
            // Existing scene instances can have an additional MeshCollider override.
            // Keep every collider on this ramp in sync to avoid invisible old geometry.
            foreach(var collider in GetComponents<MeshCollider>()){collider.sharedMesh=null;collider.sharedMesh=mesh;}
            TriangleCount=0;for(int s=0;s<mesh.subMeshCount;s++)TriangleCount+=(int)mesh.GetIndexCount(s)/3;
            LastError=null;dirty=false;return true;
        }
        Mesh PreparedSource()
        {
            int level=Mathf.Clamp(meshSubdivisions,0,4);
            if(cachedSource!=sourceMesh || cachedSubdivisions!=level){
                ReleaseSubdivision();cachedSource=sourceMesh;cachedSubdivisions=level;
                if(level>0)subdividedSource=SonicRampMeshSubdivision.Create(sourceMesh,level);
            }
            return subdividedSource!=null?subdividedSource:sourceMesh;
        }
        void ReleaseSubdivision()
        {
            if(subdividedSource!=null){if(Application.isPlaying)Destroy(subdividedSource);else DestroyImmediate(subdividedSource);}
            subdividedSource=null;cachedSource=null;cachedSubdivisions=-1;baseVertices=null;
        }
        static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        static bool ValidKnots(float[] knots,int count)
        {
            if(count<3 || count>MaxPointsPerAxis || knots==null || knots.Length!=count+3)return false;
            for(int i=0;i<knots.Length;i++)if(!Finite(knots[i]) || knots[i]<0 || knots[i]>1 || (i>0 && knots[i]<knots[i-1]))return false;
            return knots[0]==0 && knots[1]==0 && knots[2]==0 && knots[count]==1 && knots[count+1]==1 && knots[count+2]==1;
        }
        static List<float> ValidationSamples(float[] knots)
        {
            var result=new List<float>{0};for(int i=2;i<knots.Length-3;i++){
                float a=knots[i],b=knots[i+1];if(b<=a)continue;
                result.Add(Mathf.Lerp(a,b,.25f));result.Add((a+b)*.5f);result.Add(Mathf.Lerp(a,b,.75f));result.Add(b);
            }return result;
        }
        bool ExtendBase(Mesh topology,Vector3[] vertices,Vector3[] normals,float bottom)
        {
            // The imported ramp has a decorative overhanging rim above its main base.
            // Choose the lowest downward-facing submesh, leaving the rim and road intact.
            if(baseVertices==null){
            var original=topology.vertices;var bottomIndices=new HashSet<int>();float lowest=float.PositiveInfinity;
            for(int s=0;s<topology.subMeshCount;s++){
                var triangles=topology.GetTriangles(s);var indices=new HashSet<int>();float height=0;int count=0;
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
            // Spatial buckets avoid a quadratic scan now that the mesh can have 56k triangles.
            float tolerance=sourceMesh.bounds.size.magnitude*.00001f;
            Vector3Int Key(Vector3 p)=>new Vector3Int(Mathf.RoundToInt(p.x/tolerance),Mathf.RoundToInt(p.y/tolerance),Mathf.RoundToInt(p.z/tolerance));
            var positions=new Dictionary<Vector3Int,List<Vector3>>();
            foreach(int j in bottomIndices){var key=Key(original[j]);if(!positions.TryGetValue(key,out var list)){list=new List<Vector3>();positions.Add(key,list);}list.Add(original[j]);}
            baseVertices=new bool[vertices.Length];
            for(int i=0;i<vertices.Length;i++){
                var key=Key(original[i]);
                for(int z=-1;z<=1 && !baseVertices[i];z++)for(int y=-1;y<=1 && !baseVertices[i];y++)for(int x=-1;x<=1 && !baseVertices[i];x++)
                    if(positions.TryGetValue(key+new Vector3Int(x,y,z),out var list))foreach(var p in list)
                        if((original[i]-p).sqrMagnitude<=tolerance*tolerance){baseVertices[i]=true;break;}
            }
            }
            for(int i=0;i<vertices.Length;i++)if(baseVertices[i])vertices[i].y=bottom;
            // Recompute only the extended sides and underside. Preserve the road's normals.
            if(normals.Length==vertices.Length){
                var sums=new Vector3[vertices.Length];var affected=new bool[vertices.Length];var triangles=topology.triangles;
                for(int i=0;i<triangles.Length;i+=3){
                    int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                    var n=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);
                    sums[a]+=n;sums[b]+=n;sums[c]+=n;
                    if(baseVertices[a]||baseVertices[b]||baseVertices[c])affected[a]=affected[b]=affected[c]=true;
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
