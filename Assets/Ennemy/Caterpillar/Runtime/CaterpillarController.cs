using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
namespace SonicFX.Caterpillar
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public class CaterpillarController : MonoBehaviour
    {
        public enum Behaviour { Patrol, Retracting, Charging, Recovering, Defeated }
        public enum ContactResult { None, HurtSonic, Defeated }
        [Header("Longueur de la chenille")]
        [Range(1,32),Tooltip("Nombre de boules du corps, sans compter la tete.")] public int bodySegmentCount=3;
        [HideInInspector] public Transform[] segmentPool;
        [Header("Patrouille")]
        public Transform[] patrolPoints = new Transform[0];
        [Min(0), InspectorName("Distance sans points")] public float patrolDistance = 4;
        [Min(0), InspectorName("Vitesse de patrouille")] public float patrolSpeed = 1.4f;
        [Min(0), InspectorName("Pause aux extremites")] public float pauseDuration = 1;
        [Min(1), InspectorName("Vitesse de rotation")] public float turnSpeed = 170;
        [Header("Detection")]
        public PlayerBhysics player;
        [Min(1), InspectorName("Distance de vision")] public float viewDistance = 20;
        [Range(20,360), InspectorName("Angle de vision")] public float viewAngle = 150;
        [Min(0), InspectorName("Temps de reaction")] public float reactionTime = .25f;
        [Tooltip("Terrain et murs. Exclure Player, Enemies et EnemyTrigger.")] public LayerMask environmentLayers = 1;
        [Header("Retraction et charge")]
        [Min(.1f), InspectorName("Temps de retraction")] public float retractDuration = .7f;
        [Range(.2f,.9f), InspectorName("Compression du corps")] public float compressedSpacing = .4f;
        [Min(.1f), InspectorName("Vitesse de charge")] public float chargeSpeed = 24;
        [Min(.5f), InspectorName("Distance maximale de charge")] public float chargeDistance = 15;
        [Min(0), InspectorName("Recuperation apres la charge")] public float recoveryDuration = 1.2f;
        [Min(0), InspectorName("Delai entre les attaques")] public float attackCooldown = 2.5f;
        [Header("Zones vulnerables")]
        [Range(10,70), InspectorName("Angle vulnerable devant et derriere")] public float weakSpotAngle = 45;
        [Min(.1f), InspectorName("Delai entre les degats a Sonic")] public float damageInterval = 1.5f;
        [Header("Sol")]
        [Min(.1f)] public float maximumDrop = .7f;
        [Range(5,65)] public float maximumSlope = 45;
        [Header("References configurees")]
        public CaterpillarVisual visual;
        public Transform[] parts;
        public SphereCollider[] contactColliders;
        public Transform frontTarget, rearTarget;
        public Behaviour State { get; private set; }
        public float Retraction { get; private set; }
        public float MoveRate { get; private set; }
        public int ChargesStarted { get; private set; }
        public int DefeatEvents { get; private set; }
        public int DamageEvents { get; private set; }
        public Vector3 ChargeDirection => chargeDirection;
        public string Status => status;
        public float Size => Mathf.Max(.1f,Mathf.Max(Mathf.Abs(transform.lossyScale.x),Mathf.Abs(transform.lossyScale.z)));
        Rigidbody body; bool initialized; Vector3 home, patrolAxis, destination, chargeDirection;
        float timer, cooldown, seenTime, searchAt, charged, phase, nextDamage;
        int waypoint, waypointDirection=1; bool towardEnd=true; string status;
        readonly RaycastHit[] hits = new RaycastHit[64]; readonly Collider[] overlaps = new Collider[64];
        Vector3[] lastCenters;
        struct Touch { public PlayerBhysics player; public int part; public Vector3 center; }
        readonly List<Touch> touches=new List<Touch>();
        PhysicsScene World => gameObject.scene.GetPhysicsScene();
        public Vector3 Eye => transform.position + Vector3.up * (1.35f*Size);
        void Start() { Initialize(); }
        void OnEnable() { initialized=false; }
        public void ApplyBodySize(bool recordUndo=false)
        {
            bodySegmentCount=Mathf.Clamp(bodySegmentCount,1,32);
            if(visual==null)visual=GetComponent<CaterpillarVisual>();
            var source=segmentPool!=null && segmentPool.Length>=2?segmentPool:(parts!=null && parts.Length>=2?parts:visual!=null?visual.segments:null);
            if(source==null || source.Length<2 || source[0]==null || source[1]==null)return;
            RecordSize(this,recordUndo);RecordSize(visual,recordUndo);
            var pool=new List<Transform>();foreach(var part in source)if(part!=null && !pool.Contains(part))pool.Add(part);
            if(pool.Count<2)return;
            while(pool.Count<bodySegmentCount+1)
            {
                var clone=Instantiate(pool[1].gameObject,transform,false);clone.name="Segment_"+pool.Count;
#if UNITY_EDITOR
                if(recordUndo && !Application.IsPlaying(gameObject))UnityEditor.Undo.RegisterCreatedObjectUndo(clone,"Ajouter une boule a la chenille");
#endif
                pool.Add(clone.transform);
            }
            segmentPool=pool.ToArray();parts=new Transform[bodySegmentCount+1];contactColliders=new SphereCollider[parts.Length];
            for(int i=0;i<pool.Count;i++)
            {
                var part=pool[i];bool active=i<parts.Length;RecordSize(part.gameObject,recordUndo);RecordSize(part,recordUndo);
                part.gameObject.SetActive(active);var contact=part.GetComponent<SphereCollider>();
                if(active)
                {
                    if(contact==null)contact=part.gameObject.AddComponent<SphereCollider>();RecordSize(contact,recordUndo);
                    contact.isTrigger=true;contact.enabled=State!=Behaviour.Defeated;parts[i]=part;contactColliders[i]=contact;
                }
                else if(contact!=null){RecordSize(contact,recordUndo);contact.enabled=false;}
#if UNITY_EDITOR
                if(!Application.IsPlaying(gameObject)){UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(part.gameObject);if(contact!=null)UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(contact);}
#endif
            }
            if(visual!=null)visual.segments=parts;
            RecordSize(frontTarget,recordUndo);RecordSize(rearTarget,recordUndo);
            Pose(0);lastCenters=new Vector3[parts.Length];for(int i=0;i<parts.Length;i++)lastCenters[i]=parts[i].position;
#if UNITY_EDITOR
            if(!Application.IsPlaying(gameObject))
            {
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);if(visual!=null)UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
                foreach(var part in parts)UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(part);
                if(frontTarget!=null)UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(frontTarget);if(rearTarget!=null)UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(rearTarget);
            }
#endif
        }
        static void RecordSize(Object obj,bool recordUndo)
        {
#if UNITY_EDITOR
            if(recordUndo && obj!=null && !Application.IsPlaying(obj))UnityEditor.Undo.RecordObject(obj,"Modifier la longueur de la chenille");
#endif
        }
        public void Initialize()
        {
            body=GetComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;body.interpolation=RigidbodyInterpolation.None;
            home=transform.position;patrolAxis=Flat(transform.forward).normalized;waypoint=0;waypointDirection=1;towardEnd=true;
            destination=PatrolDestination();State=Behaviour.Patrol;timer=0;cooldown=seenTime=charged=nextDamage=0;Retraction=MoveRate=0;phase=0;
            if(visual==null)visual=GetComponent<CaterpillarVisual>();
            ApplyBodySize();
            lastCenters=new Vector3[parts.Length];Pose(0);for(int i=0;i<parts.Length;i++)lastCenters[i]=parts[i].position;
            initialized=true;
        }
        Vector3 PatrolDestination()
        {
            if(patrolPoints!=null && patrolPoints.Length>0 && patrolPoints[Mathf.Clamp(waypoint,0,patrolPoints.Length-1)]!=null)return patrolPoints[waypoint].position;
            return home+patrolAxis*(towardEnd?patrolDistance:-patrolDistance);
        }
        void NextPatrolPoint()
        {
            if(patrolPoints!=null && patrolPoints.Length>1){waypoint+=waypointDirection;if(waypoint>=patrolPoints.Length){waypointDirection=-1;waypoint=patrolPoints.Length-2;}else if(waypoint<0){waypointDirection=1;waypoint=1;}}
            else towardEnd=!towardEnd;
            destination=PatrolDestination();timer=pauseDuration;
        }
        bool PlayerReady()
        {
            if((player==null || !player.gameObject.activeInHierarchy) && Time.time>=searchAt)
            {
                searchAt=Time.time+1;foreach(var candidate in FindObjectsByType<PlayerBhysics>())
                    if(candidate.gameObject.scene.GetPhysicsScene()==World && candidate.gameObject.activeInHierarchy){player=candidate;break;}
            }
            var hurt=player!=null?player.GetComponent<HurtControl>():null;
            return player!=null && player.gameObject.activeInHierarchy && (hurt==null || !hurt.isDead);
        }
        public bool CanSee(Vector3 target)
        {
            Vector3 offset=target-Eye;if(offset.magnitude>viewDistance*Size)return false;
            if(offset.sqrMagnitude>.001f && Vector3.Angle(transform.forward,offset)>viewAngle*.5f)return false;
            int count=World.Raycast(Eye,offset.normalized,hits,offset.magnitude,environmentLayers,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)if(!hits[i].transform.IsChildOf(transform) && hits[i].collider.GetComponentInParent<PlayerBhysics>()==null)return false;
            return true;
        }
        void FixedUpdate(){Tick(Time.fixedDeltaTime);}
        public void Tick(float dt)
        {
            if(!initialized)Initialize();if(State==Behaviour.Defeated || dt<=0)return;
            if(parts.Length!=Mathf.Clamp(bodySegmentCount,1,32)+1)ApplyBodySize();
            cooldown=Mathf.Max(0,cooldown-dt);MoveRate=0;
            bool ready=PlayerReady();Vector3 target=ready?player.transform.position+Vector3.up*(.65f*Size):Eye;
            if(State==Behaviour.Patrol)
            {
                seenTime=ready && CanSee(target)?seenTime+dt:0;
                if(seenTime>=reactionTime && ready && CanSee(target) && cooldown<=0){State=Behaviour.Retracting;timer=0;status="Retraction";}
                else Patrol(dt);
            }
            if(State==Behaviour.Retracting)
            {
                timer+=dt;Retraction=Mathf.SmoothStep(0,1,Mathf.Clamp01(timer/retractDuration));
                if(!ready){State=Behaviour.Recovering;timer=0;}
                else
                {
                    Face(Flat(target-transform.position),dt);
                    if(timer>=retractDuration){chargeDirection=Flat(target-transform.position).normalized;if(chargeDirection.sqrMagnitude<.1f)chargeDirection=transform.forward;SetRotation(Quaternion.LookRotation(chargeDirection));State=Behaviour.Charging;timer=charged=0;ChargesStarted++;status="Charge";}
                }
            }
            else if(State==Behaviour.Charging)
            {
                timer+=dt;Retraction=Mathf.Max(0,1-timer/.18f);
                float travel=Mathf.Min(chargeSpeed*Size*dt,chargeDistance*Size-charged);
                if(travel<=0 || !MoveSafely(chargeDirection*travel,true)){State=Behaviour.Recovering;timer=0;cooldown=attackCooldown;status="Recuperation : obstacle, bord ou fin de charge";}
                else{charged+=travel;MoveRate=chargeSpeed;}
            }
            else if(State==Behaviour.Recovering)
            {
                timer+=dt;Retraction=Mathf.MoveTowards(Retraction,0,dt*3);
                if(timer>=recoveryDuration){State=Behaviour.Patrol;timer=pauseDuration;seenTime=0;destination=PatrolDestination();}
            }
            phase+=dt*(MoveRate>0?2.5f+MoveRate*.8f:1);Pose(dt);
            if(Application.isPlaying)CheckContacts();
            for(int i=0;i<parts.Length;i++)lastCenters[i]=parts[i].position;
        }
        void Patrol(float dt)
        {
            status="Patrouille";Retraction=Mathf.MoveTowards(Retraction,0,dt*3);
            if(timer>0){timer-=dt;return;}
            Vector3 delta=Flat(destination-transform.position);
            if(delta.magnitude<.25f*Size){NextPatrolPoint();return;}
            Face(delta,dt);
            Vector3 direction=Flat(transform.forward).normalized;
            if(Vector3.Angle(direction,delta)>35)return;
            float step=Mathf.Min(delta.magnitude,patrolSpeed*Size*dt)*(.65f+.35f*Mathf.Sin(phase)*Mathf.Sin(phase));
            if(!MoveSafely(direction*step,false)){NextPatrolPoint();return;}MoveRate=patrolSpeed;
        }
        void Face(Vector3 direction,float dt){if(direction.sqrMagnitude>.001f)SetRotation(Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),turnSpeed*dt));}
        void SetRotation(Quaternion q){body.rotation=q;transform.rotation=q;}
        bool Ground(Vector3 point,out RaycastHit hit)
        {
            if(!World.Raycast(point+Vector3.up*(.5f*Size),Vector3.down,out hit,(.5f+maximumDrop)*Size,environmentLayers,QueryTriggerInteraction.Ignore))return false;
            return hit.normal.y>=Mathf.Cos(maximumSlope*Mathf.Deg2Rad);
        }
        public bool MoveSafely(Vector3 delta,bool charge)
        {
            if(delta.sqrMagnitude<.000001f)return false;
            // Sample the path at intervals, so a fast charge cannot cross a thin wall or skip a gap.
            int steps=Mathf.Max(1,Mathf.CeilToInt(delta.magnitude/(.3f*Size)));Vector3 next=transform.position;
            for(int i=1;i<=steps;i++)
            {
                Vector3 candidate=transform.position+delta*(i/(float)steps);
                if(!Ground(candidate,out var ground))return false;
                Vector3 from=next+Vector3.up*(.8f*Size);Vector3 motion=candidate-next;
                if(World.SphereCast(from,.42f*Size,motion.normalized,out var obstruction,motion.magnitude,environmentLayers,QueryTriggerInteraction.Ignore) && !obstruction.transform.IsChildOf(transform) && obstruction.normal.y<.6f)return false;
                next=new Vector3(candidate.x,ground.point.y+.025f*Size,candidate.z);
            }
            body.position=next;transform.position=next;return true;
        }
        void Pose(float dt)
        {
            if(visual!=null)visual.Pose(State,Retraction,phase,MoveRate,compressedSpacing);
            if(frontTarget!=null && parts.Length>0)frontTarget.position=parts[0].position+transform.forward*(.48f*Size);
            if(rearTarget!=null && parts.Length>1)rearTarget.position=parts[parts.Length-1].position-transform.forward*(.4f*Size);
        }
        public bool Vulnerable(int partIndex,Vector3 attackCenter)
        {
            if(partIndex<0 || partIndex>=parts.Length)return false;
            if(partIndex!=0 && partIndex!=parts.Length-1)return false;
            Vector3 axis=partIndex==0?transform.forward:-transform.forward;
            Vector3 offset=attackCenter-parts[partIndex].position;
            // Full 3D angle prevents attacks from above from counting as head/tail hits.
            return offset.sqrMagnitude>.0001f && Vector3.Angle(axis,offset)<=weakSpotAngle;
        }
        public static bool Attacking(PlayerBhysics attacker)
        {
            var actions=attacker!=null?attacker.GetComponent<ActionManager>():null;if(actions==null || actions.BallBlocked || actions.Action==4)return false;
            return attacker.isRolling || actions.Action==3 || actions.Action==6 || actions.Action==8 || (actions.Action==2 && actions.Action02!=null && !actions.Action02.IsAirDash) || (actions.Action==1 && actions.Action01!=null && actions.Action01.JumpBall!=null && actions.Action01.JumpBall.activeSelf);
        }
        public ContactResult ResolveContact(PlayerBhysics attacker,int partIndex,Vector3 attackCenter,float now)
        {
            if(State==Behaviour.Defeated || attacker==null)return ContactResult.None;
            var hurt=attacker.GetComponent<HurtControl>();if(hurt!=null && hurt.isDead)return ContactResult.None;
            if(Attacking(attacker) && Vulnerable(partIndex,attackCenter))
            {
                State=Behaviour.Defeated;DefeatEvents++;foreach(var c in contactColliders)if(c!=null)c.enabled=false;
                if(frontTarget!=null)frontTarget.gameObject.SetActive(false);if(rearTarget!=null)rearTarget.gameObject.SetActive(false);
                var actions=attacker.GetComponent<ActionManager>();
                if(attacker.p_rigidbody!=null && !attacker.isRolling){Vector3 v=attacker.p_rigidbody.linearVelocity;v.y=Mathf.Max(6,v.y);attacker.p_rigidbody.linearVelocity=v;}
                var interaction=attacker.GetComponent<Objects_Interaction>();if(interaction!=null)interaction.updateTargets=true;
                if(Application.isPlaying){var health=GetComponent<EnemyHealth>();if(health!=null)health.DealDamage(Mathf.Max(1,health.MaxHealth));else Destroy(gameObject);}
                return ContactResult.Defeated;
            }
            if(now<nextDamage || hurt==null || hurt.IsHurt || hurt.IsInvencible)return ContactResult.None;
            var damage=attacker.GetComponent<Objects_Interaction>();if(damage==null || damage.Actions==null || damage.Actions.Action==4)return ContactResult.None;
            nextDamage=now+damageInterval;damage.DamagePlayer();DamageEvents++;HedgeCamera.Shakeforce=damage.EnemyDamageShakeAmmount;return ContactResult.HurtSonic;
        }
        void CheckContacts()
        {
            // Triggers remain untagged: generic Enemy hits must never bypass armour.
            touches.Clear();
            for(int i=0;i<parts.Length;i++)
            {
                float radius=contactColliders[i].radius*Size;Vector3 center=parts[i].position;
                int count=World.OverlapSphere(center,radius,overlaps,~0,QueryTriggerInteraction.Ignore);
                for(int j=0;j<count;j++)
                {
                    var c=overlaps[j];var attacker=c.GetComponentInParent<PlayerBhysics>();if(attacker==null)continue;
                    touches.Add(new Touch { player=attacker,part=i,center=c.bounds.center });
                }
                Vector3 motion=center-lastCenters[i];if(motion.sqrMagnitude<.000001f)continue;
                count=World.SphereCast(lastCenters[i],radius,motion.normalized,hits,motion.magnitude,~0,QueryTriggerInteraction.Ignore);
                for(int j=0;j<count;j++)
                {
                    var c=hits[j].collider;var attacker=c.GetComponentInParent<PlayerBhysics>();if(attacker==null)continue;
                    // Evaluate the side struck at impact, before a fast charge crosses Sonic.
                    Vector3 impactCenter=lastCenters[i]+motion.normalized*hits[j].distance;
                    touches.Add(new Touch { player=attacker,part=i,center=center+(c.bounds.center-impactCenter) });
                }
            }
            // A character has several physical colliders. Prefer a valid end hit over an
            // overlapping body contact, independently of the order returned by physics.
            foreach(var touch in touches)if(Attacking(touch.player) && Vulnerable(touch.part,touch.center))
                if(ResolveContact(touch.player,touch.part,touch.center,Time.time)==ContactResult.Defeated)return;
            foreach(var touch in touches)ResolveContact(touch.player,touch.part,touch.center,Time.time);
        }
        static Vector3 Flat(Vector3 p){p.y=0;return p;}
        void OnDrawGizmosSelected()
        {
            Gizmos.color=Color.yellow;Gizmos.DrawWireSphere(Eye,viewDistance*Size);
            Gizmos.DrawRay(Eye,Quaternion.AngleAxis(viewAngle*.5f,Vector3.up)*transform.forward*viewDistance*Size);Gizmos.DrawRay(Eye,Quaternion.AngleAxis(-viewAngle*.5f,Vector3.up)*transform.forward*viewDistance*Size);
            Gizmos.color=Color.red;Gizmos.DrawRay(transform.position,transform.forward*chargeDistance*Size);
            if(parts==null || parts.Length<2)return;Gizmos.color=Color.green;
            Gizmos.DrawRay(parts[0].position,transform.forward*Size);Gizmos.DrawRay(parts[parts.Length-1].position,-transform.forward*Size);
        }
    }
}
