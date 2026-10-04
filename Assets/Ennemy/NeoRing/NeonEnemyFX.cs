using UnityEngine;
using System.Collections.Generic;

// Animations procedurales des ennemis cyberpunk (les GLB n'ont pas d'animation).
// Pneu/jante : rotation continue ; rotors : rotation rapide ; LEDs antennes : clignotent pres du joueur ; bob vertical optionnel.
public class NeonEnemyFX : MonoBehaviour
{
    public float WheelSpeed = 540f, RotorSpeed = 1440f;
    public float BobAmplitude = 0f, BobSpeed = 3f;
    public float BlinkDistance = 25f;

    readonly List<Transform> wheels = new List<Transform>(), rotors = new List<Transform>();
    readonly List<Renderer> leds = new List<Renderer>();
    Transform player;
    Vector3 baseLocalPos;

    void Awake()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "pneu" || t.name == "jante" || t.name.StartsWith("moyeu")) wheels.Add(t);
            else if (t.name.StartsWith("rotor_")) rotors.Add(t);
            else if (t.name.StartsWith("antenne_led") && t.TryGetComponent(out Renderer r)) leds.Add(r);
        }
        baseLocalPos = transform.localPosition;
    }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p) player = p.transform;
    }

    static void Spin(Transform t, Vector3 axis, float angle)
    {
        Vector3 c = t.TryGetComponent(out Renderer r) ? r.bounds.center : t.position;
        t.RotateAround(c, axis, angle);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        // Axes du modele (pas ceux des noeuds glTF) : essieu = droite du modele, rotors = haut du modele.
        // Rotation autour du centre du mesh : les pivots glTF ne sont pas forcement au centre des pieces.
        foreach (var w in wheels) Spin(w, transform.right, WheelSpeed * dt);
        foreach (var r in rotors) Spin(r, transform.up, RotorSpeed * dt);
        if (BobAmplitude > 0) transform.localPosition = baseLocalPos + Vector3.up * Mathf.Sin(Time.time * BobSpeed) * BobAmplitude;
        if (leds.Count > 0)
        {
            bool near = player && (player.position - transform.position).sqrMagnitude < BlinkDistance * BlinkDistance;
            bool on = !near || Mathf.Repeat(Time.time * 6f, 1f) > .5f;
            foreach (var l in leds) l.enabled = on;
        }
    }
}
