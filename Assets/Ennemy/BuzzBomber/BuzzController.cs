using UnityEngine;

namespace SonicFX.Buzz
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(Animator))]
    public class BuzzController : MonoBehaviour
    {
        [Header("Parcours editable")]
        public BuzzRoute route;
        [Min(0.05f)] public float arrivalDistance = 0.15f;
        [Min(1f)] public float turnSpeed = 180f;
        [Header("Detection de Sonic")]
        public PlayerBhysics player;
        [Min(1f)] public float noticeDistance = 24f;
        [Min(1f)] public float loseDistance = 32f;
        [Min(0f)] public float lostSightDelay = 2f;
        [Min(1f), Tooltip("Distance maximale depuis le point ou Sonic a ete repere.")]
        public float maximumChaseDistance = 14f;
        [Header("Approche et attaque")]
        [Min(0.1f)] public float approachSpeed = 4f;
        [Min(1f)] public float standOffDistance = 9f;
        [Min(1f)] public float firingDistance = 17f;
        [Min(0.1f)] public float attackCooldown = 2.2f;
        [Min(0.1f)] public float projectileSpeed = 14f;
        public BuzzProjectile projectilePrefab;
        public Transform muzzle;
        [Header("Obstacles")]
        [Tooltip("Terrain et murs uniquement. Exclure Player, Enemies et EnemyTrigger.")]
        public LayerMask environmentLayers = 1;
        [Min(0.1f)] public float clearanceRadius = 0.9f;
        [Header("Diagnostic pendant Play")]
        [SerializeField] private string status = "Patrouille";
        [SerializeField] private float sonicDistance;
        [SerializeField] private bool seesSonic;
        [SerializeField] private string obstacle;

        private enum Mode { Patrol, Engage, Attack, Return }
        private Mode mode;
        private Rigidbody body;
        private Animator animator;
        private int pointIndex = -1, direction = 1, firedMask;
        private float pauseRemaining, unseen, nextShot, attackAge, nextSearch;
        private bool arrived;
        private Vector3 home, engagementAnchor, lockedAim;
        private static readonly int Flying = Animator.StringToHash("Flying");
        private static readonly int Shoot = Animator.StringToHash("Shoot");

        private void Awake()
        {
            body = GetComponent<Rigidbody>(); animator = GetComponent<Animator>();
            body.isKinematic = true; body.useGravity = false;
            home = body.position; engagementAnchor = home;
            pointIndex = route != null ? route.FirstValid() : -1;
        }
        private bool Available()
        {
            if (player == null && Time.time >= nextSearch)
            {
                nextSearch = Time.time + 1f;
                foreach (var obj in GameObject.FindGameObjectsWithTag("Player"))
                {
                    var candidate = obj.GetComponentInParent<PlayerBhysics>();
                    if (candidate == null) candidate = obj.GetComponentInChildren<PlayerBhysics>();
                    if (candidate != null && candidate.gameObject.activeInHierarchy) { player = candidate; break; }
                }
            }
            if (player == null || !player.isActiveAndEnabled) return false;
            var actions = player.GetComponent<ActionManager>();
            return actions == null || actions.Action04Control == null || !actions.Action04Control.isDead;
        }
        private Vector3 AimPoint => player.transform.position + Vector3.up * 0.65f;
        private bool Visible(Vector3 from, Vector3 to)
        {
            foreach (var hit in Physics.RaycastAll(from, (to-from).normalized, Vector3.Distance(from,to), environmentLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<PlayerBhysics>() != null) continue;
                obstacle = hit.collider.name; return false;
            }
            return true;
        }
        private Vector3 Anchor => transform.parent != null ? transform.parent.TransformPoint(engagementAnchor) : engagementAnchor;
        private void RememberAnchor()
        {
            engagementAnchor = transform.parent != null ? transform.parent.InverseTransformPoint(body.position) : body.position;
        }
        private bool HasPoint => route != null && route.points != null && pointIndex >= 0 && pointIndex < route.points.Length && route.points[pointIndex] != null;

        private void FixedUpdate()
        {
            obstacle = "";
            float dt = Time.fixedDeltaTime;
            bool available = Available();
            sonicDistance = available ? Vector3.Distance(body.position, player.transform.position) : -1f;
            seesSonic = available && sonicDistance <= loseDistance && Visible(body.position + Vector3.up * .4f, AimPoint);
            unseen = seesSonic ? 0f : unseen + dt;
            bool moving = false;
            if (mode == Mode.Patrol && seesSonic && sonicDistance <= noticeDistance && Time.time >= nextShot)
            { RememberAnchor(); mode = Mode.Engage; }
            if ((mode == Mode.Engage || mode == Mode.Attack) &&
                (!available || sonicDistance > loseDistance || unseen > lostSightDelay || Vector3.Distance(body.position, Anchor) > maximumChaseDistance + 0.5f))
            {
                mode = Mode.Return; animator.ResetTrigger(Shoot); animator.CrossFadeInFixedTime("SurPlace", .12f);
                nextShot = Time.time + attackCooldown;
            }
            switch (mode)
            {
                case Mode.Patrol:
                    status = "Patrouille";
                    if (!HasPoint) { pointIndex = route != null ? route.FirstValid() : -1; arrived = false; }
                    if (!HasPoint) { status = "Aucun point : vol sur place"; break; }
                    var point = route.points[pointIndex];
                    float distance = Vector3.Distance(body.position, point.transform.position);
                    if (distance <= arrivalDistance)
                    {
                        if (!arrived) { arrived = true; pauseRemaining = Mathf.Max(0f, point.pause); }
                        pauseRemaining -= dt; status = "Pause au point " + (pointIndex + 1);
                        if (pauseRemaining <= 0f) { pointIndex = route.NextValid(pointIndex, ref direction); arrived = false; }
                    }
                    else { arrived = false; moving = MoveTo(point.transform.position, point.speed, true); }
                    break;
                case Mode.Engage:
                    status = "Approche de Sonic";
                    Face(AimPoint - body.position);
                    Vector3 horizontal = player.transform.position - body.position; horizontal.y = 0;
                    // Keep route altitude during combat, so the flying enemy never dives into the terrain.
                    Vector3 desired = body.position;
                    if (horizontal.magnitude > standOffDistance + .5f)
                        desired += horizontal.normalized * (horizontal.magnitude - standOffDistance);
                    else if (horizontal.magnitude < standOffDistance - 2f)
                        desired -= horizontal.normalized * (standOffDistance - 2f - horizontal.magnitude);
                    desired.y = Anchor.y;
                    desired = Anchor + Vector3.ClampMagnitude(desired - Anchor, maximumChaseDistance);
                    if (Vector3.Distance(desired, body.position) > .2f) moving = MoveTo(desired, approachSpeed, false);
                    // Can fire even when approach is blocked, provided the muzzle sees Sonic.
                    if (seesSonic && sonicDistance <= firingDistance && !moving && Time.time >= nextShot
                        && Vector3.Angle(transform.forward, horizontal) < 15f && muzzle != null && projectilePrefab != null
                        && Visible(muzzle.position, AimPoint))
                    {
                        lockedAim = AimPoint; firedMask = 0; attackAge = 0f; mode = Mode.Attack;
                        animator.SetTrigger(Shoot);
                    }
                    if (muzzle == null || projectilePrefab == null) status = "Reference canon/projectile manquante";
                    break;
                case Mode.Attack:
                    status = "Preparation et salve"; attackAge += dt;
                    if (attackAge > 1.6f) FinishBuzzAttack();
                    break;
                case Mode.Return:
                    status = "Retour au parcours";
                    Vector3 target = HasPoint ? route.points[pointIndex].transform.position : home;
                    if (Vector3.Distance(body.position, target) <= arrivalDistance)
                    { mode = Mode.Patrol; arrived = false; }
                    else moving = MoveTo(target, approachSpeed, true);
                    break;
            }
            if (!available) status += " / Sonic non attribue";
            if (!string.IsNullOrEmpty(obstacle)) status += " / obstacle : " + obstacle;
            animator.SetBool(Flying, moving);
        }

        private void Face(Vector3 delta)
        {
            delta.y = 0;
            if (delta.sqrMagnitude > .001f)
                body.MoveRotation(Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(delta), turnSpeed * Time.fixedDeltaTime));
        }
        private bool MoveTo(Vector3 target, float speed, bool turn)
        {
            Vector3 delta = target - body.position;
            float length = Mathf.Min(delta.magnitude, Mathf.Max(.1f, speed) * Time.fixedDeltaTime);
            if (length < .0001f) return false;
            Vector3 dir = delta.normalized;
            if (turn) Face(dir);
            float scale = Mathf.Max(.01f, Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Max(Mathf.Abs(transform.lossyScale.y), Mathf.Abs(transform.lossyScale.z))));
            float radius = clearanceRadius * scale;
            foreach (var hit in Physics.SphereCastAll(body.position, radius, dir, length + .1f, environmentLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<PlayerBhysics>() != null) continue;
                obstacle = hit.collider.name; return false;
            }
            foreach (var col in Physics.OverlapSphere(body.position + dir * length, radius, environmentLayers, QueryTriggerInteraction.Ignore))
            {
                if (col.transform.IsChildOf(transform) || col.GetComponentInParent<PlayerBhysics>() != null) continue;
                obstacle = col.name; return false;
            }
            body.MovePosition(body.position + dir * length); return true;
        }

        // Animation events lock aim before the first shot, leaving Sonic time to dodge.
        public void FireBuzzProjectile(int index)
        {
            if (!isActiveAndEnabled || mode != Mode.Attack || index < 0 || index > 1 || (firedMask & (1 << index)) != 0) return;
            firedMask |= 1 << index;
            if (!Available() || muzzle == null || projectilePrefab == null || !Visible(muzzle.position, lockedAim)) return;
            LayerMask mask = environmentLayers;
            foreach (var col in player.GetComponentsInChildren<Collider>()) mask |= 1 << col.gameObject.layer;
            var shot = Instantiate(projectilePrefab, muzzle.position, Quaternion.identity);
            shot.Launch(transform, lockedAim, projectileSpeed, mask);
        }
        public void FinishBuzzAttack()
        {
            if (mode != Mode.Attack) return;
            mode = Mode.Engage; nextShot = Time.time + attackCooldown;
        }
        private void OnDisable() { if (animator != null) animator.ResetTrigger(Shoot); }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, noticeDistance);
            Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, firingDistance);
            if (Application.isPlaying && player != null)
            { Gizmos.color = seesSonic ? Color.green : Color.gray; Gizmos.DrawLine(transform.position, AimPoint); }
        }
    }
}
