using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
namespace SonicFX.Lava
{
    [ExecuteAlways,DisallowMultipleComponent,AddComponentMenu("Sonic FX/Lave/Geyser de lave")]
    public sealed class SonicLavaGeyser : MonoBehaviour
    {
        [Header("Alignement facultatif sur la lave")]
        [Tooltip("Utilise uniquement par le bouton d'alignement. Aucune lave n'est necessaire pour declencher l'eruption."), InspectorName("Volume de lave pour l'alignement")] public SonicLavaVolume lava;
        [Min(.1f), InspectorName("Distance de recherche pour l'alignement")] public float surfaceTolerance=5;
        [Header("Eruption")]
        [Min(.2f), InspectorName("Delai d'avertissement (secondes)")] public float warningDelay=1.2f;
        [Min(0), InspectorName("Delai entre deux rochers")] public float rockInterval=.18f;
        [Min(0), InspectorName("Pause entre deux eruptions")] public float cooldown=6;
        [Tooltip("0 : les rochers restent jusqu'a la fin du niveau."), Min(0), InspectorName("Duree des rochers apres impact")] public float rockLifetime;
        [Min(.1f), InspectorName("Marge de la marque rouge")] public float warningMargin=.5f;
        [Header("Apparence")]
        [Min(.25f), InspectorName("Rayon du geyser")] public float ventRadius=2;
        [Min(.1f), InspectorName("Hauteur du rebord")] public float ventHeight=.8f;
        [Min(1), InspectorName("Hauteur du jet visuel")] public float jetHeight=12;
        [InspectorName("Son de l'eruption (facultatif)")] public AudioClip eruptionSound;
        [Header("Son des impacts")]
        [InspectorName("Jouer le son a l'atterrissage")] public bool playImpactSound=true;
        [Tooltip("Vide : utilise le bruit de debris fourni par defaut."), InspectorName("Son d'atterrissage des rochers")] public AudioClip impactSound;
        [Range(0,1), InspectorName("Volume des impacts")] public float impactVolume=1;
        public AudioClip ResolvedImpactSound=>impactSound!=null?impactSound:Resources.Load<AudioClip>("Geyser_Roche_Impact");
        [Header("Zones et modeles")]
        public SonicGeyserLandingZone[] landingZones=new SonicGeyserLandingZone[0];
        public GameObject[] rockPrefabs=new GameObject[0];
        [SerializeField,HideInInspector] public Transform vent,mouth;
        [SerializeField,HideInInspector] public ParticleSystem jet,sparks;
        [SerializeField,HideInInspector] public Material warningMaterial;
        [SerializeField,HideInInspector] public AudioSource audioSource;
        [SerializeField,HideInInspector] public Renderer jetVisual;
        [InspectorName("Evenement eruption")] public UnityEvent onErupted=new UnityEvent();
        class Plan{public SonicGeyserLandingZone zone;public GameObject prefab;public Vector3 point,normal;public float scale,due;public SonicGeyserWarning warning;}
        readonly List<Plan> plans=new List<Plan>();
        readonly List<SonicGeyserProjectile> rocks=new List<SonicGeyserProjectile>();
        float elapsed,readyIn,jetRemaining;bool erupting,soundPlayed;
        MaterialPropertyBlock jetProperties;
        public bool Erupting=>erupting;
        public int Eruptions {get;private set;}
        public int PendingCount=>plans.Count;
        public IReadOnlyList<SonicGeyserProjectile> Rocks=>rocks;
        public string LastRefusal {get;private set;}
        void OnValidate(){ventRadius=Mathf.Max(.25f,ventRadius);ventHeight=Mathf.Max(.1f,ventHeight);jetHeight=Mathf.Max(1,jetHeight);warningDelay=Mathf.Max(.2f,warningDelay);rockInterval=Mathf.Max(0,rockInterval);cooldown=Mathf.Max(0,cooldown);rockLifetime=Mathf.Max(0,rockLifetime);}
        void OnEnable(){RefreshVisual();SetJetStrength(0);}
        void Update(){RefreshVisual();if(Application.IsPlaying(gameObject))Tick(Time.deltaTime);}
        public void RefreshVisual()
        {
            if(vent!=null)vent.localScale=new Vector3(ventRadius,ventHeight,ventRadius);
            if(mouth!=null)mouth.localPosition=Vector3.up*(ventHeight*.85f);
            if(jetVisual!=null)jetVisual.transform.localScale=new Vector3(ventRadius*.7f,jetHeight,ventRadius*.7f);
        }
        public void SetJetStrength(float strength)
        {
            if(jetVisual==null)return;if(jetProperties==null)jetProperties=new MaterialPropertyBlock();jetProperties.SetFloat("_Strength",Mathf.Clamp01(strength));jetVisual.SetPropertyBlock(jetProperties);jetVisual.enabled=strength>0;
        }
        bool Covers(SonicLavaVolume volume)
        {
            if(volume==null || !volume.isActiveAndEnabled || volume.gameObject.scene!=gameObject.scene)return false;
            var p=volume.transform.InverseTransformPoint(transform.position);float distance=Mathf.Abs(volume.transform.TransformVector(Vector3.up*p.y).magnitude);
            return Mathf.Abs(p.x)<=volume.width*.5f && Mathf.Abs(p.z)<=volume.length*.5f && distance<=surfaceTolerance;
        }
        public bool FindLava()
        {
            if(Covers(lava))return true;lava=null;float best=float.PositiveInfinity;
            foreach(var volume in Object.FindObjectsByType<SonicLavaVolume>())
            {
                if(!Covers(volume))continue;float d=(volume.transform.position-transform.position).sqrMagnitude;
                if(d<best){best=d;lava=volume;}
            }
            return lava!=null;
        }
        // Can be selected directly from an existing engine trigger's UnityEvent.
        public void TriggerEruption(){TryErupt();}
        public bool TryErupt()
        {
            LastRefusal=null;
            if(!isActiveAndEnabled || erupting || readyIn>0){LastRefusal="Geyser en eruption ou en recharge.";return false;}
            if(warningMaterial==null || mouth==null){LastRefusal="References du prefab manquantes.";return false;}
            Physics.SyncTransforms();int index=0;
            foreach(var zone in landingZones)
            {
                if(zone==null || !zone.isActiveAndEnabled)continue;
                for(int i=0;i<Mathf.Clamp(zone.rockCount,1,64);i++)
                {
                    var prefab=zone.rockPrefab;if(prefab==null && rockPrefabs.Length>0)prefab=rockPrefabs[Random.Range(0,rockPrefabs.Length)];
                    if(prefab==null)continue;
                    Vector3 p=zone.SamplePoint(Random.insideUnitCircle);
                    if(!zone.ResolveGround(p,out var ground,out var normal))continue;
                    float scale=Random.Range(Mathf.Max(.1f,zone.minimumScale),Mathf.Max(zone.minimumScale,zone.maximumScale));
                    var mesh=prefab.GetComponentInChildren<MeshFilter>();float radius=scale;
                    if(mesh!=null && mesh.sharedMesh!=null){var ext=mesh.sharedMesh.bounds.extents;radius=new Vector2(ext.x*Mathf.Abs(prefab.transform.localScale.x),ext.z*Mathf.Abs(prefab.transform.localScale.z)).magnitude*scale;}
                    var warningGo=new GameObject("Impact annonce");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(warningGo,gameObject.scene);var warning=warningGo.AddComponent<SonicGeyserWarning>();warning.Initialise(zone,ground,normal,radius+warningMargin,warningMaterial);
                    plans.Add(new Plan{zone=zone,prefab=prefab,point=ground,normal=normal,scale=scale,due=warningDelay+index*rockInterval,warning=warning});index++;
                }
            }
            if(plans.Count==0){LastRefusal="Aucune cible valide : verifier les zones, les modeles et le sol sous les zones.";return false;}
            elapsed=0;erupting=true;soundPlayed=false;Eruptions++;onErupted.Invoke();return true;
        }
        public void Tick(float delta)
        {
            delta=Mathf.Max(0,delta);jetRemaining=Mathf.Max(0,jetRemaining-delta);SetJetStrength(Mathf.Min(1,jetRemaining*3));if(!erupting)readyIn=Mathf.Max(0,readyIn-delta);
            for(int i=rocks.Count-1;i>=0;i--)
            {
                var r=rocks[i];if(r==null){rocks.RemoveAt(i);continue;}r.Tick(delta);
                if(!r.gameObject.activeSelf){SonicGeyserProjectile.Dispose(r.gameObject);rocks.RemoveAt(i);}
            }
            if(!erupting)return;
            elapsed+=delta;
            for(int i=plans.Count-1;i>=0;i--)
            {
                var p=plans[i];if(elapsed<p.due)continue;
                if(p.zone==null || p.prefab==null){if(p.warning!=null)SonicGeyserProjectile.Dispose(p.warning.gameObject);plans.RemoveAt(i);continue;}
                var go=Instantiate(p.prefab);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,gameObject.scene);go.name="Roche volcanique projetee";go.transform.localScale*=p.scale;
                var r=go.AddComponent<SonicGeyserProjectile>();r.Initialise(mouth.position,p.point,p.normal,p.zone.flightTime,p.zone.arcHeight,rockLifetime,p.warning,playImpactSound?ResolvedImpactSound:null,impactVolume,audioSource!=null?audioSource.outputAudioMixerGroup:null);rocks.Add(r);r.Tick(Mathf.Max(0,elapsed-p.due));plans.RemoveAt(i);
                if(jet!=null){var main=jet.main;main.startSpeed=Mathf.Sqrt(2*24*jetHeight);jet.Play();}if(sparks!=null)sparks.Play();
                jetRemaining=1;SetJetStrength(1);
                if(!soundPlayed){if(audioSource!=null && eruptionSound!=null)audioSource.PlayOneShot(eruptionSound);soundPlayed=true;}
            }
            bool airborne=false;foreach(var r in rocks)if(r!=null && !r.Landed){airborne=true;break;}
            if(plans.Count==0 && !airborne){erupting=false;readyIn=cooldown;}
        }
        void OnDisable()
        {
            foreach(var p in plans)if(p.warning!=null)SonicGeyserProjectile.Dispose(p.warning.gameObject);plans.Clear();
            foreach(var r in rocks)if(r!=null)SonicGeyserProjectile.Dispose(r.gameObject);rocks.Clear();erupting=false;readyIn=0;
            if(jet!=null)jet.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);if(sparks!=null)sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            SetJetStrength(0);
        }
    }
}
