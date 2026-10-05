using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SonicFX.Menu.Editor
{
    [InitializeOnLoad] public static class SonicNewLevelRuntimeVerification
    {
        static bool started;
        static string Request=>Path.Combine(SonicNewLevelInstaller.Reports,"runtime-request.txt");
        static SonicNewLevelRuntimeVerification(){EditorApplication.update+=Ready;EditorApplication.playModeStateChanged+=Changed;}
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredEditMode || !SessionState.GetBool("SonicFX.NewLevelProbe",false))return;
            string previous=SessionState.GetString("SonicFX.NewLevelPreviousStart","");
            EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            SessionState.SetBool("SonicFX.NewLevelProbe",false);
        }
        static void Ready()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||started||!File.Exists(Request))return;
            if(!EditorApplication.isPlayingOrWillChangePlaymode){
                SessionState.SetString("SonicFX.NewLevelPreviousStart",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
                SessionState.SetBool("SonicFX.NewLevelProbe",true);
                EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/BumperEngineV1/Scenes/LogoScreen.unity");
                EditorApplication.isPlaying=true;return;
            }
            if(!EditorApplication.isPlaying)return;
            started=true;File.Delete(Request);
            var probe=new GameObject("Verification temporaire New Level");UnityEngine.Object.DontDestroyOnLoad(probe);probe.AddComponent<SonicNewLevelProbe>();
        }
    }
    public sealed class SonicNewLevelProbe:MonoBehaviour
    {
        string failure;string originalSave;bool hadSave;
        readonly System.Collections.Generic.List<string> errors=new System.Collections.Generic.List<string>();
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        void Log(string message,string trace,LogType type){if(type==LogType.Exception && errors.Count<8)errors.Add(message+"\n"+trace);}
        IEnumerator Start()
        {
            originalSave=PlayerPrefs.GetString("SonicFX.Story.Save.v1","");hadSave=PlayerPrefs.HasKey("SonicFX.Story.Save.v1");Application.logMessageReceived+=Log;
            var run=Run();
            while(true){bool more=false;object current=null;try{more=run.MoveNext();if(more)current=run.Current;}catch(Exception e){failure=e.ToString();}if(failure!=null||!more)break;yield return current;}
            Application.logMessageReceived-=Log;
            if(PlayerPrefs.GetString("SonicFX.Story.Save.v1","")!=originalSave || PlayerPrefs.HasKey("SonicFX.Story.Save.v1")!=hadSave)failure="La sauvegarde Histoire a change";
            if(errors.Count>0)failure=(failure??"")+"\nExceptions en Play :\n"+string.Join("\n",errors);
            File.WriteAllText(Path.Combine(SonicNewLevelInstaller.Reports,"runtime-verification.txt"),(failure==null?"PASS\nMenus et chargements reels : Free Act 1-1, Amy Act 1-2, Shadow BoundArounds. Capacites, camera, HUD unique et musique du menu verifies. Sauvegarde Histoire intacte.\n":"FAIL\n"+failure+"\n")+DateTime.Now.ToString("s"));
            EditorApplication.isPlaying=false;
        }
        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1);
            string[] levels={"Act 1-1","Act 1-2","BoundArounds"},characters={"SonicManiaFree","Amy","Shadow"};
            for(int i=0;i<levels.Length;i++){
                if(i>0){yield return SceneManager.LoadSceneAsync("LogoScreen");yield return null;}
                var menu=UnityEngine.Object.FindAnyObjectByType<SonicXFrontMenu>();Check(menu!=null,"Menu charge");
                Check(menu.music!=null && menu.music.clip==AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath("18eda1bd1eb41cb408006c9c0c6b4b70")),"Musique du menu conservee");
                menu.Show(SonicXFrontMenu.Page.NewLevels);yield return null;
                menu.GetComponentsInChildren<Button>().Single(b=>b.name==levels[i]).onClick.Invoke();
                Check(menu.CurrentPage==SonicXFrontMenu.Page.NewLevelCharacters,"Selection reelle : "+levels[i]);yield return null;
                menu.GetComponentsInChildren<Button>().Single(b=>b.name==characters[i]).onClick.Invoke();
                float deadline=Time.realtimeSinceStartup+30;
                while(SceneManager.GetActiveScene().name=="LogoScreen" && Time.realtimeSinceStartup<deadline)yield return null;
                Check(SceneManager.GetActiveScene().path.StartsWith("Assets/Level/"),"Niveau reel charge");
                yield return new WaitForSecondsRealtime(1);
                var players=UnityEngine.Object.FindObjectsByType<PlayerBhysics>().Where(p=>p.gameObject.activeInHierarchy).ToArray();Check(players.Length==1,"Un seul joueur actif");var player=players[0];
                Check(player.transform.root.name==characters[i],"Personnage choisi effectivement instancie");
                Check(player.GetComponent<CameraControl>().Cam.Player==player,"Camera reliee au joueur choisi");
                if(i==0)Check(player.TopSpeed==65 && player.MaxSpeed==350 && player.GetComponent<ActionManager>().AvailableAbilities==SonicAbility.All,"Free en Play : vitesse et capacites");
                var hud=player.GetComponent<SonicFX.HUD.SonicGameplayHud>();Check(hud!=null && hud.HudCanvas!=null,"HUD en Play");
                Check(hud.HudCanvas.GetComponentsInChildren<Transform>().Count(t=>t.name=="Compteur de vitesse")==1,"Un compteur de vitesse");
                Check(!SonicXProgress.IsStorySession && SonicNewLevelSession.IsActive,"Session libre independante");
            }
        }
    }
}
