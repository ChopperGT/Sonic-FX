using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SonicFX.Water;
using Object=UnityEngine.Object;
namespace SonicFX.Bat.Editor
{
    [InitializeOnLoad]
    static class BatVerification
    {
        static string Folder=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/Bat");
        static BatVerification(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            string request=Path.Combine(Folder,"request.txt");
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request))return;
            File.Delete(request);Verify();
        }
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        static void Private(object obj,string field,object value){obj.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(obj,value);}
        static GameObject New(Scene scene,string name){var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,scene);return go;}
        static BatController Spawn(Scene scene,Vector3 pos)
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BatBuilder.Prefab));SceneManager.MoveGameObjectToScene(go,scene);
            go.transform.position=pos;var b=go.GetComponent<BatController>();b.enabled=false;b.snapToCeiling=true;BatController.Active.Add(b);b.Initialize();return b;
        }
        static void Step(BatController b,float seconds){while(seconds>.00001f){float dt=Mathf.Min(.02f,seconds);b.Tick(dt);seconds-=dt;}}
        static void Verify()
        {
            Directory.CreateDirectory(Folder);var active=SceneManager.GetActiveScene();bool dirty=active.isDirty;var selection=Selection.objects;
            var batSet=new HashSet<BatController>(BatController.Active);var rng=UnityEngine.Random.state;
            int rings=Objects_Interaction.RingAmount;bool shield=Monitors_Interactions.HasShield;
            var ringField=typeof(SonicFX.Score.SonicLevelScore).GetField("<RingsTowardLife>k__BackingField",BindingFlags.NonPublic|BindingFlags.Static);object ringProgress=ringField.GetValue(null);
            Scene scene=default;var report=new StringBuilder();
            try
            {
                BatBuilder.Build();scene=EditorSceneManager.NewPreviewScene();Check(scene.GetPhysicsScene()!=Physics.defaultPhysicsScene,"Isolated physics");
                var ceiling=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(ceiling,scene);ceiling.transform.position=new Vector3(0,8,0);ceiling.transform.localScale=new Vector3(50,.25f,50);
                var go=New(scene,"Sonic verification");go.transform.position=new Vector3(0,2,2);var rb=go.AddComponent<Rigidbody>();rb.useGravity=false;
                var player=go.AddComponent<PlayerBhysics>();player.enabled=false;player.p_rigidbody=rb;player.TopSpeed=65;player.MaxSpeed=350;
                var actions=go.AddComponent<ActionManager>();actions.enabled=false;actions.RestorePackAbilities();
                actions.Action00=go.AddComponent<Action00_Regular>();actions.Action00.enabled=false;
                actions.Action01=go.AddComponent<Action01_Jump>();actions.Action01.enabled=false;actions.Action01.JumpBall=New(scene,"Jump ball");
                actions.Action04=go.AddComponent<Action04_Hurt>();actions.Action04.enabled=false;Private(actions.Action04,"Player",player);
                var hurt=go.AddComponent<HurtControl>();hurt.enabled=false;actions.Action04Control=hurt;
                var sound=go.AddComponent<SonicSoundsControl>();sound.enabled=false;sound.Source3=go.AddComponent<AudioSource>();sound.PainVoiceClips=new AudioClip[0];
                var damage=go.AddComponent<Objects_Interaction>();damage.enabled=false;damage.Player=player;damage.Actions=actions;damage.Sounds=sound;
                var b=Spawn(scene,new Vector3(0,6,0));b.player=player;Physics.SyncTransforms();b.Initialize();
                Check(Mathf.Abs(b.Perch.y-7.025f)<.02f,"Ceiling attachment");Check(b.State==BatController.Behaviour.Sleeping,"Starts asleep");
                b.startsAsleep=false;b.Initialize();Check(b.State==BatController.Behaviour.Watching,"Awake checkbox");b.startsAsleep=true;b.Initialize();
                var nearby=Spawn(scene,new Vector3(8,6,0));nearby.watchSize=Vector3.one;var far=Spawn(scene,new Vector3(35,6,0));far.snapToCeiling=false;far.watchSize=Vector3.one;far.Initialize();
                Check(b.CanSee(player),"Sonic in watch zone");
                go.transform.position=new Vector3(24,2,2);Check(!b.CanSee(player),"Outside watch zone");go.transform.position=new Vector3(0,2,2);
                var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(obstacle,scene);obstacle.transform.position=new Vector3(0,4.7f,1);obstacle.transform.localScale=new Vector3(3,2,.5f);Physics.SyncTransforms();Check(!b.CanSee(player),"Wall occludes surveillance");Object.DestroyImmediate(obstacle);Physics.SyncTransforms();
                player.isRolling=true;Check(!b.Attach(player),"Rolling protects from attachment");player.isRolling=false;actions.Action=1;actions.Action01.JumpBall.SetActive(true);Check(!b.Attach(player),"Ball jump protects from attachment");actions.Action=0;actions.Action01.JumpBall.SetActive(false);
                Step(b,.28f);Check(b.State==BatController.Behaviour.Sleeping,"Reaction delay");Step(b,.04f);
                Check(b.State==BatController.Behaviour.Pursuing && b.CallsEmitted==1,"Wake and shriek");Check(nearby.State==BatController.Behaviour.Pursuing && nearby.CallsEmitted==0,"Nearby wake without call loop");Check(far.State==BatController.Behaviour.Sleeping,"Outside hearing radius");
                Step(b,2);var status=go.GetComponent<SonicBatAttachment>();Check(status!=null && status.Count==1 && b.State==BatController.Behaviour.Attached,"Pursuit and attachment");
                Check(!b.contact.enabled && !b.homingTarget.activeSelf,"Attached cannot collide or be homing targeted");Check(player.EffectiveTopSpeed==60 && player.EffectiveMaxSpeed==345,"Temporary speed penalty");
                Check(!actions.CanUse(SonicAbility.SpinDash) && !actions.CanUse(SonicAbility.HomingAttack) && actions.CanChangeAction(1),"Ball attacks blocked, normal jump allowed");Check(!actions.Action01.JumpBall.activeSelf,"Jump ball removed");
                status.PressRoll();Check(Mathf.Abs(status.Progress-1f/8)<.001f,"First bat escape difficulty");status.Tick(.2f);Check(status.Progress<1f/8,"Progress decays");
                for(int i=0;i<8;i++)status.PressRoll();Check(status.Count==0 && b.State==BatController.Behaviour.Escaping,"Mash detaches one");Check(player.TopSpeed==65 && player.MaxSpeed==350 && player.EffectiveTopSpeed==65 && actions.CanUse(SonicAbility.SpinDash),"Exact restoration without save mutation");
                go.transform.position=new Vector3(40,2,2);Step(b,5);Check(b.State==BatController.Behaviour.Sleeping,"Returns to ceiling sleeping");go.transform.position=new Vector3(0,2,2);
                report.AppendLine("PASS ceiling, sleep checkbox, editable watch zone, reaction, shriek, nearby wake, distant sleep, pursuit, attach, speed -5, ball lock, jump, mash and restore.");
                // Four bats never start the damage timer; five do, exactly once per ten seconds.
                var group=new List<BatController>();for(int i=0;i<4;i++){var bat=Spawn(scene,new Vector3(i,6,-3));Check(bat.Attach(player),"Group attach");group.Add(bat);}
                status.Tick(20);Check(status.DamageEvents==0 && status.DamageTimer==0,"Four bats: no countdown");
                var fifth=Spawn(scene,new Vector3(5,6,-3));fifth.Attach(player);group.Add(fifth);status.PressRoll();Check(Mathf.Abs(status.Progress-1f/24)<.001f,"Five bats require more presses");
                Check(player.EffectiveTopSpeed==40 && player.EffectiveMaxSpeed==325,"Stacked speed penalty");
                status.Tick(9.98f);Check(status.DamageEvents==0 && !hurt.isDead,"No early damage");Objects_Interaction.RingAmount=0;Monitors_Interactions.HasShield=false;
                status.Tick(.03f);Check(status.DamageEvents==1 && hurt.isDead && actions.Action==4,"Ten seconds dispatches actual Sonic damage/death");hurt.isDead=false;actions.Action=0;
                status.Repel(false);Check(status.Count==0 && status.DamageTimer==0,"Countdown reset on escape");
                report.AppendLine("PASS four/five threshold, ten-second damage through Objects_Interaction, cumulative -25, increased mash difficulty, reset.");
                // Enemy collision: independent 70% rolls, including the exact boundary.
                for(int i=0;i<5;i++){var bat=Spawn(scene,new Vector3(i,6,-5));bat.Attach(player);}
                float[] rolls={.1f,.69f,.7f,.9f,.2f};int index=0;Check(status.ResolveEnemyImpact(()=>rolls[index++])==3 && status.Count==2,"Independent seventy-percent destruction");status.Repel(false);
                report.AppendLine("PASS enemy collision independent rolls and 70% boundary.");
                var water=New(scene,"Water").AddComponent<SonicWaterVolume>();water.transform.position=new Vector3(0,3,2);water.width=10;water.length=10;water.depth=10;
                // Attach on dry land then cross into the existing water system.
                go.transform.position=new Vector3(12,2,2);var wetBat=Spawn(scene,new Vector3(12,6,2));wetBat.waterAlertChance=0;wetBat.Attach(player);go.transform.position=new Vector3(0,2,2);status.Tick(.02f);
                Check(status.Count==0 && wetBat.State==BatController.Behaviour.Escaping,"Entering water repels");Step(wetBat,4);Check(wetBat.State==BatController.Behaviour.Sleeping,"Zero alert chance sleeps");
                go.transform.position=new Vector3(12,2,2);var alertBat=Spawn(scene,new Vector3(12,6,-2));alertBat.waterAlertChance=1;alertBat.Attach(player);go.transform.position=new Vector3(0,2,2);status.Tick(.02f);Step(alertBat,4);Check(alertBat.State==BatController.Behaviour.Watching,"Full alert chance stays alert");
                Check(Mathf.Approximately(b.waterAlertChance,.3f),"Default thirty-percent alert");Object.DestroyImmediate(water.gameObject);
                go.transform.position=new Vector3(0,2,-4);var fall=New(scene,"Waterfall").AddComponent<SonicWaterfall>();fall.transform.position=new Vector3(0,8,0);fall.height=10;fall.width=10;fall.thickness=1;fall.bulge=0;fall.matchWaterHeight=false;fall.Refresh();
                SonicBatAttachment.RefreshWaterfallCache();var fallBat=Spawn(scene,new Vector3(0,6,-4));fallBat.Attach(player);Private(status,"wetRefresh",0f);status.Tick(.02f);go.transform.position=new Vector3(0,2,4);status.Tick(.02f);Check(status.Count==0 && fallBat.State==BatController.Behaviour.Escaping,"Swept waterfall crossing repels at speed");
                report.AppendLine("PASS water, waterfall swept crossing, flee to another ceiling, 30% default, sleeping/alert return.");
                Capture(b,false,"flight.png");Capture(b,true,"ceiling.png");
                Check(SceneManager.GetActiveScene()==active && active.isDirty==dirty,"User map unchanged");Check(Selection.objects.Length==selection.Length,"Selection unchanged");
                File.WriteAllText(Path.Combine(Folder,"tests.txt"),"PASS\n"+report+DateTime.Now.ToString("s"));Debug.Log("Chauve-souris : verification complete, aucun niveau modifie.");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Folder,"tests.txt"),"FAIL\n"+report+e);Debug.LogException(e);}
            finally
            {
                if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);BatController.Active.Clear();foreach(var bat in batSet)if(bat!=null)BatController.Active.Add(bat);
                UnityEngine.Random.state=rng;Objects_Interaction.RingAmount=rings;Monitors_Interactions.HasShield=shield;ringField.SetValue(null,ringProgress);SonicBatAttachment.RefreshWaterfallCache();
            }
        }
        static void Capture(BatController source,bool hanging,string filename)
        {
            var preview=new PreviewRenderUtility();Texture2D texture=null;
            try
            {
                var go=Object.Instantiate(source.gameObject);preview.AddSingleGO(go);go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                var b=go.GetComponent<BatController>();b.enabled=false;b.visual.Pose(hanging?BatController.Behaviour.Sleeping:BatController.Behaviour.Pursuing,0);
                preview.camera.transform.position=new Vector3(3,1.8f,7);preview.camera.transform.LookAt(Vector3.zero);preview.camera.fieldOfView=40;
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.03f,.07f,.12f);
                preview.lights[0].intensity=1.2f;preview.lights[0].transform.rotation=Quaternion.Euler(25,-25,0);preview.lights[1].intensity=.8f;preview.ambientColor=Color.gray;
                preview.BeginStaticPreview(new Rect(0,0,1100,800));preview.Render();texture=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(Folder,filename),texture.EncodeToPNG());
            }finally{if(texture!=null)Object.DestroyImmediate(texture);preview.Cleanup();}
        }
    }
}
