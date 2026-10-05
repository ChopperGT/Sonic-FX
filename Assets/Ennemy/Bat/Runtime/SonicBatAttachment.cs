using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SonicFX.Water;
namespace SonicFX.Bat
{
    [DefaultExecutionOrder(-50),DisallowMultipleComponent]
    public class SonicBatAttachment : MonoBehaviour
    {
        readonly List<BatController> bats=new List<BatController>();
        readonly Dictionary<EnemyHealth,float> enemyContacts=new Dictionary<EnemyHealth,float>();
        sealed class FallsCache {public float refresh;public SonicWaterfall[] falls;}
        static readonly Dictionary<Scene,FallsCache> fallCache=new Dictionary<Scene,FallsCache>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache(){fallCache.Clear();}
        public static void RefreshWaterfallCache(){fallCache.Clear();}
        PlayerBhysics player;ActionManager actions;HurtControl hurt;
        Vector3 previous;bool previousValid;float wetRefresh;
        SonicWaterfall[] waterfalls=new SonicWaterfall[0];
        public int Count=>bats.Count;
        public float Progress {get;private set;}
        public float DamageTimer {get;private set;}
        public int DamageEvents {get;private set;}
        public float SpeedPenalty {get{float sum=0;foreach(var bat in bats)if(bat!=null)sum+=Mathf.Max(0,bat.speedPenalty);return sum;}}
        public static bool BlocksBall(Component c){var status=c.GetComponent<SonicBatAttachment>();return status!=null && status.Count>0;}
        public static float Penalty(Component c){var status=c.GetComponent<SonicBatAttachment>();return status==null?0:status.SpeedPenalty;}
        void Awake(){player=GetComponent<PlayerBhysics>();actions=GetComponent<ActionManager>();hurt=GetComponent<HurtControl>();previous=transform.position;previousValid=true;}
        public void Add(BatController bat)
        {
            if(bats.Contains(bat))return;bats.Add(bat);Progress=0;if(player==null)Awake();
            if(player!=null){player.isRolling=false;var rb=player.GetComponent<Rigidbody>();if(rb!=null){Vector3 horizontal=Vector3.ProjectOnPlane(rb.linearVelocity,Vector3.up);rb.linearVelocity-=horizontal.normalized*Mathf.Min(horizontal.magnitude,Mathf.Max(0,bat.speedPenalty));}}
            if(actions!=null)
            {
                if(actions.Action!=0 && actions.Action!=1 && actions.Action!=4 && actions.Action00!=null)actions.ChangeAction(0);
                if(actions.Action01!=null && actions.Action01.JumpBall!=null)actions.Action01.JumpBall.SetActive(false);
            }
            PositionBat(bat);
        }
        public void Remove(BatController bat){bats.Remove(bat);if(bats.Count==0){Progress=DamageTimer=0;enemyContacts.Clear();}}
        public void PositionBat(BatController bat)
        {
            int i=bats.IndexOf(bat);if(i<0)return;
            Transform skin=actions!=null && actions.Action00!=null && actions.Action00.CharacterAnimator!=null?actions.Action00.CharacterAnimator.transform:transform;
            float angle=i*137.5f*Mathf.Deg2Rad;
            bat.transform.SetPositionAndRotation(skin.position+skin.up*(.55f+(i%3)*.26f)+skin.right*Mathf.Cos(angle)*.55f+skin.forward*Mathf.Sin(angle)*.45f,skin.rotation);
        }
        void Update(){if(Time.timeScale>0 && Count>0 && PadInput.GetButtonDown("R1"))PressRoll();}
        public void PressRoll()
        {
            if(Count==0)return;var rule=bats[0];int presses=Mathf.Max(1,rule.baseRollPresses+rule.extraRollPresses*(Count-1));
            Progress=Mathf.Min(1,Progress+1f/presses);
            if(Progress>=.9999f){bats[bats.Count-1].Escape(false);Progress=0;}
        }
        void FixedUpdate(){Tick(Time.fixedDeltaTime);}
        public void Tick(float dt)
        {
            bats.RemoveAll(b=>b==null);
            if(hurt!=null && hurt.isDead){Repel(false);return;}
            if(Count==0){DamageTimer=Progress=0;previous=transform.position;previousValid=true;return;}
            if(IsWet(player,previousValid?previous:transform.position,ref waterfalls,ref wetRefresh)){Repel(true);previous=transform.position;return;}
            previous=transform.position;previousValid=true;player.isRolling=false;
            Progress=Mathf.Max(0,Progress-Mathf.Max(0,bats[0].escapeDecay)*dt);
            var rule=bats[0];if(Count<rule.damageThreshold)DamageTimer=0;
            else if((DamageTimer+=dt)>=rule.damageDelay)
            {
                DamageTimer=0;DamageEvents++;
                var interaction=GetComponent<Objects_Interaction>();if(interaction!=null)interaction.DamagePlayer();
            }
        }
        public void Repel(bool water){foreach(var bat in bats.ToArray())if(bat!=null)bat.Escape(water);bats.Clear();Progress=DamageTimer=0;}
        public int EnemyImpact(Collider enemy)
        {
            if(enemy==null || Count==0 || enemy.GetComponentInParent<BatController>()!=null)return 0;
            var health=enemy.GetComponentInParent<EnemyHealth>();if(health==null)return 0;
            if(enemyContacts.TryGetValue(health,out float last) && Time.time-last<.25f)return 0;enemyContacts[health]=Time.time;
            return ResolveEnemyImpact(()=>Random.value);
        }
        public int ResolveEnemyImpact(System.Func<float> roll)
        {
            int removed=0;foreach(var bat in bats.ToArray())if(bat!=null && roll()<bat.enemyDestroyChance)
            {
                Remove(bat);bat.Escape(false);removed++;
                if(Application.isPlaying)Destroy(bat.gameObject);else DestroyImmediate(bat.gameObject);
            }
            return removed;
        }
        public static bool IsWet(PlayerBhysics p,Vector3 old){return p!=null && IsWetPosition(p.transform.position+Vector3.up*.4f,old+Vector3.up*.4f,p.gameObject.scene);}
        public static bool IsWetPosition(Vector3 pos,Vector3 old,Scene scene)
        {
            foreach(var water in SonicWaterVolume.Active)if(water!=null && water.gameObject.scene==scene && water.isActiveAndEnabled && (water.Contains(pos)||water.Contains(old)||water.CrossesSurface(old,pos,out _)))return true;
            foreach(var fall in FallsInScene(scene))if(fall!=null && fall.isActiveAndEnabled && (fall.ContainsCurtain(pos)||fall.IntersectsCurtain(old,pos,out _)))return true;
            return false;
        }
        static bool IsWet(PlayerBhysics p,Vector3 old,ref SonicWaterfall[] falls,ref float refresh)
        {
            if(p==null)return false;Vector3 now=p.transform.position+Vector3.up*.4f;
            foreach(var water in SonicWaterVolume.Active)if(water!=null && water.gameObject.scene==p.gameObject.scene && water.isActiveAndEnabled && (water.Contains(now)||water.Contains(old+Vector3.up*.4f)||water.CrossesSurface(old+Vector3.up*.4f,now,out _)))return true;
            if(Time.time>=refresh){falls=FallsInScene(p.gameObject.scene);refresh=Time.time+1;}
            foreach(var fall in falls)if(fall!=null && fall.gameObject.scene==p.gameObject.scene && fall.isActiveAndEnabled && (fall.ContainsCurtain(now)||fall.IntersectsCurtain(old+Vector3.up*.4f,now,out _)))return true;
            return false;
        }
        static SonicWaterfall[] FallsInScene(Scene scene)
        {
            if(fallCache.TryGetValue(scene,out var cached) && Time.realtimeSinceStartup<cached.refresh)return cached.falls;
            var result=new List<SonicWaterfall>();
            if(scene.IsValid())foreach(var root in scene.GetRootGameObjects())result.AddRange(root.GetComponentsInChildren<SonicWaterfall>(false));
            var falls=result.ToArray();fallCache[scene]=new FallsCache{refresh=Time.realtimeSinceStartup+.25f,falls=falls};return falls;
        }
        void OnDisable(){Repel(false);}
        void OnGUI()
        {
            if(Count==0 || (hurt!=null && hurt.isDead))return;
            float s=Mathf.Clamp(Screen.height/900f,.6f,2);Rect box=new Rect((Screen.width-400*s)*.5f,Screen.height-120*s,400*s,95*s);
            GUI.Box(box,GUIContent.none);GUI.Label(new Rect(box.x+12*s,box.y+8*s,box.width-24*s,28*s),"CHAUVE-SOURIS : "+Count+"   |   Appuie sur Boule (R1)");
            Color old=GUI.color;GUI.color=new Color(.05f,.75f,1);GUI.DrawTexture(new Rect(box.x+12*s,box.y+40*s,(box.width-24*s)*Progress,16*s),Texture2D.whiteTexture);GUI.color=old;
            if(Count>=bats[0].damageThreshold)GUI.Label(new Rect(box.x+12*s,box.y+60*s,box.width-24*s,25*s),"Degats dans "+Mathf.CeilToInt(Mathf.Max(0,bats[0].damageDelay-DamageTimer))+" s");
        }
    }
}
