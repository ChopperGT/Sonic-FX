using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace SonicFX.Structures.Editor
{
    [InitializeOnLoad] public static class SonicBreakBlockBuilder
    {
        public const string Root="Assets/Structure/BlocDestructible";
        public const string PrefabPath=Root+"/Bloc_Destructible.prefab";
        public static readonly string Reports=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/BreakBlock");
        static SonicBreakBlockBuilder(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)return;
            EditorApplication.update-=Ready;if(!File.Exists(PrefabPath))Build();
        }
        [MenuItem("Tools/Sonic FX/Bloc destructible/Creer le prefab")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            Directory.CreateDirectory(Root);Directory.CreateDirectory(Reports);
            GameObject root=null;
            try
            {
                Material wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/Structure/décore/GreenHill/Bois_Dore.mat");
                if(wood==null)wood=Material("Bois",new Color(.62f,.3f,.08f),0);
                var steel=Material("Renforts",new Color(.06f,.12f,.18f),.55f);
                var bolt=Material("Boulons",new Color(1,.7f,.12f),.35f);
                var cyan=Material("Cible_Rotation",new Color(.1f,.85f,1),.2f);
                root=new GameObject("Bloc_Destructible");var block=root.AddComponent<SonicBreakBlock>();
                block.solid=root.AddComponent<BoxCollider>();block.solid.center=new Vector3(0,3,0);block.solid.size=new Vector3(8,6,2);
                block.sensor=root.AddComponent<BoxCollider>();block.sensor.isTrigger=true;block.sensor.center=block.solid.center;block.sensor.size=new Vector3(10,7,4);
                var visual=new GameObject("Apparence");visual.transform.SetParent(root.transform,false);block.visual=visual.transform;block.debrisMaterial=wood;
                for(int i=0;i<6;i++)Cube(visual.transform,"Planche",new Vector3((i-2.5f)*1.31f,3,0),new Vector3(1.29f,6,1.96f),wood);
                foreach(float side in new[]{-1f,1f})
                {
                    foreach(float x in new[]{-3.75f,3.75f})Cube(visual.transform,"Renfort vertical",new Vector3(x,3,side*1.025f),new Vector3(.4f,6,.12f),steel);
                    foreach(float y in new[]{.25f,5.75f})Cube(visual.transform,"Renfort horizontal",new Vector3(0,y,side*1.04f),new Vector3(8,.5f,.15f),steel);
                    foreach(float x in new[]{-3.75f,3.75f})foreach(float y in new[]{.25f,5.75f})Sphere(visual.transform,new Vector3(x,y,side*1.16f),.22f,bolt);
                    var plate=GameObject.CreatePrimitive(PrimitiveType.Cylinder);plate.name="Medaille de rotation";plate.transform.SetParent(visual.transform,false);plate.transform.localPosition=new Vector3(0,3,side*1.09f);plate.transform.localRotation=Quaternion.Euler(90,0,0);plate.transform.localScale=new Vector3(2.65f,.08f,2.65f);Object.DestroyImmediate(plate.GetComponent<Collider>());plate.GetComponent<Renderer>().sharedMaterial=steel;
                    for(int i=0;i<14;i++)
                    {
                        float angle=(i*24+10)*Mathf.Deg2Rad;var mark=Cube(visual.transform,"Rotation",new Vector3(Mathf.Cos(angle),3+Mathf.Sin(angle),side*1.2f),new Vector3(.3f,.15f,.08f),cyan);mark.transform.localRotation=Quaternion.Euler(0,0,i*24+100);
                    }
                    var arrow=Cube(visual.transform,"Fleche",new Vector3(1.01f,2.65f,side*1.22f),new Vector3(.4f,.3f,.1f),bolt);arrow.transform.localRotation=Quaternion.Euler(0,0,-35);
                }
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);AssetDatabase.SaveAssets();
                File.WriteAllText(Path.Combine(Reports,"builder-report.txt"),"PASS: prefab created; solid obstacle + outer sensor; wooden planks/metal braces/rotation target; editable difficulty.\n"+DateTime.Now.ToString("s"));
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Reports,"builder-report.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally{if(root!=null)Object.DestroyImmediate(root);}
        }
        static Material Material(string name,Color color,float metallic)
        {
            string path=Root+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(material,path);}
            material.color=color;material.SetFloat("_Metallic",metallic);material.SetFloat("_Glossiness",.32f);EditorUtility.SetDirty(material);return material;
        }
        static GameObject Cube(Transform parent,string name,Vector3 pos,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static void Sphere(Transform parent,Vector3 pos,float size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name="Boulon";go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=Vector3.one*size;go.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(go.GetComponent<Collider>());
        }
    }
}
