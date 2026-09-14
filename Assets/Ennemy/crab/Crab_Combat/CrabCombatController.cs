using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(Animator))]
public class CrabCombatController : MonoBehaviour
{
    [Header("Patrouille")]
    [Min(0.1f)] public float patrolDistance = 4f;
    [Min(0f)] public float patrolSpeed = 0.8f;
    [Min(0f)] public float pauseMin = 0.8f;
    [Min(0f)] public float pauseMax = 2f;

    [Header("Detection de Sonic")]
    public PlayerBhysics player;
    [Min(0.1f)] public float noticeDistance = 22f;
    [Min(0.1f)] public float loseDistance = 30f;
    [Min(0.1f)] public float maximumChaseDistance = 32f;
    [Min(0f)] public float loseSightDelay = 2f;
    [Min(0f)] public float reactionTime = 0.3f;

    [Header("Approche et distance de tir")]
    [Min(0f)] public float approachSpeed = 1.6f;
    [Min(0.1f)] public float standOffDistance = 8f;
    [Min(0.1f)] public float tooCloseDistance = 5f;
    [Min(0.1f)] public float firingDistance = 11f;
    [Min(1f)] public float turnSpeed = 220f;

    [Header("Tirs en cloche")]
    public CrabLobProjectile projectilePrefab;
    public Transform leftMuzzle;
    public Transform rightMuzzle;
    [Min(0f)] public float attackCooldown = 1.8f;
    [Min(0.1f)] public float arcHeight = 3.5f;
    [Min(0.1f)] public float projectileGravity = 20f;
    [Min(0f), Tooltip("Petite anticipation du mouvement de Sonic, en secondes.")]
    public float predictionTime = 0.12f;
    [Min(0f)] public float maximumLead = 2f;

    [Header("Sol et obstacles")]
    [Tooltip("Couches du terrain et des murs. Exclure Player et Enemies.")]
    public LayerMask environmentLayers = 1;
    [Min(0.1f)] public float bodyRadius = 0.55f;
    [Min(0.1f)] public float stepHeight = 0.55f;
    [Min(0.1f)] public float maximumDrop = 0.65f;
    [Range(0f, 80f)] public float maximumSlope = 50f;
    [Min(0.1f)] public float gravity = 25f;
    [Min(0.01f)] public float animationReferenceSpeed = 0.65f;

