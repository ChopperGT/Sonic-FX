using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SonicFX.Water.Editor
{
    [InitializeOnLoad]
    public static class SonicWaterRuntimeDiagnostic
    {
        static double next;
        static SonicWaterRuntimeDiagnostic()
        {
            EditorApplication.update += Capture;
            EditorApplication.update += VerifyOnce;
        }
        static void VerifyOnce()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)return;
            EditorApplication.update -= VerifyOnce;
            string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs");
            string marker=Path.Combine(folder,"Water_Fix/unity-tests.txt");
            if(File.Exists(marker))return;
            SonicWaterVerification.Run();
            SonicUnderwaterVerification.Run();
            File.WriteAllText(marker,File.ReadAllText(Path.Combine(folder,"SonicWater/unity-tests.txt"))+"\n"+
                File.ReadAllText(Path.Combine(folder,"UnderwaterEffect/unity-tests.txt")));
        }
        static void Capture()
        {
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+2;
            var text=new StringBuilder();
            text.AppendLine("timeScale="+Time.timeScale);
            foreach(var volume in SonicWaterVolume.Active)
            {
                if(volume==null)continue;
                text.AppendLine("water="+volume.name+" pos="+volume.transform.position+" scale="+volume.transform.lossyScale+
                    " dimensions="+volume.width+","+volume.depth+","+volume.length+" music="+(volume.drowningMusic!=null?volume.drowningMusic.name:"MISSING"));
                foreach(var player in UnityEngine.Object.FindObjectsByType<PlayerBhysics>())
                {
                    var state=player.GetComponent<SonicWaterPlayer>();var body=player.GetComponent<Rigidbody>();
                    text.AppendLine("player="+player.name+" local="+volume.transform.InverseTransformPoint(player.transform.position)+
                        " headInside="+volume.Contains(player.transform.position+Vector3.up*volume.breathingHeight)+
                        " physicsEnabled="+player.enabled+" kinematic="+(body!=null&&body.isKinematic)+
                        " air="+(state!=null?state.AirRemaining.ToString():"NO COMPONENT")+" underwater="+(state!=null&&state.Underwater));
                }
                if(Camera.main!=null)text.AppendLine("cameraLocal="+volume.transform.InverseTransformPoint(Camera.main.transform.position)+" inside="+volume.Contains(Camera.main.transform.position));
            }
            string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/Water_Fix");
            Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"runtime.txt"),text.ToString());
        }
    }
}
