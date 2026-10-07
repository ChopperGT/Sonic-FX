using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace SonicFX.Lava.Editor
{
    [InitializeOnLoad]
    public static class SonicLavaGeyserBuilder
    {
        public const string Folder="Assets/Structure/GeyserLave";
        public const string PrefabPath=Folder+"/Geyser_Lave.prefab";
        public static string Reports=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/LavaGeyser");
        static SonicLavaGeyserBuilder(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null)return;
            EditorApplication.update-=Ready;string report=Path.Combine(Reports,"tests.txt");
            if(!File.Exists(report)||!File.ReadAllText(report).StartsWith("PASS"))Build();
        }
        static Material Mat(string name,string shader)
        {
            string p=Folder+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);if(m!=null)return m;
            var s=Shader.Find(shader);if(s==null || ShaderUtil.ShaderHasError(s))throw new Exception("Shader indisponible : "+shader);
            m=new Material(s){name=name};AssetDatabase.CreateAsset(m,p);return m;
        }
        static Mesh MeshAsset(string name,Func<Mesh> create)
        {
            string p=Folder+"/Meshes/"+name+".asset";var m=AssetDatabase.LoadAssetAtPath<Mesh>(p);if(m!=null)return m;m=create();m.name=name;AssetDatabase.CreateAsset(m,p);return m;
        }
        static GameObject Child(GameObject parent,string name){var go=new GameObject(name);go.transform.SetParent(parent.transform,false);return go;}
        static MeshFilter Visual(GameObject parent,string name,Mesh mesh,Material material)
        {
            var go=Child(parent,name);var f=go.AddComponent<MeshFilter>();f.sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;return f;
        }
        [MenuItem("Sonic FX/Lave/Installer et verifier le geyser")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;Directory.CreateDirectory(Reports);GameObject root=null;
            var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();bool dirty=active.isDirty;var selection=Selection.objects;
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                foreach(var part in new[]{"Meshes","Materials"})if(!AssetDatabase.IsValidFolder(Folder+"/"+part))AssetDatabase.CreateFolder(Folder,part);
                var warning=Mat("Avertissement_Rouge","Sonic FX/Geyser Impact");var jet=Mat("Jet_Lave","Sonic FX/Geyser Jet Lave");var droplets=Mat("Gouttes_Lave","Sonic FX/Geyser Goutte");var mouth=Mat("Lave_Bouche","Sonic FX/Lave");
                var magma=AssetDatabase.LoadAssetAtPath<Material>("Assets/Structure/PierresMagma/Magma.mat");if(magma==null)throw new Exception("Materiau des pierres de magma introuvable.");
                var rockNames=new[]{"Petite","Moyenne","Grande","Allongee","Pointue","Plate"};var rocks=new GameObject[rockNames.Length];
                for(int i=0;i<rocks.Length;i++){rocks[i]=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Structure/PierresMagma/Pierre_Magma_"+rockNames[i]+".prefab");if(rocks[i]==null)throw new Exception("Pierre magma manquante : "+rockNames[i]);}
                if(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)==null)
                {
                    root=new GameObject("Geyser_Lave");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);var g=root.AddComponent<SonicLavaGeyser>();g.warningMaterial=warning;g.rockPrefabs=rocks;
                    var rim=Visual(root,"Rebord_Volcanique",MeshAsset("Rebord",Rim),magma);g.vent=rim.transform;rim.gameObject.AddComponent<MeshCollider>().sharedMesh=rim.sharedMesh;
                    var pool=Visual(root,"Bouche_Lave",MeshAsset("Bouche",Disk),mouth);pool.transform.localScale=new Vector3(1.25f,1,1.25f);pool.transform.localPosition=Vector3.up*.2f;
                    var emission=Child(root,"Sortie_Lave");g.mouth=emission.transform;
                    g.jetVisual=Visual(emission,"Colonne_Lave",MeshAsset("Colonne",Column),jet).GetComponent<Renderer>();
                    g.jet=Particles(emission,"Jet_et_Gouttes",droplets,48,.32f,20,2.5f,.18f);
                    g.sparks=Particles(emission,"Explosion_Etincelles",droplets,35,.12f,14,1.1f,.65f);
                    g.audioSource=root.AddComponent<AudioSource>();g.audioSource.playOnAwake=false;g.audioSource.spatialBlend=1;g.audioSource.minDistance=8;g.audioSource.maxDistance=90;
                    var trigger=Child(root,"Zone_Declenchement");trigger.transform.localPosition=new Vector3(0,4,-9);var zone=trigger.AddComponent<SonicGeyserTriggerZone>();zone.geyser=g;trigger.GetComponent<BoxCollider>().isTrigger=true;trigger.GetComponent<BoxCollider>().size=new Vector3(16,8,12);
                    g.landingZones=new SonicGeyserLandingZone[3];
                    for(int i=0;i<3;i++){var z=Child(root,"Zone_Chute_"+(i+1));z.transform.localPosition=new Vector3((i-1)*7,0,13+i%2*3);g.landingZones[i]=z.AddComponent<SonicGeyserLandingZone>();}
                    g.RefreshVisual();g.SetJetStrength(0);if(PrefabUtility.SaveAsPrefabAsset(root,PrefabPath)==null)throw new Exception("Prefab geyser non enregistre.");
                    Object.DestroyImmediate(root);root=null;
                }
                string triggerPath=Folder+"/Zone_Geyser.prefab";
                if(AssetDatabase.LoadAssetAtPath<GameObject>(triggerPath)==null)
                {
                    root=new GameObject("Zone_Geyser");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);root.AddComponent<SonicGeyserTriggerZone>();root.GetComponent<BoxCollider>().isTrigger=true;root.GetComponent<BoxCollider>().center=Vector3.up*4;root.GetComponent<BoxCollider>().size=new Vector3(16,8,12);PrefabUtility.SaveAsPrefabAsset(root,triggerPath);
                }
                if(active!=UnityEngine.SceneManagement.SceneManager.GetActiveScene() || active.isDirty!=dirty)throw new Exception("Scene utilisateur modifiee.");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Reports,"tests.txt"),"FAIL installation\n"+e);Debug.LogException(e);return;}
            finally{if(root!=null)Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);Selection.objects=selection;}
            SonicLavaGeyserVerification.Verify();
        }
        static ParticleSystem Particles(GameObject parent,string name,Material material,int count,float size,float speed,float lifetime,float spread)
        {
            var go=Child(parent,name);var p=go.AddComponent<ParticleSystem>();var m=p.main;m.playOnAwake=false;m.loop=false;m.duration=.8f;m.startLifetime=new ParticleSystem.MinMaxCurve(lifetime*.7f,lifetime);m.startSpeed=new ParticleSystem.MinMaxCurve(speed*.6f,speed);m.startSize=new ParticleSystem.MinMaxCurve(size*.7f,size*1.7f);m.simulationSpace=ParticleSystemSimulationSpace.World;m.gravityModifier=2.45f;m.maxParticles=300;
            var e=p.emission;e.rateOverTime=0;e.SetBursts(new[]{new ParticleSystem.Burst(0,(short)count)});var shape=p.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=spread*60;shape.radius=.45f;shape.rotation=new Vector3(-90,0,0);
            var color=p.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(.8f,.35f,.1f),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,.65f),new GradientAlphaKey(0,1)});color.color=gradient;
            var renderer=p.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=2;renderer.velocityScale=.05f;p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);return p;
        }
        static Mesh Rim()
        {
            const int n=64;var v=new List<Vector3>();var t=new List<int>();float[] r={1,.96f,.68f,.54f};float[] h={0,.48f,.96f,.08f};
            for(int ring=0;ring<4;ring++)for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;float rough=1+.05f*Mathf.Sin(i*2.7f);v.Add(new Vector3(Mathf.Cos(a)*r[ring]*rough,h[ring]*(1+.08f*Mathf.Cos(i*1.9f)),Mathf.Sin(a)*r[ring]*rough));}
            for(int ring=0;ring<4;ring++)for(int i=0;i<n;i++){int next=(i+1)%n;int a=ring*n+i,b=ring*n+next,c=((ring+1)%4)*n+i,d=((ring+1)%4)*n+next;t.Add(a);t.Add(c);t.Add(b);t.Add(b);t.Add(c);t.Add(d);}
            var m=new Mesh();m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;
        }
        static Mesh Disk()
        {
            const int n=64;var v=new Vector3[n+1];var t=new int[n*3];for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;v[i+1]=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));t[i*3]=0;t[i*3+1]=(i+1)%n+1;t[i*3+2]=i+1;}var m=new Mesh();m.vertices=v;m.triangles=t;m.RecalculateNormals();m.RecalculateBounds();return m;
        }
        static Mesh Column()
        {
            const int strips=4,rows=24;var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
            for(int s=0;s<strips;s++)for(int row=0;row<=rows;row++)for(int side=0;side<2;side++)
            {
                float y=(float)row/rows;float x=(side*2-1)*(.55f-.35f*y);float a=s*Mathf.PI/strips;v.Add(new Vector3(Mathf.Cos(a)*x,y,Mathf.Sin(a)*x));uv.Add(new Vector2(side,y));
                if(row<rows && side==0){int k=v.Count-1;t.Add(k);t.Add(k+2);t.Add(k+1);t.Add(k+1);t.Add(k+2);t.Add(k+3);}
            }
            var m=new Mesh();m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;
        }
    }
}
