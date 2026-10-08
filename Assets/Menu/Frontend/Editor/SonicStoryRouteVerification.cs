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
            EditorApplication.update-=Ready;
            string request=Path.Combine(Folder,"verify-request.txt");
            bool requested=File.Exists(request);
            if(requested)File.Delete(request);
            if(requested||!File.Exists(Path.Combine(Folder,"unity-report.txt")))Verify();
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
                Check(route.TryGetNextScene("sonic",SonicStoryRoute.SecondLevel,out var third)&&third==SonicStoryRoute.MarbleFirstLevel,"Act 1-2 leads to Act 2-1");
                Check(route.TryGetNextScene("sonic",third,out var end)&&string.IsNullOrEmpty(end),"Act 2-1 ends the current story route");
                Check(route.TryGetNextScene("classicsonic",SonicStoryRoute.SecondLevel,out var classicNext)&&classicNext==third,"Classic Sonic shares the next story act");
                Check(!route.TryGetNextScene("tails",SonicXProgress.FirstLevel,out _),"Sonic route does not override other characters");
                Check(EditorBuildSettings.globalScenes.Any(value=>value.enabled&&value.path==SonicXProgress.FirstLevel)&&
                    EditorBuildSettings.globalScenes.Any(value=>value.enabled&&value.path==second)&&
                    EditorBuildSettings.globalScenes.Any(value=>value.enabled&&value.path==third),"All three acts enabled in build scene list");
                Check(AssetDatabase.LoadAssetAtPath<SceneAsset>(third)!=null,"Act 2-1 scene exists");
                go=new GameObject("Story route verification");SceneManager.MoveGameObjectToScene(go,scene);
                var menu=go.AddComponent<SonicXFrontMenu>();
                string first=(string)typeof(SonicXFrontMenu).GetMethod("CharacterScene",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(menu,new object[]{"sonic"});
                Check(first==SonicXProgress.FirstLevel,"New young Sonic adventure starts at Act 1-1");
                menu.Show(SonicXFrontMenu.Page.NewLevels);
                Check(menu.GetComponentsInChildren<UnityEngine.UI.Button>().Any(button=>button.name=="Act 2-1"),"Act 2-1 available in New Level");
                typeof(SonicXFrontMenu).GetField("pendingNewLevel",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(menu,third);
                menu.Show(SonicXFrontMenu.Page.NewLevelCharacters);
                var catalog=SonicNewLevelCatalog.Load();
                Check(catalog!=null&&menu.GetComponentsInChildren<UnityEngine.UI.Button>().Length==catalog.characters.Count(entry=>entry!=null&&entry.prefab!=null)+1,"Act 2-1 offers the full character selection");
                go.SetActive(false);
                var progress=go.AddComponent<LevelProgressControl>();character.SetValue(null,"sonic");
                Check(progress.ConfiguredNextLevelScene(first)==second,"Goal configuration selects Act 1-2");
                // An explicit route ending cannot load an unrelated legacy build index.
                progress.LevelToGoNext=1;
                Check(progress.ConfiguredNextLevelScene(second)==third,"Goal configuration selects Act 2-1");
                Check(string.IsNullOrEmpty(progress.ConfiguredNextLevelScene(third)),"Final act does not fall back to unrelated stage");
                progress.NextLevelScene=first;
                Check(progress.ConfiguredNextLevelScene(second)==first,"Explicit per-level override remains available");
                var result=SonicLevelScore.Calculate(500,20,0,third);
                Check(result.nextScene==third,"Results receive Act 2-1 as the next act");
                File.WriteAllText(Path.Combine(Folder,"unity-report.txt"),"PASS\nStory: Act 1-1 -> Act 1-2 -> Act 2-1 for young and Classic Sonic. All three acts enabled without reordering existing build scenes. Goal resolver and results select Act 2-1; route ends there safely. New Level lists Act 2-1 with the full character selection. User scenes and adventure save untouched.\n"+DateTime.Now.ToString("s"));
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
                Check(Application.CanStreamedLevelBeLoaded(SonicXProgress.FirstLevel)&&Application.CanStreamedLevelBeLoaded(SonicStoryRoute.SecondLevel)&&Application.CanStreamedLevelBeLoaded(SonicStoryRoute.MarbleFirstLevel),"All three story acts loadable in Play");
                progress.NextLevelScene="";progress.LevelToGoNext=1;
                Check(progress.ResolveNextLevelScene(SonicXProgress.FirstLevel)==SonicStoryRoute.SecondLevel,"Live goal resolver leads Act 1-1 to Act 1-2");
                Check(progress.ResolveNextLevelScene(SonicStoryRoute.SecondLevel)==SonicStoryRoute.MarbleFirstLevel,"Live goal resolver leads Act 1-2 to Act 2-1");
                Check(progress.ResolveNextLevelScene(SonicStoryRoute.MarbleFirstLevel)==null,"Current final act safely returns to menu");
                Directory.CreateDirectory(Folder);
                File.WriteAllText(report,"PASS\nPlay mode: all three story acts loadable; live goal resolver leads Act 1-1 to Act 1-2 to Act 2-1. Final act has no unrelated destination.\n"+DateTime.Now.ToString("s"));
            }
            catch(Exception e){Directory.CreateDirectory(Folder);File.WriteAllText(report,"FAIL\n"+e);Debug.LogException(e);}
            finally{progress.NextLevelScene=oldOverride;progress.LevelToGoNext=oldIndex;}
        }
    }
}
