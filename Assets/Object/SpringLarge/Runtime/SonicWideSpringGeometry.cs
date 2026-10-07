using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SonicFX.Structures
{
    public static class SonicWideSpringGeometry
    {
        struct Point
        {
            public Vector3 p,n;public Vector2 uv;
            public static Point Lerp(Point a,Point b,float t)=>new Point{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t)};
        }
        public static Mesh Slice(Mesh source,float minimum,float maximum,Vector3 offset)
        {
            var vertices=source.vertices;var normals=source.normals;var tex=source.uv;
            var output=new List<Vector3>();var ns=new List<Vector3>();var uvs=new List<Vector2>();var sub=new List<int>[source.subMeshCount];
            List<Point> Clip(List<Point> before,float plane,bool keepGreater)
            {
                var after=new List<Point>();if(before.Count==0)return after;
                var a=before[before.Count-1];bool insideA=keepGreater?a.p.x>=plane:a.p.x<=plane;
                foreach(var b in before)
                {
                    bool insideB=keepGreater?b.p.x>=plane:b.p.x<=plane;
                    if(insideA!=insideB)after.Add(Point.Lerp(a,b,(plane-a.p.x)/(b.p.x-a.p.x)));
                    if(insideB)after.Add(b);a=b;insideA=insideB;
                }
                return after;
            }
            for(int s=0;s<sub.Length;s++)
            {
                sub[s]=new List<int>();var indices=source.GetTriangles(s);
                for(int i=0;i<indices.Length;i+=3)
                {
                    var poly=new List<Point>();for(int j=0;j<3;j++){int k=indices[i+j];poly.Add(new Point{p=vertices[k],n=normals[k],uv=tex[k]});}
                    poly=Clip(Clip(poly,minimum,true),maximum,false);
                    for(int j=1;j+1<poly.Count;j++)
                    {
                        if(Vector3.Cross(poly[j].p-poly[0].p,poly[j+1].p-poly[0].p).sqrMagnitude<1e-12f)continue;
                        foreach(int k in new[]{0,j,j+1}){sub[s].Add(output.Count);output.Add(poly[k].p+offset);ns.Add(poly[k].n);uvs.Add(poly[k].uv);}
                    }
                }
            }
            var mesh=new Mesh{name="Spring section",indexFormat=IndexFormat.UInt32};mesh.SetVertices(output);mesh.SetNormals(ns);mesh.SetUVs(0,uvs);mesh.subMeshCount=sub.Length;
            for(int s=0;s<sub.Length;s++)mesh.SetTriangles(sub[s],s);mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
        }
        public static Mesh Assemble(Mesh left,Mesh middle,Mesh right,Mesh single,int count)
        {
            count=Mathf.Clamp(count,1,SonicWideSpring.MaximumSlots);
            var pieces=new List<Mesh>();var shifts=new List<float>();float pitch=SonicWideSpring.Pitch;
            if(count==1){pieces.Add(single);shifts.Add(0);}
            else
            {
                pieces.Add(left);shifts.Add(-(count-3)*pitch*.5f);
                for(int i=1;i<count-1;i++){pieces.Add(middle);shifts.Add((i-(count-1)*.5f)*pitch);}
                pieces.Add(right);shifts.Add((count-3)*pitch*.5f);
            }
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new[]{new List<int>(),new List<int>()};
            for(int i=0;i<pieces.Count;i++)
            {
                var m=pieces[i];int start=vertices.Count;foreach(var p in m.vertices)vertices.Add(p+Vector3.right*shifts[i]);normals.AddRange(m.normals);uv.AddRange(m.uv);
                for(int s=0;s<2;s++)foreach(int index in m.GetTriangles(s))triangles[s].Add(start+index);
            }
            var mesh=new Mesh{name="Spring large",indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.subMeshCount=2;
            mesh.SetTriangles(triangles[0],0);mesh.SetTriangles(triangles[1],1);mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
        }
    }
}
