using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using SonicFX.HUD;
using SonicFX.Menu;

namespace SonicFX.Score
{
    // Ecran de fin de niveau, maquette "Tableaux de score Sonic" (02 Resultats), cotes en 1920x1080.
    public sealed class SonicLevelResults : MonoBehaviour
    {
        public Canvas ResultsCanvas {get;private set;}
        LevelScoreResult result;
        Font font;
        bool leaving;
        float visibleSince;
        RectTransform recordBadge;

        void Start()
        {
            if(SonicLevelScore.LastResult==null)return;
            // L'ancien ecran "STAGE COMPLETE" de la scene passerait a travers : on le masque.
            foreach(var other in FindObjectsByType<Canvas>(FindObjectsSortMode.None))if(other.isRootCanvas)other.enabled=false;
            Build(SonicLevelScore.LastResult);
            Time.timeScale=1; Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
            if(EventSystem.current==null)
            {
                var events=new GameObject("Resultats - Navigation",typeof(EventSystem),typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            if(!result.isNewLevel){var button=ResultsCanvas.GetComponentInChildren<Button>();if(button!=null)EventSystem.current.SetSelectedGameObject(button.gameObject);}
        }
        void Update()
        {
            if(result==null || Time.unscaledTime-visibleSince<.5f)return;
            var k=Keyboard.current;var g=Gamepad.current;
            // A / R : recommencer le niveau.  B / Entree / Start : niveau suivant.
            if(!result.isNewLevel){
                if((k!=null && (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)) || (g!=null && (g.buttonSouth.wasPressedThisFrame || g.startButton.wasPressedThisFrame)))Continue();
                return;
            }
            if((k!=null && k.rKey.wasPressedThisFrame) || (g!=null && g.buttonSouth.wasPressedThisFrame))Replay();
            else if((k!=null && k.enterKey.wasPressedThisFrame) || (g!=null && (g.buttonEast.wasPressedThisFrame || g.startButton.wasPressedThisFrame)))Continue();
        }
        void OnDestroy(){if(ResultsCanvas==null)return;if(Application.isPlaying)Destroy(ResultsCanvas.gameObject);else DestroyImmediate(ResultsCanvas.gameObject);}
        void LateUpdate()
        {
            if(recordBadge!=null)recordBadge.localScale=Vector3.one*(1+.03f*(1-Mathf.Cos(Time.unscaledTime*2*Mathf.PI/1.4f)));
        }

        // Coordonnees de la maquette (origine en haut a gauche) -> ancre au centre.
        static Vector2 At(float x,float y)=>new Vector2(x-960,540-y);
        static readonly Vector2 Mid=new Vector2(.5f,.5f);

        public void Build(LevelScoreResult value)
        {
            if(ResultsCanvas!=null)return;
            result=value;visibleSince=Time.unscaledTime;font=SonicUi.DesignFont();
            if(!value.isNewLevel){BuildStory(value);return;}
            var canvas=new GameObject("Bilan du niveau",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            ResultsCanvas=canvas.GetComponent<Canvas>();ResultsCanvas.renderMode=RenderMode.ScreenSpaceOverlay;ResultsCanvas.sortingOrder=500;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var root=canvas.transform;

            // Fond : derniere image du niveau en basse resolution (= flou), puis voile bleu nuit.
            var bg=SonicUi.Stretch(root,"Fond").gameObject.AddComponent<RawImage>();bg.raycastTarget=false;
            bg.texture=SonicLevelScore.Snapshot;bg.color=bg.texture!=null?Color.white:SonicUi.Hex("0a1626");
            SonicUi.Stretch(root,"Voile").gameObject.AddComponent<Image>().color=new Color(.024f,.07f,.14f,.84f);
            var content=SonicUi.Rect(root,"Contenu",Mid,Vector2.zero,new Vector2(1920,1080));

            // Bandeau titre
            var title=SonicUi.Skew(content,"Titre",Mid,At(90+270,70),new Vector2(540,72),SonicUi.Cyan,SonicUi.Cyan,Color.clear,0,0);
            SonicUi.Fill(title.transform,"NIVEAU TERMINÉ",font,44,SonicUi.Hex("0a1626"),TextAnchor.MiddleCenter);
            string zone=string.IsNullOrEmpty(value.levelKey)?"":System.IO.Path.GetFileNameWithoutExtension(value.levelKey).ToUpperInvariant();
            SonicUi.Text(content,zone,font,24,SonicUi.Hex("9fe3f5"),TextAnchor.MiddleLeft,Mid,At(658+300,70),new Vector2(600,40));

            // Temps du joueur (+ badge record)
            SonicUi.Text(content,"TON TEMPS",font,22,SonicUi.Hex("9fe3f5"),TextAnchor.MiddleCenter,Mid,At(960,164),new Vector2(600,30));
            var time=SonicUi.Text(content,SonicGameplayHud.FormatTime(value.elapsedSeconds),font,124,Color.white,TextAnchor.MiddleCenter,Mid,At(value.newRecord?795:960,246),new Vector2(760,130));
            SonicUi.Drop(time,"0a2d4a",6);
            if(value.newRecord)
            {
                var badge=SonicUi.Skew(content,"Nouveau record",Mid,At(1310,246),new Vector2(300,56),SonicUi.Hex("ff3b3b"),SonicUi.Hex("ff3b3b"),Color.clear,0,0);
                SonicUi.Fill(badge.transform,"NOUVEAU RECORD",font,26,Color.white,TextAnchor.MiddleCenter);
                recordBadge=badge.rectTransform;recordBadge.localEulerAngles=new Vector3(0,0,6);
            }

            // Arc-en-ciel, diamant, or, argent, bronze. Lowest tier appears first.
            var times=value.medalTimes;int earned=SonicMedal.TierFor(value.elapsedSeconds,times);
            var audio=gameObject.AddComponent<AudioSource>();audio.playOnAwake=false;
            for(int tier=SonicMedal.Rainbow;SonicMedal.ValidTimes(times) && tier>=SonicMedal.LowestTier(times);tier--)
            {
                float x=960+(SonicMedal.Rainbow-tier-(times.Length-1)*.5f)*300,target=SonicMedal.Target(times,tier);bool got=tier<=earned;
                var clip=Resources.Load<AudioClip>("SonicMedals/medal_"+SonicMedal.Ids[tier]);
                if(clip==null && tier==SonicMedal.Bronze)clip=Resources.Load<AudioClip>("SonicMedals/medal_silver");
                SonicMedal.Create(content,tier,Mid,At(x,535),230,got,tier==earned,.25f+(tier-SonicMedal.LowestTier(times))*.18f,audio,clip);
                SonicUi.Text(content,SonicMedal.RichName(tier),font,22,SonicMedal.LabelColors[tier],TextAnchor.MiddleCenter,Mid,At(x,685),new Vector2(280,28));
                SonicUi.Text(content,SonicGameplayHud.FormatTime(target),font,30,Color.white,TextAnchor.MiddleCenter,Mid,At(x,720),new Vector2(280,36));
                string note=got?(tier==earned?"PALIER ATTEINT":"Obtenu"):"Encore −"+SonicGameplayHud.FormatTime(value.elapsedSeconds-target);
                SonicUi.Text(content,note,font,16,SonicUi.Hex("9fb3c8"),TextAnchor.MiddleCenter,Mid,At(x,754),new Vector2(280,22));
            }
            StartCoroutine(Jingles(audio,earned>=0,value.newRecord,SonicMedal.ValidTimes(times)?1.05f+(times.Length-1)*.18f:1.6f));

            // Top 3 du joueur
            var best=SonicRecords.Top(value.levelKey??"");
            float rowWidth=300+best.Length*256,cursor=960-rowWidth/2;
            SonicUi.Text(content,"TES MEILLEURS TEMPS",font,18,SonicUi.Hex("9fe3f5"),TextAnchor.MiddleLeft,Mid,At(cursor+145,934),new Vector2(290,30));
            cursor+=300;
            bool newShown=false;
            for(int i=0;i<best.Length;i++)
            {
                // NEW : le temps de ce run est entre dans le top 3.
                bool first=i==0,isNew=!newShown && Mathf.Abs(best[i]-value.elapsedSeconds)<.001f;
                newShown|=isNew;
                var chip=SonicUi.Skew(content,"Temps "+(i+1),Mid,At(cursor+115,934),new Vector2(230,52),
                    first?new Color(.37f,.88f,1,.2f):new Color(.04f,.12f,.2f,.6f),first?new Color(.37f,.88f,1,.2f):new Color(.04f,.12f,.2f,.6f),first?SonicUi.Cyan:new Color(.37f,.88f,1,.5f));
                SonicUi.Fill(chip.transform,(i+1).ToString(),font,18,first?SonicUi.Hex("ffd75e"):SonicUi.Hex("bfe8f5"),TextAnchor.MiddleLeft,22,0);
                SonicUi.Fill(chip.transform,SonicGameplayHud.FormatTime(best[i]),font,28,first?Color.white:SonicUi.Hex("e6f6ff"),TextAnchor.MiddleCenter,10,isNew?30:0);
                if(isNew)SonicUi.Fill(chip.transform,"NEW",font,13,SonicUi.Hex("ff6b6b"),TextAnchor.MiddleRight,0,18);
                cursor+=256;
            }

            // Actions
            bool canReplay=!string.IsNullOrEmpty(value.levelKey);
            bool next=!string.IsNullOrEmpty(value.nextScene) && Application.CanStreamedLevelBeLoaded(value.nextScene);
            if(canReplay)Action(content,"A","RECOMMENCER",SonicUi.Hex("5bff8a"),At(780,1022),Replay);
            Action(content,"B",next?"NIVEAU SUIVANT":"RETOUR AU MENU",SonicUi.Hex("ff5b5b"),At(canReplay?1120:960,1022),Continue);
        }

        void BuildStory(LevelScoreResult value)
        {
            var canvas=new GameObject("Bilan du niveau",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            ResultsCanvas=canvas.GetComponent<Canvas>();ResultsCanvas.renderMode=RenderMode.ScreenSpaceOverlay;ResultsCanvas.sortingOrder=500;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var bg=SonicUi.Stretch(canvas.transform,"Fond").gameObject.AddComponent<RawImage>();bg.raycastTarget=false;
            bg.texture=SonicLevelScore.Snapshot;bg.color=bg.texture!=null?Color.white:SonicUi.Hex("0a1626");
            SonicUi.Stretch(canvas.transform,"Voile").gameObject.AddComponent<Image>().color=new Color(.024f,.07f,.14f,.86f);
            var content=SonicUi.Rect(canvas.transform,"Resultats Histoire",Mid,Vector2.zero,new Vector2(1280,1000));
            SonicUi.Text(content,"NIVEAU TERMINÉ",font,52,SonicUi.Cyan,TextAnchor.MiddleCenter,Mid,new Vector2(0,414),new Vector2(1200,70));
            string zone=string.IsNullOrEmpty(value.levelKey)?"RÉSULTATS":System.IO.Path.GetFileNameWithoutExtension(value.levelKey).ToUpperInvariant()+" · RÉSULTATS";
            SonicUi.Text(content,zone,font,24,SonicUi.Hex("9fe3f5"),TextAnchor.MiddleCenter,Mid,new Vector2(0,345),new Vector2(1200,42));
            StoryRow(content,"Score final du niveau",value.levelScore,250);
            StoryRow(content,"Rings : "+value.rings+" × 10",value.ringBonus,150);
            StoryRow(content,"Bonus temps : "+SonicGameplayHud.FormatTime(value.elapsedSeconds),value.timeBonus,50);
            StoryRow(content,value.deaths==0?"Bonus sans mort":"Bonus sans mort · "+value.deaths+" mort(s)",value.noDeathBonus,-50);
            StoryRow(content,"Score total de la sauvegarde",value.totalScore,-174,true);
            string lives=value.bonusLives>0?"+"+value.bonusLives+" VIE(S) BONUS · "+SonicXProgress.Lives+" VIE(S) RESTANTE(S)":SonicXProgress.Lives+" VIE(S) RESTANTE(S) · UNE VIE BONUS TOUS LES 50 000 POINTS";
            SonicUi.Text(content,lives,font,22,SonicUi.Hex("ffd75e"),TextAnchor.MiddleCenter,Mid,new Vector2(0,-276),new Vector2(1280,44));
            bool next=!string.IsNullOrEmpty(value.nextScene) && Application.CanStreamedLevelBeLoaded(value.nextScene);
            var hit=SonicUi.Skew(content,next?"Niveau suivant":"Retour au menu",Mid,new Vector2(0,-390),new Vector2(460,72),SonicUi.Cyan,SonicUi.Cyan,Color.clear,0,0);hit.raycastTarget=true;
            SonicUi.Fill(hit.transform,next?"NIVEAU SUIVANT":"RETOUR AU MENU",font,28,SonicUi.Hex("0a1626"),TextAnchor.MiddleCenter);
            var button=hit.gameObject.AddComponent<Button>();button.targetGraphic=hit;button.onClick.AddListener(Continue);
        }

        void StoryRow(Transform parent,string label,long value,float y,bool total=false)
        {
            var color=total?SonicUi.Hex("ffd75e"):SonicUi.Cyan;
            var row=SonicUi.Skew(parent,label,Mid,new Vector2(0,y),new Vector2(1280,76),new Color(.04f,.12f,.2f,.65f),new Color(.04f,.12f,.2f,.85f),color);
            SonicUi.Fill(row.transform,label,font,28,total?color:SonicUi.Hex("cfeefb"),TextAnchor.MiddleLeft,44,300);
            SonicUi.Fill(row.transform,value.ToString("N0",System.Globalization.CultureInfo.GetCultureInfo("fr-FR")),font,total?38:34,total?color:Color.white,TextAnchor.MiddleRight,0,44);
        }

        void Action(Transform parent,string key,string label,Color color,Vector2 pos,UnityEngine.Events.UnityAction onClick)
        {
            var hit=SonicUi.Img(parent,label,Mid,pos,new Vector2(320,44),Color.clear);hit.raycastTarget=true;
            var button=hit.gameObject.AddComponent<Button>();button.targetGraphic=hit;button.onClick.AddListener(onClick);
            var dot=SonicUi.Img(hit.transform,"Touche",new Vector2(0,.5f),new Vector2(0,0),new Vector2(36,36),color,SonicMedal.Circle);
            SonicUi.Fill(dot.transform,key,font,20,SonicUi.Hex("0a1626"),TextAnchor.MiddleCenter);
            SonicUi.Fill(hit.transform,label,font,20,SonicUi.Hex("cfeefb"),TextAnchor.MiddleLeft,48,0);
        }

        IEnumerator Jingles(AudioSource audio,bool earned,bool newRecord,float delay)
        {
            // Wait until every medal's entrance animation has finished.
            yield return new WaitForSecondsRealtime(delay);
            if(earned)audio.PlayOneShot(Resources.Load<AudioClip>("SonicMedals/medal_get"));
            if(!newRecord)yield break;
            yield return new WaitForSecondsRealtime(1.2f);
            audio.PlayOneShot(Resources.Load<AudioClip>("SonicMedals/new_record"));
        }

        void Replay()
        {
            if(leaving || result==null || string.IsNullOrEmpty(result.levelKey) || !Application.CanStreamedLevelBeLoaded(result.levelKey))return;
            leaving=true;Time.timeScale=1;Objects_Interaction.RingAmount=0;Monitors_Interactions.HasShield=false;
            SceneManager.LoadSceneAsync(result.levelKey);
        }
        void Continue()
        {
            if(leaving || result==null)return;
            leaving=true;Time.timeScale=1;
            string next=!string.IsNullOrEmpty(result.nextScene) && Application.CanStreamedLevelBeLoaded(result.nextScene)?result.nextScene:"LogoScreen";
            SceneManager.LoadSceneAsync(next);
        }
    }
}
