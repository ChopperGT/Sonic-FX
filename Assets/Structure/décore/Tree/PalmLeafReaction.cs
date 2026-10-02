using UnityEngine;

namespace SonicFX.Decor
{
    [DisallowMultipleComponent, RequireComponent(typeof(MeshFilter))]
    public sealed class PalmLeafReaction : MonoBehaviour
    {
        [InspectorName("Animer les feuilles au passage de Sonic")]
        public bool reactToSonic=true;
        [Header("Reaction des feuilles")]
        [Min(.1f), InspectorName("Distance de reaction")]
        public float reactionDistance=3f;
        [Min(.1f), InspectorName("Vitesse pour l'effet maximum")]
        public float referenceSpeed=60f;
        [Min(0f), InspectorName("Deplacement maximum des feuilles")]
        [Tooltip("Amplitude en unites du monde. Les feuilles reviennent ensuite en place.")]
        public float maximumDisplacement=1.5f;
        [Min(1f), InspectorName("Souplesse du retour")]
        public float springStrength=45f;
        [Min(.1f), InspectorName("Amortissement")]
        public float damping=5f;
        [HideInInspector] public PalmLeafMotionData motionData;

        static PlayerBhysics cachedPlayer;
        static float nextPlayerSearch;
        MeshFilter filter;
        Mesh originalMesh,animatedMesh;
        Vector3[] restVertices,vertices,restNormals;
        Vector3 offset,offsetVelocity,lastPlayerPosition;
        bool hasPreviousPosition,deformed,wasInRange;
        Bounds worldLeaves;
        Matrix4x4 lastMatrix;
        public Vector3 CurrentDisplacement=>offset;

