using System;
using System.Collections.Generic;
using UnityEngine;

namespace SonicFX.Menu
{
    [Serializable] public sealed class SonicTimeRecord
    {
        public long milliseconds;
        public string character;
        public string achievedUtc;
    }
    [Serializable] public sealed class SonicLevelTimeRecords
    {
        public string scene;
        public List<SonicTimeRecord> records=new List<SonicTimeRecord>();
    }
    [Serializable] sealed class SonicTimeRecordSave
    {
        public int version=1;
        public List<SonicLevelTimeRecords> levels=new List<SonicLevelTimeRecords>();
    }

    public static class SonicTimeRecords
    {
        const string Key="SonicFX.TimeRecords.v1";
        public const int RecordsPerLevel=5;

        public static List<SonicLevelTimeRecords> ReadAll()=>ReadKey(Key).levels;
        public static bool Record(string scene,float seconds,string character)=>RecordToKey(Key,scene,seconds,character);

        static bool RecordToKey(string key,string scene,float seconds,string character)
        {
            if(string.IsNullOrWhiteSpace(scene)||float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<=0)return false;
            double milliseconds=Math.Floor((double)seconds*1000);
            if(milliseconds<1 || milliseconds>=long.MaxValue)return false;
            var save=ReadKey(key);
            var level=save.levels.Find(value=>value.scene==scene);
            if(level==null){level=new SonicLevelTimeRecords{scene=scene};save.levels.Add(level);}
            var record=new SonicTimeRecord{milliseconds=(long)milliseconds,character=NormalizeCharacter(character),achievedUtc=DateTime.UtcNow.ToString("o")};
            int index=level.records.FindIndex(value=>value.milliseconds>record.milliseconds);
            if(index<0)index=level.records.Count;
            if(index>=RecordsPerLevel)return false;
            level.records.Insert(index,record);
            if(level.records.Count>RecordsPerLevel)level.records.RemoveAt(RecordsPerLevel);
            save.levels.Sort((a,b)=>StringComparer.Ordinal.Compare(a.scene,b.scene));
            // Separate from the adventure slot: new games and Game Over preserve records.
            PlayerPrefs.SetString(key,JsonUtility.ToJson(save));PlayerPrefs.Save();
            return true;
        }

        static SonicTimeRecordSave ReadKey(string key)
        {
            SonicTimeRecordSave save;
            try{save=JsonUtility.FromJson<SonicTimeRecordSave>(PlayerPrefs.GetString(key,""));}
            catch{return new SonicTimeRecordSave();}
            if(save==null || save.version!=1 || save.levels==null)return new SonicTimeRecordSave();
            var clean=new SonicTimeRecordSave();
            foreach(var level in save.levels)
            {
                if(level==null || string.IsNullOrWhiteSpace(level.scene) || level.records==null)continue;
                var target=clean.levels.Find(value=>value.scene==level.scene);
                if(target==null){target=new SonicLevelTimeRecords{scene=level.scene};clean.levels.Add(target);}
                foreach(var record in level.records)
                    if(record!=null && record.milliseconds>0){record.character=NormalizeCharacter(record.character);target.records.Add(record);}
            }
            clean.levels.RemoveAll(level=>level.records.Count==0);
            foreach(var level in clean.levels)
            {
                level.records.Sort((a,b)=>a.milliseconds.CompareTo(b.milliseconds));
                if(level.records.Count>RecordsPerLevel)level.records.RemoveRange(RecordsPerLevel,level.records.Count-RecordsPerLevel);
            }
            clean.levels.Sort((a,b)=>StringComparer.Ordinal.Compare(a.scene,b.scene));return clean;
        }
        static string NormalizeCharacter(string value)=>value=="tails"||value=="amy"||value=="shadow"||SonicNewLevelCatalog.Load()?.Find(value)!=null?value:"sonic";
        public static string CharacterName(string value)=>SonicNewLevelCatalog.Load()?.Find(value)?.displayName??(value=="tails"?"Tails":value=="amy"?"Amy":value=="shadow"?"Shadow":"Sonic (jeune)");
        public static string FormatTime(long milliseconds)
        {
            milliseconds=Math.Max(0,milliseconds);
            return (milliseconds/60000).ToString("00")+"'"+(milliseconds/1000%60).ToString("00")+"\""+(milliseconds%1000).ToString("000");
        }
        public static string LevelName(string scene)
        {
            if(string.IsNullOrEmpty(scene))return "Niveau";
            string path=scene.Replace('\\','/');
            string name=System.IO.Path.GetFileNameWithoutExtension(path);
            if(name.Length>0)name=char.ToUpperInvariant(name[0])+name.Substring(1);
            const string prefix="Assets/Level/";
            if(path.StartsWith(prefix,StringComparison.Ordinal))
            {
                string remainder=path.Substring(prefix.Length);int slash=remainder.IndexOf('/');
                if(slash>0)return remainder.Substring(0,slash)+" - "+name;
            }
            return name;
        }
    }
}
