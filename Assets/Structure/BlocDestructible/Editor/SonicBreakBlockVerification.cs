using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace SonicFX.Structures.Editor
{
    [InitializeOnLoad] public static class SonicBreakBlockVerification
    {
        static string Report=>Path.Combine(SonicBreakBlockBuilder.Reports,"unity-report.txt");
        static SonicBreakBlockVerification(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)return;
            if(AssetDatabase.LoadAssetAtPath<GameObject>(SonicBreakBlockBuilder.PrefabPath)==null)return;
            EditorApplication.update-=Ready;if(!File.Exists(Report))Verify();
        }
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        static void Invoke(object target,string name,params object[] args){target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);}
        static void Field(object target,string name,object value){target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);}
        [MenuItem("Tools/Sonic FX/Bloc destructible/Verifier le bloc")]
        public static void Verify()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            Directory.CreateDirectory(SonicBreakBlockBuilder.Reports);
            var scene=EditorSceneManager.NewPreviewScene();GameObject playerRoot=null;
            try
            {
                Check(Mathf.Approximately(SonicBreakChallenge.EntryBonus(55,55,.6f),.6f),"Entry bonus at reference speed");
                Check(Mathf.Approximately(SonicBreakChallenge.EntryBonus(27.5f,55,.6f),.3f),"Proportional entry bonus");
                Check(SonicBreakChallenge.EntryBonus(500,55,1)<=.95f,"Entry cannot instantly destroy block");
                var challenge=new SonicBreakChallenge(0,4,5,0);challenge.Step(0,true);Check(challenge.Progress==0,"Pause ignores input");
                for(int i=0;i<3;i++)challenge.Step(.1f,true);Check(!challenge.Won,"Only three presses");challenge.Step(.1f,true);Check(challenge.Won,"Fourth distinct press completes");challenge.Step(20,false);Check(challenge.Won && !challenge.Lost,"Outcome is stable");
                challenge=new SonicBreakChallenge(.5f,10,2,.1f);challenge.Step(1,false);Check(Mathf.Abs(challenge.Progress-.4f)<.001f,"Progress drains without pressing");challenge.Step(1.1f,true);Check(challenge.Lost,"Late press cannot beat deadline");

                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(SonicBreakBlockBuilder.PrefabPath);
                SonicBreakBlock Block()
                {
                    var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);go.transform.position=new Vector3(0,100,0);return go.GetComponent<SonicBreakBlock>();
                }
                var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BumperEngineV1/PlayerPrefabs/PO_Mania.prefab");Check(original!=null,"Sonic prefab exists");
                playerRoot=(GameObject)PrefabUtility.InstantiatePrefab(original,scene);
                // Editor-only verification: no gameplay update, score, saving or scene changes.
                foreach(var script in playerRoot.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
                var player=playerRoot.GetComponentInChildren<PlayerBhysics>(true);var rb=player.GetComponent<Rigidbody>();var actions=player.GetComponent<ActionManager>();var hurt=player.GetComponent<HurtControl>();
                player.p_rigidbody=rb;player.Gravity=new Vector3(0,-1.5f,0);player.enabled=true;actions.ChangeAction(0);actions.Action00.enabled=false;
                void Prepare(float speed)
                {
                    rb.isKinematic=false;rb.detectCollisions=true;rb.position=new Vector3(0,100,-2.4f);rb.rotation=Quaternion.identity;rb.linearVelocity=Vector3.forward*speed;rb.useGravity=false;player.isRolling=true;player.Grounded=true;player.GroundNormal=Vector3.up;player.enabled=true;hurt.isDead=false;hurt.IsHurt=false;actions.ChangeAction(0);actions.Action00.enabled=false;Physics.SyncTransforms();
                }
                Prepare(27.5f);var block=Block();Physics.SyncTransforms();
                Check(block.solid.enabled && !block.solid.isTrigger && block.sensor.isTrigger,"Solid obstacle and sensor");
                player.isRolling=false;Check(!block.TryBegin(player),"Standing Sonic cannot start");player.isRolling=true;Check(block.TryBegin(player),"Rolling Sonic starts challenge");
                Check(rb.isKinematic && !player.enabled && actions.Action==-1 && Mathf.Abs(block.Progress-.3f)<.001f,"Captured and speed bonus applied");
                var other=Block();Check(!other.TryBegin(player),"Two blocks cannot capture same player");Object.DestroyImmediate(other.gameObject);
                int lives=SonicFX.Menu.SonicXProgress.Lives,deaths=SonicFX.Score.SonicLevelScore.Deaths;
                for(int i=0;i<30 && block.State==SonicBreakBlock.Phase.Charging;i++)block.AdvanceChallenge(.08f,true);
                Check(block.State==SonicBreakBlock.Phase.Broken && !block.solid.enabled && !block.visual.gameObject.activeSelf,"Success clears the path");
                Check(!rb.isKinematic && player.enabled && actions.Action==0 && Mathf.Abs(rb.linearVelocity.z-block.launchSpeed)<.001f,"Success restores control and configured speed");
                Object.DestroyImmediate(block.gameObject);

                Prepare(20);block=Block();Physics.SyncTransforms();Check(block.TryBegin(player),"New attempt starts");block.AdvanceChallenge(20,false);
                Check(block.State==SonicBreakBlock.Phase.Recoiling && !rb.isKinematic && rb.linearVelocity.z<0 && rb.linearVelocity.y>0,"Failure recoil goes back and up");
                Check(!hurt.isDead && SonicFX.Menu.SonicXProgress.Lives==lives && SonicFX.Score.SonicLevelScore.Deaths==deaths,"Failure is not a real death");
                Check(actions.Action00.CharacterAnimator.GetBool("Dead"),"Failure uses death animation parameter");
                Field(block,"phaseTime",.3f);rb.linearVelocity=Vector3.down;
                Invoke(block,"NotifyGround",Vector3.right);Check(block.State==SonicBreakBlock.Phase.Recoiling,"A wall is not a landing");
                Invoke(block,"NotifyGround",Vector3.up);Check(block.State==SonicBreakBlock.Phase.Recovering,"Ground contact starts recovery");
                Field(block,"phaseTime",block.recoverDuration);Invoke(block,"Update");
                Check(block.State==SonicBreakBlock.Phase.Idle && player.enabled && !rb.isKinematic && !actions.Action00.CharacterAnimator.GetBool("Dead"),"Recovery restores normal animation and movement");
                Check(block.solid.enabled && block.visual.gameObject.activeSelf,"Failed block remains available");Object.DestroyImmediate(block.gameObject);

                Prepare(30);block=Block();Physics.SyncTransforms();Check(block.TryBegin(player),"Cancellation attempt starts");block.enabled=false;
                Check(player.enabled && !rb.isKinematic && actions.Action==0,"Disabling block restores player");Object.DestroyImmediate(block.gameObject);
                Prepare(30);block=Block();Physics.SyncTransforms();Check(block.TryBegin(player),"Death interruption starts");hurt.isDead=true;actions.ChangeAction(4);Invoke(block,"Update");
                Check(player.enabled && !rb.isKinematic && hurt.isDead && actions.Action==4,"Real death interruption preserves death action");hurt.isDead=false;Object.DestroyImmediate(block.gameObject);
                Render(prefab);
                File.WriteAllText(Report,"PASS\nChallenge formula, cap, distinct presses, timeout, pause, drain; real Sonic prefab: entry, speed fill, mutual exclusion, success/launch, failure/death pose without lost life, ground recovery, wall rejection, cancellation and real death interruption. Prefab preview rendered.\n"+DateTime.Now.ToString("s"));
            }
            catch(Exception e){File.WriteAllText(Report,"FAIL\n"+e);Debug.LogException(e);}
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        static void Render(GameObject prefab)
        {
            var preview=new PreviewRenderUtility();Texture2D texture=null;
            try
            {
                var go=Object.Instantiate(prefab);preview.AddSingleGO(go);preview.camera.transform.position=new Vector3(10,7,-14);preview.camera.transform.LookAt(new Vector3(0,3,0));preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=100;preview.camera.clearFlags=CameraClearFlags.Color;preview.camera.backgroundColor=new Color(.045f,.095f,.15f);preview.ambientColor=Color.gray;preview.lights[0].intensity=1.3f;preview.lights[0].transform.rotation=Quaternion.Euler(35,-25,0);preview.lights[1].intensity=.7f;
                preview.BeginStaticPreview(new Rect(0,0,1200,800));preview.Render();texture=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(SonicBreakBlockBuilder.Reports,"Bloc_Unity.png"),texture.EncodeToPNG());
            }
            finally{if(texture!=null)Object.DestroyImmediate(texture);preview.Cleanup();}
        }
    }
}
