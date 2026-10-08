using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using SonicFX.Menu;

public static class ClassicSonicVerify
{
    const string Folder="Assets/Characters/ClassicSonic";
    static string Out => Path.Combine(Path.GetTempPath(),"SonicFXClassicSonic") + Path.DirectorySeparatorChar;
    [MenuItem("Sonic FX/Personnages/Verifier Sonic Classique")]
    public static void Run(){if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("Sonic Classique : quittez Play pour verifier.");return;}Directory.CreateDirectory(Out);var report=new StringBuilder();var scene=EditorSceneManager.NewPreviewScene();try{
        var modern=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BumperEngineV1/PlayerPrefabs/PO_ModernSonic.prefab").GetComponentInChildren<SonicSoundsControl>(true);
        foreach(bool free in new[]{false,true}){
            string path=Folder+(free?"/SonicClassiqueFree.prefab":"/Resources/SonicClassique.prefab");var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Require(prefab,"Prefab "+path);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);var player=go.GetComponentInChildren<PlayerBhysics>(true);var action=player.GetComponent<ActionManager>();var animator=action.Action00.CharacterAnimator;
            var face=animator.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Head_Neck");var faceRest=face.GetComponentsInChildren<Transform>(true).Where(t=>t!=face).Select(t=>new FacePose(t)).ToArray();var faceRotation=face.rotation;
            var original=AssetDatabase.LoadAssetAtPath<GameObject>(free?"Assets/BumperEngineV1/PlayerPrefabs/SonicManiaFree.prefab":"Assets/BumperEngineV1/PlayerPrefabs/PO_Mania.prefab").GetComponentInChildren<PlayerBhysics>(true);
            Require(player.TopSpeed==original.TopSpeed && player.MaxSpeed==original.MaxSpeed,"Reglages Mania conserves");Require(action.AvailableAbilities==original.GetComponent<ActionManager>().AvailableAbilities,"Capacites Mania conservees");
            var sounds=go.GetComponentInChildren<SonicSoundsControl>(true);Require(sounds.CombatVoiceClips.SequenceEqual(modern.CombatVoiceClips)&&sounds.JumpingVoiceClips.SequenceEqual(modern.JumpingVoiceClips)&&sounds.PainVoiceClips.SequenceEqual(modern.PainVoiceClips),"Voix Moderne identiques");Require(sounds.Source4 && sounds.Source4.transform.IsChildOf(go.transform),"Source des voix interne au prefab");
            Require(animator.avatar && animator.avatar.isValid,"Avatar valide");Require(animator.transform.Find("ClassicRig").GetComponentsInChildren<SkinnedMeshRenderer>(true).Length==10,"10 maillages Classic, ancien modele retire");
            VerifyBallAndDamage(go,original,report);
            VerifyOriginalAppearance(animator.gameObject,report);
            var clips=animator.runtimeAnimatorController.animationClips.Distinct().ToArray();Require(clips.Length>=15,"Toutes les animations du controleur Moderne");
            foreach(var evt in clips.SelectMany(AnimationUtility.GetAnimationEvents))Require(animator.GetComponents<MonoBehaviour>().Any(b=>b&&b.GetType().GetMethod(evt.functionName,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!=null),"Recepteur evenement "+evt.functionName);
            Require(animator.GetComponentsInChildren<ParticleSystemRenderer>(true).Length==original.GetComponent<Action00_Regular>().CharacterAnimator.GetComponentsInChildren<ParticleSystemRenderer>(true).Length,"Effets de Mania conserves");
            report.AppendLine("REST "+Bounds(animator.gameObject)+" scale="+animator.transform.lossyScale+" rig="+animator.transform.Find("ClassicRig").localScale);
            var idle=clips.First(c=>c.name=="Idle");idle.SampleAnimation(animator.gameObject,0);Render(animator.gameObject,Out+"Idle-diagnostic.png");
            var library=AssetDatabase.FindAssets("t:AnimationClip",new[]{Folder+"/Animations"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".anim")).Select(AssetDatabase.LoadAssetAtPath<AnimationClip>).Distinct().ToArray();
            foreach(var clip in library){
                foreach(var binding in AnimationUtility.GetCurveBindings(clip))Require(!faceRest.Any(p=>AnimationUtility.CalculateTransformPath(p.transform,animator.transform)==binding.path),"Aucune animation directe des os du visage : "+clip.name);
                int frames=Mathf.Max(1,Mathf.CeilToInt(clip.length*30));for(int frame=0;frame<=frames;frame++){clip.SampleAnimation(animator.gameObject,clip.length*frame/frames);foreach(var pose in faceRest)pose.Check(clip.name+" frame "+frame);}
                foreach(var binding in AnimationUtility.GetCurveBindings(clip))if(binding.type==typeof(Transform))Require(animator.transform.Find(binding.path),"Chemin animation "+binding.path);foreach(float time in new[]{0,.25f,.5f,.75f,1}){clip.SampleAnimation(animator.gameObject,clip.length*time);var bound=Bounds(animator.gameObject);report.AppendLine(clip.name+" "+time+" "+bound);Require(!float.IsNaN(bound.size.sqrMagnitude) && bound.size.magnitude<12,"Maillage stable "+clip.name+" "+time+" bounds="+bound);}}
            foreach(var name in new[]{"Idle","Run","MachRun","Rise","Damage","Roll","Rail_Med[i]"}){var clip=clips.FirstOrDefault(c=>c.name==name);if(clip){clip.SampleAnimation(animator.gameObject,clip.length*.3f);Render(animator.gameObject,Out+name+".png");RenderFace(animator.gameObject,Out+name+"-face.png",faceRotation);}}
            idle.SampleAnimation(animator.gameObject,0);
            var head=animator.GetComponentsInChildren<Transform>(true).First(t=>t.name=="HeadTop_Neck");var foot=animator.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Foot_L_Neck");Require(head.position.y>foot.position.y+.5f,"Pose Idle debout");
            var body=animator.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.sharedMaterial.mainTexture!=null);Require(body.sharedMaterial.mainTexture!=null,"Texture couleur conservee");
            animator.enabled=true;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();animator.SetBool("Grounded",true);animator.SetInteger("Action",0);animator.SetFloat("GroundSpeed",0);animator.SetFloat("NormalSpeed",20);animator.Update(.2f);
            Require(head.position.y>foot.position.y+.5f,"Controleur en Idle debout");Require(animator.transform.Find("ClassicRig").localScale.x<1,"Avatar conserve la taille ajustee");
            animator.SetFloat("GroundSpeed",65);for(int step=0;step<30;step++)animator.Update(1f/30);report.AppendLine("CONTROLLER "+animator.GetCurrentAnimatorStateInfo(0).fullPathHash+" time="+animator.GetCurrentAnimatorStateInfo(0).normalizedTime+" clips="+string.Join(",",animator.GetCurrentAnimatorClipInfo(0).Select(c=>c.clip.name+":"+c.weight)));foreach(var pose in faceRest)pose.Check("Controleur course");
            animator.SetFloat("GroundSpeed",30);for(int step=0;step<30;step++){animator.Update(1f/60);foreach(var pose in faceRest)pose.Check("Transition course");}
            var q=foot.localRotation;var position=foot.position;animator.Update(.1f);report.AppendLine("FOOT angle="+Quaternion.Angle(q,foot.localRotation)+" distance="+Vector3.Distance(position,foot.position));Require(Quaternion.Angle(q,foot.localRotation)>.01f||Vector3.Distance(position,foot.position)>.001f,"Animation de course jouee par le controleur");
            report.AppendLine("PASS face rigide : "+faceRest.Length+" os, "+library.Length+" animations, transitions du controleur.");
            report.AppendLine("PASS "+go.name+" speed="+player.TopSpeed+" max="+player.MaxSpeed+" abilities="+action.AvailableAbilities+" clips="+clips.Length+" voices="+sounds.CombatVoiceClips.Length+"/"+sounds.JumpingVoiceClips.Length+"/"+sounds.PainVoiceClips.Length);
            UnityEngine.Object.DestroyImmediate(go);
            var spawn=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BumperEngineV1/PlayerPrefabs/PO_Mania.prefab"),scene);var before=spawn.GetComponentInChildren<PlayerBhysics>();var start=before.transform.position;var nextLevel=before.GetComponent<LevelProgressControl>().NextLevelScene;
            var install=typeof(SonicNewLevelSession).GetMethod("InstallCharacter",BindingFlags.Static|BindingFlags.NonPublic);var installed=(PlayerBhysics)install.Invoke(null,new object[]{scene,new SonicNewLevelCharacter{id=free?"classicsonicfree":"classicsonic",displayName="Classic verification",prefab=prefab},free});
            Require(installed&&Vector3.Distance(installed.transform.position,start)<.001f,"Point de depart conserve");Require(installed.TopSpeed==original.TopSpeed&&installed.MaxSpeed==original.MaxSpeed,"Vitesses conservees apres installation");Require(installed.GetComponent<ActionManager>().AvailableAbilities==original.GetComponent<ActionManager>().AvailableAbilities,"Capacites conservees apres installation");Require(installed.GetComponent<LevelProgressControl>().NextLevelScene==nextLevel,"Parcours de la map conserve");var cam=installed.GetComponent<CameraControl>().Cam;Require(cam&&cam.transform.IsChildOf(installed.transform.root),"Camera interne a la copie");Require(scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PlayerBhysics>()).Count()==1,"Un seul joueur apres remplacement");report.AppendLine("PASS installation "+(free?"Free":"Histoire")+", camera et depart.");UnityEngine.Object.DestroyImmediate(installed.transform.root.gameObject);
        }
        Require(SonicXProgress.TryParse("{\"version\":5,\"character\":\"classicsonic\",\"scene\":\"test\",\"lives\":3}",out var save)&&save.character=="classicsonic","Sauvegarde Classic reconnue");
        var catalog=SonicNewLevelCatalog.Load();Require(catalog.Find("classicsonic")?.prefab&&catalog.Find("classicsonicfree")?.prefab,"Deux personnages dans New Level");
        Require(SonicStoryRoute.TryResolve("classicsonic",SonicXProgress.FirstLevel,out var next)&&next==SonicStoryRoute.SecondLevel,"Histoire Classic Act1-1 vers1-2");report.AppendLine("PASS sauvegarde, catalogue, parcours histoire.");
    }catch(Exception e){report.AppendLine("FAIL "+e);Debug.LogException(e);}finally{EditorSceneManager.ClosePreviewScene(scene);File.WriteAllText(Out+"verification.txt",report.ToString());}}
    static void VerifyBallAndDamage(GameObject root,PlayerBhysics original,StringBuilder report){
        var player=root.GetComponentInChildren<PlayerBhysics>(true);var actions=player.GetComponent<ActionManager>();var hurt=player.GetComponent<HurtControl>();var appearance=player.GetComponent<ClassicSonicAppearance>();
        Require(appearance&&appearance.ManiaBall&&appearance.BodySkins.Length==10,"Apparence corps/boule complete");
        var originalBall=original.GetComponent<Action03_SpinDash>().SpinDashBall;
        Require(appearance.ManiaBall.sharedMesh==originalBall.sharedMesh&&appearance.ManiaBall.sharedMaterials.SequenceEqual(originalBall.sharedMaterials),"Modele et materiaux boule de Mania identiques");
        Require(!appearance.BodySkins.Contains(appearance.ManiaBall),"Boule distincte du corps");
        Require(actions.Action03.PlayerSkin.SequenceEqual(appearance.BodySkins)&&actions.Action08.PlayerSkin.SequenceEqual(appearance.BodySkins)&&hurt.SonicSkins.SequenceEqual(appearance.BodySkins),"References de toutes les parties du corps");
        var animator=actions.Action00.CharacterAnimator;var idle=animator.runtimeAnimatorController.animationClips.First(c=>c.name=="Idle");idle.SampleAnimation(animator.gameObject,0);
        foreach(int state in new[]{0,1,3,6,8,4,0}){
            actions.Action=state;player.isRolling=false;hurt.IsHurt=false;hurt.IsInvencible=false;actions.Action03.ResetSpinDashVariables();appearance.RefreshVisuals();bool ball=state==1||state==3||state==6||state==8;
            Require(appearance.ManiaBall.enabled==ball&&appearance.ManiaBall.gameObject.activeInHierarchy,"Boule visible dans action "+state);Require(appearance.BodySkins.All(s=>s.enabled==!ball),"Corps entier visible dans action "+state);
            if(state==1)RenderVisible(animator.gameObject,Out+root.name+"-ManiaBall.png");
        }
        player.isRolling=true;appearance.RefreshVisuals();Require(appearance.ManiaBall.enabled&&appearance.BodySkins.All(s=>!s.enabled),"Boule en roulade au sol");
        actions.Action=4;hurt.IsHurt=true;hurt.IsInvencible=true;
        bool sawVisible=false,sawHidden=false;
        for(int frame=0;frame<20;frame++){
            hurt.ToggleSkin(frame%2==0);actions.Action03.ResetSpinDashVariables();appearance.RefreshVisuals(frame*.05f+.01f);Require(!appearance.ManiaBall.enabled,"Boule masquee pendant degats");Require(appearance.BodySkins.All(s=>s.enabled==appearance.BodySkins[0].enabled),"Clignotement synchrone, aucun morceau restant seul");sawVisible|=appearance.BodySkins[0].enabled;sawHidden|=!appearance.BodySkins[0].enabled;
        }
        Require(sawVisible&&sawHidden,"Les deux phases du clignotement sont verifiees");
        actions.Action=1;appearance.RefreshVisuals(.01f);Require(appearance.ManiaBall.enabled&&appearance.BodySkins.All(s=>!s.enabled),"Le saut en boule reste possible apres le recul, pendant l'invincibilite");actions.Action=4;
        hurt.IsInvencible=false;appearance.RefreshVisuals();Require(appearance.BodySkins.All(s=>s.enabled),"Modele entier restaure apres invincibilite");
        var damage=animator.runtimeAnimatorController.animationClips.First(c=>c.name=="Damage");damage.SampleAnimation(animator.gameObject,damage.length*.3f);RenderVisible(animator.gameObject,Out+root.name+"-DamageVisible.png");
        actions.Action=0;hurt.IsHurt=false;player.isRolling=false;appearance.RefreshVisuals();
        report.AppendLine("PASS boule Mania (saut, roulade, charge, rebond, dropdash), degats, clignotement et retour du modele complet.");
    }
    static void RenderVisible(GameObject model,string path){
        var preview=new PreviewRenderUtility();try{
            var clone=UnityEngine.Object.Instantiate(model);clone.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);foreach(var a in clone.GetComponentsInChildren<Animator>(true))a.enabled=false;preview.AddSingleGO(clone);
            var skins=clone.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s=>s.enabled&&s.gameObject.activeInHierarchy).ToArray();Require(skins.Length>0,"Modele visible pour apercu");var bounds=skins[0].bounds;foreach(var s in skins.Skip(1))bounds.Encapsulate(s.bounds);
            preview.camera.transform.position=bounds.center+new Vector3(1.7f,.8f,3)*Mathf.Max(1,bounds.size.y);preview.camera.transform.LookAt(bounds.center);preview.camera.fieldOfView=30;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=100;preview.camera.backgroundColor=new Color(.15f,.2f,.28f);preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.lights[0].intensity=1.3f;preview.lights[0].transform.rotation=Quaternion.Euler(35,-30,0);preview.lights[1].intensity=.7f;preview.ambientColor=new Color(.55f,.55f,.55f);preview.BeginStaticPreview(new Rect(0,0,640,640));preview.Render();var texture=preview.EndStaticPreview();File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
        }finally{preview.Cleanup();}
    }
    static void VerifyOriginalAppearance(GameObject model,StringBuilder report){
        var original=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/classic_sonic.glb");
        var originalBones=original.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
        var face=model.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Head_Neck");
        foreach(var bone in face.GetComponentsInChildren<Transform>(true).Where(t=>t!=face)){
            Require(originalBones.TryGetValue(bone.name,out var source),"Os du visage original present");
            Require(Vector3.Distance(bone.localPosition,source.localPosition)<.0001f&&Quaternion.Angle(bone.localRotation,source.localRotation)<.05f&&Vector3.Distance(bone.localScale,source.localScale)<.0001f,"Forme originale du visage : "+bone.name);
        }
        var sourceSkins=original.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach(var skin in model.transform.Find("ClassicRig").GetComponentsInChildren<SkinnedMeshRenderer>(true)){
            var source=sourceSkins.FirstOrDefault(r=>r.sharedMesh==skin.sharedMesh);Require(source,"Maillage et UV originaux conserves : "+skin.name);
            Require(source.sharedMaterials.Length==skin.sharedMaterials.Length,"Nombre materiaux conserve");
            for(int i=0;i<source.sharedMaterials.Length;i++){
                var material=source.sharedMaterials[i];var converted=skin.sharedMaterials[i];var property=material.HasProperty("baseColorTexture")?"baseColorTexture":material.HasProperty("_BaseColorTexture")?"_BaseColorTexture":"_MainTex";
                if(material.HasProperty(property)&&material.GetTexture(property)){
                    Require(converted.mainTexture==material.GetTexture(property),"Texture originale conservee");
                    Require(converted.mainTextureScale==material.GetTextureScale(property)&&converted.mainTextureOffset==material.GetTextureOffset(property),"Placement UV conserve : "+skin.name);
                }
            }
        }
        report.AppendLine("PASS geometrie du visage, maillages, UV et textures identiques au GLB original.");
    }
    sealed class FacePose {
        public readonly Transform transform;readonly Vector3 position,scale;readonly Quaternion rotation;
        public FacePose(Transform t){transform=t;position=t.localPosition;rotation=t.localRotation;scale=t.localScale;}
        public void Check(string context){Require(Vector3.Distance(position,transform.localPosition)<.0001f&&Quaternion.Angle(rotation,transform.localRotation)<.05f&&Vector3.Distance(scale,transform.localScale)<.0001f,"Visage deforme : "+transform.name+" "+context);}
    }
    static void RenderFace(GameObject model,string path,Quaternion neutralRotation){
        var preview=new PreviewRenderUtility();try{
            var clone=UnityEngine.Object.Instantiate(model);clone.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);foreach(var a in clone.GetComponentsInChildren<Animator>(true))a.enabled=false;preview.AddSingleGO(clone);
            var head=clone.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Head_Neck");var delta=head.rotation*Quaternion.Inverse(neutralRotation);var center=head.position+delta*new Vector3(0,.25f,.05f);
            preview.camera.transform.position=center+delta*new Vector3(.9f,.3f,2.5f);preview.camera.transform.LookAt(center,delta*Vector3.up);preview.camera.fieldOfView=35;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=100;preview.camera.backgroundColor=new Color(.15f,.2f,.28f);preview.camera.clearFlags=CameraClearFlags.SolidColor;
            preview.lights[0].intensity=1.3f;preview.lights[0].transform.rotation=delta*Quaternion.Euler(35,-30,0);preview.lights[1].intensity=.7f;preview.lights[1].transform.rotation=delta*Quaternion.Euler(0,140,0);preview.ambientColor=new Color(.55f,.55f,.55f);preview.BeginStaticPreview(new Rect(0,0,640,640));preview.Render();var texture=preview.EndStaticPreview();File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
        }finally{preview.Cleanup();}
    }
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static Bounds Bounds(GameObject go){var bounds=new Bounds();bool first=true;foreach(var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)){var mesh=r.sharedMesh;var matrices=r.bones.Select((b,i)=>b.localToWorldMatrix*mesh.bindposes[i]).ToArray();var verts=mesh.vertices;var weights=mesh.boneWeights;for(int i=0;i<verts.Length;i++){var w=weights[i];var v=verts[i];var p=matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}}return bounds;}
    static void Render(GameObject model,string path){var preview=new PreviewRenderUtility();GameObject clone=null;try{clone=UnityEngine.Object.Instantiate(model);clone.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);foreach(var animator in clone.GetComponentsInChildren<Animator>(true))animator.enabled=false;preview.AddSingleGO(clone);var bounds=Bounds(clone);preview.camera.transform.position=bounds.center+new Vector3(1.7f,.8f,3)*Mathf.Max(1,bounds.size.y);preview.camera.transform.LookAt(bounds.center);preview.camera.fieldOfView=30;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=100;preview.camera.backgroundColor=new Color(.15f,.2f,.28f);preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.lights[0].intensity=1.3f;preview.lights[0].transform.rotation=Quaternion.Euler(35,-30,0);preview.lights[1].intensity=.7f;preview.lights[1].transform.rotation=Quaternion.Euler(0,140,0);preview.ambientColor=new Color(.55f,.55f,.55f);preview.BeginStaticPreview(new Rect(0,0,640,640));preview.Render();var texture=preview.EndStaticPreview();File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);}finally{preview.Cleanup();}}
}








