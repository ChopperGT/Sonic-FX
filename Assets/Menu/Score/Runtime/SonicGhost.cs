using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SonicFX.Score
{
    // Enregistre le modele de Sonic pendant le run et rejoue le meilleur run en fantome.
    // Synchronise sur SonicLevelScore.Elapsed : pauses et morts n'avancent ni l'enregistrement ni le fantome.
    [DisallowMultipleComponent] public sealed class SonicGhost : MonoBehaviour
    {
        struct Sample { public float t; public Vector3 p; public Quaternion r; public int state; public float norm; }
        const float Rate=1/30f;
        const string EnabledKey="SonicGhostEnabled";

        Animator model;
        readonly List<Sample> recording=new List<Sample>();
        List<Sample> best;
        GameObject ghost; Animator ghostAnimator; int cursor;

        static string PathFor(string level)=>Path.Combine(Application.persistentDataPath,"ghost_"+level.Replace('/','_').Replace('\\','_')+".bytes");
        public static bool Enabled { get=>PlayerPrefs.GetInt(EnabledKey,1)==1; set{PlayerPrefs.SetInt(EnabledKey,value?1:0);} }
        public static string RecordDate(string level)=>PlayerPrefs.GetString("SonicGhostDate_"+level,"");

        public bool HasGhost=>ghost!=null;
        public float BestTime=>best!=null && best.Count>0?best[best.Count-1].t:0;
        // Ecart avec le fantome au meme endroit du parcours : negatif = en avance. NaN si inconnu.
        public float Delta {get;private set;}=float.NaN;

        void Start()
        {
            var actions=GetComponent<ActionManager>();
            model=actions!=null && actions.Action00!=null?actions.Action00.CharacterAnimator:GetComponentInChildren<Animator>();
            if(model==null){enabled=false;return;}
            // Un seul fantome : celui du meilleur temps, et seulement si le niveau a deja ete termine.
            var top=SonicRecords.Top(gameObject.scene.path);
            best=top.Length>0?Load(PathFor(gameObject.scene.path)):null;
            if(best!=null && (best.Count<2 || Mathf.Abs(best[best.Count-1].t-top[0])>.25f))best=null;
            if(best!=null)SpawnGhost();
        }

        void Update()
        {
            if((Keyboard.current!=null && Keyboard.current.gKey.wasPressedThisFrame) || (Gamepad.current!=null && Gamepad.current.selectButton.wasPressedThisFrame))Enabled=!Enabled;
            float t=SonicLevelScore.Elapsed;
            if(SonicLevelScore.IsRunning && (recording.Count==0 || t-recording[recording.Count-1].t>=Rate))
            {
                var info=model.GetCurrentAnimatorStateInfo(0);
                recording.Add(new Sample{t=t,p=model.transform.position,r=model.transform.rotation,state=info.fullPathHash,norm=info.normalizedTime});
            }
            if(ghost!=null)Play(t);
        }

        void Play(float t)
        {
            bool show=Enabled && t<=best[best.Count-1].t;
            if(ghost.activeSelf!=show)ghost.SetActive(show);
            if(!show){Delta=float.NaN;return;}
            while(cursor<best.Count-2 && best[cursor+1].t<=t)cursor++;
            Sample a=best[cursor],b=best[cursor+1];
            float k=Mathf.InverseLerp(a.t,b.t,t);
            ghost.transform.SetPositionAndRotation(Vector3.Lerp(a.p,b.p,k),Quaternion.Slerp(a.r,b.r,k));
            ghostAnimator.Play(a.state,0,a.norm);
            Delta=t-NearestSampleTime(model.transform.position);
        }

        // ponytail: recherche lineaire sur +/-5 s autour du fantome ; suffit tant qu'on ne compare qu'un seul fantome.
        float NearestSampleTime(Vector3 position)
        {
            int from=Mathf.Max(0,cursor-150),to=Mathf.Min(best.Count-1,cursor+150);float bestDistance=float.MaxValue,time=best[cursor].t;
            for(int i=from;i<=to;i++){float d=(best[i].p-position).sqrMagnitude;if(d<bestDistance){bestDistance=d;time=best[i].t;}}
            return time;
        }

        // Appele par LevelProgressControl quand le run est un nouveau record.
        public void SaveAsBest()
        {
            PlayerPrefs.SetString("SonicGhostDate_"+gameObject.scene.path,System.DateTime.Now.ToString("d MMM yyyy",new System.Globalization.CultureInfo("fr-FR")));
            using(var w=new BinaryWriter(File.Create(PathFor(gameObject.scene.path))))
            {
                w.Write(recording.Count);
                foreach(var s in recording)
                {
                    w.Write(s.t);w.Write(s.p.x);w.Write(s.p.y);w.Write(s.p.z);
                    w.Write(s.r.x);w.Write(s.r.y);w.Write(s.r.z);w.Write(s.r.w);w.Write(s.state);w.Write(s.norm);
                }
            }
        }

        static List<Sample> Load(string path)
        {
            if(!File.Exists(path))return null;
            try
            {
                using(var r=new BinaryReader(File.OpenRead(path)))
                {
                    int n=r.ReadInt32();var list=new List<Sample>(n);
                    for(int i=0;i<n;i++)list.Add(new Sample{t=r.ReadSingle(),p=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle()),
                        r=new Quaternion(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle()),state=r.ReadInt32(),norm=r.ReadSingle()});
                    return list;
                }
            }
            catch(System.Exception e){Debug.LogWarning("Fantome illisible, ignore : "+e.Message);return null;}
        }

        void SpawnGhost()
        {
            // Clone sous un parent inactif : les scripts copies ne s'executent jamais (pas de second Sonic, camera ou son).
            var holder=new GameObject("Fantome (construction)");holder.SetActive(false);
            ghost=Instantiate(model.gameObject,holder.transform);ghost.name="Fantome du record";
            // Une simple coquille visuelle : aucun script, collision ni physique.
            foreach(var c in ghost.GetComponentsInChildren<MonoBehaviour>(true))DestroyImmediate(c);
            foreach(var c in ghost.GetComponentsInChildren<Component>(true))
                if(c is Collider || c is Rigidbody || c is AudioSource || c is Camera || c is AudioListener)DestroyImmediate(c);
            ghost.transform.SetParent(null,false);Destroy(holder);
            ghostAnimator=ghost.GetComponent<Animator>();ghostAnimator.speed=0;
            // ponytail: Shader.Find exige que l'Unlit URP soit dans le build (Always Included) ; sinon le fantome garde ses materiaux d'origine.
            var shader=Shader.Find("Universal Render Pipeline/Unlit");
            if(shader==null)return;
            var mat=new Material(shader);
            mat.SetFloat("_Surface",1);mat.SetFloat("_Blend",0);mat.SetFloat("_ZWrite",0);
            mat.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=(int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetColor("_BaseColor",new Color(.3f,.75f,1f,.4f));
            foreach(var rend in ghost.GetComponentsInChildren<Renderer>(true))
            {
                var mats=new Material[rend.sharedMaterials.Length];
                for(int i=0;i<mats.Length;i++)mats[i]=mat;
                rend.sharedMaterials=mats;rend.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        void OnDestroy(){if(ghost!=null)Destroy(ghost);}
    }
}
