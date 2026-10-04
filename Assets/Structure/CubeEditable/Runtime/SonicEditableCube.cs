using UnityEngine;

namespace SonicFX.Structures
{
    // Share ramp_C's deformation engine; each cube keeps its own serialized cage.
    [ExecuteAlways,DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider))]
    [AddComponentMenu("Sonic FX/Structures/Cube editable")]
    public sealed class SonicEditableCube : SonicEditableRamp
    {
        public override string StructureName => "Cube";
    }
}
