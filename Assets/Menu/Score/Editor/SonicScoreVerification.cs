using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using SonicFX.Menu;
using SonicFX.Score;
using Object=UnityEngine.Object;

namespace SonicFX.Score.Editor
{
    [InitializeOnLoad] public static class SonicScoreVerification
    {
        static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/RingLives");
        static SonicScoreVerification(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)return;
            EditorApplication.update-=Ready;if(!File.Exists(Path.Combine(Folder,"unity-report.txt")))Verify();
        }
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        static object Invoke(object obj,string method,params object[] args)=>obj.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,args);
        static Dictionary<FieldInfo,object> Snapshot(Type type)=>type.GetFields(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Where(f=>!f.IsLiteral && !f.IsInitOnly).ToDictionary(f=>f,f=>f.GetValue(null));
        static void Set(string property,object value)=>typeof(SonicXProgress).GetField("<"+property+">k__BackingField",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,value);
        [MenuItem("Tools/Sonic FX/Score/Verifier le score et le bilan")]
        static void Verify()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            Directory.CreateDirectory(Folder);var saved=Snapshot(typeof(SonicXProgress));var scoreSaved=Snapshot(typeof(SonicLevelScore));int rings=Objects_Interaction.RingAmount;
            var scene=EditorSceneManager.NewPreviewScene();var next=EditorSceneManager.NewPreviewScene();
            GameObject Create(string name){var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,scene);return go;}
            try
            {
                Set("Lives",3);Set("TotalScore",0L);typeof(SonicXProgress).GetField("storySession",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,false);
                SonicLevelScore.BeginLevel(scene);Objects_Interaction.RingAmount=0;
                var enemy=Create("Test enemy");enemy.SetActive(false);var health=enemy.AddComponent<EnemyHealth>();health.MaxHealth=2;health.ScoreOnDefeat=200;Invoke(health,"Awake");
                Check(!(bool)Invoke(health,"TakeDamage",1) && SonicLevelScore.Current==0,"No score on nonfatal hit");
                Check((bool)Invoke(health,"TakeDamage",1) && SonicLevelScore.Current==200,"Score on lethal hit");
                Check(!(bool)Invoke(health,"TakeDamage",100) && SonicLevelScore.Current==200,"Enemy counted once");
                var monitor=Create("Monitor").AddComponent<MonitorData>();monitor.ScoreOnDestroy=250;
                Check((bool)Invoke(monitor,"ClaimScore"),"Monitor reward");Check(!(bool)Invoke(monitor,"ClaimScore") && SonicLevelScore.Current==450,"Monitor counted once");
                var ring=Create("Ring");Check(SonicLevelScore.CollectRing(ring),"Ring collection");Check(!SonicLevelScore.CollectRing(ring) && SonicLevelScore.Current==460 && Objects_Interaction.RingAmount==1,"Ring counted once");
                SonicLevelScore.AddRings(10);Check(SonicLevelScore.Current==560 && Objects_Interaction.RingAmount==11,"Monitor rings give ten points each");
                SonicLevelScore.BeginLevel(scene);Check(SonicLevelScore.Current==560,"Second player initialization does not reset score");
                Check(SonicLevelScore.CalculateTimeBonus(0,10000,120,300)==10000,"Time bonus before ideal");
                Check(SonicLevelScore.CalculateTimeBonus(120,10000,120,300)==10000,"Time bonus at ideal");
                Check(SonicLevelScore.CalculateTimeBonus(150,10000,120,300)==8333,"Time bonus 2m30");
                Check(SonicLevelScore.CalculateTimeBonus(180,10000,120,300)==6667,"Time bonus 3m");
                Check(SonicLevelScore.CalculateTimeBonus(240,10000,120,300)==3333,"Time bonus 4m");
                Check(SonicLevelScore.CalculateTimeBonus(300,10000,120,300)==0 && SonicLevelScore.CalculateTimeBonus(360,10000,120,300)==0,"Time bonus at/after limit");
                Check(SonicLevelScore.CalculateTimeBonus(90,6000,60,120)==3000,"Custom level timings");
                Check(SonicLevelScore.CalculateTimeBonus(180,0,120,300)==0,"Disabled time bonus");
                SonicLevelScore.Tick(0,false);SonicLevelScore.Tick(3,true);Check(SonicLevelScore.Elapsed==0,"Pause/death time excluded");
                var result=SonicLevelScore.Complete(73);Check(result.levelScore==560 && result.ringBonus==730 && result.timeBonus==10000 && result.noDeathBonus==1000 && result.totalScore==12290 && SonicXProgress.TotalScore==12290,"73 rings bonus and total");
                Check(SonicLevelScore.Complete(73)==result && SonicXProgress.TotalScore==12290,"Duplicate goal has no effect");SonicLevelScore.AddPoints(1000);Check(SonicLevelScore.Current==560,"No scoring after goal");
                SonicLevelScore.BeginLevel(next);Check(SonicLevelScore.Current==0,"Next level reset");Set("TotalScore",38990L);SonicLevelScore.AddPoints(10);SonicLevelScore.Complete(0);
                Check(SonicXProgress.TotalScore==50000 && SonicXProgress.Lives==4,"Exact 50k threshold grants one life");SonicLevelScore.Complete(0);Check(SonicXProgress.Lives==4,"No duplicate life");
                Check(SonicLevelScore.Calculate(100010,0,49990).bonusLives==3,"Multiple thresholds");Check(SonicLevelScore.Calculate(1,0,50000).bonusLives==0,"Already earned threshold not awarded again");
                Check(SonicXProgress.TryParse("{\"version\":1,\"character\":\"sonic\",\"scene\":\"level\"}",out var old) && old.lives==3 && old.totalScore==0 && old.version==3,"V1 migration");
                Check(SonicXProgress.TryParse("{\"version\":2,\"lives\":2,\"character\":\"sonic\",\"scene\":\"level\"}",out old) && old.lives==2 && old.totalScore==0,"V2 migration");
                var data=new StorySave{scene=SonicXProgress.FirstLevel,totalScore=150123,lives=6};Check(SonicXProgress.TryParse(JsonUtility.ToJson(data),out var restored) && restored.totalScore==150123 && restored.lives==6,"Score and lives save roundtrip");
                var defaults=new Dictionary<string,int>{
                    {"Assets/Prototyping/PrototypeEnemies/[Enemy] - Motobug.prefab",50},
                    {"Assets/Ennemy/Chameleon/Cameleon_Sol.prefab",200},{"Assets/Ennemy/Chameleon/Cameleon_Mur.prefab",200},
                    {"Assets/Ennemy/crab/Crab_Combat/Crab_Ennemi/Crab_Ennemi.prefab",50},
                    {"Assets/Ennemy/BuzzBomber/Guepe_Generee 1/Guepe_Robot_Parcours.prefab",100},
                    {"Assets/Ennemy/Piranha/Piranha_Pret/Piranha_Ennemi.prefab",100}};
                foreach(var pair in defaults){var asset=AssetDatabase.LoadAssetAtPath<GameObject>(pair.Key);Check(asset!=null && asset.GetComponentInChildren<EnemyHealth>(true).ScoreOnDefeat==pair.Value,"Prefab points: "+pair.Key);}
                Check(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BumperEngineV1/ObjectPrefabs/RingMonitor.prefab").GetComponent<MonitorData>().ScoreOnDestroy==500,"Ring monitor 500");
                Check(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BumperEngineV1/ObjectPrefabs/ShieldMonitor.prefab").GetComponent<MonitorData>().ScoreOnDestroy==250,"Shield monitor 250");
                SonicLevelScore.BeginLevel(scene);Set("Lives",3);Set("TotalScore",0L);
                SonicLevelScore.Tick(180,false);Check(SonicLevelScore.Elapsed==180,"Gameplay timer");
                var player=Create("Death tracking");player.SetActive(false);var hurt=player.AddComponent<HurtControl>();
                Invoke(hurt,"RegisterDeathOnce");Invoke(hurt,"RegisterDeathOnce");Check(SonicLevelScore.Deaths==1 && SonicXProgress.Lives==2,"Death counted once");
                SonicLevelScore.BeginLevel(scene);Check(SonicLevelScore.Deaths==1 && SonicLevelScore.Elapsed==180,"Checkpoint or same-scene init preserves death/time");
                result=SonicLevelScore.Complete(0);Check(result.noDeathBonus==0 && result.timeBonus==6667 && result.totalScore==6667,"Death removes only no-death reward");
                SonicLevelScore.Tick(20,false);Check(SonicLevelScore.Elapsed==180,"Timer stopped at goal");
                SonicLevelScore.BeginLevel(next);Check(SonicLevelScore.Deaths==0 && SonicLevelScore.Elapsed==0,"Next level resets deaths/time");
                Set("TotalScore",48000L);Set("Lives",3);SonicLevelScore.Tick(270,false);result=SonicLevelScore.Complete(0);
                Check(result.timeBonus==1667 && result.noDeathBonus==1000 && result.totalScore==50667 && SonicXProgress.Lives==4,"New bonuses contribute to 50k life");
                SonicLevelScore.BeginLevel(scene);Set("Lives",3);Set("TotalScore",1234L);Objects_Interaction.RingAmount=0;
                SonicLevelScore.AddRings(99);Check(SonicXProgress.Lives==3 && SonicLevelScore.RingsCollected==99,"No life before 100");
                var hundredth=Create("100th ring");Check(SonicLevelScore.CollectRing(hundredth) && SonicXProgress.Lives==4,"100th pickup immediately grants one life");
                Check(!SonicLevelScore.CollectRing(hundredth) && SonicXProgress.Lives==4,"Repeated collision cannot grant duplicate life");
                SonicLevelScore.ResetRingLifeProgress();Objects_Interaction.RingAmount=0;SonicLevelScore.AddRings(99);Check(SonicXProgress.Lives==4 && SonicLevelScore.RingsTowardLife==99,"After damage, 99 new rings do not grant a life");
                SonicLevelScore.AddRings(1);Check(SonicXProgress.Lives==5 && SonicLevelScore.RingsCollected==200,"200 collected rings grant second life");
                SonicLevelScore.AddRings(250);Check(SonicXProgress.Lives==7 && SonicLevelScore.RingsCollected==450,"Multi-threshold ring reward");
                SonicLevelScore.AddRings(0);SonicLevelScore.AddRings(-10);Check(SonicXProgress.Lives==7 && SonicLevelScore.RingsCollected==450,"Invalid ring increments ignored");
                SonicLevelScore.BeginLevel(scene);Check(SonicLevelScore.RingsCollected==450,"Same-level initialization keeps ring progress");
                SonicXProgress.LoseLife();SonicLevelScore.RecordDeath();SonicLevelScore.BeginLevel(scene);Check(SonicLevelScore.RingsCollected==450 && SonicLevelScore.RingsTowardLife==0 && SonicLevelScore.Deaths==1,"Death/checkpoint resets life progress and preserves statistics");
                SonicLevelScore.AddRings(99);Check(SonicXProgress.Lives==6,"Before monitor threshold");
                SonicLevelScore.AddRings(10);Check(SonicXProgress.Lives==7 && SonicLevelScore.RingsCollected==559 && SonicLevelScore.RingsTowardLife==9,"Monitor rings cross new threshold after death");
                Check(SonicLevelScore.Deaths==1,"Ring life does not erase death for no-death bonus");
                string testKey="SonicFX.Tests.RingLives."+Guid.NewGuid().ToString("N");
                try {
                    PlayerPrefs.SetString(testKey,JsonUtility.ToJson(new StorySave{scene=SonicXProgress.FirstLevel,lives=3,totalScore=1234}));
                    typeof(SonicXProgress).GetMethod("PersistRemainingLives",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{testKey});
                    Check(SonicXProgress.TryParse(PlayerPrefs.GetString(testKey),out var lifeSave) && lifeSave.lives==7 && lifeSave.totalScore==1234,"Life persistence preserves score and writes increased lives");
                }finally{PlayerPrefs.DeleteKey(testKey);PlayerPrefs.Save();}
                SonicLevelScore.Complete(0);int completedLives=SonicXProgress.Lives;SonicLevelScore.AddRings(100);Check(SonicXProgress.Lives==completedLives && SonicLevelScore.RingsCollected==559,"No ring life after completion");
                SonicLevelScore.BeginLevel(next);Check(SonicLevelScore.RingsCollected==0,"New level resets ring progress");
                SonicLevelScore.AddRings(100);Check(SonicXProgress.Lives==completedLives+1,"New level can earn a new 100-ring life");
                Set("Lives",0);SonicXProgress.GainLives(1);SonicLevelScore.AddRings(100);Check(SonicXProgress.Lives==0 && SonicLevelScore.RingsCollected==100,"Game over cannot be revived by pending ring rewards");
                Set("Lives",4);
                var preview=SonicLevelScore.Calculate(12750,73,40500,null,6667,1000,180,0);
                Render(scene,Create("Results").AddComponent<SonicLevelResults>(),preview);
                File.WriteAllText(Path.Combine(Folder,"unity-report.txt"),"PASS\nEnemy/monitor/ring rewards and duplicate guards; HUD source connected; 73 rings = 730; totals; 50k and multi-threshold lives; repeated goal; next level reset; v1/v2 migration and v3 save roundtrip; prefab defaults; time formula and boundaries; pause/death/goal timer; death counted once and checkpoint continuity; no-death reward; bonus-driven 50k life; five-row results UI rendered; immediate ring lives at 100/200; multi-threshold batches; monitor rings; duplicate guard; damage/death/checkpoint continuity; new-level reset; no post-goal/game-over rewards; saved life count with unchanged score.\n"+DateTime.Now.ToString("s"));
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Folder,"unity-report.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally
            {
                foreach(var pair in saved)pair.Key.SetValue(null,pair.Value);foreach(var pair in scoreSaved)pair.Key.SetValue(null,pair.Value);Objects_Interaction.RingAmount=rings;
                EditorSceneManager.ClosePreviewScene(scene);EditorSceneManager.ClosePreviewScene(next);
            }
        }
        static void Render(Scene scene,SonicLevelResults ui,LevelScoreResult result)
        {
            ui.Build(result);var cameraObject=new GameObject("Preview Camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraObject,scene);var camera=cameraObject.GetComponent<Camera>();camera.scene=scene;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.orthographic=true;camera.orthographicSize=360;camera.nearClipPlane=.01f;camera.farClipPlane=100;
            var rt=new RenderTexture(1280,720,24);var previous=RenderTexture.active;Texture2D texture=null;
            try
            {
                camera.targetTexture=rt;ui.ResultsCanvas.renderMode=RenderMode.ScreenSpaceCamera;ui.ResultsCanvas.worldCamera=camera;ui.ResultsCanvas.planeDistance=1;
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(Folder,"Bilan_Unity.png"),texture.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);if(texture!=null)Object.DestroyImmediate(texture);}
        }
    }
}
