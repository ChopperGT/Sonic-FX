using System.Collections.Generic;
using UnityEngine;

namespace SonicFX.Lava
{
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(-150)]
    public class SonicLavaVolume : MonoBehaviour
    {
        [Header("Dimensions : pivot au niveau de la surface")]
        [Min(.1f), InspectorName("Largeur (X)")] public float width = 30;
        [Min(.1f), InspectorName("Longueur (Z)")] public float length = 30;
        [Min(.1f), InspectorName("Profondeur")] public float depth = 20;
        [Header("Aspect de la lave")]
        public Color crustColor = new Color(.12f, .025f, .016f);
        public Color lavaColor = new Color(1, .18f, .015f);
        public Color hotColor = new Color(1, .85f, .08f);
        [Min(.05f), InspectorName("Taille du motif")] public float patternScale = .65f;
        [Min(0), InspectorName("Vitesse d'animation")] public float flowSpeed = .3f;
        [Range(0, 6), InspectorName("Intensite lumineuse")] public float glow = 2.2f;
        [Header("References deja configurees")]
        public Transform surface;
        public BoxCollider lethalVolume;
        public int DeathsTriggered { get; private set; }

        struct Shape
        {
            public Vector3 a, b;
            public float radius;
        }
        readonly Dictionary<Collider, Shape> previous = new Dictionary<Collider, Shape>();
        readonly List<Collider> seen = new List<Collider>();
        readonly RaycastHit[] hits = new RaycastHit[128];
        PlayerBhysics[] players = new PlayerBhysics[0];
        float nextSearch;
        MaterialPropertyBlock properties;
        Renderer surfaceRenderer;

        void OnEnable() { nextSearch = 0; previous.Clear(); Refresh(); }
        void OnDisable() { previous.Clear(); }
        void OnValidate()
        {
            width = Mathf.Max(.1f, width); length = Mathf.Max(.1f, length);
            depth = Mathf.Max(.1f, depth); patternScale = Mathf.Max(.05f, patternScale);
        }
        void Update() { Refresh(); }
        public void Refresh()
        {
            if (surface != null)
            {
                surface.localPosition = Vector3.zero;
                surface.localScale = new Vector3(width, 1, length);
                if (surfaceRenderer == null) surfaceRenderer = surface.GetComponent<Renderer>();
                if (surfaceRenderer != null)
                {
                    if (properties == null) properties = new MaterialPropertyBlock();
                    properties.SetColor("_CrustColor", crustColor);
                    properties.SetColor("_LavaColor", lavaColor);
                    properties.SetColor("_HotColor", hotColor);
                    properties.SetFloat("_PatternScale", patternScale);
                    properties.SetFloat("_FlowSpeed", flowSpeed);
                    properties.SetFloat("_Glow", glow);
                    surfaceRenderer.SetPropertyBlock(properties);
                }
            }
            if (lethalVolume != null)
            {
                lethalVolume.isTrigger = true;
                lethalVolume.center = new Vector3(0, -depth * .5f, 0);
                lethalVolume.size = new Vector3(width, depth, length);
            }
        }
        public bool Contains(Vector3 world)
        {
            Vector3 p = transform.InverseTransformPoint(world);
            return Mathf.Abs(p.x) <= width * .5f && Mathf.Abs(p.z) <= length * .5f && p.y <= 0 && p.y >= -depth;
        }
        void FixedUpdate()
        {
            if (!Application.isPlaying) return;
            if (Time.time >= nextSearch)
            {
                players = Object.FindObjectsByType<PlayerBhysics>(); nextSearch = Time.time + .25f;
            }
            foreach (var player in players) CheckPlayer(player);
        }
        void OnTriggerEnter(Collider other) { CheckPlayer(other.GetComponentInParent<PlayerBhysics>()); }
        void OnTriggerStay(Collider other) { CheckPlayer(other.GetComponentInParent<PlayerBhysics>()); }

        // Uses the player's physical colliders, rather than its larger interaction triggers.
        // The sweep also catches a complete crossing between two physics frames.
        public bool CheckPlayer(PlayerBhysics player)
        {
            if (player == null || lethalVolume == null || !isActiveAndEnabled || !lethalVolume.enabled || !player.gameObject.activeInHierarchy || player.gameObject.scene.GetPhysicsScene() != gameObject.scene.GetPhysicsScene()) return false;
            var hurt = player.GetComponent<HurtControl>();
            if (hurt == null || hurt.isDead)
            {
                foreach (var col in player.GetComponentsInChildren<Collider>()) previous.Remove(col);
                return false;
            }
            seen.Clear(); bool touched = false;
            foreach (var col in player.GetComponentsInChildren<Collider>())
            {
                if (!col.enabled || col.isTrigger || !col.gameObject.activeInHierarchy) continue;
                seen.Add(col);
                if (Physics.ComputePenetration(col, col.transform.position, col.transform.rotation, lethalVolume, lethalVolume.transform.position, lethalVolume.transform.rotation, out _, out _)) touched = true;
                Shape shape = ColliderShape(col);
                if (!touched && previous.TryGetValue(col, out var old))
                {
                    Vector3 motion = (shape.a + shape.b - old.a - old.b) * .5f;
                    float distance = motion.magnitude;
                    if (distance > .00001f)
                    {
                        int count = gameObject.scene.GetPhysicsScene().CapsuleCast(old.a, old.b, Mathf.Max(.001f, old.radius), motion / distance, hits, distance, 1 << gameObject.layer, QueryTriggerInteraction.Collide);
                        for (int i = 0; i < count; i++) if (hits[i].collider == lethalVolume) { touched = true; break; }
                    }
                }
                previous[col] = shape;
            }
            // Remove old colliders when a skin/collider is replaced or disabled.
            var stale = new List<Collider>();
            foreach (var pair in previous) if (pair.Key == null || (pair.Key.GetComponentInParent<PlayerBhysics>() == player && !seen.Contains(pair.Key))) stale.Add(pair.Key);
            foreach (var col in stale) previous.Remove(col);
            return touched && Kill(player);
        }
        static Shape ColliderShape(Collider col)
        {
            Vector3 scale = col.transform.lossyScale; scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            if (col is SphereCollider sphere)
            {
                Vector3 center = col.transform.TransformPoint(sphere.center);
                return new Shape { a = center, b = center, radius = sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)) };
            }
            if (col is CapsuleCollider capsule)
            {
                Vector3 center = col.transform.TransformPoint(capsule.center);
                Vector3 axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward;
                float axialScale = scale[capsule.direction];
                float radialScale = capsule.direction == 0 ? Mathf.Max(scale.y, scale.z) : capsule.direction == 1 ? Mathf.Max(scale.x, scale.z) : Mathf.Max(scale.x, scale.y);
                float radius = capsule.radius * radialScale;
                Vector3 extent = col.transform.TransformDirection(axis).normalized * Mathf.Max(0, capsule.height * axialScale * .5f - radius);
                return new Shape { a = center + extent, b = center - extent, radius = radius };
            }
            // Fallback for custom pack characters using a different physical collider.
            Bounds bounds = col.bounds;
            return new Shape { a = bounds.center, b = bounds.center, radius = Mathf.Min(bounds.extents.x, Mathf.Min(bounds.extents.y, bounds.extents.z)) };
        }
        public bool Kill(PlayerBhysics player)
        {
            var hurt = player != null ? player.GetComponent<HurtControl>() : null;
            if (hurt == null || hurt.isDead) return false;
            // HurtControl's normal death path counts exactly one life, records the death,
            // respawns at the checkpoint, and handles game over. Rings and shields cannot save him.
            hurt.isDead = true; DeathsTriggered++;
            var interaction = player.GetComponent<Objects_Interaction>();
            if (interaction != null && interaction.Sounds != null && interaction.Sounds.Source3 != null) interaction.Sounds.DieSound();
            var actions = player.GetComponent<ActionManager>();
            if (actions != null)
            {
                if (actions.Action01 != null && actions.Action01.JumpBall != null) actions.Action01.JumpBall.SetActive(false);
                if (actions.Action04 != null && player.p_rigidbody != null) { actions.ChangeAction(4); actions.Action04.InitialEvents(); }
            }
            return true;
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix; Gizmos.color = new Color(1, .3f, .02f);
            Gizmos.DrawWireCube(new Vector3(0, -depth * .5f, 0), new Vector3(width, depth, length));
        }
    }
}
