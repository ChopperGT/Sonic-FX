using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SonicFX.Menu
{
    [Serializable] public class StorySave { public int version=3; public long totalScore; public int lives=3; public string character="sonic"; public string scene; }
    public static class SonicXProgress
    {
        public const string FirstLevel="Assets/Level/Sonic 1/Act 1 GreenHiill/act 1-1/act 1-1.unity";
        const string SaveKey="SonicFX.Story.Save.v1";
        static StorySave pending; static bool storySession;
        public static int Lives {get;private set;}=3;
        public static long TotalScore {get;private set;}
        public static bool IsGameOver=>Lives<=0;
        public static bool IsStorySession=>storySession;
        public static string Character { get; private set; }="sonic";
        public static bool IsUnlocked(string id)=>id=="sonic" || PlayerPrefs.GetInt("SonicFX.Unlock."+id,0)==1;
        public static void Unlock(string id){PlayerPrefs.SetInt("SonicFX.Unlock."+id,1);PlayerPrefs.Save();}
        public static bool TryRead(out StorySave data)=>TryParse(PlayerPrefs.GetString(SaveKey,""),out data);
        public static bool TryParse(string json,out StorySave data)
        {
            data=null;if(string.IsNullOrEmpty(json))return false;
            try{var value=JsonUtility.FromJson<StorySave>(json);if(value==null || (value.version!=1 && value.version!=2 && value.version!=3) || string.IsNullOrEmpty(value.scene) || (value.character!="sonic" && value.character!="tails" && value.character!="amy" && value.character!="shadow"))return false;if(value.version==1){value.lives=3;value.version=2;}if(value.version<3){value.totalScore=0;value.version=3;}if(value.lives<=0 || value.totalScore<0)return false;data=value;return true;}catch{return false;}
        }
        public static bool CanContinue=>TryRead(out var save) && Application.CanStreamedLevelBeLoaded(save.scene);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){pending=null;storySession=false;Lives=3;TotalScore=0;Character="sonic";SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
        public static AsyncOperation Begin(string character,string scene,int lives=3,long totalScore=0)
        {
            if(lives<=0 || !Application.CanStreamedLevelBeLoaded(scene))return null;
            Time.timeScale=1;Character=character;Lives=lives;TotalScore=Math.Max(0,totalScore);Objects_Interaction.RingAmount=0;pending=new StorySave{character=character,scene=scene,lives=lives,totalScore=TotalScore};storySession=true;
            try {var op=SceneManager.LoadSceneAsync(scene);if(op==null){pending=null;storySession=false;}return op;}
            catch{pending=null;storySession=false;throw;}
        }
        static void Loaded(Scene scene,LoadSceneMode mode)
        {
            if(mode!=LoadSceneMode.Single)return;
            if(scene.name=="LogoScreen"){storySession=false;pending=null;return;}
            if(!storySession)return;
            if(pending!=null && scene.path==pending.scene){SaveLevel(scene.path);pending=null;}
            else if(pending==null && scene.path.StartsWith("Assets/Level/",StringComparison.Ordinal))SaveLevel(scene.path);
        }
        // Called once per death by HurtControl, regardless of its cause.
        public static void LoseLife()
        {
            if(Lives<=0)return;
            Lives--;
            if(!storySession)return; // Editor/direct-level tests never erase an unrelated adventure.
            PersistRemainingLives(SaveKey);
        }
        public static void GainLives(int amount)
        {
            if(amount<=0 || IsGameOver)return;
            Lives=(int)Math.Min(int.MaxValue,(long)Lives+amount);
            if(storySession)PersistRemainingLives(SaveKey);
        }
        static void PersistRemainingLives(string key)
        {
            if(Lives==0)
            {
                PlayerPrefs.DeleteKey(key);pending=null;storySession=false;
            }
            else if(TryParse(PlayerPrefs.GetString(key,""),out var save))
            {
                save.lives=Lives;save.totalScore=TotalScore;PlayerPrefs.SetString(key,JsonUtility.ToJson(save));
            }
            PlayerPrefs.Save();
        }
        public static void ApplyLevelResult(SonicFX.Score.LevelScoreResult result)
        {
            if(result==null || IsGameOver || result.previousTotal!=TotalScore)return;
            TotalScore=result.totalScore;
            Lives=(int)Math.Min(int.MaxValue,(long)Lives+result.bonusLives);
            if(!storySession)return;
            if(TryRead(out var save))
            {
                save.totalScore=TotalScore;save.lives=Lives;
                if(!string.IsNullOrEmpty(result.nextScene) && Application.CanStreamedLevelBeLoaded(result.nextScene))save.scene=result.nextScene;
                PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(save));PlayerPrefs.Save();
            }
        }
        public static void SaveLevel(string scene)
        {
            if(!storySession || IsGameOver || string.IsNullOrEmpty(scene) || !Application.CanStreamedLevelBeLoaded(scene))return;
            PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(new StorySave{character=Character,scene=scene,lives=Lives,totalScore=TotalScore}));PlayerPrefs.Save();
        }
    }
}
