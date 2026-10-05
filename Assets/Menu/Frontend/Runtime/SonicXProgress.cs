using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SonicFX.Menu
{
    [Serializable] public class StorySave { public int version=5; public int unlockedAbilities; public long totalScore; public int lives=3; public string character="sonic"; public string scene; public string[] redRingLevels=Array.Empty<string>(); }
    public static class SonicXProgress
    {
        public const string FirstLevel="Assets/Level/Sonic 1/Act 1 GreenHiill/act 1-1/act 1-1.unity";
        const string SaveKey="SonicFX.Story.Save.v1";
        static StorySave pending; static bool storySession;
        static readonly HashSet<string> redRingLevels=new HashSet<string>(StringComparer.Ordinal);
        static string pendingRedRingLevel;
        public static int RedRingRewards=>redRingLevels.Count;
        public static int RedRingTopSpeedBonus=>Math.Min(4,RedRingRewards)*5;
        public static bool HasRedRingReward(string scene)=>!string.IsNullOrEmpty(scene)&&redRingLevels.Contains(scene);
        public static void BeginRedRingLevel(){pendingRedRingLevel=null;}
        public static bool MarkRedRingChallengeComplete(string scene)
        {
            if(!storySession||IsGameOver||string.IsNullOrEmpty(scene)||HasRedRingReward(scene))return false;
            pendingRedRingLevel=scene;return true;
        }
        public static bool CommitRedRingReward(string scene)
        {
            if(!storySession||IsGameOver||string.IsNullOrEmpty(scene)||pendingRedRingLevel!=scene)return false;
            pendingRedRingLevel=null;return redRingLevels.Add(scene);
        }
        public static void ApplyRedRingSpeedBonus(PlayerBhysics player)
        {
            if(player==null||!storySession||RedRingRewards==0)return;
            player.MaxSpeed=Mathf.Min(300,player.MaxSpeed+RedRingRewards);
            player.TopSpeed=Mathf.Min(player.MaxSpeed,player.TopSpeed+RedRingTopSpeedBonus);
        }
        static void RestoreRedRingLevels(string[] levels)
        {
            redRingLevels.Clear();pendingRedRingLevel=null;
            if(levels!=null)foreach(string level in levels)if(!string.IsNullOrEmpty(level))redRingLevels.Add(level);
        }
        static void CopyRedRingProgress(StorySave save){save.version=5;save.redRingLevels=redRingLevels.OrderBy(s=>s,StringComparer.Ordinal).ToArray();}
        public static int Lives {get;private set;}=3;
        public static event Action<int> LivesGained;
        public static long TotalScore {get;private set;}
        public static SonicAbility UnlockedAbilities {get;private set;}=SonicAbility.None;
        public static bool UnlockAbility(SonicAbility ability)
        {
            if(IsGameOver || ability==SonicAbility.None || (ability&~SonicAbility.All)!=0)return false;
            var updated=UnlockedAbilities|ability;if(updated==UnlockedAbilities)return false;
            UnlockedAbilities=updated;
            if(pending!=null)pending.unlockedAbilities=(int)updated;
            PersistUnlockedAbilities(SaveKey);
            return true;
        }
        static void PersistUnlockedAbilities(string key)
        {
            if(!storySession || !TryParse(PlayerPrefs.GetString(key,""),out var save))return;
            save.unlockedAbilities=(int)UnlockedAbilities;PlayerPrefs.SetString(key,JsonUtility.ToJson(save));PlayerPrefs.Save();
        }
        public static bool IsGameOver=>Lives<=0;
        public static bool IsStorySession=>storySession;
        public static string Character { get; private set; }="sonic";
        public static bool IsUnlocked(string id)=>id=="sonic" || PlayerPrefs.GetInt("SonicFX.Unlock."+id,0)==1;
        public static void Unlock(string id){PlayerPrefs.SetInt("SonicFX.Unlock."+id,1);PlayerPrefs.Save();}
        public static bool TryRead(out StorySave data)=>TryParse(PlayerPrefs.GetString(SaveKey,""),out data);
        public static bool TryParse(string json,out StorySave data)
        {
            data=null;if(string.IsNullOrEmpty(json))return false;
            try{var value=JsonUtility.FromJson<StorySave>(json);if(value==null || value.version<1 || value.version>5 || string.IsNullOrEmpty(value.scene) || (value.character!="sonic" && value.character!="tails" && value.character!="amy" && value.character!="shadow"))return false;if(value.version==1){value.lives=3;value.version=2;}if(value.version<3){value.totalScore=0;value.version=3;}if(value.version<4){value.unlockedAbilities=0;value.version=4;}if(value.version<5)value.redRingLevels=Array.Empty<string>();value.version=5;value.redRingLevels=(value.redRingLevels??Array.Empty<string>()).Where(s=>!string.IsNullOrEmpty(s)).Distinct(StringComparer.Ordinal).ToArray();value.unlockedAbilities&=(int)SonicAbility.All;if(value.lives<=0 || value.totalScore<0)return false;data=value;return true;}catch{return false;}
        }
        public static bool CanContinue=>TryRead(out var save) && Application.CanStreamedLevelBeLoaded(save.scene);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){LivesGained=null;pending=null;storySession=false;RestoreRedRingLevels(null);Lives=3;TotalScore=0;UnlockedAbilities=SonicAbility.None;Character="sonic";SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
        public static AsyncOperation Begin(string character,string scene,int lives=3,long totalScore=0,SonicAbility unlockedAbilities=SonicAbility.None,string[] completedRedRingLevels=null)
        {
            // A story launch never inherits a free-play character or its abilities.
            SonicNewLevelSession.End();
            if(character!="sonic" && character!="tails" && character!="amy" && character!="shadow")return null;
            if(lives<=0 || !Application.CanStreamedLevelBeLoaded(scene))return null;
            RestoreRedRingLevels(completedRedRingLevels);
            Time.timeScale=1;Character=character;Lives=lives;TotalScore=Math.Max(0,totalScore);UnlockedAbilities=unlockedAbilities&SonicAbility.All;Objects_Interaction.RingAmount=0;pending=new StorySave{character=character,scene=scene,lives=lives,totalScore=TotalScore,unlockedAbilities=(int)UnlockedAbilities};CopyRedRingProgress(pending);storySession=true;
            try {var op=SceneManager.LoadSceneAsync(scene);if(op==null){pending=null;storySession=false;}return op;}
            catch{pending=null;storySession=false;throw;}
        }
        static void Loaded(Scene scene,LoadSceneMode mode)
        {
            if(mode!=LoadSceneMode.Single)return;
            if(scene.name=="LogoScreen"){storySession=false;pending=null;UnlockedAbilities=SonicAbility.None;SonicNewLevelSession.End();return;}
            if(!storySession)return;
            if(pending!=null && scene.path==pending.scene){SaveLevel(scene.path);pending=null;}
            else if(pending==null && scene.path.StartsWith("Assets/Level/",StringComparison.Ordinal))SaveLevel(scene.path);
        }
        // Independent run: leave the existing story save and its unlocks untouched.
        internal static void BeginNewLevel(string character)
        {
            pending=null;storySession=false;RestoreRedRingLevels(null);Lives=3;TotalScore=0;
            UnlockedAbilities=SonicAbility.None;Character=character;Objects_Interaction.RingAmount=0;
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
            int previous=Lives;
            Lives=(int)Math.Min(int.MaxValue,(long)Lives+amount);
            if(Lives==previous)return;
            if(storySession)PersistRemainingLives(SaveKey);
            LivesGained?.Invoke(Lives-previous);
        }
        static void PersistRemainingLives(string key)
        {
            if(Lives==0)
            {
                PlayerPrefs.DeleteKey(key);pending=null;storySession=false;RestoreRedRingLevels(null);UnlockedAbilities=SonicAbility.None;
            }
            else if(TryParse(PlayerPrefs.GetString(key,""),out var save))
            {
                save.lives=Lives;save.totalScore=TotalScore;save.unlockedAbilities=(int)UnlockedAbilities;CopyRedRingProgress(save);PlayerPrefs.SetString(key,JsonUtility.ToJson(save));
            }
            PlayerPrefs.Save();
        }
        public static void ApplyLevelResult(SonicFX.Score.LevelScoreResult result)
        {
            if(result==null || IsGameOver || result.previousTotal!=TotalScore)return;
            TotalScore=result.totalScore;
            GainLives(result.bonusLives);
            if(!storySession)return;
            if(TryRead(out var save))
            {
                save.totalScore=TotalScore;save.lives=Lives;save.unlockedAbilities=(int)UnlockedAbilities;
                CopyRedRingProgress(save);
                if(!string.IsNullOrEmpty(result.nextScene) && Application.CanStreamedLevelBeLoaded(result.nextScene))save.scene=result.nextScene;
                PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(save));PlayerPrefs.Save();
            }
        }
        public static void SaveLevel(string scene)
        {
            if(!storySession || IsGameOver || string.IsNullOrEmpty(scene) || !Application.CanStreamedLevelBeLoaded(scene))return;
            var save=new StorySave{character=Character,scene=scene,lives=Lives,totalScore=TotalScore,unlockedAbilities=(int)UnlockedAbilities};CopyRedRingProgress(save);
            PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(save));PlayerPrefs.Save();
        }
    }
}
