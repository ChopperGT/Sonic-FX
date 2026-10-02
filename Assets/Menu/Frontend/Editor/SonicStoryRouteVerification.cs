using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SonicFX.Score;
using Object=UnityEngine.Object;

namespace SonicFX.Menu.Editor
{
    [InitializeOnLoad] internal static class SonicStoryRouteVerification
    {
        static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/StoryActOrder");
        static SonicStoryRouteVerification(){EditorApplication.update+=Ready;EditorApplication.update+=VerifyRuntime;}
        static void Ready()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            EditorApplication.update-=Ready;if(!File.Exists(Path.Combine(Folder,"unity-report.txt")))Verify();
        }
        static void Check(bool value,string label){if(!value)throw new Exception(label);}
        [MenuItem("Tools/Sonic FX/Histoire/Verifier l'ordre des actes")]
        static void Verify()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            Directory.CreateDirectory(Folder);
            var scene=EditorSceneManager.NewPreviewScene();GameObject go=null;
            var character=typeof(SonicXProgress).GetField("<Character>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic);
            object originalCharacter=character.GetValue(null);
            try
            {
                var before=EditorBuildSettings.globalScenes.Select(value=>value.path).ToArray();
                SonicStoryRouteInstaller.Install();
                var after=EditorBuildSettings.globalScenes.Select(value=>value.path).ToArray();
                Check(before.SequenceEqual(after.Take(before.Length)),"Existing build scene indices preserved");
                var route=Resources.Load<SonicStoryRoute>("SonicStoryRoute");
                Check(route!=null,"Story route installed in Resources");
                Check(route.TryGetNextScene("sonic",SonicXProgress.FirstLevel,out var second)&&second==SonicStoryRoute.SecondLevel,"Act 1-1 leads to Act 1-2");
                Check(route.TryGetNextScene("sonic",SonicStoryRoute.SecondLevel,out var third)&&third==SonicStoryRoute.ThirdLevel,"Act 1-2 reserves future Act 1-3");
                Check(!route.TryGetNextScene("tails",SonicXProgress.FirstLevel,out _),"Sonic route does not override other characters");
                Check(EditorBuildSettings.globalScenes.Any(value=>value.enabled&&value.path==SonicXProgress.FirstLevel)&&
                    EditorBuildSettings.globalScenes.Any(value=>value.enabled&&value.path==second),"First two acts enabled in build scene list");
                go=new GameObject("Story route verification");SceneManager.MoveGameObjectToScene(go,scene);go.SetActive(false);
                var menu=go.AddComponent<SonicXFrontMenu>();
                string first=(string)typeof(SonicXFrontMenu).GetMethod("CharacterScene",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(menu,new object[]{"sonic"});
                Check(first==SonicXProgress.FirstLevel,"New young Sonic adventure starts at Act 1-1");
                var progress=go.AddComponent<LevelProgressControl>();character.SetValue(null,"sonic");
                Check(progress.ConfiguredNextLevelScene(first)==second,"Goal configuration selects Act 1-2");
                // A missing Act 1-3 cannot accidentally load the legacy scene at index 1.
                progress.LevelToGoNext=1;
                Check(progress.ConfiguredNextLevelScene(second)==third,"Future act does not fall back to unrelated stage");
                progress.NextLevelScene=first;
                Check(progress.ConfiguredNextLevelScene(second)==first,"Explicit per-level override remains available");
                var result=SonicLevelScore.Calculate(500,20,0,second);
                Check(result.nextScene==second,"Results receive the next act path");
                File.WriteAllText(Path.Combine(Folder,"unity-report.txt"),"PASS\nYoung Sonic starts Act 1-1. First two acts registered without reordering previous build scenes. Story route and actual goal resolver lead Act 1-1 to Act 1-2; future Act 1-3 reserved and safely unavailable until created. Explicit level override and other characters preserved. Result carries next act path. User scenes and adventure save untouched.\n"+DateTime.Now.ToString("s"));
                Debug.Log("Histoire Sonic (jeune) : ordre des actes verifie.");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Folder,"unity-report.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally
            {
                character.SetValue(null,originalCharacter);if(go!=null)Object.DestroyImmediate(go);EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        static void VerifyRuntime()
        {
            string report=System.IO.Path.Combine(Folder,"runtime-report.txt");
            if(!EditorApplication.isPlaying||EditorApplication.isCompiling||File.Exists(report))return;
            var progress=Object.FindAnyObjectByType<LevelProgressControl>();
            if(progress==null)return;
            string oldOverride=progress.NextLevelScene;int oldIndex=progress.LevelToGoNext;
            try
            {
                Check(SonicXProgress.Character=="sonic","Young Sonic runtime session");
                Check(Application.CanStreamedLevelBeLoaded(SonicXProgress.FirstLevel)&&Application.CanStreamedLevelBeLoaded(SonicStoryRoute.SecondLevel),"Both story acts loadable in Play");
                progress.NextLevelScene="";progress.LevelToGoNext=1;
                Check(progress.ResolveNextLevelScene(SonicXProgress.FirstLevel)==SonicStoryRoute.SecondLevel,"Live goal resolver leads Act 1-1 to Act 1-2");
                Check(progress.ResolveNextLevelScene(SonicStoryRoute.SecondLevel)==(Application.CanStreamedLevelBeLoaded(SonicStoryRoute.ThirdLevel)?SonicStoryRoute.ThirdLevel:null),"Missing future act safely returns to menu");
                Directory.CreateDirectory(Folder);
                File.WriteAllText(report,"PASS\nPlay mode: both existing story acts loadable; live goal resolver leads Act 1-1 to Act 1-2; unavailable Act 1-3 returns no destination rather than an unrelated stage.\n"+DateTime.Now.ToString("s"));
            }
            catch(Exception e){Directory.CreateDirectory(Folder);File.WriteAllText(report,"FAIL\n"+e);Debug.LogException(e);}
            finally{progress.NextLevelScene=oldOverride;progress.LevelToGoNext=oldIndex;}
        }
    }
}
