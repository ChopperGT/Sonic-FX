using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using SonicFX.Menu;
using SonicFX.Menu.Editor;

[InitializeOnLoad] public static class CharacterCorrectionsInstaller
{
    const string Version="SonicFX.CharacterGroundAndHair.v4";
    static string Output=>Path.Combine(Path.GetTempPath(),"SonicFXCharacterFit");
    static CharacterCorrectionsInstaller(){EditorApplication.update+=Ready;}
    static void Ready(){if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||PrefabStageUtility.GetCurrentPrefabStage()!=null)return;EditorApplication.update-=Ready;if(SessionState.GetBool(Version,false)||EditorPrefs.GetBool(Application.dataPath+Version,false))return;SessionState.SetBool(Version,true);Run();}
    [MenuItem("Sonic FX/Personnages/Reparer appui cheveux et selection")]
    public static void Run(){Directory.CreateDirectory(Output);var report=new StringBuilder();try{
        SoniaBuilder.Build();ClassicSonicBuilder.Build();report.AppendLine("PASS prefabs Sonia, Classique Histoire et Free reconstruits.");
        var catalog=SonicNewLevelCatalog.Load();catalog.characters=SonicNewLevelInstaller.MergeCharacters(catalog.characters,catalog.characters);EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
        var merged=SonicNewLevelInstaller.MergeCharacters(catalog.characters,catalog.characters.Where(c=>c.id!="classicsonic"&&c.id!="classicsonicfree"&&c.id!="sonia").ToArray());
        foreach(var id in new[]{"classicsonic","classicsonicfree","sonia"})Require(merged.Count(c=>c.id==id)==1,"Personnage conserve apres reinstallation : "+id);
        Require(SonicXProgress.IsUnlocked("classicsonic"),"Classique selectionnable sans deverrouillage");var route=Resources.Load<SonicStoryRoute>("SonicStoryRoute");foreach(var stage in route.stages.Where(s=>s.character=="sonic"))Require(route.TryGetNextScene("classicsonic",stage.levelScene,out var next)&&next==stage.nextScene,"Parcours Mania partage");report.AppendLine("PASS selection persistante et parcours Histoire identique a Mania.");
        var scene=EditorSceneManager.NewPreviewScene();try{
            foreach(var path in new[]{"Assets/Characters/Sonia/Sonia.prefab","Assets/Characters/ClassicSonic/Resources/SonicClassique.prefab","Assets/Characters/ClassicSonic/SonicClassiqueFree.prefab"}){
                var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),scene);var p=root.GetComponentInChildren<PlayerBhysics>(true);var a=p.GetComponent<ActionManager>().Action00.CharacterAnimator;a.enabled=false;var model=a.transform.Find(path.Contains("Sonia")?"SoniaRig":"ClassicRig");var shoes=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.name.StartsWith("Blaze.008_Blaze.004")||r.name.StartsWith("SonicClassic.001")).ToArray();var capsule=(CapsuleCollider)p.CollisionCapsule;float floor=capsule.transform.TransformPoint(capsule.center-Vector3.up*capsule.height*.5f).y;
                foreach(var name in new[]{"Idle","Walk","Jog","Run","MachRun","Skid"}){var clip=a.runtimeAnimatorController.animationClips.First(c=>c.name==name);float lowest=float.PositiveInfinity;int frames=Mathf.CeilToInt(clip.length*120);for(int f=0;f<=frames;f++){clip.SampleAnimation(a.gameObject,clip.length*f/Mathf.Max(1,frames));lowest=Mathf.Min(lowest,shoes.Min(r=>CharacterFitDiagnostic.Bounds(r).min.y));}Require(lowest>=floor-.012f,"Semelles sous le sol : "+root.name+" "+name+" "+(lowest-floor));report.AppendLine("PASS semelles "+root.name+" "+name+" marge="+(lowest-floor).ToString("F4"));}
                a.enabled=true;a.Rebind();a.SetBool("Grounded",true);a.SetFloat("GroundSpeed",0);a.SetInteger("Action",0);a.Update(.01f);p.Grounded=true;p.GetComponent<ActionManager>().Action=0;p.GetComponent<CharacterGroundContact>().Apply();Require(shoes.Min(r=>CharacterFitDiagnostic.Bounds(r).min.y)>=floor-.012f,"Appui conserve apres Rebind");report.AppendLine("PASS appui pendant la transition du controleur "+root.name);UnityEngine.Object.DestroyImmediate(root);
            }
            var menuGO=new GameObject("Character menu verification");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(menuGO,scene);var menu=menuGO.AddComponent<SonicXFrontMenu>();menu.Show(SonicXFrontMenu.Page.Characters);Require(menu.GetComponentsInChildren<Button>().Any(b=>b.name=="Sonic Classique"),"Classique present dans Histoire");
        }finally{EditorSceneManager.ClosePreviewScene(scene);}
        SoniaVerify.Run();ClassicSonicVerify.Run();report.AppendLine("PASS animations, cheveux solidaires du visage, textures, boules et degats.");
        CharacterFitDiagnostic.Run();report.AppendLine("SUCCESS");EditorPrefs.SetBool(Application.dataPath+Version,true);
    }catch(Exception e){report.AppendLine("FAIL "+e);Debug.LogException(e);}finally{File.WriteAllText(Path.Combine(Output,"corrections.txt"),report.ToString());}}
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
}


