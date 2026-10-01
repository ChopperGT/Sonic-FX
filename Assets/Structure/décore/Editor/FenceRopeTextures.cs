using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;
namespace SonicFX.Decor.Editor
{
    [InitializeOnLoad] public static class FenceRopeTextures
    {
        const string Root="Assets/Structure/décore";
        const string Output=Root+"/GreenHill";
        static readonly string Reports=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/FenceRope");
        static readonly string[] Names={"fence","fence_End"};
        static readonly long[] Ids={-8223828571603864074,-6506103069093853348};
        static FenceRopeTextures(){EditorApplication.update+=Ready;}
        static void Ready(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;EditorApplication.update-=Ready;if(!File.Exists(Output+"/Cordes_Barrieres.txt"))Apply();}
        [MenuItem("Tools/Sonic FX/Decor/Corriger les cordes des barrieres")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;Directory.CreateDirectory(Reports);
            try {
                var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Structure/GreenHill_Bridge/Bridge_GreenHill_Atlas.png");
                var wood=AssetDatabase.LoadAssetAtPath<Material>(Output+"/Bois_Dore.mat");if(atlas==null||wood==null)throw new Exception("Texture du pont ou bois manquant");
                string ropePath=Output+"/Corde_Beige.mat";var rope=AssetDatabase.LoadAssetAtPath<Material>(ropePath);
                if(rope==null){rope=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(rope,ropePath);}
                rope.mainTexture=atlas;rope.color=Color.white;rope.SetFloat("_Metallic",0);rope.SetFloat("_Glossiness",.08f);EditorUtility.SetDirty(rope);
                var meshes=AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath("8659c38aa37038d4c8e5df514fda2eea")).OfType<Mesh>().ToArray();var report=new List<string>();
                // Prepare both meshes before updating the prefabs.
                var prepared=new Mesh[2];
                for(int i=0;i<2;i++){
                    var source=meshes.First(m=>{AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m,out string guid,out long id);return id==Ids[i];});
                    prepared[i]=Map(source,report);prepared[i].name=Names[i]+"_Bois_Corde";
                }
                for(int i=0;i<2;i++) {
                    string meshPath=Output+"/"+prepared[i].name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if(saved==null){saved=prepared[i];AssetDatabase.CreateAsset(saved,meshPath);}else{EditorUtility.CopySerialized(prepared[i],saved);Object.DestroyImmediate(prepared[i]);EditorUtility.SetDirty(saved);}
                    string path=Root+"/"+Names[i]+".prefab";var prefab=PrefabUtility.LoadPrefabContents(path);
                    try{prefab.GetComponent<MeshFilter>().sharedMesh=saved;prefab.GetComponent<MeshRenderer>().sharedMaterials=new[]{wood,rope};PrefabUtility.SaveAsPrefabAsset(prefab,path);}finally{PrefabUtility.UnloadPrefabContents(prefab);}
                }
                AssetDatabase.SaveAssets();Preview();File.WriteAllText(Output+"/Cordes_Barrieres.txt","Bois pour les poteaux ; corde pour les liens et la traversee.");AssetDatabase.ImportAsset(Output+"/Cordes_Barrieres.txt");
                File.WriteAllText(Path.Combine(Reports,"unity-report.txt"),"PASS\nOriginal geometry preserved; wood/rope submeshes; bounded rope atlas UVs; both prefabs updated.\n"+string.Join("\n",report)+"\n"+DateTime.Now.ToString("s"));
            }catch(Exception e){File.WriteAllText(Path.Combine(Reports,"unity-report.txt"),"FAIL\n"+e);Debug.LogException(e);}
        }
        static Mesh Map(Mesh source,List<string> report)
        {
            var vertices=source.vertices;var normals=source.normals;var triangles=source.triangles;
            int[] parents=Enumerable.Range(0,vertices.Length).ToArray();int Find(int a){while(parents[a]!=a){parents[a]=parents[parents[a]];a=parents[a];}return a;}void Join(int a,int b){parents[Find(a)]=Find(b);}
            var matching=new Dictionary<Vector3,int>();for(int i=0;i<vertices.Length;i++){if(matching.TryGetValue(vertices[i],out int other))Join(i,other);else matching[vertices[i]]=i;}
            for(int i=0;i<triangles.Length;i+=3){Join(triangles[i],triangles[i+1]);Join(triangles[i],triangles[i+2]);}
            var bounds=new Dictionary<int,Bounds>();
            for(int i=0;i<vertices.Length;i++){int id=Find(i);if(!bounds.TryGetValue(id,out var b))b=new Bounds(vertices[i],Vector3.zero);b.Encapsulate(vertices[i]);bounds[id]=b;}
            float maxHeight=bounds.Values.Max(b=>b.size.y);
            var ropeIds=new HashSet<int>();foreach(var pair in bounds){bool rope=pair.Value.size.y<maxHeight*.8f; if(rope)ropeIds.Add(pair.Key);report.Add(source.name+" part="+pair.Key+" "+(rope?"ROPE":"WOOD")+" "+pair.Value);}
            File.WriteAllText(Path.Combine(Reports,"parts.txt"),string.Join("\n",report));
            var woodIndices=new List<int>();var ropeIndices=new List<int>();var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();
            for(int i=0;i<triangles.Length;i+=3) {
                int id=Find(triangles[i]);var b=bounds[id];
                // The imported posts and their rope wrappings are welded together.
                // These are the two boundary loops of the wrapping in the source meshes.
                const float wrapBottom=12.712f, wrapTop=19.889f;
                bool wrapping=true;
                for(int j=0;j<3;j++) {
                    float y=vertices[triangles[i+j]].y;
                    if(y<wrapBottom-.002f||y>wrapTop+.002f)wrapping=false;
                }
                bool rope=ropeIds.Contains(id)||wrapping;
                if(wrapping)b=new Bounds(new Vector3(b.center.x,(wrapBottom+wrapTop)*.5f,b.center.z),new Vector3(b.size.x,wrapTop-wrapBottom,b.size.z));
                Vector3 face=Vector3.Cross(vertices[triangles[i+1]]-vertices[triangles[i]],vertices[triangles[i+2]]-vertices[triangles[i]]).normalized;
                int axis=b.size.y>=b.size.x&&b.size.y>=b.size.z?1:b.size.x>=b.size.z?0:2;
                // Rings wrap around the vertical post; long horizontal rope follows X.
                for(int j=0;j<3;j++) {
                    int src=triangles[i+j];Vector3 p=vertices[src];Vector2 coord;
                    if(rope) {
                        bool ring=b.size.x<b.size.z*2 && b.size.z<b.size.x*2;
                        if(ring) {
                            float angle=Mathf.Atan2(p.z-b.center.z,p.x-b.center.x)/(2*Mathf.PI)+.5f;
                            // Keep each ring within the rope-only quadrant of the existing atlas.
                            coord=new Vector2(angle,(p.y-b.min.y)/Mathf.Max(.001f,b.size.y));
                        } else {
                            int a=(axis+1)%3,c=(axis+2)%3;
                            float angle=Mathf.Atan2(p[c]-b.center[c],p[a]-b.center[a])/(2*Mathf.PI)+.5f;
                            coord=new Vector2((p[axis]-b.min[axis])/Mathf.Max(.001f,b.size[axis]),angle);
                        }
                        coord=new Vector2(.025f+Mathf.Clamp01(coord.x)*.45f,.025f+Mathf.Clamp01(coord.y)*.45f);
                    }else {
                        int a=(axis+1)%3,c=(axis+2)%3;int across=Mathf.Abs(face[a])>Mathf.Abs(face[c])?c:a;
                        int length=Mathf.Abs(face[axis])>.8f?(across==a?c:a):axis;
                        coord=new Vector2((p[across]-b.min[across])/Mathf.Max(.001f,b.size[across]),(p[length]-b.min[length])/Mathf.Max(.001f,b.size[length]));
                    }
                    (rope?ropeIndices:woodIndices).Add(v.Count);v.Add(p);n.Add(normals.Length==vertices.Length?normals[src]:face);uv.Add(coord);
                }
            }
            if(woodIndices.Count==0||ropeIndices.Count==0)throw new Exception("Separation bois/corde impossible pour "+source.name);
            var mesh=new Mesh();mesh.vertices=v.ToArray();mesh.normals=n.ToArray();mesh.uv=uv.ToArray();mesh.subMeshCount=2;mesh.SetTriangles(woodIndices,0);mesh.SetTriangles(ropeIndices,1);mesh.RecalculateBounds();mesh.RecalculateTangents();
            if((mesh.bounds.center-source.bounds.center).sqrMagnitude>.000001f||(mesh.bounds.size-source.bounds.size).sqrMagnitude>.000001f)throw new Exception("Bounds modifies");
            for(int i=0;i<triangles.Length;i++)if(v[i]!=vertices[triangles[i]])throw new Exception("Geometrie modifiee");report.Add(source.name+" wood triangles="+woodIndices.Count/3+" rope triangles="+ropeIndices.Count/3);return mesh;
        }
        static void Preview()
        {
            var preview=new PreviewRenderUtility();Texture2D image=null;var stage=new GameObject("Barriere bois et corde");
            try {
                preview.AddSingleGO(stage);Bounds total=default;
                for(int i=0;i<2;i++){
                    var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+Names[i]+".prefab"),stage.transform);go.transform.localPosition=Vector3.zero;go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one;
                    var renderer=go.GetComponent<Renderer>();var b=renderer.bounds;go.transform.localPosition=new Vector3(i==0?-18:30,0,0)-new Vector3(b.center.x,b.min.y,b.center.z);if(i==0)total=renderer.bounds;else total.Encapsulate(renderer.bounds);
                }
                var center=total.center;preview.camera.orthographic=true;preview.camera.orthographicSize=27;preview.camera.transform.position=center+new Vector3(18,20,-150);preview.camera.transform.LookAt(center);preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=500;preview.camera.clearFlags=CameraClearFlags.Color;preview.camera.backgroundColor=new Color(.09f,.2f,.28f);preview.ambientColor=new Color(.65f,.65f,.65f);preview.lights[0].intensity=1.2f;preview.lights[0].transform.rotation=Quaternion.Euler(35,-30,0);preview.lights[1].intensity=.8f;
                preview.BeginStaticPreview(new Rect(0,0,1500,850));preview.Render();image=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(Reports,"Barrieres_Corde.png"),image.EncodeToPNG());
            }finally{if(image!=null)Object.DestroyImmediate(image);preview.Cleanup();}
        }
    }
}
