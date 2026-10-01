using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using SonicFX.Menu;

namespace SonicFX.Score
{
    public sealed class SonicLevelResults : MonoBehaviour
    {
        public Canvas ResultsCanvas {get;private set;}
        LevelScoreResult result;
        Font font;
        bool leaving;
        float visibleSince;
        void Start()
        {
            if(SonicLevelScore.LastResult==null)return;
            Build(SonicLevelScore.LastResult);
            Time.timeScale=1; Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
            if(EventSystem.current==null)
            {
                var events=new GameObject("Resultats - Navigation",typeof(EventSystem),typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }
        void Update()
        {
            if(result==null || Time.unscaledTime-visibleSince<.5f)return;
            if((Keyboard.current!=null && Keyboard.current.enterKey.wasPressedThisFrame) ||
               (Gamepad.current!=null && (Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.startButton.wasPressedThisFrame)))Continue();
        }
        public void Build(LevelScoreResult value)
        {
            if(ResultsCanvas!=null)return;
            result=value;visibleSince=Time.unscaledTime;
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas=new GameObject("Bilan du niveau",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform,false); ResultsCanvas=canvas.GetComponent<Canvas>();ResultsCanvas.renderMode=RenderMode.ScreenSpaceOverlay;ResultsCanvas.sortingOrder=500;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            var background=Panel(canvas.transform,"Fond",Vector2.zero,new Vector2(1280,720),new Color(.018f,.055f,.115f));
            background.anchorMin=Vector2.zero;background.anchorMax=Vector2.one;background.offsetMin=background.offsetMax=Vector2.zero;
            var content=new GameObject("Contenu",typeof(RectTransform)).GetComponent<RectTransform>();content.SetParent(canvas.transform,false);content.sizeDelta=new Vector2(1040,640);
            Panel(content,"Accent",new Vector2(-482,260),new Vector2(8,76),new Color(.12f,.85f,.95f));
            Label(content,"NIVEAU TERMINÉ",new Vector2(0,270),new Vector2(920,65),46,new Color(.98f,.82f,.19f),TextAnchor.MiddleLeft);
            Label(content,"RÉSULTATS",new Vector2(0,218),new Vector2(920,32),18,new Color(.48f,.76f,.88f),TextAnchor.MiddleLeft);
            Row(content,"Score final du niveau",value.levelScore.ToString("N0"),140,false);
            Row(content,"Rings : "+value.rings+" × 10",value.ringBonus.ToString("N0"),68,false);
            Row(content,"Bonus temps : "+SonicFX.HUD.SonicGameplayHud.FormatTime(value.elapsedSeconds),value.timeBonus.ToString("N0"),-4,false);
            Row(content,value.deaths==0?"Bonus sans mort":"Bonus sans mort ("+value.deaths+" mort"+(value.deaths>1?"s":"")+")",value.noDeathBonus.ToString("N0"),-76,false);
            Row(content,"Score total de la sauvegarde",value.totalScore.ToString("N0"),-162,true);
            string lives=value.bonusLives>0?"+"+value.bonusLives+" VIE"+(value.bonusLives>1?"S":"")+" !  •  "+SonicXProgress.Lives+" vies restantes":"Une vie supplémentaire tous les 50 000 points";
            Label(content,lives,new Vector2(0,-228),new Vector2(940,42),23,value.bonusLives>0?new Color(1,.84f,.22f):new Color(.65f,.78f,.87f),TextAnchor.MiddleCenter);
            bool next=!string.IsNullOrEmpty(value.nextScene) && Application.CanStreamedLevelBeLoaded(value.nextScene);
            var buttonRect=Panel(content,"Continuer",new Vector2(0,-286),new Vector2(380,50),new Color(.025f,.38f,.6f));
            var button=buttonRect.gameObject.AddComponent<Button>();button.targetGraphic=buttonRect.GetComponent<Image>();button.onClick.AddListener(Continue);
            Label(buttonRect,next?"NIVEAU SUIVANT":"RETOUR AU MENU",Vector2.zero,new Vector2(370,52),23,Color.white,TextAnchor.MiddleCenter);
        }
        void Row(Transform parent,string title,string points,float y,bool total)
        {
            var panel=Panel(parent,title,new Vector2(0,y),new Vector2(980,64),total?new Color(.035f,.26f,.36f):new Color(.055f,.115f,.19f));
            Panel(panel,"Bord",new Vector2(-487,0),new Vector2(6,64),total?new Color(.98f,.8f,.14f):new Color(.1f,.56f,.72f));
            Label(panel,title,new Vector2(-160,0),new Vector2(590,64),25,Color.white,TextAnchor.MiddleLeft);
            Label(panel,points,new Vector2(310,0),new Vector2(300,64),35,total?new Color(1,.85f,.2f):Color.white,TextAnchor.MiddleRight);
        }
        static RectTransform Panel(Transform parent,string name,Vector2 position,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;
            go.GetComponent<Image>().color=color;return rect;
        }
        void Label(Transform parent,string text,Vector2 pos,Vector2 size,int fontSize,Color color,TextAnchor alignment)
        {
            var go=new GameObject(text,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var rect=(RectTransform)go.transform;
            rect.anchoredPosition=pos;rect.sizeDelta=size;var label=go.GetComponent<Text>();label.text=text;label.font=font;label.fontSize=fontSize;label.color=color;label.alignment=alignment;label.raycastTarget=false;
            label.resizeTextForBestFit=true;label.resizeTextMinSize=16;label.resizeTextMaxSize=fontSize;
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
