using UnityEngine;
namespace SonicFX.Structures
{
    public sealed class SonicWideSpringHitState : StateMachineBehaviour
    {
        public override void OnStateEnter(Animator animator,AnimatorStateInfo stateInfo,int layerIndex)
        {
            var slot=animator.GetComponent<SonicWideSpringSlot>();if(slot!=null)slot.Hit();
        }
    }
}
