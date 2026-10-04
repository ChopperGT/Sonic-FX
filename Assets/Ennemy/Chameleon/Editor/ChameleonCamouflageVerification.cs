using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;
namespace SonicFX.Chameleon.Editor
{
    [InitializeOnLoad] public static class ChameleonCamouflageVerification
    {
        static string Folder=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/ChameleonFix");
        static ChameleonCamouflageVerification(){EditorApplication.update+=Ready;}
        static void Ready(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;EditorApplication.update-=Ready;if(!File.Exists(ChameleonBuilder.Folder+"/Editor/CamouflageV2Verified.txt"))Verify();}
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        [MenuItem("Sonic FX/Ennemis/Verifier le camouflage du cameleon")]
        public static void Verify()
        {
            Directory.CreateDirectory(Folder);GameObject enemy=null,wall=null,terrainObject=null;TerrainData data=null;
            try {
                var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/BumperEngineV1/Models/Materials/GreenHillTile.mat");Check(material!=null,"GreenHillTile existe");
                enemy=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ChameleonBuilder.Wall));enemy.transform.position=new Vector3(26000,9000,26000);
                var camouflage=enemy.GetComponent<ChameleonCamouflage>();Check(!ShaderUtil.ShaderHasError(camouflage.camouflageTemplate.shader),"Shader valide");var original=camouflage.surfaces[0].sharedMaterial;
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=enemy.transform.position+Vector3.back;wall.transform.localScale=new Vector3(12,12,1);wall.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(wall.GetComponent<Collider>());
                var child=new GameObject("Collider_separe");child.transform.SetParent(wall.transform,false);var box=child.AddComponent<BoxCollider>();Physics.SyncTransforms();
                Check(box.Raycast(new Ray(enemy.transform.position,Vector3.back),out var hit,5),"Collision enfant");camouflage.Capture(hit);camouflage.Apply(true);
                Check(camouflage.HasWallTexture && camouflage.surfaces[0].sharedMaterial.mainTexture==material.GetTexture("_Side"),"Texture copiee depuis le renderer parent");camouflage.Apply(false);Check(camouflage.surfaces[0].sharedMaterial==original,"Restauration apres camouflage");
                data=new TerrainData();data.heightmapResolution=33;data.size=new Vector3(8,8,8);var heights=new float[33,33];for(int z=0;z<33;z++)for(int x=0;x<33;x++)heights[z,x]=x>=16?1:0;data.SetHeights(0,0,heights);
                terrainObject=Terrain.CreateTerrainGameObject(data);terrainObject.transform.position=enemy.transform.position+Vector3.right*30;terrainObject.GetComponent<Terrain>().materialTemplate=material;Physics.SyncTransforms();
                var terrainCollider=terrainObject.GetComponent<TerrainCollider>();var ray=new Ray(terrainObject.transform.position+new Vector3(1,4,4),Vector3.right);Check(terrainCollider.Raycast(ray,out hit,7),"Paroi de terrain detectee");
                camouflage.Capture(hit);camouflage.Apply(true);Check(camouflage.HasWallTexture && camouflage.surfaces[0].sharedMaterial.mainTexture==material.GetTexture("_Side"),"Terrain sans couches avec materiau GreenHill");Check(camouflage.surfaces[0].sharedMaterial.GetFloat("_Triplanar")==1,"Projection mondiale preservee");
                Preview(material);
                File.WriteAllText(Path.Combine(Folder,"tests.txt"),"PASS\nShader, child collider/parent renderer, Terrain custom GreenHill material without layers, side texture and world mapping, material restoration, camouflage visual preview.\n"+DateTime.Now.ToString("s"));
                File.WriteAllText(ChameleonBuilder.Folder+"/Editor/CamouflageV2Verified.txt","Camouflage terrain et collider enfant verifies");AssetDatabase.ImportAsset(ChameleonBuilder.Folder+"/Editor/CamouflageV2Verified.txt");
            }catch(Exception e){File.WriteAllText(Path.Combine(Folder,"tests.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally{if(enemy!=null)Object.DestroyImmediate(enemy);if(wall!=null)Object.DestroyImmediate(wall);if(terrainObject!=null)Object.DestroyImmediate(terrainObject);if(data!=null)Object.DestroyImmediate(data);}
        }
        static void Preview(Material material)
        {
            var preview=new PreviewRenderUtility();Texture2D image=null;GameObject wall=null,enemy=null;
            try{
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.localScale=new Vector3(12,9,1);wall.GetComponent<Renderer>().sharedMaterial=material;
                enemy=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ChameleonBuilder.Wall));enemy.transform.position=new Vector3(0,0,1);enemy.GetComponent<ChameleonController>().wallCollider=wall.GetComponent<Collider>();Physics.SyncTransforms();enemy.GetComponent<ChameleonController>().Initialize();
                preview.AddSingleGO(wall);preview.AddSingleGO(enemy);preview.camera.transform.position=new Vector3(5,2.2f,10);preview.camera.transform.LookAt(new Vector3(0,.3f,.5f));preview.camera.fieldOfView=40;preview.camera.clearFlags=CameraClearFlags.Color;preview.camera.backgroundColor=Color.gray;preview.lights[0].intensity=1.2f;preview.lights[0].transform.rotation=Quaternion.Euler(30,210,0);preview.lights[1].intensity=.8f;preview.ambientColor=Color.gray;
                preview.BeginStaticPreview(new Rect(0,0,1100,800));preview.Render();image=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(Folder,"Camouflage_Mur.png"),image.EncodeToPNG());
            }finally{if(image!=null)Object.DestroyImmediate(image);preview.Cleanup();if(wall!=null)Object.DestroyImmediate(wall);if(enemy!=null)Object.DestroyImmediate(enemy);}
        }
    }
}
