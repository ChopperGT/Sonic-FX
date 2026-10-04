using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace SonicFX.Structures
{
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(-150)]
    public sealed class SonicSwingPlatform : MonoBehaviour
    {
        [Range(1,80), Tooltip("Nombre de maillons. La planche descend sans changer de taille.")]
        public int linkCount=5;
        [Tooltip("Taille de la planche seule sur X/Z. 1 conserve sa taille initiale ; les chaines et l'epaisseur ne changent pas.")]
        public Vector2 platformSizeScale=Vector2.one;
        [Tooltip("Active le mouvement pendant le jeu.")] public bool swingEnabled=true;
        [Range(0,80), Tooltip("Angle maximal de chaque cote, en degres.")] public float amplitude=25;
        [Min(.2f), Tooltip("Duree d'un aller-retour complet, en secondes.")] public float period=4;
        [Range(0,360), Tooltip("0 = axe X local ; 90 = axe Z local.")] public float direction=0;
        [Range(0,360), Tooltip("Decalage du mouvement pour desynchroniser plusieurs plateformes.")] public float phase=0;
        [Tooltip("Transporte Sonic quand il est pose sur la planche.")] public bool carryPlayer=true;

        [SerializeField,HideInInspector] Mesh linkSource;
        [SerializeField,HideInInspector] Mesh restChainMesh;
        [SerializeField,HideInInspector] Transform anchor;
        [SerializeField,HideInInspector] Transform links;
        [SerializeField,HideInInspector] Transform platform;
        [SerializeField,HideInInspector] Rigidbody platformBody;
        [SerializeField,HideInInspector] MovingPlatformControl movement;
        [SerializeField,HideInInspector] BoxCollider carryTrigger;
        [SerializeField,HideInInspector] Vector3 originalFloorOffset;
        [SerializeField,HideInInspector] float originalLength=150.24052f;
        [SerializeField,HideInInspector] float linkSpacing=27.4f;
        [SerializeField,HideInInspector] float firstLinkY=-27.63f;
        [SerializeField,HideInInspector] Vector3 originalPlatformScale=Vector3.one;
        [SerializeField,HideInInspector] bool platformScaleInitialized;
        Mesh generated;
        int builtCount=-1;
        double startedAt;
        bool dirty=true;
        readonly List<Rider> riders=new List<Rider>();
        PhysicsMaterial deckMaterial;
        readonly Dictionary<Collider,PhysicsMaterial> originalMaterials=new Dictionary<Collider,PhysicsMaterial>();
        sealed class Rider
        {
            public PlayerBhysics player;
            public Rigidbody body;
            public ActionManager actions;
        }

        // The trigger only registers candidates. Support is checked again before every move:
        // overlapping the side/underside, jumping or standing on another object must not carry Sonic.
        public void RegisterRider(PlayerBhysics player)
        {
            if(player==null || !carryPlayer)return;
            foreach(var rider in riders)if(rider.player==player)return;
            var body=player.GetComponent<Rigidbody>();
            if(body!=null)riders.Add(new Rider{player=player,body=body,actions=player.GetComponent<ActionManager>()});
        }
        bool IsSupported(Rider rider)
        {
            if(rider.player==null || rider.body==null || !rider.player.gameObject.activeInHierarchy ||
                rider.body.isKinematic || !rider.player.Grounded)return false;
            if(rider.actions!=null && rider.actions.Action!=0 && rider.actions.Action!=3)return false;
            // An upward impulse belongs to Sonic, not the pendulum. Never drag a jumping player down.
            if(rider.body.linearVelocity.y>1f)return false;
            var scene=gameObject.scene.GetPhysicsScene();
            return scene.Raycast(rider.body.position+Vector3.up*2,Vector3.down,out var hit,
                2+Mathf.Max(.1f,rider.player.RayToGroundDistance)+.05f,
                rider.player.Playermask,QueryTriggerInteraction.Ignore) &&
                hit.collider.attachedRigidbody==platformBody && hit.normal.y>.9f;
        }
        void ConfigureDeckContacts()
        {
            if(platform==null)return;
            if(deckMaterial==null)deckMaterial=new PhysicsMaterial("ChainSwing : appui sans rebond"){
                hideFlags=HideFlags.DontSave,staticFriction=0,dynamicFriction=0,bounciness=0,
                frictionCombine=PhysicsMaterialCombine.Minimum,bounceCombine=PhysicsMaterialCombine.Minimum};
            foreach(var collider in platform.GetComponents<Collider>()){
                if(collider.isTrigger || !collider.enabled)continue;
                if(!originalMaterials.ContainsKey(collider))originalMaterials.Add(collider,collider.sharedMaterial);
                collider.sharedMaterial=deckMaterial;
            }
        }
        public Transform Platform=>platform;
        public Transform Links=>links;
        public Rigidbody PlatformBody=>platformBody;
        public MovingPlatformControl Movement=>movement;
        public Mesh LinkSource=>linkSource;
        public float LocalLength=>originalLength+(Mathf.Clamp(linkCount,1,80)-5)*linkSpacing;
        public float WorldLength=>LocalLength*Mathf.Abs(transform.lossyScale.y);
        public Vector3 AnchorPosition=>anchor!=null?anchor.position:transform.position;
        public Bounds PlatformLocalBounds {
            get {
                var filter=platform!=null?platform.GetComponent<MeshFilter>():null;
                return filter!=null && filter.sharedMesh!=null?filter.sharedMesh.bounds:default;
            }
        }
        public Vector2 WorldPlatformSize {
            get {
                if(platform==null)return Vector2.zero;
                var size=PlatformLocalBounds.size;var scale=platform.lossyScale;
                return new Vector2(size.x*Mathf.Abs(scale.x),size.z*Mathf.Abs(scale.z));
            }
        }
        void InitializePlatformScale()
        {
            if(platform==null || platformScaleInitialized)return;
            originalPlatformScale=platform.localScale;platformScaleInitialized=true;
        }
        public void SetWorldPlatformSize(Vector2 metres)
        {
            if(platform==null)return;
            InitializePlatformScale();
            var size=PlatformLocalBounds.size;var scale=transform.lossyScale;
            platformSizeScale=new Vector2(
                Mathf.Max(.01f,metres.x)/Mathf.Max(.0001f,size.x*Mathf.Abs(originalPlatformScale.x*scale.x)),
                Mathf.Max(.01f,metres.y)/Mathf.Max(.0001f,size.z*Mathf.Abs(originalPlatformScale.z*scale.z)));
            dirty=true;Rebuild();
        }
        public Vector3 SwingDirection {
            get {
                var d=transform.TransformDirection(new Vector3(Mathf.Cos(direction*Mathf.Deg2Rad),0,Mathf.Sin(direction*Mathf.Deg2Rad)));
                d=Vector3.ProjectOnPlane(d,Vector3.up);
                return d.sqrMagnitude>.00001f?d.normalized:Vector3.right;
            }
        }
        public Quaternion FlatRotation {
            get {
                var f=Vector3.ProjectOnPlane(transform.forward,Vector3.up);
                if(f.sqrMagnitude<.00001f)f=Vector3.forward;
                return Quaternion.LookRotation(f.normalized,Vector3.up);
            }
        }

        public void Initialize(Mesh source,Mesh rest,Transform top,Transform chain,Transform floor,float spacing,float firstY)
        {
            linkSource=source;restChainMesh=rest;anchor=top;links=chain;platform=floor;
            InitializePlatformScale();
            originalFloorOffset=floor.localPosition;originalLength=-originalFloorOffset.y;linkSpacing=spacing;firstLinkY=firstY;
            platformBody=floor.GetComponent<Rigidbody>();movement=floor.GetComponent<MovingPlatformControl>();
            foreach(var c in floor.GetComponents<BoxCollider>())if(c.isTrigger)carryTrigger=c;
            Rebuild();
        }
        void OnEnable(){startedAt=Time.timeAsDouble;dirty=true;Rebuild();if(Application.IsPlaying(gameObject))PlaceAtAngle(AngleAt(0),false);}
        void OnValidate(){linkCount=Mathf.Clamp(linkCount,1,80);platformSizeScale.x=Mathf.Max(.0001f,platformSizeScale.x);platformSizeScale.y=Mathf.Max(.0001f,platformSizeScale.y);amplitude=Mathf.Clamp(amplitude,0,80);period=Mathf.Max(.2f,period);dirty=true;}
        void Update(){if(!Application.IsPlaying(gameObject) && dirty)Rebuild();}
        void FixedUpdate()
        {
            if(!Application.IsPlaying(gameObject) || platformBody==null)return;
            if(dirty || builtCount!=linkCount)Rebuild();
            PlaceAtAngle(AngleAt((float)(Time.timeAsDouble-startedAt)),true);
        }
        void LateUpdate()
        {
            if(!Application.IsPlaying(gameObject) || links==null || platform==null)return;
            // Follow the interpolated deck, rather than the next physics target.
            UpdateChainRotation(platform.position-RestPosition());
        }
        public float AngleAt(float elapsed)=>swingEnabled?Mathf.Clamp(amplitude,0,80)*Mathf.Sin(elapsed*2*Mathf.PI/Mathf.Max(.2f,period)+phase*Mathf.Deg2Rad):0;
        public Vector3 RestPosition()
        {
            var scale=transform.lossyScale;
            return AnchorPosition+Vector3.down*WorldLength+FlatRotation*new Vector3(originalFloorOffset.x*scale.x,0,originalFloorOffset.z*scale.z);
        }
        public Vector3 PositionAtAngle(float degrees)
        {
            float a=Mathf.Clamp(degrees,-80,80)*Mathf.Deg2Rad;
            return RestPosition()+SwingDirection*(WorldLength*Mathf.Sin(a))+Vector3.up*(WorldLength*(1-Mathf.Cos(a)));
        }
        public void SetWorldLength(float metres)
        {
            float scale=Mathf.Max(.0001f,Mathf.Abs(transform.lossyScale.y));
            linkCount=Mathf.Clamp(5+Mathf.RoundToInt((metres/scale-originalLength)/linkSpacing),1,80);dirty=true;
        }
        public void Rebuild()
        {
            if(linkSource==null || links==null || platform==null)return;
            InitializePlatformScale();
            platform.localScale=new Vector3(originalPlatformScale.x*Mathf.Max(.0001f,platformSizeScale.x),originalPlatformScale.y,originalPlatformScale.z*Mathf.Max(.0001f,platformSizeScale.y));
            if(generated==null || builtCount!=linkCount){
                ReleaseMesh();
                var v=linkSource.vertices;var normals=linkSource.normals;var uv=linkSource.uv;var triangles=linkSource.triangles;
                int count=Mathf.Clamp(linkCount,1,80);var vv=new Vector3[v.Length*count];var nn=new Vector3[vv.Length];var uu=new Vector2[vv.Length];var tt=new int[triangles.Length*count];
                for(int n=0;n<count;n++){
                    var offset=Vector3.up*(firstLinkY-linkSpacing*n);
                    for(int i=0;i<v.Length;i++){int k=n*v.Length+i;vv[k]=v[i]+offset;if(normals.Length==v.Length)nn[k]=normals[i];if(uv.Length==v.Length)uu[k]=uv[i];}
                    for(int i=0;i<triangles.Length;i++)tt[n*triangles.Length+i]=triangles[i]+n*v.Length;
                }
                generated=new Mesh{name="Chain_Maillons_"+count,hideFlags=HideFlags.DontSave,indexFormat=vv.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                generated.vertices=vv;generated.triangles=tt;generated.uv=uu;
                if(normals.Length==v.Length)generated.normals=nn;else generated.RecalculateNormals();
                generated.RecalculateBounds();links.GetComponent<MeshFilter>().sharedMesh=generated;builtCount=count;
            }
            if(platformBody!=null){platformBody.isKinematic=true;platformBody.useGravity=false;}
            if(Application.IsPlaying(gameObject))ConfigureDeckContacts();
            if(movement!=null)movement.enabled=false; // Publish offsets without running its independent sine movement.
            if(carryTrigger!=null)carryTrigger.enabled=carryPlayer;
            if(!Application.IsPlaying(gameObject))PlaceAtAngle(0,false);
            dirty=false;
        }
        public void PlaceAtAngle(float angle,bool usePhysics)
        {
            if(platform==null)return;
            var target=PositionAtAngle(angle);
            var previous=platformBody!=null?platformBody.position:platform.position;
            // Do not publish a second offset to Objects_Interaction. Move the collision deck and
            // supported riders together before PlayerBhysics performs its ground raycasts.
            if(movement!=null){movement.Moving=target;movement.TranslateVector=Vector3.zero;}
            if(usePhysics && platformBody!=null){
                ConfigureDeckContacts();
                var displacement=target-previous;
                for(int i=riders.Count-1;i>=0;i--){
                    var rider=riders[i];
                    if(!carryPlayer || !IsSupported(rider)){riders.RemoveAt(i);continue;}
                    rider.body.position+=displacement;
                }
                // MovePosition gives the solver a vertical platform velocity, which used to launch
                // Sonic after the manual carry. This pose update keeps his own velocity unchanged.
                platformBody.position=target;platformBody.rotation=FlatRotation;
                Physics.SyncTransforms();
            }
            else {platform.SetPositionAndRotation(target,FlatRotation);if(platformBody!=null){platformBody.position=target;platformBody.rotation=FlatRotation;}}
            UpdateChainRotation(target-RestPosition());
        }
        void UpdateChainRotation(Vector3 displacement)
        {
            if(links==null)return;
            var end=Vector3.down*WorldLength+displacement;
            links.rotation=Quaternion.FromToRotation(Vector3.down,end.normalized)*FlatRotation;
        }
        void OnDisable(){
            riders.Clear();
            foreach(var pair in originalMaterials)if(pair.Key!=null)pair.Key.sharedMaterial=pair.Value;
            originalMaterials.Clear();
            if(deckMaterial!=null){if(Application.IsPlaying(gameObject))Destroy(deckMaterial);else DestroyImmediate(deckMaterial);deckMaterial=null;}
            if(movement!=null)movement.TranslateVector=Vector3.zero;
            if(!Application.IsPlaying(gameObject))PlaceAtAngle(0,false);ReleaseMesh();dirty=true;
        }
        void OnDestroy(){ReleaseMesh();}
        void ReleaseMesh()
        {
            if(generated==null)return;
            if(links!=null && links.GetComponent<MeshFilter>().sharedMesh==generated)links.GetComponent<MeshFilter>().sharedMesh=restChainMesh;
            if(Application.IsPlaying(gameObject))Destroy(generated);else DestroyImmediate(generated);
            generated=null;builtCount=-1;
        }
    }
}
