using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad] internal static class SonicSteeringVerification
{
    internal static readonly string Reports=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/SteeringAndLifeAudio");
    static SonicSteeringVerification(){EditorApplication.update+=Ready;}
    static void Ready()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        EditorApplication.update-=Ready;
        if(!File.Exists(Path.Combine(Reports,"steering-report.txt")))Verify();
    }
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    [MenuItem("Tools/Sonic FX/Verifier la direction a grande vitesse")]
    static void Verify()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        Directory.CreateDirectory(Reports);var scene=EditorSceneManager.NewPreviewScene();GameObject go=null;
        try
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BumperEngineV1/PlayerPrefabs/PO_Mania.prefab");
            var original=prefab.GetComponentInChildren<PlayerBhysics>(true);var originalInput=prefab.GetComponentInChildren<PlayerBinput>(true);
            Check(original!=null&&originalInput!=null,"Actual Sonic movement components available");
            go=new GameObject("Verification direction");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);
            var body=go.AddComponent<Rigidbody>();body.useGravity=false;
            var physics=go.AddComponent<PlayerBhysics>();physics.enabled=false;EditorUtility.CopySerialized(original,physics);
            physics.p_rigidbody=body;physics.Grounded=true;physics.GroundNormal=Vector3.up;physics.TimeOnGround=0;physics.SpeedMagnitude=0;
            var input=go.AddComponent<PlayerBinput>();input.enabled=false;EditorUtility.CopySerialized(originalInput,input);
            var smooth=typeof(PlayerBinput).GetMethod("SmoothMovementInput",BindingFlags.Instance|BindingFlags.NonPublic);
            var control=typeof(PlayerBhysics).GetMethod("HandleGroundControl",BindingFlags.Instance|BindingFlags.NonPublic);
            Check(input.responsiveSteering&&input.minimumInputResponse>=10,"Placed prefabs receive responsive default without rebuilding");
            var log=new StringBuilder("PASS\nActual PO_Mania curves and movement controller tested in isolated preview scene.\n");
            foreach(float speed in new[]{20f,40f,80f,160f,300f})
            {
                physics.MaxSpeed=Mathf.Max(original.MaxSpeed,speed);
                float oldAngle=Turn(physics,input,body,smooth,control,speed,false,false);
                float newAngle=Turn(physics,input,body,smooth,control,speed,true,false);
                float rollAngle=Turn(physics,input,body,smooth,control,speed,true,true);
                log.AppendLine("Speed "+speed+": direction after 0.8 s, legacy="+oldAngle.ToString("F1")+" deg; corrected="+newAngle.ToString("F1")+" deg; rolling="+rollAngle.ToString("F1")+" deg.");
                Check(newAngle>65&&rollAngle>60,"Running and rolling still turn at speed "+speed+" ("+newAngle+", "+rollAngle+")");
                if(speed>=original.MaxSpeed)Check(newAngle>oldAngle+20,"Regression reproduced and improved at max/high speed "+speed);
            }
            input.responsiveSteering=true;
            Vector3 baseline=Vector3.zero;
            foreach(int fps in new[]{30,60,144})
            {
                Vector3 current=Vector3.forward;
                int steps=fps;for(int i=0;i<steps;i++)current=(Vector3)smooth.Invoke(input,new object[]{current,Vector3.right,.25f,1f/fps});
                if(fps==30)baseline=current;else Check(Vector3.Distance(current,baseline)<.0001f,"Input response independent of rendering frame rate");
                current=Vector3.forward;
                for(int i=0;i<fps;i++)current=(Vector3)smooth.Invoke(input,new object[]{current,Vector3.zero,1f,1f/fps});
                Check(current.magnitude<.001f,"Release does not keep slow stale input at speed");
            }
            input.responsiveSteering=false;
            var slow=(Vector3)smooth.Invoke(input,new object[]{Vector3.forward,Vector3.right,.25f,.1f});
            Check(Vector3.Angle(Vector3.forward,slow)<3,"Checkbox can restore slow configured curve");
            input.responsiveSteering=true;
            var stationary=(Vector3)smooth.Invoke(input,new object[]{Vector3.forward,Vector3.right,.25f,0f});
            Check(stationary==Vector3.forward,"Zero elapsed time cannot change input");
            log.AppendLine("30/60/144 FPS input response identical; release response, rolling and opt-out checked. No scene or player save changed.");
            log.AppendLine(DateTime.Now.ToString("s"));File.WriteAllText(Path.Combine(Reports,"steering-report.txt"),log.ToString());
            Debug.Log("Direction de Sonic : commandes et virages a grande vitesse verifies.");
        }
        catch(Exception e){File.WriteAllText(Path.Combine(Reports,"steering-report.txt"),"FAIL\n"+e);Debug.LogException(e);}
        finally{if(go!=null)Object.DestroyImmediate(go);EditorSceneManager.ClosePreviewScene(scene);}
    }
    static float Turn(PlayerBhysics physics,PlayerBinput input,Rigidbody body,MethodInfo smooth,MethodInfo control,float speed,bool responsive,bool rolling)
    {
        body.linearVelocity=Vector3.forward*speed;physics.isRolling=rolling;input.responsiveSteering=responsive;
        Vector3 movement=Vector3.forward;float dt=Time.fixedDeltaTime;
        for(int i=0;i<Mathf.CeilToInt(.8f/dt);i++)
        {
            float ratio=body.linearVelocity.sqrMagnitude/(physics.MaxSpeed*physics.MaxSpeed);
            float curveRate=(input.UtopiaTurning?input.UtopiaInputLerpingRateOverSpeed:input.InputLerpingRateOverSpeed).Evaluate(ratio);
            movement=(Vector3)smooth.Invoke(input,new object[]{movement,Vector3.right,curveRate,dt});
            control.Invoke(physics,new object[]{1f,movement});
            Check(body.linearVelocity.sqrMagnitude>.001f,"Steering cannot stop Sonic completely");
        }
        return Vector3.Angle(Vector3.forward,body.linearVelocity);
    }
}
