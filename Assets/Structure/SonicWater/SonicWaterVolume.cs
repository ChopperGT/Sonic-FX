using System.Collections.Generic;
using UnityEngine;

namespace SonicFX.Water
{
    [ExecuteAlways, DisallowMultipleComponent]
    public class SonicWaterVolume : MonoBehaviour
    {
        public static readonly List<SonicWaterVolume> Active = new List<SonicWaterVolume>();
        [Header("Dimensions : origine au niveau de la surface")]
        [Min(1)] public float width = 30;
        [Min(1)] public float length = 30;
        [Min(0.5f)] public float depth = 100;
        [Header("Sonic sous l'eau")]
        [Range(0.1f, 1)] public float speedMultiplier = 0.55f;
        [Range(0.1f, 1)] public float gravityMultiplier = 0.8f;
        [Range(1, 1.5f)] public float jumpMultiplier = 1.05f;
        [Min(0)] public float horizontalDrag = 0.45f;
        [Min(1)] public float maximumFallSpeed = 18;
        [Header("Respiration")]
        [Min(1)] public float airSeconds = 30;
        [Min(1)] public float warningSeconds = 10;
        [Tooltip("Hauteur de la tete par rapport au pivot de Sonic, en unites du monde.")]
        [Min(0)] public float breathingHeight = 0.9f;
        public AudioClip drowningMusic;
        [Range(0, 1)] public float warningVolume = 0.8f;
        [Min(0)] public float musicStartTime;
        [Tooltip("Facultatif : musique du niveau. Sinon le mixeur Music du menu est utilise pour l'identifier.")]
        public AudioSource levelMusic;
        [Header("Eclaboussures")]
        public Material splashMaterial;
        [Min(0.1f)] public float splashMultiplier = 1;
        [Header("Filtre visuel sous-marin")]
        public bool underwaterVisuals = true;
        public Color underwaterTint = new Color(0.2f, 0.7f, 1f, 1f);
        [Range(0, 0.8f)] public float tintStrength = 0.3f;
        [Range(0, 0.02f)] public float waveDistortion = 0.004f;
        [Min(0.5f)] public float waveInterval = 5f;
        [Min(0.01f)] public float visualTransitionSeconds = 0.35f;
        [Header("Surface")]
        public Transform surface;
        static float nextPlayerSearch;

        void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
            RefreshSurface();
            nextPlayerSearch = 0;
        }
        void OnDisable() { Active.Remove(this); }
        void OnValidate()
        {
            width = Mathf.Max(1, width); length = Mathf.Max(1, length); depth = Mathf.Max(0.5f, depth);
            airSeconds = Mathf.Max(1, airSeconds); warningSeconds = Mathf.Clamp(warningSeconds, 1, airSeconds);
            RefreshSurface();
        }
        void Update()
        {
            RefreshSurface();
            if (!Application.isPlaying || Time.unscaledTime < nextPlayerSearch) return;
            nextPlayerSearch = Time.unscaledTime + 1;
            var camera = Camera.main;
            if (camera != null && camera.GetComponent<SonicUnderwaterEffect>() == null)
                camera.gameObject.AddComponent<SonicUnderwaterEffect>();
            foreach (var player in Object.FindObjectsByType<PlayerBhysics>())
                if (player.GetComponent<SonicWaterPlayer>() == null) player.gameObject.AddComponent<SonicWaterPlayer>();
        }
        void RefreshSurface()
        {
            if (surface == null) return;
            surface.localPosition = Vector3.zero;
            surface.localScale = new Vector3(width, 1, length);
        }
        public bool Contains(Vector3 world)
        {
            Vector3 p = transform.InverseTransformPoint(world);
            return Mathf.Abs(p.x) <= width * 0.5f && Mathf.Abs(p.z) <= length * 0.5f && p.y <= 0 && p.y >= -depth;
        }
        public bool CrossesSurface(Vector3 from, Vector3 to, out Vector3 point)
        {
            Vector3 a = transform.InverseTransformPoint(from), b = transform.InverseTransformPoint(to);
            point = to;
            if (a.y <= 0 || b.y > 0 || Mathf.Abs(a.y - b.y) < 0.00001f) return false;
            Vector3 hit = Vector3.Lerp(a, b, a.y / (a.y - b.y));
            if (Mathf.Abs(hit.x) > width * 0.5f || Mathf.Abs(hit.z) > length * 0.5f) return false;
            point = transform.TransformPoint(hit); return true;
        }
        public void Splash(Vector3 position, float fallingSpeed)
        {
            float size = Mathf.Clamp(0.6f + Mathf.Max(0, fallingSpeed) * 0.055f, 0.6f, 4.5f) * splashMultiplier;
            SonicWaterSplash.Spawn(position, transform.up, size, splashMaterial);
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.1f, 0.8f, 1, 0.9f);
            Gizmos.DrawWireCube(new Vector3(0, -depth * 0.5f, 0), new Vector3(width, depth, length));
        }
    }
}
