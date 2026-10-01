using UnityEngine;
using System.Collections.Generic;
using SonicFX.Water;

namespace SonicFX.Piranha
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public class PiranhaController : MonoBehaviour
    {
        [Header("Eau : detection automatique si vide")]
        public SonicWaterVolume water;
        public PlayerBhysics player;
        [Header("Patrouille autour du point de depart")]
        [Min(0)] public float patrolDistance = 4;
        [Min(.1f)] public float swimSpeed = 2;
        [Min(0)] public float pauseSeconds = 1;
        [Min(0), Tooltip("Variation verticale de la patrouille autour du point de depart.")] public float patrolVerticalRange = 1.5f;
        [Min(.1f)] public float chaseSpeed = 4;
        [Min(.5f), Tooltip("S'approche de Sonic avant de preparer la morsure.")] public float biteStartDistance = 3.5f;
        [Min(1)] public float loseDistance = 25;
        [Header("Detection et morsure sous l'eau")]
        [Min(1)] public float noticeDistance = 18;
        [Min(.1f)] public float chargeSpeed = 10;
        [Min(.05f)] public float reactionTime = .35f;
        [Min(.1f)] public float chargeDuration = 1;
        [Min(.1f)] public float attackCooldown = 1.5f;
        [Min(1)] public float turnSpeed = 240;
        [Header("Saut en cloche")]
        [Tooltip("Hauteur maximale du centre du poisson au-dessus de la surface, en unites du monde.")]
        [Min(1), InspectorName("Hauteur maximale du saut (Y)")] public float jumpHeight = 20;
        [Min(1)] public float jumpGravity = 20;
        [Tooltip("Distance maximale entre depart et retombee, y compris la difference de hauteur.")]
        [Min(1), InspectorName("Portee maximale du saut")] public float maximumJumpDistance = 40;
        [Min(2), Tooltip("Rayon de recherche dans lequel le poisson cherche une sortie libre autour de Sonic.")]
        public float launchSearchRadius = 24;
        [Tooltip("Autorise un demi-tour au sommet du saut pour retomber dans l'eau du depart si le pont est trop large.")]
        public bool allowReturnJump = true;
        [Tooltip("Largeur maximale de terrain sec franchissable entre deux zones d'eau.")]
        [Min(0)] public float maximumDryGap = 3;
        [Header("Obstacles et taille")]
        [Tooltip("Couches du decor : terrain, murs, plateformes. Exclure Player et Enemies.")]
        public LayerMask environmentLayers = 1;
        [Min(.1f)] public float bodyRadius = 1.4f;
        public Animator animator;
        [Header("Diagnostic en Play")]
        [SerializeField] string status;
        [SerializeField] float distanceToSonic;
        enum Mode { Patrol, PrepareCharge, Charge, ApproachSurface, Leap, Retreat, Chase }
        Mode mode;
        Rigidbody body;
        Vector3 home, patrolTarget, chargeDirection, launch, landing, velocity, stagingPoint, plannedTarget;
        SonicWaterVolume landingWater;
        float pause, nextAttack, nextSearch, modeAge, flightTime, flightDuration;
        bool hasPatrolTarget, hasStagingPoint;
        float nextPlan;
        readonly List<Vector3> approachRoute = new List<Vector3>();
        int approachIndex;
        string planningFailure;
        bool returnJump;
        IEnumerator<bool> launchSearch;
        int lastPlanFrame=-1;
        struct LeapPlan { public Vector3 start, end, velocity; public float duration; public SonicWaterVolume destination; public bool returns; }
        float Radius => Mathf.Max(.1f, bodyRadius) * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Max(Mathf.Abs(transform.lossyScale.y), Mathf.Abs(transform.lossyScale.z)));
        Vector3 Aim => player.transform.position + Vector3.up * .65f;

        void Awake()
        {
            body = GetComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            home = transform.position;
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }
        public static bool Fits(SonicWaterVolume zone, Vector3 position, float radius)
        {
            if (zone == null || !zone.isActiveAndEnabled) return false;
            Vector3 scale = zone.transform.lossyScale;
            if (Mathf.Abs(scale.x) < .0001f || Mathf.Abs(scale.y) < .0001f || Mathf.Abs(scale.z) < .0001f) return false;
            Vector3 p = zone.transform.InverseTransformPoint(position);
            Vector3 r = new Vector3(radius / Mathf.Abs(scale.x), radius / Mathf.Abs(scale.y), radius / Mathf.Abs(scale.z));
            return Mathf.Abs(p.x) + r.x <= zone.width * .5f && Mathf.Abs(p.z) + r.z <= zone.length * .5f
                && p.y + r.y <= 0 && p.y - r.y >= -zone.depth;
        }
        static SonicWaterVolume ZoneAt(Vector3 point, float radius)
        {
            foreach (var zone in SonicWaterVolume.Active) if (Fits(zone, point, radius)) return zone;
            return null;
        }
        // Surface intersection is only meaningful for the horizontal water used by this project.
        static bool Horizontal(SonicWaterVolume zone) => zone != null && Vector3.Dot(zone.transform.up, Vector3.up) > .999f;
        bool Available()
        {
            if (player == null && Time.time >= nextSearch)
            {
                nextSearch = Time.time + 1;
                foreach (var candidate in Object.FindObjectsByType<PlayerBhysics>())
                    if (candidate.isActiveAndEnabled) { player = candidate; break; }
            }
            if (player == null || !player.isActiveAndEnabled) return false;
            var hurt = player.GetComponent<HurtControl>();
            return hurt == null || !hurt.isDead;
        }
        bool IsObstacle(Collider col) => col != null && !col.transform.IsChildOf(transform) && col.GetComponentInParent<PlayerBhysics>() == null;
        bool Clear(Vector3 from, Vector3 to)
        {
            float radius = Radius;
            foreach (var col in Physics.OverlapSphere(to, radius, environmentLayers, QueryTriggerInteraction.Ignore))
                if (IsObstacle(col)) return false;
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < .000001f) return true;
            foreach (var hit in Physics.SphereCastAll(from, radius, delta.normalized, delta.magnitude, environmentLayers, QueryTriggerInteraction.Ignore))
                if (IsObstacle(hit.collider)) return false;
            return true;
        }
        bool Sees(Vector3 point)
        {
            foreach (var hit in Physics.RaycastAll(body.position, (point-body.position).normalized, Vector3.Distance(body.position, point), environmentLayers, QueryTriggerInteraction.Ignore))
                if (IsObstacle(hit.collider)) return false;
            return true;
        }
        void Face(Vector3 delta)
        {
            if (delta.sqrMagnitude < .0001f) return;
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(delta.normalized, Vector3.up), turnSpeed * Time.fixedDeltaTime));
        }
        bool WetPath(Vector3 from, Vector3 to)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(to, from) / Mathf.Max(.1f, Radius * .5f)));
            for (int i=0; i<=steps; i++) if (ZoneAt(Vector3.Lerp(from, to, (float)i/steps), Radius) == null) return false;
            return Clear(from, to);
        }
        bool Swim(Vector3 target, float speed)
        {
            Vector3 next = Vector3.MoveTowards(body.position, target, speed * Time.fixedDeltaTime);
            if ((next-body.position).sqrMagnitude < .00001f || !WetPath(body.position,next)) return false;
            Face(next-body.position); body.MovePosition(next); return true;
        }
        bool Navigate(Vector3 target, float speed)
        {
            if (Swim(target,speed)) return true;
            // Local steering around banks and small obstacles; each step stays entirely in water.
            Vector3 direction=(target-body.position).normalized;
            foreach (float angle in new[]{35f,-35f,70f,-70f,110f,-110f})
            {
                Vector3 detour=Quaternion.AngleAxis(angle,Vector3.up)*direction;
                if(WetPath(body.position,body.position+detour*Mathf.Max(Radius,speed*.3f)) && Swim(body.position+detour*2,speed)) return true;
            }
            return false;
        }
        bool ChoosePatrolTarget()
        {
            hasPatrolTarget=false;
            if(patrolDistance<=.05f)return false;
            for(int attempt=0;attempt<32;attempt++)
            {
                Vector2 offset=Random.insideUnitCircle*patrolDistance;
                Vector3 candidate=home+new Vector3(offset.x,Random.Range(-patrolVerticalRange,patrolVerticalRange),offset.y);
                if(Vector3.Distance(candidate,body.position)<Mathf.Min(1,patrolDistance*.5f) || !WetPath(body.position,candidate))continue;
                patrolTarget=candidate;hasPatrolTarget=true;return true;
            }
            return false;
        }
        public static Vector3 Arc(Vector3 start, Vector3 initialVelocity, float gravity, float time)
            => start + initialVelocity*time + Vector3.down * (.5f*gravity*time*time);
        public static Vector3 ReturnArc(Vector3 start, Vector3 initialVelocity, float gravity, float duration, float time)
            => Arc(start,initialVelocity,gravity,Mathf.Max(0,Mathf.Min(time,duration-time)));
        Vector3 FlightPosition(float time)=>returnJump?ReturnArc(launch,velocity,jumpGravity,flightDuration,time):Arc(launch,velocity,jumpGravity,time);
        public static bool SolveArc(Vector3 start, Vector3 end, float apex, float gravity, out Vector3 initialVelocity, out float duration)
        {
            initialVelocity = Vector3.zero; duration = 0;
            if (gravity <= 0 || apex <= Mathf.Max(start.y, end.y)) return false;
            float up = Mathf.Sqrt(2*(apex-start.y)/gravity), down = Mathf.Sqrt(2*(apex-end.y)/gravity);
            duration = up+down; initialVelocity = (end-start)/duration; initialVelocity.y = gravity*up;
            return true;
        }
        bool NearWaterBelow(Vector3 point)
        {
            foreach (var zone in SonicWaterVolume.Active)
            {
                if (!Horizontal(zone) || !zone.isActiveAndEnabled) continue;
                Vector3 probe = new Vector3(point.x, zone.transform.position.y-Radius-.1f, point.z);
                if (Fits(zone, probe, Radius)) return true;
            }
            return false;
        }
        public bool ValidateArc(Vector3 start, Vector3 end, Vector3 initialVelocity, float duration, SonicWaterVolume destination, Vector3 target)
            => ValidateFlight(start,end,initialVelocity,duration,destination,target,false);
        bool ValidateFlight(Vector3 start, Vector3 end, Vector3 initialVelocity, float duration, SonicWaterVolume destination, Vector3 target,bool returns)
        {
            if (!Fits(destination, end, Radius) || Vector3.Distance(start,end) > maximumJumpDistance) return false;
            if(returns && Vector3.Distance(start,Arc(start,initialVelocity,jumpGravity,duration*.5f))>maximumJumpDistance)return false;
            float lengthBound = (initialVelocity.magnitude + jumpGravity*duration)*duration;
            int steps = Mathf.Clamp(Mathf.CeilToInt(lengthBound / Mathf.Max(.1f,Radius*.4f)), 24, 1024);
            float nearest=float.MaxValue;
            for(int i=1;i<=steps;i++) nearest=Mathf.Min(nearest,Vector3.Distance(returns?ReturnArc(start,initialVelocity,jumpGravity,duration,duration*i/steps):Arc(start,initialVelocity,jumpGravity,duration*i/steps),target));
            if(nearest>Radius+.9f)return false;
            float gap=0;
            Vector3 previous=start;
            for (int i=1;i<=steps;i++)
            {
                Vector3 point=returns?ReturnArc(start,initialVelocity,jumpGravity,duration,duration*i/steps):Arc(start,initialVelocity,jumpGravity,duration*i/steps);
                if (!Clear(previous,point)) return false;
                float span=Vector2.Distance(new Vector2(previous.x,previous.z),new Vector2(point.x,point.z));
                gap=NearWaterBelow(point)?0:gap+span;
                if (gap>maximumDryGap) return false;
                nearest=Mathf.Min(nearest,Vector3.Distance(point,target)); previous=point;
            }
            return nearest <= Radius + .9f;
        }
        bool TryLeapFrom(Vector3 start, Vector3 target, out LeapPlan plan)
        {
            plan=default;
            var source=ZoneAt(start,Radius);
            if(!Horizontal(source) || !Clear(start,start))return false;
            float maxApex=source.transform.position.y+Mathf.Max(1,jumpHeight);
            // Aim above the platform by the fish's radius, rather than driving its belly into the edge.
            if(target.y+Mathf.Max(.25f,Radius-.5f)>maxApex || target.y<source.transform.position.y)return false;
            float gravity=Mathf.Max(1,jumpGravity);
            Vector3 horizontal=Vector3.ProjectOnPlane(target-start,Vector3.up);
            for(int clearance=0;clearance<3;clearance++)
            {
            float attackY=target.y+Mathf.Lerp(Mathf.Max(.25f,Radius-.5f),Radius+.75f,clearance/2f);
            float minApex=Mathf.Max(attackY+.05f,source.transform.position.y+.5f);
            if(minApex>maxApex)continue;
            for(int height=0;height<6;height++)
            {
                float apex=Mathf.Lerp(minApex,maxApex,height/5f);
                float vy=Mathf.Sqrt(2*gravity*(apex-start.y));
                float root=Mathf.Sqrt(Mathf.Max(0,vy*vy-2*gravity*(attackY-start.y)));
                // Passing Sonic on ascent OR descent permits asymmetric flights over a wide bridge.
                for(int side=0;side<2;side++)
                {
                    float targetTime=(vy+(side==0?-root:root))/gravity;
                    if(targetTime<.05f)continue;
                    foreach(var zone in SonicWaterVolume.Active)
                    {
                        if(!Horizontal(zone) || !zone.isActiveAndEnabled)continue;
                        float endY=zone.transform.position.y-Radius-.15f;
                        if(endY>=apex)continue;
                        float duration=vy/gravity+Mathf.Sqrt(2*(apex-endY)/gravity);
                        Vector3 speed=horizontal/targetTime;speed.y=vy;
                        Vector3 candidate=Arc(start,speed,gravity,duration);
                        if(Vector3.Distance(start,candidate)>maximumJumpDistance || !Fits(zone,candidate,Radius))continue;
                        if(!ValidateArc(start,candidate,speed,duration,zone,target))continue;
                        plan=new LeapPlan{start=start,end=candidate,velocity=speed,duration=duration,destination=zone};return true;
                    }
                }
            }
            }
            if(allowReturnJump)
            {
                // Turn at the apex and retrace a verified path into the same nearby water.
                // This avoids needing to cross the entire width of a bridge to attack its edge.
                for(int clearance=0;clearance<5;clearance++)
                {
                    float apex=target.y+Mathf.Lerp(Mathf.Max(.4f,Radius-.35f),Radius+.75f,clearance/4f);
                    if(apex>maxApex || apex<=start.y)continue;
                    float upTime=Mathf.Sqrt(2*(apex-start.y)/gravity);
                    Vector3 speed=horizontal/upTime;speed.y=gravity*upTime;
                    if(!ValidateFlight(start,start,speed,upTime*2,source,target,true))continue;
                    plan=new LeapPlan{start=start,end=start,velocity=speed,duration=upTime*2,destination=source,returns=true};return true;
                }
            }
            return false;
        }
        void StartLeap(LeapPlan plan)
        {
            launch=plan.start;landing=plan.end;velocity=plan.velocity;flightDuration=plan.duration;flightTime=0;landingWater=plan.destination;
            returnJump=plan.returns;
            water.Splash(new Vector3(launch.x,water.transform.position.y,launch.z),4);
            mode=Mode.Leap;hasStagingPoint=false;
        }
        bool PlanLeap()
        {
            if(!TryLeapFrom(body.position,Aim,out var plan))return false;
            StartLeap(plan);return true;
        }
        // A route consists only of checked, fully submerged segments. First try simple
        // horizontal/depth detours; a small visibility graph then handles submerged piers.
        bool FindSwimRoute(Vector3 goal, List<Vector3> route)
        {
            route.Clear();Vector3 start=body.position;
            if(WetPath(start,goal)){route.Add(goal);return true;}
            float surface=water.transform.position.y-Radius-.15f;
            foreach(float depth in new[]{start.y,Mathf.Min(start.y,goal.y),surface-Radius*2,start.y-Radius*2,start.y-Radius*4})
            {
                Vector3 a=new Vector3(start.x,depth,start.z),b=new Vector3(goal.x,depth,goal.z);
                if(WetPath(start,a) && WetPath(a,b) && WetPath(b,goal)){route.Add(a);route.Add(b);route.Add(goal);return true;}
            }
            var nodes=new List<Vector3>{start,goal};
            var bounds=new Bounds((start+goal)*.5f,new Vector3(Mathf.Abs(start.x-goal.x)+Radius*6,Mathf.Abs(start.y-goal.y)+Radius*8,Mathf.Abs(start.z-goal.z)+Radius*6));
            foreach(var col in Physics.OverlapBox(bounds.center,bounds.extents,Quaternion.identity,environmentLayers,QueryTriggerInteraction.Ignore))
            {
                if(!IsObstacle(col))continue;
                Bounds obstacle=col.bounds;obstacle.Expand((Radius+.25f)*2);
                foreach(float depth in new[]{Mathf.Min(start.y,goal.y),Mathf.Min(start.y,obstacle.min.y)})
                foreach(float x in new[]{obstacle.min.x,obstacle.max.x})
                foreach(float z in new[]{obstacle.min.z,obstacle.max.z})
                {
                    Vector3 p=new Vector3(x,depth,z);
                    if(Vector3.Distance(start,p)<=launchSearchRadius*2 && ZoneAt(p,Radius)!=null && Clear(p,p))nodes.Add(p);
                    if(nodes.Count>=50)break;
                }
                if(nodes.Count>=50)break;
            }
            var cost=new float[nodes.Count];var parent=new int[nodes.Count];var closed=new bool[nodes.Count];
            for(int i=0;i<cost.Length;i++){cost[i]=float.PositiveInfinity;parent[i]=-1;}cost[0]=0;
            for(int iteration=0;iteration<nodes.Count;iteration++)
            {
                int current=-1;float best=float.PositiveInfinity;
                for(int i=0;i<nodes.Count;i++)if(!closed[i] && cost[i]+Vector3.Distance(nodes[i],goal)<best){current=i;best=cost[i]+Vector3.Distance(nodes[i],goal);}
                if(current<0)break;
                if(current==1)
                {
                    for(int node=1;node!=0;node=parent[node])route.Add(nodes[node]);route.Reverse();return true;
                }
                closed[current]=true;
                for(int i=1;i<nodes.Count;i++)
                {
                    float next=cost[current]+Vector3.Distance(nodes[current],nodes[i]);
                    if(closed[i] || next>=cost[i] || !WetPath(nodes[current],nodes[i]))continue;
                    cost[i]=next;parent[i]=current;
                }
            }
            return false;
        }
        bool FindLaunchPoint()
        {
            foreach(bool found in SearchLaunchPoints())if(found)return true;
            return false;
        }
        void AdvanceLaunchSearch()
        {
            if(launchSearch==null || lastPlanFrame==Time.frameCount)return;
            lastPlanFrame=Time.frameCount;
            var budget=System.Diagnostics.Stopwatch.StartNew();
            do
            {
                if(!launchSearch.MoveNext() || launchSearch.Current)
                {launchSearch.Dispose();launchSearch=null;nextPlan=Time.time+.8f;break;}
            }while(budget.Elapsed.TotalMilliseconds<3);
        }
        IEnumerable<bool> SearchLaunchPoints()
        {
            hasStagingPoint=false;approachRoute.Clear();approachIndex=0;
            planningFailure="Aucune trajectoire libre avec retombee dans l'eau proche";
            if(!Horizontal(water))yield break;
            Vector3 target=Aim;
            float required=target.y+Mathf.Max(.25f,Radius-.5f)+.05f-water.transform.position.y;
            if(required>jumpHeight){planningFailure="Saut trop bas : hauteur Y requise >= "+required.ToString("0.0")+" (reglage "+jumpHeight.ToString("0.0")+")";yield break;}
            Vector3 near=body.position;near.y=water.transform.position.y-Radius-.15f;
            var candidates=new List<Vector3>{near};
            Vector3 delta=Vector3.ProjectOnPlane(body.position-target,Vector3.up);
            float baseAngle=Mathf.Atan2(delta.z,delta.x);
            float far=Mathf.Min(launchSearchRadius,maximumJumpDistance*.65f);
            for(int ring=0;ring<6;ring++)
            {
                float radius=Mathf.Lerp(Mathf.Max(2,Radius*2),Mathf.Max(3,far),ring/5f);
                for(int sector=0;sector<16;sector++)
                {
                    int step=(sector+1)/2*(sector%2==0?-1:1);float angle=baseAngle+step*Mathf.PI/8;
                    Vector3 p=target+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);p.y=near.y;candidates.Add(p);
                }
            }
            candidates.Sort((a,b)=>(a-body.position).sqrMagnitude.CompareTo((b-body.position).sqrMagnitude));
            foreach(var candidate in candidates)
            {
                yield return false;
                // Test whether the full body can emerge here before expensive ballistic planning.
                if(!Fits(water,candidate,Radius) || !Clear(candidate,candidate+Vector3.up*(Radius*2+.3f)))continue;
                if(!TryLeapFrom(candidate,target,out _) || !FindSwimRoute(candidate,approachRoute))continue;
                stagingPoint=candidate;plannedTarget=target;hasStagingPoint=true;yield return true;yield break;
            }
        }
        bool SearchSwim()
        {
            if(!hasPatrolTarget && !ChoosePatrolTarget())return false;
            bool moved=Swim(patrolTarget,swimSpeed);
            if(!moved || Vector3.Distance(body.position,patrolTarget)<.2f)hasPatrolTarget=false;
            return moved;
        }
        void Cooldown() { mode=Mode.Patrol; nextAttack=Time.time+attackCooldown; pause=.3f; hasPatrolTarget=false; hasStagingPoint=false; launchSearch?.Dispose();launchSearch=null; }
        void FixedUpdate()
        {
            bool moving=false, attacking=false;
            float dt=Time.fixedDeltaTime; modeAge+=dt;
            bool available=Available(); distanceToSonic=available?Vector3.Distance(body.position,Aim):-1;
            if (mode==Mode.Leap || mode==Mode.Retreat)
            {
                attacking=true;
                if (mode==Mode.Leap && (!Fits(landingWater,landing,Radius) || !Clear(body.position,FlightPosition(Mathf.Min(flightDuration,flightTime+dt))))) mode=Mode.Retreat;
                flightTime=Mathf.Clamp(flightTime+(mode==Mode.Leap?dt:-dt),0,flightDuration);
                Vector3 next=FlightPosition(flightTime);
                // An obstacle appearing mid-flight triggers a return along the already traversed arc.
                if (!Clear(body.position,next)) { status="Retour bloque : attente"; Animate(false,true); return; }
                Face(next-body.position); body.MovePosition(next); moving=true;
                status=mode==Mode.Leap?"Saut vers Sonic":"Retour de securite";
                if (flightTime>=flightDuration || (mode==Mode.Retreat&&flightTime<=0))
                {
                    water=ZoneAt(next,Radius);
                    if (water!=null) water.Splash(new Vector3(next.x,water.transform.position.y,next.z),Mathf.Abs(velocity.y-jumpGravity*flightTime));
                    home=next; Cooldown();
                }
                Animate(moving,attacking); return;
            }
            if (!Fits(water,body.position,Radius)) water=ZoneAt(body.position,Radius);
            if (water==null)
            {
                status="Placer le poisson entier dans une zone Sonic Water Volume"; Animate(false,false); return;
            }
            bool submerged=available && ZoneAt(Aim,0)!=null;
            float trackingDistance=available && !submerged?Vector3.ProjectOnPlane(Aim-body.position,Vector3.up).magnitude:distanceToSonic;
            bool detected=available && trackingDistance<=noticeDistance;
            bool tracking=available && trackingDistance<=Mathf.Max(noticeDistance,loseDistance);
            if((mode==Mode.Chase || mode==Mode.PrepareCharge || mode==Mode.ApproachSurface) && !tracking)Cooldown();
            if(mode==Mode.Patrol && detected)
            {
                modeAge=0;hasPatrolTarget=false;
                if(submerged)mode=Mode.Chase;
                else if(Horizontal(water) && Aim.y>=water.transform.position.y)
                { mode=Mode.ApproachSurface;hasStagingPoint=false;nextPlan=0; }
            }
            switch(mode)
            {
                case Mode.Patrol:
                    status="Patrouille libre";pause-=dt;
                    if(pause<=0)
                    {
                        if(!hasPatrolTarget && !ChoosePatrolTarget()){pause=.5f;break;}
                        moving=Swim(patrolTarget,swimSpeed);
                        if(!moving || Vector3.Distance(body.position,patrolTarget)<.2f)
                        {hasPatrolTarget=false;pause=Random.Range(pauseSeconds*.5f,pauseSeconds*1.5f);}
                    }
                    break;
                case Mode.Chase:
                    status="Poursuite de Sonic";
                    if(!submerged){mode=Mode.ApproachSurface;hasStagingPoint=false;nextPlan=0;break;}
                    if(distanceToSonic<=biteStartDistance && Sees(Aim) && Time.time>=nextAttack)
                    {mode=Mode.PrepareCharge;modeAge=0;break;}
                    moving=Navigate(Aim,chaseSpeed);
                    if(!moving)status="Poursuite : obstacle ou bord du bassin";
                    break;
                case Mode.PrepareCharge:
                    status="Preparation de la morsure";attacking=true;Face(Aim-body.position);
                    if(!submerged || distanceToSonic>biteStartDistance*1.2f || !Sees(Aim))
                    {mode=Mode.Chase;modeAge=0;break;}
                    if(modeAge>=reactionTime)
                    {chargeDirection=(Aim-body.position).normalized;modeAge=0;mode=Mode.Charge;}
                    break;
                case Mode.Charge:
                    status="Charge sous-marine";attacking=true;
                    moving=Swim(body.position+chargeDirection*(chargeSpeed*dt+1),chargeSpeed);
                    if(!moving || modeAge>=chargeDuration || !available)Cooldown();
                    break;
                case Mode.ApproachSurface:
                    status="Recherche d'un point de saut autour de la plateforme";
                    if(submerged){mode=Mode.Chase;break;}
                    if(Time.time<nextAttack) {status="Recuperation avant le saut";break;}
                    if((hasStagingPoint || launchSearch!=null) && Vector3.Distance(Aim,plannedTarget)>1.5f){hasStagingPoint=false;launchSearch?.Dispose();launchSearch=null;nextPlan=Time.time+.2f;}
                    if(!hasStagingPoint && launchSearch==null && Time.time>=nextPlan)
                    {
                        plannedTarget=Aim;launchSearch=SearchLaunchPoints().GetEnumerator();
                    }
                    if(!hasStagingPoint)AdvanceLaunchSearch();
                    if(!hasStagingPoint){status=launchSearch!=null?"Recherche progressive d'un saut sur":planningFailure;moving=SearchSwim();break;}
                    if(Vector3.Distance(body.position,stagingPoint)>.005f)
                    {
                        while(approachIndex<approachRoute.Count-1 && Vector3.Distance(body.position,approachRoute[approachIndex])<.05f)approachIndex++;
                        Vector3 waypoint=approachIndex<approachRoute.Count?approachRoute[approachIndex]:stagingPoint;
                        status="Contournement vers le point de saut ("+(approachIndex+1)+"/"+approachRoute.Count+")";
                        moving=Swim(waypoint,chaseSpeed);
                        if(!moving){hasStagingPoint=false;nextPlan=Time.time+.4f;}
                    }
                    else if(!PlanLeap()) {hasStagingPoint=false;nextPlan=Time.time+.4f;}
                    break;
            }
            Animate(moving,attacking);
        }
        void Animate(bool moving,bool attacking)
        {
            if (animator==null) return;
            animator.SetBool("Moving",moving); animator.SetBool("Biting",attacking);
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color=Color.yellow; Gizmos.DrawWireSphere(transform.position,noticeDistance);
            Gizmos.color=Color.cyan; Vector3 center=Application.isPlaying?home:transform.position;
            Gizmos.DrawWireSphere(center,patrolDistance);
            if(Application.isPlaying && hasPatrolTarget)Gizmos.DrawLine(transform.position,patrolTarget);
            if(Application.isPlaying && hasStagingPoint){Gizmos.color=Color.cyan;Gizmos.DrawWireSphere(stagingPoint,.3f);Gizmos.DrawLine(transform.position,stagingPoint);}
            if(Application.isPlaying && hasStagingPoint){Gizmos.color=Color.cyan;Vector3 p=transform.position;for(int i=approachIndex;i<approachRoute.Count;i++){Gizmos.DrawLine(p,approachRoute[i]);p=approachRoute[i];}}
            if (water!=null) { Gizmos.color=Color.magenta; Vector3 top=transform.position; top.y=water.transform.position.y+jumpHeight; Gizmos.DrawLine(transform.position,top); }
            if (!Application.isPlaying || flightDuration<=0) return;
            Gizmos.color=Color.green;Vector3 previous=launch;
            for(int i=1;i<=40;i++){var point=FlightPosition(flightDuration*i/40);Gizmos.DrawLine(previous,point);previous=point;}
        }
    }
}
