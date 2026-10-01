using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using SonicFX.Menu;

namespace SonicFX.HUD.Editor
{
    [InitializeOnLoad] public static class SonicHudInstaller
    {
        const string Root="Assets/Menu/GameplayHUD";
        public static readonly string Reports=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/SonicGameplayHUD");
        static SonicHudInstaller(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)return;
            EditorApplication.update-=Ready;if(!File.Exists(Root+"/Editor/HudInstalled.txt"))Install();
        }
        [MenuItem("Tools/Sonic FX/HUD/Installer et verifier le HUD")]
        public static void Install()
        {
            Directory.CreateDirectory(Reports);
            try
            {
                string path=Root+"/Textures/Sonic-Life.png";var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
                if(!AssetDatabase.IsValidFolder(Root+"/Resources"))AssetDatabase.CreateFolder(Root,"Resources");
                var settings=AssetDatabase.LoadAssetAtPath<SonicHudSettings>(Root+"/Resources/SonicHudSettings.asset");
                if(settings==null){settings=ScriptableObject.CreateInstance<SonicHudSettings>();AssetDatabase.CreateAsset(settings,Root+"/Resources/SonicHudSettings.asset");}
                settings.sonicIcon=AssetDatabase.LoadAssetAtPath<Sprite>(path);settings.ringIcon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prototyping/Proto_UI/Ring_SatSR.png");settings.numberFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/BumperEngineV1/Fonts/kimberley bl.ttf");
                if(settings.sonicIcon==null || settings.ringIcon==null || settings.numberFont==null)throw new Exception("Une ressource du HUD manque.");
                EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();Verify(settings);
                File.WriteAllText(Root+"/Editor/HudInstalled.txt","HUD quatre lignes installe, vies et Game Over actifs.");AssetDatabase.ImportAsset(Root+"/Editor/HudInstalled.txt");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Reports,"unity-tests.txt"),"FAIL\n"+e);Debug.LogException(e);}
        }
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        static void Verify(SonicHudSettings settings)
        {
            GameObject go=null;
            var livesField=typeof(SonicXProgress).GetField("<Lives>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic);
            var sessionField=typeof(SonicXProgress).GetField("storySession",BindingFlags.Static|BindingFlags.NonPublic);
            int oldLives=SonicXProgress.Lives;bool oldSession=(bool)sessionField.GetValue(null);
            const string key="SonicFX.Story.Save.v1";bool hadSave=PlayerPrefs.HasKey(key);string saved=PlayerPrefs.GetString(key,"");
            string testKey="SonicFX.HudTest."+Guid.NewGuid().ToString("N");
            try
            {
                Check(SonicXProgress.TryParse("{\"version\":1,\"character\":\"sonic\",\"scene\":\"test\"}",out var legacy) && legacy.lives==3,"Old saves migrate to three lives");
                Check(SonicXProgress.TryParse(JsonUtility.ToJson(new StorySave{scene="test",lives=1}),out var resumed) && resumed.lives==1,"Save roundtrip preserves last life");
                Check(!SonicXProgress.TryParse(JsonUtility.ToJson(new StorySave{scene="test",lives=0}),out _),"Exhausted saves cannot continue");
                Check(SonicGameplayHud.FormatTime(0)=="00'00\"000" && SonicGameplayHud.FormatTime(61.25f)=="01'01\"250","Timer format including milliseconds");
                // Direct-level/practice session: exercise the actual death gate without touching the user's save.
                sessionField.SetValue(null,false);livesField.SetValue(null,3);
                go=new GameObject("HUD verification"){hideFlags=HideFlags.HideAndDontSave};go.SetActive(false);
                var hurt=go.AddComponent<HurtControl>();var count=typeof(HurtControl).GetMethod("RegisterDeathOnce",BindingFlags.Instance|BindingFlags.NonPublic);var gate=typeof(HurtControl).GetField("deathCounted",BindingFlags.Instance|BindingFlags.NonPublic);
                count.Invoke(hurt,null);count.Invoke(hurt,null);Check(SonicXProgress.Lives==2,"One life per death, not per frame");
                gate.SetValue(hurt,false);count.Invoke(hurt,null);Check(SonicXProgress.Lives==1,"Second death leaves one life");
                gate.SetValue(hurt,false);count.Invoke(hurt,null);Check(SonicXProgress.Lives==0 && SonicXProgress.IsGameOver,"Third death is game over");count.Invoke(hurt,null);Check(SonicXProgress.Lives==0,"Lives never negative");
                Check(PlayerPrefs.HasKey(key)==hadSave && PlayerPrefs.GetString(key,"")==saved,"Practice tests preserve the player's adventure");
                var persist=typeof(SonicXProgress).GetMethod("PersistRemainingLives",BindingFlags.Static|BindingFlags.NonPublic);
                PlayerPrefs.SetString(testKey,JsonUtility.ToJson(new StorySave{scene="test",lives=3}));livesField.SetValue(null,2);persist.Invoke(null,new object[]{testKey});
                Check(SonicXProgress.TryParse(PlayerPrefs.GetString(testKey),out var decreased) && decreased.lives==2,"Death persists remaining lives");
                livesField.SetValue(null,0);persist.Invoke(null,new object[]{testKey});Check(!PlayerPrefs.HasKey(testKey),"Game over deletes the exhausted save slot");
                var hud=go.AddComponent<SonicGameplayHud>();hud.settings=settings;hud.Build();
                Check(hud.ScoreText!=null && hud.TimeText!=null && hud.RingsText!=null && hud.LivesText!=null,"All four HUD rows exist");
                Check(hud.HudCanvas.renderMode==RenderMode.ScreenSpaceOverlay,"HUD uses screen space");
                File.WriteAllText(Path.Combine(Reports,"unity-tests.txt"),"PASS\nOld saves migrate to three lives. Resume preserves remaining lives. Zero-life saves rejected. Death counted once; 3 -> 2 -> 1 -> 0. Remaining lives persisted and exhausted slot deleted using an isolated test key. Practice tests leave the existing save untouched. Four HUD rows, icons/font and timer format validated.\n");
            }
            finally{PlayerPrefs.DeleteKey(testKey);PlayerPrefs.Save();if(go!=null)UnityEngine.Object.DestroyImmediate(go);livesField.SetValue(null,oldLives);sessionField.SetValue(null,oldSession);}
        }
    }
}
