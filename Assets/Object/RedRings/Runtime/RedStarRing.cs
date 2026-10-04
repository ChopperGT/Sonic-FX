using UnityEngine;
using UnityEngine.Serialization;
using SonicFX.Menu;

namespace SonicFX.RedRings
{
    [DisallowMultipleComponent]
    public sealed class RedStarRing : MonoBehaviour
    {
        [Min(0), InspectorName("Ordre (0 = ordre dans la Hierarchy)")] public int order;
        [InspectorName("Rotation par seconde")] public float rotationSpeed=150;
        [Min(.05f), InspectorName("Rayon de ramassage (monde)")] public float pickupRadius=.8f;
        [Header("Parcours automatique, sans parent Defi")]
        [Min(.1f), InspectorName("Temps imparti (secondes)")] public float autoTimeLimit=60;
        [InspectorName("Son de reussite")] public AudioClip autoSuccessSound;
        [Tooltip("Points intermediaires optionnels pour guider vers CE ring. Placer ces objets hors du ring qui tourne.")]
        public Transform[] guidePoints;
        public RedRingChallenge Challenge {get;internal set;}
        public bool Collected {get;private set;}
        [HideInInspector] public bool manualStars;
        [HideInInspector, FormerlySerializedAs("moveFollowingStars")] public bool movePreviousStars=true;
        [HideInInspector] public bool[] lockedStarFollow=System.Array.Empty<bool>();
        [HideInInspector] public Vector3[] editableStarPositions=System.Array.Empty<Vector3>();
        [HideInInspector] public bool curveStars;
        [HideInInspector] public Vector3[] starCurvePoints=System.Array.Empty<Vector3>();
        [HideInInspector] public bool[] starCurveLocks=System.Array.Empty<bool>();
        public Vector3 WorldCenter
        {
            get{var shape=pickup!=null?pickup:GetComponent<SphereCollider>();return shape!=null?transform.TransformPoint(shape.center):transform.position;}
        }
        // Use a non-rotating frame, shared by the editor and the runtime. Automatic
        // scene routes do not reparent rings, so their existing parent remains valid.
        Transform GuideSpace
        {
            get{var route=GetComponentInParent<RedRingChallenge>();return route!=null?route.transform:transform.parent;}
        }
        public Vector3 GuideToWorld(Vector3 point)=>GuideSpace!=null?GuideSpace.TransformPoint(point):point;
        public Vector3 WorldToGuide(Vector3 point)=>GuideSpace!=null?GuideSpace.InverseTransformPoint(point):point;
        Renderer[] visuals;bool[] originalVisibility;SphereCollider pickup;
        void Awake(){Prepare();}
        internal void Prepare()
        {
            if(visuals!=null)return;
            visuals=GetComponentsInChildren<Renderer>(true);originalVisibility=new bool[visuals.Length];
            for(int i=0;i<visuals.Length;i++)originalVisibility[i]=visuals[i].enabled;
            pickup=GetComponent<SphereCollider>();if(pickup==null)pickup=gameObject.AddComponent<SphereCollider>();
            pickup.isTrigger=true;
            float scale=Mathf.Max(Mathf.Abs(transform.lossyScale.x),Mathf.Abs(transform.lossyScale.y),Mathf.Abs(transform.lossyScale.z),.001f);
            pickup.radius=Mathf.Max(.05f,pickupRadius)/scale;
        }
        void Start()
        {
            if(Challenge==null)Challenge=GetComponentInParent<RedRingChallenge>();
            if(Challenge==null)Challenge=RedRingChallenge.FindOrCreate(gameObject.scene);
            Challenge.Initialize();
            if(!Challenge.Contains(this))SetVisible(false);
        }
        void Update(){if(!Collected)transform.Rotate(0,rotationSpeed*Time.deltaTime,0,Space.Self);}
        void OnTriggerEnter(Collider other)
        {
            var player=other.GetComponentInParent<PlayerBhysics>();
            if(player==null&&other.attachedRigidbody!=null)player=other.attachedRigidbody.GetComponent<PlayerBhysics>();
            if(player!=null&&Challenge!=null)Challenge.TryCollect(this,player);
        }
        internal void SetCollected(bool value){Collected=value;SetVisible(!value);}
        internal void SetVisible(bool visible)
        {
            Prepare();
            for(int i=0;i<visuals.Length;i++)if(visuals[i]!=null)visuals[i].enabled=visible&&originalVisibility[i];
            if(pickup!=null)pickup.enabled=visible;
        }
    }
}
