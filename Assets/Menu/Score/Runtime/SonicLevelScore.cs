using System;
using UnityEngine;
using SonicFX.Menu;

namespace SonicFX.Score
{
    [Serializable] public sealed class LevelScoreResult
    {
        public long levelScore, ringBonus, previousTotal, totalScore;
        public int rings, bonusLives, timeBonus, noDeathBonus, deaths;
        public float elapsedSeconds;
        public string nextScene;
    }

    public static class SonicLevelScore
    {
        public const int PointsPerRing = 10;
        public const int RingsPerLife = 100;
        public static long RingsCollected { get; private set; }
        public static int RingsTowardLife { get; private set; }
        public const int PointsPerLife = 50000;
        public const int NoDeathReward = 1000;
        public static float Elapsed { get; private set; }
        public static int Deaths { get; private set; }
        public static long Current { get; private set; }
        public static bool IsRunning { get; private set; }
        public static LevelScoreResult LastResult { get; private set; }
        static UnityEngine.SceneManagement.Scene levelScene;
        static bool hasLevel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Current=0; RingsCollected=0; RingsTowardLife=0; Elapsed=0; Deaths=0; IsRunning=false; LastResult=null; hasLevel=false; }

        public static void BeginLevel(UnityEngine.SceneManagement.Scene scene)
        {
            if(hasLevel && levelScene==scene)return;
            levelScene=scene; hasLevel=true; Current=0; RingsCollected=0; RingsTowardLife=0; Elapsed=0; Deaths=0; LastResult=null; IsRunning=true;
            SonicXProgress.BeginRedRingLevel();
        }

        // Receives scaled gameplay delta time: pausing contributes zero seconds.
        public static void Tick(float deltaTime,bool playerDead)
        {
            if(IsRunning && !playerDead && !SonicXProgress.IsGameOver && deltaTime>0 && !float.IsInfinity(deltaTime))Elapsed+=deltaTime;
        }

        public static void RecordDeath()
        {
            ResetRingLifeProgress();
            if(IsRunning && Deaths<int.MaxValue)Deaths++;
        }

        // Only accepted damage resets the streak, not contacts during invincibility.
        public static void ResetRingLifeProgress()
        {
            RingsTowardLife=0;
        }

        public static int CalculateTimeBonus(float elapsedSeconds,int maximum,float idealSeconds,float limitSeconds)
        {
            if(maximum<=0 || float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds))return 0;
            if(float.IsNaN(idealSeconds) || float.IsNaN(limitSeconds) || float.IsInfinity(idealSeconds) || float.IsInfinity(limitSeconds))return 0;
            double ideal=Math.Max(0,idealSeconds),limit=Math.Max(ideal+.01,limitSeconds);
            if(elapsedSeconds<=ideal)return maximum;
            if(elapsedSeconds>=limit)return 0;
            return (int)Math.Round(maximum*(limit-elapsedSeconds)/(limit-ideal),MidpointRounding.AwayFromZero);
        }

        public static void AddPoints(int points)
        {
            if(IsRunning && points>0 && !SonicXProgress.IsGameOver)
                Current=Math.Min(int.MaxValue, Current+points);
        }

        // Deactivation happens immediately, unlike Destroy, which waits until frame end.
        public static bool CollectRing(GameObject ring)
        {
            if(!IsRunning || ring==null || !ring.activeInHierarchy)return false;
            ring.SetActive(false); AddRings(1); return true;
        }

        public static void AddRings(int count)
        {
            if(!IsRunning || count<=0 || SonicXProgress.IsGameOver)return;
            Objects_Interaction.RingAmount=(int)Math.Min(int.MaxValue,(long)Math.Max(0,Objects_Interaction.RingAmount)+count);
            AddPoints((int)Math.Min(int.MaxValue,(long)count*PointsPerRing));
            // Keep the level statistics, but award lives only for rings since the last hit.
            RingsCollected+=Math.Min(long.MaxValue-RingsCollected,count);
            long progress=(long)RingsTowardLife+count;
            int lives=(int)(progress/RingsPerLife);
            RingsTowardLife=(int)(progress%RingsPerLife);
            SonicXProgress.GainLives(lives);
        }

        public static LevelScoreResult Calculate(long levelScore,int rings,long previousTotal,string nextScene=null,int timeBonus=0,int noDeathBonus=0,float elapsedSeconds=0,int deaths=0)
        {
            long score=Math.Max(0,levelScore), bonus=(long)Math.Max(0,rings)*PointsPerRing;
            long previous=Math.Max(0,previousTotal);
            long addition=Math.Min(long.MaxValue-previous,score);
            long total=previous+addition;
            total+=Math.Min(long.MaxValue-total,bonus);
            timeBonus=Math.Max(0,timeBonus);noDeathBonus=Math.Max(0,noDeathBonus);
            total+=Math.Min(long.MaxValue-total,timeBonus);
            total+=Math.Min(long.MaxValue-total,noDeathBonus);
            return new LevelScoreResult {
                levelScore=score,rings=Math.Max(0,rings),ringBonus=bonus,previousTotal=previous,totalScore=total,
                bonusLives=(int)Math.Min(int.MaxValue,total/PointsPerLife-previous/PointsPerLife),nextScene=nextScene,
                timeBonus=timeBonus,noDeathBonus=noDeathBonus,elapsedSeconds=Math.Max(0,elapsedSeconds),deaths=Math.Max(0,deaths)
            };
        }

        public static LevelScoreResult Complete(int rings,string nextScene=null,int maximumTimeBonus=10000,float idealSeconds=120,float limitSeconds=300)
        {
            if(!IsRunning || SonicXProgress.IsGameOver)return LastResult;
            IsRunning=false;
            LastResult=Calculate(Current,rings,SonicXProgress.TotalScore,nextScene,
                CalculateTimeBonus(Elapsed,maximumTimeBonus,idealSeconds,limitSeconds),Deaths==0?NoDeathReward:0,Elapsed,Deaths);
            SonicXProgress.CommitRedRingReward(levelScene.path);
            SonicXProgress.ApplyLevelResult(LastResult);
            // The completion gate above prevents duplicate goal triggers from adding a record twice.
            // Preview scenes and editor verification never write to the player's rankings.
            if(Application.isPlaying && !string.IsNullOrEmpty(levelScene.path))
                SonicTimeRecords.Record(levelScene.path,Elapsed,SonicXProgress.Character);
            return LastResult;
        }
    }
}
