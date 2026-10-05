using UnityEngine;
using UnityEngine.UI;
using SonicFX.Score;

namespace SonicFX.HUD
{
    // Maquette "Tableaux de score Sonic" (01 HUD) : meilleurs temps, prochain palier, ecart avec le fantome
    // et compteur de vitesse (variante 2a, barre segmentee). Le menu pause est dans SonicPauseMenu. Cotes de la maquette 1920x1080, mises a l'echelle du HUD.
    [DisallowMultipleComponent] public sealed class SonicRecordsHud : MonoBehaviour
    {
        const float KmhPerUnit=3.6f; // ponytail: suppose 1 unite Unity = 1 m ; a ajuster si le compteur parait faux
        static readonly Vector2 TopRight=new Vector2(1,1),BottomRight=new Vector2(1,0),TopLeft=new Vector2(0,1),Mid=new Vector2(.5f,.5f);
        static readonly Color Navy=new Color(.04f,.12f,.2f,1);

        Font font;PlayerBhysics player;SonicGhost ghost;float[] medalTimes;string level;
        Transform hudRoot;float scale;
        Text speedText;readonly Graphic[] segments=new Graphic[12];
        GameObject ghostPill;Text ghostDelta;Graphic ghostDot;


        static Color A(Color c,float a){c.a=a;return c;}

        public void Build(Canvas hud)
        {
            font=SonicUi.DesignFont();level=gameObject.scene.path;hudRoot=hud.transform;
            scale=hud.GetComponent<CanvasScaler>().referenceResolution.y/1080f;
            player=GetComponent<PlayerBhysics>();
            var progress=GetComponent<LevelProgressControl>();
            if(progress!=null)medalTimes=SonicLevelScore.MedalTimes(progress.MedalTimesSeconds,progress.IdealTimeSeconds);
            BuildRecords(Corner("Meilleurs temps",TopRight,new Vector2(-52,-44)));
            BuildSpeed(Corner("Compteur de vitesse",BottomRight,new Vector2(-52,44)));
        }

        RectTransform Corner(string name,Vector2 corner,Vector2 designPos)
        {
            var rt=SonicUi.Rect(hudRoot,name,corner,designPos*scale,Vector2.zero);rt.localScale=Vector3.one*scale;return rt;
        }

        // Panneau haut-droite : n'existe que si le niveau a deja ete termine.
        void BuildRecords(RectTransform root)
        {
            var best=SonicRecords.Top(level);
            if(best.Length==0)return;
            var head=SonicUi.Skew(root,"Titre",TopRight,Vector2.zero,new Vector2(250,26),A(Navy,.72f),A(Navy,.72f),SonicUi.Cyan);
            SonicUi.Fill(head.transform,"MEILLEURS TEMPS",font,16,SonicUi.Cyan,TextAnchor.MiddleCenter);
            var tag=SonicUi.Skew(root,"Repere",TopRight,new Vector2(-262,0),new Vector2(70,26),A(SonicUi.Cyan,.18f),A(SonicUi.Cyan,.18f),SonicUi.Cyan);
            for(int i=0;i<3;i++)SonicUi.Img(tag.transform,"Trait",new Vector2(0,.5f),new Vector2(16+i*9,0),new Vector2(3,14),SonicUi.Cyan);

            float y=-36;
            for(int i=0;i<best.Length;i++)
            {
                bool first=i==0;float h=first?54:46,w=first?380:i==1?340:300;
                var row=SonicUi.Skew(root,"Temps "+(i+1),TopRight,new Vector2(0,y),new Vector2(w,h),A(Navy,first?.55f:.45f),A(Navy,first?.85f:.75f),first?SonicUi.Cyan:A(SonicUi.Cyan,.7f),2,first?6:2);
                SonicUi.Fill(row.transform,(i+1).ToString(),font,first?22:18,SonicUi.Hex(first?"ffd75e":"bfe8f5"),TextAnchor.MiddleLeft,24,0);
                SonicUi.Drop(SonicUi.Fill(row.transform,SonicGameplayHud.FormatTime(best[i]),font,first?34:28,first?Color.white:SonicUi.Hex("e6f6ff"),TextAnchor.MiddleRight,0,30));
                // Pastille = medaille obtenue avec ce temps.
                int tier=SonicMedal.TierFor(best[i],medalTimes);var dotPos=new Vector2(-(w+14),y-(h-28)/2);
                if(tier>=0)SonicMedal.Dot(root,tier,TopRight,dotPos,28);
                else SonicUi.Img(root,"Pastille",TopRight,dotPos,new Vector2(28,28),A(Navy,.8f),SonicMedal.Circle);
                y-=h+10;
            }

            int bestTier=SonicMedal.TierFor(best[0],medalTimes);
            if(medalTimes!=null && bestTier<SonicMedal.Rainbow)
            {
                int next=bestTier+1;y-=6;
                var box=SonicUi.Skew(root,"Prochain palier",TopRight,new Vector2(0,y),new Vector2(420,34),Color.clear,Color.clear,A(SonicUi.Cyan,.6f));
                SonicUi.Fill(box.transform,"PROCHAIN PALIER",font,14,SonicUi.Hex("9fe3f5"),TextAnchor.MiddleLeft,24,0);
                var goal=SonicUi.Fill(box.transform,SonicMedal.Names[next]+" "+SonicGameplayHud.FormatTime(SonicMedal.Target(medalTimes,next)),font,16,SonicUi.Hex("dff8ff"),TextAnchor.MiddleRight,0,24);
                SonicMedal.Dot(box.transform,next,new Vector2(1,.5f),new Vector2(-(24+goal.preferredWidth+8),0),14);
                y-=44;
            }

            // Indicateur fantome : visible quand le fantome du record joue.
            var pill=SonicUi.Img(root,"Fantome",TopRight,new Vector2(0,y-4),new Vector2(300,30),A(Navy,.6f),SonicMedal.Circle);
            ghostDot=SonicUi.Img(pill.transform,"Voyant",new Vector2(0,.5f),new Vector2(14,0),new Vector2(10,10),SonicUi.Cyan,SonicMedal.Circle);
            SonicUi.Fill(pill.transform,"FANTÔME · RECORD",font,14,SonicUi.Hex("cfeefb"),TextAnchor.MiddleLeft,34,0);
            ghostDelta=SonicUi.Fill(pill.transform,"",font,15,SonicUi.Cyan,TextAnchor.MiddleRight,0,16);
            ghostPill=pill.gameObject;ghostPill.SetActive(false);
        }

        void BuildSpeed(RectTransform root)
        {
            if(player==null)return;
            // 12 crans : cyan, jaune sur les 7e-9e, rouge sur les 3 derniers ; eteints en bleu sombre.
            for(int i=0;i<segments.Length;i++)
                segments[i]=SonicUi.Skew(root,"Cran "+(i+1),BottomRight,new Vector2(-(segments.Length-1-i)*29,0),new Vector2(24,12),Color.white,Color.white,Color.clear,0,0);
            var box=SonicUi.Rect(root,"Vitesse",BottomRight,new Vector2(0,18),new Vector2(343,60));
            speedText=SonicUi.Fill(box,"000",font,56,Color.white,TextAnchor.MiddleRight,0,74);SonicUi.Drop(speedText);
            SonicUi.Fill(box,"KM/H",font,14,SonicUi.Hex("9fe3f5"),TextAnchor.MiddleRight,0,4);
        }

        void Update()
        {
            if(ghost==null)ghost=GetComponent<SonicGhost>();
            float blink=.35f+.65f*(.5f+.5f*Mathf.Cos(Time.unscaledTime*2*Mathf.PI/1.2f));

            if(speedText!=null && player.p_rigidbody!=null)
            {
                // Jauge graduee sur le plafond reel du joueur (MaxSpeed), le chiffre n'est jamais ecrete.
                float speed=player.p_rigidbody.linearVelocity.magnitude*KmhPerUnit,ratio=Mathf.Clamp01(speed/(Mathf.Max(1f,player.MaxSpeed)*KmhPerUnit));
                speedText.text=Mathf.RoundToInt(speed).ToString("000");
                int lit=Mathf.CeilToInt(ratio*segments.Length);
                for(int i=0;i<segments.Length;i++)
                    segments[i].color=i>=lit?SonicUi.Hex("0f3a44",.8f):SonicUi.Hex(i>=9?"ff5a5a":i>=6?"ffe14d":"5ee0ff");
            }

            bool hasGhost=ghost!=null && ghost.HasGhost,on=hasGhost && SonicGhost.Enabled;
            if(ghostPill!=null)
            {
                if(ghostPill.activeSelf!=on)ghostPill.SetActive(on);
                if(on)
                {
                    float d=ghost.Delta;
                    ghostDelta.text=float.IsNaN(d)?"--":(d<0?"−":"+")+Mathf.FloorToInt(Mathf.Abs(d))+"\""+Mathf.FloorToInt(Mathf.Abs(d)*1000%1000).ToString("000");
                    ghostDot.color=A(SonicUi.Cyan,blink);
                }
            }
        }
    }
}
