using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

// Prototype pour le kit PlayerBhysics + ActionManager de Sonic-FX.
// A placer sur Entree_Tube, enfant de l'objet possedant SplineContainer.
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(SphereCollider))]
public class SonicTube : MonoBehaviour
{
    [Header("Parcours")]
    public SplineContainer splineContainer;
    [Tooltip("Choisi automatiquement a l'ajout du composant selon l'entree la plus proche.")]
    public bool entranceAtEnd;
    [Tooltip("Repositionne l'entree quand les points du tube changent.")]
    public bool followSpline = true;

    [Header("Deplacement")]
    [Min(0.5f)] public float exitClearance = 2f;
    [Range(64, 4096)] public int pathSamples = 512;

    [Header("Effet des pentes")]
    [Tooltip("Intensite de la gravite le long du tube. 0 conserve la vitesse d'entree. Valeur de depart : 20.")]
    [Min(0f)] public float slopeAcceleration = 20f;
    [Tooltip("Vitesse minimale en montee pour terminer le trajet. Ne fait pas accelerer une entree plus lente.")]
    [Min(0.1f)] public float minimumSpeed = 2f;

    [Header("Entree")]
    [Tooltip("Cree une barriere physique invisible dans l'ouverture pendant le jeu.")]
    public bool blockStanding = true;
    [Tooltip("Rayon de la barriere physique. 0 conserve la taille initiale de la detection. Les poignees de detection preservent cette taille pour ne pas agrandir l'obstacle invisible.")]
    [Min(0f)] public float standingBarrierRadius;
    [Tooltip("Autorise aussi le saut en boule du kit (Action01 avec JumpBall actif).")]
    public bool allowJumpBall = true;
    public bool logEvents = true;

    SphereCollider sensor;
    float initialBarrierRadius;
    GameObject gate;
    BoxCollider gateCollider;
    PlayerBhysics player;
    ActionManager actions;
    Rigidbody body;
    Animator animator;
    readonly List<Behaviour> paused = new List<Behaviour>();
    readonly List<Vector3> points = new List<Vector3>();
    readonly List<Vector3> ups = new List<Vector3>();
    readonly List<float> distances = new List<float>();
    int segment;
    float distance;
    // Capture actual velocity before the body becomes kinematic.
    float entrySpeed;
    float currentSpeed;
    float rideMinimumSpeed;
    Vector3 savedPosition, savedVelocity, savedAngularVelocity;
    Quaternion savedRotation;
    bool savedCollisions, savedGravity, savedRolling, savedGrounded;
    Vector3 savedNormal;
    CollisionDetectionMode savedDetection;
    RigidbodyInterpolation savedInterpolation;
    bool active;
    int savedAction;
    SonicTubeCamera tubeCamera;

    static readonly HashSet<Rigidbody> Riders = new HashSet<Rigidbody>();
    static readonly Dictionary<Rigidbody, float> Cooldowns = new Dictionary<Rigidbody, float>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearSession() { Riders.Clear(); Cooldowns.Clear(); }

    void Reset()
    {
        sensor = GetComponent<SphereCollider>();
        sensor.isTrigger = true;
        splineContainer = GetComponentInParent<SplineContainer>();
        ChooseNearestEntrance();
    }

    [ContextMenu("Choisir l'extremite la plus proche")]
    public void ChooseNearestEntrance()
    {
        if (!ValidPath()) return;
        Vector3 first = splineContainer.EvaluatePosition(0f);
        Vector3 last = splineContainer.EvaluatePosition(1f);
        entranceAtEnd = (transform.position - last).sqrMagnitude <
                        (transform.position - first).sqrMagnitude;
        AlignEntrance();
    }

    bool ValidPath()
    {
        return splineContainer != null && splineContainer.Splines.Count > 0 &&
               splineContainer.Spline.Count >= 2 && !splineContainer.Spline.Closed &&
               splineContainer.transform != transform;
    }

    void OnEnable()
    {
        sensor = GetComponent<SphereCollider>();
        initialBarrierRadius = sensor.radius;
        if (splineContainer == null) splineContainer = GetComponentInParent<SplineContainer>();
        if (Application.IsPlaying(gameObject)) CreateGate();
    }

    void Start()
    {
        if (!Application.IsPlaying(gameObject)) return;
        if (!ValidPath() || !sensor.isTrigger)
        {
            Debug.LogError("SonicTube : placer ce script sur Entree_Tube, avec une Sphere Collider Is Trigger et un Spline Container parent contenant une courbe ouverte.", this);
            enabled = false;
        }
    }

    void Update()
    {
        if (followSpline && !active) AlignEntrance();
        if (gate != null) UpdateGate();
    }

