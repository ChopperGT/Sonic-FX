using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SonicFX.Score;
using SonicFX.HUD;

namespace SonicFX.Menu.Editor
{
    // Explicit opt-in test request. Preserve the user's open, unsaved scene and preferences.
    [InitializeOnLoad] public static class SonicModeVerification
    {
        public static string Reports=>"C:/Users/Lecle/Documents/Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/StoryModes";
        static readonly string[] Keys={"SonicFX.Story.Save.v1","SonicFX.TimeRecords.v1","SonicBestTimes_"+SonicXProgress.FirstLevel};
        const string ProbeKey="SonicFX.ModeProbe";
        static bool started;
        static SonicModeVerification(){EditorApplication.update+=Ready;EditorApplication.playModeStateChanged+=Changed;}
        static void Backup()
        {
            foreach(var key in Keys){SessionState.SetBool(ProbeKey+key+".exists",PlayerPrefs.HasKey(key));SessionState.SetString(ProbeKey+key,PlayerPrefs.GetString(key,""));}
            SessionState.SetBool(ProbeKey+"ghost.exists",PlayerPrefs.HasKey("SonicGhostEnabled"));SessionState.SetInt(ProbeKey+"ghost",PlayerPrefs.GetInt("SonicGhostEnabled",1));
            SessionState.SetBool(ProbeKey,true);
        }
        public static void Restore()
        {
            if(!SessionState.GetBool(ProbeKey,false))return;
            foreach(var key in Keys){if(SessionState.GetBool(ProbeKey+key+".exists",false))PlayerPrefs.SetString(key,SessionState.GetString(ProbeKey+key,""));else PlayerPrefs.DeleteKey(key);}
            if(SessionState.GetBool(ProbeKey+"ghost.exists",false))PlayerPrefs.SetInt("SonicGhostEnabled",SessionState.GetInt(ProbeKey+"ghost",1));else PlayerPrefs.DeleteKey("SonicGhostEnabled");
            PlayerPrefs.Save();
        }
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(ProbeKey,false))return;
            Restore();string previous=SessionState.GetString(ProbeKey+"start","");
            EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            SessionState.SetBool(ProbeKey,false);
        }
        static void Ready()
        {
            string request=Path.Combine(Reports,"request.txt");
            if(started || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(request))return;
            if(!EditorApplication.isPlayingOrWillChangePlaymode){
                VerifyPrefabs();Backup();SessionState.SetString(ProbeKey+"start",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
                EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/BumperEngineV1/Scenes/LogoScreen.unity");
                EditorApplication.isPlaying=true;return;
            }
            if(!EditorApplication.isPlaying)return;
            started=true;File.Delete(request);
            var gameView=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");if(gameView!=null)EditorWindow.GetWindow(gameView).Show();
            var probe=new GameObject("Verification temporaire modes");UnityEngine.Object.DontDestroyOnLoad(probe);probe.AddComponent<SonicModeProbe>();
        }
        static void VerifyPrefabs()
        {
            float[] tops={55,65,65,45,65,85,65,35,55,55,40};
            var catalog=SonicNewLevelCatalog.Load();
            for(int i=0;i<catalog.characters.Length;i++){
                var entry=catalog.characters[i];var player=entry.prefab.GetComponentInChildren<PlayerBhysics>(true);
                int abilities=new SerializedObject(player.GetComponent<ActionManager>()).FindProperty("startingAbilities").intValue;
                SonicModeProbe.Check(abilities==(entry.id=="sonic"?0:127),"Capacites du prefab "+entry.id);
                SonicModeProbe.Check(player.TopSpeed==(entry.id=="sonic"?40:tops[i]) && player.MaxSpeed==(entry.id=="sonic"?80:entry.id=="sonicmaniafree"?350:343),"Vitesses du prefab "+entry.id);
            }
            File.WriteAllText(Path.Combine(Reports,"prefabs.txt"),"PASS : 11 prefabs, capacites du pack, vitesses d'origine, exception Mania Histoire 40/80 et Free 65/350.\n"+DateTime.Now.ToString("s"));
        }
    }

    public sealed class SonicModeProbe:MonoBehaviour
    {
        string failure;
        readonly System.Collections.Generic.List<string> errors=new System.Collections.Generic.List<string>();
        public static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        void Log(string message,string trace,LogType type){if(type==LogType.Exception && errors.Count<8)errors.Add(message+"\n"+trace);}
        IEnumerator Start()
        {
            Application.logMessageReceived+=Log;var run=Run();
            while(true){bool more=false;object current=null;try{more=run.MoveNext();if(more)current=run.Current;}catch(Exception e){failure=e.ToString();}if(failure!=null || !more)break;yield return current;}
            Application.logMessageReceived-=Log;SonicModeVerification.Restore();
            if(errors.Count>0)failure=(failure??"")+"\nExceptions :\n"+string.Join("\n",errors);
            File.WriteAllText(Path.Combine(SonicModeVerification.Reports,"runtime.txt"),(failure==null?"PASS\nHistoire : fantome force OFF, aucune option/carte/records, bilan cinq lignes, calcul et sauvegarde, fin reelle Act 1-1 puis bouton Act 1-2. New Level : Sonic jeune 55/343, toutes capacites, records et options fantome presents, bilan temps conserve. Free 65/350, Moderne 65/343, Amy 45/343 en Play. Preferences et sauvegarde d'origine restaurees.\n":"FAIL\n"+failure+"\n")+DateTime.Now.ToString("s"));
            EditorApplication.isPlaying=false;
        }
        static PlayerBhysics Player()=>UnityEngine.Object.FindObjectsByType<PlayerBhysics>().Single(p=>p.gameObject.activeInHierarchy);
        static void Freeze(PlayerBhysics player){player.enabled=false;var body=player.GetComponent<Rigidbody>();body.linearVelocity=Vector3.zero;body.isKinematic=true;player.GetComponent<HurtControl>().enabled=false;}
        static bool Named(Transform parent,string name)=>parent.GetComponentsInChildren<Transform>(true).Any(t=>t.name==name);
        static string[] Labels(Transform parent)=>parent.GetComponentsInChildren<Text>(true).Select(t=>t.text).ToArray();
        void Capture(string name)=>ScreenCapture.CaptureScreenshot(Path.Combine(SonicModeVerification.Reports,name+".png"));
        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1);
            PlayerPrefs.SetString("SonicBestTimes_"+SonicXProgress.FirstLevel,"120;150;180");PlayerPrefs.SetInt("SonicGhostEnabled",1);
            yield return SonicXProgress.Begin("sonic",SonicXProgress.FirstLevel,3,49000);yield return new WaitForSecondsRealtime(1);
            var player=Player();Freeze(player);
            Check(SonicXProgress.IsStorySession && !SonicNewLevelSession.IsActive && !SonicGhost.Enabled,"Fantome OFF en Histoire meme avec preference ON");
            Check(player.TopSpeed==40 && player.MaxSpeed==80 && player.GetComponent<ActionManager>().AvailableAbilities==SonicAbility.None,"Mania Histoire conserve");
            Check(player.GetComponent<SonicGhost>()==null,"Aucun enregistreur fantome en Histoire");
            var hud=player.GetComponent<SonicGameplayHud>();Check(!Named(hud.HudCanvas.transform,"Meilleurs temps"),"Records absents en Histoire meme avec anciens records");
            var pause=player.transform.root.GetComponentInChildren<PauseCotrol>(true);pause.PauseToggle();yield return null;
            Check(Named(hud.HudCanvas.transform,"Menu pause"),"Pause construite");
            Check(!Named(hud.HudCanvas.transform,"Ghost") && !Named(hud.HudCanvas.transform,"Fiche fantome"),"Option et cadre fantome absents en Histoire");
            Capture("pause-histoire");yield return new WaitForEndOfFrame();pause.Resume();yield return null;
            Check(!Named(hud.HudCanvas.transform,"Meilleurs temps"),"Records toujours absents apres pause");
            var progress=player.GetComponent<LevelProgressControl>();Check(progress.ResolveNextLevelScene(SonicXProgress.FirstLevel)==SonicNewLevelSession.SecondAct,"Route Act 1-1 vers Act 1-2");
            SonicLevelScore.AddPoints(500);Objects_Interaction.RingAmount=73;
            var goal=UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include).First(c=>c.CompareTag("GoalRing"));
            progress.OnTriggerEnter(goal);progress.OnTriggerEnter(goal);
            float deadline=Time.realtimeSinceStartup+30;
            while(SceneManager.GetActiveScene().name!="StageCompleteScreen" && Time.realtimeSinceStartup<deadline)yield return null;
            Check(SceneManager.GetActiveScene().name=="StageCompleteScreen","Le vrai GoalRing ouvre le bilan");yield return new WaitForSecondsRealtime(.8f);
            var result=SonicLevelScore.LastResult;var results=UnityEngine.Object.FindAnyObjectByType<SonicLevelResults>();
            Check(results!=null && !result.isNewLevel && !result.newRecord,"Bilan Histoire sans record New Level");
            var labels=Labels(results.ResultsCanvas.transform);
            foreach(string label in new[]{"Score final du niveau","Rings : 73 × 10","Bonus sans mort","Score total de la sauvegarde"})Check(labels.Contains(label),"Ligne du bilan : "+label);
            Check(labels.Any(t=>t.StartsWith("Bonus temps :")),"Ligne bonus temps");
            Check(result.levelScore==500 && result.ringBonus==730 && result.noDeathBonus==1000 && result.totalScore==61230 && result.bonusLives==1 && SonicXProgress.Lives==4,"Calcul complet et vie des 50k");
            Check(!labels.Contains("TES MEILLEURS TEMPS"),"Aucun record sur le bilan Histoire");
            Capture("bilan-histoire");yield return new WaitForEndOfFrame();
            results.ResultsCanvas.GetComponentsInChildren<Button>().Single(b=>b.name=="Niveau suivant").onClick.Invoke();
            deadline=Time.realtimeSinceStartup+30;while(SceneManager.GetActiveScene().path!=SonicNewLevelSession.SecondAct && Time.realtimeSinceStartup<deadline)yield return null;
            Check(SceneManager.GetActiveScene().path==SonicNewLevelSession.SecondAct,"Bouton charge reellement Act 1-2");yield return new WaitForSecondsRealtime(.5f);Freeze(Player());
            Check(SonicXProgress.TryRead(out var save) && save.scene==SonicNewLevelSession.SecondAct && save.totalScore==61230 && save.lives==4,"Sauvegarde au niveau suivant");
            string storySave=PlayerPrefs.GetString("SonicFX.Story.Save.v1");

            var catalog=SonicNewLevelCatalog.Load();
            yield return SonicNewLevelSession.Begin(catalog.Find("sonic"),SonicXProgress.FirstLevel);yield return new WaitForSecondsRealtime(1);player=Player();Freeze(player);
            Check(player.TopSpeed==55 && player.MaxSpeed==343 && player.GetComponent<ActionManager>().AvailableAbilities==SonicAbility.All,"Sonic jeune New Level : pack restaure");
            Check(SonicGhost.Allowed && Named(player.GetComponent<SonicGameplayHud>().HudCanvas.transform,"Meilleurs temps"),"Records disponibles en New Level");
            var ghost=player.GetComponent<SonicGhost>();Check(ghost!=null && ghost.enabled,"Fantome actif seulement en New Level");
            // A read-only stand-in verifies the pause card even on a machine with no ghost recording.
            SonicGhost.Enabled=false;var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            if(!ghost.HasGhost){
                var visual=new GameObject("Fantome verification");typeof(SonicGhost).GetField("ghost",flags).SetValue(ghost,visual);
                var bestField=typeof(SonicGhost).GetField("best",flags);var best=(IList)Activator.CreateInstance(bestField.FieldType);
                var sampleType=bestField.FieldType.GetGenericArguments()[0];var sample=Activator.CreateInstance(sampleType);sampleType.GetField("t").SetValue(sample,120f);best.Add(sample);best.Add(sample);bestField.SetValue(ghost,best);
            }
            hud=player.GetComponent<SonicGameplayHud>();pause=player.transform.root.GetComponentInChildren<PauseCotrol>(true);pause.PauseToggle();yield return null;
            Check(Named(hud.HudCanvas.transform,"Ghost") && Named(hud.HudCanvas.transform,"Fiche fantome"),"Option et cadre disponibles en New Level");Capture("pause-newlevel");yield return new WaitForEndOfFrame();pause.Resume();yield return null;
            SonicLevelScore.Tick(200,false);progress=player.GetComponent<LevelProgressControl>();goal=UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include).First(c=>c.CompareTag("GoalRing"));progress.OnTriggerEnter(goal);
            deadline=Time.realtimeSinceStartup+30;while(SceneManager.GetActiveScene().name!="StageCompleteScreen" && Time.realtimeSinceStartup<deadline)yield return null;
            yield return new WaitForSecondsRealtime(.8f);results=UnityEngine.Object.FindAnyObjectByType<SonicLevelResults>();
            Check(results!=null && SonicLevelScore.LastResult.isNewLevel && Labels(results.ResultsCanvas.transform).Contains("TON TEMPS"),"Bilan temps New Level conserve");
            Check(!Labels(results.ResultsCanvas.transform).Contains("Score total de la sauvegarde"),"Bilan New Level independant");
            Capture("bilan-newlevel");yield return new WaitForEndOfFrame();
            foreach(string id in new[]{"sonicmaniafree","modernsonic","amy"}){
                yield return SonicNewLevelSession.Begin(catalog.Find(id),SonicNewLevelSession.SecondAct);yield return new WaitForSecondsRealtime(.6f);player=Player();Freeze(player);
                Check(player.GetComponent<ActionManager>().AvailableAbilities==SonicAbility.All,"Capacites reelles : "+id);
                Check(player.TopSpeed==(id=="amy"?45:65) && player.MaxSpeed==(id=="sonicmaniafree"?350:343),"Vitesses reelles : "+id);
            }
            Check(PlayerPrefs.GetString("SonicFX.Story.Save.v1")==storySave,"New Level ne modifie pas la sauvegarde Histoire");
        }
    }
}
