using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SonicFX.Menu.Editor
{
    [InitializeOnLoad] internal static class SonicStoryRouteInstaller
    {
        internal const string Path="Assets/Menu/Frontend/Resources/SonicStoryRoute.asset";
        static bool queued;
        static SonicStoryRouteInstaller(){Queue();}
        internal static void Queue()
        {
            if(queued)return;
            queued=true;EditorApplication.update+=Ready;
        }
        static void Ready()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            EditorApplication.update-=Ready;queued=false;
            try{Install();}catch(Exception e){Debug.LogException(e);}
        }
        [MenuItem("Tools/Sonic FX/Histoire/Configurer les actes")]
        internal static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            var route=AssetDatabase.LoadAssetAtPath<SonicStoryRoute>(Path);
            if(route==null)
            {
                if(!AssetDatabase.IsValidFolder("Assets/Menu/Frontend/Resources"))AssetDatabase.CreateFolder("Assets/Menu/Frontend","Resources");
                route=ScriptableObject.CreateInstance<SonicStoryRoute>();
                route.stages=new[]{
                    new SonicStoryRoute.StageLink{character="sonic",levelScene=SonicXProgress.FirstLevel,nextScene=SonicStoryRoute.SecondLevel},
                    new SonicStoryRoute.StageLink{character="sonic",levelScene=SonicStoryRoute.SecondLevel,nextScene=SonicStoryRoute.MarbleFirstLevel},
                    new SonicStoryRoute.StageLink{character="sonic",levelScene=SonicStoryRoute.MarbleFirstLevel,nextScene=""}
                };
                AssetDatabase.CreateAsset(route,Path);AssetDatabase.SaveAssetIfDirty(route);
            }
            var builds=EditorBuildSettings.globalScenes.ToList();bool changed=false;
            var paths=new System.Collections.Generic.HashSet<string>{SonicXProgress.FirstLevel};
            foreach(var stage in route.stages??new SonicStoryRoute.StageLink[0])
                if(stage!=null){paths.Add(stage.levelScene);paths.Add(stage.nextScene);}
            foreach(string path in paths.OrderBy(value=>value,StringComparer.Ordinal))
            {
                if(string.IsNullOrEmpty(path)||!path.EndsWith(".unity",StringComparison.OrdinalIgnoreCase)||AssetDatabase.LoadAssetAtPath<SceneAsset>(path)==null)continue;
                int index=builds.FindIndex(value=>value.path==path);
                if(index<0){builds.Add(new EditorBuildSettingsScene(path,true));changed=true;}
                else if(!builds[index].enabled){builds[index].enabled=true;changed=true;}
            }
            if(changed)EditorBuildSettings.globalScenes=builds.ToArray();
        }
    }
    internal sealed class SonicStoryRouteAssetWatcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] movedFrom)
        {
            if(imported.Concat(moved).Any(path=>path==SonicStoryRouteInstaller.Path || path.StartsWith("Assets/Level/",StringComparison.Ordinal)&&path.EndsWith(".unity",StringComparison.OrdinalIgnoreCase)))
                SonicStoryRouteInstaller.Queue();
        }
    }
}