    private enum Mode { Pause, Patrol, React, Engage, Attack, ReturnHome }
    private Mode mode;
    private Rigidbody body;
    private Animator anim;
    private Vector3 home, patrolAxis, destination;
    private int patrolSign = 1, firedMask;
    private float timer, unseenTime, nextShot, searchAt, attackElapsed;
    private RaycastHit ground;
    private bool grounded;
    private static readonly int Moving = Animator.StringToHash("Moving");
    private static readonly int WalkRate = Animator.StringToHash("WalkRate");
    private static readonly int Shoot = Animator.StringToHash("Shoot");

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
        body.useGravity = false;
        home = transform.position;
        patrolAxis = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        if (patrolAxis.sqrMagnitude < 0.5f) patrolAxis = Vector3.right;
        BeginPause();
    }

    private float Scale => Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));
    private Vector3 AimPoint => player.transform.position + Vector3.up * 0.65f;

    private bool PlayerAvailable()
    {
        if (player == null && Time.time >= searchAt)
        {
            searchAt = Time.time + 1f;
            GameObject obj = GameObject.FindGameObjectWithTag("Player");
            if (obj != null) player = obj.GetComponent<PlayerBhysics>();
        }
        if (player == null || !player.gameObject.activeInHierarchy) return false;
        var actions = player.GetComponent<ActionManager>();
        return actions == null || actions.Action04Control == null || !actions.Action04Control.isDead;
    }

    private bool CanSeePlayer()
    {
        Vector3 origin = body.position + Vector3.up * (1.6f * Scale);
        return !Physics.Linecast(origin, AimPoint, environmentLayers, QueryTriggerInteraction.Ignore);
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        float s = Scale;
        grounded = Physics.SphereCast(body.position + Vector3.up * (0.6f * s), 0.2f * s,
            Vector3.down, out ground, 0.65f * s, environmentLayers, QueryTriggerInteraction.Ignore)
            && ground.normal.y >= Mathf.Cos(maximumSlope * Mathf.Deg2Rad);
        bool available = PlayerAvailable();
        float distance = available ? Vector3.Distance(body.position, player.transform.position) : float.PositiveInfinity;
        bool visible = available && distance <= loseDistance && CanSeePlayer();
        Vector3 movement = Vector3.zero;
        float speed = 0f;
        if (visible) unseenTime = 0f; else unseenTime += dt;

        if (mode == Mode.Attack)
        {
            attackElapsed += dt;
            // Events normally finish the attack. This guard prevents a permanent lock with a changed controller.
            if (attackElapsed > 2.5f) FinishCrabAttack();
        }
        else if (mode == Mode.React || mode == Mode.Engage)
        {
            if (!available || distance > loseDistance || unseenTime > loseSightDelay
                || Vector3.Distance(body.position, home) > maximumChaseDistance)
                mode = Mode.ReturnHome;
        }
        else if (visible && distance <= noticeDistance && Vector3.Distance(body.position, home) <= maximumChaseDistance)
        {
            mode = Mode.React;
            timer = reactionTime;
        }

        switch (mode)
        {
            case Mode.Pause:
                timer -= dt;
                if (timer <= 0f)
                {
                    destination = home + patrolAxis * (patrolDistance * patrolSign);
                    mode = Mode.Patrol;
                }
                break;
            case Mode.Patrol:
                movement = Flat(destination - body.position);
                if (movement.magnitude < 0.25f) { patrolSign *= -1; BeginPause(); movement = Vector3.zero; }
                else { speed = patrolSpeed; Face(movement); }
                break;
            case Mode.ReturnHome:
                movement = Flat(home - body.position);
                if (movement.magnitude < 0.4f) { BeginPause(); movement = Vector3.zero; }
                else { speed = patrolSpeed; Face(movement); }
                break;
            case Mode.React:
                if (available) Face(player.transform.position - body.position);
                timer -= dt;
                if (timer <= 0f) mode = Mode.Engage;
                break;
            case Mode.Engage:
                if (!available) break;
                Vector3 toward = Flat(player.transform.position - body.position);
                float horizontalDistance = toward.magnitude;
                Face(toward);
                if (horizontalDistance > standOffDistance + 0.6f) { movement = toward; speed = approachSpeed; }
                else if (horizontalDistance < Mathf.Min(tooCloseDistance, standOffDistance - 0.5f)) { movement = -toward; speed = approachSpeed * 0.8f; }
                else if (visible && distance <= firingDistance && Time.time >= nextShot && grounded
                    && Vector3.Angle(transform.forward, toward) < 12f && projectilePrefab != null)
                {
                    mode = Mode.Attack; firedMask = 0; attackElapsed = 0f;
                    anim.SetBool(Moving, false);
                    anim.SetTrigger(Shoot);
                }
                break;
        }

        bool moved = MoveSafely(movement, speed);
        if (!moved && movement.sqrMagnitude > 0.01f && grounded && mode == Mode.Patrol)
        {
            patrolSign *= -1;
            BeginPause();
        }
        anim.SetBool(Moving, moved);
        anim.SetFloat(WalkRate, Mathf.Clamp(speed / Mathf.Max(0.01f, animationReferenceSpeed * s), 0.35f, 2.5f));
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    private void Face(Vector3 direction)
    {
        direction = Flat(direction);
        if (direction.sqrMagnitude < 0.001f) return;
        body.MoveRotation(Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(direction), turnSpeed * Time.fixedDeltaTime));
    }

    private void BeginPause()
    {
        mode = Mode.Pause;
        timer = Random.Range(Mathf.Max(0f, pauseMin), Mathf.Max(pauseMin, pauseMax));
    }

    private bool MoveSafely(Vector3 direction, float speed)
    {
        bool moving = grounded && direction.sqrMagnitude > 0.001f && speed > 0f;
        Vector3 desired = Vector3.zero;
        float s = Scale;
        if (moving)
        {
            direction.Normalize();
            float lookAhead = bodyRadius * s + Mathf.Max(0.15f, speed * Time.fixedDeltaTime);
            Vector3 ahead = body.position + direction * lookAhead;
            RaycastHit floor;
            bool safeFloor = Physics.Raycast(ahead + Vector3.up * (stepHeight * s), Vector3.down, out floor,
                (stepHeight + maximumDrop) * s, environmentLayers, QueryTriggerInteraction.Ignore)
                && floor.normal.y >= Mathf.Cos(maximumSlope * Mathf.Deg2Rad);
            RaycastHit wall;
            bool blocked = Physics.SphereCast(body.position + Vector3.up * (0.85f * s), bodyRadius * s * 0.85f,
                direction, out wall, speed * Time.fixedDeltaTime + 0.15f * s, environmentLayers, QueryTriggerInteraction.Ignore)
                && wall.normal.y < Mathf.Cos(maximumSlope * Mathf.Deg2Rad);
            moving = safeFloor && !blocked;
            if (moving) desired = Vector3.ProjectOnPlane(direction, ground.normal).normalized * speed;
        }
        if (grounded)
            body.linearVelocity = desired - ground.normal * 1.5f;
        else
            body.linearVelocity = new Vector3(0f, Mathf.Max(-40f, body.linearVelocity.y - gravity * Time.fixedDeltaTime), 0f);
        return moving;
    }

    // These methods are invoked by animation events, so the ball leaves during the claw recoil.
    public void FireCrabProjectile(int side)
    {
        if (!isActiveAndEnabled || mode != Mode.Attack || side < 0 || side > 1 || (firedMask & (1 << side)) != 0) return;
        firedMask |= 1 << side;
        if (!PlayerAvailable() || !CanSeePlayer() || Vector3.Distance(body.position, player.transform.position) > loseDistance) return;
        Transform muzzle = side == 0 ? leftMuzzle : rightMuzzle;
        if (muzzle == null || projectilePrefab == null) return;
        Vector3 lead = player.p_rigidbody != null ? player.p_rigidbody.linearVelocity * predictionTime : Vector3.zero;
        Vector3 target = AimPoint + Vector3.ClampMagnitude(lead, maximumLead);
        LayerMask layers = environmentLayers;
        foreach (var collider in player.GetComponentsInChildren<Collider>()) layers |= 1 << collider.gameObject.layer;
        CrabLobProjectile shot = Instantiate(projectilePrefab, muzzle.position, Quaternion.identity);
        shot.Launch(target, transform, projectileGravity, arcHeight, layers);
    }

    public void FinishCrabAttack()
    {
        if (mode != Mode.Attack) return;
        nextShot = Time.time + attackCooldown;
        mode = Mode.Engage;
    }

    private void OnDisable()
    {
        if (body != null && !body.isKinematic) body.linearVelocity = Vector3.zero;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.1f);
        Gizmos.DrawWireSphere(transform.position, noticeDistance);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, standOffDistance);
    }
}
