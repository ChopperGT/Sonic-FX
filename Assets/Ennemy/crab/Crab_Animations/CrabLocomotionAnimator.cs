using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(Animator))]
public class CrabLocomotionAnimator : MonoBehaviour
{
    [Tooltip("Cocher pendant Play pour voir la marche sur place.")]
    public bool previewWalking;
    [Tooltip("Objet dont le deplacement commande la marche. Vide : cet objet.")]
    public Transform movementSource;
    [Min(0.01f), Tooltip("Vitesse de deplacement correspondant au cycle de marche normal.")]
    public float referenceSpeed = 0.65f;
    [Min(0f)] public float movementThreshold = 0.03f;
    [Min(0.01f)] public float teleportDistance = 5f;

    private Animator anim;
    private Transform tracked;
    private Vector3 lastPosition;
    private float filteredSpeed;
    private static readonly int Moving = Animator.StringToHash("Moving");
    private static readonly int WalkRate = Animator.StringToHash("WalkRate");

    private void OnEnable()
    {
        anim = GetComponent<Animator>();
        tracked = movementSource != null ? movementSource : transform;
        lastPosition = tracked.position;
        filteredSpeed = 0f;
    }

    private void Update()
    {
        Transform source = movementSource != null ? movementSource : transform;
        if (source != tracked)
        {
            tracked = source;
            lastPosition = source.position;
            filteredSpeed = 0f;
        }
        Vector3 delta = source.position - lastPosition;
        lastPosition = source.position;
        if (Time.deltaTime <= 0f) return;
        float speed = Vector3.ProjectOnPlane(delta, source.up).magnitude / Time.deltaTime;
        if (delta.magnitude > teleportDistance) { speed = 0f; filteredSpeed = 0f; }
        // Smooth fixed-step movement to avoid toggling Idle between physics ticks.
        filteredSpeed = Mathf.Lerp(filteredSpeed, speed, 1f - Mathf.Exp(-Time.deltaTime / 0.08f));
        bool moving = previewWalking || filteredSpeed > movementThreshold;
        float rate = previewWalking ? 1f : Mathf.Clamp(filteredSpeed / Mathf.Max(0.01f, referenceSpeed), 0.35f, 2.5f);
        anim.SetBool(Moving, moving);
        anim.SetFloat(WalkRate, rate);
    }
}
