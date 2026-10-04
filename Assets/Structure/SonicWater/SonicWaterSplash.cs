using UnityEngine;

namespace SonicFX.Water
{
    public static class SonicWaterSplash
    {
        public static void Spawn(Vector3 position, Vector3 up, float size, Material material)
        {
            if (material == null) return;
            var go = new GameObject("Eclaboussure eau");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false; main.duration = 2;
            main.startLifetime = 1.5f; main.startSpeed = 0;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1.3f;
            main.maxParticles = 600;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var color = ps.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(0.55f,0.9f,1),0), new GradientColorKey(Color.white,1) },
                new[] { new GradientAlphaKey(0.9f,0), new GradientAlphaKey(0.8f,0.5f), new GradientAlphaKey(0,1) });
            color.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, up);
            ps.Play();
            int count = Mathf.Clamp(Mathf.RoundToInt(65 * size), 35, 350);
            for (int i = 0; i < count; i++)
            {
                float angle = Random.value * Mathf.PI * 2;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var particle = new ParticleSystem.EmitParams();
                particle.position = position + rotation * radial * Random.Range(0, 0.3f * size);
                particle.velocity = rotation * (radial * Random.Range(1, 3.5f) + Vector3.up * Random.Range(2, 5.5f)) * Mathf.Sqrt(size);
                particle.startSize = Random.Range(0.06f, 0.16f) * Mathf.Sqrt(size);
                particle.startLifetime = Random.Range(0.65f, 1.5f);
                ps.Emit(particle, 1);
            }
            Object.Destroy(go, 2.5f);
        }
    }
}
