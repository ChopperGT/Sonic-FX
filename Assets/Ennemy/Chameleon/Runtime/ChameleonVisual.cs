using UnityEngine;
namespace SonicFX.Chameleon
{
    public sealed class ChameleonVisual : MonoBehaviour
    {
        public ChameleonController controller;
        public Transform body, jaw;
        public Transform[] legs, tail;
        public LineRenderer tongue;
        public Transform tongueTip;
        Quaternion[] legRest, tailRest; Quaternion jawRest; Vector3 bodyRest; float phase;
        void Awake()
        {
            legRest = new Quaternion[legs.Length]; tailRest = new Quaternion[tail.Length];
            for (int i=0;i<legs.Length;i++) legRest[i]=legs[i].localRotation;
            for (int i=0;i<tail.Length;i++) tailRest[i]=tail[i].localRotation;
            jawRest=jaw.localRotation; bodyRest=body.localPosition; HideTongue();
        }
        void LateUpdate()
        {
            if (legRest == null || controller == null) return;
            float movement = Mathf.Clamp01(controller.MoveRate); phase += Time.deltaTime * (3+controller.MoveRate*5);
            bool still = controller.IsCamouflaged;
            for (int i=0;i<legs.Length;i++) {
                float step=Mathf.Sin(phase+(i==0||i==3?0:Mathf.PI))*movement;
                legs[i].localRotation=legRest[i]*Quaternion.Euler(step*25,step*12,controller.IsLeaping?35:0);
            }
            for(int i=0;i<tail.Length;i++) tail[i].localRotation=tailRest[i]*Quaternion.Euler(0,still?0:Mathf.Sin(phase*.3f+i*.4f)*2,0);
            body.localPosition=bodyRest+Vector3.up*(still?0:Mathf.Sin(phase)*.035f*movement);
            jaw.localRotation=jawRest*Quaternion.Euler(controller.AttackPose*32,0,0);
        }
        public void ShowTongue(Vector3 start, Vector3 end, float width)
        {
            if (tongue == null) return;
            tongue.enabled=true; tongue.positionCount=3; tongue.startWidth=width; tongue.endWidth=width*.65f;
            tongue.SetPosition(0,start); tongue.SetPosition(1,Vector3.Lerp(start,end,.5f)); tongue.SetPosition(2,end);
            if(tongueTip!=null) { tongueTip.gameObject.SetActive(true); tongueTip.position=end; }
        }
        public void HideTongue() { if(tongue!=null) tongue.enabled=false; if(tongueTip!=null) tongueTip.gameObject.SetActive(false); }
    }
}
