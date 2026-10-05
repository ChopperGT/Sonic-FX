using UnityEngine;
namespace SonicFX.Bat
{
    public class BatVisual : MonoBehaviour
    {
        public Transform body,leftWing,rightWing,flame;
        public float flapSpeed=13,flapAngle=42;
        public void Pose(BatController.Behaviour state,float time)
        {
            bool hanging=state==BatController.Behaviour.Sleeping || state==BatController.Behaviour.Watching;
            if(body!=null)body.localRotation=hanging?Quaternion.Euler(0,0,180):Quaternion.identity;
            float flap=hanging?72:state==BatController.Behaviour.Attached?55:Mathf.Sin(time*flapSpeed)*flapAngle;
            if(leftWing!=null)leftWing.localRotation=Quaternion.Euler(0,0,-flap);
            if(rightWing!=null)rightWing.localRotation=Quaternion.Euler(0,0,flap);
            if(flame!=null){flame.gameObject.SetActive(!hanging && state!=BatController.Behaviour.Attached);flame.localScale=new Vector3(.22f,.22f,.45f+Mathf.Sin(time*32)*.09f);}
        }
    }
}
