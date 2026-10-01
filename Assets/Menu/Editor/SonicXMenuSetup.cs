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
    public static class SonicXMenuSetup
    {
        const string ScenePath="Assets/BumperEngineV1/Scenes/LogoScreen.unity";
        const string LogoPath="Assets/Menu/image/logo SonicX.png";
        const string Marker="Assets/Menu/Editor/SonicXLogoInstalled.txt";
        static readonly string Reports=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/SonicX_Menu");
        static SonicXMenuSetup(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)return;
            if(File.Exists(Marker)){EditorApplication.update-=Ready;return;}
            var existing=SceneManager.GetSceneByPath(ScenePath);
            if(existing.IsValid() && existing.isDirty)return;
            EditorApplication.update-=Ready;Apply();
        }
        [MenuItem("Tools/Sonic FX/Menu/Installer le logo SonicX")]
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            Directory.CreateDirectory(Reports);
            var scene=SceneManager.GetSceneByPath(ScenePath);bool opened=!scene.IsValid() || !scene.isLoaded;
            if(!opened && scene.isDirty){Debug.LogWarning("Enregistrer LogoScreen avant d'installer le logo SonicX.");return;}
            try
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(LogoPath);
                if(importer==null)throw new InvalidOperationException("Logo SonicX introuvable.");
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;
                importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
                if(sprite==null)throw new InvalidOperationException("Import du sprite impossible.");
                if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
                var logo=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Image>(true)).Single(i=>i.gameObject.name=="Image" && i.transform.parent.name=="BGUI");
                logo.sprite=sprite;logo.preserveAspect=true;logo.raycastTarget=false;
                logo.rectTransform.sizeDelta=new Vector2(980,980f*sprite.rect.height/sprite.rect.width);
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new IOException("Enregistrement du menu impossible.");
                if(logo.sprite!=sprite || !logo.preserveAspect)throw new InvalidOperationException("Verification du logo echouee.");
                File.WriteAllText(Marker,"Logo SonicX installe. Musique conservee : choisir un AudioClip sur [MenuMusic].");
                AssetDatabase.ImportAsset(Marker);
                File.WriteAllText(Path.Combine(Reports,"unity-report.txt"),"PASS: SonicX sprite assigned to BGUI/Image, aspect preserved, title scene saved. Music unchanged.\n");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Reports,"unity-report.txt"),"FAIL\n"+e);Debug.LogException(e);return;}
            finally{if(opened && scene.IsValid() && scene.isLoaded)EditorSceneManager.CloseScene(scene,true);}
            RenderPreview();
        }
        public static void RenderPreview()
        {
            var scene=EditorSceneManager.OpenPreviewScene(ScenePath);RenderTexture rt=null;Texture2D image=null;var previous=RenderTexture.active;
            try
            {
                var roots=scene.GetRootGameObjects();var camera=roots.SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).First();
                foreach(var canvas in roots.SelectMany(g=>g.GetComponentsInChildren<Canvas>(true))){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}
                foreach(var fade in roots.SelectMany(g=>g.GetComponentsInChildren<Image>(true)).Where(i=>i.name=="Fade"))fade.enabled=false;
                rt=new RenderTexture(1270,720,24);camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
                image=new Texture2D(1270,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1270,720),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(Reports,"Menu_Unity.png"),image.EncodeToPNG());
            }
            catch(Exception e){Debug.LogException(e);File.AppendAllText(Path.Combine(Reports,"unity-report.txt"),"Preview failed: "+e);}
            finally{RenderTexture.active=previous;if(image!=null)UnityEngine.Object.DestroyImmediate(image);if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
