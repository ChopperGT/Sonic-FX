using UnityEngine;
using System.Collections;

public class EnemyHealth : MonoBehaviour {

    public int MaxHealth = 1;
    int HP;
    [Min(0), Tooltip("Points obtenus quand cet ennemi est battu.")] public int ScoreOnDefeat = 50;
    bool defeated;

    public GameObject Explosion;
    public EnemySpawnerEternal SpawnReference { get; set; }

    void Awake()
    {
        HP = MaxHealth;
    }

    bool TakeDamage(int damage)
    {
        if(defeated || damage<=0)return false;
        HP-=damage;
        if(HP>0)return false;
        defeated=true;
        SonicFX.Score.SonicLevelScore.AddPoints(ScoreOnDefeat);
        return true;
    }
    public void DealDamage(int Damage)
    {
        if(TakeDamage(Damage))
        {
            if(SpawnReference != null)
            {
                SpawnReference.ResartSpawner();
            }
            if(Explosion != null) GameObject.Instantiate(Explosion, transform.position,Quaternion.identity);
            Destroy(gameObject);
        }
    }

}
