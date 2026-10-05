using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SonicFX.HUD;

namespace SonicFX.Score
{
    // Medaille de la maquette "Tableaux de score" : disque degrade + halo, anneau pointille qui tourne,
    // centre Sonic, reflet qui balaie, coche si obtenue. Textures dessinees en code puis mises en cache.
    public sealed class SonicMedal : MonoBehaviour
    {
        public const int Bronze=0,Silver=1,Gold=2,Diamond=3,Rainbow=4;
        public static readonly string[] Ids={"bronze","silver","gold","diamond","rainbow"};
        public static readonly string[] Names={"BRONZE","ARGENT","OR","DIAMANT","ARC-EN-CIEL"};
        public static readonly Color[] LabelColors={SonicUi.Hex("e8ab72"),SonicUi.Hex("dfe6ec"),SonicUi.Hex("ffd75e"),SonicUi.Hex("aeeeff"),Color.white};

        // Targets are descending tiers. Legacy four-target results still omit bronze.
        public static bool ValidTimes(float[] times)=>times!=null && (times.Length==4 || times.Length==5);
        public static int LowestTier(float[] times)=>times!=null && times.Length==5?Bronze:Silver;
        public static float Target(float[] times,int tier)=>times[Rainbow-tier];
        public static int TierFor(float seconds,float[] times)
        {
            if(!ValidTimes(times))return -1;
            for(int t=Rainbow;t>=LowestTier(times);t--)if(seconds<=Target(times,t))return t;
            return -1;
        }
        // Nom arc-en-ciel lettre par lettre, comme le degrade de texte de la maquette.
        public static string RichName(int tier)
        {
            if(tier!=Rainbow)return Names[tier];
            string[] hues={"ff4b4b","ffb13b","fff35b","5bff8a","5ee0ff","6b7bff","d86bff"};
            var sb=new System.Text.StringBuilder();
            for(int i=0;i<Names[tier].Length;i++)sb.Append("<color=#").Append(hues[i%hues.Length]).Append('>').Append(Names[tier][i]).Append("</color>");
            return sb.ToString();
        }

        // ---------- Textures ----------
        static readonly Dictionary<string,Texture2D> cache=new Dictionary<string,Texture2D>();
        static Sprite circle;
        const float Halo=1.35f; // la texture du disque deborde pour le halo

        static Color[] Stops(int tier)=>tier switch {
            Bronze=>new[]{SonicUi.Hex("ffe0ba"),SonicUi.Hex("b87333"),SonicUi.Hex("78411f"),SonicUi.Hex("efb47d")},
            Silver=>new[]{SonicUi.Hex("ffffff"),SonicUi.Hex("9aa4ad"),SonicUi.Hex("6b7680"),SonicUi.Hex("e9edf0")},
            Gold=>new[]{SonicUi.Hex("fff3c4"),SonicUi.Hex("d9a520"),SonicUi.Hex("b07a0c"),SonicUi.Hex("ffe99a")},
            Diamond=>new[]{SonicUi.Hex("ffffff"),SonicUi.Hex("7fd8f0"),SonicUi.Hex("cdf4ff"),SonicUi.Hex("ffffff")},
            _=>new[]{SonicUi.Hex("ff4b4b"),SonicUi.Hex("ffb13b"),SonicUi.Hex("fff35b"),SonicUi.Hex("5bff8a"),SonicUi.Hex("5ee0ff"),SonicUi.Hex("6b7bff"),SonicUi.Hex("d86bff"),SonicUi.Hex("ff4b4b")}};
        static float[] StopAt(int tier)=>tier==Diamond?new[]{0,.45f,.7f,1}:new[]{0,.5f,.75f,1};
        static readonly Color[] Glow={new Color(.9f,.5f,.2f,.5f),new Color(.86f,.9f,.94f,.4f),new Color(1,.82f,.35f,.6f),new Color(.5f,.85f,.94f,.55f),new Color(1,1,1,.45f)};
        static readonly Color[] Dots={new Color(.32f,.14f,.05f,.7f),new Color(.16f,.2f,.24f,.6f),new Color(.47f,.27f,0,.7f),new Color(.08f,.35f,.47f,.7f),new Color(1,1,1,.85f)};
        static readonly Color[] Bezel={SonicUi.Hex("512c16"),SonicUi.Hex("3a444d"),SonicUi.Hex("6b4a00"),SonicUi.Hex("1a4a5e"),SonicUi.Hex("1a1a2e")};

        static Color Body(int tier,float nx,float ny)
        {
            var c=Stops(tier);
            if(tier==Rainbow)
            {
                // conic-gradient depuis le haut, sens horaire
                float a=Mathf.Repeat(Mathf.Atan2(nx,ny)/(2*Mathf.PI),1)*(c.Length-1);int i=Mathf.Min((int)a,c.Length-2);
                return Color.Lerp(c[i],c[i+1],a-i);
            }
            float u=Mathf.Clamp01(.5f+(nx-ny)/4),prev=0;var at=StopAt(tier);
            for(int i=1;i<c.Length;i++){if(u<=at[i])return Color.Lerp(c[i-1],c[i],(u-prev)/(at[i]-prev));prev=at[i];}
            return c[c.Length-1];
        }
        static Color Grey(Color c){float l=c.r*.2126f+c.g*.7152f+c.b*.0722f;var g=Color.Lerp(c,new Color(l,l,l,c.a),.9f)*.55f;g.a=c.a;return g;}
        static Color Over(Color under,Color over){float a=over.a+under.a*(1-over.a);if(a<=0)return Color.clear;var c=(over*over.a+under*under.a*(1-over.a))/a;c.a=a;return c;}

