using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace SonicFX.Chameleon.Editor
{
    [InitializeOnLoad] public static class ChameleonRotationVerification
    {
        static string Folder=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/ChameleonRotation");
        static ChameleonRotationVerification(){EditorApplication.update+=Ready;}
        static void Ready(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;EditorApplication.update-=Ready;if(!File.Exists(ChameleonBuilder.Folder+"/Editor/RotationV1Verified.txt"))Verify();}
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        [MenuItem("Sonic FX/Ennemis/Verifier orientation murale")]
        public static void Verify()
        {
            Directory.CreateDirectory(Folder);Scene scene=default;GameObject enemy=null,wall=null,dummy=null;
            try {
                scene=EditorSceneManager.NewPreviewScene();var physics=scene.GetPhysicsScene();
                Check(physics.IsValid() && physics!=Physics.defaultPhysicsScene,"Scene physique de test isolee");
                enemy=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ChameleonBuilder.Wall));SceneManager.MoveGameObjectToScene(enemy,scene);
                var c=enemy.GetComponent<ChameleonController>();var rb=enemy.GetComponent<Rigidbody>();c.enabled=false;
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(wall,scene);wall.transform.localScale=new Vector3(20,20,1);wall.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/BumperEngineV1/Models/Materials/GreenHillTile.mat");
                dummy=new GameObject("Sonic_Rotation_Test");SceneManager.MoveGameObjectToScene(dummy,scene);c.player=dummy.AddComponent<PlayerBhysics>();c.player.enabled=false;c.reactionTime=0;
                foreach(float angle in new[]{0f,90f,180f,270f}) {
                    Quaternion rotation=Quaternion.Euler(0,angle,0);Vector3 normal=rotation*Vector3.forward;Vector3 center=new Vector3(34000,5000,34000);
                    wall.transform.SetPositionAndRotation(center,rotation);enemy.transform.SetPositionAndRotation(center+normal*1.2f,Quaternion.identity);rb.position=enemy.transform.position;rb.rotation=Quaternion.identity;c.onWall=true;c.startCamouflaged=true;c.wallCollider=wall.GetComponent<Collider>();dummy.transform.position=center+Vector3.up*100;
                    Physics.SyncTransforms();c.Initialize();
                    Check(c.AttachedToWall,"Accroche au mur "+angle);
                    Check(Vector3.Dot(enemy.transform.up,normal)>.999f,"Pattes orientees vers le mur "+angle);
                    Check(Vector3.Dot(rb.rotation*Vector3.up,normal)>.999f,"Rotation Rigidbody alignee "+angle);
                    Check(Vector3.Dot(rb.rotation*Vector3.forward,Vector3.up)>.999f,"Tete vers le haut "+angle);
                    Vector3 pos=rb.position;
                    for(int i=0;i<30;i++){c.Tick(.02f);physics.Simulate(.02f);}
                    Check(Vector3.Dot(rb.rotation*Vector3.up,normal)>.999f,"Rotation conservee apres 30 pas physiques "+angle);Check(Vector3.Distance(pos,rb.position)<.001f,"Camouflage immobile "+angle);
                    dummy.transform.position=c.Eye+normal*2-Vector3.up*.65f;c.Tick(.02f);Check(c.IsLeaping&&!c.AttachedToWall,"Bond depuis le mur "+angle);Check(Vector3.Dot(rb.rotation*Vector3.up,Vector3.up)>.999f,"Retour debout pendant le bond "+angle);
                }
                File.WriteAllText(Path.Combine(Folder,"tests.txt"),"PASS\nFour wall directions; Transform and interpolated Rigidbody aligned; head up / feet toward wall; 30 local physics steps preserve orientation and immobility; jump returns to upright.\n"+DateTime.Now.ToString("s"));
                File.WriteAllText(ChameleonBuilder.Folder+"/Editor/RotationV1Verified.txt","Orientation physique verifiee");AssetDatabase.ImportAsset(ChameleonBuilder.Folder+"/Editor/RotationV1Verified.txt");
            }catch(Exception e){File.WriteAllText(Path.Combine(Folder,"tests.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally{if(enemy!=null)Object.DestroyImmediate(enemy);if(wall!=null)Object.DestroyImmediate(wall);if(dummy!=null)Object.DestroyImmediate(dummy);if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);}
        }
    }
}
