using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SonicFX.Menu;
using SonicFX.Score;

namespace SonicFX.RedRings
{
    [DisallowMultipleComponent]
    public sealed class RedRingChallenge : MonoBehaviour
    {
        [Header("Parcours, dans l'ordre de ramassage")]
        [Tooltip("Le premier ring lance le chrono. Deplacer les enfants dans la Scene pour dessiner le parcours.")]
        public RedStarRing[] rings=Array.Empty<RedStarRing>();
        [Tooltip("Decalage local du nouveau ring par rapport au precedent, lors de sa creation dans l'editeur.")]
        public Vector3 newRingOffset=new Vector3(8,0,0);
        [Min(.1f), InspectorName("Temps imparti (secondes)")] public float timeLimit=60;
        [Header("Reussite")]
        [InspectorName("Son de reussite")] public AudioClip successSound;
        public AudioMixerGroup successMixer;
        [Range(0,1)] public float successVolume=1;
        [Header("Chemin etoile")]
        [Min(.25f), InspectorName("Ecart entre les etoiles")] public float starSpacing=2;
        [Min(.05f), InspectorName("Taille des etoiles")] public float starSize=.35f;
        public Color starColor=new Color(1,.8f,.1f,1);
        [Tooltip("Un parcours sans liste explicite rassemble les rings de la scene dans l'ordre de la Hierarchy.")]
        public bool automaticSceneRoute;
        public bool Active {get;private set;}
        public bool Succeeded {get;private set;}
        public int CollectedCount {get;private set;}
        public float Remaining {get;private set;}
        public bool Available {get;private set;}
        bool initialized;int deathsAtStart;float messageUntil;
        PlayerBhysics participant;RedRingStarGuide guide;Text label;GameObject hud;AudioSource sound;

