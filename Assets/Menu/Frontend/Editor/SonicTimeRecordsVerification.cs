using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SonicFX.Score;
using Object=UnityEngine.Object;

namespace SonicFX.Menu.Editor
{
    [InitializeOnLoad] internal static class SonicTimeRecordsVerification
    {
        static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/TimeRankings");
        static SonicTimeRecordsVerification(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            EditorApplication.update-=Ready;if(!File.Exists(Path.Combine(Folder,"unity-report.txt")))Verify();
        }
        static void Check(bool value,string label){if(!value)throw new Exception(label);}
        static Dictionary<FieldInfo,object> Snapshot(Type type)=>type.GetFields(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)
            .Where(field=>!field.IsLiteral&&!field.IsInitOnly).ToDictionary(field=>field,field=>field.GetValue(null));
        [MenuItem("Tools/Sonic FX/Menu/Verifier les classements")]
        static void Verify()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            Directory.CreateDirectory(Folder);
            string key="SonicFX.Tests.Records."+Guid.NewGuid().ToString("N");
            const string realKey="SonicFX.TimeRecords.v1";
            bool hadReal=PlayerPrefs.HasKey(realKey);string originalReal=PlayerPrefs.GetString(realKey,"");
            var savedProgress=Snapshot(typeof(SonicXProgress));var savedScore=Snapshot(typeof(SonicLevelScore));
            var scene=EditorSceneManager.NewPreviewScene();GameObject stage=null,cameraObject=null;RenderTexture render=null;Texture2D capture=null;
            var previousRender=RenderTexture.active;
            try
            {
                var insert=typeof(SonicTimeRecords).GetMethod("RecordToKey",BindingFlags.Static|BindingFlags.NonPublic);
                var read=typeof(SonicTimeRecords).GetMethod("ReadKey",BindingFlags.Static|BindingFlags.NonPublic);
                Action<float> add=seconds=>insert.Invoke(null,new object[]{key,SonicXProgress.FirstLevel,seconds,"sonic"});
                Func<List<SonicLevelTimeRecords>> levels=()=>
                {
                    object save=read.Invoke(null,new object[]{key});
                    return (List<SonicLevelTimeRecords>)save.GetType().GetField("levels").GetValue(save);
                };
                Check(levels().Count==0,"Empty rankings are supported");
                foreach(float time in new[]{120.125f,90.5f,80.75f,180f,100f,70.25f,95f})add(time);
                var first=levels().Single();
                Check(first.records.Select(value=>value.milliseconds).SequenceEqual(new long[]{70250,80750,90500,95000,100000}),"Five fastest times retained in ascending order");
                string before=PlayerPrefs.GetString(key);
                foreach(float invalid in new[]{0f,-1f,float.NaN,float.PositiveInfinity})add(invalid);
                Check(PlayerPrefs.GetString(key)==before,"Invalid or unfinished times cannot be recorded");
                add(999f);Check(PlayerPrefs.GetString(key)==before,"Slower sixth record does not replace the top five");
                string secondScene="Assets/Level/Sonic 1/Act 1 GreenHiill/act 1-2/act 1-2.unity";
                insert.Invoke(null,new object[]{key,secondScene,62.125f,"tails"});
                var persisted=levels();Check(persisted.Count==2&&persisted.Single(value=>value.scene==secondScene).records[0].character=="tails","Levels separated and character persisted");
                Check(persisted.Single(value=>value.scene==SonicXProgress.FirstLevel).records.Count==5,"Adding another level preserves first level records");
                Check(SonicTimeRecords.FormatTime(61234)=="01'01\"234","Millisecond formatting exact");
                Check(SonicTimeRecords.LevelName(SonicXProgress.FirstLevel)=="Sonic 1 - Act 1-1","Readable level title");
                foreach(string malformed in new[]{"{broken","null","{\"version\":99}","{\"version\":1,\"levels\":null}"})
                {
                    PlayerPrefs.SetString(key,malformed);Check(levels().Count==0,"Invalid record data handled");
                }
                PlayerPrefs.SetString(key,before);
                foreach(var level in persisted)Check(level.records.Count<=5&&level.records.All(value=>!string.IsNullOrEmpty(value.achievedUtc)),"Five-record cap and completion date preserved on reload");

                typeof(SonicXProgress).GetField("storySession",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,false);
                typeof(SonicXProgress).GetField("<Lives>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,3);
                SonicLevelScore.BeginLevel(scene);SonicLevelScore.Tick(61.25f,false);var result=SonicLevelScore.Complete(0);
                Check(result.elapsedSeconds==61.25f&&!SonicLevelScore.IsRunning,"Completion captures the actual gameplay timer");
                Check(SonicLevelScore.Complete(0)==result,"Repeated completion is ignored");

                stage=new GameObject("Rankings preview");SceneManager.MoveGameObjectToScene(stage,scene);stage.SetActive(false);
                var menu=stage.AddComponent<SonicXFrontMenu>();menu.logo=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Menu/image/logo SonicX.png");
                menu.Show(SonicXFrontMenu.Page.Main);
                menu.GetComponentsInChildren<Button>(true).Single(value=>value.name=="Classement").onClick.Invoke();
                Check(menu.CurrentPage==SonicXFrontMenu.Page.Rankings,"Main menu opens rankings");
                foreach(var button in menu.GetComponentsInChildren<Button>(true).Where(value=>value.interactable))
                    Check(button.navigation.selectOnUp!=null&&button.navigation.selectOnUp.interactable&&button.navigation.selectOnDown!=null&&button.navigation.selectOnDown.interactable,"Controller navigation skips unavailable level buttons");
                menu.GetComponentsInChildren<Button>(true).Single(value=>value.name=="Retour").onClick.Invoke();Check(menu.CurrentPage==SonicXFrontMenu.Page.Main,"Rankings return to main menu");
                menu.Show(SonicXFrontMenu.Page.Loading);
                typeof(SonicXFrontMenu).GetMethod("DrawRankings",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(menu,new object[]{persisted});
                Check(menu.GetComponentsInChildren<Text>(true).Any(value=>value.text=="01'10\"250"),"Best persisted record shown in menu");
                Check(menu.transform.Find("SonicX Menu UI/Menu/Records du niveau/Record 5")!=null,"All five ranking rows created");

                stage.SetActive(true);menu.enabled=false;
                cameraObject=new GameObject("Rankings camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraObject,scene);
                var camera=cameraObject.GetComponent<Camera>();camera.scene=scene;camera.orthographic=true;camera.orthographicSize=360;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.nearClipPlane=.01f;camera.farClipPlane=100;
                render=new RenderTexture(1270,720,24);camera.targetTexture=render;
                menu.MenuCanvas.renderMode=RenderMode.ScreenSpaceCamera;menu.MenuCanvas.worldCamera=camera;menu.MenuCanvas.planeDistance=1;
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=render;
                capture=new Texture2D(1270,720,TextureFormat.RGB24,false);capture.ReadPixels(new Rect(0,0,1270,720),0,0);capture.Apply();
                File.WriteAllBytes(Path.Combine(Folder,"Classement-preview.png"),capture.EncodeToPNG());
                Check(PlayerPrefs.HasKey(realKey)==hadReal&&PlayerPrefs.GetString(realKey,"")==originalReal,"User's real records untouched by verification");
                File.WriteAllText(Path.Combine(Folder,"unity-report.txt"),"PASS\nFive fastest times sorted and persisted per level; slower/invalid times rejected; character and date retained on reload; malformed saves handled. Completion timer and duplicate goal gate verified. Main menu, rankings navigation and five visible records verified. Actual Canvas preview rendered with isolated sample data. User records preserved.\n"+DateTime.Now.ToString("s"));
                Debug.Log("Classement : enregistrement des temps et menu verifies.");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Folder,"unity-report.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally
            {
                PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();RenderTexture.active=previousRender;
                if(cameraObject!=null)Object.DestroyImmediate(cameraObject);if(capture!=null)Object.DestroyImmediate(capture);
                if(render!=null){render.Release();Object.DestroyImmediate(render);}if(stage!=null)Object.DestroyImmediate(stage);EditorSceneManager.ClosePreviewScene(scene);
                foreach(var pair in savedScore)pair.Key.SetValue(null,pair.Value);foreach(var pair in savedProgress)pair.Key.SetValue(null,pair.Value);
            }
        }
    }
}
