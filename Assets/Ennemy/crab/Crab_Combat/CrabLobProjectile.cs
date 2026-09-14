using UnityEngine;

public class CrabLobProjectile : MonoBehaviour
{
    [Min(0.01f)] public float collisionRadius = 0.2f;
    public Transform spinningShell;
    public GameObject impactEffect;
    public TrailRenderer trail;
    [Min(0.1f)] public float maximumLife = 8f;
    private Vector3 origin, velocity;
    private float gravity, age;
    private Transform owner;
    private LayerMask hitLayers;
    private bool launched, consumed;

    public void Launch(Vector3 target, Transform shooter, float gravityValue, float arcHeight, LayerMask layers)
    {
        origin = transform.position;
        owner = shooter;
        gravity = gravityValue;
        hitLayers = layers;
        float flightTime;
        velocity = CrabBallistics.LaunchVelocity(origin, target, gravity, arcHeight, out flightTime);
        age = 0f;
        consumed = false;
        launched = true;
        if (trail != null) trail.Clear();
        // A muzzle already inside a wall must not shoot through it.
        foreach (var col in Physics.OverlapSphere(origin, collisionRadius, hitLayers, QueryTriggerInteraction.Collide))
        {
            if (!ValidHit(col)) continue;
            Impact(col, origin);
            break;
        }
    }

    private void Update()
    {
        if (spinningShell != null && launched)
            spinningShell.Rotate(new Vector3(170f, 260f, 90f) * Time.deltaTime, Space.Self);
    }

    private void FixedUpdate()
    {
        if (!launched || consumed) return;
        float remaining = Time.fixedDeltaTime;
        while (remaining > 0f && !consumed)
        {
            float step = Mathf.Min(remaining, 0.02f);
            Vector3 from = CrabBallistics.Position(origin, velocity, gravity, age);
            // Also handle Sonic moving onto the ball between two physics steps.
            foreach (var col in Physics.OverlapSphere(from, collisionRadius, hitLayers, QueryTriggerInteraction.Collide))
            {
                if (!ValidHit(col)) continue;
                Impact(col, from);
                return;
            }
            age += step;
            Vector3 to = CrabBallistics.Position(origin, velocity, gravity, age);
            Vector3 segment = to - from;
            float length = segment.magnitude;
            if (length > 0.00001f)
            {
                var hits = Physics.SphereCastAll(from, collisionRadius, segment / length, length,
                    hitLayers, QueryTriggerInteraction.Collide);
                float closest = float.PositiveInfinity;
                RaycastHit selected = default;
                bool found = false;
                foreach (var hit in hits)
                {
                    if (hit.distance >= closest || !ValidHit(hit.collider)) continue;
                    closest = hit.distance; selected = hit; found = true;
                }
                if (found) { Impact(selected.collider, selected.point); return; }
            }
            transform.position = to;
            remaining -= step;
            if (age >= maximumLife) { consumed = true; Destroy(gameObject); }
        }
    }

    private bool ValidHit(Collider col)
    {
        if (col == null || col.transform.IsChildOf(transform)) return false;
        if (owner != null && col.transform.IsChildOf(owner)) return false;
        // Trigger zones in the level are not walls; Sonic's trigger colliders can receive damage.
        return !col.isTrigger || col.GetComponentInParent<PlayerBhysics>() != null;
    }

    private void Impact(Collider col, Vector3 point)
    {
        if (consumed) return;
        consumed = true;
        transform.position = point;
        var player = col.GetComponentInParent<PlayerBhysics>();
        if (player != null)
        {
            var interaction = player.GetComponent<Objects_Interaction>();
            if (interaction == null) interaction = player.GetComponentInChildren<Objects_Interaction>();
            if (interaction != null)
            {
                interaction.DamagePlayer();
                HedgeCamera.Shakeforce = interaction.EnemyDamageShakeAmmount;
            }
        }
        if (impactEffect != null) Instantiate(impactEffect, point, Quaternion.identity);
        Destroy(gameObject);
    }
}
