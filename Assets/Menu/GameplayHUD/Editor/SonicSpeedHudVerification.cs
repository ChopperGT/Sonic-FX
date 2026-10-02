using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace SonicFX.HUD.Editor
{
    [InitializeOnLoad] internal static class SonicSpeedHudVerification
    {
        static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/SonicSpeedHUD");
        static SonicSpeedHudVerification(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            EditorApplication.update-=Ready;
            if(!File.Exists(Path.Combine(Folder,"unity-report.txt")))Verify();
        }
        static void Check(bool value,string label){if(!value)throw new Exception(label);}
        [MenuItem("Tools/Sonic FX/HUD/Verifier la vitesse")]
        static void Verify()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            Directory.CreateDirectory(Folder);
            var scene=EditorSceneManager.NewPreviewScene();GameObject go=null,cameraObject=null;
            RenderTexture render=null;Texture2D capture=null;
            var previousRender=RenderTexture.active;
            try
            {
                go=new GameObject("Speed HUD verification");SceneManager.MoveGameObjectToScene(go,scene);go.SetActive(false);
                var player=go.AddComponent<PlayerBhysics>();var hud=go.AddComponent<SonicGameplayHud>();
                hud.settings=AssetDatabase.LoadAssetAtPath<SonicHudSettings>("Assets/Menu/GameplayHUD/Resources/SonicHudSettings.asset");
                Check(hud.settings!=null,"Installed HUD settings available");hud.Build();
                Check(hud.ScoreText!=null&&hud.TimeText!=null&&hud.RingsText!=null&&hud.LivesText!=null,"Existing four counters preserved");
                var refresh=typeof(SonicGameplayHud).GetMethod("RefreshSpeed",BindingFlags.Instance|BindingFlags.NonPublic);
                Action<float> sample=speed=>{player.SpeedMagnitude=speed;refresh.Invoke(hud,new object[]{10f});};
                sample(0);Check(hud.SpeedText.text=="0"&&hud.SpeedText.color==Color.white&&hud.SpeedText.rectTransform.localScale==Vector3.one,"Stopped Sonic is white at normal size");
                float redAt=hud.settings.speedRedAt;
                sample(redAt/2f);Check(Mathf.Abs(hud.SpeedText.color.g-.5f)<.001f&&hud.SpeedText.rectTransform.localScale.x>1f,"Intermediate speed changes color and size progressively");
                sample(redAt);Check(hud.SpeedText.color==Color.red&&Mathf.Abs(hud.SpeedText.rectTransform.localScale.x-hud.settings.speedMaximumScale)<.001f,"Fast Sonic is red at configured maximum size");
                sample(redAt*3);Check(hud.CurrentSpeed==redAt*3&&hud.SpeedText.color==Color.red&&hud.SpeedText.rectTransform.localScale.x<=1.5f,"Number remains accurate beyond visual threshold; size stays capped");
                player.enabled=false;sample(88);Check(hud.SpeedText.text=="88","Speed still displayed when tube suspends physics component");
                sample(0);Check(hud.SpeedText.color==Color.white&&hud.SpeedText.rectTransform.localScale==Vector3.one,"Braking restores white and normal size");
                var root=(RectTransform)hud.SpeedText.transform.parent;
                Check(root.anchorMin==Vector2.zero&&root.anchorMax==Vector2.zero&&root.anchoredPosition==hud.settings.speedMargin,"Counter anchored bottom left with configured margin");
                Check(!hud.SpeedText.raycastTarget,"Counter cannot intercept gameplay input");
                // Render the actual Canvas in an isolated preview scene without starting or changing the user's level.
                sample(redAt);go.SetActive(true);player.enabled=false;hud.enabled=false;
                cameraObject=new GameObject("HUD preview camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraObject,scene);
                var camera=cameraObject.GetComponent<Camera>();camera.scene=scene;camera.orthographic=true;camera.orthographicSize=360;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.06f,.12f,.16f);camera.nearClipPlane=.01f;camera.farClipPlane=100;
                render=new RenderTexture(1270,720,24);camera.targetTexture=render;
                hud.HudCanvas.renderMode=RenderMode.ScreenSpaceCamera;hud.HudCanvas.worldCamera=camera;hud.HudCanvas.planeDistance=1;
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=render;
                capture=new Texture2D(1270,720,TextureFormat.RGB24,false);capture.ReadPixels(new Rect(0,0,1270,720),0,0);capture.Apply();
                File.WriteAllBytes(Path.Combine(Folder,"HUD-preview.png"),capture.EncodeToPNG());
                File.WriteAllText(Path.Combine(Folder,"unity-report.txt"),"PASS\nExisting HUD counters preserved; live physics speed source including tubes; progressive white-to-red color and capped growth; braking restores normal appearance; bottom-left anchor and non-interactive label validated. Preview rendered from actual HUD Canvas.\n"+DateTime.Now.ToString("s"));
                Debug.Log("HUD : compteur de vitesse verifie.");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Folder,"unity-report.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally
            {
                RenderTexture.active=previousRender;
                if(cameraObject!=null)Object.DestroyImmediate(cameraObject);
                if(capture!=null)Object.DestroyImmediate(capture);
                if(render!=null){render.Release();Object.DestroyImmediate(render);}
                if(go!=null)Object.DestroyImmediate(go);EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
