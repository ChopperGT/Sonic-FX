using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class SonicFallSpeedVerification
{
    const string Version="SonicFX.FallingSpeed360.v1";
    static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/SonicFallSpeed");
    static SonicFallSpeedVerification(){EditorApplication.update+=Ready;}
    static void Ready(){
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null)return;
        EditorApplication.update-=Ready;if(SessionState.GetBool(Version,false))return;SessionState.SetBool(Version,true);Verify();
    }
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    [MenuItem("Tools/Sonic FX/Verifier la vitesse de chute de Sonic")]
    public static void Verify()
    {
        Directory.CreateDirectory(Folder);var scene=EditorSceneManager.NewPreviewScene();GameObject go=null;
        try{
            go=new GameObject("Sonic fall speed verification");SceneManager.MoveGameObjectToScene(go,scene);
            var body=go.AddComponent<Rigidbody>();body.useGravity=false;
            var player=go.AddComponent<PlayerBhysics>();player.p_rigidbody=body;player.MaxSpeed=30;
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var apply=typeof(PlayerBhysics).GetMethod("ApplySpeedLimits",flags);
            var awake=typeof(PlayerBhysics).GetMethod("Awake",flags);
            var general=typeof(PlayerBhysics).GetMethod("GeneralPhysics",flags);
            var actions=go.AddComponent<ActionManager>();typeof(PlayerBhysics).GetField("Action",flags).SetValue(player,actions);
            player.AccellOverSpeed=AnimationCurve.Linear(0,1,1,1);player.TangDragOverSpeed=AnimationCurve.Linear(0,1,1,1);player.SlopePowerOverSpeed=AnimationCurve.Linear(0,1,1,1);
            player.Gravity=new Vector3(0,-1.5f,0);player.Playermask=0;
            player.MaxFallingSpeed=-500;awake.Invoke(player,null);Check(player.MaxFallingSpeed==-360,"Existing serialized fall cap not upgraded");
            player.Grounded=false;
            foreach(var velocity in new[]{new Vector3(0,-200,0),new Vector3(90,-120,0),new Vector3(0,-360,0),new Vector3(280,-180,0)}){
                body.linearVelocity=velocity;apply.Invoke(player,null);Check(Vector3.Distance(body.linearVelocity,velocity)<.001f,"MaxSpeed changes sub-360 fall: "+velocity);
            }
            foreach(float groundLimit in new[]{15f,30f,100f,300f}){
                player.MaxSpeed=groundLimit;body.linearVelocity=new Vector3(300,-400,0);apply.Invoke(player,null);
                Check(Mathf.Abs(body.linearVelocity.magnitude-360)<.001f,"Fall vector must cap at 360 independently of MaxSpeed");
                Check(Vector3.Dot(body.linearVelocity.normalized,new Vector3(300,-400,0).normalized)>.99999f,"Fall cap changes trajectory direction");
            }
            body.linearVelocity=new Vector3(0,-500,0);apply.Invoke(player,null);Check(body.linearVelocity==Vector3.down*360,"Vertical fall not capped at 360");
            player.MaxSpeed=30;player.Grounded=true;body.linearVelocity=new Vector3(60,5,80);apply.Invoke(player,null);
            Check(Mathf.Abs(new Vector2(body.linearVelocity.x,body.linearVelocity.z).magnitude-30)<.001f && body.linearVelocity.y==5,"Ground limit must retain vertical motion");
            player.Grounded=false;body.linearVelocity=new Vector3(60,55,80);apply.Invoke(player,null);
            Check(Mathf.Abs(new Vector2(body.linearVelocity.x,body.linearVelocity.z).magnitude-30)<.001f && body.linearVelocity.y==55,"Jump ascent modified by falling cap");
            player.MaxFallingSpeed=-18;body.linearVelocity=new Vector3(20,-200,0);apply.Invoke(player,null);
            Check(body.linearVelocity==new Vector3(20,-18,0),"Lower water sinking cap ignored");
            player.MaxFallingSpeed=18;body.linearVelocity=Vector3.down*100;apply.Invoke(player,null);Check(body.linearVelocity.y==-18,"Positive legacy fall limit creates upward velocity");
            player.MaxFallingSpeed=-360;body.linearVelocity=Vector3.down*360;
            for(int frame=0;frame<900;frame++){body.linearVelocity+=new Vector3(0,-1.5f,0);apply.Invoke(player,null);Check(body.linearVelocity.y==-360 && body.linearVelocity.magnitude<=360.001f,"Gravity overshoots terminal speed");}
            player.MaxFallingSpeed=0;body.linearVelocity=Vector3.down*100;apply.Invoke(player,null);Check(body.linearVelocity.y==0,"Zero sinking limit must remain valid");
            // Exercise the complete controller tick, not only the limiter: the first tick after
            // walking off an edge still starts with Grounded=true and must already bypass MaxSpeed.
            player.MaxFallingSpeed=-360;player.Grounded=true;body.linearVelocity=new Vector3(200,-100,0);
            general.Invoke(player,null);Check(!player.Grounded && Vector3.Distance(body.linearVelocity,new Vector3(200,-101.5f,0))<.001f,"First airborne tick uses stale grounded speed limit");
            for(int frame=0;frame<1200;frame++){
                general.Invoke(player,null);Check(body.linearVelocity.magnitude<=360.001f && body.linearVelocity.y<0,"Complete physics tick violates fall speed or direction");
            }
            Check(Mathf.Abs(body.linearVelocity.magnitude-360)<.001f,"Full gravity integration never reaches terminal speed");
            File.WriteAllText(Path.Combine(Folder,"unity-tests.txt"),"PASS\n"+DateTime.Now.ToString("s")+"\nExisting serialized limits reset to -360; vertical/diagonal fall bypasses MaxSpeed; total falling vector capped at 360 for ground limits 15/30/100/300; direction preserved; ground limit and jump ascent preserved; lower water sinking limit; legacy positive sign; 900 gravity steps at terminal speed; zero water sinking limit; full controller first airborne tick and 1200 gravity ticks.\n");
            Debug.Log("Sonic : chute limitee a 360, independante de Max Speed, verifiee.");
        }catch(Exception e){File.WriteAllText(Path.Combine(Folder,"unity-tests.txt"),"FAIL\n"+e);Debug.LogException(e);}
        finally{if(go!=null)Object.DestroyImmediate(go);EditorSceneManager.ClosePreviewScene(scene);}
    }
}
