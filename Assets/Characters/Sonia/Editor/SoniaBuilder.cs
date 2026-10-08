using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using SonicFX.Menu;

public static class SoniaBuilder
{
    const string Folder="Assets/Characters/Sonia";
    static string Out => Path.Combine(Path.GetTempPath(),"SonicFXSonia") + Path.DirectorySeparatorChar;
    [InitializeOnLoadMethod]
    static void PendingVerification()
    {
        string pending=Path.Combine(Path.GetTempPath(),"SonicFXSonia","pending.txt");
        if(!File.Exists(pending))return;
        EditorApplication.delayCall+=()=>{if(EditorApplication.isPlayingOrWillChangePlaymode)return;File.Delete(pending);BuildAndVerify();};
    }
    [MenuItem("Sonic FX/Personnages/Reconstruire Sonia")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null){Debug.LogWarning("Sonia : quittez Play et le mode Prefab avant de reconstruire.");return;}
        Directory.CreateDirectory(Out);Directory.CreateDirectory(Folder+"/Animations");Directory.CreateDirectory(Folder+"/Resources");Directory.CreateDirectory(Folder+"/Materials");AssetDatabase.Refresh();
        var scene=EditorSceneManager.NewPreviewScene();GameObject source=null,model=null;
        try{
            source=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BumperEngineV1/PlayerPrefabs/PO_ModernSonic.prefab"),scene);PrefabUtility.UnpackPrefabInstance(source,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            var sourceAnimator=source.GetComponentInChildren<Action00_Regular>(true).CharacterAnimator;
            var driver=sourceAnimator.gameObject;driver.transform.SetParent(null);driver.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);driver.transform.localScale=Vector3.one;sourceAnimator.enabled=false;
            var main=driver.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.bones.Any(b=>b!=null&&b.name=="Hips"));
            var src=driver.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
            // The two pack rigs share their bone axes. Mania preserves the neutral
            // T-pose; its rotations avoid the reflected FBX bind-matrix ambiguity.
            var calibration=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BumperEngineV1/PlayerPrefabs/PO_Mania.prefab"),scene);
            var calibrationSkin=calibration.GetComponentInChildren<Action00_Regular>(true).CharacterAnimator.transform;
            foreach(var bone in calibrationSkin.GetComponentsInChildren<Transform>(true))if(src.TryGetValue(bone.name,out var sourceBone))sourceBone.rotation=Quaternion.Inverse(calibrationSkin.rotation)*bone.rotation;
            UnityEngine.Object.DestroyImmediate(calibration);
            model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/sonia_the_hedgehog.glb"),scene);PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);model.name="SoniaRig";
            foreach(var light in model.GetComponentsInChildren<Light>(true))UnityEngine.Object.DestroyImmediate(light);
            foreach(var a in model.GetComponentsInChildren<Animator>(true))UnityEngine.Object.DestroyImmediate(a);
            // Keep imported UVs, textures and alpha; use the game's built-in Standard shader.
            var materials=new Dictionary<Material,Material>();foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>{
                    if(materials.TryGetValue(m,out var existing))return existing;
                    var n=new Material(Shader.Find("Standard")){name="Sonia_"+m.name};
                    var color=m.HasProperty("baseColorFactor")?m.GetColor("baseColorFactor"):m.HasProperty("_BaseColor")?m.GetColor("_BaseColor"):m.HasProperty("_Color")?m.GetColor("_Color"):Color.white;
                    var tex=m.HasProperty("baseColorTexture")?m.GetTexture("baseColorTexture"):m.HasProperty("_BaseColorTexture")?m.GetTexture("_BaseColorTexture"):m.HasProperty("_MainTex")?m.GetTexture("_MainTex"):null;
                    n.color=color;n.mainTexture=tex;
                    string textureProperty=m.HasProperty("baseColorTexture")?"baseColorTexture":m.HasProperty("_BaseColorTexture")?"_BaseColorTexture":"_MainTex";
                    if(tex){n.mainTextureScale=m.GetTextureScale(textureProperty);n.mainTextureOffset=m.GetTextureOffset(textureProperty);}
                    // The supplied eye UVs are mirrored vertically and place the iris below the muzzle.
                    if(m.name=="Material.024"){n.mainTextureScale=new Vector2(1,-1);n.mainTextureOffset=new Vector2(0,.75f);}n.SetFloat("_Glossiness",.23f);n.SetFloat("_Metallic",.05f);
                    if(m.renderQueue>=3000){n.SetFloat("_Mode",3);n.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.One);n.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);n.SetInt("_ZWrite",0);n.EnableKeyword("_ALPHAPREMULTIPLY_ON");n.renderQueue=3000;}
                    var path=Folder+"/Materials/"+materials.Count+".mat";n.name=materials.Count.ToString();n=SaveAsset(n,path);materials[m]=n;return n;
                }).ToArray();renderer.updateWhenOffscreen=true;
            }
            var classic=model.GetComponentsInChildren<Transform>(true);var target=classic.GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
            var records=new List<Mapping>();foreach(var t in classic){var sourceName=SourceName(t.name);if(sourceName!=null && src.TryGetValue(sourceName,out var s))records.Add(new Mapping(t,s));}
            var srcRest=driver.GetComponentsInChildren<Transform>(true).Select(t=>new PoseState(t)).ToArray();var dstRest=classic.Select(t=>new PoseState(t)).ToArray();
            var sourceContainerRest=new PoseState(src["chr_Sonic 1"]);
            var animated=new GameObject("SoniaAnimator");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(animated,scene);model.transform.SetParent(animated.transform,false);
            var controller=new AnimatorOverrideController(sourceAnimator.runtimeAnimatorController){name="Sonia"};
            var pairs=new List<KeyValuePair<AnimationClip,AnimationClip>>();controller.GetOverrides(pairs);
            float sourceHeight=main.bounds.size.y;float targetHeight=MeshBounds(model).size.y;float rootRatio=targetHeight/Mathf.Max(.01f,sourceHeight);
            var library=AssetDatabase.FindAssets("t:AnimationClip",new[]{"Assets/BumperEngineV1/Models/PlayerModels/ModernSonic"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".anim")).Select(AssetDatabase.LoadAssetAtPath<AnimationClip>).Where(c=>c!=null);
            var allClips=pairs.Select(p=>p.Key).Concat(library).Distinct().ToArray();
            foreach(var clip in allClips){
                var sampleRoot=driver;var clipRecords=records;var clipRest=srcRest;float clipRatio=rootRatio;GameObject alternate=null;
                if(!AnimationUtility.GetCurveBindings(clip).Any(b=>b.path.StartsWith("chr_Sonic 1/Reference/Hips",StringComparison.Ordinal))){
                    var asset=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(clip));
                    alternate=asset?(GameObject)PrefabUtility.InstantiatePrefab(asset,scene):UnityEngine.Object.Instantiate(driver);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(alternate,scene);if(PrefabUtility.IsPartOfPrefabInstance(alternate))PrefabUtility.UnpackPrefabInstance(alternate,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);sampleRoot=alternate;
                    foreach(var a in alternate.GetComponentsInChildren<Animator>(true))a.enabled=false;
                    var alternateSkin=alternate.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.bones.Any(b=>b&&b.name=="hips"));
                    if(alternateSkin.sharedMesh)for(int bone=0;bone<alternateSkin.bones.Length;bone++){var t=alternateSkin.bones[bone];if(!t)continue;var matrix=alternateSkin.transform.localToWorldMatrix*alternateSkin.sharedMesh.bindposes[bone].inverse;t.SetPositionAndRotation(matrix.GetColumn(3),matrix.rotation);}
                    var alternateBones=alternate.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());foreach(var p in dstRest)p.Restore();
                    clipRecords=records.Where(m=>alternateBones.ContainsKey(AlternateName(m.source.name))).Select(m=>new Mapping(m.target,alternateBones[AlternateName(m.source.name)])).ToList();
                    if(!clipRecords.Any(m=>m.target.name=="HipsTranslation_Amy_skeleton"))throw new Exception("Bassin source absent pour "+clip.name);
                    foreach(var m in clipRecords){var end=DirectionEnd(m.source.name);if(end!=null&&alternateBones.TryGetValue(AlternateName(end),out var endpoint)){m.sourceEnd=endpoint;var targetEnd=TargetDirectionEnd(m.target.name);if(targetEnd!=null&&target.TryGetValue(targetEnd,out var t))m.targetDirection=t.position-m.target.position;}}
                    clipRest=alternate.GetComponentsInChildren<Transform>(true).Select(t=>new PoseState(t)).ToArray();clipRatio=targetHeight/Mathf.Max(.01f,Vector3.Distance(alternateBones["head"].position,alternateBones["foot_L"].position));
                }
                var baked=new AnimationClip{name=clip.name,frameRate=30,wrapMode=clip.wrapMode};var curves=new Dictionary<Transform,AnimationCurve[]>();foreach(var map in clipRecords)curves[map.target]=Enumerable.Range(0,7).Select(_=>new AnimationCurve()).ToArray();
                int count=Mathf.Max(1,Mathf.CeilToInt(clip.length*30));
                for(int frame=0;frame<=count;frame++){
                    float time=clip.length*frame/count;foreach(var p in clipRest)p.Restore();foreach(var p in dstRest)p.Restore();
                    clip.SampleAnimation(sampleRoot,time);
                    // Ignore the FBX container's fixed Z-up conversion. The pack
                    // avatar handles it at runtime; SampleAnimation applies it directly.
                    if(!alternate)sourceContainerRest.Restore();
                    foreach(var map in clipRecords){
                        map.target.rotation=alternate&&map.sourceEnd&&map.targetDirection.sqrMagnitude>.0001f?Quaternion.FromToRotation(map.targetDirection,map.sourceEnd.position-map.source.position)*map.targetRotation:map.source.rotation*Quaternion.Inverse(map.sourceRotation)*map.targetRotation;
                        if(map.target.name=="HipsTranslation_Amy_skeleton")map.target.position=map.targetPosition+(map.source.position-map.sourcePosition)*clipRatio;
                    }
                    foreach(var map in clipRecords){var t=map.target;var q=t.localRotation;var c=curves[t];if(frame>0){var prev=new Quaternion(c[3].keys[frame-1].value,c[4].keys[frame-1].value,c[5].keys[frame-1].value,c[6].keys[frame-1].value);if(Quaternion.Dot(prev,q)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);}float[] v={t.localPosition.x,t.localPosition.y,t.localPosition.z,q.x,q.y,q.z,q.w};for(int k=0;k<7;k++)c[k].AddKey(time,v[k]);}
                }
                foreach(var item in curves){string path=AnimationUtility.CalculateTransformPath(item.Key,animated.transform);string[] properties={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"};for(int k=0;k<7;k++){var c=Simplify(item.Value[k],.0001f);for(int key=0;key<c.length;key++){AnimationUtility.SetKeyLeftTangentMode(c,key,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(c,key,AnimationUtility.TangentMode.Linear);}AnimationUtility.SetEditorCurve(baked,EditorCurveBinding.FloatCurve(path,typeof(Transform),properties[k]),c);}}
                AnimationUtility.SetAnimationClipSettings(baked,AnimationUtility.GetAnimationClipSettings(clip));AnimationUtility.SetAnimationEvents(baked,AnimationUtility.GetAnimationEvents(clip).Where(e=>e.functionName!="CombatVoicePlay"&&e.functionName!="JumpingVoicePlay"&&e.functionName!="PainVoicePlay").ToArray());baked.EnsureQuaternionContinuity();
                baked=SaveAsset(baked,Folder+"/Animations/"+Safe(clip.name)+".anim");for(int i=0;i<pairs.Count;i++)if(pairs[i].Key==clip)pairs[i]=new KeyValuePair<AnimationClip,AnimationClip>(clip,baked);
                if(alternate)UnityEngine.Object.DestroyImmediate(alternate);
            }
            controller.ApplyOverrides(pairs);controller=SaveAsset(controller,Folder+"/Animations/Sonia.overrideController");foreach(var p in dstRest)p.Restore();
            var avatar=AvatarBuilder.BuildGenericAvatar(animated,"");avatar.name="SoniaAvatar";avatar=SaveAsset(avatar,Folder+"/Animations/SoniaAvatar.asset");
            BuildPlayer(model,controller,scene);
            RegisterMenu();AssetDatabase.SaveAssets();
            UnityEngine.Object.DestroyImmediate(animated);UnityEngine.Object.DestroyImmediate(driver);
            File.WriteAllText(Out+"build.txt","Built "+records.Count+" mapped bones, "+pairs.Count+" animation states, "+allClips.Length+" animation clips.\n"+DateTime.UtcNow.ToString("O"));
        }finally{if(model)UnityEngine.Object.DestroyImmediate(model);if(source)UnityEngine.Object.DestroyImmediate(source);EditorSceneManager.ClosePreviewScene(scene);}
    }
    public static void BuildAndVerify(){Build();SoniaVerify.Run();}
    static void BuildPlayer(GameObject model,RuntimeAnimatorController controller,UnityEngine.SceneManagement.Scene scene)
    {
        var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BumperEngineV1/PlayerPrefabs/PO_ModernSonic.prefab"),scene);PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);root.name="Sonia";
        var action=root.GetComponentInChildren<Action00_Regular>(true);var animator=action.CharacterAnimator;var player=action.GetComponent<PlayerBhysics>();var actions=player.GetComponent<ActionManager>();actions.RestorePackAbilities();
        var spin=actions.Action03;var ball=spin.SpinDashBall;var oldSkins=animator.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r!=ball).ToArray();var oldBounds=MeshBounds(animator.gameObject);foreach(var r in oldSkins)r.enabled=false;
        var clone=UnityEngine.Object.Instantiate(model,animator.transform);clone.name="SoniaRig";clone.transform.localPosition=Vector3.zero;clone.transform.localRotation=model.transform.localRotation;clone.transform.localScale=model.transform.localScale;
        var bounds=MeshBounds(clone);clone.transform.localScale*=oldBounds.size.y/bounds.size.y;bounds=MeshBounds(clone);clone.transform.position+=new Vector3(oldBounds.center.x-bounds.center.x,oldBounds.min.y-bounds.min.y,oldBounds.center.z-bounds.center.z);
        animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
        var skins=clone.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        // The imported forelock sits above the scalp. Keep its original shape and
        // head parenting, but seat its root on the head before fitting animations.
        var hair=clone.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Hair1_Amy_skeleton");
        hair.position-=animator.transform.up*.40f;
        var samplingAvatar=AvatarBuilder.BuildGenericAvatar(animator.gameObject,"");animator.avatar=samplingAvatar;
        CharacterGroundFit.Apply(player,animator,clone.transform,"HipsTranslation_Amy_skeleton");
        var fitted=AvatarBuilder.BuildGenericAvatar(animator.gameObject,"");fitted.name="SoniaAvatar";animator.avatar=SaveAsset(fitted,Folder+"/Animations/SoniaAvatar.asset");UnityEngine.Object.DestroyImmediate(samplingAvatar);
        // Body arrays must contain all meshes. Keep the pack's ball and effects.
        foreach(var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true)){if(!behaviour)continue;var so=new SerializedObject(behaviour);var prop=so.GetIterator();while(prop.Next(true))if(prop.propertyType==SerializedPropertyType.ObjectReference&&prop.objectReferenceValue is SkinnedMeshRenderer r&&oldSkins.Contains(r))prop.objectReferenceValue=skins[0];so.ApplyModifiedPropertiesWithoutUndo();}
        ball=InstallAmyBall(root,spin,scene);spin.PlayerSkin=skins;spin.SpinDashBall=ball;ball.enabled=false;
        foreach(var drop in root.GetComponentsInChildren<Action08_DropDash>(true))drop.PlayerSkin=skins;
        foreach(var hurt in root.GetComponentsInChildren<HurtControl>(true))hurt.SonicSkins=skins;
        var appearance=player.gameObject.AddComponent<SoniaAppearance>();appearance.BodySkins=skins;appearance.SpinBall=ball;
        var contact=player.gameObject.AddComponent<CharacterGroundContact>();contact.ModelRoot=clone.transform;contact.Shoes=skins.Where(r=>r.name.StartsWith("Blaze.008_Blaze.004")).ToArray();
        foreach(var sounds in root.GetComponentsInChildren<SonicSoundsControl>(true)){sounds.CombatVoiceClips=Array.Empty<AudioClip>();sounds.JumpingVoiceClips=Array.Empty<AudioClip>();sounds.PainVoiceClips=Array.Empty<AudioClip>();if(sounds.Source4){sounds.Source4.Stop();sounds.Source4.clip=null;sounds.Source4.mute=true;sounds.Source4.playOnAwake=false;}}
        foreach(var r in oldSkins)UnityEngine.Object.DestroyImmediate(r);
        PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Sonia.prefab");UnityEngine.Object.DestroyImmediate(root);
    }
    static SkinnedMeshRenderer InstallAmyBall(GameObject root,Action03_SpinDash spin,UnityEngine.SceneManagement.Scene scene)
    {
        var amy=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BumperEngineV1/PlayerPrefabs/PO_Amy.prefab"),scene);
        try {
            var original=spin.BallAnimator;var oldBall=spin.SpinDashBall;var source=amy.GetComponentInChildren<Action03_SpinDash>(true);
            var clone=UnityEngine.Object.Instantiate(source.BallAnimator.gameObject,original.transform.parent);
            clone.name=original.name;clone.transform.localPosition=original.transform.localPosition;clone.transform.localRotation=original.transform.localRotation;clone.transform.localScale=original.transform.localScale;
            var newAnimator=clone.GetComponent<Animator>();var newBall=clone.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.sharedMesh==source.SpinDashBall.sharedMesh);
            foreach(var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true)){if(!behaviour)continue;var so=new SerializedObject(behaviour);var prop=so.GetIterator();while(prop.Next(true))if(prop.propertyType==SerializedPropertyType.ObjectReference){if(prop.objectReferenceValue==oldBall)prop.objectReferenceValue=newBall;else if(prop.objectReferenceValue==original)prop.objectReferenceValue=newAnimator;}so.ApplyModifiedPropertiesWithoutUndo();}
            spin.BallAnimator=newAnimator;spin.SpinDashBall=newBall;newBall.enabled=false;UnityEngine.Object.DestroyImmediate(original.gameObject);return newBall;
        }finally{UnityEngine.Object.DestroyImmediate(amy);}
    }
    static void RegisterMenu(){var catalog=SonicNewLevelCatalog.Load();if(!catalog)throw new Exception("Catalogue New Level absent");catalog.characters=catalog.characters.Where(c=>c!=null&&c.id!="sonia").Concat(new[]{new SonicNewLevelCharacter{id="sonia",displayName="Sonia",prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Sonia.prefab")}}).ToArray();EditorUtility.SetDirty(catalog);}
    // Explicit body mapping only. Face and hair keep their authored local pose.
    static string SourceName(string name){
        const string suffix="_Amy_skeleton";if(!name.EndsWith(suffix,StringComparison.Ordinal))return null;var n=name.Substring(0,name.Length-suffix.Length);
        if(n=="HipsTranslation"||n=="Hips")return "Hips";
        if(new[]{"Spine","Spine1","Neck","Head"}.Contains(n))return n;
        foreach(var side in new[]{"L","R"}){
            foreach(var joint in new[]{"Shoulder","UpperArm","ForeArm","Hand","Thigh","Calf","Foot","Toe"})if(n==joint+"_"+side)return n;
            foreach(var root in new[]{"001","004","008","012","016"})if(n=="Hand_"+side+"."+root)return "Hand_"+side;
        }return null;
    }
    static string AlternateName(string name){if(name=="Hips")return "hips";if(name=="Spine1")return "chest";if(name=="Head")return "head";foreach(var side in new[]{"L","R"}){if(name=="UpperArm_"+side)return "upper_arm_"+side;if(name=="ForeArm_"+side)return "forearm_"+side;if(name=="Hand_"+side)return "hand_"+side+"_001";if(name=="Thigh_"+side)return "thigh_"+side;if(name=="Calf_"+side)return "shin_"+side;if(name=="Foot_"+side)return "foot_"+side;if(name=="Toe_"+side)return "toe_"+side;foreach(var finger in new[]{"Thumb","Index","Middle","Ring","Pinky"})for(int i=1;i<=3;i++)if(name==finger+i+"_"+side)return (finger=="Thumb"?"thumb":"f_"+finger.ToLowerInvariant())+"_"+i.ToString("00")+"_"+side;}return name.ToLowerInvariant();}
    static string DirectionEnd(string name){if(name=="hips")return "Spine1";if(name=="chest")return "Head";foreach(var side in new[]{"L","R"}){if(name=="upper_arm_"+side)return "ForeArm_"+side;if(name=="forearm_"+side)return "Hand_"+side;if(name=="thigh_"+side)return "Calf_"+side;if(name=="shin_"+side)return "Foot_"+side;}return null;}
    static string TargetDirectionEnd(string name){var source=SourceName(name);if(source=="Hips")return "Spine1_Amy_skeleton";if(source=="Spine1")return "Head_Amy_skeleton";foreach(var side in new[]{"L","R"}){if(source=="UpperArm_"+side)return "ForeArm_"+side+"_Amy_skeleton";if(source=="ForeArm_"+side)return "Hand_"+side+"_Amy_skeleton";if(source=="Thigh_"+side)return "Calf_"+side+"_Amy_skeleton";if(source=="Calf_"+side)return "Foot_"+side+"_Amy_skeleton";}return null;}
    static Bounds MeshBounds(GameObject go){var renderers=go.GetComponentsInChildren<SkinnedMeshRenderer>(true);var bounds=new Bounds();bool first=true;foreach(var r in renderers){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}return bounds;}
    static T SaveAsset<T>(T value,string path) where T:UnityEngine.Object {var existing=AssetDatabase.LoadAssetAtPath<T>(path);if(existing){EditorUtility.CopySerialized(value,existing);UnityEngine.Object.DestroyImmediate(value);return existing;}AssetDatabase.CreateAsset(value,path);return value;}
    static string Safe(string name)=>string.Concat(name.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c));
    static AnimationCurve Simplify(AnimationCurve curve,float epsilon){var keys=curve.keys;var keep=new SortedSet<int>{0,keys.Length-1};Action<int,int> simplify=null;simplify=(first,last)=>{float max=epsilon;int index=-1;for(int i=first+1;i<last;i++){float value=Mathf.Lerp(keys[first].value,keys[last].value,(keys[i].time-keys[first].time)/(keys[last].time-keys[first].time));float error=Mathf.Abs(value-keys[i].value);if(error>max){max=error;index=i;}}if(index>=0){keep.Add(index);simplify(first,index);simplify(index,last);}};simplify(0,keys.Length-1);return new AnimationCurve(keep.Select(i=>keys[i]).ToArray());}
    sealed class Mapping{public Transform target,source,sourceEnd;public Quaternion sourceRotation,targetRotation;public Vector3 sourcePosition,targetPosition,targetDirection;public Mapping(Transform t,Transform s){target=t;source=s;sourceRotation=s.rotation;targetRotation=t.rotation;sourcePosition=s.position;targetPosition=t.position;}}
    sealed class PoseState{readonly Transform t;readonly Vector3 p,s;readonly Quaternion r;public PoseState(Transform t){this.t=t;p=t.localPosition;s=t.localScale;r=t.localRotation;}public void Restore(){t.localPosition=p;t.localRotation=r;t.localScale=s;}}
}