    void AlignEntrance()
    {
        if (!ValidPath()) return;
        float t = entranceAtEnd ? 1f : 0f;
        Vector3 forward = (Vector3)splineContainer.EvaluateTangent(t) * (entranceAtEnd ? -1f : 1f);
        if (forward.sqrMagnitude < 0.000001f) return;
        Vector3 up = splineContainer.EvaluateUpVector(t);
        transform.SetPositionAndRotation(splineContainer.EvaluatePosition(t), SafeRotation(forward, up));
    }

    static Quaternion SafeRotation(Vector3 forward, Vector3 up)
    {
        if (forward.sqrMagnitude < 0.000001f) return Quaternion.identity;
        forward.Normalize();
        up = Vector3.ProjectOnPlane(up, forward);
        if (up.sqrMagnitude < 0.000001f)
            up = Vector3.ProjectOnPlane(Mathf.Abs(forward.y) < 0.9f ? Vector3.up : Vector3.right, forward);
        return Quaternion.LookRotation(forward, up.normalized);
    }

    void CreateGate()
    {
        if (gate != null || !blockStanding) return;
        gate = new GameObject("Barriere_Entree_Automatique");
        gate.layer = gameObject.layer;
        gate.transform.SetParent(transform, false);
        gateCollider = gate.AddComponent<BoxCollider>();
        UpdateGate();
    }

    void UpdateGate()
    {
        // Solid plate slightly behind the trigger centre. Only the captured rider
        // disables its own collisions; the gate stays solid for other characters.
        float radius = standingBarrierRadius > 0f ? standingBarrierRadius : initialBarrierRadius;
        gateCollider.size = new Vector3(radius * 2f, radius * 2f, 0.15f);
        gateCollider.center = sensor.center + Vector3.forward * 0.15f;
        gateCollider.enabled = enabled && blockStanding;
    }

    void OnTriggerEnter(Collider other) { TryEnter(other); }
    void OnTriggerStay(Collider other) { TryEnter(other); }

    void TryEnter(Collider other)
    {
        // Trigger callbacks can be delivered to disabled MonoBehaviours.
        if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled || active || !ValidPath()) return;
        var candidateBody = other.attachedRigidbody;
        if (candidateBody == null || candidateBody.isKinematic || Riders.Contains(candidateBody)) return;
        if (Cooldowns.TryGetValue(candidateBody, out float until) && Time.time < until) return;
        var candidate = candidateBody.GetComponent<PlayerBhysics>();
        var manager = candidateBody.GetComponent<ActionManager>();
        if (candidate == null || !candidate.enabled ||
            manager == null || manager.Action00 == null ||
            manager.Action00.CharacterAnimator == null) return;
        bool rolling = candidate.isRolling && manager.Action == 0;
        bool jumpingBall = allowJumpBall && manager.Action == 1 && manager.Action01 != null &&
                           manager.Action01.JumpBall != null && manager.Action01.JumpBall.activeInHierarchy;
        if (!rolling && !jumpingBall) return;
        Vector3 forward = transform.forward;
        if (Vector3.Dot(candidateBody.linearVelocity, forward) < 0.1f) return;
        if (Vector3.Dot(candidateBody.position - transform.position, forward) > 0.6f) return;
        if (!BuildPath(candidateBody.position)) return;

        player = candidate;
        body = candidateBody;
        actions = manager;
        animator = manager.Action00.CharacterAnimator;
        savedPosition = body.position;
        savedRotation = body.rotation;
        savedVelocity = body.linearVelocity;
        entrySpeed = savedVelocity.magnitude;
        currentSpeed = entrySpeed;
        rideMinimumSpeed = Mathf.Min(entrySpeed, Mathf.Max(0.1f, minimumSpeed));
        savedAngularVelocity = body.angularVelocity;
        savedCollisions = body.detectCollisions;
        savedGravity = body.useGravity;
        savedDetection = body.collisionDetectionMode;
        savedInterpolation = body.interpolation;
        savedRolling = player.isRolling;
        savedGrounded = player.Grounded;
        savedNormal = player.GroundNormal;
        savedAction = actions.Action;
        Riders.Add(body);
        active = true;
        segment = 0;
        distance = 0;

