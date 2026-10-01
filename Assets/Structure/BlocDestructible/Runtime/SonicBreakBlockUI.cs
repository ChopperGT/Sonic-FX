using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
namespace SonicFX.Structures
{
    public sealed class SonicBreakBlockUI : MonoBehaviour
    {
        public Canvas Canvas {get;private set;}
        SonicBreakBlock owner;Text title,info;RectTransform fill;Image fillImage;
        Font font;
        public void Build(SonicBreakBlock block)
        {
            owner=block;font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go=new GameObject("Barre de destruction",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));go.transform.SetParent(transform,false);
            Canvas=go.GetComponent<Canvas>();Canvas.renderMode=RenderMode.ScreenSpaceOverlay;Canvas.sortingOrder=65;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            var panel=Box(go.transform,"Panneau",new Vector2(700,155),new Color(.01f,.04f,.08f,.94f));panel.anchorMin=panel.anchorMax=new Vector2(.5f,0);panel.anchoredPosition=new Vector2(0,130);
            title=Label(panel,"MARTÈLE LA TOUCHE DE SAUT !",new Vector2(0,45),new Vector2(670,36),27);
            var track=Box(panel,"Fond de barre",new Vector2(646,32),new Color(.08f,.14f,.2f));track.anchoredPosition=new Vector2(0,0);
            fill=Box(track,"Progression",Vector2.zero,new Color(.05f,.8f,.95f));fill.anchorMin=Vector2.zero;fill.anchorMax=new Vector2(0,1);fill.offsetMin=fill.offsetMax=Vector2.zero;fillImage=fill.GetComponent<Image>();
            info=Label(panel,"",new Vector2(0,-46),new Vector2(670,35),20);Refresh();
        }
        void Update(){Refresh();}
        public void Refresh()
        {
            if(owner==null || fill==null)return;
            fill.anchorMax=new Vector2(owner.Progress,1);
            if(owner.State==SonicBreakBlock.Phase.Recoiling || owner.State==SonicBreakBlock.Phase.Recovering)
            {
                title.text=owner.State==SonicBreakBlock.Phase.Recovering?"SONIC SE RELÈVE…":"LE BLOC RÉSISTE !";
                info.text="Reviens en boule pour réessayer";fillImage.color=new Color(1,.28f,.12f);
            }
            else
            {
                title.text="MARTÈLE LA TOUCHE DE SAUT !";
                string button=Gamepad.current!=null?"Saut : bouton du bas (Croix / A)":"Saut : "+owner.keyboardJump;
                info.text=button+"     •     "+Mathf.RoundToInt(owner.Progress*100)+" %     •     "+owner.Remaining.ToString("0.0")+" s";
                fillImage.color=Color.Lerp(new Color(.05f,.65f,.95f),new Color(.4f,1,.25f),owner.Progress);
            }
        }
        RectTransform Box(Transform parent,string name,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var rt=(RectTransform)go.transform;
            rt.sizeDelta=size;go.GetComponent<Image>().color=color;go.GetComponent<Image>().raycastTarget=false;return rt;
        }
        Text Label(Transform parent,string text,Vector2 pos,Vector2 size,int fontSize)
        {
            var go=new GameObject("Texte",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var rt=(RectTransform)go.transform;rt.sizeDelta=size;rt.anchoredPosition=pos;
            var label=go.GetComponent<Text>();label.font=font;label.fontSize=fontSize;label.text=text;label.color=Color.white;label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;return label;
        }
    }
}
