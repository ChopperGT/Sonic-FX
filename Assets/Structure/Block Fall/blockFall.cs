using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FallingPlatform : MonoBehaviour
{
    [SerializeField, Min(0)] private float fallDelay = 1.5f;
    [Tooltip("Couches des terrains et blocs qui detruisent la plateforme pendant sa chute.")]
    [SerializeField] private LayerMask impactLayers = ~0;
    [Tooltip("Descente minimale avant de detruire le bloc a l'atterrissage. Evite sa disparition au contact initial du terrain.")]
    [SerializeField, Min(0.01f)] private float minimumFallDistance = .25f;

    private Rigidbody rb;
    private bool triggered;
    private bool falling;
    private bool destroyed;
    private float fallStartY;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (falling)
        {
            BreakOnImpact(collision);
            return;
        }
        if (triggered || destroyed || !IsPlayer(collision.collider)) return;
        triggered = true;
        StartCoroutine(FallAfterDelay());
    }

    private void OnCollisionStay(Collision collision)
    {
        // A standing player may already be in contact when the block is enabled.
        if (!falling) { OnCollisionEnter(collision); return; }
        BreakOnImpact(collision);
    }

    private bool ShouldBreak(Collider other)
    {
        if (!falling || destroyed || fallStartY - rb.position.y < minimumFallDistance || other == null || other.isTrigger) return false;
        if (other.transform.IsChildOf(transform)) return false;
        if ((impactLayers.value & (1 << other.gameObject.layer)) == 0) return false;
        // Sonic standing on or colliding with the block must not destroy it.
        if (IsPlayer(other)) return false;
        if (other.GetComponentInParent<EnemyHealth>() != null) return false;
        return true;
    }

    private static bool IsPlayer(Collider other)
    {
        if (other == null) return false;
        if (other.GetComponentInParent<PlayerBhysics>() != null) return true;
        for (Transform parent = other.transform; parent != null; parent = parent.parent)
            if (parent.CompareTag("Player")) return true;
        return false;
    }

    private void BreakOnImpact(Collision collision)
    {
        if (!ShouldBreak(collision.collider)) return;
        // Brushing the vertical edge of the starting terrain is not a landing.
        bool landed = false;
        for (int i = 0; i < collision.contactCount; i++)
            if (Vector3.Dot(collision.GetContact(i).normal, Vector3.up) > .35f) { landed = true; break; }
        if (!landed) return;
        destroyed = true;
        // Remove collision immediately so the landing block cannot obstruct Sonic.
        foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        foreach (var renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = false;
        Destroy(gameObject);
    }

    private IEnumerator FallAfterDelay()
    {
        yield return new WaitForSeconds(fallDelay);
        fallStartY = rb.position.y;
        falling = true;
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }
}
