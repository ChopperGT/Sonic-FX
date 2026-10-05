using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace SonicFX.HUD.Editor
{
 public static class SonicSpeedHudVerification
 {
  [MenuItem("Tools/Sonic FX/HUD/Verifier la vitesse")]
  public static void Verify()
  {
   if(EditorApplication.isPlayingOrWillChangePlaymode)return;
   string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/NewLevelCharacters");Directory.CreateDirectory(folder);
   var scene=EditorSceneManager.NewPreviewScene();
   try{
    var go=new GameObject("HUD verification");SceneManager.MoveGameObjectToScene(go,scene);
    var player=go.AddComponent<PlayerBhysics>();var body=go.AddComponent<Rigidbody>();body.useGravity=false;player.p_rigidbody=body;player.MaxSpeed=350;
    var hud=go.AddComponent<SonicGameplayHud>();hud.settings=AssetDatabase.LoadAssetAtPath<SonicHudSettings>("Assets/Menu/GameplayHUD/Resources/SonicHudSettings.asset");hud.Build();
    var records=go.GetComponent<SonicRecordsHud>();records.Build(hud.HudCanvas);
    foreach(float value in new[]{0f,65f,350f,360f,500f}){
     body.linearVelocity=Vector3.right*value;records.RefreshSpeed();
     if(records.SpeedText.text!=value.ToString("000"))throw new Exception("Vitesse incorrecte : "+value);
    }
    player.enabled=false;player.SpeedMagnitude=88;body.linearVelocity=Vector3.zero;records.RefreshSpeed();
    if(records.SpeedText.text!="088")throw new Exception("Vitesse du tube incorrecte");
    var counters=hud.HudCanvas.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Compteur de vitesse").ToArray();
    if(counters.Length!=1 || ((RectTransform)counters[0]).anchorMin!=new Vector2(1,0))throw new Exception("Un seul compteur en bas a droite requis");
    if(hud.ScoreText==null||hud.TimeText==null||hud.RingsText==null||hud.LivesText==null)throw new Exception("Un compteur principal manque");
    player.SpeedMagnitude=65;records.RefreshSpeed();
    SonicFX.Menu.Editor.SonicNewLevelVerification.Capture(hud.HudCanvas,scene,Path.Combine(folder,"hud.png"));
    File.WriteAllText(Path.Combine(folder,"speed-verification.txt"),"PASS\n"+DateTime.Now.ToString("s")+"\n65 affiche 065 ; 350, 360 et 500 restent exacts. Tube : 088. Un seul compteur en bas a droite ; quatre compteurs principaux conserves.\n");
   }catch(Exception e){File.WriteAllText(Path.Combine(folder,"speed-verification.txt"),"FAIL\n"+e);throw;}
   finally{EditorSceneManager.ClosePreviewScene(scene);}
  }
 }
}