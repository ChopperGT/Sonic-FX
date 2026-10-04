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
    public static class SonicEditableRampSubdivisionVerification
    {
        const string Version="SonicFX.RampC.SubdividedSurface.v1";
        static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/RampSubdivision");
        static SonicEditableRampSubdivisionVerification(){EditorApplication.update+=Ready;}
        static void Ready(){
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null)return;
            EditorApplication.update-=Ready;if(SessionState.GetBool(Version,false))return;SessionState.SetBool(Version,true);Verify();
        }
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        static void Near(Vector3 a,Vector3 b,float tolerance,string message){Check(Vector3.Distance(a,b)<tolerance,message);}
        static void Same(Vector3[] a,Vector3[] b,float tolerance,string message){Check(a.Length==b.Length,message+": count");for(int i=0;i<a.Length;i++)Near(a[i],b[i],tolerance,message+": "+i);}
        static int Triangles(Mesh mesh){int n=0;for(int s=0;s<mesh.subMeshCount;s++)n+=(int)mesh.GetIndexCount(s)/3;return n;}
        static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
        static float CurveError(SonicEditableRamp ramp,Mesh prepared,Mesh deformed){
            var p=prepared.vertices;var d=deformed.vertices;var indices=prepared.triangles;float error=0;
            for(int i=0;i<indices.Length;i+=3){int a=indices[i],b=indices[i+1],c=indices[i+2];
                error=Mathf.Max(error,Vector3.Distance(ramp.DeformPoint((p[a]+p[b]+p[c])/3),(d[a]+d[b]+d[c])/3));
            }return error;
        }
        static void VerifySharedEdges(){
            var source=new Mesh();Mesh dense=null,seam=null;
            try{
                source.vertices=new[]{new Vector3(0,0,0),new Vector3(0,0,1),new Vector3(1,0,1),new Vector3(1,0,0)};
                source.normals=new[]{Vector3.up,Vector3.up,Vector3.up,Vector3.up};source.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};
                source.SetUVs(3,new List<Vector4>{Vector4.zero,Vector4.one,Vector4.one*2,Vector4.one*3});
                source.colors=new[]{Color.red,Color.green,Color.blue,Color.white};source.triangles=new[]{0,1,2,0,2,3};
                dense=SonicRampMeshSubdivision.Create(source,1);Check(dense.vertexCount==9 && Triangles(dense)==8,"Shared edge midpoint duplicated / triangles missing");
                var vertices=dense.vertices;int diagonal=0;for(int i=0;i<vertices.Length;i++)if(vertices[i]==new Vector3(.5f,0,.5f)){diagonal++;Check(Vector2.Distance(dense.uv[i],Vector2.one*.5f)<.00001f,"Midpoint UV interpolation");}
                Check(diagonal==1,"Internal diagonal creates crack");var uv3=new List<Vector4>();dense.GetUVs(3,uv3);Check(uv3.Count==dense.vertexCount,"Secondary UV lost");
                var triangles=dense.triangles;for(int i=0;i<triangles.Length;i+=3)Check(Vector3.Cross(vertices[triangles[i+1]]-vertices[triangles[i]],vertices[triangles[i+2]]-vertices[triangles[i]]).y>0,"Subdivision flips winding");
                Object.DestroyImmediate(dense);dense=null;
                // A duplicated diagonal carries different texture/normal values on each side.
                source.Clear();source.vertices=new[]{new Vector3(0,0,0),new Vector3(0,0,1),new Vector3(1,0,1),new Vector3(0,0,0),new Vector3(1,0,1),new Vector3(1,0,0)};
                source.normals=new[]{Vector3.up,Vector3.up,Vector3.up,Vector3.right,Vector3.right,Vector3.right};
                source.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.one*3,Vector2.one*4,Vector2.one*5};source.triangles=new[]{0,1,2,3,4,5};
                seam=SonicRampMeshSubdivision.Create(source,1);int duplicates=0;bool up=false,right=false;var seamPoints=seam.vertices;var normals=seam.normals;
                for(int i=0;i<seamPoints.Length;i++)if(seamPoints[i]==new Vector3(.5f,0,.5f)){duplicates++;up|=normals[i]==Vector3.up;right|=normals[i]==Vector3.right;}
                Check(duplicates==2 && up && right,"Texture/normal seam overwritten or unjoined positions");
            }finally{Object.DestroyImmediate(source);if(dense!=null)Object.DestroyImmediate(dense);if(seam!=null)Object.DestroyImmediate(seam);}
        }
        [MenuItem("Tools/Sonic FX/Structures/Verifier le lissage des polygones de ramp_C")]
        public static void Verify(){
            Directory.CreateDirectory(Folder);var scene=EditorSceneManager.NewPreviewScene();GameObject go=null,copy=null;SonicEditableRamp ramp=null;Mesh prepared=null;
            try{
                VerifySharedEdges();var source=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Structure/RampeEditable/Ramp_C_Source.asset");Check(source!=null,"Ramp source missing");
                var original=source.vertices;int originalCount=Triangles(source);float tolerance=source.bounds.size.magnitude*.00001f;
                go=new GameObject("Ramp polygon smoothing verification");SceneManager.MoveGameObjectToScene(go,scene);ramp=go.AddComponent<SonicEditableRamp>();
                Check(ramp.meshSubdivisions==2,"Default smoothing not enabled");ramp.Initialize(source);var filter=go.GetComponent<MeshFilter>();var collider=go.GetComponent<MeshCollider>();var extra=go.AddComponent<MeshCollider>();
                var curve=new List<int>();for(int z=0;z<3;z++)for(int y=0;y<3;y++)curve.Add(ramp.PointIndex(1,y,z));
                ramp.MovePoints(curve,Vector3.up*source.bounds.size.y*.5f);var controls=(Vector3[])ramp.Points.Clone();float coarseError=0,previousError=float.PositiveInfinity;
                var stats=new List<string>();
                for(int level=0;level<=4;level++){
                    ramp.meshSubdivisions=level;Check(ramp.Rebuild(),"Build "+level+": "+ramp.LastError);var mesh=filter.sharedMesh;
                    prepared=SonicRampMeshSubdivision.Create(source,level);var positions=prepared.vertices;var actual=mesh.vertices;var normals=mesh.normals;
                    Check(Triangles(mesh)==originalCount*(1<<(level*2)) && ramp.TriangleCount==Triangles(mesh),"Incorrect polygon density at "+level);
                    Check(mesh.subMeshCount==source.subMeshCount && mesh.uv.Length==mesh.vertexCount,"UV/material slots lost");
                    for(int i=0;i<positions.Length;i++){
                        Near(ramp.DeformPoint(positions[i]),actual[i],tolerance,"New vertex not evaluated on curve");
                        Check(Finite(normals[i].x) && Finite(normals[i].y) && Finite(normals[i].z) && Mathf.Abs(normals[i].magnitude-1)<.001f,"Invalid surface normal");
                    }
                    float error=CurveError(ramp,prepared,mesh);if(level==0)coarseError=error;else Check(error<previousError*.3f,"More polygons do not improve curve accuracy");previousError=error;
                    Check(level!=4 || error<coarseError/200,"Finest level not sufficiently smoother than original");
                    Check(collider.sharedMesh==mesh && extra.sharedMesh==mesh,"Collision uses coarse original surface");Same(controls,ramp.Points,tolerance,"Smoothing changes edited controls");
                    Object.DestroyImmediate(prepared);prepared=null;
                    Physics.SyncTransforms();var b=mesh.bounds;bool hit=false;
                    for(int x=1;x<6;x++)for(int z=1;z<6;z++)hit|=collider.Raycast(new Ray(new Vector3(Mathf.Lerp(b.min.x,b.max.x,x/6f),b.max.y+10,Mathf.Lerp(b.min.z,b.max.z,z/6f)),Vector3.down),out _,b.size.y+20);
                    Check(hit,"Smoothed road has no physical surface");
                    var noDepth=mesh.vertices;float bottom=b.min.y;ramp.wallDepth=source.bounds.size.y*.25f;Check(ramp.Rebuild(),"Dense depth wall failed");
                    Check(Mathf.Abs(filter.sharedMesh.bounds.min.y-(bottom-ramp.wallDepth))<tolerance,"Dense wall does not reach requested depth");
                    prepared=SonicRampMeshSubdivision.Create(source,level);var faces=prepared.triangles;var deep=filter.sharedMesh.vertices;
                    for(int i=0;i<faces.Length;i+=3){int a=faces[i],b0=faces[i+1],c=faces[i+2];
                        if(Vector3.Cross(noDepth[b0]-noDepth[a],noDepth[c]-noDepth[a]).normalized.y<=.05f)continue;
                        Near(deep[a],noDepth[a],tolerance,"Depth moves dense road");Near(deep[b0],noDepth[b0],tolerance,"Depth moves dense road");Near(deep[c],noDepth[c],tolerance,"Depth moves dense road");
                    }
                    Object.DestroyImmediate(prepared);prepared=null;ramp.wallDepth=0;Check(ramp.Rebuild(),"Zero depth");Same(noDepth,filter.sharedMesh.vertices,tolerance,"Depth reset changes dense surface");
                    stats.Add("Level "+level+": "+ramp.TriangleCount+" triangles, maximum curve error "+error.ToString("G6"));
                }
                ramp.meshSubdivisions=2;Check(ramp.Rebuild(),ramp.LastError);
                var cacheField=typeof(SonicEditableRamp).GetField("subdividedSource",BindingFlags.Instance|BindingFlags.NonPublic);var cache=cacheField.GetValue(ramp);
                Check(ramp.InsertPoints(0,.5f,out _) && ramp.Rebuild(),"New controls fail on dense mesh");Check(ReferenceEquals(cache,cacheField.GetValue(ramp)),"Topology rebuilt on control edit");
                Undo.IncrementCurrentGroup();Undo.RecordObject(ramp,"Verification: polygon density");ramp.meshSubdivisions=3;Check(ramp.Rebuild(),ramp.LastError);Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();Check(ramp.Rebuild() && ramp.meshSubdivisions==2 && ramp.TriangleCount==originalCount*16,"Undo loses polygon quality");
                Undo.PerformRedo();Check(ramp.Rebuild() && ramp.meshSubdivisions==3 && ramp.TriangleCount==originalCount*64,"Redo loses polygon quality");Undo.ClearUndo(ramp);
                copy=new GameObject("Serialized dense ramp");SceneManager.MoveGameObjectToScene(copy,scene);var duplicate=copy.AddComponent<SonicEditableRamp>();
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(ramp),duplicate);Check(duplicate.Rebuild(),duplicate.LastError);Same(filter.sharedMesh.vertices,copy.GetComponent<MeshFilter>().sharedMesh.vertices,tolerance,"Saved quality/controls lost");
                var good=filter.sharedMesh;for(int i=0;i<ramp.Points.Length;i++)ramp.Points[i]=Vector3.zero;Check(!ramp.Rebuild() && collider.sharedMesh==good,"Invalid curve destroys valid dense collision");
                ramp.ResetShape();ramp.meshSubdivisions=0;Check(ramp.Rebuild(),ramp.LastError);Same(original,filter.sharedMesh.vertices,tolerance,"Zero restores original geometry");
                Check(cacheField.GetValue(ramp)==null,"Disabled subdivision keeps dense source allocation");ramp.meshSubdivisions=2;ramp.Rebuild();ramp.enabled=false;
                Check(filter.sharedMesh==source && cacheField.GetValue(ramp)==null,"Disabling ramp leaks generated topology");ramp.enabled=true;Check(ramp.TriangleCount==originalCount*16,"Re-enable loses quality");
                Same(original,source.vertices,tolerance,"Original imported source changed");
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Structure/ramp_C.prefab");Check(prefab.GetComponent<SonicEditableRamp>().meshSubdivisions==2,"Placed prefab does not inherit quality 2");
                Render(0);Render(3);
                File.WriteAllText(Path.Combine(Folder,"unity-tests.txt"),"PASS\n"+DateTime.Now.ToString("s")+"\n"+string.Join("\n",stats)+"\nShared midpoint welding; UV/normal seams; preserved controls/materials; pre-deformation subdivision; matching physical colliders; wall depth and road preservation; cache reuse; new control points; Undo/Redo; serialization; invalid curve protection; level 0 restoration; cache cleanup; installed prefab default 2; previews.\n");
                Debug.Log("Rampe C : lissage par polygones verifie de 0 a 4, courbes et collisions plus precises.");
            }catch(Exception e){File.WriteAllText(Path.Combine(Folder,"unity-tests.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally{if(ramp!=null)Undo.ClearUndo(ramp);if(prepared!=null)Object.DestroyImmediate(prepared);if(go!=null)Object.DestroyImmediate(go);if(copy!=null)Object.DestroyImmediate(copy);EditorSceneManager.ClosePreviewScene(scene);}
        }
        static void Render(int quality){
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Structure/ramp_C.prefab");var preview=new PreviewRenderUtility();Texture2D image=null;
            try{
                var go=Object.Instantiate(prefab);preview.AddSingleGO(go);go.transform.position=Vector3.zero;var ramp=go.GetComponent<SonicEditableRamp>();ramp.meshSubdivisions=quality;
                var points=new List<int>();for(int z=0;z<3;z++)for(int y=0;y<3;y++)points.Add(ramp.PointIndex(1,y,z));
                ramp.MovePoints(points,Vector3.up*ramp.SourceMesh.bounds.size.y*.5f);Check(ramp.Rebuild(),ramp.LastError);
                var b=go.GetComponent<Renderer>().bounds;float size=b.size.magnitude;preview.camera.fieldOfView=40;preview.camera.transform.position=b.center+new Vector3(.7f,.75f,-1).normalized*size*1.9f;preview.camera.transform.LookAt(b.center);
                preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=size*10+100;preview.camera.clearFlags=CameraClearFlags.Color;preview.camera.backgroundColor=new Color(.06f,.1f,.16f);
                preview.ambientColor=Color.gray;preview.lights[0].intensity=1.4f;preview.lights[0].transform.rotation=Quaternion.Euler(45,-30,0);preview.lights[1].intensity=.8f;
                preview.BeginStaticPreview(new Rect(0,0,1000,750));preview.Render();image=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(Folder,"ramp-quality-"+quality+".png"),image.EncodeToPNG());
            }finally{if(image!=null)Object.DestroyImmediate(image);preview.Cleanup();}
        }
    }
}
