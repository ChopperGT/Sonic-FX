using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SonicFX.Decor.Editor
{
    [InitializeOnLoad]
    public static class GreenHillPalmTextures
    {
        const string Root="Assets/Structure/décore/Tree";
        const string Output=Root+"/GreenHill";
        static readonly string Reports=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/GreenHill_Palms");
        static readonly string[] Names={"palm_A","palm_B","palm_C"};
        static readonly long[] MeshIds={7441698055141181763,2311249197989016218,-878771243359391196};
        [Serializable] class Mapping { public Entry[] entries; }
        [Serializable] class Entry { public Vector2 source,target; }
        static bool busy;
        static GreenHillPalmTextures() { EditorApplication.update+=ApplyWhenReady; }
        static void ApplyWhenReady()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)return;
            EditorApplication.update-=ApplyWhenReady;
            if(!File.Exists(Output+"/Textures_appliquees.txt")) Apply();
        }
        [MenuItem("Tools/Sonic FX/Decor/Appliquer les textures des palmiers")]
        public static void Apply()
        {
            if(busy || EditorApplication.isPlayingOrWillChangePlaymode)return;
            busy=true;Directory.CreateDirectory(Reports);
            var clones=new List<Mesh>();
            try
            {
                string fbx=AssetDatabase.GUIDToAssetPath("8659c38aa37038d4c8e5df514fda2eea");
                var imported=AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().ToArray();
                // Prepare all three UV copies before updating any prefab.
                for(int tree=0;tree<3;tree++)
                {
                    var original=imported.FirstOrDefault(m=>{AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m,out string guid,out long id);return id==MeshIds[tree];});
                    if(original==null)throw new InvalidOperationException("Maillage introuvable : "+Names[tree]);
                    var mapping=JsonUtility.FromJson<Mapping>(File.ReadAllText(Root+"/Editor/"+Names[tree]+"_UV.json"));
                    var lookup=new Dictionary<Vector2,Vector2>();foreach(var e in mapping.entries)lookup[e.source]=e.target;
                    var mesh=UnityEngine.Object.Instantiate(original);mesh.name=Names[tree]+"_GreenHill";clones.Add(mesh);
                    var uv=mesh.uv;
                    for(int vertex=0;vertex<uv.Length;vertex++)
                    {
                        Vector2 old=uv[vertex];
                        if(lookup.TryGetValue(old,out var mapped))uv[vertex]=mapped;
                        else
                        {
                            var closest=mapping.entries.OrderBy(e=>(e.source-old).sqrMagnitude).First();
                            if((closest.source-old).sqrMagnitude>1e-10f)throw new InvalidOperationException(Names[tree]+" : UV non reconnu "+old);
                            uv[vertex]=closest.target;
                        }
                    }
                    mesh.uv=uv;mesh.RecalculateTangents();
                    if(!mesh.vertices.SequenceEqual(original.vertices) || !mesh.triangles.SequenceEqual(original.triangles) || !mesh.normals.SequenceEqual(original.normals) || mesh.bounds!=original.bounds)
                        throw new InvalidOperationException("La geometrie a change : "+Names[tree]);
                }
                string texturePath=Root+"/Textures/Palmiers_GreenHill_Atlas.png";
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if(texture==null)throw new InvalidOperationException("Atlas des palmiers manquant.");
                var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=true;importer.maxTextureSize=2048;importer.sRGBTexture=true;importer.SaveAndReimport();
                if(!AssetDatabase.IsValidFolder(Output))AssetDatabase.CreateFolder(Root,"GreenHill");
                var shader=Shader.Find("Standard");
                if(shader==null || ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Shader Standard invalide.");
                var material=AssetDatabase.LoadAssetAtPath<Material>(Output+"/Palmiers_GreenHill.mat");
                if(material==null){material=new Material(shader){name="Palmiers_GreenHill"};AssetDatabase.CreateAsset(material,Output+"/Palmiers_GreenHill.mat");}
                material.mainTexture=texture;material.color=Color.white;material.SetFloat("_Metallic",0);material.SetFloat("_Glossiness",.12f);
                material.SetFloat("_SpecularHighlights",0);material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");EditorUtility.SetDirty(material);
                for(int tree=0;tree<3;tree++)
                {
                    string meshPath=Output+"/"+Names[tree]+"_GreenHill.asset";
                    Mesh saved=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if(saved==null){saved=clones[tree];AssetDatabase.CreateAsset(saved,meshPath);}
                    else{EditorUtility.CopySerialized(clones[tree],saved);EditorUtility.SetDirty(saved);}
                    string path=Root+"/"+Names[tree]+".prefab";
                    var prefab=PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        prefab.GetComponent<MeshFilter>().sharedMesh=saved;
                        prefab.GetComponent<MeshRenderer>().sharedMaterials=new[]{material};
                        PrefabUtility.SaveAsPrefabAsset(prefab,path);
                    }
                    finally{PrefabUtility.UnloadPrefabContents(prefab);}
                }
                AssetDatabase.SaveAssets();
                RenderPreview();
                File.WriteAllText(Output+"/Textures_appliquees.txt","Palmiers Green Hill : atlas commun et UV adaptes. Geometrie et transforms preserves.");
                AssetDatabase.ImportAsset(Output+"/Textures_appliquees.txt");
                File.WriteAllText(Path.Combine(Reports,"unity-report.txt"),"PASS\nThree prefab meshes retain their original vertices, triangles, normals and bounds. All UVs mapped. Shared atlas and Standard material assigned. Preview rendered.\n"+string.Join("\n",Names.Select(n=>Root+"/"+n+".prefab")));
                Debug.Log("Textures Green Hill appliquees aux trois palmiers.");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Reports,"unity-report.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally
            {
                foreach(var mesh in clones)if(mesh!=null && !AssetDatabase.Contains(mesh))UnityEngine.Object.DestroyImmediate(mesh);
                busy=false;
            }
        }
        [MenuItem("Tools/Sonic FX/Decor/Apercu des palmiers")]
        public static void RenderPreview()
        {
            Directory.CreateDirectory(Reports);
            var preview=new PreviewRenderUtility();Texture2D image=null;var stage=new GameObject("Palmiers Green Hill");
            try
            {
                preview.AddSingleGO(stage);
                float maxWidth=0;
                var trees=new List<GameObject>();
                foreach(string name in Names)
                {
                    var tree=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+name+".prefab"),stage.transform);
                    tree.transform.localPosition=Vector3.zero;tree.transform.localRotation=Quaternion.identity;
                    maxWidth=Mathf.Max(maxWidth,tree.GetComponent<Renderer>().bounds.size.x);trees.Add(tree);
                }
                var total=new Bounds();
                for(int i=0;i<trees.Count;i++)
                {
                    var renderer=trees[i].GetComponent<Renderer>();var bounds=renderer.bounds;
                    trees[i].transform.position+=new Vector3((i-1)*maxWidth*1.1f-bounds.center.x,-bounds.min.y,-bounds.center.z);
                    if(i==0)total=renderer.bounds;else total.Encapsulate(renderer.bounds);
                }
                Vector3 center=total.center;
                float size=Mathf.Max(total.size.y,total.size.x/1.5f)*.65f;
                preview.camera.orthographic=true;preview.camera.orthographicSize=size;
                preview.camera.transform.position=center+new Vector3(0,size*.22f,-size*4);preview.camera.transform.LookAt(center);
                preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=size*12;
                preview.camera.clearFlags=CameraClearFlags.Color;preview.camera.backgroundColor=new Color(.08f,.52f,.88f);
                preview.ambientColor=new Color(.65f,.65f,.65f);
                preview.lights[0].intensity=1.1f;preview.lights[0].transform.rotation=Quaternion.Euler(40,-30,0);preview.lights[1].intensity=.6f;
                preview.BeginStaticPreview(new Rect(0,0,1500,1000));preview.camera.Render();image=preview.EndStaticPreview();
                File.WriteAllBytes(Path.Combine(Reports,"Palmiers_Unity.png"),image.EncodeToPNG());
            }
            finally {if(image!=null)UnityEngine.Object.DestroyImmediate(image);preview.Cleanup();}
        }
    }
}
