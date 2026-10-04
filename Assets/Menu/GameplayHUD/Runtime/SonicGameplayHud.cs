using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using SonicFX.Menu;

namespace SonicFX.HUD
{
    [DisallowMultipleComponent] public class SonicGameplayHud : MonoBehaviour
    {
        public SonicHudSettings settings;
        public float Elapsed => SonicFX.Score.SonicLevelScore.Elapsed;
        public Canvas HudCanvas {get;private set;}
        public Text ScoreText {get;private set;}
        public Text TimeText {get;private set;}
        public Text RingsText {get;private set;}
        public Text LivesText {get;private set;}
        public Text SpeedText {get;private set;}
        public float CurrentSpeed {get;private set;}
        PlayerBhysics player;float speedIntensity;
        HurtControl hurt;bool gameOverQueued,gameOverVisible,returning;
        GameObject canvasObject;Font font;
        void Start()
        {
            hurt=GetComponent<HurtControl>();settings=Resources.Load<SonicHudSettings>("SonicHudSettings");
            foreach(var t in transform.root.GetComponentsInChildren<Transform>(true))if(t.name=="Game_Stats")t.gameObject.SetActive(false);
            Build();Refresh();
        }
        void Update()
        {
            Refresh();
            if(SonicXProgress.IsGameOver && !gameOverQueued){gameOverQueued=true;StartCoroutine(GameOver());}
            if(gameOverVisible && ((Keyboard.current!=null && Keyboard.current.enterKey.wasPressedThisFrame) || (Gamepad.current!=null && (Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.startButton.wasPressedThisFrame))))ReturnToMenu();
        }
        void LateUpdate(){RefreshSpeed(Time.unscaledDeltaTime);}
        public static string FormatTime(float seconds)
        {
            long ms=(long)Math.Round(Math.Max(0,(double)seconds)*1000);return (ms/60000).ToString("00")+"'"+(ms/1000%60).ToString("00")+"\""+(ms%1000).ToString("000");
        }
        public void Build()
        {
            if(HudCanvas!=null)return;
            font=settings!=null && settings.numberFont!=null?settings.numberFont:Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvasObject=new GameObject("HUD - Score Temps Rings Vies",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvasObject.transform.SetParent(transform,false);
            HudCanvas=canvasObject.GetComponent<Canvas>();HudCanvas.renderMode=RenderMode.ScreenSpaceOverlay;HudCanvas.sortingOrder=30;
            var scaler=canvasObject.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1270,720);scaler.matchWidthOrHeight=.5f;
            var root=new GameObject("Compteurs",typeof(RectTransform)).GetComponent<RectTransform>();root.SetParent(canvasObject.transform,false);root.anchorMin=root.anchorMax=root.pivot=new Vector2(0,1);root.anchoredPosition=settings!=null?new Vector2(settings.margin.x,-settings.margin.y):new Vector2(18,-24);root.sizeDelta=new Vector2(440,260);root.localScale=Vector3.one*(settings!=null?settings.scale:1);
            var rows=new RectTransform[4];
            for(int i=0;i<4;i++)
            {
                var go=new GameObject(new[]{"Score","Temps","Rings","Vies"}[i],typeof(RectTransform),typeof(SonicHudPlate));var row=(RectTransform)go.transform;row.SetParent(root,false);row.anchorMin=row.anchorMax=row.pivot=new Vector2(0,1);row.anchoredPosition=new Vector2(0,-i*64);row.sizeDelta=new Vector2(i<2?440:340,54);go.GetComponent<SonicHudPlate>().raycastTarget=false;rows[i]=row;
            }
            ScoreText=Number(rows[0],"0",new Vector2(212,-12),new Vector2(205,52),38,TextAnchor.MiddleRight);
            TimeText=Number(rows[1],FormatTime(0),new Vector2(167,-12),new Vector2(254,52),32,TextAnchor.MiddleRight);
            RingsText=Number(rows[2],"000",new Vector2(213,-12),new Vector2(103,52),37,TextAnchor.MiddleRight);
            LivesText=Number(rows[3],"3",new Vector2(234,-12),new Vector2(82,52),38,TextAnchor.MiddleRight);
            if(settings!=null){Icon(rows[2],settings.ringIcon,new Vector2(161,-9),new Vector2(48,48));Icon(rows[3],settings.sonicIcon,new Vector2(150,-4),new Vector2(81,54));}
            var speedRoot=new GameObject("Vitesse - bas gauche",typeof(RectTransform)).GetComponent<RectTransform>();
            speedRoot.SetParent(canvasObject.transform,false);speedRoot.anchorMin=speedRoot.anchorMax=speedRoot.pivot=Vector2.zero;
            speedRoot.anchoredPosition=settings!=null?settings.speedMargin:new Vector2(18,24);
            speedRoot.sizeDelta=new Vector2(260,110);speedRoot.localScale=Vector3.one*(settings!=null?settings.scale:1);
            Number(speedRoot,"VITESSE",Vector2.zero,new Vector2(200,22),16,TextAnchor.MiddleLeft);
            SpeedText=Number(speedRoot,"0",Vector2.zero,new Vector2(200,64),44,TextAnchor.MiddleLeft);
            SpeedText.gameObject.name="Vitesse actuelle";
            SpeedText.rectTransform.anchorMin=SpeedText.rectTransform.anchorMax=SpeedText.rectTransform.pivot=Vector2.zero;
            SpeedText.rectTransform.anchoredPosition=Vector2.zero;
            SpeedText.verticalOverflow=VerticalWrapMode.Overflow;
            var records=GetComponent<SonicRecordsHud>();if(records==null)records=gameObject.AddComponent<SonicRecordsHud>();
            records.Build(HudCanvas);
            var pauseMenu=GetComponent<SonicPauseMenu>();if(pauseMenu==null)pauseMenu=gameObject.AddComponent<SonicPauseMenu>();
            pauseMenu.Build(HudCanvas);
        }
        Text Number(Transform parent,string text,Vector2 pos,Vector2 size,int fontSize,TextAnchor align)
        {
            var go=new GameObject("Valeur",typeof(RectTransform),typeof(Text),typeof(Shadow));go.transform.SetParent(parent,false);var rt=(RectTransform)go.transform;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);rt.anchoredPosition=pos;rt.sizeDelta=size;
            var label=go.GetComponent<Text>();label.font=font;label.text=text;label.fontSize=fontSize;label.alignment=align;label.color=Color.white;label.raycastTarget=false;label.horizontalOverflow=HorizontalWrapMode.Overflow;
            var shadow=go.GetComponent<Shadow>();shadow.effectColor=new Color(0,.03f,.08f,.8f);shadow.effectDistance=new Vector2(2,-2);return label;
        }
        static void Icon(Transform parent,Sprite sprite,Vector2 pos,Vector2 size)
        {
            if(sprite==null)return;
            var go=new GameObject("Icone",typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var rt=(RectTransform)go.transform;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);rt.anchoredPosition=pos;rt.sizeDelta=size;var image=go.GetComponent<Image>();image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;
        }
        void Refresh()
        {
            if(TimeText==null)return;
            ScoreText.text=SonicFX.Score.SonicLevelScore.Current.ToString();TimeText.text=FormatTime(Elapsed);RingsText.text=Mathf.Max(0,Objects_Interaction.RingAmount).ToString("D3");RingsText.color=Objects_Interaction.RingAmount<=0?new Color(1,.08f,.07f):Color.white;LivesText.text=SonicXProgress.Lives.ToString();
        }
        void RefreshSpeed(float deltaTime)
        {
            if(SpeedText==null)return;
            if(player==null)player=GetComponent<PlayerBhysics>();
            // The physics speed also reflects spline travel when the tube suspends the Rigidbody.
            float speed=player!=null?player.SpeedMagnitude:0f;
            CurrentSpeed=float.IsNaN(speed)||float.IsInfinity(speed)?0f:Mathf.Max(0f,speed);
            SpeedText.text=CurrentSpeed.ToString("F0");
            float redAt=settings!=null?Mathf.Max(1f,settings.speedRedAt):60f;
            float target=Mathf.Clamp01(CurrentSpeed/redAt);
            speedIntensity=Mathf.Lerp(speedIntensity,target,1f-Mathf.Exp(-10f*Mathf.Max(0f,deltaTime)));
            SpeedText.color=Color.Lerp(Color.white,Color.red,speedIntensity);
            float maximumScale=settings!=null?Mathf.Clamp(settings.speedMaximumScale,1f,1.5f):1.25f;
            SpeedText.rectTransform.localScale=Vector3.one*Mathf.Lerp(1f,maximumScale,speedIntensity);
        }
        IEnumerator GameOver()
        {
            yield return new WaitForSecondsRealtime(2.5f);
            gameOverVisible=true;Time.timeScale=0;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            foreach(var pause in transform.root.GetComponentsInChildren<PauseCotrol>(true)){if(pause.Pause!=null)pause.Pause.SetActive(false);if(pause.OptionsMenu!=null)pause.OptionsMenu.SetActive(false);pause.enabled=false;}
            var go=new GameObject("GAME OVER",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster),typeof(Image));go.transform.SetParent(canvasObject.transform,false);var rt=(RectTransform)go.transform;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;go.GetComponent<Canvas>().overrideSorting=true;go.GetComponent<Canvas>().sortingOrder=1000;go.GetComponent<Image>().color=new Color(.01f,.035f,.09f,.96f);
            var center=new GameObject("Contenu",typeof(RectTransform)).GetComponent<RectTransform>();center.SetParent(rt,false);center.anchorMin=center.anchorMax=new Vector2(.5f,.5f);center.sizeDelta=new Vector2(700,330);
            Number(center,"GAME OVER",new Vector2(0,0),new Vector2(700,100),65,TextAnchor.MiddleCenter);
            Number(center,"Tu n'as plus de vies.\nCette aventure est terminée.",new Vector2(0,-115),new Vector2(700,90),25,TextAnchor.MiddleCenter);
            var label=Number(center,"RETOUR AU MENU",new Vector2(110,-235),new Vector2(480,70),28,TextAnchor.MiddleCenter);label.raycastTarget=true;var button=label.gameObject.AddComponent<Button>();button.targetGraphic=label;button.onClick.AddListener(ReturnToMenu);
        }
        void ReturnToMenu(){if(returning)return;returning=true;Time.timeScale=1;SceneManager.LoadSceneAsync("LogoScreen");}
        void OnDestroy(){if(gameOverVisible)Time.timeScale=1;}
    }
}
