using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace SonicFX.Decor.Editor
{
    [InitializeOnLoad] public static class GreenHillExtraTextures
    {
        const string Root="Assets/Structure/décore";
        const string Output=Root+"/GreenHill";
        static readonly string Reports=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/GreenHillExtra");
        static readonly string[] Names={"small_rock","plant","fence","fence_End"};
        static readonly long[] Ids={3219433978443475866,46604309741045415,-8223828571603864074,-6506103069093853348};
        static readonly string[] Textures={"Roche_Violette","Feuillage_Vert","Bois_Dore","Bois_Dore"};
        static GreenHillExtraTextures(){EditorApplication.update+=Ready;}
        static void Ready(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;EditorApplication.update-=Ready;if(!File.Exists(Output+"/Textures_Decors_Supplementaires.txt"))Apply();}
        static Material MakeMaterial(string textureName,bool leaf)
        {
            string texturePath=Root+"/Textures/"+textureName+".png";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);if(texture==null)throw new Exception("Texture manquante : "+texturePath);
            var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.wrapMode=TextureWrapMode.Repeat;importer.mipmapEnabled=true;importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
            string path=Output+"/"+textureName+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader=Shader.Find(leaf?"SonicFX/Decor Foliage Two Sided":"Standard");if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Shader indisponible");
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}material.shader=shader;material.mainTexture=texture;material.color=Color.white;material.SetFloat("_Glossiness",.08f);material.SetFloat("_Metallic",0);EditorUtility.SetDirty(material);return material;
        }
        [MenuItem("Tools/Sonic FX/Decor/Textures roche plante et barrieres")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;Directory.CreateDirectory(Reports);
            try {
                string fbx=AssetDatabase.GUIDToAssetPath("8659c38aa37038d4c8e5df514fda2eea");var imported=AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().ToArray();
                var materials=new Dictionary<string,Material>();foreach(string name in Textures.Distinct())materials[name]=MakeMaterial(name,name=="Feuillage_Vert");
                var details=new List<string>();
                for(int i=0;i<Names.Length;i++) {
                    var original=imported.FirstOrDefault(m=>{AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m,out string guid,out long id);return id==Ids[i];});if(original==null)throw new Exception("Modele manquant : "+Names[i]);
                    var mesh=Map(original,i);mesh.name=Names[i]+"_Texture_GreenHill";
                    string meshPath=Output+"/"+mesh.name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if(saved==null){saved=mesh;AssetDatabase.CreateAsset(saved,meshPath);}else{EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}
                    string path=Root+"/"+Names[i]+".prefab";var prefab=PrefabUtility.LoadPrefabContents(path);
                    try {
                        var position=prefab.transform.localPosition;var rotation=prefab.transform.localRotation;var scale=prefab.transform.localScale;
                        prefab.GetComponent<MeshFilter>().sharedMesh=saved;prefab.GetComponent<MeshRenderer>().sharedMaterials=new[]{materials[Textures[i]]};
                        if(prefab.transform.localPosition!=position||prefab.transform.localRotation!=rotation||prefab.transform.localScale!=scale)throw new Exception("Transform modifie");
                        PrefabUtility.SaveAsPrefabAsset(prefab,path);
                    }finally{PrefabUtility.UnloadPrefabContents(prefab);}
                    details.Add(Names[i]+": "+original.vertexCount+" vertices source; "+saved.triangles.Length/3+" triangles; bounds "+saved.bounds+"; texture "+Textures[i]);
                }
                AssetDatabase.SaveAssets();FenceRopeTextures.Apply();Preview();
                File.WriteAllText(Output+"/Textures_Decors_Supplementaires.txt","Textures appliquees aux quatre prefabs. Geometrie et transforms conserves.");AssetDatabase.ImportAsset(Output+"/Textures_Decors_Supplementaires.txt");
                File.WriteAllText(Path.Combine(Reports,"unity-report.txt"),"PASS\nTriangle positions, normals and bounds preserved; UVs complete and finite; four prefabs assigned; textures imported and preview rendered.\n"+string.Join("\n",details)+"\n"+DateTime.Now.ToString("s"));
            }catch(Exception e){File.WriteAllText(Path.Combine(Reports,"unity-report.txt"),"FAIL\n"+e);Debug.LogException(e);}
        }
        static Mesh Map(Mesh original,int kind)
        {
            var v=original.vertices;var n=original.normals;var triangles=original.triangles;var light=original.uv2;var colors=original.colors;
            int[] parent=Enumerable.Range(0,v.Length).ToArray();int Find(int a){while(parent[a]!=a){parent[a]=parent[parent[a]];a=parent[a];}return a;}
            void Join(int a,int b){parent[Find(a)]=Find(b);}
            var matching=new Dictionary<Vector3,int>();for(int i=0;i<v.Length;i++){if(matching.TryGetValue(v[i],out int first))Join(i,first);else matching[v[i]]=i;}
            for(int i=0;i<triangles.Length;i+=3){Join(triangles[i],triangles[i+1]);Join(triangles[i],triangles[i+2]);}
            var parts=new Dictionary<int,Bounds>();for(int i=0;i<v.Length;i++){int id=Find(i);if(parts.TryGetValue(id,out var b)){b.Encapsulate(v[i]);parts[id]=b;}else parts[id]=new Bounds(v[i],Vector3.zero);}
            Vector3[] vertices=new Vector3[triangles.Length],normals=new Vector3[triangles.Length];Vector2[] uv=new Vector2[triangles.Length],uv2=light.Length==v.Length?new Vector2[triangles.Length]:null;Color[] outColors=colors.Length==v.Length?new Color[triangles.Length]:null;
            for(int i=0;i<triangles.Length;i+=3) {
                Vector3 normal=Vector3.Cross(v[triangles[i+1]]-v[triangles[i]],v[triangles[i+2]]-v[triangles[i]]).normalized;Bounds part=parts[Find(triangles[i])];
                for(int j=0;j<3;j++) {
                    int dst=i+j,src=triangles[dst];Vector3 p=v[src];vertices[dst]=p;normals[dst]=n.Length==v.Length?n[src]:normal;
                    if(uv2!=null)uv2[dst]=light[src];if(outColors!=null)outColors[dst]=colors[src];
                    if(kind==1) {
                        var b=original.bounds;int horizontal=b.size.x>=b.size.z?0:2;
                        uv[dst]=new Vector2((p[horizontal]-b.min[horizontal])/Mathf.Max(.001f,b.size[horizontal]),(p.y-b.min.y)/Mathf.Max(.001f,b.size.y));
                    }else if(kind>=2) {
                        int longitudinal=part.size.y>=part.size.x&&part.size.y>=part.size.z?1:part.size.x>=part.size.z?0:2;
                        int a=(longitudinal+1)%3,b=(longitudinal+2)%3;
                        int across=Mathf.Abs(normal[a])>Mathf.Abs(normal[b])?b:a;
                        int lengthAxis=Mathf.Abs(normal[longitudinal])>.8f?(across==a?b:a):longitudinal;
                        uv[dst]=new Vector2((p[across]-part.min[across])/Mathf.Max(.001f,part.size[across]),(p[lengthAxis]-part.min[lengthAxis])/Mathf.Max(.001f,part.size[lengthAxis]));
                    }else {
                        var bounds=original.bounds;Vector3 q=p-bounds.min;float size=Mathf.Max(.001f,bounds.size.y);
                        uv[dst]=Mathf.Abs(normal.y)>.8f?new Vector2(q.x/size,q.z/size):Mathf.Abs(normal.x)>Mathf.Abs(normal.z)?new Vector2(q.z/size,q.y/size):new Vector2(q.x/size,q.y/size);
                    }
                    if(float.IsNaN(uv[dst].x)||float.IsInfinity(uv[dst].x)||float.IsNaN(uv[dst].y)||float.IsInfinity(uv[dst].y))throw new Exception("UV invalide");
                }
            }
            var mesh=new Mesh();mesh.indexFormat=vertices.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16;mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;if(uv2!=null)mesh.uv2=uv2;if(outColors!=null)mesh.colors=outColors;mesh.triangles=Enumerable.Range(0,vertices.Length).ToArray();mesh.RecalculateBounds();mesh.RecalculateTangents();
            if(Vector3.Distance(mesh.bounds.center,original.bounds.center)>.0001f||Vector3.Distance(mesh.bounds.size,original.bounds.size)>.0001f)throw new Exception("Bounds differents");
            var mapped=mesh.vertices;for(int i=0;i<triangles.Length;i++)if(mapped[i]!=v[triangles[i]])throw new Exception("Geometrie differente");return mesh;
        }
        static void Preview()
        {
            var preview=new PreviewRenderUtility();Texture2D image=null;var stage=new GameObject("Apercu decors");
            try {
                preview.AddSingleGO(stage);
                for(int i=0;i<Names.Length;i++) {
                    var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+Names[i]+".prefab"),stage.transform);go.transform.localPosition=Vector3.zero;go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one;
                    var r=go.GetComponent<Renderer>();var size=r.bounds.size;if(size.x<size.z)go.transform.localRotation=Quaternion.Euler(0,90,0);
                    go.transform.localScale=Vector3.one*(3/Mathf.Max(r.bounds.size.x,r.bounds.size.y,r.bounds.size.z));var b=r.bounds;go.transform.position=new Vector3((i-1.5f)*3.8f,0,0)-new Vector3(b.center.x,b.min.y,b.center.z);
                }
                preview.camera.orthographic=true;preview.camera.orthographicSize=3.7f;preview.camera.transform.position=new Vector3(0,5,-20);preview.camera.transform.LookAt(new Vector3(0,1.5f,0));preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=100;preview.camera.clearFlags=CameraClearFlags.Color;preview.camera.backgroundColor=new Color(.05f,.5f,.9f);preview.ambientColor=new Color(.65f,.65f,.65f);preview.lights[0].intensity=1.1f;preview.lights[0].transform.rotation=Quaternion.Euler(35,-30,0);preview.lights[1].intensity=.7f;
                preview.BeginStaticPreview(new Rect(0,0,1600,700));preview.Render();image=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(Reports,"Decors_Unity.png"),image.EncodeToPNG());
            }finally{if(image!=null)Object.DestroyImmediate(image);preview.Cleanup();}
        }
    }
}
