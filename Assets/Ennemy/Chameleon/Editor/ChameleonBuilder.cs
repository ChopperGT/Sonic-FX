using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;
namespace SonicFX.Chameleon.Editor
{
    [InitializeOnLoad]
    public static class ChameleonBuilder
    {
        public const string Folder="Assets/Ennemy/Chameleon";
        public const string Ground=Folder+"/Cameleon_Sol.prefab", Wall=Folder+"/Cameleon_Mur.prefab";
        static string Reports=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/Chameleon");
        static ChameleonBuilder() { EditorApplication.update+=Ready; }
        static void Ready() {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update-=Ready;
            if(!File.Exists(Folder+"/Editor/Verified.txt")) Build();
        }
        static Material green,metal,yellow,black,orange,pink;
        static Material Material(string name,Color color,float metallic,float gloss) {
            string path=Folder+"/Materials/"+name+".mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null) { m=new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m,path); }
            m.color=color;m.SetFloat("_Metallic",metallic);m.SetFloat("_Glossiness",gloss);EditorUtility.SetDirty(m);return m;
        }
        [MenuItem("Sonic FX/Ennemis/Creer ou verifier le cameleon")]
        public static void Build() {
            Directory.CreateDirectory(Reports);
            try {
                Directory.CreateDirectory(Folder+"/Materials"); AssetDatabase.Refresh();
                green=Material("Carapace_Verte",new Color(.32f,.67f,.055f),.3f,.48f);
                metal=Material("Articulations_Argent",new Color(.66f,.75f,.81f),.65f,.55f);
                yellow=Material("Yeux_Jaunes",new Color(1,.87f,.04f),.1f,.6f);
                black=Material("Pupilles_Joints",new Color(.018f,.025f,.033f),.2f,.45f);
                orange=Material("Crete_Orange",new Color(1,.43f,.06f),.35f,.45f);
                pink=Material("Langue_Rose",new Color(.9f,.09f,.28f),.1f,.6f);
                // Fine shell grain is baked into a reusable texture with UVs on every primitive.
                string texturePath=Folder+"/Materials/Carapace.asset";
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if(texture==null) {
                    texture=new Texture2D(128,128,TextureFormat.RGBA32,true); texture.name="Carapace";
                    var pixels=new Color[128*128];
                    for(int y=0;y<128;y++) for(int x=0;x<128;x++) { float noise=Mathf.PerlinNoise(x*.17f,y*.17f); float stripes=Mathf.Pow(Mathf.Max(0,Mathf.Sin((y+x*.3f)*.22f)),24)*.1f;pixels[y*128+x]=Color.white*(.88f+noise*.12f+stripes); }
                    texture.SetPixels(pixels);texture.Apply();AssetDatabase.CreateAsset(texture,texturePath);
                }
                green.mainTexture=texture;
                string camouflagePath=Folder+"/Materials/Camouflage.mat";var camouflage=AssetDatabase.LoadAssetAtPath<Material>(camouflagePath);
                if(camouflage==null) { var shader=Shader.Find("SonicFX/Chameleon Camouflage"); if(shader==null) throw new Exception("Shader de camouflage pas encore importe. Relancer la creation."); camouflage=new Material(shader);AssetDatabase.CreateAsset(camouflage,camouflagePath); }
                var projectile=BuildProjectile();var explosion=BuildExplosion();
                if(AssetDatabase.LoadAssetAtPath<GameObject>(Ground)==null) BuildEnemy(Ground,false,camouflage,projectile,explosion);
                if(AssetDatabase.LoadAssetAtPath<GameObject>(Wall)==null) BuildEnemy(Wall,true,camouflage,projectile,explosion);
                AssetDatabase.SaveAssets();Verify();Preview();
                File.WriteAllText(Folder+"/Editor/Verified.txt","Creation et verification initiale terminees.");AssetDatabase.ImportAsset(Folder+"/Editor/Verified.txt");
                File.WriteAllText(Path.Combine(Reports,"unity-tests.txt"),"PASS\nPrefabs, materials, projectile, contact hierarchy, wall placement, camouflage, homing exclusion, restoration, visibility and occlusion, one projectile per attack, close leap reveals, ground tongue without projectile.\n"+DateTime.Now.ToString("s"));
                Debug.Log("Cameleon pret : "+Ground+" et "+Wall);
            } catch(Exception e) {File.WriteAllText(Path.Combine(Reports,"unity-tests.txt"),"FAIL\n"+e);Debug.LogException(e);}
        }
        static Transform Pivot(string name,Transform parent,Vector3 position) {var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;return go.transform;}
        static Transform Shape(string name,Transform parent,Vector3 position,Vector3 scale,Material material,PrimitiveType type=PrimitiveType.Sphere) {
            var go=GameObject.CreatePrimitive(type);go.name=name;Object.DestroyImmediate(go.GetComponent<Collider>());go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;return go.transform;
        }
        static void Rod(string name,Transform parent,Vector3 a,Vector3 b,float width,Material material) {var p=Shape(name,parent,(a+b)*.5f,new Vector3(width,Vector3.Distance(a,b)*.5f,width),material,PrimitiveType.Cylinder);p.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
        static GameObject BuildProjectile() {
            string path=Folder+"/Cameleon_Projectile.prefab";var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
            var go=new GameObject("Cameleon_Projectile");try {
                go.AddComponent<ChameleonProjectile>();Shape("Coeur",go.transform,Vector3.zero,Vector3.one*.36f,orange);Shape("Coque",go.transform,Vector3.zero,new Vector3(.25f,.25f,.7f),metal);
                var trail=go.AddComponent<TrailRenderer>();trail.sharedMaterial=pink;trail.time=.13f;trail.startWidth=.12f;trail.endWidth=0;trail.minVertexDistance=.05f;
                return PrefabUtility.SaveAsPrefabAsset(go,path);
            }finally{Object.DestroyImmediate(go);}
        }
        static GameObject BuildExplosion() {
            string path=Folder+"/Cameleon_Explosion.prefab";var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
            var go=new GameObject("Cameleon_Explosion");try {
                var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=ps.main;main.duration=.4f;main.loop=false;main.startLifetime=.55f;main.startSpeed=4;main.startSize=.18f;main.startColor=new Color(1,.6f,.1f);main.simulationSpace=ParticleSystemSimulationSpace.World;
                var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,22)});var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.3f;
                ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=orange;go.AddComponent<ChameleonLifetime>();return PrefabUtility.SaveAsPrefabAsset(go,path);
            }finally{Object.DestroyImmediate(go);}
        }
        static void BuildEnemy(string path,bool wall,Material camo,GameObject projectile,GameObject explosion) {
            var root=new GameObject(wall?"Cameleon_Mur":"Cameleon_Sol");try {
                int enemies=LayerMask.NameToLayer("Enemies"),triggerLayer=LayerMask.NameToLayer("EnemyTrigger");if(enemies<0||triggerLayer<0)throw new Exception("Couches Enemies / EnemyTrigger manquantes.");
                root.layer=enemies;var rb=root.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
                var brain=root.AddComponent<ChameleonController>();brain.onWall=wall;brain.startCamouflaged=wall;brain.projectilePrefab=projectile.GetComponent<ChameleonProjectile>();
                var visual=root.AddComponent<ChameleonVisual>();brain.visual=visual;visual.controller=brain;
                var skin=Pivot("Modele",root.transform,Vector3.zero);visual.body=skin;
                Shape("Carapace",skin,new Vector3(0,1.1f,-.25f),new Vector3(1.45f,1.4f,2.05f),green);
                Shape("Ventre",skin,new Vector3(0,.83f,-.12f),new Vector3(1.26f,.7f,1.6f),metal);
                Shape("Tete",skin,new Vector3(0,1.2f,.91f),new Vector3(1.35f,1.15f,1.25f),green);
                Shape("Museau",skin,new Vector3(0,1.05f,1.4f),new Vector3(1.2f,.65f,.75f),green);
                Shape("Bouche_ombre",skin,new Vector3(0,.82f,1.55f),new Vector3(.85f,.25f,.33f),black);
                visual.jaw=Pivot("Machoire",skin,new Vector3(0,.7f,1.1f));Shape("Machoire_verte",visual.jaw,new Vector3(0,0,.27f),new Vector3(.92f,.23f,.72f),green);
                for(int side=-1;side<=1;side+=2) {
                    Shape("Orbite",skin,new Vector3(side*.61f,1.43f,1.05f),new Vector3(.61f,.76f,.8f),green);
                    Shape("Oeil",skin,new Vector3(side*.8f,1.45f,1.13f),new Vector3(.26f,.62f,.63f),yellow);
                    Shape("Pupille",skin,new Vector3(side*.934f,1.46f,1.23f),new Vector3(.045f,.14f,.14f),black);
                    Shape("Reflet",skin,new Vector3(side*.96f,1.5f,1.255f),Vector3.one*.038f,metal);
                }
                for(int i=0;i<4;i++) Shape("Crete",skin,new Vector3(0,1.77f-i*.04f,.65f-i*.43f),new Vector3(.2f,.24f,.54f),orange);
                var legs=new List<Transform>();
                for(int row=0;row<2;row++)for(int side=-1;side<=1;side+=2) {
                    float z=row==0?.4f:-.85f;var leg=Pivot("Patte_"+row+"_"+side,skin,new Vector3(side*.62f,1.02f,z));legs.Add(leg);
                    Shape("Rotule",leg,Vector3.zero,Vector3.one*.52f,metal);Vector3 knee=new Vector3(side*.38f,-.43f,.04f),foot=new Vector3(side*.55f,-.84f,.3f);
                    Rod("Bras",leg,Vector3.zero,knee,.21f,metal);Shape("Joint",leg,knee,Vector3.one*.26f,black);Rod("Tibia",leg,knee,foot,.19f,metal);
                    for(int toe=-1;toe<=1;toe+=2) {Vector3 end=foot+new Vector3(toe*.14f,-.02f,.34f);Rod("Griffe",leg,foot,end,.13f,metal);Shape("Bout",leg,end,Vector3.one*.13f,metal);}
                }
                visual.legs=legs.ToArray();var tails=new List<Transform>();
                Vector3 last=new Vector3(0,1,-1.1f);
                for(int i=0;i<22;i++) {
                    float t=(i+1)/22f;float angle=-Mathf.PI*.5f+t*Mathf.PI*1.9f;float radius=.95f*(1-t*.7f);
                    Vector3 point=new Vector3(0,1.08f+radius*Mathf.Sin(angle),-2.15f-radius*Mathf.Cos(angle));
                    if(i==0)point=new Vector3(0,.75f,-1.4f);
                    var segment=Pivot("Queue_"+i,skin,(last+point)*.5f);tails.Add(segment);Rod("Anneau",segment,last-segment.localPosition,point-segment.localPosition,Mathf.Lerp(.48f,.12f,t),i%3==0?metal:green);Shape("Joint",segment,point-segment.localPosition,Vector3.one*Mathf.Lerp(.46f,.11f,t),i%3==0?metal:green);last=point;
                }
                visual.tail=tails.ToArray();brain.mouth=Pivot("Sortie_Bouche",skin,new Vector3(0,.88f,1.74f));
                var cam=root.AddComponent<ChameleonCamouflage>();brain.camouflage=cam;cam.camouflageTemplate=camo;cam.surfaces=skin.GetComponentsInChildren<Renderer>();
                var tongue=Pivot("Langue",root.transform,Vector3.zero).gameObject.AddComponent<LineRenderer>();tongue.sharedMaterial=pink;tongue.useWorldSpace=true;tongue.numCapVertices=5;tongue.numCornerVertices=4;tongue.enabled=false;visual.tongue=tongue;
                visual.tongueTip=Shape("Bout_Langue",root.transform,Vector3.zero,Vector3.one*.36f,pink);visual.tongueTip.gameObject.SetActive(false);
                var collision=Pivot("Corps_Collision",root.transform,new Vector3(0,1,.1f));collision.gameObject.layer=enemies;var solid=collision.gameObject.AddComponent<SphereCollider>();solid.radius=.68f;
                var contact=Pivot("Contact_Sonic",root.transform,new Vector3(0,1,.1f));contact.gameObject.layer=triggerLayer;contact.tag="Enemy";var trigger=contact.gameObject.AddComponent<SphereCollider>();trigger.radius=.85f;trigger.isTrigger=true;
                brain.homingTarget=Pivot("Cible_Homing",root.transform,new Vector3(0,1,.1f)).gameObject;brain.homingTarget.tag="HomingTarget";brain.homingTarget.SetActive(!wall);
                var health=root.AddComponent<EnemyHealth>();health.Explosion=explosion;health.ScoreOnDefeat=200;
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }finally{Object.DestroyImmediate(root);}
        }
        static void Check(bool condition,string message) {if(!condition)throw new Exception(message);}
        static void Verify() {
            GameObject enemy=null,wall=null,block=null,dummy=null;
            var previousShots=new HashSet<ChameleonProjectile>(Object.FindObjectsByType<ChameleonProjectile>());
            try {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Wall);Check(prefab!=null,"Prefab mur");enemy=Object.Instantiate(prefab);enemy.hideFlags=HideFlags.HideAndDontSave;
                Vector3 origin=new Vector3(18000,8000,18000);enemy.transform.position=origin;
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=origin+Vector3.back;wall.transform.localScale=new Vector3(12,12,1);wall.GetComponent<Renderer>().sharedMaterial=green;
                var brain=enemy.GetComponent<ChameleonController>();brain.wallCollider=wall.GetComponent<Collider>();Physics.SyncTransforms();brain.Initialize();Physics.SyncTransforms();
                Check(brain.AttachedToWall && brain.IsCamouflaged,"Placement et camouflage au mur");Check(!brain.homingTarget.activeInHierarchy,"Cible desactivee");
                Check(Array.IndexOf(GameObject.FindGameObjectsWithTag("HomingTarget"),brain.homingTarget)<0,"Camoufle absent de l'auto-lock");
                Vector3 point=brain.Eye+enemy.transform.up*8;Check(brain.CanSee(point),"Vision devant le mur");Check(!brain.CanSee(brain.Eye-enemy.transform.up*8),"Vision derriere le mur");
                block=GameObject.CreatePrimitive(PrimitiveType.Cube);block.transform.position=(brain.Eye+point)*.5f;block.transform.localScale=Vector3.one*2;Physics.SyncTransforms();Check(!brain.CanSee(point),"Obstacle masque Sonic");Object.DestroyImmediate(block);block=null;
                Vector3 before=enemy.transform.position;brain.Tick(.1f);Check(enemy.transform.position==before,"Pas de patrouille camouflee");
                brain.SetCamouflaged(false);Check(brain.homingTarget.activeInHierarchy,"Cible retablie");Check(brain.camouflage.surfaces[0].sharedMaterial==green,"Materiaux retablis");
                Check(enemy.transform.Find("Contact_Sonic").parent.GetComponent<EnemyHealth>()!=null,"Contact compatible EnemyHealth");Check(brain.projectilePrefab!=null && brain.mouth!=null,"References tir");
                var ground=AssetDatabase.LoadAssetAtPath<GameObject>(Ground).GetComponent<ChameleonController>();Check(!ground.onWall && ground.groundSpeed>ground.wallSpeed,"Sol plus rapide");
                dummy=new GameObject("Sonic_Test_Cameleon");brain.player=dummy.AddComponent<PlayerBhysics>();brain.player.enabled=false;brain.reactionTime=0;
                dummy.transform.position=point-Vector3.up*.65f;brain.SetCamouflaged(true);brain.Tick(.02f);brain.Tick(brain.windup+.01f);Check(brain.ProjectilesFired==1,"Un seul projectile emis");brain.Tick(.1f);Check(brain.ProjectilesFired==1,"Pas de rafale pendant l'attaque");
                dummy.transform.position=brain.Eye+enemy.transform.up*2-Vector3.up*.65f;brain.Tick(.02f);Check(brain.IsLeaping && !brain.IsCamouflaged && brain.homingTarget.activeSelf && !brain.AttachedToWall,"Bond proche et decamouflage");
                Object.DestroyImmediate(enemy);enemy=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Ground));enemy.transform.position=origin;brain=enemy.GetComponent<ChameleonController>();brain.player=dummy.GetComponent<PlayerBhysics>();brain.reactionTime=0;brain.Initialize();dummy.transform.position=brain.Eye+Vector3.forward*3-Vector3.up*.65f;Physics.SyncTransforms();
                brain.Tick(.02f);brain.Tick(brain.windup+brain.tongueDuration*.5f);Check(brain.visual.tongue.enabled && brain.ProjectilesFired==0,"Langue au sol sans projectile");
            } finally {if(enemy!=null)Object.DestroyImmediate(enemy);if(wall!=null)Object.DestroyImmediate(wall);if(block!=null)Object.DestroyImmediate(block);if(dummy!=null)Object.DestroyImmediate(dummy);foreach(var shot in Object.FindObjectsByType<ChameleonProjectile>())if(!previousShots.Contains(shot))Object.DestroyImmediate(shot.gameObject);HomingAttackControl.UpdateHomingTargets();}
        }
        static void Preview() {
            var preview=new PreviewRenderUtility();Texture2D image=null;
            try {
                var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Ground));preview.AddSingleGO(go);
                preview.camera.transform.position=new Vector3(5,3.2f,6);preview.camera.transform.LookAt(new Vector3(0,1,-.6f));preview.camera.fieldOfView=36;
                preview.camera.clearFlags=CameraClearFlags.Color;preview.camera.backgroundColor=new Color(.12f,.16f,.21f);preview.lights[0].intensity=1.3f;preview.lights[0].transform.rotation=Quaternion.Euler(35,-30,0);preview.lights[1].intensity=.8f;preview.ambientColor=Color.gray;
                preview.BeginStaticPreview(new Rect(0,0,1000,850));preview.Render();image=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(Reports,"Cameleon.png"),image.EncodeToPNG());
            } finally {if(image!=null)Object.DestroyImmediate(image);preview.Cleanup();}
        }
    }
    [CustomEditor(typeof(ChameleonController))]
    public sealed class ChameleonInspector:UnityEditor.Editor {
        public override void OnInspectorGUI() {
            DrawDefaultInspector();var c=(ChameleonController)target;
            EditorGUILayout.HelpBox("Mur : placer pres de la paroi puis cocher Colle au mur. Camoufle = immobile et sans auto-lock. Un bond le fait passer au sol. Les rayons jaunes montrent la detection, le cercle rouge la portee d'attaque.",MessageType.Info);
            if(!Application.isPlaying && GUILayout.Button("Coller et orienter sur le mur")) {
                Undo.RecordObjects(new UnityEngine.Object[]{c.transform,c,c.GetComponent<Rigidbody>()},"Coller et orienter le cameleon");
                c.onWall=true;if(!c.SnapToWall())Debug.LogWarning("Aucun mur detecte : rapprocher le cameleon ou renseigner Mur support.",c);
                PrefabUtility.RecordPrefabInstancePropertyModifications(c);PrefabUtility.RecordPrefabInstancePropertyModifications(c.transform);
                EditorUtility.SetDirty(c);
            }
            if(Application.isPlaying) {
                EditorGUILayout.LabelField("Etat",c.Status);
                if(c.camouflage!=null && c.IsCamouflaged) EditorGUILayout.HelpBox(c.camouflage.TextureSource,c.camouflage.HasWallTexture?MessageType.Info:MessageType.Warning);
            }
        }
    }
}
