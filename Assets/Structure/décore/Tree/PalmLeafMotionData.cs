using UnityEngine;

namespace SonicFX.Decor
{
    public sealed class PalmLeafMotionData : ScriptableObject
    {
        public int version;
        public Mesh sourceMesh;
        public Mesh trunkCollisionMesh;
        public Bounds leafBounds;
        public Vector3 crownCenter;
        public int[] leafVertices;
        public float[] bendWeights;
    }
}
