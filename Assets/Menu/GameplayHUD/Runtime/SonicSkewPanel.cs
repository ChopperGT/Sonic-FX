using UnityEngine;
using UnityEngine.UI;
namespace SonicFX.HUD
{
    // Parallelogramme de la maquette (skewX -18deg) : degrade horizontal + bordure, bord droit plus epais en option.
    [RequireComponent(typeof(CanvasRenderer))]
    public class SonicSkewPanel : MaskableGraphic
    {
        public Color left=new Color(.04f,.12f,.2f,.55f),right=new Color(.04f,.12f,.2f,.85f),border=new Color(.37f,.88f,1f);
        public float borderWidth=2,rightBorder=2,leftBorder=-1,skew=18; // leftBorder < 0 : comme borderWidth

        public void Set(Color l,Color r,Color b,float width=2,float rightWidth=2,float leftWidth=-1){left=l;right=r;border=b;borderWidth=width;rightBorder=rightWidth;leftBorder=leftWidth;SetVerticesDirty();}

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;float t=Mathf.Tan(skew*Mathf.Deg2Rad),cy=r.center.y;
            // Le haut penche vers la droite, comme le skew CSS.
            Vector2 P(float x,float y)=>new Vector2(x+(y-cy)*t,y);
            Vector2 obl=P(r.xMin,r.yMin),otl=P(r.xMin,r.yMax),otr=P(r.xMax,r.yMax),obr=P(r.xMax,r.yMin);
            float w=borderWidth,lw=leftBorder<0?w:leftBorder;
            Vector2 ibl=P(r.xMin+lw,r.yMin+w),itl=P(r.xMin+lw,r.yMax-w),itr=P(r.xMax-rightBorder,r.yMax-w),ibr=P(r.xMax-rightBorder,r.yMin+w);
            Color l=left*color,rr=right*color,b=border*color;
            Quad(vh,ibl,itl,itr,ibr,l,l,rr,rr);
            if(b.a<=0)return;
            Quad(vh,obl,otl,itl,ibl,b,b,b,b);Quad(vh,otl,otr,itr,itl,b,b,b,b);
            Quad(vh,otr,obr,ibr,itr,b,b,b,b);Quad(vh,obr,obl,ibl,ibr,b,b,b,b);
        }
        static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color ca,Color cb,Color cc,Color cd)
        {
            int s=vh.currentVertCount;
            vh.AddVert(a,ca,Vector2.zero);vh.AddVert(b,cb,Vector2.zero);vh.AddVert(c,cc,Vector2.zero);vh.AddVert(d,cd,Vector2.zero);
            vh.AddTriangle(s,s+1,s+2);vh.AddTriangle(s,s+2,s+3);
        }
    }

    // Petits constructeurs uGUI partages par le HUD, l'ecran de fin et la fiche fantome (cotes de la maquette 1920x1080).
    public static class SonicUi
    {
        public static Color Hex(string hex,float alpha=1){ColorUtility.TryParseHtmlString("#"+hex,out var c);c.a=alpha;return c;}
        public static readonly Color Cyan=Hex("5ee0ff");
        public static Font DesignFont()
        {
            var settings=Resources.Load<SonicHudSettings>("SonicHudSettings");
            return settings!=null && settings.numberFont!=null?settings.numberFont:Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        public static RectTransform Rect(Transform parent,string name,Vector2 anchor,Vector2 pos,Vector2 size)
        {
            var rt=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rt.SetParent(parent,false);
            rt.anchorMin=rt.anchorMax=rt.pivot=anchor;rt.anchoredPosition=pos;rt.sizeDelta=size;return rt;
        }
        public static RectTransform Stretch(Transform parent,string name)
        {
            var rt=Rect(parent,name,new Vector2(.5f,.5f),Vector2.zero,Vector2.zero);rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;return rt;
        }
        public static SonicSkewPanel Skew(Transform parent,string name,Vector2 anchor,Vector2 pos,Vector2 size,Color l,Color r,Color border,float width=2,float rightWidth=2)
        {
            var p=Rect(parent,name,anchor,pos,size).gameObject.AddComponent<SonicSkewPanel>();p.raycastTarget=false;p.Set(l,r,border,width,rightWidth);return p;
        }
        public static Image Img(Transform parent,string name,Vector2 anchor,Vector2 pos,Vector2 size,Color color,Sprite sprite=null)
        {
            var img=Rect(parent,name,anchor,pos,size).gameObject.AddComponent<Image>();img.color=color;img.sprite=sprite;img.raycastTarget=false;
            // 9-slice : coins = moitie du petit cote, pour des pilules vraiment arrondies.
            if(sprite!=null && sprite.border!=Vector4.zero){img.type=UnityEngine.UI.Image.Type.Sliced;img.pixelsPerUnitMultiplier=sprite.border.x*2/Mathf.Max(1,Mathf.Min(size.x,size.y));}
            return img;
        }
        public static Text Text(Transform parent,string text,Font font,int size,Color color,TextAnchor align,Vector2 anchor,Vector2 pos,Vector2 box)
        {
            var label=Rect(parent,"Texte",anchor,pos,box).gameObject.AddComponent<Text>();
            label.text=text;label.font=font;label.fontSize=size;label.color=color;label.alignment=align;label.raycastTarget=false;label.supportRichText=true;
            label.horizontalOverflow=HorizontalWrapMode.Overflow;label.verticalOverflow=VerticalWrapMode.Overflow;return label;
        }
        // Texte qui remplit son parent, avec marges gauche/droite.
        public static Text Fill(Transform parent,string text,Font font,int size,Color color,TextAnchor align,float padLeft=0,float padRight=0)
        {
            var label=Text(parent,text,font,size,color,align,new Vector2(.5f,.5f),Vector2.zero,Vector2.zero);
            var rt=label.rectTransform;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=new Vector2(padLeft,0);rt.offsetMax=new Vector2(-padRight,0);return label;
        }
        public static Shadow Drop(Graphic g,string hex="0a2d4a",float y=3){var s=g.gameObject.AddComponent<Shadow>();s.effectColor=Hex(hex);s.effectDistance=new Vector2(0,-y);return s;}
    }
}
