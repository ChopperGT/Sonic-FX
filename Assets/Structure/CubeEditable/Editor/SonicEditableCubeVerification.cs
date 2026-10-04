using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace SonicFX.Structures.Editor
{
    public static class SonicEditableCubeVerification
    {
        static string Folder=>SonicEditableCubeBuilder.Reports;
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        static void Same(Vector3[] a,Vector3[] b,float tolerance,string message){Check(a.Length==b.Length,message+": count");for(int i=0;i<a.Length;i++)Check(Vector3.Distance(a[i],b[i])<tolerance,message+": "+i);}
        static List<int> Layer(SonicEditableCube cube,int axis,int layer){
            var counts=cube.PointCounts;var result=new List<int>();
            for(int z=0;z<counts.z;z++)for(int y=0;y<counts.y;y++)for(int x=0;x<counts.x;x++)
                if((axis==0?x:axis==1?y:z)==layer)result.Add(cube.PointIndex(x,y,z));return result;
        }
        [MenuItem("Tools/Sonic FX/Structures/Verifier Cube editable")]
        public static void Verify(){
            if(EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null){Debug.LogWarning("Quitte Play et le mode Prefab avant de verifier le Cube.");return;}
            Directory.CreateDirectory(Folder);var scene=EditorSceneManager.NewPreviewScene();GameObject go=null,copy=null;SonicEditableCube cube=null;UnityEditor.Editor inspector=null;
            try{
                // The common engine was opened for inheritance: retest the ramp tools too.
                SonicEditableRampPointsVerification.Verify();SonicEditableRampSubdivisionVerification.Verify();
                foreach(string report in new[]{"RampEditablePoints","RampSubdivision"}){
                    var path=Path.Combine(Path.GetDirectoryName(Folder),report,"unity-tests.txt");Check(File.ReadAllText(path).StartsWith("PASS"),"Ramp regression: "+report);
                }
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(SonicEditableCubeBuilder.PrefabPath);Check(prefab!=null,"Cube prefab missing");
                var source=prefab.GetComponent<SonicEditableCube>()?.SourceMesh;Check(source!=null && source.isReadable,"Cube source not installed/readable");
                string beforeMeta=Path.Combine(Folder,"Cube-before-editing.prefab.meta");
                if(File.Exists(beforeMeta))foreach(string line in File.ReadAllLines(beforeMeta))if(line.StartsWith("guid: "))Check(AssetDatabase.AssetPathToGUID(SonicEditableCubeBuilder.PrefabPath)==line.Substring(6).Trim(),"Prefab identity changed");
                var original=source.vertices;float tolerance=source.bounds.size.magnitude*.00001f;int triangles=source.triangles.Length/3;
                go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);cube=go.GetComponent<SonicEditableCube>();
                Check(cube!=null && go.GetComponent<ProBuilderMesh>()==null,"ProBuilder still overwrites Cube");
                Check(cube.SourceMesh==source && cube.PointCounts==new Vector3Int(3,3,3) && cube.Points.Length==27,"Inherited initial cage missing");
                Check(cube.meshSubdivisions==3 && cube.TriangleCount==triangles*64,"Installed quality 3 not active");
                Check(go.transform.localPosition==prefab.transform.localPosition && go.transform.localRotation==prefab.transform.localRotation && go.transform.localScale==prefab.transform.localScale,"Instance placement changed");
                var filter=go.GetComponent<MeshFilter>();var collider=go.GetComponent<MeshCollider>();var extra=go.AddComponent<MeshCollider>();var materials=go.GetComponent<MeshRenderer>().sharedMaterials;
                inspector=UnityEditor.Editor.CreateEditor(cube);Check(inspector is SonicEditableRampEditor,"Cube point inspector not registered");Object.DestroyImmediate(inspector);inspector=null;
                var qualityStats=new List<string>();
                for(int level=0;level<=4;level++){
                    cube.meshSubdivisions=level;Check(cube.Rebuild(),cube.LastError);
                    Check(cube.TriangleCount==triangles*(1<<(level*2)),"Subdivision polygon count wrong");
                    Check(filter.sharedMesh.uv.Length==filter.sharedMesh.vertexCount && filter.sharedMesh.subMeshCount==source.subMeshCount,"Subdivision loses UV/material slots");
                    Check(collider.sharedMesh==filter.sharedMesh && extra.sharedMesh==filter.sharedMesh,"Collision remains coarse");
                    var current=go.GetComponent<MeshRenderer>().sharedMaterials;Check(current.Length==materials.Length,"Material count changed");for(int i=0;i<current.Length;i++)Check(current[i]==materials[i],"Texture material changed");
                    if(level==0)Same(original,filter.sharedMesh.vertices,tolerance,"Original cube shape changed");qualityStats.Add("Level "+level+": "+cube.TriangleCount+" triangles");
                }
                cube.meshSubdivisions=3;Check(cube.Rebuild(),cube.LastError);var unchanged=filter.sharedMesh.vertices;
                for(int axis=0;axis<3;axis++)Check(cube.InsertPoints(axis,.5f,out _) && cube.Rebuild(),"Cannot add points on axis "+axis);
                Check(cube.PointCounts==new Vector3Int(4,4,4) && cube.Points.Length==64,"Variable inherited grid not saved");Same(unchanged,filter.sharedMesh.vertices,tolerance,"Insertion changes cube shape");
                var counts=cube.PointCounts;int control=cube.PointIndex(counts.x/2,counts.y-1,counts.z/2);var before=(Vector3[])cube.Points.Clone();
                cube.MovePoints(new[]{control,control,-1,999999},Vector3.up*source.bounds.size.y*.1f);Check(cube.Rebuild(),cube.LastError);
                Check(Vector3.Distance(cube.Points[control],before[control]+Vector3.up*source.bounds.size.y*.1f)<tolerance,"Group repeats movement");
                bool curved=false;var deformed=filter.sharedMesh.vertices;for(int i=0;i<deformed.Length;i++)curved|=Vector3.Distance(deformed[i],unchanged[i])>tolerance;Check(curved,"Middle surface point has no effect on cube polygons");
                Physics.SyncTransforms();var local=source.bounds.center;local.y=source.bounds.max.y+source.bounds.size.y;
                Check(collider.Raycast(new Ray(go.transform.TransformPoint(local),-go.transform.up),out _,source.bounds.size.y*4),"Curved cube has no top collision");
                PrefabUtility.RecordPrefabInstancePropertyModifications(cube);Check(PrefabUtility.GetPropertyModifications(cube).Length>0,"Scene point edits not recorded as prefab overrides");
                copy=new GameObject("Serialized Cube verification");SceneManager.MoveGameObjectToScene(copy,scene);var duplicate=copy.AddComponent<SonicEditableCube>();
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(cube),duplicate);Check(duplicate.Rebuild(),duplicate.LastError);Same(deformed,copy.GetComponent<MeshFilter>().sharedMesh.vertices,tolerance,"Inherited private controls lost on serialization");
                Check(duplicate.PointCounts==counts && duplicate.meshSubdivisions==3,"Count/density fields lost");var separate=duplicate.Points[control];cube.MovePoints(new[]{control},Vector3.up*.01f);Check(duplicate.Points[control]==separate,"Instances share control arrays");cube.Rebuild();
                Undo.IncrementCurrentGroup();Undo.RecordObject(cube,"Verification: add Cube points");var oldCounts=cube.PointCounts;var oldShape=filter.sharedMesh.vertices;
                Check(cube.InsertPoints(0,.25f,out _) && cube.Rebuild(),"Undo probe add");PrefabUtility.RecordPrefabInstancePropertyModifications(cube);Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();Check(cube.Rebuild() && cube.PointCounts==oldCounts,"Undo fails on inherited controls");Same(oldShape,filter.sharedMesh.vertices,tolerance,"Undo loses previous cube shape");
                Undo.PerformRedo();Check(cube.Rebuild() && cube.PointCounts.x==oldCounts.x+1,"Redo loses new row: counts="+cube.PointCounts+", points="+cube.Points.Length+", error="+cube.LastError);Undo.ClearUndo(cube);
                // Each of the six sides can be edited; the opposite face stays in place.
                for(int axis=0;axis<3;axis++)for(int end=0;end<2;end++){
                    cube.ResetShape();cube.Rebuild();var b=source.bounds;var opposite=b.center;opposite[axis]=end==0?b.max[axis]:b.min[axis];
                    var expected=cube.DeformPoint(opposite);var delta=Vector3.zero;delta[axis]=b.size[axis]*.05f*(end==0?-1:1);
                    cube.MovePoints(Layer(cube,axis,end==0?0:2),delta);Check(cube.Rebuild(),"Face edit "+axis+" / "+end+": "+cube.LastError);
                    Check(Vector3.Distance(cube.DeformPoint(opposite),expected)<tolerance,"One face moves its opposite");
                    var side=b.center;side[axis]=end==0?b.min[axis]:b.max[axis];Check(Vector3.Distance(cube.DeformPoint(side),side+delta)<tolerance,"Face control does not move surface");
                }
                cube.ResetShape();cube.Resize(Vector3.Scale(source.bounds.size,new Vector3(1.2f,.8f,1.4f)));Check(cube.Rebuild(),cube.LastError);
                var size=filter.sharedMesh.bounds.size;Check(Vector3.Distance(size,Vector3.Scale(source.bounds.size,new Vector3(1.2f,.8f,1.4f)))<tolerance,"Resize does not follow all three axes");
                var noDepth=filter.sharedMesh.vertices;var meshBounds=filter.sharedMesh.bounds;cube.wallDepth=20;Check(cube.Rebuild(),cube.LastError);
                Check(Mathf.Abs(filter.sharedMesh.bounds.min.y-(meshBounds.min.y-20))<tolerance && Mathf.Abs(filter.sharedMesh.bounds.max.y-meshBounds.max.y)<tolerance,"Depth changes top / misses bottom");
                cube.wallDepth=0;Check(cube.Rebuild(),cube.LastError);Same(noDepth,filter.sharedMesh.vertices,tolerance,"Zero depth loses shape");
                var valid=filter.sharedMesh;for(int i=0;i<cube.Points.Length;i++)cube.Points[i]=Vector3.zero;Check(!cube.Rebuild() && collider.sharedMesh==valid,"Collapsed cage destroys valid collision");
                cube.ResetShape();cube.Rebuild();cube.enabled=false;Check(filter.sharedMesh==source,"Disable does not restore source");cube.enabled=true;
                Check(filter.sharedMesh!=source && collider.sharedMesh==filter.sharedMesh && cube.TriangleCount==triangles*64,"Inherited lifecycle does not regenerate cube");
                Same(original,source.vertices,tolerance,"Source asset modified by scene edits");
                Render(prefab,scene,false);Render(prefab,scene,true);
                File.WriteAllText(Path.Combine(Folder,"unity-tests.txt"),"PASS\n"+DateTime.Now.ToString("s")+"\n"+string.Join("\n",qualityStats)+"\nPrefab identity/transform/materials; original source; inherited point inspector and lifecycle; all six faces; XYZ additions preserve shape; curved middle point; physical top collision; matching duplicate colliders; UVs; scene overrides; serialization; independent instances; Undo/Redo; resize; depth; invalid cage protection; previews; ramp point/subdivision regression tests.\n");
                Debug.Log("Cube installe et verifie : points sur les six faces, ajouts X / Y / Z, polygones et collisions editables.");
            }catch(Exception e){File.WriteAllText(Path.Combine(Folder,"unity-tests.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally{if(inspector!=null)Object.DestroyImmediate(inspector);if(cube!=null)Undo.ClearUndo(cube);if(go!=null)Object.DestroyImmediate(go);if(copy!=null)Object.DestroyImmediate(copy);EditorSceneManager.ClosePreviewScene(scene);}
        }
        static void Render(GameObject prefab,Scene isolatedScene,bool edited){
            var preview=new PreviewRenderUtility();Texture2D image=null;
            try{
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,isolatedScene);preview.AddSingleGO(go);go.transform.position=Vector3.zero;var cube=go.GetComponent<SonicEditableCube>();
                if(edited){
                    Check(cube.InsertPoints(0,.5f,out _) && cube.InsertPoints(2,.5f,out _),"Preview point insertion");var counts=cube.PointCounts;var selected=new List<int>();
                    for(int z=1;z<counts.z-1;z++)for(int x=1;x<counts.x-1;x++)selected.Add(cube.PointIndex(x,counts.y-1,z));
                    cube.MovePoints(selected,Vector3.up*sourceHeight(cube)*.45f);
                }
                Check(cube.Rebuild(),cube.LastError);var b=go.GetComponent<Renderer>().bounds;float size=b.size.magnitude;
                preview.camera.fieldOfView=40;preview.camera.transform.position=b.center+new Vector3(1,.7f,-1).normalized*size*1.25f;preview.camera.transform.LookAt(b.center);
                preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=size*10+100;preview.camera.clearFlags=CameraClearFlags.Color;preview.camera.backgroundColor=new Color(.06f,.1f,.16f);
                preview.ambientColor=Color.gray;preview.lights[0].intensity=1.4f;preview.lights[0].transform.rotation=Quaternion.Euler(45,-30,0);preview.lights[1].intensity=.8f;
                preview.BeginStaticPreview(new Rect(0,0,1000,750));preview.Render();image=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(Folder,edited?"cube-edited-preview.png":"cube-preview.png"),image.EncodeToPNG());
            }finally{if(image!=null)Object.DestroyImmediate(image);preview.Cleanup();}
        }
        static float sourceHeight(SonicEditableCube cube)=>cube.SourceMesh.bounds.size.y;
    }
}
