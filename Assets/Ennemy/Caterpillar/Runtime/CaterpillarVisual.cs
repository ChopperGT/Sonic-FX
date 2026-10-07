using UnityEngine;
namespace SonicFX.Caterpillar
{
    public class CaterpillarVisual : MonoBehaviour
    {
        public Transform[] segments;
        public Transform jaw;
        public Transform[] antennae;
        public void Pose(CaterpillarController.Behaviour state,float contraction,float phase,float speed,float compressedSpacing)
        {
            if(segments==null)return;
            bool moving=speed>0;float spacing=Mathf.Lerp(1.05f,1.05f*compressedSpacing,contraction);
            for(int i=0;i<segments.Length;i++)
            {
                float wave=moving?Mathf.Sin(phase-i*1.0f):Mathf.Sin(phase*.6f-i*.7f)*.12f;
                float lift=i==0?0:Mathf.Max(0,wave)*.24f;
                if(state==CaterpillarController.Behaviour.Charging)lift=Mathf.Sin(i*.9f)*.035f;
                segments[i].localPosition=new Vector3(i==0?0:wave*.08f,.91f+lift+contraction*.16f,-i*spacing);
                segments[i].localRotation=Quaternion.Euler(i==0?contraction*-12:wave*12,0,i==0?0:wave*4);
                segments[i].localScale=new Vector3(1+contraction*.13f,1+contraction*.14f,1-contraction*.16f);
            }
            if(jaw!=null)jaw.localRotation=Quaternion.Euler(state==CaterpillarController.Behaviour.Charging?30:contraction*12,0,0);
            if(antennae!=null)for(int i=0;i<antennae.Length;i++)antennae[i].localRotation=Quaternion.Euler(contraction*-24+Mathf.Sin(phase*1.3f+i)*4,0,(i==0?-1:1)*8);
        }
    }
}
