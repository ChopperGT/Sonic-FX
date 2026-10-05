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
    // Explicit request only; verification never edits the open level or prefab.
    [InitializeOnLoad]
    static class SonicCubeParallelRowsVerification
    {
        const string Reports="C:/Users/Lecle/Documents/Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/CubeRows";
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static SonicCubeParallelRowsVerification(){EditorApplication.update+=Ready;}
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        static void Ready()
        {
            string request=Path.Combine(Reports,"request.txt");
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request))return;
            File.Delete(request);Verify();
        }
        [MenuItem("Tools/Sonic FX/Structures/Verifier les rangees paralleles du Cube")]
        static void Verify()
        {
            var scene=EditorSceneManager.NewPreviewScene();GameObject go=null;UnityEditor.Editor editor=null;
            var active=SceneManager.GetActiveScene();var selection=Selection.objects;bool dirty=active.isDirty;
            try{
                var source=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Structure/CubeEditable/Cube_Source.asset");Check(source!=null,"Cube source missing");
                go=new GameObject("Cube parallel rows verification");SceneManager.MoveGameObjectToScene(go,scene);
                var cube=go.AddComponent<SonicEditableCube>();cube.Initialize(source);cube.meshSubdivisions=1;Check(cube.Rebuild(),cube.LastError);
                Func<int,int,int,int> index=(x,y,z)=>cube.PointIndex(x,y,z);
                Action<int[],Vector3,int,string> axis=(points,view,expected,name)=>Check(SonicEditableRampEditor.ParallelRowAxis(cube,points,view)==expected,name);
                axis(new[]{index(0,2,2),index(1,2,2),index(2,2,2)},Vector3.forward,1,"Wall X line must create another Y row");
                axis(new[]{index(2,2,0),index(2,2,1),index(2,2,2)},Vector3.right,1,"Wall Z line must create another Y row");
                axis(new[]{index(0,2,2),index(1,2,2),index(2,2,2)},Vector3.down,2,"Top X line must create another Z row");
                axis(new[]{index(2,2,0),index(2,2,1),index(2,2,2)},Vector3.down,0,"Top Z line must create another X row");
                axis(new[]{index(2,0,2),index(2,1,2),index(2,2,2)},Vector3.forward,0,"Vertical wall line must create an X column");
                axis(new[]{index(2,0,2),index(2,1,2),index(2,2,2)},Vector3.right,2,"Vertical side line must create a Z column");
                axis(new[]{index(2,2,2)},Vector3.forward,1,"Single wall point");
                axis(new[]{index(2,2,2)},Vector3.down,2,"Single top point");
                axis(new[]{index(0,0,2),index(2,2,2)},Vector3.down,1,"Selected wall plane overrides camera");
                axis(new[]{index(0,2,0),index(2,2,2)},Vector3.forward,2,"Selected top plane overrides camera");
                cube.transform.rotation=Quaternion.Euler(23,57,11);
                axis(new[]{index(0,2,2),index(2,2,2)},cube.transform.InverseTransformDirection(cube.transform.forward),1,"Rotated cube still inserts across local line");
                cube.MovePoints(new[]{index(1,2,1)},Vector3.up*source.bounds.size.y*.06f);Check(cube.Rebuild(),cube.LastError);
                var before=go.GetComponent<MeshFilter>().sharedMesh.vertices;float tolerance=source.bounds.size.magnitude*.00001f;
                editor=UnityEditor.Editor.CreateEditor(cube);var type=editor.GetType();Check(type==typeof(SonicEditableRampEditor),"Shared inspector missing");
                var chosen=(HashSet<int>)type.GetField("selected",Private).GetValue(editor);
                chosen.Add(index(0,2,2));chosen.Add(index(1,2,2));chosen.Add(index(2,2,2));
                var axisField=type.GetField("insertionAxis",Private);axisField.SetValue(editor,SonicEditableRampEditor.ParallelRowAxis(cube,chosen,Vector3.forward));
                var add=type.GetMethod("AddRow",Private);
                for(int count=4;count<=8;count++){
                    Check((bool)add.Invoke(editor,new object[]{cube}),"Inspector row insertion failed");
                    Check(cube.PointCounts==new Vector3Int(3,count,3),"Points were added along the selected X line instead of a new Y row");
                    Check((int)axisField.GetValue(editor)==1,"Successive additions switched axis");
                    Check(chosen.Count==9,"New row is not selected");
                    var ys=new HashSet<int>();foreach(int i in chosen)ys.Add((i/cube.PointCounts.x)%cube.PointCounts.y);Check(ys.Count==1,"Selected controls do not form one row");
                    var mesh=go.GetComponent<MeshFilter>().sharedMesh;Check(mesh.vertices.Length==before.Length,"Mesh density changed");
                    var current=mesh.vertices;for(int i=0;i<before.Length;i++)Check(Vector3.Distance(before[i],current[i])<tolerance,"Adding rows changes existing deformed shape");
                    Check(go.GetComponent<MeshCollider>().sharedMesh==mesh,"Collision mesh changed independently");
                }
                // Manual direction must continue to work when the automatic option is off.
                type.GetField("automaticRowAxis",Private).SetValue(editor,false);axisField.SetValue(editor,0);
                type.GetMethod("UpdateRowAxis",Private).Invoke(editor,new object[]{cube});Check((int)axisField.GetValue(editor)==0,"Manual axis overwritten");
                type.GetMethod("SuggestPosition",Private).Invoke(editor,new object[]{cube,false});
                Check((bool)add.Invoke(editor,new object[]{cube}) && cube.PointCounts==new Vector3Int(4,8,3),"Manual X insertion broken");
                Check(SceneManager.GetActiveScene()==active && active.isDirty==dirty,"Open level changed");
                Check(Selection.objects.Length==selection.Length,"User selection changed");for(int i=0;i<selection.Length;i++)Check(Selection.objects[i]==selection[i],"User selection changed");
                Directory.CreateDirectory(Reports);File.WriteAllText(Path.Combine(Reports,"verification.txt"),"PASS: wall/top/vertical/single-point/face/rotated selections choose a perpendicular insertion axis. Five successive Inspector additions create distinct parallel Y rows, preserve the deformed mesh and collider, retain their axis and select the new row. Manual X override works. Open level and user selection preserved.\n"+DateTime.Now.ToString("s"));
            }catch(Exception e){Directory.CreateDirectory(Reports);File.WriteAllText(Path.Combine(Reports,"verification.txt"),"FAIL\n"+e);}
            finally{if(editor!=null)Object.DestroyImmediate(editor);if(go!=null){Undo.ClearUndo(go.GetComponent<SonicEditableCube>());Object.DestroyImmediate(go);}EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
