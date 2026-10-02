using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class LevelProgressControl : MonoBehaviour {

    public Vector3 ResumePosition { get; set; }
    public Quaternion ResumeRotation { get; set; }
    public Transform ResumeTransform;
    ActionManager Actions;
    PlayerBhysics Player;
    CameraControl Cam;
    public GameObject CurrentCheckPoint { get; set; }

    public Material LampDone;
    public int LevelToGoNext = 0;
    [Tooltip("Chemin de la scene suivante dans le Build. Vide : utilise le parcours Histoire, puis Level To Go Next pour les autres niveaux.")] public string NextLevelScene;
    public string NextLevelNameLeft;
    public string NextLevelNameRight;
    public AudioClip GoalRingTouchingSound;

    [Header("Bonus de fin de niveau")]
    [Min(0), InspectorName("Bonus temps maximum")] public int MaximumTimeBonus = 10000;
    [Min(0), InspectorName("Temps ideal (secondes)")] public float IdealTimeSeconds = 120;
    [Min(.01f), InspectorName("Limite du bonus (secondes)")] public float TimeBonusLimitSeconds = 300;
    HurtControl scoreHurt;

    void OnValidate()
    {
        MaximumTimeBonus=Mathf.Max(0,MaximumTimeBonus);
        IdealTimeSeconds=Mathf.Max(0,IdealTimeSeconds);
        TimeBonusLimitSeconds=Mathf.Max(IdealTimeSeconds+.01f,TimeBonusLimitSeconds);
    }

    bool readyForNextStage = false;
    float readyCount = 0;

    bool firstime = false;

    void Start () {

        SonicFX.Score.SonicLevelScore.BeginLevel(gameObject.scene);
        Objects_Interaction.RingAmount=0;
        scoreHurt=GetComponent<HurtControl>();
        Cam = GetComponent<CameraControl>();
        Actions = GetComponent<ActionManager>();
        Player = GetComponent<PlayerBhysics>();

    }

    void Update()
    {
        SonicFX.Score.SonicLevelScore.Tick(Time.deltaTime,scoreHurt!=null && scoreHurt.isDead);
        LampDone.SetTextureOffset("_MainTex", new Vector2(0, -Time.time) * 3);
        LampDone.SetTextureOffset("_EmissionMap", new Vector2(0, -Time.time) * 3);

        if (readyForNextStage)
        {
            Player.MoveInput = Vector3.zero;
            readyCount += Time.deltaTime;
            if(readyCount > 1.5f)
            {
                Actions.Action04Control.enabled = false;
                Color alpha = Color.black;
                Actions.Action04Control.FadeOutImage.color = Color.Lerp(Actions.Action04Control.FadeOutImage.color, alpha, Time.fixedTime * 0.1f);
            }
            if(readyCount > 2.6f)
            {
                LoadingScreenControl.StageName1 = NextLevelNameLeft;
                LoadingScreenControl.StageName2 = NextLevelNameRight;
                SceneManager.LoadScene(2);
            }
        }
    }

    void LateUpdate()
    {
        if (!firstime)
        {
            ResumePosition = transform.position;
            ResumeRotation = transform.rotation;
            firstime = true;
        }
    }

    public void ResetToCheckPoint()
    {
        //Debug.Log("Reset");

		Objects_Interaction.RingAmount = 0;

		if (Monitors_Interactions.HasShield) 
		{
			Monitors_Interactions.HasShield = false;
		}

        transform.position = ResumePosition;
        transform.rotation = ResumeRotation;
        Actions.Action04.deadCounter = 0;
        var dir = -ResumeTransform.forward;
        dir.y = 0.2f;
        Cam.Cam.SetCamera(-9);
    }
    public void SetCheckPoint(Transform position)
    {
        ResumePosition = position.position;
        ResumeRotation = position.rotation;
        ResumeTransform = position;
    }

    public void OnTriggerEnter(Collider col)
    {
        if(col.tag == "Checkpoint")
        {
            if (col.GetComponent<CheckPointData>() != null)
            {
                //Set Object
                if (!col.GetComponent<CheckPointData>().IsOn)
                {
                    col.GetComponent<CheckPointData>().IsOn = true;
                    col.GetComponent<CheckPointData>().Renderer.material = LampDone;
                    col.GetComponent<AudioSource>().Play();
                    SetCheckPoint(col.GetComponent<CheckPointData>().CheckPos);
                    CurrentCheckPoint = col.gameObject;
                }
            }
        }
        if (col.tag == "GoalRing" && !readyForNextStage)
        {
            var hurt=GetComponent<HurtControl>();
            if(hurt!=null && hurt.isDead)return;
            string next=ResolveNextLevelScene(gameObject.scene.path);
            if(SonicFX.Score.SonicLevelScore.Complete(Objects_Interaction.RingAmount,next,MaximumTimeBonus,IdealTimeSeconds,TimeBonusLimitSeconds)==null)return;
            readyForNextStage=true;
            if(GoalRingTouchingSound!=null)AudioSource.PlayClipAtPoint(GoalRingTouchingSound,transform.position);
            Monitors_Interactions.HasShield=false;
            SceneManager.LoadScene("StageCompleteScreen");
        }
    }

    public string ResolveNextLevelScene(string currentScene)
    {
        string next=ConfiguredNextLevelScene(currentScene);
        return !string.IsNullOrEmpty(next)&&Application.CanStreamedLevelBeLoaded(next)?next:null;
    }

    public string ConfiguredNextLevelScene(string currentScene)
    {
        string next=NextLevelScene;
        if(string.IsNullOrEmpty(next))
        {
            // A configured future stage must not fall back to an unrelated build index.
            bool inStoryRoute=SonicFX.Menu.SonicStoryRoute.TryResolve(SonicFX.Menu.SonicXProgress.Character,currentScene,out next);
            if(!inStoryRoute && LevelToGoNext>0 && LevelToGoNext<SceneManager.sceneCountInBuildSettings)
                next=SceneUtility.GetScenePathByBuildIndex(LevelToGoNext);
        }
        return next;
    }
}
