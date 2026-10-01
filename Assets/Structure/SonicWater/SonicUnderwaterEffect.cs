using UnityEngine;

namespace SonicFX.Water
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public class SonicUnderwaterEffect : MonoBehaviour
    {
        Material material;
        SonicWaterVolume settings;
        float immersion, clock;
        int lastFrame = -1;

        void OnPreCull()
        {
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            SonicWaterVolume current = null;
            foreach (var volume in SonicWaterVolume.Active)
                if (volume != null && volume.isActiveAndEnabled && volume.underwaterVisuals && volume.Contains(transform.position)) current = volume;
            if (current != null) settings = current;
            float transition = settings != null ? Mathf.Max(0.01f, settings.visualTransitionSeconds) : 0.35f;
            immersion = Mathf.MoveTowards(immersion, current != null ? 1 : 0, Time.deltaTime / transition);
            if (immersion > 0) clock += Time.deltaTime;
            else { clock = 0; settings = null; }
        }
        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (immersion <= 0 || settings == null)
            {
                Graphics.Blit(source, destination); return;
            }
            if (material == null)
            {
                Shader shader = Resources.Load<Shader>("SonicUnderwater");
                if (shader != null && shader.isSupported)
                    material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            if (material == null) { Graphics.Blit(source, destination); return; }
            float interval = Mathf.Max(0.5f, settings.waveInterval);
            float phase = Mathf.Repeat(clock, interval);
            float pulse = phase < interval * 0.6f ? Mathf.Pow(Mathf.Sin(Mathf.PI * phase / (interval * 0.6f)), 2) : 0;
            material.SetColor("_Tint", settings.underwaterTint);
            material.SetFloat("_TintStrength", settings.tintStrength * immersion);
            material.SetFloat("_Distortion", settings.waveDistortion * 3f * pulse * immersion);
            material.SetFloat("_Clock", clock);
            Graphics.Blit(source, destination, material);
        }
        void OnDisable()
        {
            immersion = 0; clock = 0; settings = null; lastFrame = -1;
            if (material != null)
            {
                if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
                material = null;
            }
        }
    }
}
