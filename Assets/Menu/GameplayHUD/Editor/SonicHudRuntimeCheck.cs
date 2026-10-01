using System.IO;
using UnityEditor;
using UnityEngine;
using SonicFX.Menu;
namespace SonicFX.HUD.Editor
{
    [InitializeOnLoad] public static class SonicHudRuntimeCheck
    {
        static double next;static bool captured;
        static SonicHudRuntimeCheck(){EditorApplication.update+=Check;}
        static void Check()
        {
            if(!EditorApplication.isPlaying){captured=false;return;}
            if(EditorApplication.isCompiling || EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+3;
            var hud=Object.FindAnyObjectByType<SonicGameplayHud>();if(hud==null || hud.HudCanvas==null)return;
            Directory.CreateDirectory(SonicHudInstaller.Reports);
            File.WriteAllText(Path.Combine(SonicHudInstaller.Reports,"runtime.txt"),"HUD active: "+hud.HudCanvas.isActiveAndEnabled+"\nLives: "+SonicXProgress.Lives+"\nStory session: "+SonicXProgress.IsStorySession+"\nTime: "+hud.TimeText.text+"\nRings: "+hud.RingsText.text+"\nGame over: "+SonicXProgress.IsGameOver);
            if(!captured && hud.Elapsed>1){captured=true;ScreenCapture.CaptureScreenshot(Path.Combine(SonicHudInstaller.Reports,"HUD_Unity.png"));}
        }
    }
}