        void OnEnable()
        {
            filter=GetComponent<MeshFilter>();hasPreviousPosition=false;
            // Leaf collisions remain absent even if animation is turned off.
            var collider=GetComponent<MeshCollider>();
            if(collider!=null&&motionData!=null&&motionData.trunkCollisionMesh!=null)
                collider.sharedMesh=motionData.trunkCollisionMesh;
            UpdateBounds();
        }
        void OnValidate()
        {
            reactionDistance=Mathf.Max(.1f,reactionDistance);referenceSpeed=Mathf.Max(.1f,referenceSpeed);
            maximumDisplacement=Mathf.Max(0,maximumDisplacement);springStrength=Mathf.Max(1,springStrength);damping=Mathf.Max(.1f,damping);
        }
        void LateUpdate()
        {
            if(!reactToSonic){ResetMotion();hasPreviousPosition=false;return;}
            if(cachedPlayer==null&&Time.unscaledTime>=nextPlayerSearch)
            {
                nextPlayerSearch=Time.unscaledTime+1f;
                cachedPlayer=Object.FindAnyObjectByType<PlayerBhysics>();
            }
            if(cachedPlayer==null)
            {
                hasPreviousPosition=false;Step(Vector3.one*100000,Vector3.one*100000,Vector3.zero,Time.deltaTime);return;
            }
            Vector3 current=cachedPlayer.transform.position;
            Vector3 previous=hasPreviousPosition?lastPlayerPosition:current;
            Vector3 velocity=cachedPlayer.p_rigidbody!=null?cachedPlayer.p_rigidbody.linearVelocity:Vector3.zero;
            if(hasPreviousPosition&&Time.deltaTime>0)
            {
                Vector3 tracked=(current-previous)/Time.deltaTime;
                // Tubes can suspend player physics while still moving Sonic.
                if(tracked.sqrMagnitude>velocity.sqrMagnitude&&Vector3.Distance(current,previous)<100f)velocity=tracked;
            }
            lastPlayerPosition=current;hasPreviousPosition=true;
            if(Vector3.Distance(current,previous)>100f)previous=current;
            Step(previous,current,velocity,Time.deltaTime);
        }
        void UpdateBounds()
        {
            if(motionData==null)return;
            var bounds=motionData.leafBounds;
            worldLeaves=new Bounds(transform.TransformPoint(bounds.center),Vector3.zero);
            for(int i=0;i<8;i++)
                worldLeaves.Encapsulate(transform.TransformPoint(bounds.center+Vector3.Scale(bounds.extents,
                    new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
            lastMatrix=transform.localToWorldMatrix;
        }
        internal bool InRange(Vector3 previous,Vector3 current)
        {
            if(motionData==null)return false;
            if(lastMatrix!=transform.localToWorldMatrix)UpdateBounds();
            var bounds=worldLeaves;bounds.Expand(reactionDistance*2f);
            if(bounds.Contains(current)||bounds.Contains(previous))return true;
            Vector3 travel=current-previous;
            return travel.sqrMagnitude>.000001f&&bounds.IntersectRay(new Ray(previous,travel.normalized),out float distance)&&distance<=travel.magnitude;
        }
        public void Step(Vector3 previous,Vector3 current,Vector3 velocity,float deltaTime)
        {
            if(!reactToSonic){ResetMotion();return;}
            if(motionData==null||deltaTime<=0)return;
            Vector3 target=Vector3.zero;
            bool nearby=InRange(previous,current);
            if(nearby&&velocity.sqrMagnitude>.01f)
            {
                float speedFactor=Mathf.Clamp01(velocity.magnitude/referenceSpeed);
                target=velocity.normalized*(maximumDisplacement*speedFactor);
                // A fast crossing should move the leaves even if it lasts only one frame.
                if(!wasInRange)offsetVelocity+=target*4f;
            }
            wasInRange=nearby;
            if(target==Vector3.zero&&offset.sqrMagnitude<.000001f&&offsetVelocity.sqrMagnitude<.000001f)
            {
                ResetMotion();return;
            }
            if(!EnsureMesh())return;
            float remaining=Mathf.Min(deltaTime,.1f);
            while(remaining>0)
            {
                float step=Mathf.Min(remaining,1f/120f);
                offsetVelocity+=((target-offset)*springStrength-offsetVelocity*damping)*step;
                offset+=offsetVelocity*step;remaining-=step;
            }
            // Spring overshoot is limited, including during very fast falls.
            offset=Vector3.ClampMagnitude(offset,maximumDisplacement*1.35f);
            ApplyOffset();
        }
        bool EnsureMesh()
        {
            if(animatedMesh!=null)return true;
            if(filter==null)filter=GetComponent<MeshFilter>();
            if(filter==null||filter.sharedMesh==null||motionData.leafVertices==null||motionData.bendWeights==null||
                motionData.leafVertices.Length!=motionData.bendWeights.Length)return false;
            originalMesh=filter.sharedMesh;
            // Never apply a mask from another mesh or edit the shared prefab mesh.
            if(originalMesh!=motionData.sourceMesh)return false;
            animatedMesh=Instantiate(originalMesh);animatedMesh.name=originalMesh.name+"_FeuillesAnimees";animatedMesh.MarkDynamic();
            restVertices=originalMesh.vertices;vertices=(Vector3[])restVertices.Clone();restNormals=originalMesh.normals;
            filter.sharedMesh=animatedMesh;return true;
        }
        void ApplyOffset()
        {
            Vector3 localOffset=transform.InverseTransformVector(offset);
            for(int i=0;i<motionData.leafVertices.Length;i++)
            {
                int vertex=motionData.leafVertices[i];vertices[vertex]=restVertices[vertex]+localOffset*motionData.bendWeights[i];
            }
            animatedMesh.vertices=vertices;animatedMesh.RecalculateNormals();
            var normals=animatedMesh.normals;
            // Keep the original trunk shading; only foliage normals are allowed to change.
            for(int i=0;i<motionData.leafVertices.Length;i++)
            {
                int vertex=motionData.leafVertices[i];restNormals[vertex]=normals[vertex];
            }
            animatedMesh.normals=restNormals;animatedMesh.RecalculateBounds();deformed=true;
        }
        public void ResetMotion()
        {
            offset=Vector3.zero;offsetVelocity=Vector3.zero;wasInRange=false;
            if(animatedMesh==null||!deformed)return;
            System.Array.Copy(restVertices,vertices,vertices.Length);
            animatedMesh.vertices=vertices;animatedMesh.normals=originalMesh.normals;animatedMesh.bounds=originalMesh.bounds;deformed=false;
        }
        void OnDisable()
        {
            ResetMotion();hasPreviousPosition=false;
            if(animatedMesh==null)return;
            if(filter!=null&&filter.sharedMesh==animatedMesh)filter.sharedMesh=originalMesh;
            if(Application.isPlaying)Destroy(animatedMesh);else DestroyImmediate(animatedMesh);
            animatedMesh=null;restVertices=null;vertices=null;restNormals=null;
        }
        void OnDrawGizmosSelected()
        {
            if(motionData==null)return;
            UpdateBounds();var bounds=worldLeaves;bounds.Expand(reactionDistance*2f);
            Gizmos.color=new Color(.4f,1f,.4f,.7f);Gizmos.DrawWireCube(bounds.center,bounds.size);
        }
    }
}
