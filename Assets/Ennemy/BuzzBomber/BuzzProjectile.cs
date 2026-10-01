using UnityEngine;

namespace SonicFX.Buzz
{
    public class BuzzProjectile : MonoBehaviour
    {
        [Min(0.01f)] public float radius = 0.18f;
        [Min(0.1f)] public float lifetime = 5f;
        public GameObject impact;
        public Transform shell;
        private Transform owner;
        private Vector3 velocity;
        private LayerMask layers;
        private bool launched, spent;
        private float age;

        public void Launch(Transform source, Vector3 target, float speed, LayerMask mask)
        {
            owner = source; layers = mask; age = 0f; spent = false;
            Vector3 aim = target - transform.position;
            velocity = (aim.sqrMagnitude > 0.0001f ? aim.normalized : source.forward) * Mathf.Max(0.1f, speed);
            transform.rotation = Quaternion.LookRotation(velocity);
            launched = true;
            CheckOverlap();
        }
        private bool Valid(Collider other)
        {
            return other != null && !other.transform.IsChildOf(transform)
                && (owner == null || !other.transform.IsChildOf(owner))
                && (!other.isTrigger || other.GetComponentInParent<PlayerBhysics>() != null);
        }
        private bool CheckOverlap()
        {
            foreach (var other in Physics.OverlapSphere(transform.position, radius, layers, QueryTriggerInteraction.Collide))
                if (Valid(other)) { Hit(other, transform.position); return true; }
            return false;
        }
        private void FixedUpdate()
        {
            if (!launched || spent || CheckOverlap()) return;
            float dt = Time.fixedDeltaTime;
            float travel = velocity.magnitude * dt;
            float nearest = float.PositiveInfinity;
            RaycastHit chosen = default;
            bool found = false;
            foreach (var hit in Physics.SphereCastAll(transform.position, radius, velocity.normalized, travel, layers, QueryTriggerInteraction.Collide))
            {
                if (!Valid(hit.collider) || hit.distance >= nearest) continue;
                nearest = hit.distance; chosen = hit; found = true;
            }
            if (found) { Hit(chosen.collider, chosen.point); return; }
            transform.position += velocity * dt;
            if (shell != null) shell.Rotate(0f, 0f, 360f * dt, Space.Self);
            age += dt;
            if (age >= lifetime) { spent = true; Destroy(gameObject); }
        }
        private void Hit(Collider other, Vector3 point)
        {
            if (spent) return;
            spent = true;
            var player = other.GetComponentInParent<PlayerBhysics>();
            if (player != null)
            {
                var damage = player.GetComponent<Objects_Interaction>();
                if (damage == null) damage = player.GetComponentInChildren<Objects_Interaction>();
                if (damage != null)
                {
                    damage.DamagePlayer();
                    HedgeCamera.Shakeforce = damage.EnemyDamageShakeAmmount;
                }
            }
            if (impact != null) Instantiate(impact, point, Quaternion.identity);
            Destroy(gameObject);
        }
    }
}
