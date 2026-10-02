using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using SonicFX.Audio;
using Object=UnityEngine.Object;

[InitializeOnLoad] internal static class SonicLifeMusicRuntimeVerification
{
    static bool launched;
    static SonicLifeMusicRuntimeVerification(){EditorApplication.update+=Ready;}
    static void Ready()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||launched)return;
        string request=Path.Combine(SonicSteeringVerification.Reports,"runtime-request.txt");
        if(!File.Exists(request))return;
        if(!EditorApplication.isPlaying)
        {
            if(!EditorApplication.isPlayingOrWillChangePlaymode&&File.ReadAllText(request).Contains("launch-play"))
                EditorApplication.isPlaying=true;
            return;
        }
        launched=true;
        var go=new GameObject("Verification temporaire musique de vie");
        go.AddComponent<SonicLifeMusicProbe>().returnToEdit=File.ReadAllText(request).Contains("return-to-edit");
        File.Delete(request);
    }
}

internal sealed class SonicLifeMusicProbe : MonoBehaviour
{
    public bool returnToEdit;
    Scene testScene;GameObject holder;SonicExtraLifeAudio service;
    AudioClip testTrack,testJingle;SonicExtraLifeSoundSettings config;
    AudioMixer sfx;float oldSfx;bool restoreSfx;
    string failure;
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Set(SonicExtraLifeAudio obj,string name,object value){typeof(SonicExtraLifeAudio).GetField(name,Flags).SetValue(obj,value);}
    static void Call(SonicExtraLifeAudio obj,string name,params object[] args){typeof(SonicExtraLifeAudio).GetMethod(name,Flags).Invoke(obj,args);}
    IEnumerator Start()
    {
        // Execute as real play-mode audio. No life, score or save is changed.
        var test=Run();
        while(true)
        {
            bool more=false;object current=null;
            try{more=test.MoveNext();if(more)current=test.Current;}
            catch(Exception e){failure=e.ToString();}
            if(failure!=null||!more)break;
            yield return current;
        }
        Cleanup();
        string report=failure==null?"PASS\nReal play-mode audio: music paused at its current sample; cursor stable while paused; SFX and drowning alarm excluded; queued jingles keep music paused; same track resumes after the final jingle; restarted/replaced music resumes its new clip; disabling the service restores paused music. No lives, score or player save changed.\n":"FAIL\n"+failure+"\n";
        File.WriteAllText(Path.Combine(SonicSteeringVerification.Reports,"music-runtime-report.txt"),report+"SFX_VOL="+PlayerPrefs.GetFloat("SFX_VOL",1)+" dB\n"+DateTime.Now.ToString("s"));
        if(failure!=null)Debug.LogError(report);else Debug.Log("Musique de vie : pause, attente et reprise verifiees en Play.");
        if(returnToEdit)EditorApplication.isPlaying=false;
        Object.Destroy(gameObject);
    }
    IEnumerator Run()
    {
        var original=Resources.Load<SonicExtraLifeSoundSettings>("SonicExtraLifeSound");
        Check(original!=null&&original.clip!=null&&original.pauseLevelMusic&&original.musicMixer!=null,"Real sound and music mixer installed");
        config=Object.Instantiate(original);config.volume=0;
        sfx=config.mixerGroup!=null?config.mixerGroup.audioMixer:null;
        restoreSfx=sfx!=null&&sfx.GetFloat("Volume",out oldSfx);
        testTrack=AudioClip.Create("Verification piste",48000,1,48000,false);
        testJingle=AudioClip.Create("Verification jingle",9600,1,48000,false);
        config.clip=testJingle;
        testScene=SceneManager.CreateScene("Verification musique temporaire",new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        holder=new GameObject("Musique de vie - Verification");SceneManager.MoveGameObjectToScene(holder,testScene);
        service=holder.AddComponent<SonicExtraLifeAudio>();service.enabled=false;
        var jingle=holder.AddComponent<AudioSource>();
        typeof(SonicExtraLifeAudio).GetMethod("ConfigureSource",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{jingle,config});
        Set(service,"source",jingle);Set(service,"settings",config);
        var music=MakeSource("Probe musique",config.musicMixer.FindMatchingGroups("")[0]);
        var effect=MakeSource("Probe effet",config.mixerGroup);
        var alarm=MakeSource("Probe alarme",music.outputAudioMixerGroup);
        var physics=alarm.gameObject.AddComponent<PlayerBhysics>();physics.enabled=false;
        music.Play();effect.Play();alarm.Play();yield return new WaitForSecondsRealtime(.1f);
        Check(music.isPlaying&&effect.isPlaying&&alarm.isPlaying,"Real audio sources started");
        Call(service,"PauseMusicSources",new object[]{new[]{music,effect,alarm,jingle}});
        int cursor=music.timeSamples;
        Check(!music.isPlaying&&effect.isPlaying&&alarm.isPlaying,"Only level music pauses");
        yield return new WaitForSecondsRealtime(.12f);
        Check(Mathf.Abs(music.timeSamples-cursor)<240,"Paused music cursor remains stable");
        Set(service,"pendingSounds",2);Call(service,"PlayPending");
        Check(jingle.isPlaying&&!music.isPlaying,"First jingle starts with music paused");
        yield return new WaitForSecondsRealtime(.3f);Call(service,"PlayPending");
        Check(jingle.isPlaying&&!music.isPlaying,"Second queued jingle does not resume level music early");
        yield return new WaitForSecondsRealtime(.3f);Call(service,"PlayPending");
        Check(music.isPlaying,"Music resumes when final jingle ends");
        Check(Mathf.Abs(music.timeSamples-cursor)<2400,"Music resumes from its saved cursor, not from the beginning");

        // A music changer can replace the track during the interruption.
        Set(service,"pendingSounds",1);Call(service,"PlayPending");
        music.clip=testJingle;music.loop=true;music.Play();Call(service,"PlayPending");
        Check(!music.isPlaying,"Replaced music remains paused during the jingle");
        yield return new WaitForSecondsRealtime(.3f);Call(service,"PlayPending");
        Check(music.isPlaying&&music.clip==testJingle,"New track resumes instead of the old track");
        Call(service,"PauseMusicSources",new object[]{new[]{music}});Call(service,"OnDisable");
        Check(music.isPlaying,"Service disable releases its music pause");
    }
    AudioSource MakeSource(string name,AudioMixerGroup group)
    {
        var go=new GameObject(name);go.transform.SetParent(holder.transform);
        var audio=go.AddComponent<AudioSource>();audio.clip=testTrack;audio.loop=true;audio.playOnAwake=false;
        audio.volume=0;audio.outputAudioMixerGroup=group;audio.ignoreListenerPause=true;return audio;
    }
    void Cleanup()
    {
        if(service!=null)Call(service,"ResumeLevelMusic");
        if(restoreSfx&&sfx!=null)sfx.SetFloat("Volume",oldSfx);
        if(holder!=null)Object.Destroy(holder);
        if(testScene.IsValid())SceneManager.UnloadSceneAsync(testScene);
        if(config!=null)Object.Destroy(config);if(testTrack!=null)Object.Destroy(testTrack);if(testJingle!=null)Object.Destroy(testJingle);
    }
}
