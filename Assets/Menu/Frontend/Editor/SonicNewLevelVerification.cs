using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace SonicFX.Menu.Editor
{
    [InitializeOnLoad] public static class SonicNewLevelVerification
    {
        static SonicNewLevelVerification(){EditorApplication.update+=Ready;}
        static void Ready(){if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;EditorApplication.update-=Ready;if(SessionState.GetBool("SonicFX.NewLevelVerification.v6",false))return;SessionState.SetBool("SonicFX.NewLevelVerification.v6",true);if(SonicNewLevelCatalog.Load()!=null)Verify();}
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        public static void Verify()
        {
            string report=Path.Combine(SonicNewLevelInstaller.Reports,"verification.txt");
            var scene=EditorSceneManager.NewPreviewScene();
            try{
                var catalog=SonicNewLevelCatalog.Load();
                Check(catalog!=null && catalog.characters.Length==11,"Catalogue complet du pack");
                Check(catalog.characters.Select(c=>c.id).Distinct().Count()==11,"Identifiants uniques");
                var free=catalog.Find(SonicNewLevelSession.FreeCharacterId).prefab.GetComponentInChildren<PlayerBhysics>(true);
                Check(free.TopSpeed==65 && free.MaxSpeed==350,"SonicManiaFree 65 / 350");
                Check(new SerializedObject(free.GetComponent<ActionManager>()).FindProperty("startingAbilities").intValue==(int)SonicAbility.All,"Toutes les capacites du personnage Free");
                var original=catalog.Find("sonic").prefab.GetComponentInChildren<PlayerBhysics>(true);
                Check(original.TopSpeed!=65 && original.MaxSpeed!=350,"Mania original conserve");
                foreach(var character in catalog.characters){
                    var source=(GameObject)PrefabUtility.InstantiatePrefab(catalog.Find("sonic").prefab,scene);
                    var old=source.GetComponentInChildren<PlayerBhysics>(true);
                    old.transform.position=new Vector3(121,87,32);old.GetComponent<LevelProgressControl>().IdealTimeSeconds=83;
                    var enemyObject=new GameObject("Motobug reference verification");SceneManager.MoveGameObjectToScene(enemyObject,scene);
                    var enemy=enemyObject.AddComponent<MotobugControl>();
                    var playerField=typeof(MotobugControl).GetField("Player",BindingFlags.Instance|BindingFlags.NonPublic);
                    playerField.SetValue(enemy,old.transform.GetComponentsInChildren<Transform>().First(t=>t.name=="CharacterCapsule"));
                    var method=typeof(SonicNewLevelSession).GetMethod("InstallCharacter",BindingFlags.Static|BindingFlags.NonPublic);
                    var player=(PlayerBhysics)method.Invoke(null,new object[]{scene,character});
                    Check(player!=null && Vector3.Distance(player.transform.position,new Vector3(121,87,32))<.001f,"Position de depart : "+character.id);
                    Check(player.GetComponent<LevelProgressControl>().IdealTimeSeconds==83,"Parametres du niveau : "+character.id);
                    Check(player.GetComponent<CameraControl>().Cam!=null,"Camera du personnage : "+character.id);
                    Check(character.id=="sonic" || (Transform)playerField.GetValue(enemy)==player.transform,"Detection Motobug apres remplacement : "+character.id);
                    Check(scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PlayerBhysics>()).Count()==1,"Un seul joueur actif : "+character.id);
                    foreach(var root in scene.GetRootGameObjects())Object.DestroyImmediate(root);
                }
                var menuObject=new GameObject("Menu verification");SceneManager.MoveGameObjectToScene(menuObject,scene);
                var menu=menuObject.AddComponent<SonicXFrontMenu>();menu.logo=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Menu/image/logo SonicX.png");
                foreach(string label in new[]{"BoundArounds","Act 1-1","Act 1-2"}){
                    menu.Show(SonicXFrontMenu.Page.NewLevels);
                    Check(menu.GetComponentsInChildren<Button>().Any(b=>b.name==label),"Niveau present : "+label);
                    string path=label=="BoundArounds"?menu.speedrunCocoScene:label=="Act 1-1"?SonicXProgress.FirstLevel:SonicNewLevelSession.SecondAct;
                    Check(EditorBuildSettings.scenes.Any(s=>s.enabled && s.path==path),"Scene incluse : "+label);
                    // CanStreamedLevelBeLoaded is a Play-mode API. Preview the page without loading a user scene.
                    typeof(SonicXFrontMenu).GetField("pendingNewLevel",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(menu,path);
                    menu.Show(SonicXFrontMenu.Page.NewLevelCharacters);
                    Check(menu.GetComponentsInChildren<Button>().Length==12,"Tous les personnages accessibles : "+label);
                }
                Capture(menu.MenuCanvas,scene,Path.Combine(SonicNewLevelInstaller.Reports,"characters.png"));
                menu.Show(SonicXFrontMenu.Page.Characters);
                Check(!menu.GetComponentsInChildren<Button>().Any(b=>b.name=="SonicManiaFree"),"Free absent de l'Histoire");
                SonicFX.HUD.Editor.SonicSpeedHudVerification.Verify();
                File.WriteAllText(report,"PASS\n"+DateTime.Now.ToString("s")+"\n11 personnages : remplacement, position de depart, camera et configuration du niveau. Trois scenes incluses, selection complete ; Free exclu du menu Histoire. HUD verifie separement.\n");
            }catch(Exception e){File.WriteAllText(report,"FAIL\n"+e);Debug.LogException(e);}
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        internal static void Capture(Canvas canvas,Scene scene,string path)
        {
            var cameraObject=new GameObject("Preview camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraObject,scene);
            var camera=cameraObject.GetComponent<Camera>();camera.scene=scene;camera.orthographic=true;camera.orthographicSize=360;camera.nearClipPlane=.01f;camera.farClipPlane=100;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.04f,.09f,.13f);
            var render=new RenderTexture(1270,720,24);var previous=RenderTexture.active;Texture2D image=null;
            try{
                camera.targetTexture=render;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=render;
                image=new Texture2D(1270,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1270,720),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
            }finally{RenderTexture.active=previous;camera.targetTexture=null;canvas.worldCamera=null;if(image!=null)Object.DestroyImmediate(image);Object.DestroyImmediate(cameraObject);render.Release();Object.DestroyImmediate(render);}
        }
    }
}
