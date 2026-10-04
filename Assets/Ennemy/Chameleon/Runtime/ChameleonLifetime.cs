using UnityEngine;
namespace SonicFX.Chameleon { public sealed class ChameleonLifetime : MonoBehaviour { public float seconds=1.5f; void Start() { Destroy(gameObject,seconds); } } }
