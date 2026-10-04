using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace SonicFX.Piranha.Editor
{
    [InitializeOnLoad]
    public static class PiranhaBuilder
    {
        const string Folder="Assets/Ennemy/Piranha";
        const string Output=Folder+"/Piranha_Pret";
        [Serializable] class Model { public float[] vertices,normals,uv; public int[] weights,materials,triangles; public Bone[] bones; }
        [Serializable] class Bone { public string name; public float[] pivot; }
        public static string ReportFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/Piranha");
        static PiranhaBuilder() { EditorApplication.update += AutoBuild; }
        static void AutoBuild()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)return;
            EditorApplication.update-=AutoBuild;
            if(!File.Exists(Output+"/Piranha_Ennemi.prefab") && File.Exists(Folder+"/Textures/Piranha_Atlas.png")) Build();
        }
        [MenuItem("Tools/Sonic FX/Piranha/Creer le poisson ennemi")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            Directory.CreateDirectory(ReportFolder);
            GameObject root=null;
            try
            {
                if(File.Exists(Output+"/Piranha_Ennemi.prefab")) { Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/Piranha_Ennemi.prefab"); return; }
                var data=JsonUtility.FromJson<Model>(File.ReadAllText(Folder+"/Editor/PiranhaModel.json"));
                if(!AssetDatabase.IsValidFolder(Output)) AssetDatabase.CreateFolder(Folder,"Piranha_Pret");
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Textures/Piranha_Atlas.png");
                if(texture==null)throw new InvalidOperationException("Piranha_Atlas.png manquant");
                var importer=(TextureImporter)AssetImporter.GetAtPath(Folder+"/Textures/Piranha_Atlas.png");
                importer.wrapMode=TextureWrapMode.Clamp;importer.mipmapEnabled=true;importer.maxTextureSize=2048;importer.SaveAndReimport();
                var shader=Shader.Find("Standard");
                if(shader==null)throw new InvalidOperationException("Shader Standard manquant");
                string[] names={"Carapace_Rouge","Nageoires_Argent","Dents_Yeux","Articulations_Bleues","Bouche_Pupilles"};
                var mats=new Material[5];
                for(int i=0;i<5;i++)
                {
                    var mat=new Material(shader){name=names[i]};
                    if(i<4)mat.mainTexture=texture;else mat.color=new Color(.015f,.022f,.029f);
                    mat.SetFloat("_Metallic",i==1?.7f:i==2?0:.25f);mat.SetFloat("_Glossiness",i==1?.55f:.4f);
                    Save(mat,Output+"/"+names[i]+".mat");mats[i]=AssetDatabase.LoadAssetAtPath<Material>(Output+"/"+names[i]+".mat");
                }
                int count=data.vertices.Length/3;
                var vertices=new Vector3[count];var normals=new Vector3[count];var uv=new Vector2[count];var weights=new BoneWeight[count];
                for(int i=0;i<count;i++)
                {
                    vertices[i]=V(data.vertices,i*3);normals[i]=V(data.normals,i*3);uv[i]=new Vector2(data.uv[i*2],data.uv[i*2+1]);
                    weights[i]=new BoneWeight{boneIndex0=data.weights[i],weight0=1};
                }
                root=new GameObject("Piranha_Ennemi");root.layer=LayerMask.NameToLayer("Enemies");root.tag="Enemy";
                var visual=new GameObject("Visuel");visual.transform.SetParent(root.transform,false);
                var rig=new GameObject("Rig");rig.transform.SetParent(visual.transform,false);
                var bones=new Transform[data.bones.Length];
                for(int i=0;i<bones.Length;i++)
                {
                    var bone=new GameObject(data.bones[i].name).transform;bone.SetParent(i==0?rig.transform:bones[0],false);
                    bone.localPosition=V(data.bones[i].pivot,0);bones[i]=bone;
                }
                var mesh=new Mesh{name="Piranha_Rigged",vertices=vertices,normals=normals,uv=uv,boneWeights=weights,subMeshCount=5};
                var bind=new Matrix4x4[bones.Length];for(int i=0;i<bind.Length;i++)bind[i]=bones[i].worldToLocalMatrix*visual.transform.localToWorldMatrix;mesh.bindposes=bind;
                for(int m=0;m<5;m++)
                {
                    var triangles=new List<int>();for(int t=0;t<data.materials.Length;t++)if(data.materials[t]==m)triangles.AddRange(new[]{data.triangles[t*3],data.triangles[t*3+1],data.triangles[t*3+2]});
                    mesh.SetTriangles(triangles,m);
                }
                mesh.RecalculateBounds();mesh.RecalculateTangents();Save(mesh,Output+"/Piranha_Mesh.asset");
                var renderer=visual.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Output+"/Piranha_Mesh.asset");renderer.sharedMaterials=mats;
                renderer.bones=bones;renderer.rootBone=bones[0];renderer.localBounds=new Bounds(Vector3.zero,Vector3.one*5);renderer.updateWhenOffscreen=true;
                var animator=root.AddComponent<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.applyRootMotion=false;
                string controllerPath=Output+"/Piranha_Animator.controller";
                var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                else { foreach(var state in controller.layers[0].stateMachine.states)controller.layers[0].stateMachine.RemoveState(state.state); controller.parameters=new AnimatorControllerParameter[0]; }
                controller.AddParameter("Moving",AnimatorControllerParameterType.Bool);controller.AddParameter("Biting",AnimatorControllerParameterType.Bool);
                var machine=controller.layers[0].stateMachine;
                var idle=machine.AddState("Repos");idle.motion=Clip("Repos",2,5,3,2);
                var swim=machine.AddState("Nage");swim.motion=Clip("Nage",.6f,22,14,4);
                var bite=machine.AddState("Morsure");bite.motion=Clip("Morsure",.4f,30,20,24);
                machine.defaultState=idle;
                Transition(idle,swim,"Moving",true);Transition(swim,idle,"Moving",false);
                Transition(idle,bite,"Biting",true);Transition(swim,bite,"Biting",true);Transition(bite,idle,"Biting",false);
                animator.runtimeAnimatorController=controller;
                var rb=root.AddComponent<Rigidbody>();rb.useGravity=false;rb.isKinematic=true;rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
                var hit=new GameObject("ContactSonic");hit.transform.SetParent(root.transform,false);hit.tag="Enemy";hit.layer=LayerMask.NameToLayer("EnemyTrigger");
                var collider=hit.AddComponent<SphereCollider>();collider.radius=.8f;collider.isTrigger=true;
                var target=new GameObject("HomingTarget");target.transform.SetParent(root.transform,false);target.tag="HomingTarget";target.layer=root.layer;
                var health=root.AddComponent<EnemyHealth>();health.MaxHealth=1;health.ScoreOnDefeat=100;
                health.Explosion=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Ennemy/crab/Crab_Combat/Crab_Ennemi/Crab_Impact.prefab");
                if(health.Explosion==null)throw new InvalidOperationException("Crab_Impact.prefab manque pour la destruction");
                var brain=root.AddComponent<PiranhaController>();brain.animator=animator;brain.bodyRadius=1.4f;
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,Output+"/Piranha_Ennemi.prefab");
                AssetDatabase.SaveAssets();
                RenderPreview(root);
                File.WriteAllText(Path.Combine(ReportFolder,"unity-build.txt"),"PASS\nPrefab, mesh, atlas, 5 materials, 3 animation clips, health and water AI created.\n"+Output+"/Piranha_Ennemi.prefab");
                Selection.activeObject=prefab;EditorGUIUtility.PingObject(prefab);
                EditorApplication.delayCall+=PiranhaVerification.Run;
            }
            catch(Exception e){File.WriteAllText(Path.Combine(ReportFolder,"unity-build.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally { if(root!=null)UnityEngine.Object.DestroyImmediate(root); }
        }
        static Vector3 V(float[] array,int i)=>new Vector3(array[i],array[i+1],array[i+2]);
        static void Save(UnityEngine.Object obj,string path)
        {
            var previous=AssetDatabase.LoadMainAssetAtPath(path);
            if(previous==null)AssetDatabase.CreateAsset(obj,path);
            else { EditorUtility.CopySerialized(obj,previous);EditorUtility.SetDirty(previous);UnityEngine.Object.DestroyImmediate(obj); }
        }
        static AnimationClip Clip(string name,float duration,float tail,float fin,float jaw)
        {
            var clip=new AnimationClip{name="Piranha_"+name,frameRate=60};
            string[] paths={"Visuel/Rig/Body","Visuel/Rig/Body/Tail","Visuel/Rig/Body/FinLeft","Visuel/Rig/Body/FinRight","Visuel/Rig/Body/Jaw"};
            for(int b=0;b<5;b++)
            {
                var curves=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};
                for(int k=0;k<=32;k++)
                {
                    float phase=k/32f*Mathf.PI*2,angle=0;Vector3 axis=Vector3.up;
                    if(b==0)angle=Mathf.Sin(phase)*2;
                    if(b==1)angle=Mathf.Sin(phase)*tail;
                    if(b==2||b==3){axis=Vector3.forward;angle=Mathf.Sin(phase)*fin*(b==2?1:-1);}
                    if(b==4){axis=Vector3.right;angle=(.5f-.5f*Mathf.Cos(phase))*jaw;}
                    Quaternion q=Quaternion.AngleAxis(angle,axis);
                    for(int a=0;a<4;a++)curves[a].AddKey(k*duration/32,q[a]);
                }
                for(int a=0;a<4;a++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(paths[b],typeof(Transform),"m_LocalRotation."+"xyzw"[a]),curves[a]);
            }
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);clip.EnsureQuaternionContinuity();
            string path=Output+"/Piranha_"+name+".anim";Save(clip,path);return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        }
        static void Transition(AnimatorState from,AnimatorState to,string parameter,bool on)
        {
            var t=from.AddTransition(to);t.hasExitTime=false;t.duration=.1f;t.AddCondition(on?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,parameter);
        }
        public static void RenderPreview(GameObject root)
        {
            var preview=new PreviewRenderUtility();Texture2D image=null;
            try
            {
                var copy=UnityEngine.Object.Instantiate(root);preview.AddSingleGO(copy);
                foreach(var mb in copy.GetComponents<MonoBehaviour>())mb.enabled=false;
                copy.transform.position=Vector3.zero;copy.transform.rotation=Quaternion.identity;
                preview.camera.transform.position=new Vector3(4.6f,2.1f,4.2f);preview.camera.transform.LookAt(Vector3.zero);
                preview.camera.nearClipPlane=.05f;preview.camera.farClipPlane=30;preview.camera.fieldOfView=32;
                preview.camera.clearFlags=CameraClearFlags.Color;preview.camera.backgroundColor=new Color(.13f,.2f,.25f);
                preview.lights[0].intensity=1.3f;preview.lights[0].transform.rotation=Quaternion.Euler(35,-35,0);preview.lights[1].intensity=.7f;
                preview.ambientColor=new Color(.5f,.5f,.5f);
                preview.BeginStaticPreview(new Rect(0,0,900,800));preview.camera.Render();image=preview.EndStaticPreview();
                File.WriteAllBytes(Path.Combine(ReportFolder,"Piranha_Unity.png"),image.EncodeToPNG());
            }
            finally{if(image!=null)UnityEngine.Object.DestroyImmediate(image);preview.Cleanup();}
        }
    }
}
