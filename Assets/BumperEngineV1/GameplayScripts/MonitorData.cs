using UnityEngine;
using System.Collections;

public enum MonitorType
{
    Ring, Shield
}

public class MonitorData : MonoBehaviour {

    public MonitorType Type;
    public int RingAmount;
    [Min(0), Tooltip("Points obtenus a la destruction du monitor.")] public int ScoreOnDestroy = 500;
    bool destroyed;
    public bool IsDestroyed => destroyed;

    public GameObject MonitorExplosion;

    bool ClaimScore()
    {
        if(destroyed)return false;
        destroyed=true;SonicFX.Score.SonicLevelScore.AddPoints(ScoreOnDestroy);return true;
    }
    public void DestroyMonitor()
    {
        if(!ClaimScore())return;
        if(MonitorExplosion != null) GameObject.Instantiate(MonitorExplosion, transform.position, Quaternion.identity);
        Destroy(gameObject);
    }

}
