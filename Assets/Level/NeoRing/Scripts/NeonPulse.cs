using UnityEngine;

// Pulsation d'echelle (boost pads) et scintillement optionnel (panneaux holo).
public class NeonPulse : MonoBehaviour
{
    public float MinScale = .95f, MaxScale = 1.05f, Speed = 4f;
    public bool Flicker;
    Vector3 baseScale;
    Renderer rend;

    void Start() { baseScale = transform.localScale; rend = GetComponent<Renderer>(); }

    void Update()
    {
        float t = (Mathf.Sin(Time.time * Speed) + 1f) * .5f;
        transform.localScale = baseScale * Mathf.Lerp(MinScale, MaxScale, t);
        if (Flicker && rend) rend.enabled = Mathf.PerlinNoise(Time.time * 8f, transform.position.x) > .15f;
    }
}
