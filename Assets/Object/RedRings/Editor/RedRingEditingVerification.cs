using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SonicFX.Menu;
using SonicFX.Score;
using SonicFX.RedRings;
using Object=UnityEngine.Object;

[InitializeOnLoad] internal static class RedRingEditingVerification
{
    static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/RedRingEditing");
    static readonly BindingFlags Flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static RedRingEditingVerification(){EditorApplication.update+=Ready;}
    static void Ready()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        EditorApplication.update-=Ready;if(!File.Exists(Path.Combine(Folder,"unity-curve-report.txt")))Verify();
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static bool Near(Vector3 a,Vector3 b)=>(a-b).sqrMagnitude<.00001f;
    [MenuItem("Tools/Sonic FX/Rings rouges/Verifier l'edition des rings et etoiles")]
    static void Verify()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        Directory.CreateDirectory(Folder);var scene=EditorSceneManager.NewPreviewScene();GameObject root=null;
        var saved=new[]{typeof(SonicXProgress),typeof(SonicLevelScore)}.SelectMany(t=>t.GetFields(Flags)).Where(f=>!f.IsLiteral&&!f.IsInitOnly).ToDictionary(f=>f,f=>f.GetValue(null));
        Undo.IncrementCurrentGroup();int firstGroup=Undo.GetCurrentGroup();int held=Objects_Interaction.RingAmount;
        string path="Assets/Object/RedRings/Editor/Verification_Edit_"+Guid.NewGuid().ToString("N")+".prefab";
        try
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(RedRingBuilder.Route);Check(prefab!=null,"Installed route exists");
            root=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);var route=root.GetComponent<RedRingChallenge>();
            int original=route.rings.Length;Check(original==5,"Original five-ring prefab retained");var originalFirst=route.rings[0];
            root.transform.position=new Vector3(11,20,30);root.transform.rotation=Quaternion.Euler(0,40,0);root.transform.localScale=Vector3.one*2;
            Vector3 last=route.rings[4].transform.position;route.newRingOffset=new Vector3(5,1,2);
            RedRingRouteEditing.SetRingCount(route,7);Undo.FlushUndoRecordObjects();
            Check(route.rings.Length==7&&route.rings.Distinct().Count()==7&&route.GetComponentsInChildren<RedStarRing>().Length==7,"Increasing count creates unique physical rings");
            Check(Near(route.rings[5].transform.position,last+root.transform.TransformVector(route.newRingOffset))&&Near(route.rings[6].transform.position,route.rings[5].transform.position+root.transform.TransformVector(route.newRingOffset)),"New rings appear beside last using parent axes and scale");
            Check(route.rings[0]==originalFirst&&route.rings.Select((r,i)=>r.order==i+1).All(b=>b),"Existing rings and updated order retained");
            Check(route.rings[5].editableStarPositions.Length==0&&route.rings[5].guidePoints.Length==0,"New ring has its own empty guide");
            Undo.PerformUndo();Check(route.rings.Length==5&&route.GetComponentsInChildren<RedStarRing>().Length==5,"Count creation is undoable");
            Undo.PerformRedo();Check(route.rings.Length==7&&route.GetComponentsInChildren<RedStarRing>().Length==7,"Count creation is redoable: refs="+route.rings.Length+", children="+route.GetComponentsInChildren<RedStarRing>().Length+", non-null="+route.rings.Count(r=>r!=null));
            RedRingRouteEditing.SetRingCount(route,3);Undo.FlushUndoRecordObjects();Check(route.rings.Length==3&&route.GetComponentsInChildren<RedStarRing>().Length==3,"Reducing count removes trailing owned rings");
            Undo.PerformUndo();Check(route.rings.Length==7&&route.rings.All(r=>r!=null),"Removed rings and references restored by Undo");
            route.rings=new[]{route.rings[0],route.rings[1],route.rings[2],route.rings[3],route.rings[4],route.rings[4],route.rings[4]};
            RedRingRouteEditing.SetRingCount(route,7);Check(route.rings.Distinct().Count()==7&&route.GetComponentsInChildren<RedStarRing>().Length==7,"Legacy array-size duplicates become real rings without leaving orphan rings");
            var target=route.rings[1];Vector3 start=route.rings[0].WorldCenter;
            Undo.FlushUndoRecordObjects();Undo.IncrementCurrentGroup();RedRingRouteEditing.MakeCurveEditable(start,target,route.starSpacing);Undo.FlushUndoRecordObjects();
            Check(target.curveStars&&target.starCurvePoints.Length==2,"Straight legacy route becomes a curve with two editable interior points");
            Undo.PerformUndo();Check(!target.curveStars,"Curve conversion is undoable");Undo.PerformRedo();Check(target.curveStars,"Curve conversion is redoable");
            var initial=target.starCurvePoints.ToArray();var lineBefore=RedRingStarGuide.BuildCurvePolyline(start,target);var oldStars=RedRingStarGuide.BuildPositions(start,target,route.starSpacing);
            Vector3 desired=target.GuideToWorld(initial[0])+new Vector3(-2,6,3);
            Undo.IncrementCurrentGroup();RedRingRouteEditing.MoveCurvePoint(target,0,desired);Undo.FlushUndoRecordObjects();
            Check(Near(target.GuideToWorld(target.starCurvePoints[0]),desired)&&Near(target.starCurvePoints[1],initial[1]),"Moving one control point preserves every other authored point");
            var line=RedRingStarGuide.BuildCurvePolyline(start,target);var stars=RedRingStarGuide.BuildPositions(start,target,route.starSpacing);
            Check(Near(line[0],lineBefore[0])&&Near(line[line.Length-1],lineBefore[lineBefore.Length-1]),"Both ring anchors stay fixed during deformation");
            Check(line.Max(p=>p.y)>lineBefore.Max(p=>p.y)+1&&!stars.SequenceEqual(oldStars),"Curve and generated stars bend around the edited point");
            Check(stars.All(p=>!float.IsNaN(p.x)&&!float.IsInfinity(p.y))&&stars.Zip(stars.Skip(1),(a,b)=>Vector3.Distance(a,b)).All(d=>d>.01f&&d<=route.starSpacing*1.5f),"Generated stars remain spaced along the curve without clumping");
            Check(Near(stars[stars.Length-1],target.WorldCenter+Vector3.up*.25f),"Curve reaches the next ring");
            Undo.PerformUndo();Check(target.starCurvePoints.Zip(initial,Near).All(v=>v),"Control point movement is undoable");Undo.PerformRedo();Check(Near(target.GuideToWorld(target.starCurvePoints[0]),desired),"Control point movement is redoable");
            Undo.IncrementCurrentGroup();RedRingRouteEditing.SetCurvePointLocked(target,0,true);Undo.FlushUndoRecordObjects();
            Check(RedRingRouteEditing.IsCurvePointLocked(target,0),"Control point can be locked independently");
            var lockedPosition=target.starCurvePoints[0];RedRingRouteEditing.MoveCurvePoint(target,0,desired+Vector3.up);Check(Near(target.starCurvePoints[0],lockedPosition),"Locked point resists accidental movement");
            Undo.PerformUndo();Check(!RedRingRouteEditing.IsCurvePointLocked(target,0),"Control lock is undoable");Undo.PerformRedo();Check(RedRingRouteEditing.IsCurvePointLocked(target,0),"Control lock is redoable");
            var pointsBefore=target.starCurvePoints.ToArray();var locksBefore=target.starCurveLocks.ToArray();
            RedRingRouteEditing.InsertCurvePoint(start,target,0);Check(target.starCurvePoints.Length==3&&RedRingRouteEditing.IsCurvePointLocked(target,0)&&!RedRingRouteEditing.IsCurvePointLocked(target,1),"Inserted control point is unlocked and existing locks stay aligned");
            RedRingRouteEditing.RemoveCurvePoint(target,1);Check(target.starCurvePoints.Zip(pointsBefore,Near).All(v=>v)&&target.starCurveLocks.SequenceEqual(locksBefore),"Removing control point preserves remaining geometry and locks");
            root.transform.position+=new Vector3(2,4,6);root.transform.rotation=Quaternion.Euler(0,90,0);start=route.rings[0].WorldCenter;
            line=RedRingStarGuide.BuildCurvePolyline(start,target);Check(Near(line[0],start+Vector3.up*.25f)&&Near(target.GuideToWorld(target.starCurvePoints[0]),root.transform.TransformPoint(pointsBefore[0])),"Curve follows its nonrotating route parent");
            var beforeSpin=RedRingStarGuide.BuildPositions(start,target,route.starSpacing);target.transform.Rotate(0,75,0);
            Check(beforeSpin.Zip(RedRingStarGuide.BuildPositions(start,target,route.starSpacing),Near).All(v=>v),"Ring rotation cannot spin the guide curve");
            target.transform.position+=new Vector3(3,2,-1);line=RedRingStarGuide.BuildCurvePolyline(start,target);
            Check(Near(line[line.Length-1],target.WorldCenter+Vector3.up*.25f),"Moving destination ring updates curve endpoint automatically");
            var guide=root.AddComponent<RedRingStarGuide>();guide.Show(start,target,route.starSpacing,route.starSize,route.starColor);
            var firstStar=root.GetComponentsInChildren<Transform>().First(t=>t.name=="Etoile du parcours");Check(Near(firstStar.position,RedRingStarGuide.BuildPositions(start,target,route.starSpacing)[0]),"Runtime star renderer matches curve preview exactly");guide.Clear();Object.DestroyImmediate(guide);
            PrefabUtility.SaveAsPrefabAsset(root,path);var roundtrip=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<RedRingChallenge>();
            Check(roundtrip.rings.Length==7&&roundtrip.rings[1].curveStars&&roundtrip.rings[1].starCurvePoints.Zip(target.starCurvePoints,Near).All(v=>v)&&roundtrip.rings[1].starCurveLocks.SequenceEqual(target.starCurveLocks),"Curve geometry and locks survive prefab serialization");
            // Existing hand-authored bends and locked stars become control points, never discarded.
            var legacy=route.rings[2];legacy.manualStars=true;Vector3 pinned=Vector3.Lerp(start,legacy.WorldCenter,.5f)+Vector3.up*5;
            legacy.editableStarPositions=new[]{legacy.WorldToGuide(pinned),legacy.WorldToGuide(legacy.WorldCenter+Vector3.up*.25f)};legacy.lockedStarFollow=new[]{true,false};
            RedRingRouteEditing.MakeCurveEditable(start,legacy,route.starSpacing);
            Check(legacy.curveStars&&legacy.starCurvePoints.Any(p=>Near(legacy.GuideToWorld(p),pinned))&&legacy.starCurveLocks.Any(v=>v),"Converting existing manual path preserves pinned authored positions");
            typeof(SonicXProgress).GetField("storySession",Flags).SetValue(null,true);typeof(SonicXProgress).GetField("<Lives>k__BackingField",Flags).SetValue(null,3);SonicLevelScore.BeginLevel(scene);
            route.Initialize();
            Check(route.Available&&route.rings.Select((r,i)=>r.GetComponent<SphereCollider>().enabled==(i==0)&&r.GetComponentsInChildren<Renderer>(true).Any(v=>v.enabled)==(i==0)).All(b=>b),"Only first ring is visible and collectible before the challenge");
            var playerObject=new GameObject("Verification du nouvel objectif");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(playerObject,scene);playerObject.SetActive(false);var player=playerObject.AddComponent<PlayerBhysics>();
            Check(!route.TryCollect(route.rings[1],player),"Hidden future ring cannot be collected out of order");
            for(int i=0;i<5;i++)
            {
                Check(route.TryCollect(route.rings[i],player),"Pickup "+i);
                Check(route.rings.Select((r,n)=>r.GetComponent<SphereCollider>().enabled==(n==i+1)&&r.GetComponentsInChildren<Renderer>(true).Any(v=>v.enabled)==(n==i+1)).All(b=>b),"Pickup reveals only the next ring: "+i);
            }
            Check(route.Active&&route.CollectedCount==5&&!route.Succeeded&&route.rings.Length==7,"Challenge now requires all seven rings, not the old five");
            route.ResetAttempt();
            Check(route.rings.Select((r,i)=>!r.Collected&&r.GetComponent<SphereCollider>().enabled==(i==0)&&r.GetComponentsInChildren<Renderer>(true).Any(v=>v.enabled)==(i==0)).All(b=>b),"Retry reveals only first ring and clears collection");
            route.TryCollect(route.rings[0],player);route.Advance(route.timeLimit+1);
            Check(!route.Active&&route.CollectedCount==0&&route.rings.Select((r,i)=>r.GetComponent<SphereCollider>().enabled==(i==0)).All(b=>b),"Timeout hides future rings and resets to first");
            File.WriteAllText(Path.Combine(Folder,"unity-curve-report.txt"),"PASS\nIncreasing/decreasing count creates/removes real rings beside the last; unique references and order; parent axes/scale; Undo/Redo; legacy duplicated arrays repaired; stars converted, moved, inserted, removed; parent motion preserved; ring rotation excluded; exact runtime positions; prefab save roundtrip; gameplay goal updated from five to seven; Unity AutoSmooth spline guide; local control deformation with fixed ring anchors; curve conversion/movement/lock Undo and Redo; automatically spaced generated stars; independent control locks; insert/remove geometry and lock alignment; parent transform and destination-ring movement; native curve renderer parity; conversion preserves legacy pinned positions; first-ring-only visibility; sequential revealing of visuals and pickup colliders; retry and timeout visibility. Isolated preview scene; no user scene or save changed.\n"+DateTime.Now.ToString("s"));
            Debug.Log("Rings rouges : courbe fluide, points independants et espacement des etoiles verifies.");
        }
        catch(Exception e){File.WriteAllText(Path.Combine(Folder,"unity-curve-report.txt"),"FAIL\n"+e);Debug.LogException(e);}
        finally
        {
            Undo.FlushUndoRecordObjects();Undo.RevertAllDownToGroup(firstGroup);
            foreach(var pair in saved)pair.Key.SetValue(null,pair.Value);Objects_Interaction.RingAmount=held;
            EditorSceneManager.ClosePreviewScene(scene);if(AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null)AssetDatabase.DeleteAsset(path);
        }
    }
}
