using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;

namespace SonicFX.Piranha.Editor
{
    [InitializeOnLoad]
    public static class PiranhaRuntimeDiagnostic
    {
        static double next;
        static PiranhaRuntimeDiagnostic() { EditorApplication.update+=Capture; }
        static void Capture()
        {
            if(!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+2;
            var report=new StringBuilder();report.AppendLine("timeScale="+Time.timeScale);
            foreach(var fish in Object.FindObjectsByType<PiranhaController>())
            {
                var serialized=new SerializedObject(fish);
                report.AppendLine(fish.name+" position="+fish.transform.position+" scale="+fish.transform.lossyScale);
                report.AppendLine("status="+serialized.FindProperty("status").stringValue+" distance="+serialized.FindProperty("distanceToSonic").floatValue);
                report.AppendLine("jumpHeight="+fish.jumpHeight+" maxDistance="+fish.maximumJumpDistance+" dryGap="+fish.maximumDryGap+" radius="+fish.bodyRadius);
                if(fish.water!=null)report.AppendLine("water="+fish.water.name+" surface="+fish.water.transform.position+" dimensions="+fish.water.width+","+fish.water.depth+","+fish.water.length);
                if(fish.player==null)continue;
                Vector3 point=fish.player.transform.position;
                report.AppendLine("Sonic="+point+" aim="+(point+Vector3.up*.65f));
                int count=0;
                foreach(var col in Physics.OverlapSphere(point,12,fish.environmentLayers,QueryTriggerInteraction.Ignore))
                {
                    if(col.GetComponentInParent<PlayerBhysics>()!=null || col.transform.IsChildOf(fish.transform))continue;
                    report.AppendLine("obstacle="+col.name+" center="+col.bounds.center+" size="+col.bounds.size);
                    if(++count>=16)break;
                }
            }
            Directory.CreateDirectory(PiranhaBuilder.ReportFolder);
            File.WriteAllText(Path.Combine(PiranhaBuilder.ReportFolder,"runtime.txt"),report.ToString());
        }
    }
}
