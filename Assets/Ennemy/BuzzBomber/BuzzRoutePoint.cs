using UnityEngine;

namespace SonicFX.Buzz
{
    [DisallowMultipleComponent]
    public class BuzzRoutePoint : MonoBehaviour
    {
        [Min(0.1f), Tooltip("Vitesse pour aller vers ce point, en unites par seconde.")]
        public float speed = 3f;
        [Min(0f), Tooltip("Pause une fois ce point atteint, en secondes.")]
        public float pause = 1f;
    }
}
