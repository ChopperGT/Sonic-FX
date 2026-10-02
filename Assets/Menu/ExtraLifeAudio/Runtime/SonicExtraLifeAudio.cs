using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SonicFX.Menu;

namespace SonicFX.Audio
{
    public sealed class SonicExtraLifeAudio : MonoBehaviour
    {
        static SonicExtraLifeAudio instance;
        static SonicExtraLifeSoundSettings cachedSettings;
        static bool quitting, warned;
        SonicExtraLifeSoundSettings settings;
        AudioSource source;
        int pendingSounds;
        float nextMusicScan;
        readonly Dictionary<AudioSource,AudioClip> pausedMusic=new Dictionary<AudioSource,AudioClip>();
        readonly List<AudioSource> restartedMusic=new List<AudioSource>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntime()
        {
            instance=null;cachedSettings=null;quitting=false;warned=false;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Subscribe()
        {
            SonicXProgress.LivesGained-=OnLivesGained;
            SonicXProgress.LivesGained+=OnLivesGained;
        }
        static void OnLivesGained(int amount)
        {
            if(amount<=0 || !Application.isPlaying || quitting)return;
            if(cachedSettings==null)cachedSettings=Resources.Load<SonicExtraLifeSoundSettings>("SonicExtraLifeSound");
            if(cachedSettings==null || cachedSettings.clip==null)
            {
                if(!warned){warned=true;Debug.LogWarning("Son de vie manquant : Resources/SonicExtraLifeSound.");}
                return;
            }
            if(instance==null)
            {
                var go=new GameObject("Sonic FX - Son de vie");
                DontDestroyOnLoad(go);
                instance=go.AddComponent<SonicExtraLifeAudio>();
                instance.settings=cachedSettings;
                instance.source=go.AddComponent<AudioSource>();
                ConfigureSource(instance.source,cachedSettings);
            }
            if(instance.pendingSounds<int.MaxValue)instance.pendingSounds++;
            instance.PlayPending();
        }
        static void ConfigureSource(AudioSource audio,SonicExtraLifeSoundSettings config)
        {
            audio.playOnAwake=false;audio.loop=false;audio.spatialBlend=0;
            audio.pitch=1;audio.priority=32;audio.ignoreListenerPause=true;
            audio.clip=config.clip;audio.outputAudioMixerGroup=config.mixerGroup;
            ApplyVolume(audio,config);
        }
        static void ApplyVolume(AudioSource audio,SonicExtraLifeSoundSettings config)
        {
            float db=PlayerPrefs.GetFloat("SFX_VOL",1);
            audio.volume=Mathf.Clamp01(config.volume);
            if(config.mixerGroup!=null)config.mixerGroup.audioMixer.SetFloat("Volume",db);
            else audio.volume*=db<=-49?0:Mathf.Clamp01(Mathf.Pow(10,(db-1)/20));
        }
        bool IsLevelMusic(AudioSource candidate)
        {
            if(candidate==null || candidate==source || candidate.clip==null)return false;
            // The drowning alarm is attached to Sonic and must keep its own countdown.
            if(candidate.GetComponent<PlayerBhysics>()!=null)return false;
            var group=candidate.outputAudioMixerGroup;
            if(settings.musicMixer!=null && group!=null && group.audioMixer==settings.musicMixer)return true;
            // Also supports music added by hand without routing through the mixer.
            return candidate.gameObject.name=="Musique_Niveau" || candidate.gameObject.name=="[MenuMusic]";
        }
        void PauseMusicSources(AudioSource[] candidates)
        {
            if(settings==null || !settings.pauseLevelMusic)return;
            foreach(var candidate in candidates)
            {
                if(!IsLevelMusic(candidate) || !candidate.isPlaying)continue;
                pausedMusic[candidate]=candidate.clip;
                candidate.Pause(); // Preserve the cursor, volume, mute and loop settings.
            }
        }
        void PauseLevelMusic()
        {
            PauseMusicSources(Object.FindObjectsByType<AudioSource>());
            nextMusicScan=Time.unscaledTime+.25f;
        }
        void ResumeLevelMusic()
        {
            foreach(var pair in pausedMusic)
            {
                var music=pair.Key;
                if(music!=null && music.isActiveAndEnabled && music.gameObject.activeInHierarchy && music.clip==pair.Value)
                    music.UnPause(); // Does not restart stopped sources or resurrect an old clip.
            }
            pausedMusic.Clear();
        }
        void PlayPending()
        {
            if(source==null || settings==null)return;
            if(source.isPlaying)
            {
                // A track changed by a gameplay trigger must stay paused too.
                restartedMusic.Clear();
                foreach(var pair in pausedMusic)
                    if(pair.Key!=null && pair.Key.isPlaying)restartedMusic.Add(pair.Key);
                foreach(var music in restartedMusic)
                {
                    pausedMusic[music]=music.clip;
                    music.Pause();
                }
                if(Time.unscaledTime>=nextMusicScan)PauseLevelMusic();
                return;
            }
            if(pendingSounds>0)
            {
                pendingSounds--;PauseLevelMusic();ApplyVolume(source,settings);source.Play();
                if(!source.isPlaying && source.clip==null)ResumeLevelMusic();
            }
            else ResumeLevelMusic();
        }
        void SceneLoaded(Scene scene,LoadSceneMode mode)
        {
            if(source!=null && (source.isPlaying || pendingSounds>0))PauseLevelMusic();
        }
        void OnEnable(){SceneManager.sceneLoaded+=SceneLoaded;}
        void Update(){PlayPending();}
        void OnDisable()
        {
            SceneManager.sceneLoaded-=SceneLoaded;
            pendingSounds=0;if(source!=null)source.Stop();
            if(!quitting)ResumeLevelMusic();
        }
        void OnApplicationQuit(){quitting=true;}
        void OnDestroy(){if(instance==this)instance=null;}
    }
}
