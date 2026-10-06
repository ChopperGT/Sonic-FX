using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace SonicFX.Menu
{
    public class SonicXFrontMenu : MonoBehaviour
    {
        public Sprite logo;
        public AudioSource music,confirmSound;
        public AudioMixer musicMixer,sfxMixer;
        [Tooltip("Scenes propres aux personnages, une fois leurs aventures pretes.")]
        public string tailsScene="",amyScene="",shadowScene="";
        public string speedrunCocoScene="Assets/Level/SpeerunMadeByCOCO.unity";
        public string neoRingScene="Assets/Level/NeoRing.unity";
        public string arcadeScene="Assets/BumperEngineV1/Scenes/StageSelect.unity";
        public enum Page { Title,Main,Story,Characters,Settings,Overwrite,Loading,Rankings,NewLevels,NewLevelCharacters }
        public Page CurrentPage {get;private set;}
        public Canvas MenuCanvas {get;private set;}
        RectTransform panel;Text heading,status;Font font;readonly List<Button> buttons=new List<Button>();
        int changedFrame;bool built;string pendingCharacter;float initialMusicVolume=1;
        GameObject rankingsContent;string rankedScene,pendingNewLevel;
        static readonly Color Navy=new Color(.018f,.045f,.13f),Gold=new Color(1,.79f,.08f),Blue=new Color(.06f,.20f,.43f);
        void Start()
        {
            Time.timeScale=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            if(music!=null)initialMusicVolume=music.volume;
            EnsureInput();Build();ApplyAudio();Show(Page.Title);
        }
        void EnsureInput()
        {
            var system=EventSystem.current;
            if(system==null)system=new GameObject("Menu EventSystem",typeof(EventSystem)).GetComponent<EventSystem>();
            foreach(var module in system.GetComponents<BaseInputModule>())if(!(module is InputSystemUIInputModule))module.enabled=false;
            var input=system.GetComponent<InputSystemUIInputModule>();if(input==null)input=system.gameObject.AddComponent<InputSystemUIInputModule>();input.enabled=true;input.AssignDefaultActions();
        }
        void Update()
        {
            var pad=Gamepad.current;var keyboard=Keyboard.current;
            if(CurrentPage==Page.Title && ((pad!=null && (pad.startButton.wasPressedThisFrame || pad.buttonSouth.wasPressedThisFrame)) || (keyboard!=null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))))Show(Page.Main);
            else if(Time.frameCount>changedFrame && ((pad!=null && pad.buttonEast.wasPressedThisFrame) || (keyboard!=null && keyboard.escapeKey.wasPressedThisFrame)))Back();
        }
        public void Build()
        {
            if(built)return;built=true;font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go=new GameObject("SonicX Menu UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);
            MenuCanvas=go.GetComponent<Canvas>();MenuCanvas.renderMode=RenderMode.ScreenSpaceOverlay;MenuCanvas.sortingOrder=100;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1270,720);scaler.matchWidthOrHeight=.5f;
            var root=(RectTransform)go.transform;
            var bg=Box(root,"Fond",Vector2.zero,new Vector2(1270,720),Navy);bg.anchorMin=Vector2.zero;bg.anchorMax=Vector2.one;bg.sizeDelta=Vector2.zero;
            Box(root,"Accent",new Vector2(-610,0),new Vector2(10,640),Gold);
            var emblem=Box(root,"Logo SonicX",new Vector2(-310,95),new Vector2(560,235),Color.white).GetComponent<Image>();emblem.sprite=logo;emblem.preserveAspect=true;emblem.raycastTarget=false;
            Label(root,"L'AVENTURE COMMENCE ICI",new Vector2(-310,-100),new Vector2(570,45),23,Gold);
            Label(root,"SONIC X",new Vector2(-310,-145),new Vector2(500,40),16,new Color(.48f,.67f,.91f));
            panel=Box(root,"Menu",new Vector2(305,0),new Vector2(530,640),new Color(.035f,.09f,.22f));
            heading=Label(panel,"",new Vector2(0,263),new Vector2(470,65),29,Color.white);
            Box(panel,"Ligne",new Vector2(0,222),new Vector2(450,3),Gold);
            status=Label(panel,"",new Vector2(0,-264),new Vector2(470,78),17,new Color(.68f,.79f,.95f));
            Label(root,"ENTRÉE / X : Valider     ÉCHAP / O : Retour",new Vector2(0,-342),new Vector2(1200,28),16,new Color(.64f,.73f,.86f));
        }
        RectTransform Box(Transform parent,string name,Vector2 pos,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var rt=(RectTransform)go.transform;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=pos;rt.sizeDelta=size;go.GetComponent<Image>().color=color;return rt;
        }
        Text Label(Transform parent,string value,Vector2 pos,Vector2 size,int fontSize,Color color)
        {
            var go=new GameObject("Texte",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var rt=(RectTransform)go.transform;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=pos;rt.sizeDelta=size;
            var text=go.GetComponent<Text>();text.font=font;text.text=value;text.fontSize=fontSize;text.color=color;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;return text;
        }
        void Add(string label,Action action,bool compact=false)
        {
            int i=buttons.Count;var rt=Box(panel,label,new Vector2(0,170-i*(compact?47:65)),new Vector2(450,compact?40:53),Blue);
            var button=rt.gameObject.AddComponent<Button>();var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1,.88f,.46f);colors.selectedColor=Gold;colors.pressedColor=new Color(.8f,.66f,.25f);button.colors=colors;
            Label(rt,label,Vector2.zero,rt.sizeDelta-new Vector2(16,0),compact?20:25,Color.white);
            button.onClick.AddListener(()=>{if(Application.isPlaying && Time.frameCount<=changedFrame)return;if(confirmSound!=null)confirmSound.Play();action();});buttons.Add(button);
        }
        public void Show(Page page)
        {
            Build();CurrentPage=page;changedFrame=Time.frameCount;
            foreach(var b in buttons){b.gameObject.SetActive(false);if(Application.isPlaying)Destroy(b.gameObject);else DestroyImmediate(b.gameObject);}buttons.Clear();status.text="";
            if(rankingsContent!=null){rankingsContent.SetActive(false);if(Application.isPlaying)Destroy(rankingsContent);else DestroyImmediate(rankingsContent);rankingsContent=null;}
            switch(page)
            {
                case Page.Title:heading.text="BIENVENUE";Add("Appuie sur START",()=>Show(Page.Main));status.text="Une nouvelle aventure t'attend.";break;
                case Page.Main:heading.text="MENU PRINCIPAL";Add("Mode Histoire",()=>Show(Page.Story));if(SonicXProgress.IsUnlocked("arcade"))Add("Mode Arcade",()=>LoadArcade());Add("New levels",()=>Show(Page.NewLevels));Add("Paramètre",()=>Show(Page.Settings));Add("Classement",()=>Show(Page.Rankings));Add("Quitter",Quit);break;
                case Page.Story:heading.text="MODE HISTOIRE";if(SonicXProgress.CanContinue)Add("Continuer",Continue);Add("Nouvelle partie",()=>Show(Page.Characters));Add("Retour",()=>Show(Page.Main));break;
                case Page.Characters:heading.text="CHOISIS TON PERSONNAGE";Add("Sonic (jeune)",()=>Choose("sonic"));if(SonicXProgress.IsUnlocked("tails"))Add("Tails",()=>Choose("tails"));if(SonicXProgress.IsUnlocked("amy"))Add("Amy",()=>Choose("amy"));if(SonicXProgress.IsUnlocked("shadow"))Add("Shadow",()=>Choose("shadow"));Add("Retour",()=>Show(Page.Story));break;
                case Page.Overwrite:heading.text="NOUVELLE PARTIE";status.text="La progression actuelle sera remplacée.\nTes déblocages seront conservés.";Add("Commencer",()=>Launch(pendingCharacter,CharacterScene(pendingCharacter)));Add("Annuler",()=>Show(Page.Characters));break;
                case Page.NewLevels:heading.text="NEW LEVELS";Add("BoundArounds",()=>SelectNewLevel(speedrunCocoScene));Add("Act 1-1",()=>SelectNewLevel(SonicXProgress.FirstLevel));Add("Act 1-2",()=>SelectNewLevel(SonicNewLevelSession.SecondAct));Add("Neo Ring Zone",()=>SelectNewLevel(neoRingScene));Add("Retour",()=>Show(Page.Main));break;
                case Page.NewLevelCharacters:DrawNewLevelCharacters();break;
                case Page.Settings:Settings();break;
                case Page.Rankings:DrawRankings(SonicTimeRecords.ReadAll());break;
                case Page.Loading:heading.text="CHARGEMENT…";status.text="Prépare-toi pour l'aventure !";break;
            }
            var selectable=buttons.FindAll(button=>button.interactable);
            for(int i=0;i<selectable.Count;i++){var nav=selectable[i].navigation;nav.mode=Navigation.Mode.Explicit;nav.selectOnUp=selectable[(i+selectable.Count-1)%selectable.Count];nav.selectOnDown=selectable[(i+1)%selectable.Count];if(page==Page.Rankings){nav.selectOnLeft=nav.selectOnUp;nav.selectOnRight=nav.selectOnDown;}selectable[i].navigation=nav;}
            if(page==Page.NewLevelCharacters)NewLevelCharacterNavigation(selectable);
            if(Application.isPlaying && EventSystem.current!=null && selectable.Count>0)EventSystem.current.SetSelectedGameObject(selectable[0].gameObject);
        }
        void Back(){if(CurrentPage==Page.Main)Show(Page.Title);else if(CurrentPage==Page.Story || CurrentPage==Page.Settings || CurrentPage==Page.Rankings || CurrentPage==Page.NewLevels)Show(Page.Main);else if(CurrentPage==Page.Characters)Show(Page.Story);else if(CurrentPage==Page.NewLevelCharacters)Show(Page.NewLevels);else if(CurrentPage==Page.Overwrite)Show(Page.Characters);}
        string CharacterScene(string id)=>id=="sonic"?SonicXProgress.FirstLevel:id=="tails"?tailsScene:id=="amy"?amyScene:shadowScene;
        void Choose(string id)
        {
            if(!SonicXProgress.IsUnlocked(id))return;
            if(!Application.CanStreamedLevelBeLoaded(CharacterScene(id))){status.text="L'aventure de ce personnage n'est pas encore configurée.";return;}
            pendingCharacter=id;if(SonicXProgress.TryRead(out _))Show(Page.Overwrite);else Launch(id,CharacterScene(id));
        }
        void Continue(){if(SonicXProgress.TryRead(out var save))Launch(save.character,save.scene,save.lives,save.totalScore,(SonicAbility)save.unlockedAbilities,save.redRingLevels);}
        void Launch(string character,string scene,int lives=3,long totalScore=0,SonicAbility abilities=SonicAbility.None,string[] redRingLevels=null)
        {
            if(!Application.CanStreamedLevelBeLoaded(scene)){status.text="Ce niveau n'est pas disponible.";return;}
            Show(Page.Loading);StartCoroutine(LoadStory(character,scene,lives,totalScore,abilities,redRingLevels));
        }
        IEnumerator LoadStory(string character,string scene,int lives,long totalScore,SonicAbility abilities,string[] redRingLevels)
        {
            yield return null;AsyncOperation operation=null;
            try{operation=SonicXProgress.Begin(character,scene,lives,totalScore,abilities,redRingLevels);}catch(Exception e){Debug.LogException(e);}
            if(operation==null){Show(Page.Story);status.text="Impossible de charger le niveau. Réessaie.";}
        }
        void SelectNewLevel(string scene)
        {
            if(!Application.CanStreamedLevelBeLoaded(scene)){status.text="Ce niveau n'est pas disponible.";return;}
            pendingNewLevel=scene;Show(Page.NewLevelCharacters);
        }
        void DrawNewLevelCharacters()
        {
            heading.text="CHOISIS TON PERSONNAGE";
            var catalog=SonicNewLevelCatalog.Load();int index=0;
            if(catalog!=null)foreach(var character in catalog.characters){
                if(character==null || character.prefab==null)continue;
                var choice=character;Add(choice.displayName,()=>LaunchNewLevel(choice),true);
                var rect=(RectTransform)buttons[buttons.Count-1].transform;
                rect.anchoredPosition=new Vector2(index%2==0?-115:115,170-index/2*58);rect.sizeDelta=new Vector2(216,48);
                var text=buttons[buttons.Count-1].GetComponentInChildren<Text>();text.rectTransform.sizeDelta=rect.sizeDelta-new Vector2(12,0);text.resizeTextForBestFit=true;text.resizeTextMinSize=14;text.resizeTextMaxSize=20;
                index++;
            }
            Add("Retour",()=>Show(Page.NewLevels),true);((RectTransform)buttons[buttons.Count-1].transform).anchoredPosition=new Vector2(0,-205);
            status.text=index>0?SonicTimeRecords.LevelName(pendingNewLevel)+"\nPartie libre — progression Histoire conservée.":"Personnages indisponibles : attends la fin de l'importation.";
        }
        static void NewLevelCharacterNavigation(List<Button> items)
        {
            if(items.Count<2)return;int count=items.Count-1;var back=items[count];
            for(int i=0;i<count;i++){
                var nav=items[i].navigation;nav.selectOnUp=i>=2?items[i-2]:back;nav.selectOnDown=i+2<count?items[i+2]:back;
                nav.selectOnLeft=i%2==1?items[i-1]:items[i];nav.selectOnRight=i%2==0 && i+1<count?items[i+1]:items[i];items[i].navigation=nav;
            }
            var b=back.navigation;b.selectOnUp=items[count-1];b.selectOnDown=items[0];back.navigation=b;
        }
        void LaunchNewLevel(SonicNewLevelCharacter character)
        {
            if(!SonicNewLevelSession.CanLaunch(character,pendingNewLevel)){status.text="Ce personnage ou ce niveau n'est pas disponible.";return;}
            Show(Page.Loading);StartCoroutine(LoadNewLevel(character,pendingNewLevel));
        }
        IEnumerator LoadNewLevel(SonicNewLevelCharacter character,string scene)
        {
            yield return null;AsyncOperation operation=null;
            try{operation=SonicNewLevelSession.Begin(character,scene);}catch(Exception e){Debug.LogException(e);}
            if(operation==null){Show(Page.NewLevelCharacters);status.text="Impossible de charger le niveau. Réessaie.";}
        }
        void LoadArcade(){if(!SonicXProgress.IsUnlocked("arcade"))return;if(Application.CanStreamedLevelBeLoaded(arcadeScene)){Time.timeScale=1;SceneManager.LoadSceneAsync(arcadeScene);}else status.text="Le mode Arcade n'est pas encore configuré.";}
        void Quit(){PlayerPrefs.Save();Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#endif
        }
        void DrawRankings(List<SonicLevelTimeRecords> levels)
        {
            heading.text="CLASSEMENT";
            rankingsContent=new GameObject("Records du niveau",typeof(RectTransform));
            rankingsContent.transform.SetParent(panel,false);
            var content=(RectTransform)rankingsContent.transform;
            content.anchorMin=content.anchorMax=content.pivot=new Vector2(.5f,.5f);
            content.sizeDelta=panel.sizeDelta;
            int index=levels.FindIndex(level=>level.scene==rankedScene);
            if(index<0)index=0;
            var level=levels.Count>0?levels[index]:null;
            if(level!=null)rankedScene=level.scene;
            AddRankingButton("Niveau précédent",()=>{rankedScene=levels[(index+levels.Count-1)%levels.Count].scene;Show(Page.Rankings);},new Vector2(-115,174),new Vector2(220,40),levels.Count>1);
            AddRankingButton("Niveau suivant",()=>{rankedScene=levels[(index+1)%levels.Count].scene;Show(Page.Rankings);},new Vector2(115,174),new Vector2(220,40),levels.Count>1);
            var levelLabel=Label(content,level!=null?SonicTimeRecords.LevelName(level.scene):"Aucun niveau terminé",new Vector2(0,117),new Vector2(455,52),23,Gold);
            levelLabel.resizeTextForBestFit=true;levelLabel.resizeTextMinSize=15;levelLabel.resizeTextMaxSize=23;
            for(int i=0;i<SonicTimeRecords.RecordsPerLevel;i++)
            {
                var row=Box(content,"Record "+(i+1),new Vector2(0,54-i*54),new Vector2(450,45),Blue);
                Label(row,(i+1).ToString(),new Vector2(-191,0),new Vector2(40,45),24,i==0?Gold:Color.white);
                var record=level!=null&&i<level.records.Count?level.records[i]:null;
                Label(row,record!=null?SonicTimeRecords.FormatTime(record.milliseconds):"--'--\"---",new Vector2(-38,0),new Vector2(258,45),27,Color.white);
                Label(row,record!=null?SonicTimeRecords.CharacterName(record.character):"",new Vector2(155,0),new Vector2(135,45),16,new Color(.68f,.79f,.95f));
            }
            AddRankingButton("Retour",()=>Show(Page.Main),new Vector2(0,-222),new Vector2(450,43),true);
            status.text=level==null?"Termine un niveau pour enregistrer ton premier temps.":"Les 5 meilleurs temps de ce niveau.\nRecords enregistrés sur cet ordinateur.";
        }
        void AddRankingButton(string label,Action action,Vector2 position,Vector2 size,bool interactable)
        {
            Add(label,action,true);var button=buttons[buttons.Count-1];var rect=(RectTransform)button.transform;
            rect.anchoredPosition=position;rect.sizeDelta=size;button.interactable=interactable;
            var text=button.GetComponentInChildren<Text>();text.rectTransform.sizeDelta=size-new Vector2(12,0);text.fontSize=18;
        }
        void Settings()
        {
            heading.text="PARAMÈTRE";
            Add("Musique : "+VolumePercent("MUSIC_VOL")+" %",()=>CycleVolume("MUSIC_VOL"),true);
            Add("Effets sonores : "+VolumePercent("SFX_VOL")+" %",()=>CycleVolume("SFX_VOL"),true);
            Add("Sensibilité X : "+PlayerPrefs.GetFloat("X_SENS",1).ToString("0.0"),()=>CycleSensitivity("X_SENS"),true);
            Add("Sensibilité Y : "+PlayerPrefs.GetFloat("Y_SENS",1).ToString("0.0"),()=>CycleSensitivity("Y_SENS"),true);
            Add("Inverser X : "+(PlayerPrefs.GetInt("X_INV",0)==1?"Oui":"Non"),()=>Toggle("X_INV"),true);
            Add("Inverser Y : "+(PlayerPrefs.GetInt("Y_INV",0)==1?"Oui":"Non"),()=>Toggle("Y_INV"),true);
            Add("Caméra du tube : "+SonicTubeCameraSettings.Mode,()=>{SonicTubeCameraSettings.Mode=SonicTubeCameraSettings.Mode==SonicTubeCameraMode.FPS?SonicTubeCameraMode.Proche:SonicTubeCameraMode.FPS;RefreshSettings(6);},true);
            Add("Retour",()=>Show(Page.Main),true);status.text="Valider une ligne pour changer sa valeur.\nRéglages enregistrés automatiquement.";
        }
        static int VolumePercent(string key){float db=PlayerPrefs.GetFloat(key,1);return db<=-49?0:Mathf.RoundToInt(Mathf.Pow(10,(db-1)/20)*100);}
        void CycleVolume(string key){int p=VolumePercent(key);p=p<=0?100:Mathf.Max(0,Mathf.RoundToInt(p/10f)*10-10);PlayerPrefs.SetFloat(key,p==0?-50:1+20*Mathf.Log10(p/100f));PlayerPrefs.Save();ApplyAudio();RefreshSettings(key=="MUSIC_VOL"?0:1);}
        void CycleSensitivity(string key){float value=PlayerPrefs.GetFloat(key,1)+.5f;PlayerPrefs.SetFloat(key,value>5?.5f:value);PlayerPrefs.Save();RefreshSettings(key=="X_SENS"?2:3);}
        void Toggle(string key){PlayerPrefs.SetInt(key,1-PlayerPrefs.GetInt(key,0));PlayerPrefs.Save();RefreshSettings(key=="X_INV"?4:5);}
        void RefreshSettings(int index){Show(Page.Settings);if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(buttons[index].gameObject);}
        void ApplyAudio()
        {
            float m=PlayerPrefs.GetFloat("MUSIC_VOL",1),s=PlayerPrefs.GetFloat("SFX_VOL",1);
            if(musicMixer!=null)musicMixer.SetFloat("Volume",m);if(sfxMixer!=null)sfxMixer.SetFloat("Volume",s);
            if(music!=null && music.outputAudioMixerGroup==null)music.volume=initialMusicVolume*(m<=-49?0:Mathf.Pow(10,(m-1)/20));
        }
    }
}