        static Texture2D Make(string key,int size,System.Func<float,float,float,Color> pixel)
        {
            if(cache.TryGetValue(key,out var tex) && tex!=null)return tex;
            tex=new Texture2D(size,size,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,name=key};
            var px=new Color[size*size];float px1=2f/size;
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float nx=(x+.5f)/size*2-1,ny=(y+.5f)/size*2-1;px[y*size+x]=pixel(nx,ny,px1);
            }
            tex.SetPixels(px);tex.Apply();cache[key]=tex;return tex;
        }
        static float Edge(float r,float radius,float aa)=>Mathf.Clamp01((radius-r)/aa+.5f);

        // Disque complet (halo, corps, filet, lunette). plain = pastille sans filet ni lunette (HUD).
        public static Texture2D BaseTexture(int tier,bool got,bool plain=false)=>Make($"medal_{tier}_{got}_{plain}",plain?64:320,(nx,ny,aa)=>
        {
            float r=Mathf.Sqrt(nx*nx+ny*ny)*(plain?1:Halo);aa*=plain?1:Halo;
            Color c=Body(tier,nx,ny);if(!got)c=Grey(c);c.a=Edge(r,1,aa);
            if(plain)return c;
            if(r>.948f && r<.965f)c=Over(c,new Color(1,1,1,tier==Diamond?.8f:.6f));
            if(r>.67f && r<.705f)c=Over(c,got?Bezel[tier]:Grey(Bezel[tier]));
            if(got && r>1){var g=Glow[tier];g.a*=Mathf.Pow(Mathf.Clamp01(1-(r-1)/(Halo-1)),2);c=Over(g,c);}
            return c;
        });
        // Anneau pointille (border 9px dotted de la maquette), tourne a part.
        public static Texture2D RingTexture(int tier)=>Make("ring_"+tier,256,(nx,ny,aa)=>
        {
            const int count=33;const float radius=.822f,dot=.039f;
            float a=Mathf.Atan2(ny,nx),step=2*Mathf.PI/count,snap=Mathf.Round(a/step)*step;
            float dx=nx-Mathf.Cos(snap)*radius,dy=ny-Mathf.Sin(snap)*radius;
            var c=Dots[tier];c.a*=Edge(Mathf.Sqrt(dx*dx+dy*dy),dot,aa);return c;
        });
        public static Texture2D TrailTexture=>Make("trail",64,(nx,ny,aa)=>new Color(1,1,1,(nx+1)/2));
        // Bande de reflet : fondue sur les cotes et aux deux bouts.
        static Texture2D ShineTexture=>Make("shine",64,(nx,ny,aa)=>new Color(1,1,1,(1-Mathf.Abs(nx))*(1-ny*ny)));
        // Disque blanc en sprite 9-slice : masques, pastilles et pilules arrondies.
        public static Sprite Circle
        {
            get
            {
                if(circle!=null)return circle;
                var tex=Make("circle",128,(nx,ny,aa)=>new Color(1,1,1,Edge(Mathf.Sqrt(nx*nx+ny*ny),1,aa)));
                return circle=Sprite.Create(tex,new Rect(0,0,128,128),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(63,63,63,63));
            }
        }

        // ---------- Widget ----------
        RectTransform pop,ring,shine;float size,delay,start,shineDelay;bool popped,pulse,spin;AudioSource audioSource;AudioClip clip;

        static RawImage Raw(Transform parent,string name,Texture tex,float size,Color color)
        {
            var img=SonicUi.Rect(parent,name,new Vector2(.5f,.5f),Vector2.zero,new Vector2(size,size)).gameObject.AddComponent<RawImage>();
            img.texture=tex;img.color=color;img.raycastTarget=false;return img;
        }

        // Petite pastille plate (HUD) : pas d'animation.
        public static RawImage Dot(Transform parent,int tier,Vector2 anchor,Vector2 pos,float size)
        {
            var img=SonicUi.Rect(parent,"Pastille "+Names[tier],anchor,pos,new Vector2(size,size)).gameObject.AddComponent<RawImage>();
            img.texture=BaseTexture(tier,true,true);img.raycastTarget=false;return img;
        }

