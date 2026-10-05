using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SonicFX.HUD;
using Object=UnityEngine.Object;

namespace SonicFX.Score.Editor
{
    [InitializeOnLoad]
    static class SonicLevelMedalsVerification
    {
        const string Reports="C:/Users/Lecle/Documents/Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/LevelMedals";
        static SonicLevelMedalsVerification(){EditorApplication.update+=Ready;}
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        static void Same(float[] a,float[] b,string message){Check(a.Length==b.Length,message);for(int i=0;i<a.Length;i++)Check(Mathf.Abs(a[i]-b[i])<.001f,message);}
        static void Ready()
        {
            string request=Path.Combine(Reports,"request.txt");
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request))return;
            File.Delete(request);Verify();
        }
        [MenuItem("Tools/Sonic FX/Niveau/Verifier les reglages des medailles")]
        static void Verify()
        {
            var active=SceneManager.GetActiveScene();bool dirty=active.isDirty;var selected=Selection.objects;
            var a=EditorSceneManager.NewPreviewScene();var b=EditorSceneManager.NewPreviewScene();UnityEditor.Editor inspector=null;
            try{
                var legacy=new[]{65f,80f,95f,140f};var expanded=new[]{65f,80f,95f,140f,175f};Same(SonicLevelScore.MedalTimes(a,legacy,120),expanded,"Legacy explicit times lost");
                Same(SonicLevelScore.MedalTimes(a,null,80),new[]{64f,72f,80f,100f,125f},"Legacy derived times lost");
                var settings=SonicLevelMedalsEditor.CreateForScene(a,false);settings.SetTimes(new[]{60f,75f,90f,120f});
                Check(SonicLevelMedalsEditor.CreateForScene(a,false)==settings,"Installer made duplicate settings");
                Same(SonicLevelScore.MedalTimes(a,legacy,40),settings.GetTimes(),"Map does not override character config");
                Same(SonicLevelScore.MedalTimes(a,null,200),settings.GetTimes(),"Thresholds depend on character");
                var other=SonicLevelMedalsEditor.CreateForScene(b,false);other.SetTimes(new[]{20f,25f,30f,40f});
                Same(SonicLevelScore.MedalTimes(a,null,120),new[]{60f,75f,90f,120f,150f},"Other loaded map changes thresholds");
                Same(SonicLevelScore.MedalTimes(b,null,120),new[]{20f,25f,30f,40f,50f},"Second map does not use own thresholds");
                var thresholds=settings.GetTimes();
                Check(SonicMedal.TierFor(60,thresholds)==SonicMedal.Rainbow,"Exact rainbow threshold");
                Check(SonicMedal.TierFor(60.01f,thresholds)==SonicMedal.Diamond,"Diamond threshold");
                Check(SonicMedal.TierFor(75.01f,thresholds)==SonicMedal.Gold,"Gold threshold");
                Check(SonicMedal.TierFor(90.01f,thresholds)==SonicMedal.Silver,"Silver threshold");
                Check(SonicMedal.TierFor(120,thresholds)==SonicMedal.Silver,"Exact silver threshold");
                Check(SonicMedal.TierFor(120.01f,thresholds)==SonicMedal.Bronze,"Bronze begins after silver");
                Check(SonicMedal.TierFor(150,thresholds)==SonicMedal.Bronze,"Exact bronze threshold");
                Check(SonicMedal.TierFor(150.01f,thresholds)==-1,"No medal beyond bronze");
                Check(SonicMedal.TierFor(140,legacy)==SonicMedal.Silver && SonicMedal.TierFor(140.01f,legacy)==-1,"Four-tier legacy result compatibility");
                settings.BronzeSeconds=-1;Check(settings.GetTimes()[4]==150,"Missing bronze value migration");
                settings.SetTimes(new[]{60f,75f,90f,120f,180f});Check(settings.GetTimes()[4]==180,"Editable bronze threshold ignored");
                thresholds[0]=999;Check(settings.GetTimes()[0]==60,"Consumers can change component's settings");
                settings.enabled=false;Same(SonicLevelScore.MedalTimes(a,legacy,120),expanded,"Disabled config did not fall back");
                Check(SonicLevelMedalsEditor.CreateForScene(a,false)==settings,"Disabled config duplicated");settings.enabled=true;
                settings.SetTimes(new[]{float.NaN,-1f,20f,float.PositiveInfinity});var sanitized=settings.GetTimes();
                Check(sanitized[0]>0 && sanitized[1]>=sanitized[0] && sanitized[2]>=sanitized[1] && sanitized[3]>=sanitized[2] && !float.IsInfinity(sanitized[3]),"Invalid thresholds not sanitized");
                inspector=UnityEditor.Editor.CreateEditor(settings);Check(inspector is SonicLevelMedalsEditor,"Custom Inspector missing");
                VerifyVisuals(a);
                Check(SceneManager.GetActiveScene()==active && active.isDirty==dirty,"Open map changed");
                Check(Selection.objects.Length==selected.Length,"Selection changed");for(int i=0;i<selected.Length;i++)Check(Selection.objects[i]==selected[i],"Selection changed");
                Directory.CreateDirectory(Reports);File.WriteAllText(Path.Combine(Reports,"verification.txt"),"PASS: all five tiers and exact boundaries, Bronze/no-medal cases, legacy four-tier migration, editable Bronze, per-map isolation, custom Inspector, idempotent installer. All five medal widgets generated; Bronze has copper icon, Sonic centre, checkmark, pop animation, rotation and reflection; screenshots saved. Open scene and selection untouched, no saves or records written.\n"+DateTime.Now.ToString("s"));
            }catch(Exception e){Directory.CreateDirectory(Reports);File.WriteAllText(Path.Combine(Reports,"verification.txt"),"FAIL\n"+e);}
            finally{if(inspector!=null)Object.DestroyImmediate(inspector);EditorSceneManager.ClosePreviewScene(a);EditorSceneManager.ClosePreviewScene(b);}
        }
        static void VerifyVisuals(Scene scene)
        {
            var go=new GameObject("Medal visuals verification");SceneManager.MoveGameObjectToScene(go,scene);
            var canvasGo=new GameObject("Medal preview",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));SceneManager.MoveGameObjectToScene(canvasGo,scene);
            var camera=go.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.02f,.05f,.12f);camera.orthographic=true;camera.orthographicSize=540;camera.nearClipPlane=.01f;camera.farClipPlane=100;
            camera.scene=scene;
            camera.cullingMask=1<<30;canvasGo.layer=30;
            var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;
            ((RectTransform)canvasGo.transform).sizeDelta=new Vector2(1920,1080);camera.transform.position=new Vector3(0,0,-10);
            var medals=new SonicMedal[5];var privateField=BindingFlags.Instance|BindingFlags.NonPublic;
            for(int tier=0;tier<5;tier++){
                float x=960+(SonicMedal.Rainbow-tier-2)*300;
                medals[tier]=SonicMedal.Create(canvasGo.transform,tier,new Vector2(.5f,.5f),new Vector2(x-960,0),230,true,tier==SonicMedal.Bronze,.25f+tier*.18f);
                SonicUi.Text(canvasGo.transform,SonicMedal.Names[tier],SonicUi.DesignFont(),26,SonicMedal.LabelColors[tier],TextAnchor.MiddleCenter,new Vector2(.5f,.5f),new Vector2(x-960,-170),new Vector2(280,36));
            }
            var bronze=medals[SonicMedal.Bronze];var pop=(RectTransform)typeof(SonicMedal).GetField("pop",privateField).GetValue(bronze);
            Check(pop.localScale==Vector3.zero,"Bronze appearance animation missing");
            typeof(SonicMedal).GetField("start",privateField).SetValue(bronze,Time.unscaledTime-.6f);typeof(SonicMedal).GetMethod("Update",privateField).Invoke(bronze,null);
            Check(pop.localScale.x>0 && pop.localScale.x<1.13f && Mathf.Abs(pop.localEulerAngles.z)>1,"Bronze pop/tilt animation missing");
            var centre=pop.Find("Centre").GetComponent<RawImage>();Check(centre.texture!=null,"Bronze Sonic centre missing");
            Check(pop.Find("Coche")!=null && pop.Find("Anneau")!=null && pop.Find("Reflet")!=null,"Bronze effects missing");
            var bronzeTexture=SonicMedal.BaseTexture(SonicMedal.Bronze,true,true);var color=bronzeTexture.GetPixel(32,32);
            Check(color.r>color.g && color.g>color.b && color.a>.9f,"Bronze icon is not copper coloured");
            Check(SonicMedal.BaseTexture(SonicMedal.Silver,true,true)!=bronzeTexture,"Bronze icon reused silver texture");
            foreach(var medal in medals){
                float delay=(float)typeof(SonicMedal).GetField("delay",privateField).GetValue(medal);
                typeof(SonicMedal).GetField("start",privateField).SetValue(medal,Time.unscaledTime-delay-2);typeof(SonicMedal).GetMethod("Update",privateField).Invoke(medal,null);
            }
            var shine=(RectTransform)typeof(SonicMedal).GetField("shine",privateField).GetValue(bronze);Check(shine.sizeDelta.y>0,"Bronze reflection is not animated");
            typeof(SonicMedal).GetField("start",privateField).SetValue(bronze,Time.unscaledTime-.25f-1.1f);typeof(SonicMedal).GetMethod("Update",privateField).Invoke(bronze,null);
            Check(Mathf.Abs(pop.localEulerAngles.y)>1,"Bronze coin rotation missing");
            // Settle after rotation for a readable icon screenshot.
            typeof(SonicMedal).GetField("start",privateField).SetValue(bronze,Time.unscaledTime-.25f-2);typeof(SonicMedal).GetMethod("Update",privateField).Invoke(bronze,null);
            foreach(var transform in canvasGo.GetComponentsInChildren<Transform>(true))transform.gameObject.layer=30;
            RenderTexture rt=null;Texture2D shot=null;var previous=RenderTexture.active;
            try{
                rt=new RenderTexture(1920,1080,24);camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();
                RenderTexture.active=rt;shot=new Texture2D(1920,1080,TextureFormat.RGB24,false);shot.ReadPixels(new Rect(0,0,1920,1080),0,0);shot.Apply();
                Directory.CreateDirectory(Reports);File.WriteAllBytes(Path.Combine(Reports,"five-medals.png"),shot.EncodeToPNG());
                int visiblePixels=0;var background=shot.GetPixel(0,0);
                for(int y=250;y<800;y+=10)for(int x=150;x<1750;x+=10){var pixel=shot.GetPixel(x,y);if(Mathf.Abs(pixel.r-background.r)+Mathf.Abs(pixel.g-background.g)+Mathf.Abs(pixel.b-background.b)>.15f)visiblePixels++;}
                Check(visiblePixels>500,"Medal screenshot is empty or medals did not render");
            }finally{RenderTexture.active=previous;camera.targetTexture=null;if(shot!=null)Object.DestroyImmediate(shot);if(rt!=null){rt.Release();Object.DestroyImmediate(rt);}}
        }
    }
}
