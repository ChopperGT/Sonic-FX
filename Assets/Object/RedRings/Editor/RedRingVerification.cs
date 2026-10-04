using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SonicFX.Menu;
using SonicFX.Score;
using SonicFX.RedRings;
using Object=UnityEngine.Object;

[InitializeOnLoad] internal static class RedRingVerification
{
    internal static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/RedRings");
    const string SaveKey="SonicFX.Story.Save.v1";
    static readonly BindingFlags Flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static RedRingVerification(){EditorApplication.update+=Ready;}
    static void Ready()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(AssetDatabase.LoadAssetAtPath<GameObject>(RedRingBuilder.Route)==null)return;
        EditorApplication.update-=Ready;if(!File.Exists(Path.Combine(Folder,"unity-report.txt")))Verify();
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Set(string property,object value)=>typeof(SonicXProgress).GetField("<"+property+">k__BackingField",Flags).SetValue(null,value);
    static void RestoreLevels(string[] levels)=>typeof(SonicXProgress).GetMethod("RestoreRedRingLevels",Flags).Invoke(null,new object[]{levels});
    [MenuItem("Tools/Sonic FX/Rings rouges/Verifier le defi et la sauvegarde")]
    static void Verify()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        Directory.CreateDirectory(Folder);
        var fields=new[]{typeof(SonicXProgress),typeof(SonicLevelScore)}.SelectMany(t=>t.GetFields(Flags))
            .Where(f=>!f.IsLiteral&&!f.IsInitOnly).ToDictionary(f=>f,f=>f.GetValue(null));
        var completed=(HashSet<string>)typeof(SonicXProgress).GetField("redRingLevels",Flags).GetValue(null);var oldLevels=completed.ToArray();
        bool hadSave=PlayerPrefs.HasKey(SaveKey);string oldSave=PlayerPrefs.GetString(SaveKey,"");
        int held=Objects_Interaction.RingAmount;var active=SceneManager.GetActiveScene();Scene scene=default;
        string path="Assets/Object/RedRings/Editor/Verification_Temp_"+Guid.NewGuid().ToString("N")+".unity";
        try
        {
            // Back up the game-only JSON before exercising the real end-of-level save path.
            File.WriteAllText(Path.Combine(Folder,"save-before-test.json"),hadSave?oldSave:"{}");
            scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            Check(EditorSceneManager.SaveScene(scene,path),"Isolated test scene has a level identity");
            SceneManager.SetActiveScene(active);
            RestoreLevels(null);Set("Lives",3);Set("TotalScore",0L);typeof(SonicXProgress).GetField("LivesGained",Flags).SetValue(null,null);
            typeof(SonicXProgress).GetField("storySession",Flags).SetValue(null,false);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(RedRingBuilder.Route);
            var hidden=((GameObject)PrefabUtility.InstantiatePrefab(prefab,scene)).GetComponent<RedRingChallenge>();hidden.Initialize();
            Check(!hidden.Available&&hidden.rings.All(r=>!r.GetComponent<SphereCollider>().enabled),"Rings disabled outside Story mode");Object.DestroyImmediate(hidden.gameObject);
            typeof(SonicXProgress).GetField("storySession",Flags).SetValue(null,true);
            PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(new StorySave{scene=path}));
            var route=((GameObject)PrefabUtility.InstantiatePrefab(prefab,scene)).GetComponent<RedRingChallenge>();route.Initialize();
            Check(route.Available&&route.rings.Length==5&&route.successSound==null&&route.successMixer!=null,"Ready prefab: five editable rings and empty audio slot");
            Check(route.rings.All(r=>r.rotationSpeed!=0&&r.GetComponent<SphereCollider>().isTrigger&&r.CompareTag("Untagged")),"Rings rotate and cannot act as ordinary rings");
            var playerObject=new GameObject("Sonic de verification");SceneManager.MoveGameObjectToScene(playerObject,scene);playerObject.SetActive(false);
            var player=playerObject.AddComponent<PlayerBhysics>();var hurt=playerObject.AddComponent<HurtControl>();
            SonicLevelScore.BeginLevel(scene);Objects_Interaction.RingAmount=0;
            Check(!route.TryCollect(route.rings[2],player)&&!route.Active,"A later ring cannot start the challenge");
            Check(route.TryCollect(route.rings[0],player)&&route.Active&&route.Remaining==60,"First ring starts configured timer");
            Check(!route.TryCollect(route.rings[0],player)&&!route.TryCollect(route.rings[3],player),"Duplicate and out-of-order triggers rejected");
            route.Advance(0);Check(route.Remaining==60,"Pause freezes timer");route.Advance(61);
            Check(!route.Active&&route.CollectedCount==0&&route.rings.Select((r,i)=>!r.Collected&&r.GetComponent<SphereCollider>().enabled==(i==0)).All(b=>b),"Timeout restores only the first ring for retry");
            route.TryCollect(route.rings[0],player);hurt.isDead=true;route.Advance(0);
            Check(!route.Active&&route.CollectedCount==0,"Death interrupts an unfinished attempt");hurt.isDead=false;
            for(int i=0;i<route.rings.Length;i++)Check(route.TryCollect(route.rings[i],player),"Ordered collection "+i);
            Check(route.rings.All(r=>!r.GetComponent<SphereCollider>().enabled&&!r.GetComponentsInChildren<Renderer>(true).Any(v=>v.enabled)),"Completed route has no visible rings or enabled pickups");
            Check(route.Succeeded&&!route.Active&&SonicXProgress.RedRingRewards==0,"All rings succeed but do not grant an early speed reward");
            Check(Objects_Interaction.RingAmount==0&&SonicLevelScore.Current==0,"Red rings do not increment ordinary rings or score");
            var result=SonicLevelScore.Complete(0,null,0);
            Check(result!=null&&SonicXProgress.RedRingRewards==1&&SonicXProgress.HasRedRingReward(path),"Actual level goal commits one reward");
            SonicLevelScore.Complete(0,null,0);Check(SonicXProgress.RedRingRewards==1,"Repeated goal cannot duplicate reward");
            Check(SonicXProgress.TryRead(out var saved)&&saved.version==5&&saved.redRingLevels.Length==1,"Completed levels saved with adventure");
            RestoreLevels(saved.redRingLevels);Check(SonicXProgress.RedRingRewards==1&&!SonicXProgress.MarkRedRingChallengeComplete(path),"Continue restores bonus and prevents repeating this level");
            var replay=((GameObject)PrefabUtility.InstantiatePrefab(prefab,scene)).GetComponent<RedRingChallenge>();replay.Initialize();Check(!replay.Available,"Completed-level rings hidden on replay");Object.DestroyImmediate(replay.gameObject);
            var stats=new GameObject("Vitesse de verification");SceneManager.MoveGameObjectToScene(stats,scene);stats.SetActive(false);var speed=stats.AddComponent<PlayerBhysics>();
            for(int count=1;count<=7;count++)
            {
                RestoreLevels(Enumerable.Range(0,count).Select(i=>"niveau"+i).ToArray());speed.TopSpeed=40;speed.MaxSpeed=80;SonicXProgress.ApplyRedRingSpeedBonus(speed);
                Check(speed.TopSpeed==40+Math.Min(4,count)*5&&speed.MaxSpeed==80+count,"Reward schedule "+count);
            }
            RestoreLevels(Enumerable.Range(0,250).Select(i=>"niveau"+i).ToArray());speed.TopSpeed=40;speed.MaxSpeed=80;SonicXProgress.ApplyRedRingSpeedBonus(speed);
            Check(speed.MaxSpeed==300&&speed.TopSpeed==60,"Max Speed cap 300 and four Top Speed bonuses only");
            typeof(SonicXProgress).GetField("storySession",Flags).SetValue(null,false);speed.TopSpeed=40;speed.MaxSpeed=80;SonicXProgress.ApplyRedRingSpeedBonus(speed);Check(speed.TopSpeed==40&&speed.MaxSpeed==80,"No speed upgrade in Arcade or direct-level tests");
            Check(SonicXProgress.TryParse("{\"version\":4,\"scene\":\"level\",\"lives\":3,\"character\":\"sonic\"}",out var migrated)&&migrated.redRingLevels.Length==0&&migrated.version==5,"Existing saves migrate without fake rewards");
            RestoreLevels(null);typeof(SonicXProgress).GetField("storySession",Flags).SetValue(null,true);SonicXProgress.BeginRedRingLevel();
            Check(!SonicXProgress.CommitRedRingReward(path),"Finishing without all rings gives no reward");SonicXProgress.MarkRedRingChallengeComplete(path);SonicXProgress.BeginRedRingLevel();Check(!SonicXProgress.CommitRedRingReward(path),"Changing level discards an unvalidated success");
            Render(scene,route);
            File.WriteAllText(Path.Combine(Folder,"unity-report.txt"),"PASS\nReal ring prefab, rotation and trigger setup; Story-only visibility; first pickup starts clock; ordered collection and duplicate guards; pause; timeout/retry; death cancellation; success pending until actual SonicLevelScore.Complete; real save/continue roundtrip; one reward per level; first four +5 Top Speed/+1 Max Speed, later +1 Max Speed; 300 cap; old-save migration; no ordinary-ring score; no reward for failed/unvalidated challenge; guide mesh preview. Original player save and scenes restored.\n"+DateTime.Now.ToString("s"));
            Debug.Log("Defi rings rouges : parcours, chrono, sauvegarde et bonus de vitesse verifies.");
        }
        catch(Exception e){File.WriteAllText(Path.Combine(Folder,"unity-report.txt"),"FAIL\n"+e);Debug.LogException(e);}
        finally
        {
            completed.Clear();foreach(string level in oldLevels)completed.Add(level);foreach(var pair in fields)pair.Key.SetValue(null,pair.Value);
            Objects_Interaction.RingAmount=held;if(hadSave)PlayerPrefs.SetString(SaveKey,oldSave);else PlayerPrefs.DeleteKey(SaveKey);PlayerPrefs.Save();
            if(scene.IsValid())EditorSceneManager.CloseScene(scene,true);if(active.IsValid())SceneManager.SetActiveScene(active);
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(path)!=null)AssetDatabase.DeleteAsset(path);
        }
    }
    static void Render(Scene scene,RedRingChallenge route)
    {
        foreach(var ring in route.rings)typeof(RedStarRing).GetMethod("SetCollected",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ring,new object[]{false});
        var guide=route.gameObject.AddComponent<RedRingStarGuide>();guide.Show(route.rings[0].transform.position,route.rings[1],1.3f,.35f,new Color(1,.8f,.1f));
        foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
        var cameraObject=new GameObject("Camera de verification");SceneManager.MoveGameObjectToScene(cameraObject,scene);
        var camera=cameraObject.AddComponent<Camera>();camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.075f,.13f);
        camera.transform.position=new Vector3(16,8,-27);camera.transform.LookAt(new Vector3(16,1.5f,0));camera.orthographic=true;camera.orthographicSize=6;
        var lightObject=new GameObject("Lumiere de verification");SceneManager.MoveGameObjectToScene(lightObject,scene);var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.cullingMask=1<<30;light.transform.rotation=Quaternion.Euler(30,-25,0);
        var texture=new RenderTexture(1000,300,24);camera.targetTexture=texture;var previous=RenderTexture.active;Texture2D image=null;
        try{camera.Render();RenderTexture.active=texture;image=new Texture2D(1000,300,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1000,300),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Folder,"preview.png"),image.EncodeToPNG());}
        finally{RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(texture);if(image!=null)Object.DestroyImmediate(image);}
    }
}
