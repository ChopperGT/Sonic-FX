using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class StageConpleteControl : MonoBehaviour {

    //WHERE EVEN IS THIS SCRIPT AND WHY IS IT HERE?


    public float End;
    float counter;
    public int LevelToGoNext;

    public Animator Anim;

    void Start()
    {
        if(SonicFX.Score.SonicLevelScore.LastResult!=null)
            gameObject.AddComponent<SonicFX.Score.SonicLevelResults>();
    }
    void Update()
    {
        if(SonicFX.Score.SonicLevelScore.LastResult!=null)return;
        ////Debug.Log("i'm here");
        counter += Time.deltaTime;
        if(counter > End)
        {
            Anim.SetInteger("Action", 1);
            if(counter > End + 2.3f)
            {
                SceneManager.LoadScene(LevelToGoNext);
            }
        }
    }

}