        public static RedRingChallenge FindOrCreate(Scene scene)
        {
            var existing=UnityEngine.Object.FindObjectsByType<RedRingChallenge>()
                .FirstOrDefault(c=>c.gameObject.scene==scene);
            if(existing!=null)return existing;
            var root=new GameObject("Defi Rings Rouges (automatique)");SceneManager.MoveGameObjectToScene(root,scene);
            var challenge=root.AddComponent<RedRingChallenge>();challenge.automaticSceneRoute=true;return challenge;
        }
        static string HierarchyKey(Transform t)
        {
            string key="";while(t!=null){key=t.GetSiblingIndex().ToString("D6")+"/"+key;t=t.parent;}return key;
        }
        void Start(){Initialize();}
        public static RedStarRing[] OrderedSceneRings(Scene scene)
        {
            return UnityEngine.Object.FindObjectsByType<RedStarRing>(FindObjectsInactive.Include)
                .Where(r=>r.gameObject.scene==scene&&r.GetComponentInParent<RedRingChallenge>()==null)
                .OrderBy(r=>r.order>0?r.order:int.MaxValue).ThenBy(r=>HierarchyKey(r.transform),StringComparer.Ordinal).ToArray();
        }
        public void Initialize()
        {
            if(initialized)return;initialized=true;
            if(automaticSceneRoute)
            {
                rings=OrderedSceneRings(gameObject.scene).Where(r=>r.gameObject.activeInHierarchy).ToArray();
                if(rings.Length>0){timeLimit=rings[0].autoTimeLimit;successSound=rings[0].autoSuccessSound;}
            }
            rings=(rings??Array.Empty<RedStarRing>()).Where(r=>r!=null).Distinct().ToArray();
            Available=SonicXProgress.IsStorySession&&!SonicXProgress.IsGameOver
                &&!SonicXProgress.HasRedRingReward(gameObject.scene.path)&&rings.Length>0;
            foreach(var ring in rings){ring.Challenge=this;ring.Prepare();}
            RefreshRingVisibility();
        }
        public bool Contains(RedStarRing ring)=>rings!=null&&Array.IndexOf(rings,ring)>=0;
        public bool TryCollect(RedStarRing ring,PlayerBhysics player)
        {
            Initialize();
            if(!Available||Succeeded||player==null||player.gameObject.scene!=gameObject.scene||!SonicLevelScore.IsRunning
                ||!SonicXProgress.IsStorySession||SonicXProgress.IsGameOver||CollectedCount>=rings.Length||rings[CollectedCount]!=ring)return false;
            var hurt=player.GetComponent<HurtControl>();if(hurt!=null&&hurt.isDead)return false;
            if(Active&&(participant!=player||Remaining<=0))return false;
            if(!Active){Active=true;Remaining=Mathf.Max(.1f,timeLimit);participant=player;deathsAtStart=SonicLevelScore.Deaths;}
            ring.SetCollected(true);CollectedCount++;
            RefreshRingVisibility();
            if(CollectedCount==rings.Length)
            {
                Succeeded=SonicXProgress.MarkRedRingChallengeComplete(gameObject.scene.path);Active=false;ClearGuide();
                if(Succeeded){ShowMessage("RINGS ROUGES : REUSSI !\nTerminez le niveau pour gagner la recompense.");PlaySuccess();}
                else ResetAttempt("Defi indisponible.");
            }
            else
            {
                if(Application.isPlaying)
                {
                    if(guide==null)guide=gameObject.AddComponent<RedRingStarGuide>();
                    guide.Show(ring.WorldCenter,rings[CollectedCount],starSpacing,starSize,starColor);
                }
                ShowTimer();
            }
            return true;
        }
        void Update(){Advance(Time.deltaTime);}
        // Scaled time freezes naturally while the pause menu is open.
        public void Advance(float deltaTime)
        {
            if(!Active)return;
            var hurt=participant!=null?participant.GetComponent<HurtControl>():null;
            if(participant==null||(hurt!=null&&hurt.isDead)||SonicLevelScore.Deaths!=deathsAtStart)
            {ResetAttempt("Defi interrompu. Reprenez au premier ring.");return;}
            if(!SonicLevelScore.IsRunning||!SonicXProgress.IsStorySession||SonicXProgress.IsGameOver)
            {ResetAttempt("Defi non termine.");return;}
            if(deltaTime>0&&!float.IsInfinity(deltaTime)&&!float.IsNaN(deltaTime))Remaining=Mathf.Max(0,Remaining-deltaTime);
            if(Remaining<=0)ResetAttempt("Temps ecoule ! Reprenez au premier ring.");else ShowTimer();
        }
        public void ResetAttempt(string message=null)
        {
            Active=false;Succeeded=false;CollectedCount=0;Remaining=0;participant=null;ClearGuide();
            foreach(var ring in rings)if(ring!=null)ring.SetCollected(false);
            RefreshRingVisibility();
            if(message!=null)ShowMessage(message);
        }
        // Keep future rings alive for route data, but hide both their visual and pickup.
        // Only the current step is visible; a failed attempt reveals the first again.
        void RefreshRingVisibility()
        {
            for(int i=0;i<rings.Length;i++)if(rings[i]!=null)
                rings[i].SetVisible(Available&&!Succeeded&&i==CollectedCount);
        }
        void ClearGuide(){if(guide!=null)guide.Clear();}
        void MakeHud()
        {
            if(hud!=null||!Application.isPlaying)return;
            hud=new GameObject("HUD - Defi Rings Rouges",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));hud.transform.SetParent(transform,false);
            var canvas=hud.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=110;
            var scaler=hud.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var text=new GameObject("Temps et progression",typeof(RectTransform),typeof(Text),typeof(Outline));text.transform.SetParent(hud.transform,false);
            var rect=text.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-45);rect.sizeDelta=new Vector2(850,100);
            label=text.GetComponent<Text>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=30;label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;
            text.GetComponent<Outline>().effectColor=Color.black;text.GetComponent<Outline>().effectDistance=new Vector2(2,-2);
        }
        void ShowTimer()
        {
            MakeHud();if(label==null)return;label.enabled=true;label.color=Remaining<=10?new Color(1,.25f,.2f):Color.white;
            label.text="RINGS ROUGES  "+CollectedCount+" / "+rings.Length+"    "+Mathf.CeilToInt(Remaining)+" s";
        }
        void ShowMessage(string message)
        {
            MakeHud();messageUntil=Time.time+5;if(label!=null){label.enabled=true;label.color=Succeeded?new Color(1,.85f,.15f):Color.white;label.text=message;}
        }
        void LateUpdate(){if(!Active&&label!=null&&Time.time>=messageUntil)label.enabled=false;}
        void PlaySuccess()
        {
            if(successSound==null||!Application.isPlaying)return;
            if(sound==null)sound=gameObject.AddComponent<AudioSource>();
            sound.playOnAwake=false;sound.loop=false;sound.spatialBlend=0;sound.priority=32;sound.volume=successVolume;
            sound.outputAudioMixerGroup=successMixer;sound.PlayOneShot(successSound);
        }
        void OnDisable(){ClearGuide();if(hud!=null)hud.SetActive(false);}
        void OnEnable(){if(hud!=null)hud.SetActive(true);}
    }
}
