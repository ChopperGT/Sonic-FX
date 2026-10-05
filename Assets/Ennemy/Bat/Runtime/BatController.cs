using System.Collections.Generic;
using UnityEngine;
namespace SonicFX.Bat
{
    [DisallowMultipleComponent]
    public class BatController : MonoBehaviour
    {
        public enum Behaviour { Sleeping, Watching, Pursuing, Attached, Escaping }
        [Header("Depart et plafond")]
        [InspectorName("Endormie au depart")] public bool startsAsleep=true;
        [InspectorName("Accrochage automatique au plafond")] public bool snapToCeiling=true;
        [Min(.5f),InspectorName("Recherche du plafond")] public float ceilingSearch=20;
        public LayerMask environmentLayers=1;
        [Header("Zone surveillee relative au point de depart")]
        public Vector3 watchCenter=new Vector3(0,-4,0), watchSize=new Vector3(16,10,16);
        [Min(0),InspectorName("Temps de reaction")] public float reactionTime=.3f;
        [Min(1),InspectorName("Rayon du hurlement")] public float hearingRadius=18;
        public AudioClip shriek;
        [Range(0,1)] public float shriekVolume=.8f;
        [Header("Vol et accrochage")]
        [Min(.1f)] public float flightSpeed=16, fleeSpeed=12, attachDistance=1;
        [Min(0),InspectorName("Vitesse perdue par chauve-souris")] public float speedPenalty=5;
        [Min(.1f),InspectorName("Delai avant de pouvoir raccrocher")] public float escapeGrace=3;
        [Range(0,1),InspectorName("Chance de rester alerte apres l'eau")] public float waterAlertChance=.3f;
        [Header("Se liberer et degats")]
        [Min(1),InspectorName("Appuis pour la premiere")] public int baseRollPresses=8;
        [Min(0),InspectorName("Appuis supplementaires par chauve-souris")] public int extraRollPresses=4;
        [Range(0,1),InspectorName("Perte de progression par seconde")] public float escapeDecay=.12f;
        [Range(0,1),InspectorName("Chance de destruction par ennemi")] public float enemyDestroyChance=.7f;
        [Min(1),InspectorName("Nombre avant degats")] public int damageThreshold=5;
        [Min(.1f),InspectorName("Secondes avant degats")] public float damageDelay=10;
        [Header("References deja remplies")]
        public PlayerBhysics player;
        public BatVisual visual;
        public Collider contact;
        public GameObject homingTarget;
        public Behaviour State {get;private set;}
        public Vector3 Perch {get;private set;}
        public SonicBatAttachment Carrier {get;private set;}
        public int CallsEmitted {get;private set;}
        public static readonly HashSet<BatController> Active=new HashSet<BatController>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry(){Active.Clear();}
        Quaternion perchRotation;
        Vector3 initialPerch,escapeDestination;
        float reaction,grace,searchTimer,escapeTime;
        bool initialized,returnAlert;
        AudioSource voice;
        readonly RaycastHit[] hits=new RaycastHit[64];
        void OnEnable(){Active.Add(this);if(initialized)Initialize();}
        void OnDisable(){Active.Remove(this);if(Carrier!=null)Carrier.Remove(this);Carrier=null;}
        void Start(){Initialize();}
        public void Initialize()
        {
            if(Carrier!=null)Carrier.Remove(this);Carrier=null;
            initialPerch=Perch=transform.position;perchRotation=transform.rotation;
            if(snapToCeiling && FindCeiling(Perch,out var point))Perch=initialPerch=point;
            transform.position=Perch;State=startsAsleep?Behaviour.Sleeping:Behaviour.Watching;
            reaction=grace=searchTimer=0;initialized=true;SetContact(true);
            if(visual!=null)visual.Pose(State,0);
        }
        bool Alive(PlayerBhysics p)
        {
            if(p==null || !p.gameObject.activeInHierarchy || p.gameObject.scene!=gameObject.scene)return false;
            var hurt=p.GetComponent<HurtControl>();return hurt==null || !hurt.isDead;
        }
        void FixedUpdate(){Tick(Time.fixedDeltaTime);}
        public void Tick(float dt)
        {
            if(!initialized)Initialize();grace=Mathf.Max(0,grace-dt);
            if(State==Behaviour.Attached)return;
            if(State==Behaviour.Escaping)
            {
                escapeTime+=dt;Fly(escapeDestination,fleeSpeed,dt);
                if((transform.position-escapeDestination).sqrMagnitude<.12f)
                {
                    Perch=escapeDestination;perchRotation=Quaternion.Euler(0,transform.eulerAngles.y,0);
                    State=returnAlert?Behaviour.Watching:Behaviour.Sleeping;reaction=0;SetContact(true);
                }
                else if(escapeTime>4){ChooseEscapePerch();escapeTime=0;}
                return;
            }
            if(!Alive(player))
            {
                player=null;searchTimer-=dt;
                if(searchTimer<=0){searchTimer=.5f;foreach(var p in Object.FindObjectsByType<PlayerBhysics>())if(Alive(p)){player=p;break;}}
            }
            if(State==Behaviour.Pursuing)
            {
                if(!Alive(player)){Escape(false);return;}
                if(SonicBatAttachment.IsWet(player,player.transform.position)){Escape(true);return;}
                Vector3 target=player.transform.position+player.transform.up*.65f;Fly(target,flightSpeed,dt);
                if(grace<=0 && Vector3.Distance(transform.position,target)<=attachDistance && ClearLine(transform.position,target))Attach(player);
                return;
            }
            transform.SetPositionAndRotation(Perch,perchRotation);
            if(grace>0 || !Alive(player) || SonicBatAttachment.IsWet(player,player.transform.position) || !CanSee(player)){reaction=0;return;}
            reaction+=dt;if(reaction>=reactionTime)Wake(player,true);
        }
        public bool CanSee(PlayerBhysics target)
        {
            if(!Alive(target))return false;
            Vector3 eye=target.transform.position+target.transform.up*.65f;
            Vector3 local=Quaternion.Inverse(perchRotation)*(eye-Perch)-watchCenter,half=watchSize*.5f;
            return Mathf.Abs(local.x)<=half.x && Mathf.Abs(local.y)<=half.y && Mathf.Abs(local.z)<=half.z && ClearLine(transform.position,eye);
        }
        bool ClearLine(Vector3 from,Vector3 to)
        {
            float distance=Vector3.Distance(from,to);if(distance<.01f)return true;
            int count=gameObject.scene.GetPhysicsScene().Raycast(from,(to-from)/distance,hits,distance,environmentLayers,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)if(!hits[i].transform.IsChildOf(transform) && hits[i].collider.GetComponentInParent<PlayerBhysics>()==null)return false;
            return true;
        }
        public void Wake(PlayerBhysics target,bool call)
        {
            if(!initialized)Initialize();
            if(State==Behaviour.Attached || State==Behaviour.Escaping || grace>0 || !Alive(target))return;
            player=target;State=Behaviour.Pursuing;if(!call)return;CallsEmitted++;
            if(shriek!=null && Application.isPlaying)
            {
                if(voice==null){voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=.7f;voice.maxDistance=hearingRadius;voice.rolloffMode=AudioRolloffMode.Linear;}
                voice.PlayOneShot(shriek,shriekVolume);
            }
            foreach(var other in Active)
                if(other!=null && other!=this && other.gameObject.scene==gameObject.scene && (other.State==Behaviour.Sleeping || other.State==Behaviour.Watching) && Vector3.Distance(transform.position,other.transform.position)<=hearingRadius)other.Wake(target,false);
        }
        public bool Attach(PlayerBhysics target)
        {
            if(!initialized)Initialize();
            if(State==Behaviour.Attached || State==Behaviour.Escaping || grace>0 || !Alive(target) || SonicBatAttachment.IsWet(target,target.transform.position))return false;
            var actions=target.GetComponent<ActionManager>();
            if(target.isRolling || (actions!=null && !actions.BallBlocked && (actions.Action==2 || actions.Action==3 || actions.Action==6 || actions.Action==8 || (actions.Action==1 && actions.Action01!=null && actions.Action01.JumpBall!=null && actions.Action01.JumpBall.activeSelf))))return false;
            var status=target.GetComponent<SonicBatAttachment>();if(status==null)status=target.gameObject.AddComponent<SonicBatAttachment>();
            player=target;Carrier=status;State=Behaviour.Attached;SetContact(false);status.Add(this);return true;
        }
        void SetContact(bool active){if(contact!=null)contact.enabled=active;if(homingTarget!=null)homingTarget.SetActive(active);}
        public void Escape(bool water)
        {
            if(Carrier!=null)Carrier.Remove(this);Carrier=null;State=Behaviour.Escaping;SetContact(false);grace=escapeGrace;escapeTime=0;
            returnAlert=water?Random.value<waterAlertChance:false;ChooseEscapePerch();
        }
        void ChooseEscapePerch()
        {
            for(int i=0;i<16;i++)
            {
                float angle=(i*137.5f+transform.position.GetHashCode()%360)*Mathf.Deg2Rad;
                Vector3 origin=transform.position+new Vector3(Mathf.Cos(angle)*(3+i*.5f),1,Mathf.Sin(angle)*(3+i*.5f));
                if(FindCeiling(origin,out var perch) && !SonicBatAttachment.IsWetPosition(perch,perch,gameObject.scene)){escapeDestination=perch;return;}
            }
            escapeDestination=initialPerch;
        }
        public bool FindCeiling(Vector3 origin,out Vector3 point)
        {
            point=origin;float nearest=float.PositiveInfinity;
            int count=gameObject.scene.GetPhysicsScene().Raycast(origin,Vector3.up,hits,ceilingSearch,environmentLayers,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var h=hits[i];if(h.transform.IsChildOf(transform) || h.collider.GetComponentInParent<PlayerBhysics>()!=null || h.normal.y>-.45f || h.distance>=nearest)continue;
                nearest=h.distance;point=h.point+Vector3.down*.85f;
            }
            return !float.IsPositiveInfinity(nearest);
        }
        void Fly(Vector3 target,float speed,float dt)
        {
            Vector3 delta=target-transform.position;float step=Mathf.Min(delta.magnitude,speed*dt);if(step<.001f)return;
            Vector3 dir=delta.normalized;
            if(gameObject.scene.GetPhysicsScene().SphereCast(transform.position,.28f,dir,out var hit,step+.15f,environmentLayers,QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<PlayerBhysics>()==null)
            {
                Vector3 slide=Vector3.ProjectOnPlane(dir,hit.normal)+Vector3.up*.75f;
                if(slide.sqrMagnitude>.01f && ClearLine(transform.position,transform.position+slide.normalized*(step+.3f)))dir=slide.normalized;else return;
            }
            transform.position+=dir*step;
            transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(dir,Vector3.up),1-Mathf.Exp(-10*dt));
        }
        void LateUpdate(){if(State==Behaviour.Attached && Carrier!=null)Carrier.PositionBat(this);if(visual!=null)visual.Pose(State,Time.time);}
        void OnDrawGizmosSelected()
        {
            Gizmos.matrix=Matrix4x4.TRS(initialized?Perch:transform.position,initialized?perchRotation:transform.rotation,Vector3.one);
            Gizmos.color=new Color(1,.6f,.08f);Gizmos.DrawWireCube(watchCenter,watchSize);
            Gizmos.matrix=Matrix4x4.identity;Gizmos.color=Color.cyan;Gizmos.DrawWireSphere(transform.position,hearingRadius);
            Gizmos.color=Color.white;Gizmos.DrawLine(transform.position,transform.position+Vector3.up*ceilingSearch);
        }
    }
}
