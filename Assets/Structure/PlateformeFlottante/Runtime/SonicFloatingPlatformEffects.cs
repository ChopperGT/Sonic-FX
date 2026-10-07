using UnityEngine;
namespace SonicFX.Structures
{
    // Add sliding after Sonic's ordinary ground friction, before the physics solver.
    [DefaultExecutionOrder(200)]
    public class SonicFloatingPlatformEffects : MonoBehaviour
    {
        public SonicFloatingPlatform platform;
        void FixedUpdate(){if(platform!=null)platform.ApplyEffects(Time.fixedDeltaTime);}
    }
}
