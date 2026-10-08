using UnityEngine;
using UnityEngine.Audio;
namespace SonicFX.Lava
{
    public sealed class SonicGeyserProjectile : MonoBehaviour
    {
        public bool Landed { get; private set; }
        public float Progress { get; private set; }
        Vector3 start,end,normal;Quaternion landedRotation;float duration,height,time,lifetime,landedTime;
        Collider[] colliders;SonicFX.Magma.SonicMagmaRock[] damage;
        SonicGeyserWarning warning;
        AudioClip impactSound;float impactVolume;AudioMixerGroup impactMixer;
        public AudioSource ImpactSource {get;private set;}
        public void Initialise(Vector3 origin,Vector3 destination,Vector3 groundNormal,float flightDuration,float arc,float retainedLifetime,SonicGeyserWarning marker,AudioClip landingSound=null,float landingVolume=1,AudioMixerGroup mixer=null)
        {
            start=origin;end=destination;normal=groundNormal;duration=Mathf.Max(.3f,flightDuration);height=Mathf.Max(1,arc);lifetime=Mathf.Max(0,retainedLifetime);warning=marker;
            impactSound=landingSound;impactVolume=Mathf.Clamp01(landingVolume);impactMixer=mixer;ImpactSource=null;
            landedRotation=Quaternion.FromToRotation(Vector3.up,normal)*Quaternion.Euler(0,Random.Range(0,360),0);
            colliders=GetComponentsInChildren<Collider>();damage=GetComponentsInChildren<SonicFX.Magma.SonicMagmaRock>();
            foreach(var c in colliders)c.enabled=false;foreach(var d in damage)d.contactDamage=false;
            foreach(var rb in GetComponentsInChildren<Rigidbody>()){rb.isKinematic=true;rb.useGravity=false;}
            transform.position=start;transform.rotation=landedRotation;Landed=false;time=0;Progress=0;
        }
        public static Vector3 Trajectory(Vector3 start,Vector3 end,float arc,float t)
        {
            // Clear the higher endpoint, including platforms above the vent.
            float clearance=Mathf.Max(1,arc)+Mathf.Abs(end.y-start.y)*.5f;
            return Vector3.Lerp(start,end,t)+Vector3.up*(4*clearance*t*(1-t));
        }
        public void Tick(float delta)
        {
            if(Landed){landedTime+=delta;if(lifetime>0 && landedTime>=lifetime)gameObject.SetActive(false);return;}
            time+=Mathf.Max(0,delta);Progress=Mathf.Clamp01(time/duration);transform.position=Trajectory(start,end,height,Progress);
            transform.rotation=landedRotation*Quaternion.Euler(Progress*540,Progress*180,Progress*360);
            if(warning!=null)warning.SetProgress(Progress);
            if(Progress<1)return;
            transform.position=end;transform.rotation=landedRotation;Landed=true;landedTime=0;
            PlayImpact();
            foreach(var c in colliders)if(c!=null)c.enabled=true;foreach(var d in damage)if(d!=null){d.contactDamage=true;d.Refresh();}
            if(warning!=null){warning.gameObject.SetActive(false);Dispose(warning.gameObject);warning=null;}
        }
        void PlayImpact()
        {
            if(impactSound==null || impactVolume<=0)return;
            // Independent of the rock so its audio tail survives a short rock lifetime.
            var sound=new GameObject("Son impact roche du geyser");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(sound,gameObject.scene);
            sound.transform.position=end;
            ImpactSource=sound.AddComponent<AudioSource>();ImpactSource.playOnAwake=false;
            ImpactSource.clip=impactSound;ImpactSource.volume=impactVolume;ImpactSource.loop=false;
            ImpactSource.spatialBlend=1;ImpactSource.minDistance=8;ImpactSource.maxDistance=90;
            ImpactSource.dopplerLevel=0;ImpactSource.outputAudioMixerGroup=impactMixer;
            if(Application.IsPlaying(gameObject))
            {
                ImpactSource.Play();Destroy(sound,impactSound.length+.1f);
            }
        }
        public static void Dispose(Object obj){if(obj==null)return;if(Application.isPlaying)Destroy(obj);else DestroyImmediate(obj);}
        void OnDestroy(){if(warning!=null)Dispose(warning.gameObject);}
    }
}
