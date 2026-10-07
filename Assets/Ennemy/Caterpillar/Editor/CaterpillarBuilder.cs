using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace SonicFX.Caterpillar.Editor
{
    [InitializeOnLoad]
    public static class CaterpillarBuilder
    {
        public const string Folder="Assets/Ennemy/Caterpillar", Prefab=Folder+"/Chenille.prefab";
        static Material pink,silver,gold,black,white,red;
        static CaterpillarBuilder(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)return;
            EditorApplication.update-=Ready;if(!File.Exists(Prefab))Build();else{UpgradeSegments();UpgradeTeeth();}
        }
        public static void UpgradeSegments()
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);if(asset==null)return;
            var original=asset.GetComponent<CaterpillarController>();if(original.segmentPool!=null && original.segmentPool.Length>=2)return;
            var root=PrefabUtility.LoadPrefabContents(Prefab);
            try{var c=root.GetComponent<CaterpillarController>();c.bodySegmentCount=Mathf.Max(1,c.parts.Length-1);c.ApplyBodySize();PrefabUtility.SaveAsPrefabAsset(root,Prefab);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();Debug.Log("Chenille : nombre de boules du corps reglable dans l'Inspector.");
        }
        static Material Mat(string name,Color color,float metal,float gloss)
        {
            string path=Folder+"/Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Standard"));mat.color=color;mat.SetFloat("_Metallic",metal);mat.SetFloat("_Glossiness",gloss);AssetDatabase.CreateAsset(mat,path);}return mat;
        }
        static Transform Pivot(string name,Transform parent,Vector3 position){var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;return go.transform;}
        static Transform Shape(string name,Transform parent,Vector3 p,Vector3 s,Material mat,PrimitiveType type=PrimitiveType.Sphere)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;Object.DestroyImmediate(go.GetComponent<Collider>());go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=s;go.GetComponent<Renderer>().sharedMaterial=mat;return go.transform;
        }
        static void Rod(string name,Transform parent,Vector3 a,Vector3 b,float r,Material mat)
        {
            var p=Shape(name,parent,(a+b)*.5f,new Vector3(r,(b-a).magnitude*.5f,r),mat,PrimitiveType.Cylinder);p.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
        }
        static Mesh Horn()
        {
            string path=Folder+"/Meshes/Corne.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing!=null)return existing;
            var verts=new List<Vector3>();var tris=new List<int>();var uv=new List<Vector2>();const int n=10;
            for(int r=0;r<5;r++)
            {
                float t=r/4f;Vector3 center=new Vector3(.35f*t,.05f+Mathf.Pow(t,1.8f)*.55f,.07f*Mathf.Sin(t*Mathf.PI));float radius=Mathf.Lerp(.22f,.006f,t);
                for(int i=0;i<n;i++){float angle=i*Mathf.PI*2/n;verts.Add(center+new Vector3(0,Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius));uv.Add(new Vector2(i/(float)n,t));}
            }
            for(int r=0;r<4;r++)for(int i=0;i<n;i++){int a=r*n+i,b=r*n+(i+1)%n,c=a+n,d=b+n;tris.Add(a);tris.Add(b);tris.Add(c);tris.Add(b);tris.Add(d);tris.Add(c);}
            var mesh=new Mesh{name="Corne recourbee"};mesh.SetVertices(verts);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static Mesh Tooth()
        {
            string path=Folder+"/Meshes/Croc.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing!=null)return existing;
            const int sides=12;var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int ring=0;ring<2;ring++)for(int i=0;i<sides;i++)
            {
                float angle=i*Mathf.PI*2/sides;float radius=ring==0?.10f:.055f;
                vertices.Add(new Vector3(Mathf.Cos(angle)*radius,ring==0?0:-.18f,Mathf.Sin(angle)*radius));
            }
            vertices.Add(new Vector3(0,-.34f,0));vertices.Add(Vector3.zero);
            for(int i=0;i<sides;i++)
            {
                int j=(i+1)%sides;triangles.Add(i);triangles.Add(j);triangles.Add(i+sides);triangles.Add(j);triangles.Add(j+sides);triangles.Add(i+sides);
                triangles.Add(i+sides);triangles.Add(j+sides);triangles.Add(24);triangles.Add(25);triangles.Add(j);triangles.Add(i);
            }
            var mesh=new Mesh{name="Croc droit"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        public static void UpgradeTeeth()
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);if(asset==null)return;var mesh=Tooth();bool needsUpgrade=false;
            foreach(var filter in asset.GetComponentsInChildren<MeshFilter>(true))if(filter.name=="Dent" && (filter.sharedMesh!=mesh || Quaternion.Angle(filter.transform.localRotation,Quaternion.identity)>.1f))needsUpgrade=true;
            if(!needsUpgrade)return;
            var root=PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))if(filter.name=="Dent")
                {filter.sharedMesh=mesh;filter.transform.localRotation=Quaternion.identity;filter.transform.localScale=Vector3.one;filter.transform.localPosition=new Vector3(Mathf.Sign(filter.transform.localPosition.x)*.27f,-.10f,.635f);}
                PrefabUtility.SaveAsPrefabAsset(root,Prefab);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();Debug.Log("Chenille : crocs droits orientes vers le bas.");
        }
        static void AddHorn(Transform parent,int side,Mesh mesh)
        {
            var horn=Pivot("Corne_"+side,parent,new Vector3(side*.47f,.22f,0));horn.localScale=new Vector3(side,1,1);horn.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;horn.gameObject.AddComponent<MeshRenderer>().sharedMaterial=gold;
        }
        [MenuItem("Sonic FX/Ennemis/Creer ou verifier la chenille")]
        public static void Build()
        {
            if(File.Exists(Prefab))return;
            pink=Mat("Carapace_Rose",new Color(.9f,.055f,.29f),.28f,.42f);silver=Mat("Ventre_Argent",new Color(.7f,.77f,.83f),.6f,.55f);
            gold=Mat("Cornes_Or",new Color(1,.65f,.015f),.4f,.5f);black=Mat("Joints_Pupilles",new Color(.018f,.018f,.025f),.1f,.4f);white=Mat("Yeux_Dents",new Color(.94f,.97f,1),.12f,.5f);red=Mat("Machoire",new Color(.68f,.025f,.16f),.25f,.5f);
            string texPath=Folder+"/Materials/Carapace.asset";var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if(texture==null)
            {
                texture=new Texture2D(128,128,TextureFormat.RGBA32,true){name="Carapace rose grain fin",wrapMode=TextureWrapMode.Repeat};var pixels=new Color[128*128];
                for(int y=0;y<128;y++)for(int x=0;x<128;x++){float grain=.85f+.14f*Mathf.PerlinNoise(x*.15f,y*.15f);float seam=Mathf.Pow(Mathf.Max(0,Mathf.Sin(y*.14f)),22)*.045f;pixels[y*128+x]=new Color(grain-seam,grain-seam,grain-seam,1);}
                texture.SetPixels(pixels);texture.Apply();AssetDatabase.CreateAsset(texture,texPath);
            }
            pink.mainTexture=texture;EditorUtility.SetDirty(pink);var hornMesh=Horn();var toothMesh=Tooth();
            var root=new GameObject("Chenille");
            try
            {
                int enemyLayer=LayerMask.NameToLayer("Enemies"),triggerLayer=LayerMask.NameToLayer("EnemyTrigger");if(enemyLayer<0 || triggerLayer<0)throw new Exception("Couches Enemies / EnemyTrigger manquantes.");
                root.layer=enemyLayer;var rb=root.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
                var brain=root.AddComponent<CaterpillarController>();var visual=root.AddComponent<CaterpillarVisual>();brain.visual=visual;
                var segments=new List<Transform>();var contacts=new List<SphereCollider>();
                for(int i=0;i<4;i++)
                {
                    var segment=Pivot(i==0?"Tete":"Segment_"+i,root.transform,new Vector3(0,.91f,-i*1.05f));segments.Add(segment);
                    if(i==0)
                    {
                        Shape("Tete_Rose",segment,Vector3.zero,new Vector3(1.35f,1.25f,1.2f),pink);
                        Shape("Oreille_Gauche",segment,new Vector3(-.65f,0,.08f),new Vector3(.25f,.78f,.58f),gold);Shape("Oreille_Droite",segment,new Vector3(.65f,0,.08f),new Vector3(.25f,.78f,.58f),gold);
                        var antennae=new List<Transform>();
                        for(int side=-1;side<=1;side+=2)
                        {
                            Shape("Oeil",segment,new Vector3(side*.28f,.23f,.505f),new Vector3(.48f,.55f,.23f),white);
                            Shape("Pupille",segment,new Vector3(side*.275f,.24f,.62f),new Vector3(.10f,.15f,.045f),black);
                            Shape("Reflet",segment,new Vector3(side*.26f,.285f,.642f),Vector3.one*.026f,white);
                            var antenna=Pivot("Antenne_"+side,segment,new Vector3(side*.28f,.48f,-.05f));antennae.Add(antenna);
                            Rod("Tige",antenna,Vector3.zero,new Vector3(0,.65f,0),.045f,black);Shape("Bout_Or",antenna,new Vector3(0,.7f,0),Vector3.one*.32f,gold);
                        }
                        visual.antennae=antennae.ToArray();Shape("Bouche",segment,new Vector3(0,-.19f,.49f),new Vector3(.83f,.36f,.22f),black);
                        visual.jaw=Pivot("Machoire",segment,new Vector3(0,-.3f,.22f));Shape("Machoire_Rose",visual.jaw,new Vector3(0,0,.15f),new Vector3(.99f,.4f,.64f),red);
                        for(int side=-1;side<=1;side+=2){var tooth=Pivot("Dent",segment,new Vector3(side*.27f,-.10f,.635f));tooth.gameObject.AddComponent<MeshFilter>().sharedMesh=toothMesh;tooth.gameObject.AddComponent<MeshRenderer>().sharedMaterial=white;}
                    }
                    else
                    {
                        Shape("Carapace",segment,new Vector3(0,.04f,0),new Vector3(1.13f,1.05f,1.1f),pink);
                        Shape("Ventre",segment,new Vector3(0,-.35f,0),new Vector3(1.03f,1.0f,1.0f),silver);
                        var seam=Shape("Joint",segment,new Vector3(0,-.32f,-.34f),new Vector3(1.035f,.06f,.67f),black);seam.localRotation=Quaternion.Euler(25,0,0);
                        AddHorn(segment,-1,hornMesh);AddHorn(segment,1,hornMesh);
                    }
                    segment.gameObject.layer=triggerLayer;var contact=segment.gameObject.AddComponent<SphereCollider>();contact.radius=i==0?.69f:.62f;contact.isTrigger=true;contacts.Add(contact);
                }
                visual.segments=segments.ToArray();brain.parts=segments.ToArray();brain.contactColliders=contacts.ToArray();
                brain.frontTarget=Pivot("Cible_Tete",root.transform,new Vector3(0,.91f,.48f));brain.frontTarget.tag="HomingTarget";
                brain.rearTarget=Pivot("Cible_Arriere",root.transform,new Vector3(0,.91f,-3.55f));brain.rearTarget.tag="HomingTarget";
                var health=root.AddComponent<EnemyHealth>();health.ScoreOnDefeat=100;health.Explosion=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Ennemy/Chameleon/Cameleon_Explosion.prefab");
                brain.ApplyBodySize();visual.Pose(CaterpillarController.Behaviour.Patrol,0,0,0,.4f);PrefabUtility.SaveAsPrefabAsset(root,Prefab);
            }
            finally{Object.DestroyImmediate(root);}
            AssetDatabase.SaveAssets();Debug.Log("Chenille prete : "+Prefab);
        }
    }
    [CustomEditor(typeof(CaterpillarController))]
    class CaterpillarControllerEditor : UnityEditor.Editor
    {
        void OnEnable(){Undo.undoRedoPerformed+=RefreshSize;EditorApplication.delayCall+=RefreshSize;}
        void OnDisable(){Undo.undoRedoPerformed-=RefreshSize;EditorApplication.delayCall-=RefreshSize;}
        void RefreshSize()
        {
            var c=target as CaterpillarController;if(c==null)return;
            if(EditorUtility.IsPersistent(c))
            {
                string path=AssetDatabase.GetAssetPath(c);if(string.IsNullOrEmpty(path))return;
                if(c.parts.Length==Mathf.Clamp(c.bodySegmentCount,1,32)+1)return;
                var root=PrefabUtility.LoadPrefabContents(path);
                try{root.GetComponent<CaterpillarController>().ApplyBodySize();PrefabUtility.SaveAsPrefabAsset(root,path);}
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            else c.ApplyBodySize(false);
            Repaint();
        }
        public override void OnInspectorGUI()
        {
            var c=(CaterpillarController)target;serializedObject.Update();int previous=c.bodySegmentCount;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bodySegmentCount"),new GUIContent("Nombre de boules du corps","La tete est conservee. Les animations, collisions et la cible arriere suivent la longueur."));
            DrawPropertiesExcluding(serializedObject,"bodySegmentCount","segmentPool","parts","contactColliders");serializedObject.ApplyModifiedProperties();
            if(previous!=c.bodySegmentCount)
            {
                if(EditorUtility.IsPersistent(c)){EditorApplication.delayCall-=RefreshSize;EditorApplication.delayCall+=RefreshSize;}
                else c.ApplyBodySize(!Application.IsPlaying(c.gameObject));
            }
            EditorGUILayout.HelpBox("Nombre de boules du corps : ajoute ou retire des segments, sans compter la tete. La taille change immediatement dans la Scene. Les segments retires sont masques et leurs collisions desactivees.",MessageType.Info);
            EditorGUILayout.HelpBox("La chenille suit les points facultatifs en aller-retour ; sans points elle patrouille autour de sa position de depart. Les contacts sont geres par la chenille : ne pas leur ajouter le tag Enemy ou Hazard. Zones vertes = tete et arriere vulnerables.",MessageType.Info);
            if(Application.isPlaying)EditorGUILayout.LabelField("Comportement",c.State+" / "+c.Status);
        }
    }
}
