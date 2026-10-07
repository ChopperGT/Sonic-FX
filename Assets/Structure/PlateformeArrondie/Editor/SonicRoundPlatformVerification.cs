using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SonicFX.Structures.Editor
{
    public static class SonicRoundPlatformVerification
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static void Same(Vector3[] a, Vector3[] b, string message)
        {
            Check(a.Length == b.Length, message + " vertex count");
            for (int i = 0; i < a.Length; i++) Check(Vector3.Distance(a[i], b[i]) < .0001f, message + " vertex " + i);
        }
        [MenuItem("Tools/Sonic FX/Structures/Verifier Plateforme arrondie")]
        public static void Verify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null) return;
            Directory.CreateDirectory(SonicRoundPlatformBuilder.Reports);
            var active = SceneManager.GetActiveScene(); bool dirty = active.isDirty;
            var selection = Selection.objects;
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject go = null, copy = null; UnityEditor.Editor editor = null;
            string temporary = SonicRoundPlatformBuilder.Folder + "/Editor/Verification-" + Guid.NewGuid().ToString("N") + ".prefab";
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SonicRoundPlatformBuilder.PrefabPath);
                Check(prefab != null, "Prefab missing");
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var p = go.GetComponent<SonicRoundPlatform>(); var source = p.SourceMesh;
                Check(source != null && source.isReadable && AssetDatabase.Contains(source), "Durable readable source missing");
                Check(p.Points.Length == 27 && p.PointCounts == new Vector3Int(3, 3, 3), "Cube cage missing");
                editor = UnityEditor.Editor.CreateEditor(p);
                Check(editor.GetType().Name == "SonicEditableRampEditor", "Shared Cube point inspector missing");
                Object.DestroyImmediate(editor); editor = null;
                var filter = go.GetComponent<MeshFilter>(); var collider = go.GetComponent<MeshCollider>();
                Check(!collider.convex && !collider.isTrigger, "Solid editable collision missing");
                var material = go.GetComponent<MeshRenderer>().sharedMaterial;
                Check(material != null && material == AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Structure/Cube.prefab").GetComponent<MeshRenderer>().sharedMaterial, "GreenHill Cube material missing");
                Check(Mathf.Abs(source.bounds.size.x - 16) < .001f && Mathf.Abs(source.bounds.size.z - 16) < .001f && Mathf.Abs(source.bounds.size.y - 2) < .001f, "Initial dimensions");
                var v = source.vertices; var tri = source.triangles;
                for (int i = 0; i < tri.Length; i += 3)
                {
                    Vector3 a = v[tri[i]], b = v[tri[i+1]], c = v[tri[i+2]];
                    var n = Vector3.Cross(b-a, c-a);
                    Check(n.sqrMagnitude > .00000001f, "Degenerate polygon");
                    var center = (a+b+c)/3;
                    var facing = center - source.bounds.center;
                    Check(Vector3.Dot(n, facing) > 0, "Inverted surface polygon");
                }
                for (int quality = 0; quality <= 2; quality++)
                {
                    p.meshSubdivisions = quality; Check(p.Rebuild(), p.LastError);
                    Check(p.TriangleCount == tri.Length / 3 * (1 << (quality * 2)), "Subdivision count");
                    Check(filter.sharedMesh == collider.sharedMesh && filter.sharedMesh.uv.Length == filter.sharedMesh.vertexCount, "Collision / texture topology mismatch");
                    if (quality == 0) Same(v, filter.sharedMesh.vertices, "Initial circular shape");
                }
                p.meshSubdivisions = 1; p.Rebuild(); Physics.SyncTransforms();
                for (int i = 0; i < 16; i++)
                {
                    float angle = i * Mathf.PI / 8;
                    var point = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 7.5f;
                    Check(collider.Raycast(new Ray(point + Vector3.up * 3, Vector3.down), out var hit, 5) && Mathf.Abs(hit.point.y) < .001f && hit.normal.y > .999f, "Flat edge support " + i);
                }
                Check(!collider.Raycast(new Ray(new Vector3(7,3,7),Vector3.down),out _,5), "Square corner collision outside circular platform");
                Check(collider.Raycast(new Ray(new Vector3(10,-1,0),Vector3.left),out _,5), "Side collision");
                var before = filter.sharedMesh.vertices;
                for (int axis = 0; axis < 3; axis++)
                {
                    Check(p.InsertPoints(axis, .5f, out _) && p.Rebuild(), "Add row " + axis);
                    Same(before, filter.sharedMesh.vertices, "Insertion preserves round shape");
                    float next = p.SuggestedInsertion(axis);
                    Check(p.CanInsertPoints(axis, next) && p.InsertPoints(axis, next, out _) && p.Rebuild(), "Next distinct parallel row " + axis);
                    Same(before, filter.sharedMesh.vertices, "Repeated insertion preserves shape");
                }
                Check(p.PointCounts == new Vector3Int(5,5,5) && p.Points.Length == 125, "Rows not distinct on all axes");
                var upper = new List<int>(); var counts = p.PointCounts;
                for (int z=0;z<counts.z;z++) for (int x=counts.x/2;x<counts.x;x++) upper.Add(p.PointIndex(x,counts.y-1,z));
                var points = (Vector3[])p.Points.Clone(); var duplicate = upper[0]; upper.Add(duplicate);
                p.MovePoints(upper,Vector3.up*.8f); Check(p.Rebuild(),p.LastError);
                Check(Vector3.Distance(p.Points[duplicate],points[duplicate]+Vector3.up*.8f)<.0001f,"Duplicate group selection moves twice");
                Check(filter.sharedMesh.bounds.max.y>.5f && collider.sharedMesh==filter.sharedMesh,"Curved upper surface / collision not updated");
                PrefabUtility.RecordPrefabInstancePropertyModifications(p);
                copy = new GameObject("Round serialized test"); SceneManager.MoveGameObjectToScene(copy,scene);
                var other = copy.AddComponent<SonicRoundPlatform>();
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(p),other);
                Check(other.Rebuild(),other.LastError); Same(filter.sharedMesh.vertices,copy.GetComponent<MeshFilter>().sharedMesh.vertices,"Serialized independent shape");
                var saved = other.Points[0]; p.MovePoints(new[]{0},Vector3.left*.1f); p.Rebuild();
                Check(other.Points[0]==saved,"Instances share points");
                copy.GetComponent<MeshFilter>().sharedMesh=source;copy.GetComponent<MeshCollider>().sharedMesh=source;
                PrefabUtility.SaveAsPrefabAsset(copy,temporary);
                var loaded=PrefabUtility.LoadPrefabContents(temporary);
                try { var persisted=loaded.GetComponent<SonicRoundPlatform>(); Check(persisted.PointCounts==counts && persisted.Rebuild(),"Saved rows lost"); Same(other.Points,persisted.Points,"Saved shape points"); }
                finally { PrefabUtility.UnloadPrefabContents(loaded); }
                Undo.IncrementCurrentGroup(); Undo.RecordObject(p,"Test round platform row"); var oldCounts=p.PointCounts;
                Check(p.InsertPoints(0,.75f,out _) && p.Rebuild(),"Undo probe"); PrefabUtility.RecordPrefabInstancePropertyModifications(p); Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();Check(p.Rebuild() && p.PointCounts==oldCounts,"Undo loses rows");
                Undo.PerformRedo();Check(p.Rebuild() && p.PointCounts.x==oldCounts.x+1,"Redo loses rows: "+p.PointCounts+" expected "+(oldCounts.x+1)+"; "+p.LastError);Undo.ClearUndo(p);
                p.ResetShape(); p.Resize(new Vector3(20,3,12)); Check(p.Rebuild(),p.LastError);
                Check(Vector3.Distance(filter.sharedMesh.bounds.size,new Vector3(20,3,12))<.001f,"Oval resize / thickness");
                var top=filter.sharedMesh.bounds.max.y;var bottom=filter.sharedMesh.bounds.min.y;
                p.wallDepth=5;Check(p.Rebuild(),p.LastError);
                Check(Mathf.Abs(filter.sharedMesh.bounds.max.y-top)<.001f && Mathf.Abs(filter.sharedMesh.bounds.min.y-bottom+5)<.001f,"Wall extension alters running surface");
                var valid=filter.sharedMesh;for(int i=0;i<p.Points.Length;i++)p.Points[i]=Vector3.zero;
                Check(!p.Rebuild() && collider.sharedMesh==valid,"Invalid deformation destroys valid collision");
                Capture(prefab,false); Capture(prefab,true);
                Check(SceneManager.GetActiveScene()==active && active.isDirty==dirty,"User map changed");
                File.WriteAllText(Path.Combine(SonicRoundPlatformBuilder.Reports,"tests.txt"),"PASS\n"+DateTime.Now.ToString("s")+"\nRound closed outward mesh; Cube inspector; GreenHill material; flat top support and curved side collision; XYZ/repeated rows preserve geometry; group movement; independent shapes; saved/reloaded rows; Undo/Redo; subdivisions and matching collision; oval resize/thickness/depth; invalid cage guard; previews; user map unchanged.\n");
                Debug.Log("Plateforme arrondie installee et verifiee : points du Cube, nouvelles rangees, dimensions, profondeur et collisions.");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(SonicRoundPlatformBuilder.Reports,"tests.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally
            {
                if(editor!=null)Object.DestroyImmediate(editor);
                if(go!=null){Undo.ClearUndo(go.GetComponent<SonicRoundPlatform>());Object.DestroyImmediate(go);}
                if(copy!=null)Object.DestroyImmediate(copy);
                if(AssetDatabase.LoadAssetAtPath<GameObject>(temporary)!=null)AssetDatabase.DeleteAsset(temporary);
                EditorSceneManager.ClosePreviewScene(scene);Selection.objects=selection;
            }
        }
        static void Capture(GameObject prefab,bool edited)
        {
            var preview=new PreviewRenderUtility();Texture2D image=null;
            try
            {
                var go=Object.Instantiate(prefab);preview.AddSingleGO(go);var p=go.GetComponent<SonicRoundPlatform>();
                if(edited)
                {
                    p.InsertPoints(0,.5f,out _);p.InsertPoints(2,.5f,out _);p.Resize(new Vector3(20,2,12));
                    var c=p.PointCounts;var group=new List<int>();
                    for(int z=0;z<c.z;z++)for(int x=c.x/2;x<c.x;x++)group.Add(p.PointIndex(x,c.y-1,z));
                    p.MovePoints(group,Vector3.up*2);p.meshSubdivisions=2;
                }
                Check(p.Rebuild(),p.LastError);
                var b=go.GetComponent<Renderer>().bounds;float size=b.size.magnitude;
                preview.camera.fieldOfView=36;preview.camera.transform.position=b.center+new Vector3(1,.9f,-1).normalized*size*1.2f;preview.camera.transform.LookAt(b.center);
                preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=200;preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.07f,.1f,.15f);
                preview.ambientColor=Color.gray;preview.lights[0].intensity=1.4f;preview.lights[0].transform.rotation=Quaternion.Euler(40,-30,0);preview.lights[1].intensity=.8f;
                preview.BeginStaticPreview(new Rect(0,0,1200,850));preview.Render();image=preview.EndStaticPreview();
                File.WriteAllBytes(Path.Combine(SonicRoundPlatformBuilder.Reports,edited?"plateforme-deformee.png":"plateforme-arrondie.png"),image.EncodeToPNG());
            }
            finally{if(image!=null)Object.DestroyImmediate(image);preview.Cleanup();}
        }
    }
}
