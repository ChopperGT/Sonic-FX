using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SonicFX.Structures
{
    public static class SonicRampMeshSubdivision
    {
        // Split BEFORE applying the control cage. Splitting the deformed mesh would
        // only add triangles to the same flat facets and would not improve the curve.
        public static Mesh Create(Mesh source,int levels)
        {
            levels=Mathf.Clamp(levels,0,4);
            var vertices=new List<Vector3>(source.vertices);
            var normals=new List<Vector3>(source.normals);
            var tangents=new List<Vector4>(source.tangents);
            var colors=new List<Color>(source.colors);
            bool hasNormals=normals.Count==vertices.Count,hasTangents=tangents.Count==vertices.Count,hasColors=colors.Count==vertices.Count;
            var uv=new List<Vector4>[8];for(int channel=0;channel<8;channel++){uv[channel]=new List<Vector4>();source.GetUVs(channel,uv[channel]);}
            var triangles=new int[source.subMeshCount][];for(int s=0;s<triangles.Length;s++)triangles[s]=source.GetTriangles(s);
            for(int level=0;level<levels;level++){
                // Shared edges use one midpoint. Existing UV/normal seams retain their
                // separate vertex copies, with identical positions on both sides.
                var edges=new Dictionary<ulong,int>();
                int Midpoint(int a,int b){
                    uint low=(uint)Mathf.Min(a,b),high=(uint)Mathf.Max(a,b);ulong key=((ulong)low<<32)|high;
                    if(edges.TryGetValue(key,out int index))return index;
                    index=vertices.Count;edges.Add(key,index);vertices.Add((vertices[a]+vertices[b])*.5f);
                    if(hasNormals)normals.Add((normals[a]+normals[b]).normalized);
                    if(hasTangents){var t=((Vector3)tangents[a]+(Vector3)tangents[b]).normalized;tangents.Add(new Vector4(t.x,t.y,t.z,tangents[a].w));}
                    if(hasColors)colors.Add((colors[a]+colors[b])*.5f);
                    for(int channel=0;channel<8;channel++)if(uv[channel].Count>0)uv[channel].Add((uv[channel][a]+uv[channel][b])*.5f);
                    return index;
                }
                for(int s=0;s<triangles.Length;s++){
                    var before=triangles[s];var after=new int[before.Length*4];int output=0;
                    void Triangle(int a,int b,int c){after[output++]=a;after[output++]=b;after[output++]=c;}
                    for(int i=0;i<before.Length;i+=3){
                        int a=before[i],b=before[i+1],c=before[i+2];int ab=Midpoint(a,b),bc=Midpoint(b,c),ca=Midpoint(c,a);
                        Triangle(a,ab,ca);Triangle(ab,b,bc);Triangle(ca,bc,c);Triangle(ab,bc,ca);
                    }
                    triangles[s]=after;
                }
            }
            var mesh=new Mesh{name=source.name+" subdivise",hideFlags=HideFlags.DontSave,indexFormat=vertices.Count>65535?IndexFormat.UInt32:source.indexFormat};
            mesh.SetVertices(vertices);if(hasNormals)mesh.SetNormals(normals);if(hasTangents)mesh.SetTangents(tangents);if(hasColors)mesh.SetColors(colors);
            for(int channel=0;channel<8;channel++)if(uv[channel].Count>0)mesh.SetUVs(channel,uv[channel]);
            mesh.subMeshCount=triangles.Length;for(int s=0;s<triangles.Length;s++)mesh.SetTriangles(triangles[s],s);
            if(!hasNormals)mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
