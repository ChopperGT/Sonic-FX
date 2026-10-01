using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace SonicFX.Menu.Editor
{
    [InitializeOnLoad]
    public static class SonicXFrontMenuBuilder
    {
        const string Title="Assets/BumperEngineV1/Scenes/LogoScreen.unity";
        const string Marker="Assets/Menu/Frontend/Editor/MenuInstalled.txt";
        public static readonly string Reports=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/SonicX_Frontend");
        static SonicXFrontMenuBuilder(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)return;
            if(File.Exists(Marker)){EditorApplication.update-=Ready;return;}
            var s=SceneManager.GetSceneByPath(Title);if(s.IsValid() && s.isDirty)return;
            EditorApplication.update-=Ready;Build();
        }
        [MenuItem("Tools/Sonic FX/Menu/Installer les menus Histoire")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            Directory.CreateDirectory(Reports);var scene=SceneManager.GetSceneByPath(Title);bool opened=!scene.IsValid() || !scene.isLoaded;
            if(!opened && scene.isDirty){Debug.LogWarning("Enregistre LogoScreen avant d'installer les menus.");return;}
            try
            {
                var builds=EditorBuildSettings.scenes.ToList();int index=builds.FindIndex(s=>s.path==SonicXProgress.FirstLevel);
                if(!File.Exists(SonicXProgress.FirstLevel))throw new FileNotFoundException("Act 1-1 introuvable.");
                if(index<0)builds.Add(new EditorBuildSettingsScene(SonicXProgress.FirstLevel,true));else builds[index].enabled=true;
                EditorBuildSettings.scenes=builds.ToArray();
                if(opened)scene=EditorSceneManager.OpenScene(Title,OpenSceneMode.Additive);
                var roots=scene.GetRootGameObjects();var old=roots.SelectMany(g=>g.GetComponentsInChildren<TitleScreenControl>(true)).Single();
                old.enabled=false;old.GetComponent<Canvas>().enabled=false;
                var menu=roots.SelectMany(g=>g.GetComponentsInChildren<SonicXFrontMenu>(true)).FirstOrDefault();
                if(menu==null){var go=new GameObject("SonicX - Menu principal");SceneManager.MoveGameObjectToScene(go,scene);menu=go.AddComponent<SonicXFrontMenu>();}
                menu.logo=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Menu/image/logo SonicX.png");
                if(menu.logo==null)throw new InvalidOperationException("Logo SonicX manquant.");
                menu.music=roots.Single(g=>g.name=="[MenuMusic]").GetComponent<AudioSource>();menu.confirmSound=old.Source;
                var sound=old.GetComponent<LoadSound>();if(sound!=null){menu.musicMixer=sound.Music;menu.sfxMixer=sound.Sfx;}
                EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene non enregistree.");
                VerifyAndRender(menu);
                File.WriteAllText(Marker,"Menu Histoire installe ; progression locale par niveau.\n");AssetDatabase.ImportAsset(Marker);
                File.WriteAllText(Path.Combine(Reports,"unity-report.txt"),"PASS: saved scene, original title transition disabled, Act 1-1 enabled without reordering other levels, save parser validated, menu visibility/navigation verified, four Unity previews rendered.\n");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Reports,"unity-report.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally{if(opened && scene.IsValid() && scene.isLoaded)EditorSceneManager.CloseScene(scene,true);}
        }
        static void Require(bool value,string error){if(!value)throw new InvalidOperationException(error);}
        static void VerifyAndRender(SonicXFrontMenu source)
        {
            Require(!SonicXProgress.TryParse("",out _),"Empty save accepted");Require(!SonicXProgress.TryParse("{broken",out _),"Broken save accepted");
            Require(!SonicXProgress.TryParse("{\"version\":99,\"character\":\"sonic\",\"scene\":\"x\"}",out _),"Unknown save accepted");
            Require(SonicXProgress.TryParse(JsonUtility.ToJson(new StorySave{scene=SonicXProgress.FirstLevel}),out var save) && save.character=="sonic","Save roundtrip failed");
            var preview=new PreviewRenderUtility();Texture2D image=null;
            try
            {
                var stage=new GameObject("Menu preview");preview.AddSingleGO(stage);var menu=stage.AddComponent<SonicXFrontMenu>();menu.logo=source.logo;menu.Build();
                var canvas=menu.MenuCanvas;canvas.renderMode=RenderMode.WorldSpace;canvas.GetComponent<CanvasScaler>().enabled=false;
                var rt=(RectTransform)canvas.transform;rt.sizeDelta=new Vector2(1270,720);rt.localScale=Vector3.one*.01f;rt.localPosition=Vector3.zero;
                preview.camera.orthographic=true;preview.camera.orthographicSize=3.6f;preview.camera.transform.position=new Vector3(0,0,-10);preview.camera.transform.rotation=Quaternion.identity;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=50;preview.camera.clearFlags=CameraClearFlags.Color;preview.camera.backgroundColor=Color.black;
                foreach(var page in new[]{SonicXFrontMenu.Page.Main,SonicXFrontMenu.Page.Story,SonicXFrontMenu.Page.Characters,SonicXFrontMenu.Page.Settings})
                {
                    menu.Show(page);Canvas.ForceUpdateCanvases();var names=menu.GetComponentsInChildren<Button>().Select(b=>b.name).ToArray();
                    if(page==SonicXFrontMenu.Page.Main)Require(names.Contains("Mode Arcade")==SonicXProgress.IsUnlocked("arcade"),"Arcade visibility");
                    if(page==SonicXFrontMenu.Page.Story)Require(names.Contains("Continuer")==SonicXProgress.CanContinue,"Continue visibility");
                    if(page==SonicXFrontMenu.Page.Characters){Require(names.Contains("Sonic (jeune)"),"Sonic missing");foreach(var pair in new[]{new[]{"tails","Tails"},new[]{"amy","Amy"},new[]{"shadow","Shadow"}})Require(names.Contains(pair[1])==SonicXProgress.IsUnlocked(pair[0]),"Character lock "+pair[0]);}
                    foreach(var button in menu.GetComponentsInChildren<Button>())Require(button.navigation.selectOnDown!=null && button.navigation.selectOnUp!=null,"Broken navigation");
                    preview.BeginStaticPreview(new Rect(0,0,1270,720));preview.camera.Render();image=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(Reports,page+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);image=null;
                }
                menu.Show(SonicXFrontMenu.Page.Main);menu.GetComponentsInChildren<Button>().Single(b=>b.name=="Mode Histoire").onClick.Invoke();Require(menu.CurrentPage==SonicXFrontMenu.Page.Story,"Story navigation failed");
                menu.GetComponentsInChildren<Button>().Single(b=>b.name=="Nouvelle partie").onClick.Invoke();Require(menu.CurrentPage==SonicXFrontMenu.Page.Characters,"Character navigation failed");
            }
            finally{if(image!=null)UnityEngine.Object.DestroyImmediate(image);preview.Cleanup();}
        }
    }
}
