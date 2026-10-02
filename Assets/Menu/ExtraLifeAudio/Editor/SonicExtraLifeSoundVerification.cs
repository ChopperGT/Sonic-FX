using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SonicFX.Menu;
using SonicFX.Score;
using SonicFX.Audio;
using Object=UnityEngine.Object;

namespace SonicFX.Audio.Editor
{
    [InitializeOnLoad] internal static class SonicExtraLifeSoundVerification
    {
        static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/ExtraLifeAudio");
        static SonicExtraLifeSoundVerification(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            EditorApplication.update-=Ready;
            if(!File.Exists(Path.Combine(Folder,"unity-report.txt")))Verify();
        }
        static void Check(bool condition,string name){if(!condition)throw new Exception(name);}
        static Dictionary<FieldInfo,object> Snapshot(Type type)
        {
            var state=new Dictionary<FieldInfo,object>();
            foreach(var field in type.GetFields(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic))
                if(!field.IsLiteral&&!field.IsInitOnly)state[field]=field.GetValue(null);
            return state;
        }
        static void Set(Type type,string field,object value)
        {
            type.GetField(field,BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,value);
        }
        [MenuItem("Tools/Sonic FX/Audio/Verifier le son du gain de vie")]
        static void Verify()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            Directory.CreateDirectory(Folder);
            var progress=Snapshot(typeof(SonicXProgress));var score=Snapshot(typeof(SonicLevelScore));
            int heldRings=Objects_Interaction.RingAmount;
            var preview=EditorSceneManager.NewPreviewScene();GameObject monitor=null,sourceObject=null;
            SonicExtraLifeSoundSettings config=null;
            UnityEngine.Audio.AudioMixer mixer=null;float oldMixerVolume=0;bool hasMixerVolume=false;
            try
            {
                var settings=Resources.Load<SonicExtraLifeSoundSettings>("SonicExtraLifeSound");
                Check(settings!=null&&settings.clip!=null,"Resource settings and supplied clip load");
                Check(settings.clip.name=="Extra life"&&settings.clip.length>5&&settings.clip.length<7,"Correct supplied sound imported");
                Check(settings.mixerGroup!=null&&settings.mixerGroup.audioMixer.name=="SFX","Uses effects volume mixer");
                Check(settings.clip.LoadAudioData(),"Clip data loads");
                var samples=new float[settings.clip.samples*settings.clip.channels];
                Check(settings.clip.GetData(samples,0),"PCM audio data accessible");
                float peak=0;foreach(float sample in samples)peak=Mathf.Max(peak,Mathf.Abs(sample));
                Check(peak>.01f,"Imported sound is not silent");
                config=Object.Instantiate(settings);mixer=config.mixerGroup.audioMixer;
                hasMixerVolume=mixer.GetFloat("Volume",out oldMixerVolume);
                sourceObject=new GameObject("Verification son de vie");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(sourceObject,preview);
                var source=sourceObject.AddComponent<AudioSource>();
                var configure=typeof(SonicExtraLifeAudio).GetMethod("ConfigureSource",BindingFlags.Static|BindingFlags.NonPublic);
                configure.Invoke(null,new object[]{source,config});
                Check(source.clip==settings.clip&&source.outputAudioMixerGroup==settings.mixerGroup,"Audio source has provided clip and SFX routing");
                Check(!source.loop&&!source.playOnAwake&&source.spatialBlend==0&&source.pitch==1&&source.ignoreListenerPause,"2D one-shot unaffected by position or pause");
                Check(Mathf.Approximately(source.volume,settings.volume),"Configured jingle volume");

                Set(typeof(SonicXProgress),"storySession",false);Set(typeof(SonicXProgress),"pending",null);
                Set(typeof(SonicXProgress),"<Lives>k__BackingField",3);Set(typeof(SonicXProgress),"<TotalScore>k__BackingField",0L);
                var awards=new List<int>();SonicXProgress.LivesGained+=awards.Add;
                SonicXProgress.GainLives(1);Check(SonicXProgress.Lives==4&&awards.Count==1&&awards[0]==1,"Direct reward emits one sound notification");
                SonicXProgress.GainLives(0);SonicXProgress.GainLives(-1);SonicXProgress.LoseLife();
                Check(awards.Count==1,"Invalid awards and deaths do not play the jingle");

                SonicLevelScore.BeginLevel(preview);awards.Clear();
                SonicLevelScore.AddRings(99);Check(awards.Count==0,"No sound before 100 rings");
                SonicLevelScore.AddRings(1);Check(awards.Count==1&&awards[0]==1,"100 rings notify the audio service");
                SonicLevelScore.AddRings(200);Check(awards.Count==2&&awards[1]==2,"Batch of two ring lives emits one award notification");

                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Structure/MonitorVie/Monitor_Vie.prefab");
                Check(prefab!=null,"Life monitor exists");monitor=Object.Instantiate(prefab);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(monitor,preview);
                var data=monitor.GetComponent<MonitorData>();
                var claim=typeof(MonitorData).GetMethod("ClaimReward",BindingFlags.NonPublic|BindingFlags.Instance);
                int count=awards.Count;Check((bool)claim.Invoke(data,null)&&awards.Count==count+1,"Life monitor notifies the audio service");
                Check(!(bool)claim.Invoke(data,null)&&awards.Count==count+1,"Duplicate monitor contact does not replay the reward");

                count=awards.Count;var result=SonicLevelScore.Calculate(50000,0,SonicXProgress.TotalScore);
                SonicXProgress.ApplyLevelResult(result);Check(awards.Count==count+1&&awards[awards.Count-1]==1,"50,000-point bonus notifies the same audio service");
                SonicXProgress.ApplyLevelResult(result);Check(awards.Count==count+1,"Repeated result does not replay the reward");

                Set(typeof(SonicXProgress),"<Lives>k__BackingField",0);SonicXProgress.GainLives(1);
                Check(awards.Count==count+1,"Game over cannot grant or play an extra life");
                Set(typeof(SonicXProgress),"<Lives>k__BackingField",int.MaxValue);SonicXProgress.GainLives(1);
                Check(awards.Count==count+1,"No jingle when capped lives do not increase");
                Set(typeof(SonicXProgress),"<Lives>k__BackingField",int.MaxValue-1);SonicXProgress.GainLives(5);
                Check(awards.Count==count+2&&awards[awards.Count-1]==1,"Overflow award announces actual gained life");
                File.WriteAllText(Path.Combine(Folder,"unity-report.txt"),"PASS\nProvided clip imported and non-silent; 2D source and SFX volume configured; gain notifications verified for direct rewards, 100-ring thresholds, life monitor and 50,000-point result. Invalid, duplicate, death, game-over and capped rewards ignored. Batch award emits once. Verification in isolated preview scene; original gameplay state restored, no player save changed.\n"+DateTime.Now.ToString("s"));
                Debug.Log("Son du gain de vie : clip, volume et toutes les methodes verifies.");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Folder,"unity-report.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally
            {
                foreach(var pair in progress)pair.Key.SetValue(null,pair.Value);
                foreach(var pair in score)pair.Key.SetValue(null,pair.Value);
                Objects_Interaction.RingAmount=heldRings;
                if(hasMixerVolume&&mixer!=null)mixer.SetFloat("Volume",oldMixerVolume);
                if(monitor!=null)Object.DestroyImmediate(monitor);if(sourceObject!=null)Object.DestroyImmediate(sourceObject);
                if(config!=null)Object.DestroyImmediate(config);EditorSceneManager.ClosePreviewScene(preview);
            }
        }
    }
}
