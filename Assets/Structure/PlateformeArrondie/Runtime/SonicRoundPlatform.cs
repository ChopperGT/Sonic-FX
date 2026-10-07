using UnityEngine;

namespace SonicFX.Structures
{
    // Use the same serialized control cage and collision generator as Cube.
    [ExecuteAlways, DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    [AddComponentMenu("Sonic FX/Structures/Plateforme arrondie editable")]
    public sealed class SonicRoundPlatform : SonicEditableRamp
    {
        public override string StructureName => "Plateforme arrondie";
    }
}
