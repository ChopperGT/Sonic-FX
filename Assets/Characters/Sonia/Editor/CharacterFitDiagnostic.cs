using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class CharacterFitDiagnostic
{
    public static readonly string Output=Path.Combine(Path.GetTempPath(),"SonicFXCharacterFit");
    [InitializeOnLoadMethod] static void Pending(){EditorApplication.update+=Ready;}
    static void Ready(){string flag=Path.Combine(Output,"pending.txt");if(!File.Exists(flag)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;File.Delete(flag);Run();}
    [MenuItem("Sonic FX/Personnages/Verifier appui et cheveux")]
    public static Bounds Bounds(SkinnedMeshRenderer r){var mesh=r.sharedMesh;var matrices=r.bones.Select((b,i)=>(b?b.localToWorldMatrix:r.transform.localToWorldMatrix)*mesh.bindposes[i]).ToArray();var verts=mesh.vertices;var weights=mesh.boneWeights;var bounds=new Bounds();for(int i=0;i<verts.Length;i++){var w=weights[i];var v=verts[i];var p=matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;if(i==0)bounds=new Bounds(p,Vector3.zero);else bounds.Encapsulate(p);}return bounds;}
    [MenuItem("Sonic FX/Personnages/Verifier appui et cheveux")] public static void Run(){Directory.CreateDirectory(Output);var scene=EditorSceneManager.NewPreviewScene();var report=new StringBuilder();try{foreach(var path in new[]{"Assets/Characters/Sonia/Sonia.prefab","Assets/Characters/ClassicSonic/Resources/SonicClassique.prefab","Assets/BumperEngineV1/PlayerPrefabs/PO_Mania.prefab"}){
        var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),scene);var p=root.GetComponentInChildren<PlayerBhysics>(true);var a=p.GetComponent<ActionManager>().Action00.CharacterAnimator;a.enabled=false;report.AppendLine(path+" player="+p.transform.position+" animator="+a.transform.position+" capsule="+p.CollisionCapsule.bounds+" sphere="+p.CollisionSphere.bounds);
        var skins=a.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.sharedMesh&&r!=p.GetComponent<ActionManager>().Action03.SpinDashBall).ToArray();foreach(var name in new[]{"Idle","Run","MachRun"}){var clip=a.runtimeAnimatorController.animationClips.FirstOrDefault(c=>c.name==name);if(!clip)continue;clip.SampleAnimation(a.gameObject,clip.length*.3f);foreach(var r in skins)report.AppendLine(name+" "+r.name+" mesh="+r.sharedMesh.name+" bounds="+Bounds(r));if(path.Contains("Characters"))Render(a.gameObject,skins,path.Contains("Sonia")?"Sonia-"+name:"Classic-"+name);}
        UnityEngine.Object.DestroyImmediate(root);
    }}catch(Exception e){report.AppendLine(e.ToString());Debug.LogException(e);}finally{EditorSceneManager.ClosePreviewScene(scene);File.WriteAllText(Path.Combine(Output,"diagnostic.txt"),report.ToString());}}
    static void Render(GameObject go,SkinnedMeshRenderer[] skins,string name){var preview=new PreviewRenderUtility();try{var clone=UnityEngine.Object.Instantiate(go);foreach(var a in clone.GetComponentsInChildren<Animator>(true))a.enabled=false;clone.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);preview.AddSingleGO(clone);var fittedSkins=clone.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.sharedMesh&&r.bones.All(b=>b)&&r.enabled).ToArray();var bounds=Bounds(fittedSkins[0]);foreach(var r in fittedSkins.Skip(1))bounds.Encapsulate(Bounds(r));for(int side=0;side<2;side++){preview.camera.transform.position=bounds.center+(side==0?new Vector3(.3f,.2f,3):new Vector3(3,.2f,0))*bounds.size.y;preview.camera.transform.LookAt(bounds.center);preview.camera.fieldOfView=28;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=1000;preview.camera.backgroundColor=new Color(.15f,.2f,.28f);preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.lights[0].intensity=1.3f;preview.lights[0].transform.rotation=Quaternion.Euler(35,-30,0);preview.lights[1].intensity=.7f;preview.lights[1].transform.rotation=Quaternion.Euler(0,140,0);preview.ambientColor=new Color(.55f,.55f,.55f);preview.BeginStaticPreview(new Rect(0,0,640,640));preview.Render();var tex=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(Output,name+"-"+side+".png"),tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);}}finally{preview.Cleanup();}}
}



