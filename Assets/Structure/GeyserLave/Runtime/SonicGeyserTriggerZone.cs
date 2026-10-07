using UnityEngine;
using UnityEngine.Events;
namespace SonicFX.Lava
{
    [RequireComponent(typeof(BoxCollider)),DisallowMultipleComponent,AddComponentMenu("Sonic FX/Lave/Zone declencheur du geyser")]
    public sealed class SonicGeyserTriggerZone : MonoBehaviour
    {
        [InspectorName("Geyser a activer")] public SonicLavaGeyser geyser;
        [InspectorName("Une seule activation")] public bool once=true;
        [InspectorName("Repeter tant que Sonic reste dedans")] public bool repeatWhileInside;
        [InspectorName("Evenement a l'activation")] public UnityEvent onTriggered=new UnityEvent();
        public bool Activated {get;private set;}
        void Reset(){GetComponent<BoxCollider>().isTrigger=true;GetComponent<BoxCollider>().size=new Vector3(12,8,12);}
        void OnValidate(){var box=GetComponent<BoxCollider>();if(box!=null)box.isTrigger=true;}
        void OnTriggerEnter(Collider other){TryTrigger(other);}
        void OnTriggerStay(Collider other){if(repeatWhileInside || !Activated)TryTrigger(other);}
        public bool TryTrigger(Collider other)
        {
            if(other==null || !isActiveAndEnabled || (once && Activated) || geyser==null)return false;
            var player=other.GetComponentInParent<PlayerBhysics>();if(player==null)return false;
            var hurt=player.GetComponent<HurtControl>();if(hurt!=null && hurt.isDead)return false;
            if(!geyser.TryErupt())return false;Activated=true;onTriggered.Invoke();return true;
        }
        public void ResetActivation(){Activated=false;}
        void OnDrawGizmosSelected(){var c=GetComponent<BoxCollider>();if(c==null)return;var m=Gizmos.matrix;Gizmos.matrix=transform.localToWorldMatrix;Gizmos.color=new Color(1,.45f,.03f,.2f);Gizmos.DrawCube(c.center,c.size);Gizmos.color=new Color(1,.65f,.15f,1);Gizmos.DrawWireCube(c.center,c.size);Gizmos.matrix=m;}
    }
}
