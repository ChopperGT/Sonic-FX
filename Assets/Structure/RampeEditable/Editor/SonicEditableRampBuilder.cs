using System;
using System.IO;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace SonicFX.Structures.Editor
{
    [InitializeOnLoad]
    public static class SonicEditableRampBuilder
    {
        const string PrefabPath="Assets/Structure/ramp_C.prefab";
        const string Folder="Assets/Structure/RampeEditable";
        const string SourcePath=Folder+"/Ramp_C_Source.asset";
        static string Reports=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/RampEditable");
        static SonicEditableRampBuilder(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || PrefabStageUtility.GetCurrentPrefabStage()!=null)return;
            EditorApplication.update-=Ready;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if(prefab!=null && (prefab.GetComponent<SonicEditableRamp>()==null || !File.Exists(Folder+"/Editor/VerifiedV3.txt")))Install();
        }
        [MenuItem("Tools/Sonic FX/Structures/Installer et verifier ramp_C editable")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null){Debug.LogWarning("Quitte Play et le mode Prefab avant d'installer ramp_C editable.");return;}
            Directory.CreateDirectory(Reports);GameObject root=null;
            try {
                root=PrefabUtility.LoadPrefabContents(PrefabPath);
                var filter=root.GetComponent<MeshFilter>();
                if(filter==null || filter.sharedMesh==null)throw new Exception("Geometrie de ramp_C introuvable.");
                var ramp=root.GetComponent<SonicEditableRamp>();
                var source=ramp!=null?ramp.SourceMesh:null;
                if(source==null)source=AssetDatabase.LoadAssetAtPath<Mesh>(SourcePath);
                if(source==null){source=CopyReadable(filter.sharedMesh);AssetDatabase.CreateAsset(source,SourcePath);}
                if(ramp==null)ramp=root.AddComponent<SonicEditableRamp>();
                if(ramp.SourceMesh==null)ramp.Initialize(source);
                Verify(source);
                if(!ramp.Rebuild())throw new Exception(ramp.LastError);
                // The source is the serialized fallback; each instance regenerates its own mesh.
                filter.sharedMesh=source;root.GetComponent<MeshCollider>().sharedMesh=source;
                if(PrefabUtility.SaveAsPrefabAsset(root,PrefabPath)==null)throw new Exception("Enregistrement du prefab impossible.");
                AssetDatabase.SaveAssets();
                Render(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
                Render(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),true);
                File.WriteAllText(Folder+"/Editor/VerifiedV3.txt","Rampe editable V3 : profondeur du mur, piste preservee, collision laterale et reinitialisation verifiees.");
                AssetDatabase.ImportAsset(Folder+"/Editor/VerifiedV3.txt");
                File.WriteAllText(Path.Combine(Reports,"unity-tests.txt"),"PASS V3\nIdentity shape, group editing, independent instances, depth extends lower wall without changing road, side collision, matching duplicate colliders, zero depth restores original vertices, invalid depth retains valid mesh; previews.\nVertices: "+source.vertexCount+"\n"+DateTime.Now.ToString("s"));
                Debug.Log("ramp_C est editable : selectionne ses points dans la vue Scene.");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Reports,"unity-tests.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally{if(root!=null)PrefabUtility.UnloadPrefabContents(root);}
        }
        public static Mesh CopyReadable(Mesh original)
        {
            // Editor access also supports FBX meshes whose Read/Write flag is disabled.
            using(var array=MeshUtility.AcquireReadOnlyMeshData(original)){
                var data=array[0];var mesh=new Mesh{name="Ramp_C_Source",indexFormat=original.indexFormat};
                using(var v=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp)){data.GetVertices(v);mesh.SetVertices(v);}
                if(data.HasVertexAttribute(VertexAttribute.Normal))using(var n=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp)){data.GetNormals(n);mesh.SetNormals(n);}
                if(data.HasVertexAttribute(VertexAttribute.Tangent))using(var t=new NativeArray<Vector4>(data.vertexCount,Allocator.Temp)){data.GetTangents(t);mesh.SetTangents(t);}
                if(data.HasVertexAttribute(VertexAttribute.Color))using(var c=new NativeArray<Color>(data.vertexCount,Allocator.Temp)){data.GetColors(c);mesh.SetColors(c);}
                for(int channel=0;channel<8;channel++)if(data.HasVertexAttribute((VertexAttribute)((int)VertexAttribute.TexCoord0+channel)))
                    using(var uv=new NativeArray<Vector4>(data.vertexCount,Allocator.Temp)){data.GetUVs(channel,uv);mesh.SetUVs(channel,uv);}
                mesh.subMeshCount=data.subMeshCount;
                for(int s=0;s<data.subMeshCount;s++){
                    var sub=data.GetSubMesh(s);
                    using(var indices=new NativeArray<int>(sub.indexCount,Allocator.Temp)){data.GetIndices(indices,s,true);mesh.SetIndices(indices.ToArray(),sub.topology,s);}
                }
                mesh.RecalculateBounds();if(!data.HasVertexAttribute(VertexAttribute.Normal))mesh.RecalculateNormals();return mesh;
            }
        }
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        static void Verify(Mesh source)
        {
            var scene=EditorSceneManager.NewPreviewScene();
            GameObject go=null,other=null;
            try {
                go=new GameObject("Rampe de verification");SceneManager.MoveGameObjectToScene(go,scene);
                var r=go.AddComponent<SonicEditableRamp>();r.meshSubdivisions=0;r.Initialize(source);Check(r.Rebuild(),"Initial build: "+r.LastError);
                var filter=go.GetComponent<MeshFilter>();var collider=go.GetComponent<MeshCollider>();var original=source.vertices;var vertices=filter.sharedMesh.vertices;
                float tolerance=source.bounds.size.magnitude*.00001f;
                for(int i=0;i<vertices.Length;i++)Check(Vector3.Distance(original[i],vertices[i])<tolerance,"Original shape retained");
                Check(filter.sharedMesh.subMeshCount==source.subMeshCount && filter.sharedMesh.uv.Length==source.uv.Length,"Submeshes and texture UVs retained");
                var before=(Vector3[])r.Points.Clone();var delta=Vector3.up*(source.bounds.size.y*.2f);
                r.MovePoints(new[]{0,1,1,-1,300},delta);
                for(int i=0;i<27;i++)Check(Vector3.Distance(r.Points[i],before[i]+(i==0 || i==1?delta:Vector3.zero))<tolerance,"Group translation happens once per selected point");
                r.ResetShape();var upper=new List<int>();for(int z=0;z<3;z++)for(int x=0;x<3;x++)upper.Add(SonicEditableRamp.Index(x,2,z));
                r.MovePoints(upper,delta);Check(r.Rebuild(),"Height edit: "+r.LastError);
                Check(filter.sharedMesh.bounds.max.y>source.bounds.max.y+delta.y*.9f,"Upper surface follows height handles");
                Check(collider.sharedMesh==filter.sharedMesh,"Collider follows modified mesh");
                var additional=go.AddComponent<MeshCollider>();additional.sharedMesh=source;
                Check(r.Rebuild() && additional.sharedMesh==filter.sharedMesh,"Additional scene collider follows modified mesh too");
                Physics.SyncTransforms();var b=filter.sharedMesh.bounds;bool hit=false;
                for(int x=1;x<8;x++)for(int z=1;z<8;z++){
                    var p=new Vector3(Mathf.Lerp(b.min.x,b.max.x,x/8f),b.max.y+10,Mathf.Lerp(b.min.z,b.max.z,z/8f));
                    hit|=collider.Raycast(new Ray(p,Vector3.down),out _,b.size.y+20);
                }
                Check(hit,"Modified ramp has physical collision");
                other=new GameObject("Autre rampe");SceneManager.MoveGameObjectToScene(other,scene);
                var independent=other.AddComponent<SonicEditableRamp>();independent.meshSubdivisions=0;independent.Initialize(source);Check(independent.Rebuild(),"Second instance builds");
                Check(Mathf.Abs(other.GetComponent<MeshFilter>().sharedMesh.bounds.max.y-source.bounds.max.y)<tolerance,"Instances remain independent");
                var valid=collider.sharedMesh;for(int i=0;i<27;i++)r.Points[i]=Vector3.zero;
                Check(!r.Rebuild() && collider.sharedMesh==valid,"Collapsed cage retains last valid collision");
                r.ResetShape();Check(r.Rebuild(),"Reset restores original shape");
                var noWall=filter.sharedMesh.vertices;float baseY=filter.sharedMesh.bounds.min.y;
                r.wallDepth=source.bounds.size.y*.5f;Check(r.Rebuild(),"Wall extension builds");
                var deep=filter.sharedMesh.vertices;
                Check(Mathf.Abs(filter.sharedMesh.bounds.min.y-(baseY-r.wallDepth))<tolerance,"Configured depth extends below original base");
                var faces=source.triangles;
                for(int i=0;i<faces.Length;i+=3){
                    int a=faces[i],b0=faces[i+1],c=faces[i+2];
                    if(Vector3.Cross(noWall[b0]-noWall[a],noWall[c]-noWall[a]).normalized.y<=.05f)continue;
                    Check(Vector3.Distance(noWall[a],deep[a])<tolerance && Vector3.Distance(noWall[b0],deep[b0])<tolerance && Vector3.Distance(noWall[c],deep[c])<tolerance,"Road surface stays unchanged when depth increases");
                }
                Physics.SyncTransforms();var wallBounds=filter.sharedMesh.bounds;
                var sideOrigin=new Vector3(wallBounds.center.x,baseY-r.wallDepth*.5f,wallBounds.min.z-10);
                Check(collider.Raycast(new Ray(sideOrigin,Vector3.forward),out _,wallBounds.size.z+20),"New wall has side collision below old bottom");
                Check(additional.sharedMesh==filter.sharedMesh,"Additional collider follows wall depth");
                var validWall=filter.sharedMesh;r.wallDepth=float.NaN;Check(!r.Rebuild() && filter.sharedMesh==validWall,"Invalid depth retains valid wall");
                r.wallDepth=0;Check(r.Rebuild(),"Zero depth restores original underside");
                var restored=filter.sharedMesh.vertices;for(int i=0;i<restored.Length;i++)Check(Vector3.Distance(noWall[i],restored[i])<tolerance,"Reset depth restores every vertex");
                r.enabled=false;r.enabled=true;Check(r.GetComponent<MeshFilter>().sharedMesh!=null,"Enable regenerates geometry");
                Check(source.vertices[0]==original[0],"Shared source remains unchanged");
            }
            finally{
                if(go!=null)Object.DestroyImmediate(go);if(other!=null)Object.DestroyImmediate(other);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        static void Render(GameObject prefab,bool withWall=false)
        {
            var preview=new PreviewRenderUtility();Texture2D image=null;
            try {
                var go=Object.Instantiate(prefab);go.transform.position=Vector3.zero;preview.AddSingleGO(go);var ramp=go.GetComponent<SonicEditableRamp>();
                if(withWall)ramp.wallDepth=10/go.transform.TransformVector(Vector3.up).magnitude;
                ramp.Rebuild();
                var bounds=go.GetComponent<Renderer>().bounds;float size=bounds.size.magnitude;
                preview.camera.transform.position=bounds.center+new Vector3(1,.8f,-1).normalized*size*1.4f;preview.camera.transform.LookAt(bounds.center);
                preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=size*10+100;
                preview.camera.clearFlags=CameraClearFlags.Color;preview.camera.backgroundColor=new Color(.06f,.1f,.16f);
                preview.ambientColor=Color.gray;preview.lights[0].intensity=1.4f;preview.lights[0].transform.rotation=Quaternion.Euler(45,-30,0);preview.lights[1].intensity=.8f;
                preview.BeginStaticPreview(new Rect(0,0,1000,750));preview.Render();image=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(Reports,withWall?"ramp_C-wall-preview.png":"ramp_C-preview.png"),image.EncodeToPNG());
            }
            finally{if(image!=null)Object.DestroyImmediate(image);preview.Cleanup();}
        }
    }
}