        // delay < 0 : affichee tout de suite sans pop. clip joue a l'apparition.
        public static SonicMedal Create(Transform parent,int tier,Vector2 anchor,Vector2 pos,float size,bool got,bool top,float delay=-1,AudioSource audio=null,AudioClip clip=null)
        {
            var root=SonicUi.Rect(parent,"Medaille "+Names[tier],anchor,pos,new Vector2(size,size));
            var group=root.gameObject.AddComponent<CanvasGroup>();group.alpha=got?1:.55f;group.blocksRaycasts=false;
            var pop=SonicUi.Rect(root,"Pop",new Vector2(.5f,.5f),Vector2.zero,new Vector2(size,size));
            Raw(pop,"Disque",BaseTexture(tier,got),size*Halo,Color.white);
            var ring=Raw(pop,"Anneau",RingTexture(tier),size,Color.white).rectTransform;
            var center=Raw(pop,"Centre",Resources.Load<Texture2D>("SonicMedals/medal_center"),size*154f/230,got?Color.white:new Color(.45f,.45f,.45f));
            if(center.texture==null)center.color=Color.clear;
            // Reflet : bande blanche inclinee ; sa longueur suit la corde du disque (pas de masque stencil).
            var shine=Raw(pop,"Reflet",ShineTexture,1,new Color(1,1,1,tier==Diamond?.9f:.8f)).rectTransform;
            shine.localEulerAngles=new Vector3(0,0,-20);
            if(got)
            {
                float b=size*56/230,o=size/2+size*10/230-b/2;
                SonicUi.Img(pop,"Coche fond",new Vector2(.5f,.5f),new Vector2(o,-o),new Vector2(b,b),SonicUi.Hex("0a1626"),Circle);
                var check=SonicUi.Img(pop,"Coche",new Vector2(.5f,.5f),new Vector2(o,-o),new Vector2(b-size*8/230,b-size*8/230),SonicUi.Hex("5bff8a"),Circle);
                float t=Mathf.Max(2,size*6/230);
                var c1=SonicUi.Img(check.transform,"Trait court",new Vector2(.5f,.5f),new Vector2(-b*.12f,-b*.04f),new Vector2(b*.28f,t),SonicUi.Hex("0a1626"));c1.rectTransform.localEulerAngles=new Vector3(0,0,-45);
                var c2=SonicUi.Img(check.transform,"Trait long",new Vector2(.5f,.5f),new Vector2(b*.08f,b*.04f),new Vector2(b*.48f,t),SonicUi.Hex("0a1626"));c2.rectTransform.localEulerAngles=new Vector3(0,0,50);
            }
            var m=root.gameObject.AddComponent<SonicMedal>();
            m.pop=pop;m.ring=ring;m.shine=shine;m.size=size;m.delay=delay;m.pulse=top;m.spin=got;m.audioSource=audio;m.clip=clip;
            m.start=Time.unscaledTime;m.shineDelay=tier*.3f;
            if(delay>=0)pop.localScale=Vector3.zero;else m.popped=true;
            return m;
        }

        void Update()
        {
            if(ring==null){enabled=false;return;} // medaille rechargee depuis une scene : ses liens ne sont pas serialises
            float now=Time.unscaledTime,t=now-start-Mathf.Max(0,delay);
            ring.localEulerAngles=new Vector3(0,0,-now*20); // 360deg / 18 s
            float s=Mathf.Repeat(now-shineDelay,2.6f)/2.6f;s=s*s*(3-2*s);
            // La bande inclinee de 20deg est recentree sur sa corde pour ne jamais deborder du disque.
            float x=Mathf.Lerp(-.45f,.45f,s)*size,radius=size*.46f,sin=Mathf.Sin(20*Mathf.Deg2Rad),cos=Mathf.Cos(20*Mathf.Deg2Rad);
            float mid=-x*sin,half=Mathf.Sqrt(Mathf.Max(0,radius*radius-x*x*cos*cos));
            shine.anchoredPosition=new Vector2(x+mid*sin,mid*cos);shine.sizeDelta=new Vector2(size*40/230,2*half);
            if(delay>=0)
            {
                if(t<0)return;
                if(!popped){popped=true;if(audioSource!=null && clip!=null)audioSource.PlayOneShot(clip);}
                // medalIn : scale 0 / -40deg -> 1.12 / 4deg (70 %) -> 1 / 0deg, sur 0.7 s
                float k=t/.7f;
                if(k<1)
                {
                    float scale,rot;
                    if(k<.7f){float e=1-Mathf.Pow(1-k/.7f,3);scale=1.12f*e;rot=Mathf.Lerp(-40,4,e);}
                    else{float e=(k-.7f)/.3f;scale=Mathf.Lerp(1.12f,1,e);rot=Mathf.Lerp(4,0,e);}
                    pop.localScale=Vector3.one*scale;pop.localEulerAngles=new Vector3(0,0,-rot);return;
                }
            }
            // Medaille validee : un tour de piece toutes les 3 s (decale selon le palier).
            float cycle=Mathf.Repeat(t-.7f+shineDelay,3f);
            pop.localEulerAngles=new Vector3(0,spin && cycle<.8f?Mathf.SmoothStep(0,360,cycle/.8f):0,0);
            pop.localScale=Vector3.one*(pulse?1+.03f*(1-Mathf.Cos((t-.7f)*2*Mathf.PI/1.6f)):1);
        }
    }
}
