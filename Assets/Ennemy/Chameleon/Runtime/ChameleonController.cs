using UnityEngine;
using System;
using UnityEngine.SceneManagement;

namespace SonicFX.Chameleon
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class ChameleonController : MonoBehaviour
    {
        [Header("Placement")]
        [InspectorName("Colle au mur")] public bool onWall;
        [InspectorName("Camoufle au depart")] public bool startCamouflaged = true;
        [InspectorName("Mur support (facultatif)")] public Collider wallCollider;
        [Min(.2f)] public float wallSearchDistance = 4;
        [Header("Vision de Sonic")]
        public PlayerBhysics player;
        [Min(1)] public float viewDistance = 20;
        [Range(20,360)] public float viewAngle = 140;
        [Min(0)] public float reactionTime = .35f;
        [Tooltip("Terrain, murs et plateformes ; exclure Player et Enemies.")] public LayerMask environmentLayers = 1;
        [Header("Patrouille et poursuite")]
        [Min(0)] public float patrolRadius = 4;
        [Min(0)] public float groundSpeed = 3;
        [Min(0)] public float wallSpeed = 1.2f;
        [Min(0)] public float pauseDuration = 1;
        [Min(1)] public float turnSpeed = 240;
        [Min(.1f)] public float maximumDrop = .65f;
        [Header("Tir au mur")]
        public ChameleonProjectile projectilePrefab;
        [Min(.2f)] public float attackCooldown = 2;
        [Min(.05f)] public float windup = .35f;
        [Min(.1f)] public float projectileSpeed = 16;
        [Header("Bond vers Sonic")]
        [Tooltip("Proximite horizontale de Sonic pour declencher le bond. La portee totale reste limitee par Jump Distance, meme si Sonic est plus bas que le mur.")]
        [InspectorName("Proximite avant le bond"), Min(.2f)] public float jumpTriggerDistance = 5;
        [Min(.2f)] public float jumpDistance = 5;
        [Min(.1f)] public float jumpHeight = 3;
        [Min(1)] public float gravity = 22;
        [Min(1)] public float maximumJumpSpeed = 14;
        [Header("Langue au sol")]
        [Min(.2f)] public float tongueRange = 6;
        [Min(.05f)] public float tongueRadius = .18f;
        [Min(.2f)] public float tongueDuration = .7f;
        [Header("References du modele")]
        public Transform mouth;
        public GameObject homingTarget;
        public ChameleonVisual visual;
        public ChameleonCamouflage camouflage;
        [SerializeField] string status;
        public bool IsCamouflaged { get; private set; }
        public bool AttachedToWall { get; private set; }
        public string Status => status;
        public int ProjectilesFired { get; private set; }
        public float MoveRate { get; private set; }
        public float AttackPose { get; private set; }
        public bool IsLeaping => mode == Mode.Leap;
        enum Mode { Patrol, Shot, Tongue, Leap }
        Mode mode;
        Rigidbody body;
        Vector3 home, wallNormal, destination, velocity, attackDirection;
        float timer, cooldown, seenTime, searchAt, fallSpeed, leapAge;
        bool initialized, fired, tongueHit;
        readonly RaycastHit[] hits = new RaycastHit[128];
        readonly Collider[] contacts = new Collider[128];
        PhysicsScene PhysicsWorld => gameObject.scene.GetPhysicsScene();
        public Vector3 Eye => transform.TransformPoint(new Vector3(0, 1.15f, .85f));
        Vector3 Aim => player.transform.position + Vector3.up * .65f;
        float Size => Mathf.Max(.1f, Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y)));
        void Start() { Initialize(); }
        void OnDisable() { if (visual != null) visual.HideTongue(); SetCamouflaged(false); }
        void OnEnable() { initialized = false; }
        public void Initialize()
        {
            body = GetComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            mode = Mode.Patrol; timer = pauseDuration; cooldown = seenTime = fallSpeed = leapAge = 0;
            if (visual == null) visual = GetComponent<ChameleonVisual>();
            if (visual != null) visual.HideTongue();
            AttackPose = 0;
            if (camouflage == null) camouflage = GetComponent<ChameleonCamouflage>();
            AttachedToWall = onWall && SnapToWall();
            home = destination = transform.position;
            SetCamouflaged(AttachedToWall && startCamouflaged);
            initialized = true;
        }
        public bool SnapToWall()
        {
            float nearest = float.PositiveInfinity; RaycastHit best = default; bool found = false;
            Vector3 origin = transform.position + transform.up * .15f;
            Vector3[] directions = { -transform.up, transform.forward, -transform.forward, transform.right, -transform.right, Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
            if (wallCollider != null) {
                Vector3 toward = wallCollider.ClosestPoint(origin) - origin;
                if (toward.sqrMagnitude > .00001f && wallCollider.Raycast(new Ray(origin, toward.normalized), out var h, wallSearchDistance + .2f) && Mathf.Abs(h.normal.y) < .6f) { best = h; found = true; }
            }
            if (!found) foreach (var direction in directions) {
                int count = PhysicsWorld.Raycast(origin, direction, hits, wallSearchDistance, environmentLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++) { var hit = hits[i];
                if (hit.transform.IsChildOf(transform) || Mathf.Abs(hit.normal.y) >= .6f || hit.distance >= nearest) continue;
                if (wallCollider != null && hit.collider != wallCollider) continue;
                nearest = hit.distance; best = hit; found = true;
            } }
            if (!found) { status = "Mur introuvable : retour au sol"; SetPose(transform.position, Quaternion.Euler(0, transform.eulerAngles.y, 0)); return false; }
            wallCollider = best.collider; wallNormal = best.normal;
            SetPose(best.point + wallNormal * .04f, WallRotation());
            if (camouflage == null) camouflage = GetComponent<ChameleonCamouflage>();
            if (camouflage != null) camouflage.Capture(best);
            return true;
        }
        Quaternion WallRotation()
        {
            Vector3 upWall = Vector3.ProjectOnPlane(Vector3.up, wallNormal).normalized;
            return Quaternion.LookRotation(upWall, wallNormal);
        }
        void SetPose(Vector3 position, Quaternion rotation)
        {
            if (body == null) body = GetComponent<Rigidbody>();
            // Interpolated rigidbodies own their Transform during physics updates.
            // Teleport both poses so an old upright physics pose cannot replace the wall pose.
            var interpolation = body.interpolation;
            body.interpolation = RigidbodyInterpolation.None;
            body.position = position; body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            body.interpolation = interpolation;
        }
        public void SetCamouflaged(bool value)
        {
            IsCamouflaged = value && AttachedToWall;
            if (camouflage != null) camouflage.Apply(IsCamouflaged);
            if (homingTarget != null) {
                homingTarget.SetActive(!IsCamouflaged);
                if (IsCamouflaged && HomingAttackControl.TargetObject == homingTarget) {
                    HomingAttackControl.TargetObject = null;
                    foreach (var control in FindObjectsByType<HomingAttackControl>()) {
                        control.HasTarget = false; if (control.Icon != null) control.Icon.localScale = Vector3.zero;
                    }
                }
                HomingAttackControl.UpdateHomingTargets();
            }
        }
        bool PlayerReady()
        {
            if ((player == null || !player.gameObject.activeInHierarchy) && Time.time >= searchAt) {
                searchAt = Time.time + 1;
                foreach (var go in GameObject.FindGameObjectsWithTag("Player")) {
                    var candidate = go.GetComponentInParent<PlayerBhysics>();
                    if (candidate == null) candidate = go.GetComponentInChildren<PlayerBhysics>();
                    if (candidate != null && candidate.gameObject.activeInHierarchy) { player = candidate; break; }
                }
            }
            if (player == null || !player.gameObject.activeInHierarchy) return false;
            var hurt = player.GetComponent<HurtControl>(); return hurt == null || !hurt.isDead;
        }
        public bool CanSee(Vector3 point)
        {
            Vector3 offset = point - Eye; if (offset.sqrMagnitude > viewDistance * viewDistance) return false;
            Vector3 forward = AttachedToWall ? wallNormal : transform.forward;
            if (offset.sqrMagnitude > .001f && Vector3.Angle(forward, offset) > viewAngle * .5f) return false;
            int count = PhysicsWorld.Raycast(Eye, offset.normalized, hits, offset.magnitude, environmentLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) { var hit = hits[i];
                if (!hit.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<PlayerBhysics>() == null) return false;
            }
            return true;
        }
        void FixedUpdate() { Tick(Time.fixedDeltaTime); }
        public void Tick(float dt)
        {
            if (!initialized) Initialize(); if (dt <= 0) return;
            if (AttachedToWall) body.MoveRotation(WallRotation());
            MoveRate = 0; cooldown = Mathf.Max(0, cooldown - dt);
            if (mode == Mode.Leap) { LeapStep(dt); return; }
            bool ready = PlayerReady(); bool seen = ready && CanSee(Aim);
            seenTime = seen ? seenTime + dt : 0;
            float distance = ready ? Vector3.Distance(Eye, Aim) : float.PositiveInfinity;
            // Finish a prepared attack before choosing another one. Otherwise a
            // player entering the leap radius during windup cancels every shot.
            if (mode == Mode.Shot || mode == Mode.Tongue) { AttackStep(dt); return; }
            float proximity = ready ? Vector3.ProjectOnPlane(Aim - Eye, Vector3.up).magnitude : float.PositiveInfinity;
            if (AttachedToWall && seen && distance <= jumpDistance && proximity <= jumpTriggerDistance && seenTime >= reactionTime) { BeginLeap(Aim); return; }
            AttackPose = 0;
            if (seen && seenTime >= reactionTime) {
                if (AttachedToWall) {
                    if (cooldown <= 0) BeginAttack(Mode.Shot, Aim);
                } else {
                    Face(Aim - transform.position, dt);
                    if (distance <= tongueRange && cooldown <= 0) BeginAttack(Mode.Tongue, Aim);
                    else if (distance > tongueRange * .8f) MoveGround(Vector3.ProjectOnPlane(Aim - transform.position, Vector3.up).normalized * groundSpeed * dt);
                }
            } else if (!IsCamouflaged) Patrol(dt);
            else status = "Camoufle : immobile, en surveillance";
            if (!AttachedToWall) GroundGravity(dt);
        }
        void BeginAttack(Mode next, Vector3 point)
        {
            mode = next; timer = 0; fired = tongueHit = false;
            attackDirection = (point - AttackOrigin).normalized;
            status = next == Mode.Shot ? "Preparation du projectile" : "Preparation de la langue";
        }
        void AttackStep(float dt)
        {
            timer += dt; AttackPose = Mathf.Sin(Mathf.Clamp01(timer / (windup + tongueDuration)) * Mathf.PI);
            if (mode == Mode.Tongue && player != null) Face(Aim - transform.position, dt);
            if (mode == Mode.Shot && timer >= windup && !fired) {
                fired = true;
                if (projectilePrefab != null) {
                    attackDirection = player != null ? (Aim - AttackOrigin).normalized : attackDirection;
                    var shot = Instantiate(projectilePrefab, AttackOrigin, Quaternion.LookRotation(attackDirection));
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(shot.gameObject, gameObject.scene);
                    shot.Launch(transform, attackDirection * projectileSpeed); ProjectilesFired++;
                }
                status = projectilePrefab != null ? "Un projectile tire" : "Projectile manquant : renseigner Projectile Prefab";
            }
            if (mode == Mode.Tongue && timer >= windup) {
                if (!fired) { fired = true; if (player != null) attackDirection = (Aim - AttackOrigin).normalized; }
                float progress = Mathf.Clamp01((timer - windup) / tongueDuration);
                float extension = Mathf.Sin(progress * Mathf.PI) * tongueRange;
                Vector3 start = AttackOrigin; float visibleLength = extension;
                Collider contact = FirstHit(start, tongueRadius, attackDirection, extension, out float length);
                if (contact != null) { visibleLength = length; if (!tongueHit && contact.GetComponentInParent<PlayerBhysics>() != null) { tongueHit = true; Damage(contact); } }
                if (visual != null) visual.ShowTongue(start, start + attackDirection * visibleLength, tongueRadius * 2);
                status = "Attaque de langue";
            }
            if (!AttachedToWall) GroundGravity(dt);
            float duration = mode == Mode.Shot ? windup + .4f : windup + tongueDuration;
            if (timer >= duration) { mode = Mode.Patrol; cooldown = attackCooldown; timer = pauseDuration; AttackPose = 0; if (visual != null) visual.HideTongue(); }
        }
        void BeginLeap(Vector3 target)
        {
            SetCamouflaged(false); AttachedToWall = false; onWall = false; mode = Mode.Leap; leapAge = 0;
            Vector3 start = transform.position + wallNormal * (.7f * Size);
            float top = Mathf.Max(start.y, target.y) + jumpHeight;
            float vy = Mathf.Sqrt(2 * gravity * Mathf.Max(.1f, top - start.y));
            float time = vy / gravity + Mathf.Sqrt(2 * Mathf.Max(0, top - target.y) / gravity);
            Vector3 planar = Vector3.ProjectOnPlane(target - start, Vector3.up) / Mathf.Max(.1f, time);
            planar = Vector3.ClampMagnitude(planar, maximumJumpSpeed);
            velocity = planar + Vector3.up * vy;
            Vector3 facing = planar.sqrMagnitude > .01f ? planar : Vector3.ProjectOnPlane(wallNormal, Vector3.up);
            SetPose(start, Quaternion.LookRotation(facing, Vector3.up));
            status = "Bond : camouflage retire";
        }
        void LeapStep(float dt)
        {
            leapAge += dt; Vector3 step = velocity * dt + Vector3.down * (.5f * gravity * dt * dt); velocity.y -= gravity * dt;
            Vector3 origin = body.position + Vector3.up * (.7f * Size);
            RaycastHit closest = default; float nearest = float.PositiveInfinity;
            int count = PhysicsWorld.SphereCast(origin, .5f * Size, step.normalized, hits, step.magnitude, environmentLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) { var hit = hits[i];
                if (hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<PlayerBhysics>() != null || hit.distance >= nearest) continue;
                closest = hit; nearest = hit.distance;
            }
            if (nearest < float.PositiveInfinity) {
                body.MovePosition(body.position + step.normalized * Mathf.Max(0, nearest - .02f));
                if (closest.normal.y > .5f && velocity.y <= 0) { mode = Mode.Patrol; home = destination = body.position; timer = pauseDuration; cooldown = attackCooldown; status = "Atterri : comportement au sol"; }
                else velocity = Vector3.ProjectOnPlane(velocity, closest.normal);
            } else body.MovePosition(body.position + step);
            if (leapAge > 6) { mode = Mode.Patrol; home = body.position; }
        }
        void Patrol(float dt)
        {
            if (timer > 0) { timer -= dt; status = "Pause"; return; }
            Vector3 remaining = destination - transform.position;
            if (!AttachedToWall) remaining.y = 0;
            if (remaining.magnitude < .25f) {
                Vector2 random = UnityEngine.Random.insideUnitCircle * patrolRadius;
                destination = home + (AttachedToWall ? Vector3.Cross(Vector3.up, wallNormal).normalized * random.x : new Vector3(random.x,0,random.y));
                timer = pauseDuration; return;
            }
            Vector3 move = destination - transform.position;
            if (AttachedToWall) {
                Vector3 next = body.position + move.normalized * wallSpeed * dt;
                if (wallCollider != null && wallCollider.Raycast(new Ray(next + wallNormal, -wallNormal), out var hit, 2)) {
                    body.MovePosition(hit.point + wallNormal * .04f); MoveRate = wallSpeed;
                } else destination = transform.position;
            } else { Face(move, dt); if (!MoveGround(move.normalized * groundSpeed * dt)) destination = transform.position; }
            status = "Patrouille";
        }
        void Face(Vector3 direction, float dt)
        {
            direction.y = 0; if (direction.sqrMagnitude > .0001f) body.MoveRotation(Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(direction), turnSpeed * dt));
        }
        bool MoveGround(Vector3 delta)
        {
            delta.y = 0; if (delta.sqrMagnitude < .0000001f) return false;
            Vector3 next = body.position + delta;
            if (!PhysicsWorld.Raycast(next + Vector3.up * .5f, Vector3.down, out var ground, .5f + maximumDrop, environmentLayers, QueryTriggerInteraction.Ignore) || ground.normal.y < .6f) return false;
            if (PhysicsWorld.SphereCast(body.position + Vector3.up * (.8f * Size), .45f * Size, delta.normalized, out _, delta.magnitude, environmentLayers, QueryTriggerInteraction.Ignore)) return false;
            body.MovePosition(new Vector3(next.x, ground.point.y + .04f, next.z)); MoveRate = groundSpeed; return true;
        }
        void GroundGravity(float dt)
        {
            if (MoveRate > 0) { fallSpeed = 0; return; }
            if (PhysicsWorld.Raycast(body.position + Vector3.up * .3f, Vector3.down, out var ground, .4f, environmentLayers, QueryTriggerInteraction.Ignore) && ground.normal.y > .6f) { fallSpeed = 0; return; }
            fallSpeed += gravity * dt;
            float travel = fallSpeed * dt;
            if (PhysicsWorld.Raycast(body.position + Vector3.up * .1f, Vector3.down, out ground, travel + .1f, environmentLayers, QueryTriggerInteraction.Ignore)) { body.MovePosition(ground.point + Vector3.up * .04f); fallSpeed = 0; }
            else body.MovePosition(body.position + Vector3.down * travel);
        }
        public Collider FirstHit(Vector3 origin, float radius, Vector3 direction, float distance, out float length)
        {
            length = distance; Collider chosen = null;
            int count = PhysicsWorld.OverlapSphere(origin, radius, contacts, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++) if (ValidContact(contacts[i])) { length = 0; return contacts[i]; }
            count = PhysicsWorld.SphereCast(origin, radius, direction, hits, distance, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++) { var hit = hits[i];
                if (!ValidContact(hit.collider) || hit.distance > length) continue; length = hit.distance; chosen = hit.collider;
            }
            return chosen;
        }
        bool ValidContact(Collider other) { return !other.transform.IsChildOf(transform) && (!other.isTrigger || other.GetComponentInParent<PlayerBhysics>() != null); }
        Vector3 AttackOrigin => mouth != null ? mouth.position : Eye;
        public static void Damage(Collider other)
        {
            var player = other.GetComponentInParent<PlayerBhysics>(); if (player == null) return;
            var damage = player.GetComponent<Objects_Interaction>(); if (damage == null) damage = player.GetComponentInChildren<Objects_Interaction>();
            if (damage != null) damage.DamagePlayer();
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, viewDistance);
            Vector3 forward = onWall ? transform.up : transform.forward;
            Gizmos.DrawRay(Eye, Quaternion.AngleAxis(viewAngle*.5f, Vector3.up)*forward*viewDistance);
            Gizmos.DrawRay(Eye, Quaternion.AngleAxis(-viewAngle*.5f, Vector3.up)*forward*viewDistance);
            Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, onWall ? jumpDistance : tongueRange);
            if (onWall) { Gizmos.color = new Color(1,.5f,0); Gizmos.DrawWireSphere(transform.position, jumpTriggerDistance); }
        }
    }
}
