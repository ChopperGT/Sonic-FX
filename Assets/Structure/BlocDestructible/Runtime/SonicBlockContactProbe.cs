using UnityEngine;
namespace SonicFX.Structures
{
    public sealed class SonicBlockContactProbe : MonoBehaviour
    {
        public SonicBreakBlock owner;
        void OnCollisionEnter(Collision collision){Check(collision);}
        void OnCollisionStay(Collision collision){Check(collision);}
        void Check(Collision collision)
        {
            if(owner==null)return;
            for(int i=0;i<collision.contactCount;i++)owner.NotifyGround(collision.GetContact(i).normal);
        }
    }
}
