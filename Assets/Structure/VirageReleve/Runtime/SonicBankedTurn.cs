using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SonicFX.Structures
{
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider))]
    public class SonicBankedTurn : MonoBehaviour
    {
        public enum TurnDirection { Droite, Gauche }
        [Header("Virage")]
        [InspectorName("Sens du virage")] public TurnDirection direction;
        [InspectorName("Angle du virage"),Range(10,180)] public float angle=90;
        [InspectorName("Rayon interieur"),Min(2)] public float innerRadius=18;
        [Header("Prolongements droits")]
        [InspectorName("Longueur de l'entree"),Min(0)] public float entryLength;
        [InspectorName("Longueur de la sortie"),Min(0)] public float exitLength;
        [Header("Sol et mur exterieur")]
        [InspectorName("Largeur du sol plat"),Min(.5f)] public float flatWidth=6;
        [InspectorName("Largeur de la pente"),Min(1)] public float rampWidth=10;
        [InspectorName("Hauteur du mur"),Min(1)] public float wallHeight=10;
        [InspectorName("Epaisseur du socle"),Min(.1f)] public float baseDepth=2;
        [Header("Raccord interieur")]
        [InspectorName("Combler le trou interieur")]
        [Tooltip("Prolonge le sol plat jusqu'au centre du virage et retire la paroi du trou. Le remplissage partage la collision du virage.")]
        public bool fillInterior;
        [Header("Raccords lateraux")]
        [InspectorName("Lisser les cotes")]
        [Tooltip("Ramene progressivement la pente au sol a l'entree et a la sortie. Modifie aussi la collision.")]
        public bool smoothSides;
        [InspectorName("Etendue du lissage"),Range(.1f,.5f)] public float sideSmoothing=.3f;
        [Header("Finesse de la courbe")]
        [InspectorName("Longueur des segments"),Range(.2f,2)] public float segmentLength=.5f;
        [InspectorName("Segments de la pente"),Range(16,64)] public int rampSegments=48;
        [InspectorName("Taille des UV"),Min(.1f)] public float uvSize=4;
        Mesh ownedMesh;
        bool dirty=true;
        public float Sign => direction==TurnDirection.Droite?1:-1;
        public float OuterRadius=>innerRadius+flatWidth+rampWidth;
        public Vector3 EntryPosition=>Point(0,innerRadius+flatWidth*.5f,0)-Vector3.forward*entryLength;
        public Vector3 ExitPosition=>Point(angle*Mathf.Deg2Rad,innerRadius+flatWidth*.5f,0)+ExitForward*exitLength;
        public Vector3 ExitForward=>new Vector3(Sign*Mathf.Sin(angle*Mathf.Deg2Rad),0,Mathf.Cos(angle*Mathf.Deg2Rad));

        void OnEnable(){dirty=true;Rebuild();}
        void OnValidate(){dirty=true;}
        void Update(){if(dirty || ownedMesh==null)Rebuild();}
        void OnDisable(){Release();}
        void OnDestroy(){Release();}
        void Release()
        {
            if(ownedMesh==null)return;
            var filter=GetComponent<MeshFilter>();var collider=GetComponent<MeshCollider>();
            if(filter!=null && filter.sharedMesh==ownedMesh)filter.sharedMesh=null;
            if(collider!=null && collider.sharedMesh==ownedMesh)collider.sharedMesh=null;
            if(Application.isPlaying)Destroy(ownedMesh);else DestroyImmediate(ownedMesh);ownedMesh=null;
        }
        public void Rebuild()
        {
            if(!isActiveAndEnabled)return;
            ClampParameters();
            var mesh=CreateMesh();mesh.hideFlags=HideFlags.DontSave;
            Release();ownedMesh=mesh;
            GetComponent<MeshFilter>().sharedMesh=mesh;
            var collider=GetComponent<MeshCollider>();collider.convex=false;collider.isTrigger=false;collider.sharedMesh=mesh;
            dirty=false;
        }
        void ClampParameters()
        {
            angle=Mathf.Clamp(angle,10,180);innerRadius=Mathf.Max(2,innerRadius);flatWidth=Mathf.Max(.5f,flatWidth);
            rampWidth=Mathf.Max(1,rampWidth);wallHeight=Mathf.Max(1,wallHeight);baseDepth=Mathf.Max(.1f,baseDepth);
            segmentLength=Mathf.Clamp(segmentLength,.2f,2);rampSegments=Mathf.Clamp(rampSegments,16,64);uvSize=Mathf.Max(.1f,uvSize);
            sideSmoothing=Mathf.Clamp(sideSmoothing,.1f,.5f);
            entryLength=Mathf.Clamp(entryLength,0,1000);exitLength=Mathf.Clamp(exitLength,0,1000);
        }
        public Vector3 Point(float theta,float radius,float y)
        {
            return new Vector3(Sign*(innerRadius-radius*Mathf.Cos(theta)),y,radius*Mathf.Sin(theta));
        }
        float SideBlend(float theta,out float derivative)
        {
            derivative=0;if(!smoothSides)return 1;
            float span=angle*Mathf.Deg2Rad,half=span*.5f;
            float length=span*Mathf.Clamp(sideSmoothing,.1f,.5f);
            float t=Mathf.Clamp01(Mathf.Min(theta,span-theta)/length);
            if(t>=1)return 1;
            // Quintic easing has zero slope and curvature at both ends.
            derivative=30*t*t*(1-t)*(1-t)/length*(theta<=half?1:-1);
            return t*t*t*(10+t*(-15+6*t));
        }
        public void EvaluateRamp(float theta,float progress,out Vector3 position,out Vector3 normal)
        {
            float u=Mathf.Clamp01(progress),phi=u*Mathf.PI*.5f;
            float blend=SideBlend(theta,out float db),s=Mathf.Sin(phi),c=Mathf.Cos(phi);
            // At the side openings the cross-section becomes fully flat, with regular spacing.
            float radius=innerRadius+flatWidth+rampWidth*Mathf.Lerp(u,s,blend);
            float y=wallHeight*(1-c)*blend;
            float dr=rampWidth*((1-blend)+blend*Mathf.PI*.5f*c),dy=wallHeight*Mathf.PI*.5f*s*blend;
            float rt=rampWidth*(s-u)*db,yt=wallHeight*(1-c)*db;
            Vector3 outward=new Vector3(-Sign*Mathf.Cos(theta),0,Mathf.Sin(theta));
            Vector3 tangent=new Vector3(Sign*Mathf.Sin(theta),0,Mathf.Cos(theta));
            position=Point(theta,radius,y);
            normal=(-outward*(dy*radius)+Vector3.up*(dr*radius)+tangent*(dy*rt-dr*yt)).normalized;
        }
        Vector3 ProfilePoint(float theta,int index,int flatSegments)
        {
            if(index<=flatSegments)return Point(theta,innerRadius+flatWidth*index/flatSegments,0);
            EvaluateRamp(theta,(float)(index-flatSegments)/rampSegments,out var point,out _);return point;
        }
        struct Row
        {
            public float theta, offset;
            public Row(float angle,float extension){theta=angle;offset=extension;}
            public Vector3 Shift(float sign)=>new Vector3(sign*Mathf.Sin(theta),0,Mathf.Cos(theta))*offset;
        }
        Vector3 At(Row row,float radius,float height)=>Point(row.theta,radius,height)+row.Shift(Sign);
        // With the hole filled, straight extensions also extend the interior floor.
        // For a 180-degree turn their inner edges meet: omit the shared inside wall.
        void FillExtensionWall(List<Vector3> vertices,List<Vector3> normals,List<Vector2> uv,List<int> indices,Row a,Row b,Vector3 normal)
        {
            if(Mathf.Abs(a.offset-b.offset)<.000001f)return;
            if(angle>=179.999f) {
                float other=a.theta<.001f?exitLength:entryLength;
                if(Mathf.Max(Mathf.Abs(a.offset),Mathf.Abs(b.offset))<=other)return;
                if(Mathf.Abs(a.offset)<other)a.offset=Mathf.Sign(b.offset)*other;
                if(Mathf.Abs(b.offset)<other)b.offset=Mathf.Sign(a.offset)*other;
            }
            Quad(vertices,normals,uv,indices,At(a,0,0),At(b,0,0),At(b,0,-baseDepth),At(a,0,-baseDepth),normal,uvSize);
        }
        public Mesh CreateMesh()
        {
            ClampParameters();
            int turns=Mathf.Clamp(Mathf.CeilToInt(OuterRadius*angle*Mathf.Deg2Rad/segmentLength),8,512);
            const int flatSegments=8;int across=flatSegments+rampSegments;
            var rows=new List<Row>();
            int entrySteps=entryLength>0?Mathf.Clamp(Mathf.CeilToInt(entryLength/segmentLength),1,512):0;
            int exitSteps=exitLength>0?Mathf.Clamp(Mathf.CeilToInt(exitLength/segmentLength),1,512):0;
            for(int i=0;i<entrySteps;i++)rows.Add(new Row(0,-entryLength*(1-(float)i/entrySteps)));
            for(int i=0;i<=turns;i++)rows.Add(new Row((float)i/turns*angle*Mathf.Deg2Rad,0));
            for(int i=1;i<=exitSteps;i++)rows.Add(new Row(angle*Mathf.Deg2Rad,exitLength*i/exitSteps));
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
            for(int i=0;i<rows.Count;i++)
            {
                var row=rows[i];float theta=row.theta;
                float distance=0;Vector3 previous=Vector3.zero;
                for(int j=0;j<=across;j++)
                {
                    Vector3 point,normal;
                    if(j<=flatSegments){point=ProfilePoint(theta,j,flatSegments);normal=Vector3.up;}
                    else EvaluateRamp(theta,(float)(j-flatSegments)/rampSegments,out point,out normal);
                    if(j>0)distance+=Vector3.Distance(point,previous);previous=point;
                    float radius=new Vector2(point.x-Sign*innerRadius,point.z).magnitude;
                    vertices.Add(point+row.Shift(Sign));normals.Add(normal);uv.Add(new Vector2((theta*radius+row.offset)/uvSize,distance/uvSize));
                    if(i<rows.Count-1 && j<across)
                    {
                        int a=i*(across+1)+j,b=a+1,c=a+across+1,d=c+1;
                        if(Sign>0){indices.AddRange(new[]{a,b,c,b,d,c});}else{indices.AddRange(new[]{a,c,b,b,c,d});}
                    }
                }
            }
            // Closed underside and facades share positions with the riding surface,
            // with separate normals so the outer block edges remain crisp.
            for(int i=0;i<rows.Count-1;i++)
            {
                Row r0=rows[i],r1=rows[i+1];float t0=r0.theta,t1=r1.theta,mid=(t0+t1)*.5f;
                Vector3 outward=new Vector3(-Sign*Mathf.Cos(mid),0,Mathf.Sin(mid));
                Quad(vertices,normals,uv,indices,At(r0,innerRadius,-baseDepth),At(r1,innerRadius,-baseDepth),At(r1,OuterRadius,-baseDepth),At(r0,OuterRadius,-baseDepth),Vector3.down,uvSize);
                if(!fillInterior)
                    Quad(vertices,normals,uv,indices,At(r0,innerRadius,0),At(r1,innerRadius,0),At(r1,innerRadius,-baseDepth),At(r0,innerRadius,-baseDepth),-outward,uvSize);
                else
                {
                    // Flat fan reaches the exact inner edge of the riding surface.
                    // No inner vertical face remains to catch Sonic's collision probes.
                    if(r0.offset==0 && r1.offset==0) {
                        Triangle(vertices,normals,uv,indices,Point(0,0,0),Point(t0,innerRadius,0),Point(t1,innerRadius,0),Vector3.up,uvSize);
                        Triangle(vertices,normals,uv,indices,Point(0,0,-baseDepth),Point(t1,innerRadius,-baseDepth),Point(t0,innerRadius,-baseDepth),Vector3.down,uvSize);
                    } else {
                        Quad(vertices,normals,uv,indices,At(r0,0,0),At(r0,innerRadius,0),At(r1,innerRadius,0),At(r1,0,0),Vector3.up,uvSize);
                        Quad(vertices,normals,uv,indices,At(r0,0,-baseDepth),At(r0,innerRadius,-baseDepth),At(r1,innerRadius,-baseDepth),At(r1,0,-baseDepth),Vector3.down,uvSize);
                        FillExtensionWall(vertices,normals,uv,indices,r0,r1,-outward);
                    }
                }
                Quad(vertices,normals,uv,indices,At(r0,OuterRadius,wallHeight*SideBlend(t0,out _)),At(r1,OuterRadius,wallHeight*SideBlend(t1,out _)),At(r1,OuterRadius,-baseDepth),At(r0,OuterRadius,-baseDepth),outward,uvSize);
            }
            for(int end=0;end<2;end++)
            {
                Row row=rows[end==0?0:rows.Count-1];float theta=row.theta;Vector3 n=end==0?Vector3.back:ExitForward;
                if(fillInterior)
                    Quad(vertices,normals,uv,indices,At(row,0,0),At(row,innerRadius,0),At(row,innerRadius,-baseDepth),At(row,0,-baseDepth),n,uvSize);
                for(int j=0;j<across;j++)
                {
                    Vector3 a=ProfilePoint(theta,j,flatSegments)+row.Shift(Sign),b=ProfilePoint(theta,j+1,flatSegments)+row.Shift(Sign),c=b,d=a;c.y=d.y=-baseDepth;
                    Quad(vertices,normals,uv,indices,a,b,c,d,n,uvSize);
                }
            }
            var mesh=new Mesh{name="Virage releve continu",indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }
        static void Triangle(List<Vector3> vertices,List<Vector3> normals,List<Vector2> uv,List<int> indices,Vector3 a,Vector3 b,Vector3 c,Vector3 normal,float uvSize)
        {
            int start=vertices.Count;vertices.AddRange(new[]{a,b,c});
            foreach(var p in new[]{a,b,c}){normals.Add(normal);uv.Add(new Vector2(p.x,p.z)/uvSize);}
            if(Vector3.Dot(Vector3.Cross(b-a,c-a),normal)>0)indices.AddRange(new[]{start,start+1,start+2});
            else indices.AddRange(new[]{start,start+2,start+1});
        }
        static void Quad(List<Vector3> vertices,List<Vector3> normals,List<Vector2> uv,List<int> indices,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal,float uvSize)
        {
            int start=vertices.Count;vertices.AddRange(new[]{a,b,c,d});for(int k=0;k<4;k++)normals.Add(normal);
            uv.AddRange(new[]{Vector2.zero,new Vector2(Vector3.Distance(a,b)/uvSize,0),new Vector2(Vector3.Distance(a,b)/uvSize,Vector3.Distance(b,c)/uvSize),new Vector2(0,Vector3.Distance(a,d)/uvSize)});
            if(Vector3.Dot(Vector3.Cross(b-a,c-a),normal)>0)indices.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            else indices.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});
        }
    }
}
