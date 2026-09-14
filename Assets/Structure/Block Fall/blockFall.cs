using System.Collections;
using UnityEngine;

public class FallingPlatform : MonoBehaviour
{
    [SerializeField] private float fallDelay = 1.5f;

    private Rigidbody rb;
    private bool triggered = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (triggered)
            return;

        // Pour commencer, on déclenche avec n'importe quel objet qui touche la plateforme.
        // On pourra ensuite limiter ça uniquement à Sonic.
        triggered = true;
        StartCoroutine(FallAfterDelay());
    }

    private IEnumerator FallAfterDelay()
    {
        yield return new WaitForSeconds(fallDelay);

        rb.isKinematic = false;
        rb.useGravity = true;
    }
}