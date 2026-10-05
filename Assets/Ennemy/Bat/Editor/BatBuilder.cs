using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;
namespace SonicFX.Bat.Editor
{
    [InitializeOnLoad]
    public static class BatBuilder
    {
        public const string Folder="Assets/Ennemy/Bat", Prefab=Folder+"/ChauveSouris.prefab";
        static BatBuilder(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)return;
            EditorApplication.update-=Ready;
            if(!File.Exists(Prefab))Build();
        }
        static Material blue,silver,white,black,red,yellow;
        static Material Mat(string name,Color color,float metal,float gloss,Texture2D texture=null)
        {
            string path=Folder+"/Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material!=null)return material;
            material=new Material(Shader.Find("Standard")){name=name,color=color};material.SetFloat("_Metallic",metal);material.SetFloat("_Glossiness",gloss);material.mainTexture=texture;AssetDatabase.CreateAsset(material,path);return material;
        }
        static Texture2D Texture(string name,bool metallic)
        {
            string path=Folder+"/Materials/"+name+".png";
            if(!File.Exists(path))
            {
                var texture=new Texture2D(256,256,TextureFormat.RGBA32,true);var pixels=new Color[256*256];
                for(int y=0;y<256;y++)for(int x=0;x<256;x++)
                {
                    float n=Mathf.PerlinNoise(x*.13f,y*.13f);
                    float grain=metallic?.86f+n*.12f+Mathf.Sin(y*.38f)*.025f:.9f+n*.1f;
                    if(!metallic && (x%64<2 || y%96<2))grain*=.85f;
                    pixels[y*256+x]=new Color(grain,grain,grain,1);
                }
                texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.wrapMode=TextureWrapMode.Repeat;importer.maxTextureSize=256;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        [MenuItem("Sonic FX/Ennemis/Creer ou verifier la chauve-souris")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder+"/Materials");Directory.CreateDirectory(Folder+"/Meshes");Directory.CreateDirectory(Folder+"/Sounds");AssetDatabase.Refresh();
            blue=Mat("Carapace_Bleue",new Color(.025f,.52f,.73f),.35f,.55f,Texture("Coque_Bleue",false));
            silver=Mat("Ailes_Argent",new Color(.78f,.84f,.9f),.6f,.5f,Texture("Metal_Brosse",true));
            white=Mat("Yeux_Blancs",new Color(.97f,.99f,1),.05f,.4f);black=Mat("Joints_Pupilles",new Color(.012f,.023f,.038f),.2f,.4f);
            red=Mat("Reacteur_Rouge",new Color(1,.12f,.02f),0,.1f);yellow=Mat("Reacteur_Jaune",new Color(1,.8f,.03f),0,.1f);
            if(!File.Exists(Prefab))
            {
                var root=new GameObject("ChauveSouris");try
                {
                    var brain=root.AddComponent<BatController>();var body=Pivot("Modele",root.transform,Vector3.zero);
                    var visual=root.AddComponent<BatVisual>();brain.visual=visual;visual.body=body;
                    Shape("Corps_Bleu",body,new Vector3(0,-.03f,-.05f),new Vector3(.82f,1.15f,.82f),blue);
                    Shape("Tete",body,new Vector3(0,.3f,.22f),new Vector3(.8f,.75f,.6f),blue);
                    for(int side=-1;side<=1;side+=2)
                    {
                        var ear=MeshObject("Oreille",body,new Vector3(side*.26f,.57f,.2f),Cone(side),silver);
                        Shape("Yeux",body,new Vector3(side*.2f,.37f,.48f),new Vector3(.37f,.49f,.15f),white);
                        Shape("Pupille",body,new Vector3(side*.18f,.37f,.56f),new Vector3(.10f,.15f,.055f),black);
                        Transform wing=Pivot(side<0?"Aile_Gauche":"Aile_Droite",body,new Vector3(side*.35f,.16f,-.05f));
                        MeshObject("Membrane",wing,Vector3.zero,Wing(side),silver);
                        Rod("Bord_superieur",wing,Vector3.zero,new Vector3(side*1.85f,1.4f,0),.065f,white);
                        Rod("Nervure",wing,Vector3.zero,new Vector3(side*1.45f,-.2f,0),.038f,white);
                        Rod("Nervure_centrale",wing,new Vector3(side*.3f,.55f,0),new Vector3(side*1.25f,.15f,0),.03f,white);
                        if(side<0)visual.leftWing=wing;else visual.rightWing=wing;
                        Vector3[] hook={new Vector3(side*.18f,-.49f,.1f),new Vector3(side*.2f,-.68f,.2f),new Vector3(side*.22f,-.77f,.38f),new Vector3(side*.22f,-.7f,.53f),new Vector3(side*.22f,-.57f,.55f)};
                        for(int i=0;i<hook.Length-1;i++)Rod("Griffe",body,hook[i],hook[i+1],.09f,silver);
                    }
                    Shape("Bouche",body,new Vector3(0,.05f,.5f),new Vector3(.3f,.12f,.07f),black);
                    Shape("Reacteur_Joint",body,new Vector3(0,-.26f,-.45f),new Vector3(.4f,.4f,.3f),black);
                    var jet=Shape("Reacteur",body,new Vector3(0,-.25f,-.62f),new Vector3(.3f,.24f,.3f),silver,PrimitiveType.Cylinder);jet.localRotation=Quaternion.Euler(90,0,0);
                    visual.flame=Shape("Flamme",body,new Vector3(0,-.25f,-.98f),new Vector3(.22f,.22f,.45f),red);
                    Shape("Flamme_Coeur",visual.flame,Vector3.forward*.14f,new Vector3(.6f,.6f,.6f),yellow);
                    Transform contact=Pivot("Contact_Sonic",root.transform,Vector3.zero);contact.tag="Enemy";contact.gameObject.layer=LayerMask.NameToLayer("EnemyTrigger");
                    var sphere=contact.gameObject.AddComponent<SphereCollider>();sphere.isTrigger=true;sphere.radius=.52f;brain.contact=sphere;
                    var rb=root.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;
                    brain.homingTarget=Pivot("Cible_Homing",root.transform,Vector3.zero).gameObject;brain.homingTarget.tag="HomingTarget";
                    var health=root.AddComponent<EnemyHealth>();health.MaxHealth=1;health.ScoreOnDefeat=100;
                    brain.shriek=Shriek();visual.Pose(BatController.Behaviour.Sleeping,0);
                    PrefabUtility.SaveAsPrefabAsset(root,Prefab);
                }finally{Object.DestroyImmediate(root);}
            }
            AssetDatabase.SaveAssets();Debug.Log("Chauve-souris prete : "+Prefab);
        }
        static Transform Pivot(string name,Transform parent,Vector3 pos){var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=pos;return go.transform;}
        static Transform Shape(string name,Transform parent,Vector3 pos,Vector3 scale,Material material,PrimitiveType type=PrimitiveType.Sphere)
        {var go=GameObject.CreatePrimitive(type);go.name=name;Object.DestroyImmediate(go.GetComponent<Collider>());go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;return go.transform;}
        static void Rod(string name,Transform parent,Vector3 a,Vector3 b,float width,Material material)
        {var p=Shape(name,parent,(a+b)*.5f,new Vector3(width,Vector3.Distance(a,b)*.5f,width),material,PrimitiveType.Cylinder);p.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
        static Transform MeshObject(string name,Transform parent,Vector3 pos,Mesh mesh,Material material)
        {var p=Pivot(name,parent,pos);p.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;p.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;return p;}
        static Mesh Wing(int side)
        {
            string path=Folder+"/Meshes/Aile_"+(side<0?"Gauche":"Droite")+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh!=null)return mesh;
            Vector3[] contour={new Vector3(0,0,0),new Vector3(.3f,.55f,0),new Vector3(1.85f,1.4f,0),new Vector3(1.25f,.15f,0),new Vector3(1.45f,-.2f,0),new Vector3(.65f,-.1f,0)};
            var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
            for(int face=0;face<2;face++)
            {
                int offset=vertices.Count;Vector3 center=new Vector3(.65f,.35f,0);vertices.Add(new Vector3(center.x*side,center.y,face==0?.025f:-.025f));uv.Add(new Vector2(.35f,.4f));
                foreach(var p in contour){vertices.Add(new Vector3(p.x*side,p.y,face==0?.025f:-.025f));uv.Add(new Vector2(p.x/1.85f,(p.y+.2f)/1.6f));}
                bool reverse=(side==1)==(face==0);
                for(int i=0;i<contour.Length;i++){triangles.Add(offset);triangles.Add(offset+1+(reverse?(i+1)%contour.Length:i));triangles.Add(offset+1+(reverse?i:(i+1)%contour.Length));}
            }
            // Separate front/back vertices give correct silver lighting on both sides.
            mesh=new Mesh{name="Aile"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static Mesh Cone(int side)
        {
            string path=Folder+"/Meshes/Oreille_"+side+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh!=null)return mesh;
            mesh=new Mesh{name="Oreille"};mesh.vertices=new[]{new Vector3(-.13f,0,.08f),new Vector3(.13f,0,.08f),new Vector3(side*.04f,.35f,0),new Vector3(0,0,-.12f)};
            mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};mesh.triangles=new[]{0,1,2,1,3,2,3,0,2,0,3,1};mesh.RecalculateNormals();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static AudioClip Shriek()
        {
            string path=Folder+"/Sounds/Hurlement.wav";
            if(!File.Exists(path))
            {
                const int rate=44100;int samples=(int)(rate*.38f);
                using(var writer=new BinaryWriter(File.Create(path)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+samples*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(samples*2);
                    double phase=0;for(int i=0;i<samples;i++){double t=(double)i/rate,envelope=Math.Sin(Math.PI*i/samples);double frequency=900+1900*Math.Exp(-t*9)+120*Math.Sin(t*95);phase+=2*Math.PI*frequency/rate;double value=(Math.Sin(phase)+.25*Math.Sin(phase*1.51))*envelope*.26;writer.Write((short)(value*32767));}
                }
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
