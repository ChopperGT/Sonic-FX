using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using Object=UnityEngine.Object;

namespace SonicFX.Structures.Editor
{
    [InitializeOnLoad]
    public static class SonicEditableCubeBuilder
    {
        public const string PrefabPath="Assets/Structure/Cube.prefab";
        public const string SourcePath="Assets/Structure/CubeEditable/Cube_Source.asset";
        public static readonly string Reports=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/CubeEditable");
        const string Version="SonicFX.CubeEditable.v2";
        static SonicEditableCubeBuilder(){EditorApplication.update+=Ready;}
        static void Ready(){
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null)return;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);if(prefab==null)return;
            EditorApplication.update-=Ready;if(SessionState.GetBool(Version,false))return;SessionState.SetBool(Version,true);
            var cube=prefab.GetComponent<SonicEditableCube>();if(cube==null || cube.SourceMesh==null)Install();else SonicEditableCubeVerification.Verify();
        }
        [MenuItem("Tools/Sonic FX/Structures/Installer et verifier Cube editable")]
        public static void Install(){
            if(EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null){Debug.LogWarning("Quitte Play et le mode Prefab avant d'installer le Cube editable.");return;}
            Directory.CreateDirectory(Reports);GameObject root=null;
            try{
                string backup=Path.Combine(Reports,"Cube-before-editing.prefab");if(!File.Exists(backup)){File.Copy(PrefabPath,backup);File.Copy(PrefabPath+".meta",backup+".meta");}
                root=PrefabUtility.LoadPrefabContents(PrefabPath);
                var transform=root.transform;var position=transform.localPosition;var rotation=transform.localRotation;var scale=transform.localScale;
                var renderer=root.GetComponent<MeshRenderer>();var materials=renderer.sharedMaterials;
                var cube=root.GetComponent<SonicEditableCube>();var source=cube!=null?cube.SourceMesh:null;
                if(source==null)source=AssetDatabase.LoadAssetAtPath<Mesh>(SourcePath);
                var pb=root.GetComponent<ProBuilderMesh>();
                if(source==null){
                    if(pb!=null){pb.MakeUnique();pb.ToMesh();pb.Refresh();}
                    var mesh=root.GetComponent<MeshFilter>().sharedMesh;if(mesh==null)throw new Exception("Geometrie du Cube introuvable.");
                    source=SonicEditableRampBuilder.CopyReadable(mesh);source.name="Cube_Source";AssetDatabase.CreateAsset(source,SourcePath);
                }
                // ProBuilder's editor can replace the MeshFilter independently of this
                // deformation tool. Freeze its geometry in a readable asset first.
                foreach(var component in root.GetComponents<MonoBehaviour>())
                    if(component!=null && component.GetType().FullName=="UnityEngine.ProBuilder.Shapes.ProBuilderShape")Object.DestroyImmediate(component);
                if(pb!=null)Object.DestroyImmediate(pb);
                if(cube==null)cube=root.AddComponent<SonicEditableCube>();
                if(cube.SourceMesh==null){cube.meshSubdivisions=3;cube.Initialize(source);}
                if(!cube.Rebuild())throw new Exception(cube.LastError);
                renderer.sharedMaterials=materials;
                if(transform.localPosition!=position || transform.localRotation!=rotation || transform.localScale!=scale)throw new Exception("La conversion a modifie le placement du prefab.");
                // Save a durable fallback; each enabled instance generates its own mesh.
                root.GetComponent<MeshFilter>().sharedMesh=source;foreach(var collider in root.GetComponents<MeshCollider>())collider.sharedMesh=source;
                if(PrefabUtility.SaveAsPrefabAsset(root,PrefabPath)==null)throw new Exception("Enregistrement du Cube impossible.");
                AssetDatabase.SaveAssetIfDirty(source);
                File.WriteAllText(Path.Combine(Reports,"installation.txt"),"INSTALLED\n"+DateTime.Now.ToString("s")+"\nPrefab identity, transform, material and source shape preserved; ProBuilder converted to independent editable cage.\n");
            }catch(Exception e){File.WriteAllText(Path.Combine(Reports,"installation.txt"),"FAIL\n"+e);Debug.LogException(e);return;}
            finally{if(root!=null)PrefabUtility.UnloadPrefabContents(root);}
            SonicEditableCubeVerification.Verify();
        }
    }
}
