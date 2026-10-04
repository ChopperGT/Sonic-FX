using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SonicFX.Structures
{
    [DisallowMultipleComponent]
    public sealed class SonicBreakBlock : MonoBehaviour
    {
        [Header("Forme et detection")]
        [Tooltip("Collider solide du bloc, qui bloque le chemin.")] public BoxCollider solid;
        public BoxCollider sensor;
        public Transform visual;
        [Min(.01f),InspectorName("Vitesse minimale d'entree")] public float minimumEntrySpeed=1;
        [InspectorName("Autoriser aussi le saut en boule")] public bool allowJumpBall;

        [Header("Difficulte")]
        [Min(1),InspectorName("Appuis pour remplir la barre")] public int requiredPresses=16;
        [Min(.1f),InspectorName("Duree maximale (secondes)")] public float duration=7;
        [Range(0,.5f),InspectorName("Perte de progression par seconde")] public float drainPerSecond=.04f;
        [Min(0),InspectorName("Delai avant nouvelle tentative")] public float retryDelay=1;

        [Header("Bonus de vitesse d'arrivee")]
        [Min(.1f),InspectorName("Vitesse pour le bonus maximal")] public float entryReferenceSpeed=55;
        [Range(0,.95f),InspectorName("Remplissage initial maximal")] public float maximumEntryFill=.6f;

        [Header("Reussite")]
        [Min(0),InspectorName("Vitesse de propulsion")] public float launchSpeed=65;
        [Min(0),InspectorName("Impulsion verticale")] public float launchLift=1;
        [Range(0,32),InspectorName("Nombre de debris")] public int debrisCount=14;
        [Min(.1f)] public float debrisLifetime=1.5f;
        public Material debrisMaterial;

        [Header("Echec et redressement")]
        [Min(0),InspectorName("Force de recul")] public float recoilSpeed=15;
        [Min(.1f),InspectorName("Impulsion vers le haut")] public float recoilLift=10;
        [Min(1),InspectorName("Gravite du recul")] public float recoilGravity=40;
        [Min(.1f),InspectorName("Duree du redressement")] public float recoverDuration=.65f;
        [Min(1),Tooltip("Securite : rend les commandes si Sonic ne trouve pas de sol.")] public float maximumRecoilTime=6;

        [Header("Animation et commandes")]
        [Min(.1f),InspectorName("Rotation minimale")] public float minimumSpinSpeed=2;
        [Min(.1f),InspectorName("Rotation maximale")] public float maximumSpinSpeed=12;
        [Tooltip("Nom de la touche de saut dans l'Input Manager. La croix PS5 est aussi reconnue.")] public string jumpButton="A";
        public Key keyboardJump=Key.Space;

        public enum Phase { Idle, Charging, Recoiling, Recovering, Broken }
        public Phase State {get;private set;}
        public float Progress=>challenge==null?0:challenge.Progress;
        public float Remaining=>challenge==null?duration:Mathf.Max(0,duration-challenge.Elapsed);
        static readonly HashSet<Rigidbody> Riders=new HashSet<Rigidbody>();
        readonly List<Behaviour> paused=new List<Behaviour>();
        readonly Dictionary<Renderer,bool> rendererStates=new Dictionary<Renderer,bool>();
        PlayerBhysics player;
        ActionManager actions;
        HurtControl hurt;
        Rigidbody body;
        Animator animator,ballAnimator;
        Renderer spinBall;
        GameObject jumpBall;
        SonicBlockContactProbe contactProbe;
        SonicBreakBlockUI ui;
        SonicBreakChallenge challenge;
        Vector3 direction,up,anchor,entryVelocity,groundNormal;
        bool oldGravity,oldCollisions,oldRolling,oldGrounded;
        Vector3 oldNormal;
        CollisionDetectionMode oldDetection;
        RigidbodyInterpolation oldInterpolation;
        float oldAnimatorSpeed,oldBallSpeed,phaseTime,retryAt;
        int entryFrame;
        bool legacyInputAvailable=true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession(){Riders.Clear();}
        void OnValidate()
        {
            requiredPresses=Mathf.Max(1,requiredPresses);duration=Mathf.Max(.1f,duration);
            entryReferenceSpeed=Mathf.Max(.1f,entryReferenceSpeed);maximumEntryFill=Mathf.Clamp(maximumEntryFill,0,.95f);
            minimumSpinSpeed=Mathf.Max(.1f,minimumSpinSpeed);maximumSpinSpeed=Mathf.Max(minimumSpinSpeed,maximumSpinSpeed);
        }
        void OnCollisionEnter(Collision collision)
        {
            if(!Application.isPlaying || !isActiveAndEnabled || collision.rigidbody==null)return;
            var candidate=collision.rigidbody.GetComponent<PlayerBhysics>();
            if(candidate==null || solid==null)return;
            // Fallback for fast approaches that cross the outer trigger between physics steps.
            Vector3 toward=solid.bounds.center-collision.rigidbody.position;
            Vector3 normal=candidate.Gravity.sqrMagnitude>.01f?-candidate.Gravity.normalized:Vector3.up;
            toward=Vector3.ProjectOnPlane(toward,normal).normalized;
            TryBegin(candidate,toward*collision.relativeVelocity.magnitude);
        }
        void OnTriggerEnter(Collider other){TryCollider(other);}
        void OnTriggerStay(Collider other){TryCollider(other);}
        void TryCollider(Collider other)
        {
            if(!Application.isPlaying || !isActiveAndEnabled || other.attachedRigidbody==null)return;
            TryBegin(other.attachedRigidbody.GetComponent<PlayerBhysics>());
        }
        public bool TryBegin(PlayerBhysics candidate,Vector3? impactVelocity=null)
        {
            if(!isActiveAndEnabled || State!=Phase.Idle || Time.time<retryAt || candidate==null || !candidate.enabled || solid==null)return false;
            var rb=candidate.GetComponent<Rigidbody>();var am=candidate.GetComponent<ActionManager>();var hc=candidate.GetComponent<HurtControl>();
            if(rb==null || rb.isKinematic || !rb.detectCollisions || Riders.Contains(rb) || am==null || am.Action00==null || am.Action00.CharacterAnimator==null || (hc!=null && (hc.isDead || hc.IsHurt)))return false;
            bool rolling=candidate.isRolling && am.Action==0;
            bool jumping=allowJumpBall && am.Action==1 && am.Action01!=null && am.Action01.JumpBall!=null && am.Action01.JumpBall.activeInHierarchy;
            if(!rolling && !jumping)return false;
            Vector3 normal=candidate.Gravity.sqrMagnitude>.01f?-candidate.Gravity.normalized:Vector3.up;
            Vector3 incoming=impactVelocity??rb.linearVelocity;
            Vector3 velocity=Vector3.ProjectOnPlane(incoming,normal);
            Vector3 toward=Vector3.ProjectOnPlane(solid.bounds.center-rb.position,normal);
            if(velocity.magnitude<minimumEntrySpeed || toward.sqrMagnitude<.001f || Vector3.Dot(velocity.normalized,toward.normalized)<.2f)return false;
            if(sensor!=null && !sensor.bounds.Intersects(candidate.CollisionCapsule!=null?candidate.CollisionCapsule.bounds:new Bounds(rb.position,Vector3.one)))return false;

            player=candidate;body=rb;actions=am;hurt=hc;up=normal;direction=velocity.normalized;anchor=rb.position;entryVelocity=incoming;
            animator=am.Action00.CharacterAnimator;oldAnimatorSpeed=animator.speed;
            oldGravity=rb.useGravity;oldCollisions=rb.detectCollisions;oldDetection=rb.collisionDetectionMode;oldInterpolation=rb.interpolation;
            oldRolling=player.isRolling;oldGrounded=player.Grounded;oldNormal=player.GroundNormal;
            jumpBall=am.Action01!=null?am.Action01.JumpBall:null;
            rendererStates.Clear();
            if(am.Action03!=null)
            {
                if(am.Action03.PlayerSkin!=null)foreach(var skin in am.Action03.PlayerSkin)if(skin!=null)rendererStates[skin]=skin.enabled;
                spinBall=am.Action03.SpinDashBall;if(spinBall!=null)rendererStates[spinBall]=spinBall.enabled;
                ballAnimator=am.Action03.BallAnimator;if(ballAnimator!=null)oldBallSpeed=ballAnimator.speed;
            }
            Riders.Add(body);actions.ChangeAction(-1);Pause(player);Pause(body.GetComponent<PlayerBinput>());Pause(body.GetComponent<Objects_Interaction>());Pause(body.GetComponent<Monitors_Interactions>());Pause(body.GetComponent<Rail_Interaction>());
            player.MoveInput=Vector3.zero;player.RawInput=Vector3.zero;
            body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.useGravity=false;body.collisionDetectionMode=CollisionDetectionMode.Discrete;body.isKinematic=true;
            challenge=new SonicBreakChallenge(SonicBreakChallenge.EntryBonus(velocity.magnitude,entryReferenceSpeed,maximumEntryFill),requiredPresses,duration,drainPerSecond);
            State=Phase.Charging;phaseTime=0;entryFrame=Time.frameCount;
            contactProbe=body.gameObject.AddComponent<SonicBlockContactProbe>();contactProbe.owner=this;
            if(Application.isPlaying){var go=new GameObject("Defi bloc - barre");ui=go.AddComponent<SonicBreakBlockUI>();ui.Build(this);}
            SetChargeVisual();return true;
        }
        void Pause(Behaviour component){if(component!=null && component.enabled){paused.Add(component);component.enabled=false;}}
        void Update()
        {
            if(State==Phase.Idle || State==Phase.Broken)return;
            if(player==null || body==null || !player.gameObject.activeInHierarchy || (hurt!=null && hurt.isDead) || actions.Action!=-1){Cancel();return;}
            if(Time.timeScale<=0)return;
            if(State==Phase.Charging)AdvanceChallenge(Time.deltaTime,Time.frameCount>entryFrame && JumpPressed());
            else
            {
                phaseTime+=Time.deltaTime;
                if(State==Phase.Recoiling && phaseTime>maximumRecoilTime)Restore(Vector3.zero,false,false);
                else if(State==Phase.Recovering && phaseTime>=recoverDuration)Restore(Vector3.zero,false,true);
            }
        }
        bool JumpPressed()
        {
            bool pressed=Gamepad.current!=null && Gamepad.current.buttonSouth.wasPressedThisFrame;
            if(Keyboard.current!=null && keyboardJump!=Key.None)pressed|=Keyboard.current[keyboardJump].wasPressedThisFrame;
            if(legacyInputAvailable && !string.IsNullOrEmpty(jumpButton))
            {
                try{pressed|=Input.GetButtonDown(jumpButton);}catch(UnityException){legacyInputAvailable=false;}
            }
            return pressed;
        }
        public void AdvanceChallenge(float seconds,bool pressed)
        {
            if(State!=Phase.Charging)return;
            challenge.Step(seconds,pressed);
            if(challenge.Won)Succeed();else if(challenge.Lost)Fail();
        }
        void FixedUpdate()
        {
            if(body==null)return;
            if(State==Phase.Charging){body.MovePosition(anchor);return;}
            if(State!=Phase.Recoiling)return;
            body.AddForce(-up*recoilGravity,ForceMode.Acceleration);
            if(phaseTime<.15f || Vector3.Dot(body.linearVelocity,up)>1)return;
            // A short foot probe also handles coming to rest without a new collision event.
            var hits=Physics.SphereCastAll(body.position+up*.35f,.15f,-up,.35f,player.Playermask,QueryTriggerInteraction.Ignore);
            foreach(var hit in hits)if(hit.collider.attachedRigidbody!=body && !hit.collider.transform.IsChildOf(transform) && Vector3.Dot(hit.normal,up)>.6f){NotifyGround(hit.normal);break;}
        }
        internal void NotifyGround(Vector3 normal)
        {
            if(State!=Phase.Recoiling || phaseTime<.15f || body==null || Vector3.Dot(body.linearVelocity,up)>1 || Vector3.Dot(normal,up)<.6f)return;
            groundNormal=normal;State=Phase.Recovering;phaseTime=0;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.collisionDetectionMode=CollisionDetectionMode.Discrete;body.isKinematic=true;
            animator.SetBool("Dead",false);animator.SetInteger("Action",0);animator.SetBool("Grounded",true);animator.SetFloat("GroundSpeed",0);
            CrossFade("Base Layer.[00] Grounded",recoverDuration);
        }
        void LateUpdate()
        {
            if(animator==null)return;
            if(State==Phase.Charging)SetChargeVisual();
            else if(State==Phase.Recoiling)
            {
                animator.SetInteger("Action",4);animator.SetBool("Dead",true);animator.SetBool("Grounded",false);
                animator.transform.rotation=Quaternion.LookRotation(direction,up);
            }
            else if(State==Phase.Recovering)
            {
                float t=Mathf.SmoothStep(0,1,phaseTime/Mathf.Max(.1f,recoverDuration));
                animator.transform.rotation=Quaternion.LookRotation(direction,groundNormal)*Quaternion.Euler(Mathf.Lerp(-65,0,t),0,0);
            }
        }
        void SetChargeVisual()
        {
            if(animator==null)return;
            player.isRolling=true;player.Grounded=false;
            animator.SetInteger("Action",1);animator.SetBool("Dead",false);animator.SetBool("isRolling",true);animator.SetBool("Grounded",false);animator.SetFloat("GroundSpeed",Mathf.Lerp(15,90,Progress));
            animator.transform.rotation=Quaternion.LookRotation(direction,up);
            float speed=Mathf.Lerp(minimumSpinSpeed,maximumSpinSpeed,Progress);animator.speed=speed;
            if(jumpBall!=null)jumpBall.SetActive(false);
            if(spinBall!=null)
            {
                foreach(var pair in rendererStates)if(pair.Key!=null)pair.Key.enabled=pair.Key==spinBall;
                if(ballAnimator!=null){ballAnimator.speed=speed;ballAnimator.SetFloat("SpinCharge",Mathf.Lerp(10,100,Progress));}
            }
        }
        void RestoreVisuals()
        {
            foreach(var pair in rendererStates)if(pair.Key!=null)pair.Key.enabled=pair.Value;
            if(animator!=null){animator.speed=oldAnimatorSpeed;animator.SetBool("Dead",false);}
            if(ballAnimator!=null)ballAnimator.speed=oldBallSpeed;
            if(jumpBall!=null)jumpBall.SetActive(false);
        }
        void CrossFade(string state,float seconds){if(animator!=null && animator.HasState(0,Animator.StringToHash(state)))animator.CrossFadeInFixedTime(state,seconds);}
        void Fail()
        {
            State=Phase.Recoiling;phaseTime=0;RestoreVisuals();player.isRolling=false;player.Grounded=false;
            body.isKinematic=false;body.collisionDetectionMode=oldDetection;body.detectCollisions=oldCollisions;body.useGravity=false;
            body.linearVelocity=-direction*recoilSpeed+up*recoilLift;
            animator.SetBool("isRolling",false);animator.SetInteger("Action",4);animator.SetBool("Dead",true);animator.SetBool("Grounded",false);
            CrossFade("Base Layer.[04] Die",.08f);
        }
        void Succeed()
        {
            var breakBounds=solid.bounds;
            solid.enabled=false;if(sensor!=null)sensor.enabled=false;
            if(Application.isPlaying)SpawnDebris(breakBounds);
            if(visual!=null)visual.gameObject.SetActive(false);
            Restore(direction*launchSpeed+up*launchLift,true,false);State=Phase.Broken;
            if(Application.isPlaying)Destroy(gameObject,debrisLifetime+.1f);
        }
        void Restore(Vector3 velocity,bool rolling,bool grounded)
        {
            bool interrupted=(hurt!=null && hurt.isDead) || (actions!=null && actions.Action!=-1);
            RestoreVisuals();
            if(body!=null)
            {
                body.isKinematic=false;body.useGravity=oldGravity;body.detectCollisions=oldCollisions;body.collisionDetectionMode=oldDetection;body.interpolation=oldInterpolation;
                if(!interrupted)body.linearVelocity=velocity;
                body.angularVelocity=Vector3.zero;
            }
            if(player!=null)
            {
                player.isRolling=rolling;player.Grounded=grounded;player.GroundNormal=grounded?groundNormal:up;player.WasOnAir=!grounded;
                player.MoveInput=Vector3.zero;player.RawInput=Vector3.zero;
            }
            foreach(var item in paused)if(item!=null)item.enabled=true;paused.Clear();
            if(actions!=null && !interrupted){actions.ChangeAction(0);if(animator!=null){animator.SetInteger("Action",rolling?1:0);animator.SetBool("isRolling",rolling);animator.SetBool("Grounded",grounded);}}
            if(contactProbe!=null){contactProbe.owner=null;Remove(contactProbe);}
            if(ui!=null)Remove(ui.gameObject);
            Riders.Remove(body);body=null;player=null;actions=null;hurt=null;animator=null;ballAnimator=null;spinBall=null;jumpBall=null;ui=null;contactProbe=null;rendererStates.Clear();
            retryAt=Time.time+retryDelay;State=Phase.Idle;
        }
        void Cancel()
        {
            if(State==Phase.Idle || State==Phase.Broken)return;
            groundNormal=oldNormal;Restore(State==Phase.Charging?entryVelocity:body!=null?body.linearVelocity:Vector3.zero,oldRolling,oldGrounded);
        }
        void OnDisable(){Cancel();}
        void OnDestroy(){Cancel();}
        static void Remove(Object item){if(Application.isPlaying)Destroy(item);else DestroyImmediate(item);}
        void SpawnDebris(Bounds bounds)
        {
            for(int i=0;i<debrisCount;i++)
            {
                var chunk=GameObject.CreatePrimitive(PrimitiveType.Cube);chunk.name="Eclat du bloc";Remove(chunk.GetComponent<Collider>());
                chunk.transform.position=bounds.center+Vector3.Scale(Random.insideUnitSphere,bounds.extents*.65f);chunk.transform.localScale=Vector3.one*Random.Range(.18f,.45f)*Mathf.Min(bounds.size.x,bounds.size.y);
                if(debrisMaterial!=null)chunk.GetComponent<Renderer>().sharedMaterial=debrisMaterial;
                var debris=chunk.AddComponent<SonicBlockDebris>();debris.velocity=Random.onUnitSphere*Random.Range(4,9)+up*4;debris.lifetime=debrisLifetime;
            }
        }
        void OnDrawGizmosSelected()
        {
            if(sensor!=null){Gizmos.color=new Color(0,.8f,1,.7f);Gizmos.matrix=sensor.transform.localToWorldMatrix;Gizmos.DrawWireCube(sensor.center,sensor.size);Gizmos.matrix=Matrix4x4.identity;}
        }
    }
}
