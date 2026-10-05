using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using SonicFX.Score;

namespace SonicFX.HUD
{
    // Menu pause de la maquette (03 Pause), pense manette : croix ou stick pour naviguer, A valider, B retour,
    // gauche/droite sur "Fantome du record" pour l'activer. Habille le menu d'origine (PauseCotrol) sans changer sa logique.
    [DisallowMultipleComponent] public sealed class SonicPauseMenu : MonoBehaviour
    {
        enum Item{Resume,Restart,Ghost,Options,Quit}
        static readonly Color Navy=new Color(.04f,.12f,.2f,1);
        static readonly Vector2 TopLeft=new Vector2(0,1),TopRight=new Vector2(1,1),BottomLeft=Vector2.zero;
        static Color A(Color c,float a){c.a=a;return c;}

        PauseCotrol pause;SonicGhost ghost;Font font;float[] medalTimes;
        Transform hudRoot;float scale;
        GameObject root;bool wasVisible,wasOptions;float lastStickX,lastStickY;
        readonly List<(Item item,SonicSkewPanel panel)> rows=new List<(Item,SonicSkewPanel)>();
        int selected;
        Text toggleLabel;Image toggleBg;RectTransform knob;Text cardState;Graphic cardDot;

        public void Build(Canvas hud)
        {
            hudRoot=hud.transform;scale=hud.GetComponent<CanvasScaler>().referenceResolution.y/1080f;font=SonicUi.DesignFont();
            foreach(var p in transform.root.GetComponentsInChildren<PauseCotrol>(true)){pause=p;break;}
            if(pause==null)pause=FindAnyObjectByType<PauseCotrol>(FindObjectsInactive.Include);
            if(pause==null || pause.Pause==null || !Application.isPlaying)return;
            var progress=GetComponent<LevelProgressControl>();
            medalTimes=SonicLevelScore.MedalTimes(gameObject.scene,progress!=null?progress.MedalTimesSeconds:null,progress!=null?progress.IdealTimeSeconds:120);
            // L'ancien menu reste actif (PauseCotrol s'en sert pour savoir si le jeu est en pause) mais devient invisible et inerte.
            var hidden=pause.Pause.GetComponent<CanvasGroup>();if(hidden==null)hidden=pause.Pause.AddComponent<CanvasGroup>();
            hidden.alpha=0;hidden.interactable=false;hidden.blocksRaycasts=false;
            if(pause.OptionsMenu!=null && pause.OptionsMenu.transform.IsChildOf(pause.Pause.transform))
            {
                var options=pause.OptionsMenu.GetComponent<CanvasGroup>();if(options==null)options=pause.OptionsMenu.AddComponent<CanvasGroup>();
                options.ignoreParentGroups=true;
            }
        }

        RectTransform Corner(Transform parent,string name,Vector2 corner,Vector2 designPos)
        {
            var rt=SonicUi.Rect(parent,name,corner,designPos*scale,Vector2.zero);rt.localScale=Vector3.one*scale;return rt;
        }

        void BuildMenu()
        {
            if(ghost==null)ghost=GetComponent<SonicGhost>();
            var rt=SonicUi.Stretch(hudRoot,"Menu pause");root=rt.gameObject;
            var canvas=root.AddComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=900;root.AddComponent<GraphicRaycaster>();
            SonicUi.Stretch(rt,"Voile").gameObject.AddComponent<Image>().color=new Color(.024f,.07f,.14f,.35f);
            var fade=SonicUi.Stretch(rt,"Degrade").gameObject.AddComponent<RawImage>();
            fade.texture=SonicMedal.TrailTexture;fade.uvRect=new Rect(1,0,-1,1);fade.color=new Color(.024f,.07f,.14f,.92f);fade.raycastTarget=false;

            var menu=Corner(rt,"Liste",TopLeft,new Vector2(120,-110));
            var title=SonicUi.Skew(menu,"Titre",TopLeft,Vector2.zero,new Vector2(270,76),SonicUi.Cyan,SonicUi.Cyan,Color.clear,0,0);
            SonicUi.Fill(title.transform,"PAUSE",font,54,SonicUi.Hex("0a1626"),TextAnchor.MiddleCenter);

            bool hasGhost=SonicGhost.Allowed && ghost!=null && ghost.HasGhost;
            var items=new List<Item>{Item.Resume,Item.Restart};
            if(hasGhost)items.Add(Item.Ghost);
            if(pause.OptionsMenu!=null)items.Add(Item.Options);
            items.Add(Item.Quit);
            float y=-116;
            foreach(var item in items)
            {
                float h=item==Item.Ghost?124:72;
                var panel=SonicUi.Skew(menu,item.ToString(),TopLeft,new Vector2(0,y),new Vector2(720,h),A(Navy,.7f),A(Navy,.7f),A(SonicUi.Cyan,.5f));
                panel.raycastTarget=true;int index=rows.Count;
                panel.gameObject.AddComponent<Button>().onClick.AddListener(()=>{selected=index;Refresh();Activate(rows[index].item);});
                if(item==Item.Ghost)BuildGhostItem(panel.transform);
                else SonicUi.Fill(panel.transform,Label(item),font,28,SonicUi.Hex("e6f6ff"),TextAnchor.MiddleLeft,40,0);
                rows.Add((item,panel));
                y-=h+18;
            }

            if(hasGhost)BuildCard(Corner(rt,"Fiche fantome",TopRight,new Vector2(-140,-200)));

            var help=Corner(rt,"Aide manette",BottomLeft,new Vector2(120,40));
            Hint(help,"A","VALIDER",SonicUi.Hex("5bff8a"),0);
            Hint(help,"B","RETOUR",SonicUi.Hex("ff5b5b"),210);
        }

        static string Label(Item item)=>item switch{Item.Resume=>"REPRENDRE",Item.Restart=>"RECOMMENCER",Item.Options=>"OPTIONS",Item.Quit=>"QUITTER LE NIVEAU",_=>""};

        void BuildGhostItem(Transform panel)
        {
            SonicUi.Text(panel,"FANTÔME DU RECORD",font,28,Color.white,TextAnchor.MiddleLeft,TopLeft,new Vector2(46,-26),new Vector2(460,36));
            SonicUi.Text(panel,"Rejoue la course de ton meilleur temps · <b>"+SonicGameplayHud.FormatTime(ghost.BestTime)+"</b>",font,17,SonicUi.Hex("9fe3f5"),TextAnchor.MiddleLeft,TopLeft,new Vector2(40,-70),new Vector2(480,28));
            toggleBg=SonicUi.Img(panel,"Interrupteur",new Vector2(1,.5f),new Vector2(-40,0),new Vector2(92,44),SonicUi.Cyan,SonicMedal.Circle);
            knob=SonicUi.Img(toggleBg.transform,"Bouton",new Vector2(0,.5f),new Vector2(4,0),new Vector2(32,32),Color.white,SonicMedal.Circle).rectTransform;
            toggleLabel=SonicUi.Text(panel,"ON",font,18,SonicUi.Cyan,TextAnchor.MiddleRight,new Vector2(1,.5f),new Vector2(-146,0),new Vector2(60,30));
        }

        // Fiche "Fantome actif" a droite : medaille, temps et date du record.
        void BuildCard(RectTransform corner)
        {
            var bg=SonicUi.Skew(corner,"Fond",TopRight,Vector2.zero,new Vector2(520,400),new Color(.024f,.07f,.14f,.78f),new Color(.024f,.07f,.14f,.78f),A(SonicUi.Cyan,.6f));
            bg.skew=0;bg.SetVerticesDirty();var p=bg.transform;
            cardDot=SonicUi.Img(p,"Voyant",TopLeft,new Vector2(38,-40),new Vector2(12,12),SonicUi.Cyan,SonicMedal.Circle);
            cardState=SonicUi.Text(p,"",font,22,SonicUi.Cyan,TextAnchor.MiddleLeft,TopLeft,new Vector2(62,-34),new Vector2(420,24));

            float record=ghost.BestTime;int tier=SonicMedal.TierFor(record,medalTimes);
            SonicMedal.Create(p,Mathf.Max(0,tier),TopLeft,new Vector2(38,-80),110,tier>=0,false);
            SonicUi.Text(p,"RECORD · "+(tier>=0?SonicMedal.Names[tier]:"SANS MÉDAILLE"),font,15,SonicUi.Hex("9fe3f5"),TextAnchor.MiddleLeft,TopLeft,new Vector2(172,-90),new Vector2(320,20));
            SonicUi.Drop(SonicUi.Text(p,SonicGameplayHud.FormatTime(record),font,44,Color.white,TextAnchor.MiddleLeft,TopLeft,new Vector2(172,-114),new Vector2(320,48)),"0a2d4a",4);
            string date=SonicGhost.RecordDate(gameObject.scene.path);
            if(date.Length>0)SonicUi.Text(p,"Établi le "+date,font,15,SonicUi.Hex("9fb3c8"),TextAnchor.MiddleLeft,TopLeft,new Vector2(172,-168),new Vector2(320,20));

            SonicUi.Img(p,"Separateur",TopLeft,new Vector2(38,-212),new Vector2(444,1),A(SonicUi.Cyan,.3f));
            var text=SonicUi.Text(p,"Une silhouette bleue translucide rejoue tes déplacements en temps réel. Dépasse-la pour battre ton record — l'écart s'affiche en haut à droite.",
                font,17,SonicUi.Hex("cfeefb"),TextAnchor.UpperLeft,TopLeft,new Vector2(38,-235),new Vector2(444,80));
            text.horizontalOverflow=HorizontalWrapMode.Wrap;
            float x=38;
            foreach(var chip in new[]{"Opacité 40 %","Sans collision","Écart live"})
            {
                float w=chip.Length*9+30;
                var pill=SonicUi.Img(p,chip,TopLeft,new Vector2(x,-335),new Vector2(w,30),A(SonicUi.Cyan,.15f),SonicMedal.Circle);
                SonicUi.Fill(pill.transform,chip,font,14,SonicUi.Hex("9fe3f5"),TextAnchor.MiddleCenter);
                x+=w+12;
            }
        }

        void Hint(Transform parent,string key,string label,Color color,float x)
        {
            var dot=SonicUi.Img(parent,key,BottomLeft,new Vector2(x,0),new Vector2(36,36),color,SonicMedal.Circle);
            SonicUi.Fill(dot.transform,key,font,20,SonicUi.Hex("0a1626"),TextAnchor.MiddleCenter);
            SonicUi.Text(parent,label,font,20,SonicUi.Hex("cfeefb"),TextAnchor.MiddleLeft,BottomLeft,new Vector2(x+48,3),new Vector2(160,30));
        }

        void Refresh()
        {
            for(int i=0;i<rows.Count;i++)
            {
                if(i==selected)rows[i].panel.Set(A(SonicUi.Cyan,.18f),A(SonicUi.Cyan,.18f),SonicUi.Cyan,3,3,14);
                else rows[i].panel.Set(A(Navy,.7f),A(Navy,.7f),A(SonicUi.Cyan,.5f),2,2,2);
            }
        }

        void Activate(Item item)
        {
            switch(item)
            {
                case Item.Resume:pause.Resume();break;
                case Item.Restart:
                    Time.timeScale=1;Objects_Interaction.RingAmount=0;Monitors_Interactions.HasShield=false;
                    SceneManager.LoadScene(gameObject.scene.path);break;
                case Item.Ghost:SonicGhost.Enabled=!SonicGhost.Enabled;break;
                case Item.Options:
                    pause.OptionsToggle();
                    // Donne le focus manette au premier reglage du menu d'options.
                    var first=pause.OptionsMenu.GetComponentInChildren<Selectable>();
                    if(first!=null && EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(first.gameObject);
                    break;
                case Item.Quit:pause.Quit();break;
            }
        }

        // Entrees : croix, stick gauche (sur front) ou fleches clavier.
        bool Pressed(System.Func<Gamepad,bool> pad,params Key[] keys)
        {
            if(Gamepad.current!=null && pad(Gamepad.current))return true;
            if(Keyboard.current!=null)foreach(var k in keys)if(Keyboard.current[k].wasPressedThisFrame)return true;
            return false;
        }

        void Update()
        {
            if(pause==null || pause.Pause==null)return;
            bool paused=pause.Pause.activeSelf,options=pause.OptionsMenu!=null && pause.OptionsMenu.activeSelf,visible=paused && !options;
            if(visible && root==null)BuildMenu();
            if(root!=null && root.activeSelf!=visible)
            {
                root.SetActive(visible);
                // Le reste du HUD (compteurs, records, vitesse) s'efface pendant le menu.
                foreach(Transform child in hudRoot)if(child.gameObject!=root)child.gameObject.SetActive(!visible);
            }
            if(visible && !wasVisible){selected=0;Refresh();}
            bool closedOptions=wasOptions && !options;
            wasVisible=visible;wasOptions=options;

            var stick=Gamepad.current!=null?Gamepad.current.leftStick.ReadValue():Vector2.zero;
            bool up=Pressed(g=>g.dpad.up.wasPressedThisFrame,Key.UpArrow,Key.W) || (stick.y>.5f && lastStickY<=.5f);
            bool down=Pressed(g=>g.dpad.down.wasPressedThisFrame,Key.DownArrow,Key.S) || (stick.y<-.5f && lastStickY>=-.5f);
            bool side=Pressed(g=>g.dpad.left.wasPressedThisFrame || g.dpad.right.wasPressedThisFrame,Key.LeftArrow,Key.RightArrow,Key.A,Key.D) || (Mathf.Abs(stick.x)>.5f && Mathf.Abs(lastStickX)<=.5f);
            bool confirm=Pressed(g=>g.buttonSouth.wasPressedThisFrame,Key.Enter,Key.Space);
            bool back=Pressed(g=>g.buttonEast.wasPressedThisFrame,Key.Backspace);
            lastStickX=stick.x;lastStickY=stick.y;

            if(options){if(back)pause.OptionsToggle();return;}
            if(!visible || rows.Count==0 || closedOptions)return;
            if(up||down){selected=(selected+(down?1:-1)+rows.Count)%rows.Count;Refresh();}
            if(side && rows[selected].item==Item.Ghost)SonicGhost.Enabled=!SonicGhost.Enabled;
            if(confirm)Activate(rows[selected].item);
            else if(back)Activate(Item.Resume);

            if(toggleBg==null)return;
            bool on=SonicGhost.Enabled;float blink=.35f+.65f*(.5f+.5f*Mathf.Cos(Time.unscaledTime*2*Mathf.PI/1.2f));
            toggleLabel.text=on?"ON":"OFF";toggleLabel.color=on?SonicUi.Cyan:SonicUi.Hex("9fb3c8");
            toggleBg.color=on?SonicUi.Cyan:A(Navy,.8f);knob.anchoredPosition=new Vector2(on?56:4,0);
            cardState.text=on?"FANTÔME ACTIF":"FANTÔME DÉSACTIVÉ";cardState.color=on?SonicUi.Cyan:SonicUi.Hex("9fb3c8");
            cardDot.color=on?A(SonicUi.Cyan,blink):SonicUi.Hex("9fb3c8");
        }
    }
}
