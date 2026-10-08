using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using SonicFX.Menu;

public static class ClassicSonicBuilder
{
    const string Folder="Assets/Characters/ClassicSonic";
    static string Out => Path.Combine(Path.GetTempPath(),"SonicFXClassicSonic") + Path.DirectorySeparatorChar;
    [MenuItem("Sonic FX/Personnages/Reconstruire Sonic Classique")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null){Debug.LogWarning("Sonic Classique : quittez Play et le mode Prefab avant de reconstruire.");return;}
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
            model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/classic_sonic.glb"),scene);PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);model.name="ClassicRig";
            foreach(var light in model.GetComponentsInChildren<Light>(true))UnityEngine.Object.DestroyImmediate(light);
            foreach(var a in model.GetComponentsInChildren<Animator>(true))UnityEngine.Object.DestroyImmediate(a);
            // Keep imported UVs, textures and alpha; use the game's built-in Standard shader.
            var materials=new Dictionary<Material,Material>();foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>{
                    if(materials.TryGetValue(m,out var existing))return existing;
                    var n=new Material(Shader.Find("Standard")){name="Classic_"+m.name};
                    var color=m.HasProperty("baseColorFactor")?m.GetColor("baseColorFactor"):m.HasProperty("_BaseColor")?m.GetColor("_BaseColor"):m.HasProperty("_Color")?m.GetColor("_Color"):Color.white;
                    var tex=m.HasProperty("baseColorTexture")?m.GetTexture("baseColorTexture"):m.HasProperty("_BaseColorTexture")?m.GetTexture("_BaseColorTexture"):m.HasProperty("_MainTex")?m.GetTexture("_MainTex"):null;
                    n.color=color;n.mainTexture=tex;
                    string textureProperty=m.HasProperty("baseColorTexture")?"baseColorTexture":m.HasProperty("_BaseColorTexture")?"_BaseColorTexture":"_MainTex";
                    if(tex){n.mainTextureScale=m.GetTextureScale(textureProperty);n.mainTextureOffset=m.GetTextureOffset(textureProperty);}n.SetFloat("_Glossiness",.23f);n.SetFloat("_Metallic",.05f);
                    if(m.renderQueue>=3000){n.SetFloat("_Mode",3);n.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.One);n.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);n.SetInt("_ZWrite",0);n.EnableKeyword("_ALPHAPREMULTIPLY_ON");n.renderQueue=3000;}
                    var path=Folder+"/Materials/"+materials.Count+".mat";n.name=materials.Count.ToString();n=SaveAsset(n,path);materials[m]=n;return n;
                }).ToArray();renderer.updateWhenOffscreen=true;
            }
            var classic=model.GetComponentsInChildren<Transform>(true);var target=classic.GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
            var records=new List<Mapping>();foreach(var t in classic){var sourceName=SourceName(t.name);if(sourceName!=null && src.TryGetValue(sourceName,out var s))records.Add(new Mapping(t,s));}
            var srcRest=driver.GetComponentsInChildren<Transform>(true).Select(t=>new PoseState(t)).ToArray();var dstRest=classic.Select(t=>new PoseState(t)).ToArray();
            var sourceContainerRest=new PoseState(src["chr_Sonic 1"]);
            var animated=new GameObject("ClassicAnimator");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(animated,scene);model.transform.SetParent(animated.transform,false);
            var controller=new AnimatorOverrideController(sourceAnimator.runtimeAnimatorController){name="ClassicSonic_Animations"};
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
                    if(!clipRecords.Any(m=>m.target.name=="Spine_Neck"))throw new Exception("Bassin source absent pour "+clip.name);
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
                        if(map.target.name=="Spine_Neck")map.target.position=map.targetPosition+(map.source.position-map.sourcePosition)*clipRatio;
                    }
                    foreach(var map in clipRecords){var t=map.target;var q=t.localRotation;var c=curves[t];if(frame>0){var prev=new Quaternion(c[3].keys[frame-1].value,c[4].keys[frame-1].value,c[5].keys[frame-1].value,c[6].keys[frame-1].value);if(Quaternion.Dot(prev,q)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);}float[] v={t.localPosition.x,t.localPosition.y,t.localPosition.z,q.x,q.y,q.z,q.w};for(int k=0;k<7;k++)c[k].AddKey(time,v[k]);}
                }
                foreach(var item in curves){string path=AnimationUtility.CalculateTransformPath(item.Key,animated.transform);string[] properties={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"};for(int k=0;k<7;k++){var c=Simplify(item.Value[k],.0001f);for(int key=0;key<c.length;key++){AnimationUtility.SetKeyLeftTangentMode(c,key,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(c,key,AnimationUtility.TangentMode.Linear);}AnimationUtility.SetEditorCurve(baked,EditorCurveBinding.FloatCurve(path,typeof(Transform),properties[k]),c);}}
                AnimationUtility.SetAnimationClipSettings(baked,AnimationUtility.GetAnimationClipSettings(clip));AnimationUtility.SetAnimationEvents(baked,AnimationUtility.GetAnimationEvents(clip));baked.EnsureQuaternionContinuity();
                baked=SaveAsset(baked,Folder+"/Animations/"+Safe(clip.name)+".anim");for(int i=0;i<pairs.Count;i++)if(pairs[i].Key==clip)pairs[i]=new KeyValuePair<AnimationClip,AnimationClip>(clip,baked);
                if(alternate)UnityEngine.Object.DestroyImmediate(alternate);
            }
            controller.ApplyOverrides(pairs);controller=SaveAsset(controller,Folder+"/Animations/ClassicSonic.overrideController");foreach(var p in dstRest)p.Restore();
            var avatar=AvatarBuilder.BuildGenericAvatar(animated,"");avatar.name="ClassicSonicAvatar";avatar=SaveAsset(avatar,Folder+"/Animations/ClassicSonicAvatar.asset");
            BuildPlayer("Assets/BumperEngineV1/PlayerPrefabs/PO_Mania.prefab",Folder+"/Resources/SonicClassique.prefab",model,controller,avatar,driver,false,scene);
            BuildPlayer("Assets/BumperEngineV1/PlayerPrefabs/SonicManiaFree.prefab",Folder+"/SonicClassiqueFree.prefab",model,controller,avatar,driver,true,scene);
            RegisterMenu();AssetDatabase.SaveAssets();
            UnityEngine.Object.DestroyImmediate(animated);UnityEngine.Object.DestroyImmediate(driver);
            File.WriteAllText(Out+"build.txt","Built "+records.Count+" mapped bones, "+pairs.Count+" animation states, "+allClips.Length+" animation clips.\n"+DateTime.UtcNow.ToString("O"));
        }finally{if(model)UnityEngine.Object.DestroyImmediate(model);if(source)UnityEngine.Object.DestroyImmediate(source);EditorSceneManager.ClosePreviewScene(scene);}
    }
    static void BuildPlayer(string original,string path,GameObject model,RuntimeAnimatorController controller,Avatar avatar,GameObject modern,bool free,UnityEngine.SceneManagement.Scene scene)
    {
        var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(original),scene);PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);root.name=free?"SonicClassiqueFree":"SonicClassique";
        var action=root.GetComponentInChildren<Action00_Regular>(true);var animator=action.CharacterAnimator;var spin=root.GetComponentInChildren<Action03_SpinDash>(true);var maniaBall=spin.SpinDashBall;var oldSkins=animator.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r!=maniaBall).ToArray();var old=oldSkins.Cast<Renderer>().ToArray();
        var oldBounds=MeshBounds(animator.gameObject);foreach(var r in old)r.enabled=false;
        var clone=UnityEngine.Object.Instantiate(model,animator.transform);clone.name="ClassicRig";clone.transform.localPosition=Vector3.zero;clone.transform.localRotation=model.transform.localRotation;clone.transform.localScale=model.transform.localScale;
        var bounds=MeshBounds(clone);float scale=oldBounds.size.y/bounds.size.y;clone.transform.localScale*=scale;bounds=MeshBounds(clone);
        // Align feet and center with Mania, without changing the capsule or physics.
        clone.transform.position+=new Vector3(oldBounds.center.x-bounds.center.x,oldBounds.min.y-bounds.min.y,oldBounds.center.z-bounds.center.z);
        animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
        var newSkins=clone.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var contact=action.gameObject.AddComponent<CharacterGroundContact>();contact.ModelRoot=clone.transform;contact.Shoes=newSkins.Where(r=>r.name.StartsWith("SonicClassic.001")).ToArray();
        var samplingAvatar=AvatarBuilder.BuildGenericAvatar(animator.gameObject,"");animator.avatar=samplingAvatar;
        CharacterGroundFit.Apply(action.GetComponent<PlayerBhysics>(),animator,clone.transform,"Spine_Neck");
        var fittedAvatar=AvatarBuilder.BuildGenericAvatar(animator.gameObject,"");fittedAvatar.name=root.name+"Avatar";animator.avatar=SaveAsset(fittedAvatar,Folder+"/Animations/"+root.name+"Avatar.asset");UnityEngine.Object.DestroyImmediate(samplingAvatar);
        foreach(var hurt in root.GetComponentsInChildren<HurtControl>(true))hurt.SonicSkins= (hurt.SonicSkins??Array.Empty<SkinnedMeshRenderer>()).Where(r=>r!=null&&!oldSkins.Contains(r)).Concat(newSkins).ToArray();
        var voice=root.GetComponentInChildren<SonicSoundsControl>(true);var source=modern.GetComponentInChildren<SonicSoundsControl>(true);voice.CombatVoiceClips=(AudioClip[])source.CombatVoiceClips.Clone();voice.JumpingVoiceClips=(AudioClip[])source.JumpingVoiceClips.Clone();voice.PainVoiceClips=(AudioClip[])source.PainVoiceClips.Clone();
        if(voice.Source4==null){var obj=new GameObject("ClassicVoice");obj.transform.SetParent(voice.transform,false);voice.Source4=obj.AddComponent<AudioSource>();}if(source.Source4){voice.Source4.volume=source.Source4.volume;voice.Source4.pitch=source.Source4.pitch;voice.Source4.outputAudioMixerGroup=source.Source4.outputAudioMixerGroup;voice.Source4.spatialBlend=source.Source4.spatialBlend;}voice.Source4.playOnAwake=false;
        // Remap serialized references to the old renderers so scripts cannot reveal Mania.
        foreach(var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true)){if(!behaviour)continue;var so=new SerializedObject(behaviour);var prop=so.GetIterator();while(prop.Next(true))if(prop.propertyType==SerializedPropertyType.ObjectReference && prop.objectReferenceValue is SkinnedMeshRenderer r && oldSkins.Contains(r))prop.objectReferenceValue=newSkins[0];so.ApplyModifiedPropertiesWithoutUndo();}
        // Arrays must contain every Classic body part, while the ball remains
        // a separate Mania renderer. Never remap the ball to a facial mesh.
        spin.PlayerSkin=newSkins;spin.SpinDashBall=maniaBall;maniaBall.enabled=false;
        foreach(var drop in root.GetComponentsInChildren<Action08_DropDash>(true))drop.PlayerSkin=newSkins;
        foreach(var hurt in root.GetComponentsInChildren<HurtControl>(true))hurt.SonicSkins=newSkins;
        var appearance=action.gameObject.AddComponent<ClassicSonicAppearance>();appearance.BodySkins=newSkins;appearance.ManiaBall=maniaBall;
        foreach(var r in old)UnityEngine.Object.DestroyImmediate(r);
        PrefabUtility.SaveAsPrefabAsset(root,path);UnityEngine.Object.DestroyImmediate(root);
    }
    static void RegisterMenu(){var catalog=SonicNewLevelCatalog.Load();var existingRoute=Resources.Load<SonicStoryRoute>("SonicStoryRoute");if(catalog?.Find("classicsonic")?.prefab&&catalog?.Find("classicsonicfree")?.prefab&&existingRoute&&existingRoute.stages.Any(s=>s.character=="classicsonic"))return;if(catalog==null)throw new Exception("Catalogue New Level absent");var list=catalog.characters.Where(c=>c.id!="classicsonic"&&c.id!="classicsonicfree").ToList();list.Add(new SonicNewLevelCharacter{id="classicsonic",displayName="Sonic Classique",prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Resources/SonicClassique.prefab")});list.Add(new SonicNewLevelCharacter{id="classicsonicfree",displayName="Sonic Classique Free",prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/SonicClassiqueFree.prefab")});catalog.characters=list.ToArray();EditorUtility.SetDirty(catalog);var route=Resources.Load<SonicStoryRoute>("SonicStoryRoute");if(route){route.stages=route.stages.Where(s=>s.character!="classicsonic").Concat(route.stages.Where(s=>s.character=="sonic").Select(s=>new SonicStoryRoute.StageLink{character="classicsonic",levelScene=s.levelScene,nextScene=s.nextScene})).ToArray();EditorUtility.SetDirty(route);}}
    // Only retarget the body. Facial controls use a different rig and must keep
    // their authored local pose; the entire cranium follows Head_Neck together.
    static string SourceName(string name){if(!name.EndsWith("_Neck"))return null;var n=name.Substring(0,name.Length-5);if(n=="Spine")return "Hips";if(n=="Head")return "Head";foreach(var side in new[]{"L","R"}){for(int i=1;i<=12;i++){if(n=="Arm"+i.ToString("00")+"_"+side)return (i<=6?"UpperArm_":"ForeArm_")+side;if(n=="Leg"+i.ToString("00")+"_"+side)return (i<=6?"Thigh_":"Calf_")+side;}foreach(var extremity in new[]{"Hand","Foot","Toe"})if(n==extremity+"_"+side)return n;foreach(var finger in new[]{"Thumb","Index","Middle","Ring","Pinky"})for(int i=0;i<=4;i++)if(n==finger+i+"_"+side)return finger+Mathf.Clamp(i,1,3)+"_"+side;}return null;}
    static string AlternateName(string name){if(name=="Hips")return "hips";if(name=="Spine1")return "chest";if(name=="Head")return "head";foreach(var side in new[]{"L","R"}){if(name=="UpperArm_"+side)return "upper_arm_"+side;if(name=="ForeArm_"+side)return "forearm_"+side;if(name=="Hand_"+side)return "hand_"+side+"_001";if(name=="Thigh_"+side)return "thigh_"+side;if(name=="Calf_"+side)return "shin_"+side;if(name=="Foot_"+side)return "foot_"+side;if(name=="Toe_"+side)return "toe_"+side;foreach(var finger in new[]{"Thumb","Index","Middle","Ring","Pinky"})for(int i=1;i<=3;i++)if(name==finger+i+"_"+side)return (finger=="Thumb"?"thumb":"f_"+finger.ToLowerInvariant())+"_"+i.ToString("00")+"_"+side;}return name.ToLowerInvariant();}
    static string DirectionEnd(string name){if(name=="hips")return "Spine1";if(name=="chest")return "Head";foreach(var side in new[]{"L","R"}){if(name=="upper_arm_"+side)return "ForeArm_"+side;if(name=="forearm_"+side)return "Hand_"+side;if(name=="thigh_"+side)return "Calf_"+side;if(name=="shin_"+side)return "Foot_"+side;}return null;}
    static string TargetDirectionEnd(string name){if(name=="Spine_Neck")return "Head_Neck";if(name=="Head_Neck")return "HeadTop_Neck";foreach(var side in new[]{"L","R"})for(int i=1;i<=12;i++){if(name=="Arm"+i.ToString("00")+"_"+side+"_Neck")return (i<=6?"Arm07_":"Hand_")+side+"_Neck";if(name=="Leg"+i.ToString("00")+"_"+side+"_Neck")return (i<=6?"Leg07_":"Foot_")+side+"_Neck";}return null;}
    static Bounds MeshBounds(GameObject go){var renderers=go.GetComponentsInChildren<SkinnedMeshRenderer>(true);var bounds=new Bounds();bool first=true;foreach(var r in renderers){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}return bounds;}
    static T SaveAsset<T>(T value,string path) where T:UnityEngine.Object {var existing=AssetDatabase.LoadAssetAtPath<T>(path);if(existing){EditorUtility.CopySerialized(value,existing);UnityEngine.Object.DestroyImmediate(value);return existing;}AssetDatabase.CreateAsset(value,path);return value;}
    static string Safe(string name)=>string.Concat(name.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c));
    static AnimationCurve Simplify(AnimationCurve curve,float epsilon){var keys=curve.keys;var keep=new SortedSet<int>{0,keys.Length-1};Action<int,int> simplify=null;simplify=(first,last)=>{float max=epsilon;int index=-1;for(int i=first+1;i<last;i++){float value=Mathf.Lerp(keys[first].value,keys[last].value,(keys[i].time-keys[first].time)/(keys[last].time-keys[first].time));float error=Mathf.Abs(value-keys[i].value);if(error>max){max=error;index=i;}}if(index>=0){keep.Add(index);simplify(first,index);simplify(index,last);}};simplify(0,keys.Length-1);return new AnimationCurve(keep.Select(i=>keys[i]).ToArray());}
    sealed class Mapping{public Transform target,source,sourceEnd;public Quaternion sourceRotation,targetRotation;public Vector3 sourcePosition,targetPosition,targetDirection;public Mapping(Transform t,Transform s){target=t;source=s;sourceRotation=s.rotation;targetRotation=t.rotation;sourcePosition=s.position;targetPosition=t.position;}}
    sealed class PoseState{readonly Transform t;readonly Vector3 p,s;readonly Quaternion r;public PoseState(Transform t){this.t=t;p=t.localPosition;s=t.localScale;r=t.localRotation;}public void Restore(){t.localPosition=p;t.localRotation=r;t.localScale=s;}}
}





