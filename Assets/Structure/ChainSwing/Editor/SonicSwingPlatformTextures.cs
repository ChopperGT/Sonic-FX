using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace SonicFX.Structures.Editor
{
    [InitializeOnLoad]
    public static class SonicSwingPlatformTextures
    {
        const string Folder="Assets/Structure/ChainSwing";
        const string PrefabPath="Assets/Structure/chain.prefab";
        const string Marker=Folder+"/Editor/Textures_Verified.txt";
        const string Reports="C:/Users/Lecle/Documents/Git/Sonic-FX/outputs/ChainSwing";
        static SonicSwingPlatformTextures(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null)return;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if(prefab==null || prefab.GetComponent<SonicSwingPlatform>()==null)return;
            EditorApplication.update-=Ready;
            if(!File.Exists(Marker))Apply();
        }
        [MenuItem("Tools/Sonic FX/Structures/Appliquer les textures GreenHill de chain")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null){Debug.LogWarning("Quitte Play et le mode Prefab pour appliquer les textures de chain.");return;}
            Directory.CreateDirectory(Reports);GameObject root=null;Mesh source=null,mapped=null;
            try {
                const string texturePath=Folder+"/Textures/Bois_Orange.png";
                if(!File.Exists(texturePath))throw new Exception("Texture Bois_Orange.png introuvable.");
                AssetDatabase.ImportAsset(texturePath);
                var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Bilinear;importer.mipmapEnabled=true;importer.maxTextureSize=1024;importer.alphaSource=TextureImporterAlphaSource.None;importer.SaveAndReimport();
                if(!AssetDatabase.IsValidFolder(Folder+"/Materials"))AssetDatabase.CreateFolder(Folder,"Materials");
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                var wood=Material("Bois_Orange",Color.white,0,.22f,texture);
                var ends=Material("Bois_Extremites",new Color(1,.82f,.48f),0,.18f,texture);
                var green=Material("Fixations_Vertes",new Color(.20f,.85f,.015f),.12f,.38f,null);
                var silver=Material("Metal_Argente",new Color(.78f,.87f,.94f),.45f,.48f,null);
                var gold=Material("Attache_Doree",new Color(1,.58f,.015f),.40f,.42f,null);
                // Low emission keeps the painted colors readable when the level lacks reflection probes.
                Glow(silver,new Color(.08f,.10f,.12f));Glow(gold,new Color(.13f,.065f,.001f));Glow(green,new Color(.015f,.055f,0));
                root=PrefabUtility.LoadPrefabContents(PrefabPath);var swing=root.GetComponent<SonicSwingPlatform>();
                if(swing==null)throw new Exception("Le balancement doit etre installe avant les textures.");
                var top=root.transform.Find("Attache");var chain=root.transform.Find("Maillons");var floor=root.transform.Find("floor");
                if(top==null || chain==null || floor==null)throw new Exception("Attache, maillons ou planche introuvables.");
                top.GetComponent<MeshRenderer>().sharedMaterial=gold;chain.GetComponent<MeshRenderer>().sharedMaterial=silver;
                var filter=floor.GetComponent<MeshFilter>();source=SonicSwingPlatformBuilder.ReadMesh(filter.sharedMesh);
                var counts=new int[4];mapped=MapFloor(source,counts);
                string meshPath=Folder+"/Chain_Planche_Texturee.asset";var asset=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(asset==null){AssetDatabase.CreateAsset(mapped,meshPath);asset=mapped;mapped=null;}
                else {EditorUtility.CopySerialized(mapped,asset);EditorUtility.SetDirty(asset);}
                filter.sharedMesh=asset;floor.GetComponent<MeshRenderer>().sharedMaterials=new[]{wood,ends,green,silver};
                foreach(var c in floor.GetComponents<MeshCollider>())c.sharedMesh=asset;
                if(asset.subMeshCount!=4 || wood.mainTexture!=texture)throw new Exception("Association des textures incorrecte.");
                for(int i=0;i<4;i++)if(counts[i]==0)throw new Exception("Partie non texturee : "+i);
                SonicSwingPlatformBuilder.Verify(root);
                swing.PlaceAtAngle(0,false);
                var serialized=new SerializedObject(swing);swing.Links.GetComponent<MeshFilter>().sharedMesh=(Mesh)serialized.FindProperty("restChainMesh").objectReferenceValue;
                if(PrefabUtility.SaveAsPrefabAsset(root,PrefabPath)==null)throw new Exception("Enregistrement du prefab impossible.");
                AssetDatabase.SaveAssets();SonicSwingPlatformBuilder.Render(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
                File.WriteAllText(Marker,"Textures installees : bois orange, fixations vertes, rivets et maillons argentes, attache doree.");AssetDatabase.ImportAsset(Marker);
                File.WriteAllText(Path.Combine(Reports,"textures-tests.txt"),"PASS\nGeometry and bounds unchanged; wood UVs along logs; all 4 material regions assigned; gold anchor and silver chain; swing, horizontal deck, length and player transport still pass.\nTriangles (wood/end/green/rivet): "+string.Join(" / ",counts)+"\n"+DateTime.Now.ToString("s"));
                Debug.Log("chain : textures GreenHill appliquees, balancement et collision conserves.");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Reports,"textures-tests.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally{if(root!=null)PrefabUtility.UnloadPrefabContents(root);if(source!=null)Object.DestroyImmediate(source);if(mapped!=null)Object.DestroyImmediate(mapped);}
        }
        static Material Material(string name,Color color,float metallic,float smoothness,Texture texture)
        {
            string path=Folder+"/Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){var shader=Shader.Find("Standard");if(shader==null)throw new Exception("Shader Standard introuvable.");mat=new Material(shader){name=name};AssetDatabase.CreateAsset(mat,path);}
            mat.color=color;mat.mainTexture=texture;mat.SetFloat("_Metallic",metallic);mat.SetFloat("_Glossiness",smoothness);EditorUtility.SetDirty(mat);return mat;
        }
        static void Glow(Material mat,Color color){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color);mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;}
        static Mesh MapFloor(Mesh source,int[] counts)
        {
            var input=source.vertices;var normals=source.normals;var verts=new List<Vector3>();var norm=new List<Vector3>();var uv=new List<Vector2>();var indices=new[]{new List<int>(),new List<int>(),new List<int>(),new List<int>()};
            foreach(var group in SonicSwingPlatformBuilder.Components(source)){
                var bounds=SonicSwingPlatformBuilder.BoundsOf(source,group);
                bool wood=bounds.size.x>source.bounds.size.x*.5f;
                bool rivet=!wood && Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z)<source.bounds.size.x*.03f;
                for(int i=0;i<group.Count;i+=3){
                    var face=Vector3.Cross(input[group[i+1]]-input[group[i]],input[group[i+2]]-input[group[i]]).normalized;
                    int slot=wood?(Mathf.Abs(face.x)>.8f?1:0):(rivet?3:2);
                    var coords=new Vector2[3];float minV=1,maxV=0;
                    for(int j=0;j<3;j++){
                        var p=input[group[i+j]];
                        if(slot==0){coords[j]=new Vector2((p.x-bounds.min.x)/bounds.size.x,Mathf.Atan2(p.y-bounds.center.y,p.z-bounds.center.z)/(2*Mathf.PI)+.5f);minV=Mathf.Min(minV,coords[j].y);maxV=Mathf.Max(maxV,coords[j].y);}
                        else coords[j]=new Vector2((p.z-bounds.min.z)/Mathf.Max(.001f,bounds.size.z),(p.y-bounds.min.y)/Mathf.Max(.001f,bounds.size.y));
                    }
                    for(int j=0;j<3;j++){
                        int src=group[i+j];var coord=coords[j];if(slot==0 && maxV-minV>.5f && coord.y<.5f)coord.y+=1;
                        indices[slot].Add(verts.Count);verts.Add(input[src]);norm.Add(normals.Length==input.Length?normals[src]:face);uv.Add(coord);
                    }
                    counts[slot]++;
                }
            }
            var mesh=new Mesh{name="Chain_Planche_Texturee"};mesh.SetVertices(verts);mesh.SetNormals(norm);mesh.SetUVs(0,uv);mesh.subMeshCount=4;
            for(int i=0;i<4;i++)mesh.SetTriangles(indices[i],i);mesh.RecalculateBounds();mesh.RecalculateTangents();
            if(Vector3.Distance(mesh.bounds.center,source.bounds.center)>.0001f || Vector3.Distance(mesh.bounds.size,source.bounds.size)>.0001f)throw new Exception("La geometrie de la planche a change.");
            if(verts.Count!=source.triangles.Length)throw new Exception("Des faces de la planche ont disparu.");
            return mesh;
        }
    }
}
