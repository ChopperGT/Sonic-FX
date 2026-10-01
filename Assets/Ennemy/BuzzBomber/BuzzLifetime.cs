using UnityEngine;
namespace SonicFX.Buzz
{
    public class BuzzLifetime : MonoBehaviour
    {
        [Min(0.1f)] public float seconds = 1.2f;
        private void Start() { Destroy(gameObject, seconds); }
    }
}
