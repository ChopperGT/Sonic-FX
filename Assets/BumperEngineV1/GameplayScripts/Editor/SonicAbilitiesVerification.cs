using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SonicFX.Menu;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class SonicAbilitiesVerification
{
    static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/SonicAbilities");
    static SonicAbilitiesVerification(){EditorApplication.update+=Ready;}
    static void Ready()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)return;
        EditorApplication.update-=Ready;
        if(!File.Exists(Path.Combine(Folder,"unity-report.txt")))Verify();
    }
    static void Check(bool value,string label){if(!value)throw new Exception(label);}
    static void Set(string property,object value)=>typeof(SonicXProgress).GetField("<"+property+">k__BackingField",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,value);
    static void Invoke(object component,string method)=>component.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(component,null);
    [MenuItem("Tools/Sonic FX/Capacites/Verifier Sonic limite")]
    public static void Verify()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        Directory.CreateDirectory(Folder);
        var fields=typeof(SonicXProgress).GetFields(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Where(f=>!f.IsLiteral && !f.IsInitOnly).ToDictionary(f=>f,f=>f.GetValue(null));
        var homingTarget=HomingAttackControl.TargetObject;var lightTarget=LightDashControl.TargetObject;
        var scene=EditorSceneManager.NewPreviewScene();GameObject go=null;
        string key="SonicFX.Tests.Abilities."+Guid.NewGuid().ToString("N");
        try {
            Set("Lives",3);Set("UnlockedAbilities",SonicAbility.None);
            typeof(SonicXProgress).GetField("storySession",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,false);
            typeof(SonicXProgress).GetField("pending",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,null);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BumperEngineV1/PlayerPrefabs/PO_Mania.prefab");
            Check(prefab!=null,"Sonic prefab exists");go=Object.Instantiate(prefab);SceneManager.MoveGameObjectToScene(go,scene);
            var actions=go.GetComponentInChildren<ActionManager>(true);Check(actions!=null,"Sonic action manager exists");
            Check(actions.AvailableAbilities==SonicAbility.None,"Default Sonic has no special abilities");
            var player=actions.GetComponent<PlayerBhysics>();var body=actions.GetComponent<Rigidbody>();
            var speed=player.TopSpeed;var jump=actions.Action01.JumpSpeed;
            actions.ChangeAction(0);Check(actions.Action00.enabled,"Normal movement remains enabled");
            player.isRolling=true;
            foreach(int id in new[]{2,3,5,6,7,8}){
                Check(!actions.CanChangeAction(id),"Special action locked: "+id);actions.ChangeAction(id);
                Check(actions.Action==0 && actions.Action00.enabled,"Locked action cannot interrupt movement: "+id);
            }
            actions.Action02.IsAirDash=true;Check(!actions.CanChangeAction(2),"Air dash is locked separately");actions.Action02.IsAirDash=false;
            // Direct calls must not produce a launch, sound or particle effect.
            body.linearVelocity=new Vector3(2,3,4);var velocity=body.linearVelocity;
            actions.Action02.InitialEvents();actions.Action03.InitialEvents();actions.Action06.InitialEvents();actions.Action07.InitialEvents();
            if(actions.Action08!=null)actions.Action08.InitialEvents();
            Check(body.linearVelocity==velocity && actions.Action==0,"Locked initial events have no gameplay side effects");
            Check(player.isRolling,"Rolling remains available");
            actions.ChangeAction(1);Check(actions.Action==1 && actions.Action01.enabled,"Normal jump remains available");
            actions.ChangeAction(4);Check(actions.Action==4 && actions.Action04.enabled,"Damage/death action remains available");actions.ChangeAction(0);
            Check(player.TopSpeed==speed && actions.Action01.JumpSpeed==jump,"Normal movement and jump values unchanged");
            var homing=actions.Action02Control;homing.Actions=actions;homing.HasTarget=true;homing.HomingAvailable=true;Invoke(homing,"FixedUpdate");
            Check(!homing.HasTarget && !homing.HomingAvailable && HomingAttackControl.TargetObject==null && homing.Icon.localScale==Vector3.zero,"Auto-lock and icon disappear while locked");
            var light=actions.Action07Control;light.Actions=actions;light.HasTarget=true;Invoke(light,"FixedUpdate");Check(!light.HasTarget && LightDashControl.TargetObject==null,"Light dash targeting is locked");
            var rail=actions.GetComponent<Rail_Interaction>();Invoke(rail,"Awake");rail.OnCollisionEnter(null);Check(rail.rail==null,"Locked rails cannot grab Sonic");
            var map=new[]{SonicAbility.HomingAttack,SonicAbility.AirDash,SonicAbility.SpinDash,SonicAbility.Bounce,SonicAbility.LightDash,SonicAbility.DropDash,SonicAbility.RailGrinding};
            foreach(var ability in map){
                Set("UnlockedAbilities",SonicAbility.None);Check(actions.UnlockAbility(ability) && actions.CanUse(ability),"Future unlock works: "+ability);
                Check(!actions.UnlockAbility(ability),"Repeated unlock is idempotent: "+ability);
                Check(actions.AvailableAbilities==ability,"Unlock does not grant other abilities: "+ability);
            }
            Check(!actions.UnlockAbility((SonicAbility)128),"Unknown abilities cannot unlock");
            Set("UnlockedAbilities",SonicAbility.HomingAttack|SonicAbility.AirDash);
            Check(actions.CanUse(SonicAbility.HomingAttack|SonicAbility.AirDash) && !actions.CanUse(SonicAbility.All),"Combined queries require every ability");
            foreach(int version in new[]{1,2,3}){
                string json="{\"version\":"+version+",\"scene\":\"test\",\"character\":\"sonic\",\"lives\":2,\"totalScore\":123}";
                Check(SonicXProgress.TryParse(json,out var migrated) && migrated.version==4 && migrated.unlockedAbilities==0,"Old save remains usable without abilities: "+version);
            }
            var save=new StorySave{scene="test",lives=2,totalScore=456,unlockedAbilities=(int)(SonicAbility.SpinDash|SonicAbility.DropDash)};
            Check(SonicXProgress.TryParse(JsonUtility.ToJson(save),out var restored) && restored.unlockedAbilities==save.unlockedAbilities && restored.lives==2 && restored.totalScore==456,"Save roundtrip preserves abilities and existing progress");
            PlayerPrefs.SetString(key,JsonUtility.ToJson(save));
            typeof(SonicXProgress).GetField("storySession",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,true);
            Set("UnlockedAbilities",SonicAbility.HomingAttack|SonicAbility.AirDash);
            typeof(SonicXProgress).GetMethod("PersistUnlockedAbilities",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{key});
            Check(SonicXProgress.TryParse(PlayerPrefs.GetString(key),out var persisted) && persisted.unlockedAbilities==(int)SonicXProgress.UnlockedAbilities && persisted.lives==2 && persisted.totalScore==456 && persisted.scene=="test","Unlock persistence preserves level, score and lives");
            var begin=typeof(SonicXProgress).GetMethod("Begin").GetParameters();Check((SonicAbility)begin[4].DefaultValue==SonicAbility.None,"New adventure starts without abilities");
            File.WriteAllText(Path.Combine(Folder,"unity-report.txt"),"PASS\nReal Sonic prefab: all special actions blocked, direct initial events harmless, movement/jump/roll preserved, damage available, icons hidden, rails blocked. Each future unlock independent and idempotent. Legacy saves migrate, acquired abilities roundtrip and persist without changing lives/score/level.\n"+DateTime.Now.ToString("s"));
            Debug.Log("Sonic limite : capacites verrouillees et sauvegarde verifies.");
        }catch(Exception e){File.WriteAllText(Path.Combine(Folder,"unity-report.txt"),"FAIL\n"+e);Debug.LogException(e);}
        finally {
            PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();
            if(go!=null)Object.DestroyImmediate(go);EditorSceneManager.ClosePreviewScene(scene);
            foreach(var pair in fields)pair.Key.SetValue(null,pair.Value);
            HomingAttackControl.TargetObject=homingTarget;LightDashControl.TargetObject=lightTarget;
        }
    }
}
