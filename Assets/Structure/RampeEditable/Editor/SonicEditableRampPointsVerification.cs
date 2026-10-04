using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace SonicFX.Structures.Editor
{
    [InitializeOnLoad]
    public static class SonicEditableRampPointsVerification
    {
        const string Version="SonicFX.RampC.AddablePoints.v4";
        static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/RampEditablePoints");
        static SonicEditableRampPointsVerification(){EditorApplication.update+=Ready;}
        static void Ready(){
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null)return;
            EditorApplication.update-=Ready;if(SessionState.GetBool(Version,false))return;SessionState.SetBool(Version,true);Verify();
        }
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        static Vector3 Bernstein(float t)=>new Vector3((1-t)*(1-t),2*t*(1-t),t*t);
        static Vector3 Legacy(Vector3[] points,Vector3 t){
            var bx=Bernstein(t.x);var by=Bernstein(t.y);var bz=Bernstein(t.z);var p=Vector3.zero;
            for(int z=0;z<3;z++)for(int y=0;y<3;y++)for(int x=0;x<3;x++)p+=points[SonicEditableRamp.Index(x,y,z)]*(bx[x]*by[y]*bz[z]);return p;
        }
        static List<Vector3> Samples(Mesh source){
            var result=new List<Vector3>();var b=source.bounds;
            for(int z=0;z<=10;z++)for(int y=0;y<=10;y++)for(int x=0;x<=10;x++)result.Add(b.min+Vector3.Scale(b.size,new Vector3(x*.1f,y*.1f,z*.1f)));
            return result;
        }
        static List<Vector3> Deformed(SonicEditableRamp ramp,List<Vector3> samples){var result=new List<Vector3>();foreach(var p in samples)result.Add(ramp.DeformPoint(p));return result;}
        static void Same(IList<Vector3> a,IList<Vector3> b,float tolerance,string reason){Check(a.Count==b.Count,reason+": count");for(int i=0;i<a.Count;i++)Check(Vector3.Distance(a[i],b[i])<tolerance,reason+": point "+i);}
        static void VerifyAutomaticRows(Scene scene,Mesh source,bool cube)
        {
            var probe=new GameObject("Automatic point distribution verification");SceneManager.MoveGameObjectToScene(probe,scene);
            UnityEditor.Editor inspector=null;
            try{
                SonicEditableRamp r=cube?(SonicEditableRamp)probe.AddComponent<SonicEditableCube>():probe.AddComponent<SonicEditableRamp>();
                r.meshSubdivisions=0;r.Initialize(source);inspector=UnityEditor.Editor.CreateEditor(r);
                var editorType=typeof(SonicEditableRampEditor);
                var suggest=editorType.GetMethod("SuggestPosition",BindingFlags.Instance|BindingFlags.NonPublic);
                var position=editorType.GetField("insertionPosition",BindingFlags.Instance|BindingFlags.NonPublic);
                var selected=(HashSet<int>)editorType.GetField("selected",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(inspector);
                var samples=Samples(source);float tolerance=source.bounds.size.magnitude*.00001f;
                for(int axis=0;axis<3;axis++){
                    r.ResetShape();r.Rebuild();selected.Clear();
                    editorType.GetField("insertionAxis",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(inspector,axis);
                    int additions=0;
                    while(r.PointCounts[axis]<SonicEditableRamp.MaxPointsPerAxis){
                        suggest.Invoke(inspector,new object[]{r,false});float t=(float)position.GetValue(inspector);
                        Check(t>=.01f && t<=.99f,"Automatic position exceeds the Inspector slider");
                        Check(r.InsertPoints(axis,t,out int layer) && r.Rebuild(),"Automatic insertion fails: "+r.LastError);
                        // Mirror the GUI's selection of the added row before its next suggestion.
                        selected.Clear();var c=r.PointCounts;
                        for(int z=0;z<c.z;z++)for(int y=0;y<c.y;y++)for(int x=0;x<c.x;x++)
                            if((axis==0?x:axis==1?y:z)==layer)selected.Add(r.PointIndex(x,y,z));
                        Same(samples,Deformed(r,samples),tolerance,"Automatic additions change the source shape");
                        if(++additions==3){
                            var expected=new[]{0f,.125f,.375f,.625f,.875f,1f};
                            for(int i=0;i<expected.Length;i++){
                                var p=Vector3Int.zero;p[axis]=i;
                                float actual=(r.Points[r.PointIndex(p.x,p.y,p.z)][axis]-source.bounds.min[axis])/source.bounds.size[axis];
                                Check(Mathf.Abs(actual-expected[i])<.0001f,"First three additions cluster at an end");
                            }
                        }
                    }
                    for(int i=1;i<r.PointCounts[axis];i++){
                        var a=Vector3Int.zero;var b=Vector3Int.zero;a[axis]=i-1;b[axis]=i;
                        float gap=(r.Points[r.PointIndex(b.x,b.y,b.z)][axis]-r.Points[r.PointIndex(a.x,a.y,a.z)][axis])/source.bounds.size[axis];
                        Check(gap>=1f/64-.0001f && gap<=.15f,"Automatic rows overlap or concentrate at one end: "+gap);
                    }
                    Check(probe.GetComponent<MeshCollider>().sharedMesh==probe.GetComponent<MeshFilter>().sharedMesh,"Distribution loses collider sync");
                    Check(!r.CanInsertPoints(axis,.1234f),"Row limit no longer enforced");
                }
                r.ResetShape();selected.Clear();editorType.GetField("insertionAxis",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(inspector,0);
                suggest.Invoke(inspector,new object[]{r,false});Check(r.InsertPoints(0,(float)position.GetValue(inspector),out _) && r.Rebuild(),"Local placement setup");
                selected.Add(r.PointIndex(3,2,2));
                suggest.Invoke(inspector,new object[]{r,true});Check(Mathf.Abs((float)position.GetValue(inspector)-.75f)<.0001f,"Explicit near-selection placement lost");
            }finally{if(inspector!=null)Object.DestroyImmediate(inspector);Object.DestroyImmediate(probe);}
        }
        [MenuItem("Tools/Sonic FX/Structures/Verifier les points ajoutables de ramp_C")]
        public static void Verify(){
            Directory.CreateDirectory(Folder);var scene=EditorSceneManager.NewPreviewScene();GameObject go=null,copy=null;SonicEditableRamp ramp=null;
            try{
                var source=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Structure/RampeEditable/Ramp_C_Source.asset");Check(source!=null,"Source mesh missing");
                VerifyAutomaticRows(scene,source,false);
                var cubeSource=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Structure/CubeEditable/Cube_Source.asset");Check(cubeSource!=null,"Cube source missing");VerifyAutomaticRows(scene,cubeSource,true);
                // Retest the existing height/group/depth/duplicate-collider behavior in isolation.
                typeof(SonicEditableRampBuilder).GetMethod("Verify",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{source});
                go=new GameObject("Ramp addable point verification");SceneManager.MoveGameObjectToScene(go,scene);ramp=go.AddComponent<SonicEditableRamp>();ramp.meshSubdivisions=0;ramp.Initialize(source);
                var filter=go.GetComponent<MeshFilter>();var collider=go.GetComponent<MeshCollider>();var extra=go.AddComponent<MeshCollider>();float tolerance=source.bounds.size.magnitude*.00001f;
                Check(ramp.Rebuild(),ramp.LastError);Same(source.vertices,filter.sharedMesh.vertices,tolerance,"Initial identity");
                var samples=Samples(source);var originalSource=source.vertices;
                ramp.MovePoints(new[]{1,4,7,10,13,16,19,22,25},Vector3.up*source.bounds.size.y*.035f);
                var legacyPoints=(Vector3[])ramp.Points.Clone();
                foreach(string field in new[]{"knotsX","knotsY","knotsZ"})typeof(SonicEditableRamp).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ramp,null);
                Check(ramp.PointCounts==new Vector3Int(3,3,3),"Legacy grid dimensions");Same(legacyPoints,ramp.Points,tolerance,"Migration resets old edited points");
                foreach(var p in samples){var d=p-source.bounds.min;var t=new Vector3(d.x/source.bounds.size.x,d.y/source.bounds.size.y,d.z/source.bounds.size.z);Check(Vector3.Distance(ramp.DeformPoint(p),Legacy(legacyPoints,t))<tolerance,"Legacy edited curve changes");}
                ramp.wallDepth=source.bounds.size.y*.2f;Check(ramp.Rebuild(),ramp.LastError);
                var shape=Deformed(ramp,samples);var vertices=filter.sharedMesh.vertices;var normals=filter.sharedMesh.normals;var uv=filter.sharedMesh.uv;
                foreach(var insertion in new[]{new Vector2(0,.5f),new Vector2(0,.25f),new Vector2(0,.75f),new Vector2(1,.4f),new Vector2(2,.3f),new Vector2(2,.7f)}){
                    int axis=(int)insertion.x;var before=ramp.PointCounts;Check(ramp.InsertPoints(axis,insertion.y,out int layer),"Insertion rejected");
                    Check(ramp.PointCounts[axis]==before[axis]+1 && layer>=0 && layer<ramp.PointCounts[axis],"New row dimensions/selection");
                    Check(ramp.Rebuild(),"Inserted grid invalid: "+ramp.LastError);Same(shape,Deformed(ramp,samples),tolerance,"Insertion changes curve");
                    Same(vertices,filter.sharedMesh.vertices,tolerance,"Insertion changes existing rendered geometry or wall depth");Same(normals,filter.sharedMesh.normals,.0001f,"Insertion changes normals");
                    Check(collider.sharedMesh==filter.sharedMesh && extra.sharedMesh==filter.sharedMesh,"Added points lose duplicate collider");
                    Check(filter.sharedMesh.subMeshCount==source.subMeshCount && filter.sharedMesh.uv.Length==uv.Length,"Materials/UV lost");
                }
                Check(ramp.Points.Length==6*4*5,"Final variable cage length");
                var saved=filter.sharedMesh;var dimensions=ramp.PointCounts;
                foreach(var invalid in new[]{new Vector2(-1,.5f),new Vector2(3,.5f),new Vector2(0,0),new Vector2(0,1),new Vector2(0,.5f),new Vector2(1,float.NaN)})
                    Check(!ramp.InsertPoints((int)invalid.x,invalid.y,out _) && ramp.PointCounts==dimensions && filter.sharedMesh==saved,"Invalid insertion changes valid ramp");
                // A new local control affects the road and collision, leaving the opposite end fixed.
                int control=ramp.PointIndex(2,dimensions.y-1,dimensions.z/2);var farEnd=ramp.DeformPoint(source.bounds.min+Vector3.Scale(source.bounds.size,new Vector3(1,1,.5f)));
                var beforeMove=filter.sharedMesh.vertices;ramp.MovePoints(new[]{control,control},Vector3.up*source.bounds.size.y*.015f);Check(ramp.Rebuild(),ramp.LastError);
                Check(Vector3.Distance(ramp.DeformPoint(source.bounds.min+Vector3.Scale(source.bounds.size,new Vector3(1,1,.5f))),farEnd)<tolerance,"Local point moves opposite end");
                bool changed=false;var afterMove=filter.sharedMesh.vertices;for(int i=0;i<afterMove.Length;i++)changed|=Vector3.Distance(beforeMove[i],afterMove[i])>tolerance;
                Check(changed && collider.sharedMesh==filter.sharedMesh,"New control has no effect on mesh/collision");
                Physics.SyncTransforms();var b=filter.sharedMesh.bounds;bool hit=false;
                for(int x=1;x<8;x++)for(int z=1;z<8;z++)hit|=collider.Raycast(new Ray(new Vector3(Mathf.Lerp(b.min.x,b.max.x,x/8f),b.max.y+10,Mathf.Lerp(b.min.z,b.max.z,z/8f)),Vector3.down),out _,b.size.y+20);
                Check(hit,"Edited ramp cannot be walked on");
                copy=new GameObject("Serialized ramp copy");SceneManager.MoveGameObjectToScene(copy,scene);var duplicate=copy.AddComponent<SonicEditableRamp>();
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(ramp),duplicate);Check(duplicate.Rebuild(),duplicate.LastError);Same(filter.sharedMesh.vertices,copy.GetComponent<MeshFilter>().sharedMesh.vertices,tolerance,"Serialization loses new controls");
                var independent=duplicate.Points[control];ramp.MovePoints(new[]{control},Vector3.up*.01f);Check(duplicate.Points[control]==independent,"Instances share mutable points");ramp.Rebuild();
                Undo.IncrementCurrentGroup();Undo.RecordObject(ramp,"Verification: add ramp row");var undoCounts=ramp.PointCounts;var undoShape=Deformed(ramp,samples);
                Check(ramp.InsertPoints(2,.5f,out _) && ramp.Rebuild(),"Undo test insertion");Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();Check(ramp.Rebuild() && ramp.PointCounts==undoCounts,"Undo does not restore grid");Same(undoShape,Deformed(ramp,samples),tolerance,"Undo changes shape");
                Undo.PerformRedo();Check(ramp.Rebuild() && ramp.PointCounts.z==undoCounts.z+1,"Redo loses added row");Undo.ClearUndo(ramp);
                var valid=collider.sharedMesh;for(int i=0;i<ramp.Points.Length;i++)ramp.Points[i]=Vector3.zero;
                Check(!ramp.Rebuild() && collider.sharedMesh==valid,"Invalid new cage replaces last valid collision");
                ramp.ResetShape();Check(ramp.PointCounts==new Vector3Int(3,3,3) && ramp.Points.Length==27 && ramp.Rebuild(),"Reset loses original cage");
                while(ramp.PointCounts.x<SonicEditableRamp.MaxPointsPerAxis)Check(ramp.InsertPoints(0,ramp.SuggestedInsertion(0),out _),"Repeated automatic insert fails");
                Check(!ramp.InsertPoints(0,.12345f,out _) && ramp.Rebuild(),"Axis limit not enforced / dense cage invalid");
                Same(originalSource,source.vertices,tolerance,"Shared source changed");
                Render();File.WriteAllText(Path.Combine(Folder,"unity-tests.txt"),"PASS\n"+DateTime.Now.ToString("s")+"\nLegacy edited cage preserved; exact shape/normals after six insertions on X/Y/Z; dynamic row selection; invalid/duplicate insertion; local road edit; matching duplicate MeshColliders and physical raycast; UV/material slots; serialized controls; independent instances; Undo/Redo; collapsed-grid collision protection; reset; balanced automatic insertion to 16 rows on X/Y/Z for Cube and ramp_C, including last-row selection and minimum spacing; explicit near-selection insertion; legacy height/depth checks; preview.\n");
                Debug.Log("Rampe C : ajout de points sur X / Y / Z, forme conservee, collisions et annulation verifies.");
            }catch(Exception e){File.WriteAllText(Path.Combine(Folder,"unity-tests.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally{if(ramp!=null)Undo.ClearUndo(ramp);if(go!=null)Object.DestroyImmediate(go);if(copy!=null)Object.DestroyImmediate(copy);EditorSceneManager.ClosePreviewScene(scene);}
        }
        static void Render(){
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Structure/ramp_C.prefab");var preview=new PreviewRenderUtility();Texture2D image=null;
            try{
                var go=Object.Instantiate(prefab);preview.AddSingleGO(go);go.transform.position=Vector3.zero;var ramp=go.GetComponent<SonicEditableRamp>();
                Check(ramp.InsertPoints(0,.5f,out int layer),"Installed prefab cannot add points");ramp.Rebuild();
                var counts=ramp.PointCounts;var selected=new List<int>();for(int z=0;z<counts.z;z++)selected.Add(ramp.PointIndex(layer,counts.y-1,z));
                ramp.MovePoints(selected,Vector3.up*ramp.SourceMesh.bounds.size.y*.075f);Check(ramp.Rebuild(),"Preview edit: "+ramp.LastError);
                var b=go.GetComponent<Renderer>().bounds;float size=b.size.magnitude;preview.camera.fieldOfView=40;preview.camera.transform.position=b.center+new Vector3(1,.8f,-1).normalized*size*2;preview.camera.transform.LookAt(b.center);
                preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=size*10+100;preview.camera.clearFlags=CameraClearFlags.Color;preview.camera.backgroundColor=new Color(.06f,.1f,.16f);
                preview.ambientColor=Color.gray;preview.lights[0].intensity=1.4f;preview.lights[0].transform.rotation=Quaternion.Euler(45,-30,0);preview.lights[1].intensity=.8f;
                preview.BeginStaticPreview(new Rect(0,0,1000,750));preview.Render();image=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(Folder,"ramp-added-points-preview.png"),image.EncodeToPNG());
            }finally{if(image!=null)Object.DestroyImmediate(image);preview.Cleanup();}
        }
    }
}
