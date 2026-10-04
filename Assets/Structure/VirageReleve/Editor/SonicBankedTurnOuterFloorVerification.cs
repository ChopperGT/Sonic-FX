using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace SonicFX.Structures.Editor
{
    [InitializeOnLoad]
    public static class SonicBankedTurnOuterFloorVerification
    {
        const string Version="SonicBankedTurn.OuterFloor.v1";
        static readonly string Folder=Path.Combine(SonicBankedTurnBuilder.Reports,"OuterFloor");
        static SonicBankedTurnOuterFloorVerification(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || PrefabStageUtility.GetCurrentPrefabStage()!=null)return;
            EditorApplication.update-=Ready;
            if(SessionState.GetBool(Version,false))return;
            SessionState.SetBool(Version,true);Verify();
        }
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        [MenuItem("Tools/Sonic FX/Structures/Verifier le sol exterieur du virage")]
        public static void Verify()
        {
            Directory.CreateDirectory(Folder);GameObject go=null,copy=null;var scene=EditorSceneManager.NewPreviewScene();
            try{
                go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SonicBankedTurnBuilder.PrefabPath));SceneManager.MoveGameObjectToScene(go,scene);
                var turn=go.GetComponent<SonicBankedTurn>();var collider=go.GetComponent<MeshCollider>();
                foreach(var direction in new[]{SonicBankedTurn.TurnDirection.Droite,SonicBankedTurn.TurnDirection.Gauche})
                foreach(bool smooth in new[]{false,true})foreach(bool fill in new[]{false,true})
                foreach(float angle in new[]{45f,90f,180f})foreach(float width in new[]{.1f,6f}){
                    turn.direction=direction;turn.smoothSides=smooth;turn.fillInterior=fill;turn.angle=angle;
                    turn.outerFloor=true;turn.outerFloorWidth=width;turn.entryLength=3;turn.exitLength=5;turn.Rebuild();Physics.SyncTransforms();
                    var mesh=go.GetComponent<MeshFilter>().sharedMesh;
                    Check(mesh==collider.sharedMesh && !collider.isTrigger && !collider.convex,"Visual and solid collision share geometry");
                    var v=mesh.vertices;var n=mesh.normals;var t=mesh.triangles;
                    Check(mesh.uv.Length==v.Length && mesh.tangents.Length==v.Length,"UVs and tangents cover the new floor");
                    for(int i=0;i<t.Length;i+=3){
                        var cross=Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]);
                        Check(cross.sqrMagnitude>1e-12f,"Degenerate triangle");
                        Check(Vector3.Dot(cross,n[t[i]]+n[t[i+1]]+n[t[i+2]])>0,"Normals disagree with surface winding");
                    }
                    float span=angle*Mathf.Deg2Rad;
                    foreach(float along in new[]{.15f,.5f,.85f})foreach(float across in new[]{.1f,.5f,.9f}){
                        turn.EvaluateOuterFloor(span*along,across,out var point,out var normal);
                        Check(collider.Raycast(new Ray(point+Vector3.up*2,Vector3.down),out var hit,4),"Missing elevated floor collision");
                        Check(Mathf.Abs(hit.point.y-point.y)<.025f && Vector3.Dot(hit.normal,normal)>.97f,"Floor height or normal inconsistent with side smoothing");
                    }
                    float mid=span*.5f;
                    turn.EvaluateRamp(mid,1,out var rampTop,out _);turn.EvaluateOuterFloor(mid,0,out var floorStart,out _);
                    Check(Vector3.Distance(rampTop,floorStart)<.00001f,"Gap between wall and outer floor");
                    var outward=new Vector3(-turn.Sign*Mathf.Cos(mid),0,Mathf.Sin(mid));
                    var seam=turn.Point(mid,turn.OuterRadius+.01f,turn.wallHeight*.5f);
                    Check(!collider.Raycast(new Ray(seam,-outward),out _,.02f),"Old outer facade remains inside expanded block");
                    foreach(bool exit in new[]{false,true}){
                        float theta=exit?span:0;var forward=exit?turn.ExitForward:Vector3.forward;
                        turn.EvaluateOuterFloor(theta,.5f,out var point,out _);
                        point+=forward*(exit?turn.exitLength*.5f:-turn.entryLength*.5f);
                        Check(collider.Raycast(new Ray(point+Vector3.up,Vector3.down),out var hit,2) && Mathf.Abs(hit.point.y-point.y)<.01f,"Floor must follow entrance/exit extension");
                        if(smooth)Check(Mathf.Abs(point.y)<.00001f,"Smoothed outer floor must return to ground at openings");
                    }
                }
                turn.smoothSides=false;turn.fillInterior=false;turn.direction=SonicBankedTurn.TurnDirection.Droite;
                turn.angle=90;turn.entryLength=turn.exitLength=0;turn.outerFloor=false;turn.outerFloorWidth=6;turn.Rebuild();
                var oldVertices=go.GetComponent<MeshFilter>().sharedMesh.vertices;var oldIndices=go.GetComponent<MeshFilter>().sharedMesh.triangles;
                turn.outerFloorWidth=19;turn.Rebuild();var unchanged=go.GetComponent<MeshFilter>().sharedMesh;
                Check(unchanged.vertexCount==oldVertices.Length && unchanged.triangles.Length==oldIndices.Length,"Disabled option changes existing turn geometry");
                var unchangedVertices=unchanged.vertices;
                for(int i=0;i<oldVertices.Length;i++)Check(unchangedVertices[i]==oldVertices[i],"Disabled width changes geometry");
                turn.outerFloor=true;turn.outerFloorWidth=9;turn.wallHeight=15;turn.Rebuild();Physics.SyncTransforms();
                turn.EvaluateOuterFloor(Mathf.PI*.25f,.5f,out var high,out _);
                Check(high.y==15 && collider.Raycast(new Ray(high+Vector3.up,Vector3.down),out var raised,2) && Mathf.Abs(raised.point.y-15)<.01f,"Wall height must move elevated floor");
                Undo.IncrementCurrentGroup();Undo.RecordObject(turn,"Verifier largeur du sol exterieur");turn.outerFloorWidth=13;Undo.FlushUndoRecordObjects();turn.Rebuild();
                Undo.PerformUndo();turn.Rebuild();Check(turn.outerFloorWidth==9,"Undo must restore floor width");
                Undo.PerformRedo();turn.Rebuild();Check(turn.outerFloorWidth==13,"Redo must restore floor width");Undo.ClearUndo(turn);Undo.IncrementCurrentGroup();
                var json=EditorJsonUtility.ToJson(turn);copy=new GameObject("Serialization probe");SceneManager.MoveGameObjectToScene(copy,scene);
                var restored=copy.AddComponent<SonicBankedTurn>();EditorJsonUtility.FromJsonOverwrite(json,restored);restored.Rebuild();
                Check(restored.outerFloor && restored.outerFloorWidth==13 && restored.wallHeight==15,"Floor settings do not survive serialization");
                turn.outerFloorWidth=-2;turn.Rebuild();Check(turn.outerFloorWidth>=.1f,"Minimum width not clamped");
                Preview();
                File.WriteAllText(Path.Combine(Folder,"unity-tests.txt"),"PASS\n"+DateTime.Now.ToString("s")+"\n48 combinations: left/right, 45/90/180 degrees, smoothing and interior fill on/off, narrow/wide outer floor, both straight extensions.\nValid triangles/normals/UVs/tangents; shared solid collision; wall/floor seam; no internal outer facade; side height transitions; wall height adjustment; disabled option preserves geometry; Undo/Redo; serialized settings; positive width.\n");
                Debug.Log("Virage_Releve : sol exterieur reglable, raccords et collisions verifies.");
            }catch(Exception e){File.WriteAllText(Path.Combine(Folder,"unity-tests.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally{if(go!=null)Object.DestroyImmediate(go);if(copy!=null)Object.DestroyImmediate(copy);EditorSceneManager.ClosePreviewScene(scene);}
        }
        static void Preview()
        {
            var preview=new PreviewRenderUtility();Texture2D image=null;
            try{
                var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SonicBankedTurnBuilder.PrefabPath));preview.AddSingleGO(go);
                var turn=go.GetComponent<SonicBankedTurn>();turn.outerFloor=true;turn.outerFloorWidth=9;turn.wallHeight=10;turn.smoothSides=true;turn.fillInterior=false;turn.entryLength=6;turn.exitLength=6;turn.Rebuild();
                var center=go.GetComponent<MeshFilter>().sharedMesh.bounds.center;
                preview.camera.transform.position=center+new Vector3(-55,65,-60);preview.camera.transform.LookAt(center);
                preview.camera.fieldOfView=42;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=250;
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.12f,.19f,.25f);
                preview.lights[0].intensity=1.3f;preview.lights[0].transform.rotation=Quaternion.Euler(48,-30,0);preview.lights[1].intensity=.8f;preview.ambientColor=new Color(.65f,.65f,.65f);
                preview.BeginStaticPreview(new Rect(0,0,1100,800));preview.Render();image=preview.EndStaticPreview();
                File.WriteAllBytes(Path.Combine(Folder,"Virage_Sol_Exterieur.png"),image.EncodeToPNG());
            }finally{if(image!=null)Object.DestroyImmediate(image);preview.Cleanup();}
        }
    }
}
