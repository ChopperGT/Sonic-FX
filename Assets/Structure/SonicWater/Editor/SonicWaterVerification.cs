using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SonicFX.Water.Editor
{
    public static class SonicWaterVerification
    {
        static void Check(bool condition,string description)
        {
            if(!condition)throw new InvalidOperationException(description);
        }
        static void Call(object component,string method)
        {
            component.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(component,null);
        }
        [MenuItem("Tools/Sonic FX/Eau/Verifier la logique de l'eau")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            string report=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/SonicWater/unity-tests.txt");
            var previous=SonicWaterVolume.Active.ToArray();
            GameObject basin=null,sonic=null,overlap=null;
            float timeScale=Time.timeScale;
            try
            {
                SonicWaterVolume.Active.Clear();
                basin=new GameObject("Water verification") { hideFlags=HideFlags.HideAndDontSave };
                basin.transform.position=new Vector3(100000,100,100000);
                var volume=basin.AddComponent<SonicWaterVolume>();
                volume.width=30;volume.length=30;volume.depth=10;
                Check(volume.Contains(basin.transform.position+Vector3.down*5),"Inside water");
                Check(!volume.Contains(basin.transform.position+Vector3.up),"Above water");
                Check(!volume.Contains(basin.transform.position+Vector3.down*11),"Below water bounds");
                Check(volume.CrossesSurface(basin.transform.position+Vector3.up*20,basin.transform.position+Vector3.down*20,out var hit),"Fast crossing");
                Check(Mathf.Abs(hit.y-100)<.001f,"Splash on surface");
                sonic=new GameObject("Sonic water verification") { hideFlags=HideFlags.HideAndDontSave };
                var body=sonic.AddComponent<Rigidbody>();body.useGravity=false;
                var player=sonic.AddComponent<PlayerBhysics>();
                var jump=sonic.AddComponent<Action01_Jump>();
                var hurt=sonic.AddComponent<HurtControl>();
                player.TopSpeed=50;player.MaxSpeed=100;player.MoveAccell=.3f;player.Gravity=new Vector3(0,-1.5f,0);player.MaxFallingSpeed=-500;
                jump.JumpSpeed=3;
                var water=sonic.AddComponent<SonicWaterPlayer>();Call(water,"Awake");
                Time.timeScale=1;
                body.position=basin.transform.position+Vector3.down*2;
                Call(water,"FixedUpdate");
                Check(water.Underwater,"Head immersion");
                Check(player.TopSpeed<50&&jump.JumpSpeed>3,"Slower motion and higher jump");
                float air=water.AirRemaining;
                overlap=new GameObject("Overlap verification") {hideFlags=HideFlags.HideAndDontSave};overlap.transform.position=basin.transform.position;
                var second=overlap.AddComponent<SonicWaterVolume>();second.depth=10;
                for(int i=0;i<4;i++)Call(water,"FixedUpdate");
                Check(water.AirRemaining<air,"Overlapping zones must not refill air");
                float pausedAir=water.AirRemaining;Time.timeScale=0;Call(water,"FixedUpdate");
                Check(water.AirRemaining==pausedAir,"Pause freezes air");Time.timeScale=1;
                body.position=basin.transform.position+Vector3.down*.4f;Call(water,"FixedUpdate");
                Check(!water.Underwater&&water.AirRemaining==30,"Head above water refills air");
                body.position=basin.transform.position+Vector3.up*2;Call(water,"FixedUpdate");
                Check(player.TopSpeed==50&&player.MaxSpeed==100&&jump.JumpSpeed==3&&player.Gravity.y==-1.5f,"Restore on exit");
                body.position=basin.transform.position+Vector3.down*2;Call(water,"FixedUpdate");
                Check(player.TopSpeed<50,"Reentry");
                // Reproduce a descent past the former ten-unit bottom without refilling air.
                volume.depth=100;
                body.position=basin.transform.position+Vector3.down*50;
                float deepAir=water.AirRemaining;
                Call(water,"FixedUpdate");
                Check(water.Underwater&&water.AirRemaining<deepAir,"Deep immersion continues countdown");
                Check(volume.Contains(basin.transform.position+Vector3.down*99),"Camera stays inside deep water");
                Check(!volume.Contains(basin.transform.position+Vector3.down*101),"Finite bottom preserved");
                int frames=Mathf.CeilToInt(30/Time.fixedDeltaTime)+2;
                bool warned=false;
                for(int i=0;i<frames&&!hurt.isDead;i++)
                {
                    Call(water,"FixedUpdate");
                    bool alarm=(bool)typeof(SonicWaterPlayer).GetField("alarmStarted",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(water);
                    if(water.AirRemaining>10)Check(!alarm,"No early warning");
                    if(alarm)warned=true;
                }
                Check(warned,"Warning at ten seconds");Check(hurt.isDead,"Drowning calls existing death state");
                Check(player.TopSpeed==50&&jump.JumpSpeed==3,"Death restores movement values");
                hurt.isDead=false;body.position=basin.transform.position+Vector3.up*2;Call(water,"FixedUpdate");
                Check(water.AirRemaining==30,"Respawn resets air");
                body.position=basin.transform.position+Vector3.down*2;Call(water,"FixedUpdate");
                water.enabled=false;
                // Normal MonoBehaviour callbacks are not dispatched in Edit Mode.
                Call(water,"OnDisable");
                Check(player.TopSpeed==50&&jump.JumpSpeed==3,"Disable restores physics");
                File.WriteAllText(report,"PASS\nBounds, fast surface crossing, slow movement, jump, overlaps, pause, breathing, exit, reentry, warning at 10, death at 0, respawn, cleanup.\nEditor integration checks; visual and audio Play test remains necessary.");
                Debug.Log("Verification eau : tous les controles logiques passent.");
            }
            catch(Exception e){File.WriteAllText(report,"FAIL\n"+e);Debug.LogException(e);}
            finally
            {
                Time.timeScale=timeScale;
                if(sonic!=null)UnityEngine.Object.DestroyImmediate(sonic);
                if(overlap!=null)UnityEngine.Object.DestroyImmediate(overlap);
                if(basin!=null)UnityEngine.Object.DestroyImmediate(basin);
                SonicWaterVolume.Active.Clear();SonicWaterVolume.Active.AddRange(previous);
            }
        }
    }
}
