using UnityEngine;

namespace SonicFX.Structures
{
    public sealed class SonicWideSpringSlot : MonoBehaviour
    {
        [HideInInspector] public SonicWideSpring owner;
        public bool TryActivate(Objects_Interaction player)
        {
            if(owner==null)owner=GetComponentInParent<SonicWideSpring>();
            return owner!=null && owner.isActiveAndEnabled && owner.TryActivate(player);
        }
        public void Hit()
        {
            if(owner==null)owner=GetComponentInParent<SonicWideSpring>();
            if(owner!=null)owner.Pulse();
            var burst=GetComponentInChildren<ParticleSystem>();if(burst!=null)burst.Play();
        }
    }
}
