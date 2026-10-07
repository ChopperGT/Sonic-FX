using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace SonicFX.Magma.Editor
{
    [InitializeOnLoad]
    public static class MagmaRockBuilder
    {
        public const string Folder="Assets/Structure/PierresMagma";
        public static readonly string[] Names={"Pierre_Magma_Petite","Pierre_Magma_Moyenne","Pierre_Magma_Grande","Pierre_Magma_Allongee","Pierre_Magma_Pointue","Pierre_Magma_Plate"};
        static readonly Vector3[] Sizes={new Vector3(.9f,.7f,.85f),new Vector3(1.6f,1.25f,1.4f),new Vector3(2.8f,2.2f,2.5f),new Vector3(3.4f,.9f,1.35f),new Vector3(1.4f,2.8f,1.35f),new Vector3(2.3f,.5f,2)};
        static MagmaRockBuilder(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || Shader.Find("Sonic FX/Pierre magma")==null)return;
            EditorApplication.update-=Ready;Build();
        }
        [MenuItem("Sonic FX/Lave/Creer ou verifier les pierres de magma")]
        public static void Build()
        {
            var shader=Shader.Find("Sonic FX/Pierre magma");
            if(shader==null)throw new InvalidOperationException("Shader de pierre magma manquant.");
            if(ShaderUtil.ShaderHasError(shader))
            {
                string details="";foreach(var m in ShaderUtil.GetShaderMessages(shader))details+="\n"+m.line+": "+m.message;
                throw new InvalidOperationException("Shader de pierre magma invalide."+details);
            }
            var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Magma.mat");
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,Folder+"/Magma.mat");}
            for(int i=0;i<Names.Length;i++)
            {
                string path=Folder+"/"+Names[i]+".prefab";if(File.Exists(path))continue;
                string meshPath=Folder+"/Meshes/"+Names[i]+".asset";
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(mesh==null){mesh=CreateMesh(i);AssetDatabase.CreateAsset(mesh,meshPath);}
                var go=new GameObject(Names[i]);
                try
                {
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
                    var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.convex=true;
                    go.AddComponent<SonicMagmaRock>().Refresh();PrefabUtility.SaveAsPrefabAsset(go,path);
                }
                finally{Object.DestroyImmediate(go);}
            }
            AssetDatabase.SaveAssets();
        }
        static Mesh CreateMesh(int variant)
        {
            const int segments=10, rings=4;
            var rng=new System.Random(731+variant*317);var grid=new Vector3[rings,segments];Vector3 size=Sizes[variant];
            float[] heights={.03f,.28f,.65f,.9f};float[] radii={.7f,1,.88f,.5f};
            for(int r=0;r<rings;r++)for(int s=0;s<segments;s++)
            {
                float angle=(s+(r%2)*.15f)*Mathf.PI*2/segments;
                float radius=radii[r]*(.87f+.26f*(float)rng.NextDouble());
                if(variant==4 && r>=2)radius*=r==2?.65f:.25f;
                float y=heights[r]*size.y+((float)rng.NextDouble()-.5f)*size.y*.055f;
                grid[r,s]=new Vector3(Mathf.Cos(angle)*radius*size.x*.5f,y,Mathf.Sin(angle)*radius*size.z*.5f);
            }
            var vertices=new List<Vector3>();var triangles=new List<int>();
            Action<Vector3,Vector3,Vector3> tri=(a,b,c)=>{int start=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);};
            for(int s=0;s<segments;s++)
            {
                int n=(s+1)%segments;tri(Vector3.zero,grid[0,s],grid[0,n]);
                for(int r=0;r<rings-1;r++){tri(grid[r,s],grid[r+1,s],grid[r,n]);tri(grid[r,n],grid[r+1,s],grid[r+1,n]);}
                tri(new Vector3(size.x*.035f,size.y,size.z*.025f),grid[rings-1,n],grid[rings-1,s]);
            }
            var mesh=new Mesh{name=Names[variant]};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