        // Use the existing manager to stop action scripts, then temporarily pause
        // movement writers. Visual follow scripts and the camera remain active.
        actions.ChangeAction(-1);
        paused.Clear();
        Pause(player);
        Pause(body.GetComponent<PlayerBinput>());
        Pause(body.GetComponent<Objects_Interaction>());
        Pause(body.GetComponent<Rail_Interaction>());
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.detectCollisions = false;
        body.useGravity = false;
        body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        KeepBall();
        tubeCamera = SonicTubeCamera.BeginRide(this, player, animator);
        if (logEvents) Debug.Log($"SonicTube : entree en boule acceptee a {entrySpeed:F2} unites/s.", this);
    }

    void Pause(Behaviour component)
    {
        if (component == null || !component.enabled) return;
        paused.Add(component);
        component.enabled = false;
    }

    bool BuildPath(Vector3 initialPosition)
    {
        points.Clear(); ups.Clear(); distances.Clear();
        AddPoint(initialPosition, transform.up);
        int count = Mathf.Clamp(pathSamples, 64, 4096);
        for (int i = 0; i <= count; i++)
        {
            float t = (float)i / count;
            if (entranceAtEnd) t = 1f - t;
            Vector3 position = splineContainer.EvaluatePosition(t);
            if (!Finite(position)) return false;
            AddPoint(position, splineContainer.EvaluateUpVector(t));
        }
        if (points.Count < 3 || distances[distances.Count - 1] < 0.1f) return false;
        Vector3 outgoing = (points[points.Count - 1] - points[points.Count - 2]).normalized;
        AddPoint(points[points.Count - 1] + outgoing * Mathf.Max(0.5f, exitClearance), ups[ups.Count - 1]);
        return true;
    }

    static bool Finite(Vector3 v)
    {
        return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
               !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
    }

    void AddPoint(Vector3 position, Vector3 up)
    {
        float length = points.Count == 0 ? 0f : Vector3.Distance(points[points.Count - 1], position);
        if (points.Count > 0 && length < 0.00001f) return;
        distances.Add(points.Count == 0 ? 0f : distances[distances.Count - 1] + length);
        points.Add(position);
        ups.Add(up);
    }

    void FixedUpdate()
    {
        if (!Application.IsPlaying(gameObject) || !active) return;
        if (player == null || body == null || !player.gameObject.activeInHierarchy ||
            actions == null || actions.Action != -1)
        {
            Release(false);
            return;
        }
        if (AdvanceAlongPath(Time.fixedDeltaTime)) { Release(true); return; }
        float t = Mathf.InverseLerp(distances[segment], distances[segment + 1], distance);
        Vector3 direction = points[segment + 1] - points[segment];
        Vector3 up = Vector3.Slerp(ups[segment], ups[segment + 1], t);
        body.MovePosition(Vector3.Lerp(points[segment], points[segment + 1], t));
        body.MoveRotation(SafeRotation(direction, up));
        player.GroundNormal = up;
        KeepBall();
    }

    bool AdvanceAlongPath(float deltaTime)
    {
        double remainingTime = deltaTime;
        // Integrate each crossed segment separately so fast riders also feel
        // short hills. The analytical step avoids dependence on FixedUpdate rate.
        while (remainingTime > 0 && segment < points.Count - 1)
        {
            float available = distances[segment + 1] - distance;
            if (available <= 0f) { segment++; continue; }
            Vector3 tangent = (points[segment + 1] - points[segment]).normalized;
            double acceleration = -Mathf.Max(0f, slopeAcceleration) * tangent.y;
            double elapsed = SonicTubeSlopeMotion.Step(currentSpeed, acceleration,
                rideMinimumSpeed, available, remainingTime, out double moved, out double speed);
            currentSpeed = (float)speed;
            distance = Mathf.Min(distances[segment + 1], distance + (float)moved);
            remainingTime = System.Math.Max(0, remainingTime - elapsed);
            if (moved >= available || distance >= distances[segment + 1])
            {
                distance = distances[segment + 1];
                segment++;
            }
            else break;
        }
        return segment >= points.Count - 1;
    }

    void LateUpdate()
    {
        if (!Application.IsPlaying(gameObject) || !active || body == null || animator == null) return;
        KeepBall();
        animator.transform.rotation = player.transform.rotation;
    }

    void KeepBall()
    {
        if (player != null)
        {
            player.isRolling = true;
            player.Grounded = false;
            player.SpeedMagnitude = currentSpeed;
        }
        if (animator == null) return;
        animator.SetInteger("Action", 1);
        animator.SetBool("isRolling", true);
        animator.SetBool("Grounded", false);
        animator.SetFloat("GroundSpeed", currentSpeed);
    }

    // Camera follows the route, not the spinning animated skin. Offsets follow
    // the curve too, which keeps the close camera inside ordinary tube bends.
    public bool TryGetTubeCameraPose(float trailingDistance, float height, float lookAhead,
        out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        if (!active || player == null || points.Count < 2) return false;
        SampleCameraPath(distance, out Vector3 rider, out Vector3 riderUp);
        Vector3 interpolationOffset = player.transform.position - rider;
        SampleCameraPath(distance - Mathf.Max(0f, trailingDistance), out Vector3 rear, out Vector3 rearUp);
        SampleCameraPath(distance + Mathf.Max(0.1f, lookAhead), out Vector3 ahead, out Vector3 aheadUp);
        position = rear + rearUp * height + interpolationOffset;
        Vector3 target = ahead + aheadUp * height + interpolationOffset;
        rotation = SafeRotation(target - position, riderUp);
        return true;
    }

    void SampleCameraPath(float arcDistance, out Vector3 position, out Vector3 up)
    {
        int last = points.Count - 1;
        if (arcDistance <= 0f)
        {
            position = points[0] + (points[1] - points[0]).normalized * arcDistance;
            up = ups[0];
            return;
        }
        if (arcDistance >= distances[last])
        {
            position = points[last] + (points[last] - points[last - 1]).normalized * (arcDistance - distances[last]);
            up = ups[last];
            return;
        }
        int low = 0, high = last;
        while (high - low > 1)
        {
            int mid = (low + high) / 2;
            if (distances[mid] <= arcDistance) low = mid;
            else high = mid;
        }
        float t = Mathf.InverseLerp(distances[low], distances[high], arcDistance);
        position = Vector3.Lerp(points[low], points[high], t);
        up = Vector3.Slerp(ups[low], ups[high], t).normalized;
    }

    void Release(bool completed)
    {
        if (!active) return;
        if (tubeCamera != null) tubeCamera.EndRide(this);
        tubeCamera = null;
        active = false;
        // Missing/deactivated path or cancelled traversal returns to its starting
        // position instead of abandoning a frozen player inside the tube.
        if (body != null)
        {
            Vector3 outgoing = points.Count >= 2 ?
                (points[points.Count - 1] - points[points.Count - 2]).normalized : transform.forward;
            body.position = completed ? points[points.Count - 1] : savedPosition;
            body.rotation = completed ? SafeRotation(outgoing, ups[ups.Count - 1]) : savedRotation;
            body.isKinematic = false;
            body.collisionDetectionMode = savedDetection;
            body.useGravity = savedGravity;
            body.interpolation = savedInterpolation;
            body.detectCollisions = savedCollisions;
            body.linearVelocity = completed ? outgoing * currentSpeed : savedVelocity;
            body.angularVelocity = savedAngularVelocity;
            Riders.Remove(body);
            Cooldowns[body] = Time.time + 0.75f;
        }
        if (player != null)
        {
            player.Grounded = completed ? false : savedGrounded;
            player.GroundNormal = completed ? Vector3.up : savedNormal;
            player.WasOnAir = completed;
            // Releasing R1 inside the tube must not leave a stuck roll afterwards.
            player.isRolling = savedRolling && PadInput.GetButton("R1");
            if (body != null) player.SpeedMagnitude = body.linearVelocity.magnitude;
        }
        foreach (var component in paused) if (component != null) component.enabled = true;
        paused.Clear();
        // Do not replace a hurt/death action that interrupted the ride.
        if (actions != null && actions.Action == -1)
        {
            actions.ChangeAction(completed ? 0 : savedAction);
            if (completed && actions.Action01 != null && actions.Action01.JumpBall != null)
                actions.Action01.JumpBall.SetActive(false);
        }
        if (animator != null && player != null) animator.SetBool("isRolling", player.isRolling);
        if (logEvents) Debug.Log(completed ? $"SonicTube : sortie a {currentSpeed:F2} unites/s, commandes restituees." : "SonicTube : trajet interrompu, commandes restituees.", this);
        body = null; player = null; actions = null; animator = null;
    }

    void OnDisable()
    {
        Release(false);
        if (gateCollider != null) gateCollider.enabled = false;
    }

    void OnDestroy()
    {
        Release(false);
        if (gate != null) Destroy(gate);
    }
}

