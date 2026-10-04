using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SonicFX.Menu;
using SonicFX.Score;

[InitializeOnLoad] internal static class SonicRingDamageVerification
{
    static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/RingDamage");
    static readonly BindingFlags StaticFlags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static SonicRingDamageVerification(){EditorApplication.update+=Ready;}
    static void Ready()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        EditorApplication.update-=Ready;
        if(!File.Exists(Path.Combine(Folder,"unity-report.txt")))Verify();
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Set(string property,object value)=>typeof(SonicXProgress).GetField("<"+property+">k__BackingField",StaticFlags).SetValue(null,value);
    [MenuItem("Tools/Sonic FX/Score/Verifier les paliers de rings apres un coup")]
    static void Verify()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        Directory.CreateDirectory(Folder);
        var saved=new[]{typeof(SonicXProgress),typeof(SonicLevelScore)}
            .SelectMany(t=>t.GetFields(StaticFlags)).Where(f=>!f.IsLiteral&&!f.IsInitOnly)
            .ToDictionary(f=>f,f=>f.GetValue(null));
        int held=Objects_Interaction.RingAmount;bool shield=Monitors_Interactions.HasShield;
        var scene=EditorSceneManager.NewPreviewScene();var next=EditorSceneManager.NewPreviewScene();AudioClip clip=null;
        try
        {
            typeof(SonicXProgress).GetField("storySession",StaticFlags).SetValue(null,false);
            typeof(SonicXProgress).GetField("LivesGained",StaticFlags).SetValue(null,null);
            Set("Lives",3);Set("TotalScore",1234L);SonicLevelScore.BeginLevel(scene);Objects_Interaction.RingAmount=0;
            var go=new GameObject("Verification temporaire coups et rings");SceneManager.MoveGameObjectToScene(go,scene);go.SetActive(false);
            var body=go.AddComponent<Rigidbody>();body.useGravity=false;
            var physics=go.AddComponent<PlayerBhysics>();physics.p_rigidbody=body;
            var actions=go.AddComponent<ActionManager>();var hurt=go.AddComponent<HurtControl>();var action=go.AddComponent<Action04_Hurt>();
            actions.Action04=action;actions.Action04Control=hurt;
            typeof(Action04_Hurt).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(action,null);
            var sounds=go.AddComponent<SonicSoundsControl>();sounds.Source3=go.AddComponent<AudioSource>();sounds.Source3.volume=0;
            sounds.PainVoiceClips=Array.Empty<AudioClip>();clip=AudioClip.Create("Verification silencieuse",480,1,48000,false);
            sounds.RingLoss=sounds.Spiked=sounds.Die=clip;action.sounds=sounds;
            var interaction=go.AddComponent<Objects_Interaction>();interaction.Actions=actions;interaction.Sounds=sounds;

            SonicLevelScore.AddRings(99);Check(SonicXProgress.Lives==3&&SonicLevelScore.RingsTowardLife==99,"No life at 99 rings");
            Monitors_Interactions.HasShield=false;interaction.DamagePlayer();
            Check(hurt.IsHurt&&SonicLevelScore.RingsTowardLife==0&&SonicLevelScore.Current==990&&SonicXProgress.Lives==3,"Accepted ring-loss hit resets progress only");
            Objects_Interaction.RingAmount=0;SonicLevelScore.AddRings(1);
            Check(SonicXProgress.Lives==3&&SonicLevelScore.RingsTowardLife==1,"99 before a hit plus 1 after cannot award a life");
            interaction.DamagePlayer();Check(SonicLevelScore.RingsTowardLife==1,"Invincibility contacts do not reset new progress");
            SonicLevelScore.AddRings(99);Check(SonicXProgress.Lives==4&&SonicLevelScore.RingsTowardLife==0,"100 new rings award one life");
            SonicLevelScore.AddRings(250);Check(SonicXProgress.Lives==6&&SonicLevelScore.RingsTowardLife==50,"Multiple thresholds award two lives and keep 50");
            hurt.IsHurt=false;actions.Action=0;Monitors_Interactions.HasShield=true;interaction.DamagePlayer();
            Check(!Monitors_Interactions.HasShield&&SonicLevelScore.RingsTowardLife==0&&SonicXProgress.Lives==6,"Shield hit resets progress and preserves earned lives");
            SonicLevelScore.AddRings(99);SonicLevelScore.AddRings(10);
            Check(SonicXProgress.Lives==7&&SonicLevelScore.RingsTowardLife==9,"Ring monitor crosses threshold after reset");
            SonicLevelScore.BeginLevel(scene);Check(SonicLevelScore.RingsTowardLife==9,"Same-scene initialization alone does not reset progress");
            typeof(HurtControl).GetMethod("RegisterDeathOnce",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hurt,null);
            Check(SonicXProgress.Lives==6&&SonicLevelScore.RingsTowardLife==0&&SonicLevelScore.Deaths==1,"Death resets progress and removes one life");
            SonicLevelScore.BeginLevel(scene);SonicLevelScore.AddRings(91);
            Check(SonicXProgress.Lives==6&&SonicLevelScore.RingsTowardLife==91,"Checkpoint cannot reuse pre-death progress");
            Check(SonicLevelScore.RingsCollected==649&&SonicLevelScore.Current==6490&&SonicXProgress.TotalScore==1234,"Statistics and score survive hits");
            SonicLevelScore.AddRings(0);SonicLevelScore.AddRings(-1);Check(SonicLevelScore.RingsTowardLife==91,"Invalid increments ignored");
            SonicLevelScore.Complete(0,null,0);int lives=SonicXProgress.Lives;SonicLevelScore.AddRings(100);
            Check(SonicXProgress.Lives==lives&&SonicLevelScore.RingsTowardLife==91,"No reward after the goal");
            SonicLevelScore.BeginLevel(next);Check(SonicLevelScore.RingsTowardLife==0,"Next level starts at zero");
            SonicLevelScore.AddRings(200);Check(SonicXProgress.Lives==lives+2&&SonicLevelScore.RingsTowardLife==0,"200 rings award two lives");
            Set("Lives",0);SonicLevelScore.AddRings(100);Check(SonicXProgress.Lives==0&&SonicLevelScore.RingsTowardLife==0,"Game over remains final");
            File.WriteAllText(Path.Combine(Folder,"unity-report.txt"),"PASS\nActual DamagePlayer paths verified: ring loss, shield loss, invincibility guard; 99 + hit + 1 gives no life; 100 new rings gives a life; 200/250 batches; monitor rings; death and checkpoint; earned lives and scores preserved; next level reset; goal/game-over guards. No player save or scene modified.\n"+DateTime.Now.ToString("s"));
            Debug.Log("Paliers de rings : gains de vie et remise a zero apres un coup verifies.");
        }
        catch(Exception e){File.WriteAllText(Path.Combine(Folder,"unity-report.txt"),"FAIL\n"+e);Debug.LogException(e);}
        finally
        {
            foreach(var pair in saved)pair.Key.SetValue(null,pair.Value);Objects_Interaction.RingAmount=held;Monitors_Interactions.HasShield=shield;
            EditorSceneManager.ClosePreviewScene(scene);EditorSceneManager.ClosePreviewScene(next);if(clip!=null)UnityEngine.Object.DestroyImmediate(clip);
        }
    }
}
