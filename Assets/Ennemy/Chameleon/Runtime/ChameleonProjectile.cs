using UnityEngine;
namespace SonicFX.Chameleon
{
    public sealed class ChameleonProjectile : MonoBehaviour
    {
        public float radius = .18f, lifetime = 5;
        Transform owner; Vector3 velocity; float age;
        public void Launch(Transform source, Vector3 speed) { owner = source; velocity = speed; }
        bool Valid(Collider c) => (owner == null || !c.transform.IsChildOf(owner)) && !c.transform.IsChildOf(transform) && (!c.isTrigger || c.GetComponentInParent<PlayerBhysics>() != null);
        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime; age += dt;
            if (age >= lifetime) { Destroy(gameObject); return; }
            foreach (var c in Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Collide)) if (Valid(c)) { Impact(c); return; }
            Vector3 step = velocity * dt; Collider contact = null; float nearest = step.magnitude;
            foreach (var h in Physics.SphereCastAll(transform.position, radius, step.normalized, step.magnitude, ~0, QueryTriggerInteraction.Collide))
                if (Valid(h.collider) && h.distance <= nearest) { nearest = h.distance; contact = h.collider; }
            transform.position += step.normalized * nearest;
            if (contact != null) Impact(contact);
        }
        void Impact(Collider c) { ChameleonController.Damage(c); Destroy(gameObject); }
    }
}
