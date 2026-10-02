using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace SonicFX.Decor.Editor
{
    [InitializeOnLoad] internal static class PalmLeafInstaller
    {
        internal const string Root="Assets/Structure/décore/Tree";
        internal const string Output=Root+"/Feuilles";
        internal static readonly string[] Names={"palm_A","palm_B","palm_C"};
        static PalmLeafInstaller(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            EditorApplication.update-=Ready;
            try{Install();}catch(Exception e){Debug.LogException(e);}
        }
        [MenuItem("Tools/Sonic FX/Decor/Installer les feuilles reactives")]
        internal static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(!AssetDatabase.IsValidFolder(Output))AssetDatabase.CreateFolder(Root,"Feuilles");
            foreach(string name in Names)
            {
                string path=Root+"/"+name+".prefab";
                var saved=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(saved==null)throw new InvalidOperationException("Palmier introuvable : "+path);
                var existing=saved.GetComponent<PalmLeafReaction>();
                if(existing!=null&&existing.motionData!=null&&existing.motionData.version==1&&saved.GetComponent<MeshFilter>().sharedMesh==existing.motionData.sourceMesh)continue;
                string report=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/PalmLeaves");
                Directory.CreateDirectory(report);
                string absolute=Path.Combine(Application.dataPath,"../"+path);
                string backup=Path.Combine(report,name+"-avant.prefab");
                if(!File.Exists(backup))File.Copy(absolute,backup);
                var prefab=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var filter=prefab.GetComponent<MeshFilter>();var original=filter.sharedMesh;
                    var uv=original.uv;var vertices=original.vertices;
                    var leafIndices=new HashSet<int>();var trunkIndices=new HashSet<int>();var leafTriangles=new List<int>();
                    var trunkTriangles=new List<int>();
                    foreach(var submesh in Enumerable.Range(0,original.subMeshCount))
                    {
                        var triangles=original.GetTriangles(submesh);
                        for(int i=0;i<triangles.Length;i+=3)
                        {
                            int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                            // The Green Hill atlas dedicates its upper half to leaf surfaces.
                            bool leaf=uv[a].y>.5f&&uv[b].y>.5f&&uv[c].y>.5f;
                            var indices=leaf?leafIndices:trunkIndices;
                            indices.Add(a);indices.Add(b);indices.Add(c);
                            if(!leaf){trunkTriangles.Add(a);trunkTriangles.Add(b);trunkTriangles.Add(c);}
                            else{leafTriangles.Add(a);leafTriangles.Add(b);leafTriangles.Add(c);}
                        }
                    }
                    if(leafIndices.Count==0||trunkTriangles.Count==0||leafIndices.Overlaps(trunkIndices))
                        throw new InvalidOperationException(name+" : separation feuilles/tronc ambigue.");
                    var source=CopyReadableMesh(original,name+"_Feuilles_Source");
                    var trunk=BuildCollisionMesh(vertices,trunkTriangles,name+"_Collision_Tronc");
                    source=SaveMesh(source,Output+"/"+name+"_Source.asset");
                    trunk=SaveMesh(trunk,Output+"/"+name+"_Tronc.asset");
                    var profile=AssetDatabase.LoadAssetAtPath<PalmLeafMotionData>(Output+"/"+name+"_Feuilles.asset");
                    if(profile==null)
                    {
                        profile=ScriptableObject.CreateInstance<PalmLeafMotionData>();
                        AssetDatabase.CreateAsset(profile,Output+"/"+name+"_Feuilles.asset");
                    }
                    profile.sourceMesh=source;profile.trunkCollisionMesh=trunk;
                    var ordered=leafIndices.OrderBy(value=>value).ToArray();
                    var bounds=new Bounds(vertices[ordered[0]],Vector3.zero);
                    foreach(int vertex in ordered)bounds.Encapsulate(vertices[vertex]);
                    profile.leafBounds=bounds;
                    profile.crownCenter=new Vector3(trunk.bounds.center.x,trunk.bounds.max.y,trunk.bounds.center.z);
                    profile.leafVertices=ordered;
                    profile.bendWeights=FrondWeights(vertices,ordered,leafTriangles,profile.crownCenter);
                    profile.version=1;
                    EditorUtility.SetDirty(profile);
                    filter.sharedMesh=source;
                    var collider=prefab.GetComponent<MeshCollider>();
                    if(collider!=null)collider.sharedMesh=trunk;
                    var reaction=prefab.GetComponent<PalmLeafReaction>();
                    if(reaction==null)reaction=prefab.AddComponent<PalmLeafReaction>();
                    reaction.motionData=profile;
                    var flags=GameObjectUtility.GetStaticEditorFlags(prefab);
                    GameObjectUtility.SetStaticEditorFlags(prefab,flags&~StaticEditorFlags.BatchingStatic);
                    if(PrefabUtility.SaveAsPrefabAsset(prefab,path)==null)throw new IOException("Prefab non enregistre : "+name);
                }
                finally{PrefabUtility.UnloadPrefabContents(prefab);}
            }
            AssetDatabase.SaveAssets();
        }
        static Mesh CopyReadableMesh(Mesh source,string name)
        {
            var mesh=new Mesh{name=name,indexFormat=source.indexFormat,vertices=source.vertices,normals=source.normals,
                tangents=source.tangents,colors32=source.colors32,uv=source.uv,uv2=source.uv2,uv3=source.uv3,uv4=source.uv4};
            mesh.subMeshCount=source.subMeshCount;
            for(int i=0;i<source.subMeshCount;i++)mesh.SetTriangles(source.GetTriangles(i),i,false);
            mesh.bounds=source.bounds;return mesh;
        }
        static float[] FrondWeights(Vector3[] vertices,int[] ordered,List<int> triangles,Vector3 crown)
        {
            // Weld only for grouping: the render mesh and its UV seams remain untouched.
            var parent=ordered.ToDictionary(value=>value,value=>value);
            int Find(int value){while(parent[value]!=value){parent[value]=parent[parent[value]];value=parent[value];}return value;}
            void Join(int a,int b){a=Find(a);b=Find(b);if(a!=b)parent[b]=a;}
            var positions=new Dictionary<Vector3,int>();
            foreach(int vertex in ordered)
            {
                if(positions.TryGetValue(vertices[vertex],out int same))Join(vertex,same);
                else positions.Add(vertices[vertex],vertex);
            }
            for(int i=0;i<triangles.Count;i+=3){Join(triangles[i],triangles[i+1]);Join(triangles[i],triangles[i+2]);}
            var weights=new Dictionary<int,float>();
            foreach(var group in ordered.GroupBy(Find))
            {
                int attachment=group.OrderBy(vertex=>(vertices[vertex]-crown).sqrMagnitude).First();
                Vector3 root=vertices[attachment];float length=group.Max(vertex=>Vector3.Distance(vertices[vertex],root));
                foreach(int vertex in group)
                    weights[vertex]=Mathf.SmoothStep(0,1,Mathf.InverseLerp(length*.12f,Mathf.Max(.001f,length),Vector3.Distance(vertices[vertex],root)));
            }
            return ordered.Select(vertex=>weights[vertex]).ToArray();
        }
        static Mesh BuildCollisionMesh(Vector3[] vertices,List<int> triangles,string name)
        {
            var remap=new Dictionary<int,int>();var compact=new List<Vector3>();var indices=new int[triangles.Count];
            for(int i=0;i<triangles.Count;i++)
            {
                int original=triangles[i];
                if(!remap.TryGetValue(original,out int mapped)){mapped=compact.Count;remap.Add(original,mapped);compact.Add(vertices[original]);}
                indices[i]=mapped;
            }
            var mesh=new Mesh{name=name};mesh.SetVertices(compact);mesh.triangles=indices;mesh.RecalculateBounds();return mesh;
        }
        static Mesh SaveMesh(Mesh mesh,string path)
        {
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,saved);EditorUtility.SetDirty(saved);Object.DestroyImmediate(mesh);return saved;
        }
    }
}
