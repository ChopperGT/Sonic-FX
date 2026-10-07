using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SonicFX.Water;
using SonicFX.Lava;
namespace SonicFX.Structures
{
    [ExecuteAlways,DisallowMultipleComponent,RequireComponent(typeof(Rigidbody)),DefaultExecutionOrder(-170)]
    public class SonicFloatingPlatform : MonoBehaviour
    {
        public static readonly List<SonicFloatingPlatform> Active=new List<SonicFloatingPlatform>();
        public enum Liquid { None, Water, Lava }
        public enum FirePhase { Normal, Spreading, Burning, Cooling, Resting }
        [Header("Placement : eau ou lave uniquement")]
        [Tooltip("Facultatif. Sinon le liquide est trouve automatiquement.")] public SonicWaterVolume water;
        public SonicLavaVolume lava;
        [InspectorName("Aligner sur le liquide dans l'editeur")] public bool snapInEditor=true;
        [Min(0),InspectorName("Profondeur immergee du dessous")] public float immersion=.12f;
        [Header("Inclinaison sous Sonic")]
        public bool tiltEnabled=true;
        [Range(0,50),InspectorName("Angle maximal")] public float maximumTilt=22;
        [Min(.1f),InspectorName("Vitesse d'inclinaison (degres/seconde)")] public float tiltSpeed=15;
        [Min(.1f),InspectorName("Vitesse de retour a plat")] public float returnSpeed=12;
        [Min(0),InspectorName("Acceleration de glissement")] public float slideAcceleration=18;
        [InspectorName("Accompagner Sonic pendant l'inclinaison")] public bool carrySonic=true;
        [Header("Feu : lave uniquement")]
        [InspectorName("Activer le feu")] public bool fireEnabled=true;
        [Min(0),InspectorName("Contact avec la lave avant ignition")] public float ignitionDelay=.2f;
        [Min(.1f),InspectorName("Duree de propagation")] public float spreadSeconds=3;
        [Min(.1f),InspectorName("Duree entierement en feu")] public float burnSeconds=3;
        [Min(.1f),InspectorName("Duree d'extinction")] public float coolSeconds=1.2f;
        [Min(0),InspectorName("Repos avant une nouvelle ignition")] public float fireRestSeconds=4;
        [Min(.1f),InspectorName("Delai entre les degats")] public float damageInterval=1.5f;
        [Min(0),InspectorName("Quantite de flammes")] public float flamesPerSecond=90;
        [Min(.1f),Tooltip("Hauteur des flammes animees, en unites du monde.")] public float flameHeight=1.8f;
        [Min(.1f),Tooltip("Largeur d'une flamme animee.")] public float flameWidth=1.1f;
        public bool embersEnabled=true;
        [Min(0),Tooltip("Quantite de petites braises qui montent depuis les flammes.")] public float embersPerSecond=12;
        [Header("References configurees")]
        public SonicEditableCube deck;
        public MeshCollider deckCollider;
        public MeshFilter fireMesh;
        public Renderer fireRenderer;
        public ParticleSystem flames;
        public ParticleSystem embers;
        public Liquid CurrentLiquid { get; private set; }
        public bool PlacementValid { get; private set; }
        public FirePhase FireState { get; private set; }
        public float FireCoverage { get; private set; }
        public float FireStrength { get; private set; }
        public Vector2 IgnitionPoint { get; private set; } = Vector2.zero;
        public int DamageEvents { get; private set; }
        public string Status { get; private set; }
        Rigidbody body;Quaternion restRotation;bool initialized;float fireTimer,contactTimer,searchAt,emitRemainder;
        SonicWaterVolume detectedWater;SonicLavaVolume detectedLava;
        PlayerBhysics[] players=new PlayerBhysics[0];
        readonly List<PlayerBhysics> supported=new List<PlayerBhysics>();
        readonly Dictionary<PlayerBhysics,float> nextDamage=new Dictionary<PlayerBhysics,float>();
        Material animatedFlameMaterial,emberMaterial; PhysicsMaterial deckMaterial;PhysicsMaterial originalMaterial;
        MeshFilter deckMesh;bool warned;
        PhysicsScene World=>gameObject.scene.GetPhysicsScene();
        public Bounds DeckBounds=>deck!=null?deck.ControlBounds:new Bounds(Vector3.zero,new Vector3(8,.8f,6));
        public Bounds RootDeckBounds
        {
            get
            {
                var b=DeckBounds;if(deck==null)return b;
                Bounds result=new Bounds();bool first=true;
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                {
                    Vector3 point=transform.InverseTransformPoint(deck.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));
                    if(first){result=new Bounds(point,Vector3.zero);first=false;}else result.Encapsulate(point);
                }
                return result;
            }
        }
        void OnEnable(){initialized=false;if(!Active.Contains(this))Active.Add(this);}
        void OnValidate(){immersion=Mathf.Max(0,immersion);tiltSpeed=Mathf.Max(.1f,tiltSpeed);spreadSeconds=Mathf.Max(.1f,spreadSeconds);burnSeconds=Mathf.Max(.1f,burnSeconds);coolSeconds=Mathf.Max(.1f,coolSeconds);}
        void Start(){if(Application.IsPlaying(gameObject))Initialize();}
        public void Initialize()
        {
            if(deckMaterial!=null){if(deckCollider!=null)deckCollider.sharedMaterial=originalMaterial;if(Application.IsPlaying(gameObject))Destroy(deckMaterial);else DestroyImmediate(deckMaterial);deckMaterial=null;}
            if(body==null)body=GetComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;body.interpolation=RigidbodyInterpolation.None;
            Vector3 f=Vector3.ProjectOnPlane(transform.forward,Vector3.up);restRotation=Quaternion.LookRotation(f.sqrMagnitude<.001f?Vector3.forward:f.normalized);
            if(deck!=null){deck.Rebuild();deckMesh=deck.GetComponent<MeshFilter>();}
            FireState=FirePhase.Normal;FireCoverage=FireStrength=0;fireTimer=contactTimer=emitRemainder=0;nextDamage.Clear();DamageEvents=0;
            if(deckCollider!=null){originalMaterial=deckCollider.sharedMaterial;deckMaterial=new PhysicsMaterial("Plateforme flottante sans rebond"){hideFlags=HideFlags.DontSave,staticFriction=0,dynamicFriction=0,bounciness=0,frictionCombine=PhysicsMaterialCombine.Minimum,bounceCombine=PhysicsMaterialCombine.Minimum};deckCollider.sharedMaterial=deckMaterial;}
            ConfigureFlames();initialized=true;RefreshPlacement(true);UpdateFireVisuals();
        }
        bool SameWorld(Component c)=>c!=null && c.gameObject.activeInHierarchy && c.gameObject.scene.GetPhysicsScene()==World;
        bool Fits(Transform liquid,float width,float length)
        {
            if(liquid==null || Vector3.Dot(liquid.up,Vector3.up)<.95f)return false;
            var bounds=RootDeckBounds;var scale=transform.lossyScale;Quaternion rotation=Application.IsPlaying(gameObject)||initialized?restRotation:Quaternion.Euler(0,transform.eulerAngles.y,0);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
            {
                Vector3 p=transform.position+rotation*Vector3.Scale(new Vector3(bounds.center.x+bounds.extents.x*x*.98f,0,bounds.center.z+bounds.extents.z*z*.98f),scale);
                p=liquid.InverseTransformPoint(p);if(Mathf.Abs(p.x)>width*.5f || Mathf.Abs(p.z)>length*.5f)return false;
            }
            return true;
        }
        public bool RefreshPlacement(bool snap)
        {
            detectedWater=null;detectedLava=null;CurrentLiquid=Liquid.None;float best=float.MaxValue;
            if(water!=null || lava!=null)
            {
                if(water!=null && SameWorld(water) && water.enabled && Fits(water.transform,water.width,water.length)){detectedWater=water;CurrentLiquid=Liquid.Water;}
                if(lava!=null && SameWorld(lava) && lava.enabled && Fits(lava.transform,lava.width,lava.length)){detectedLava=lava;detectedWater=null;CurrentLiquid=Liquid.Lava;}
            }
            else
            {
                foreach(var w in SonicWaterVolume.Active)if(w!=null && SameWorld(w) && w.enabled && Fits(w.transform,w.width,w.length))
                {float d=Mathf.Abs(transform.position.y-w.transform.position.y);if(d<best){best=d;detectedWater=w;CurrentLiquid=Liquid.Water;}}
                foreach(var l in FindObjectsByType<SonicLavaVolume>())if(SameWorld(l) && l.enabled && Fits(l.transform,l.width,l.length))
                {float d=Mathf.Abs(transform.position.y-l.transform.position.y);if(d<best){best=d;detectedWater=null;detectedLava=l;CurrentLiquid=Liquid.Lava;}}
            }
            PlacementValid=CurrentLiquid!=Liquid.None;
            Status=PlacementValid?(CurrentLiquid==Liquid.Water?"Sur l'eau":"Sur la lave"):"Placement invalide : toute la plateforme doit etre sur l'eau ou la lave.";
            if(PlacementValid && snap)
            {
                var liquid=CurrentLiquid==Liquid.Water?detectedWater.transform:detectedLava.transform;
                Vector3 p=liquid.InverseTransformPoint(transform.position);p.y=0;float surface=liquid.TransformPoint(p).y;
                Vector3 target=transform.position;target.y=surface-RootDeckBounds.min.y*Mathf.Abs(transform.lossyScale.y)-immersion;
                transform.position=target;if(body!=null)body.position=target;
            }
            if(Application.IsPlaying(gameObject) && deckCollider!=null)deckCollider.enabled=PlacementValid;
            return PlacementValid;
        }
        void Update()
        {
            if(!Application.IsPlaying(gameObject)){RefreshPlacement(snapInEditor);UpdateFireVisuals();}
        }
        void FixedUpdate()
        {
            if(!Application.IsPlaying(gameObject))return;
            if(Time.time>=searchAt){searchAt=Time.time+.5f;players=FindObjectsByType<PlayerBhysics>();RefreshPlacement(false);}
            PrepareStep(Time.fixedDeltaTime,players);
        }
        bool SupportGeometry(PlayerBhysics player,out RaycastHit hit)
        {
            hit=default;if(player==null || !SameWorld(player) || player.p_rigidbody==null || player.p_rigidbody.isKinematic)return false;
            Vector3 up=transform.up;
            return World.Raycast(player.p_rigidbody.position+up*2,-up,out hit,2+Mathf.Max(.1f,player.RayToGroundDistance)+.2f,player.Playermask,QueryTriggerInteraction.Ignore) && hit.collider==deckCollider && Vector3.Dot(hit.normal,up)>.35f && Vector3.Dot(player.p_rigidbody.position-hit.point,up)>.01f;
        }
        public bool Supported(PlayerBhysics player,out RaycastHit hit)
        {
            hit=default;if(player==null || player.p_rigidbody==null || !player.Grounded)return false;
            var hurt=player.GetComponent<HurtControl>();if(hurt!=null && hurt.isDead)return false;
            var actions=player.GetComponent<ActionManager>();if(actions!=null && actions.Action!=0 && actions.Action!=3)return false;
            if(Vector3.Dot(player.p_rigidbody.linearVelocity,transform.up)>1)return false;
            return SupportGeometry(player,out hit);
        }
        public static bool IsSupportedOverLava(PlayerBhysics player,SonicLavaVolume liquid)
        {
            foreach(var platform in Active)if(platform!=null && platform.isActiveAndEnabled && platform.PlacementValid && platform.detectedLava==liquid && platform.deckCollider!=null && platform.deckCollider.enabled && platform.SupportGeometry(player,out _))return true;
            return false;
        }
        public void PrepareStep(float dt,PlayerBhysics[] candidates)
        {
            if(!initialized)Initialize();if(dt<=0)return;
            supported.Clear();if(!PlacementValid){if(!warned){warned=true;if(Application.IsPlaying(gameObject))Debug.LogWarning(Status,this);}ResetFire();UpdateFireVisuals();return;}
            Vector3 pressure=Vector3.zero;var bounds=RootDeckBounds;
            foreach(var p in candidates)if(Supported(p,out _))
            {
                supported.Add(p);Vector3 local=transform.InverseTransformPoint(p.p_rigidbody.position)-bounds.center;
                pressure+=new Vector3(local.x/Mathf.Max(.05f,bounds.extents.x),0,local.z/Mathf.Max(.05f,bounds.extents.z));
            }
            if(supported.Count>0)pressure/=supported.Count;pressure=Vector3.ClampMagnitude(pressure,1);
            Vector3 horizontal=restRotation*pressure;
            Quaternion wanted=Quaternion.FromToRotation(Vector3.up,(Vector3.up+horizontal*Mathf.Tan(maximumTilt*Mathf.Deg2Rad)).normalized)*restRotation;
            if(!tiltEnabled)wanted=restRotation;
            Quaternion oldRotation=transform.rotation;Vector3 oldPosition=transform.position;
            Quaternion rotation=Quaternion.RotateTowards(oldRotation,wanted,(supported.Count>0?tiltSpeed:returnSpeed)*dt);
            Quaternion delta=rotation*Quaternion.Inverse(oldRotation);
            if(carrySonic)foreach(var p in supported)
            {
                var rb=p.p_rigidbody;rb.position=oldPosition+delta*(rb.position-oldPosition);rb.linearVelocity=delta*rb.linearVelocity;
                p.transform.position=rb.position;p.GroundNormal=delta*p.GroundNormal;
            }
            body.rotation=rotation;transform.rotation=rotation;Physics.SyncTransforms();
            TickFire(dt);UpdateFireVisuals();EmitFlames(dt);
        }
        public void ApplyEffects(float dt)
        {
            if(!PlacementValid)return;
            foreach(var p in supported)if(Supported(p,out var hit))
            {
                p.p_rigidbody.linearVelocity+=SlideVelocity(hit.normal,dt);
                TryFireDamage(p,hit.point,Time.time);
            }
        }
        public Vector3 SlideVelocity(Vector3 normal,float dt)=>Vector3.ProjectOnPlane(Vector3.down,normal)*slideAcceleration*dt;
        Vector2 UV(Vector3 local)
        {
            var b=DeckBounds;return new Vector2((local.x-b.min.x)/Mathf.Max(.01f,b.size.x),(local.z-b.min.z)/Mathf.Max(.01f,b.size.z));
        }
        public bool TopTouchesLava(out Vector2 ignition)
        {
            ignition=Vector2.zero;if(CurrentLiquid!=Liquid.Lava || detectedLava==null || deckMesh==null || deckMesh.sharedMesh==null)return false;
            var mesh=deckMesh.sharedMesh;var vertices=mesh.vertices;var normals=mesh.normals;
            for(int i=0;i<vertices.Length;i++)if(normals[i].y>.35f && detectedLava.Contains(deck.transform.TransformPoint(vertices[i])))
            {ignition=UV(vertices[i]);return true;}return false;
        }
        public void TickFire(float dt)
        {
            if(!fireEnabled || CurrentLiquid!=Liquid.Lava){ResetFire();return;}
            if(FireState==FirePhase.Normal)
            {
                if(!TopTouchesLava(out var start)){contactTimer=0;return;}contactTimer+=dt;
                if(contactTimer>=ignitionDelay){IgnitionPoint=start;FireState=FirePhase.Spreading;FireStrength=1;fireTimer=0;}
            }
            else
            {
                fireTimer+=dt;
                if(FireState==FirePhase.Spreading){FireCoverage=Mathf.Clamp01(fireTimer/spreadSeconds);if(fireTimer>=spreadSeconds){FireState=FirePhase.Burning;fireTimer=0;FireCoverage=1;}}
                else if(FireState==FirePhase.Burning){if(fireTimer>=burnSeconds){FireState=FirePhase.Cooling;fireTimer=0;}}
                else if(FireState==FirePhase.Cooling){FireStrength=Mathf.Clamp01(1-fireTimer/coolSeconds);if(fireTimer>=coolSeconds){FireState=FirePhase.Resting;FireCoverage=FireStrength=0;fireTimer=0;}}
                else if(FireState==FirePhase.Resting && fireTimer>=fireRestSeconds){FireState=FirePhase.Normal;contactTimer=0;fireTimer=0;}
            }
        }
        void ResetFire(){FireState=FirePhase.Normal;FireCoverage=FireStrength=fireTimer=contactTimer=0;}
        public bool PointBurning(Vector3 world)
        {
            if(!fireEnabled || FireStrength<=.1f || FireCoverage<=0 || deck==null)return false;
            Vector2 p=UV(deck.transform.InverseTransformPoint(world));float furthest=0;
            foreach(var corner in new[]{Vector2.zero,Vector2.one,Vector2.right,Vector2.up})furthest=Mathf.Max(furthest,Vector2.Distance(IgnitionPoint,corner));
            return Vector2.Distance(p,IgnitionPoint)<=FireCoverage*Mathf.Max(.01f,furthest)+.005f;
        }
        public bool TryFireDamage(PlayerBhysics p,Vector3 contact,float now)
        {
            if(p==null || !PointBurning(contact))return false;var hurt=p.GetComponent<HurtControl>();
            if(hurt==null || hurt.isDead || hurt.IsHurt || hurt.IsInvencible || (nextDamage.TryGetValue(p,out var next) && now<next))return false;
            var interaction=p.GetComponent<Objects_Interaction>();if(interaction==null || interaction.Actions==null || interaction.Actions.Action==4)return false;
            nextDamage[p]=now+damageInterval;interaction.DamagePlayer();DamageEvents++;HedgeCamera.Shakeforce=interaction.EnemyDamageShakeAmmount;return true;
        }
        public void UpdateFireVisuals()
        {
            if(deckMesh==null && deck!=null)deckMesh=deck.GetComponent<MeshFilter>();
            if(fireMesh!=null && deckMesh!=null)fireMesh.sharedMesh=deckMesh.sharedMesh;
            // Retain the old references for existing prefab instances, but never draw on the wood.
            if(fireRenderer!=null)fireRenderer.enabled=false;
            if(flames!=null && FireStrength<=0)flames.Clear();
            if(embers!=null && (FireStrength<=0 || !embersEnabled))embers.Clear();
        }
        public void ConfigureFlames()
        {
            if(flames==null)return;
            flames.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=flames.main;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.Local;
            main.scalingMode=ParticleSystemScalingMode.Shape;main.maxParticles=2000;main.startSize3D=true;main.startColor=Color.white;
            var emission=flames.emission;emission.enabled=false;var shape=flames.shape;shape.enabled=false;
            var color=flames.colorOverLifetime;color.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(1,.6f),new GradientAlphaKey(0,1)});color.color=gradient;
            var size=flames.sizeOverLifetime;size.enabled=false;
            var renderer=flames.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.VerticalBillboard;
            var shader=Shader.Find("Sonic FX/Flammes animees");
            if(shader!=null && (renderer.sharedMaterial==null || renderer.sharedMaterial.shader!=shader))
            {
                if(animatedFlameMaterial==null)animatedFlameMaterial=new Material(shader){name="Flammes animees",hideFlags=HideFlags.DontSave};
                renderer.sharedMaterial=animatedFlameMaterial;
            }
            ConfigureEmbers();
        }
        void ConfigureEmbers()
        {
            if(embers==null)
            {
                var child=transform.Find("Braises");if(child==null){var go=new GameObject("Braises");go.transform.SetParent(transform,false);child=go.transform;}
                embers=child.GetComponent<ParticleSystem>();if(embers==null)embers=child.gameObject.AddComponent<ParticleSystem>();
            }
            embers.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=embers.main;
            main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.scalingMode=ParticleSystemScalingMode.Shape;
            main.maxParticles=300;main.startSize3D=false;main.startColor=Color.white;
            var emission=embers.emission;emission.enabled=false;var shape=embers.shape;shape.enabled=false;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(1,.85f,.2f),0),new GradientColorKey(new Color(1,.23f,.02f),1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.1f),new GradientAlphaKey(0,1)});
            var color=embers.colorOverLifetime;color.enabled=true;color.color=gradient;
            var size=embers.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,.15f));
            var renderer=embers.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Billboard;
            var shader=Shader.Find("Sonic FX/Braises");
            if(shader!=null && (renderer.sharedMaterial==null || renderer.sharedMaterial.shader!=shader))
            {
                if(emberMaterial==null)emberMaterial=new Material(shader){name="Braises",hideFlags=HideFlags.DontSave};
                renderer.sharedMaterial=emberMaterial;
            }
        }
        public void EmitFlames(float dt)
        {
            if(flames==null || !fireEnabled || FireCoverage<=0 || FireStrength<=0 || deckCollider==null || !deckCollider.enabled)return;
            if(!flames.isPlaying)flames.Play();
            emitRemainder+=flamesPerSecond*FireCoverage*FireStrength*dt;int count=Mathf.Min(2000,Mathf.FloorToInt(emitRemainder));emitRemainder-=count;
            var bounds=DeckBounds;float furthest=0;
            foreach(var corner in new[]{Vector2.zero,Vector2.one,Vector2.right,Vector2.up})furthest=Mathf.Max(furthest,Vector2.Distance(IgnitionPoint,corner));
            float radius=furthest*FireCoverage;
            for(int n=0;n<count;n++)for(int attempt=0;attempt<32;attempt++)
            {
                Vector2 uv=IgnitionPoint+Random.insideUnitCircle*radius;if(uv.x<0 || uv.x>1 || uv.y<0 || uv.y>1)continue;
                Vector3 local=new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,uv.x),bounds.max.y+.5f,Mathf.Lerp(bounds.min.z,bounds.max.z,uv.y));
                Vector3 origin=deck.transform.TransformPoint(local);float distance=(bounds.size.y+1)*Mathf.Abs(deck.transform.lossyScale.y);
                if(!deckCollider.Raycast(new Ray(origin,-deck.transform.up),out var hit,distance) || !PointBurning(hit.point))continue;
                float height=Mathf.Max(.1f,flameHeight)*Random.Range(.75f,1.25f)*Mathf.Lerp(.4f,1,FireStrength);
                float width=Mathf.Max(.1f,flameWidth)*Random.Range(.8f,1.2f);
                // Generated flipbook base lies at 6% of cell height; red stores a uniform particle animation phase.
                flames.Emit(new ParticleSystem.EmitParams{position=flames.transform.InverseTransformPoint(hit.point+Vector3.up*(height*.44f+.025f)),velocity=flames.transform.InverseTransformVector(Vector3.up*.12f),startLifetime=Random.Range(.9f,1.4f),startSize3D=new Vector3(width,height,width),startColor=new Color(Random.value,1,1,FireStrength)},1);
                if(embersEnabled && embers!=null && Random.value<embersPerSecond/Mathf.Max(1,flamesPerSecond))
                {
                    if(!embers.isPlaying)embers.Play();
                    embers.Emit(new ParticleSystem.EmitParams{position=hit.point+Vector3.up*.15f,velocity=new Vector3(Random.Range(-.3f,.3f),Random.Range(.7f,1.8f),Random.Range(-.3f,.3f)),startLifetime=Random.Range(1,2),startSize=Random.Range(.035f,.085f),startColor=new Color(1,.7f,.12f,FireStrength)},1);
                }
                break;
            }
        }
        void OnDisable()
        {
            Active.Remove(this);
            if(deckCollider!=null && deckMaterial!=null)deckCollider.sharedMaterial=originalMaterial;
            if(deckMaterial!=null){if(Application.IsPlaying(gameObject))Destroy(deckMaterial);else DestroyImmediate(deckMaterial);deckMaterial=null;}
            if(flames!=null)flames.Clear();supported.Clear();initialized=false;
            if(embers!=null)embers.Clear();
            if(animatedFlameMaterial!=null){if(Application.IsPlaying(gameObject))Destroy(animatedFlameMaterial);else DestroyImmediate(animatedFlameMaterial);animatedFlameMaterial=null;}
            if(emberMaterial!=null){if(Application.IsPlaying(gameObject))Destroy(emberMaterial);else DestroyImmediate(emberMaterial);emberMaterial=null;}
        }
        void OnDrawGizmosSelected()
        {
            if(deck==null)return;Gizmos.matrix=deck.transform.localToWorldMatrix;Gizmos.color=PlacementValid?new Color(.1f,.8f,1):Color.red;Gizmos.DrawWireCube(DeckBounds.center,DeckBounds.size);
        }
    }
}
