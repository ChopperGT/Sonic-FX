using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace SonicFX.Menu.Editor
{
    [InitializeOnLoad] public static class SonicXMenuRuntimeCheck
    {
        static double next; static string capturedPage="";
        static SonicXMenuRuntimeCheck(){EditorApplication.update+=Check;}
        static void Check()
        {
            if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+3;
            Directory.CreateDirectory(SonicXFrontMenuBuilder.Reports);var scene=SceneManager.GetActiveScene();
            if(scene.name=="LogoScreen")
            {
                var menu=Object.FindAnyObjectByType<SonicXFrontMenu>();if(menu!=null)
                {
                    File.WriteAllText(Path.Combine(SonicXFrontMenuBuilder.Reports,"runtime.txt"),"Menu: "+menu.CurrentPage+"\nButtons: "+string.Join(", ",menu.GetComponentsInChildren<UnityEngine.UI.Button>().Select(b=>b.name)));
                    string page=menu.CurrentPage.ToString();if(capturedPage!=page){capturedPage=page;ScreenCapture.CaptureScreenshot(Path.Combine(SonicXFrontMenuBuilder.Reports,"Game_"+page+".png"));}
                }
            }
            else if(scene.path==SonicXProgress.FirstLevel)
            {
                var players=Object.FindObjectsByType<PlayerBhysics>();bool saved=SonicXProgress.TryRead(out var save) && save.scene==scene.path;
                File.WriteAllText(Path.Combine(SonicXFrontMenuBuilder.Reports,"runtime.txt"),"Level: "+scene.path+"\nPlayers: "+players.Length+"\nStory saved: "+saved);
            }
        }
    }
}