// Pure numerical integration, separately testable without launching Unity.
internal static class SonicTubeSlopeMotion
{
    internal static double Step(double speed, double acceleration, double minimumSpeed,
        double segmentDistance, double timeBudget, out double moved, out double finalSpeed)
    {
        double accelerationTime = timeBudget;
        if (acceleration < 0)
            accelerationTime = System.Math.Min(timeBudget, System.Math.Max(0, (minimumSpeed - speed) / acceleration));
        double acceleratingDistance = speed * accelerationTime +
            0.5 * acceleration * accelerationTime * accelerationTime;
        double fullDistance = acceleratingDistance + minimumSpeed * (timeBudget - accelerationTime);
        double elapsed = timeBudget;
        if (fullDistance >= segmentDistance)
        {
            if (segmentDistance <= acceleratingDistance)
            {
                double endSpeed = System.Math.Sqrt(System.Math.Max(0,
                    speed * speed + 2 * acceleration * segmentDistance));
                elapsed = 2 * segmentDistance / (speed + endSpeed);
            }
            else
                elapsed = accelerationTime + (segmentDistance - acceleratingDistance) / minimumSpeed;
            moved = segmentDistance;
        }
        else moved = fullDistance;
        finalSpeed = System.Math.Max(minimumSpeed,
            speed + acceleration * System.Math.Min(elapsed, accelerationTime));
        return System.Math.Min(timeBudget, elapsed);
    }
}
