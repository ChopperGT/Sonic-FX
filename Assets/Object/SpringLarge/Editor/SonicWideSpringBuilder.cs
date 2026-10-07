using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace SonicFX.Structures.Editor
{
    [InitializeOnLoad]
    public static class SonicWideSpringBuilder
    {
        public const string Folder="Assets/Structure/SpringLarge";
        public const string PrefabPath=Folder+"/Spring_Large.prefab";
        public static string Reports=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/WideSpring");
        static SonicWideSpringBuilder(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null)return;
            EditorApplication.update-=Ready;
            if(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)==null)Install();
            else {var report=Path.Combine(Reports,"tests.txt");if(!File.Exists(report)||!File.ReadAllText(report).StartsWith("PASS"))Install();}
        }
        static Texture2D Texture(string name,bool linear=false,bool alpha=false)
        {
            string path=Folder+"/Textures/"+name+".png";
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null)throw new Exception("Texture manquante : "+path);
            if(importer.sRGBTexture==linear || importer.alphaIsTransparency!=alpha || importer.wrapMode!=(alpha?TextureWrapMode.Clamp:TextureWrapMode.Repeat) || importer.textureCompression!=TextureImporterCompression.Uncompressed)
            {
                importer.sRGBTexture=!linear;importer.alphaIsTransparency=alpha;importer.wrapMode=alpha?TextureWrapMode.Clamp:TextureWrapMode.Repeat;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Material Material(string name,string diffuse,string spec,string emission=null)
        {
            string path=Folder+"/Materials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            bool create=m==null; var shader=Shader.Find("Sonic FX/Spring Large"); if(shader==null)throw new Exception("Shader Spring large introuvable.");
            if(create)m=new Material(shader){name=name};else m.shader=shader;
            m.mainTexture=Texture(diffuse);m.SetFloat("_SpecularStrength",.1f);
            m.SetColor("_Color",Color.white);m.SetTexture("_SpecGlossMap",Texture(spec,true));m.EnableKeyword("_SPECGLOSSMAP");m.SetFloat("_GlossMapScale",.25f);
            if(emission!=null){m.SetTexture("_EmissionMap",Texture(emission));m.SetColor("_EmissionColor",Color.white*.08f);m.EnableKeyword("_EMISSION");}
            if(create)AssetDatabase.CreateAsset(m,path);else{EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);}return m;
        }
        static Mesh SaveMesh(string name,Func<Mesh> create)
        {
            string path=Folder+"/Meshes/"+name+".asset";var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(m!=null)return m;
            m=create();m.name=name;AssetDatabase.CreateAsset(m,path);return m;
        }
        [MenuItem("Tools/Sonic FX/Structures/Installer et verifier Spring large")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null)return;
            Directory.CreateDirectory(Reports);GameObject root=null,template=null;
            try
            {
                foreach(string part in new[]{"Meshes","Materials","Animations"})if(!AssetDatabase.IsValidFolder(Folder+"/"+part))AssetDatabase.CreateFolder(Folder,part);
                var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BumperEngineV1/ObjectPrefabs/Spring.prefab");
                if(original==null)throw new Exception("Spring original introuvable.");
                var prop=original.GetComponent<Spring_Proprieties>();var originalAudio=original.GetComponent<AudioSource>();
                var body=Material("Corps","cmn_obj_spring_body01_dif","cmn_obj_spring_body01_spc","cmn_obj_spring_body01_e1");
                var star=Material("Etoiles","cmn_obj_spring_star01_dif","cmn_obj_spring_star01_spc");
                var source=SaveMesh("Spring_Source",ReadSource);float half=SonicWideSpring.Pitch*.5f;
                var left=SaveMesh("Extremite_Gauche",()=>SonicWideSpringGeometry.Slice(source,-100, -half,Vector3.zero));
                var middle=SaveMesh("Emplacement_Central",()=>SonicWideSpringGeometry.Slice(source,-half,half,Vector3.zero));
                var right=SaveMesh("Extremite_Droite",()=>SonicWideSpringGeometry.Slice(source,half,100,Vector3.zero));
                var single=SaveMesh("Emplacement_Unique",()=>
                {
                    var a=SonicWideSpringGeometry.Slice(source,-100,-SonicWideSpring.Pitch,Vector3.right*SonicWideSpring.Pitch);
                    var b=SonicWideSpringGeometry.Slice(source,SonicWideSpring.Pitch,100,Vector3.left*SonicWideSpring.Pitch);
                    var empty=new Mesh();empty.subMeshCount=2;empty.vertices=new Vector3[0];empty.normals=new Vector3[0];empty.uv=new Vector2[0];empty.SetTriangles(new int[0],0);empty.SetTriangles(new int[0],1);
                    // Assemble two halves with zero separation (the regular two-slot offset is undone).
                    var av=a.vertices;for(int i=0;i<av.Length;i++)av[i].x-=half;a.vertices=av;
                    var bv=b.vertices;for(int i=0;i<bv.Length;i++)bv[i].x+=half;b.vertices=bv;
                    var m=SonicWideSpringGeometry.Assemble(a,empty,b,null,2);Object.DestroyImmediate(a);Object.DestroyImmediate(b);Object.DestroyImmediate(empty);return m;
                });
                var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder+"/Animations/Spring_Large.controller");
                if(controller==null)
                {
                    controller=AnimatorController.CreateAnimatorControllerAtPath(Folder+"/Animations/Spring_Large.controller");controller.AddParameter("Hit",AnimatorControllerParameterType.Trigger);
                    var idle=new AnimationClip{name="Repos"};var hit=new AnimationClip{name="Activation"};
                    idle.SetCurve("AnimationMarker",typeof(Transform),"m_LocalScale.y",AnimationCurve.Constant(0,.45f,1));
                    hit.SetCurve("AnimationMarker",typeof(Transform),"m_LocalScale.y",AnimationCurve.Constant(0,.45f,1));
                    AssetDatabase.CreateAsset(idle,Folder+"/Animations/Repos.anim");AssetDatabase.CreateAsset(hit,Folder+"/Animations/Activation.anim");
                    var sm=controller.layers[0].stateMachine;var rest=sm.AddState("Repos");rest.motion=idle;sm.defaultState=rest;
                    var active=sm.AddState("Activation");active.motion=hit;active.AddStateMachineBehaviour<SonicWideSpringHitState>();
                    var entry=rest.AddTransition(active);entry.hasExitTime=false;entry.duration=0;entry.AddCondition(AnimatorConditionMode.If,0,"Hit");
                    var exit=active.AddTransition(rest);exit.hasExitTime=true;exit.exitTime=1;exit.duration=0;
                    EditorUtility.SetDirty(controller);AssetDatabase.SaveAssetIfDirty(controller);
                }
                var slot=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Emplacement.prefab");
                if(slot==null)
                {
                    template=new GameObject("Emplacement");template.tag="Spring";template.layer=original.layer;
                    var pos=new GameObject("Position de lancement");pos.transform.SetParent(template.transform,false);pos.transform.localPosition=Vector3.up*3.8f;
                    var marker=new GameObject("AnimationMarker");marker.transform.SetParent(template.transform,false);
                    var target=new GameObject("HomingTarget");target.tag="HomingTarget";target.transform.SetParent(template.transform,false);target.transform.localPosition=Vector3.up*3.2f;
                    var box=template.AddComponent<BoxCollider>();box.isTrigger=true;box.center=Vector3.up*3.2f;box.size=new Vector3(SonicWideSpring.Pitch-.05f,1.25f,4.4f);
                    template.AddComponent<Spring_Proprieties>();template.AddComponent<SonicWideSpringSlot>();
                    template.AddComponent<Animator>().runtimeAnimatorController=controller;
                    var audio=template.AddComponent<AudioSource>();EditorUtility.CopySerialized(originalAudio,audio);audio.playOnAwake=false;
                    var particle=new GameObject("Etoile d'activation");particle.transform.SetParent(template.transform,false);particle.transform.localPosition=Vector3.up*3.7f;
                    var ps=particle.AddComponent<ParticleSystem>();var main=ps.main;main.playOnAwake=false;main.loop=false;main.duration=.35f;main.startLifetime=.35f;main.startSpeed=1.5f;main.startSize=1.3f;main.simulationSpace=ParticleSystemSimulationSpace.World;
                    var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,1)});var shape=ps.shape;shape.enabled=false;
                    var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});color.color=gradient;
                    var flash=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/Etoile_Activation.mat");if(flash==null){flash=new Material(Shader.Find("Sprites/Default")){mainTexture=Texture("epm_ob_com_yh1_spring_star1",false,true)};AssetDatabase.CreateAsset(flash,Folder+"/Materials/Etoile_Activation.mat");}
                    ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=flash;ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                    slot=PrefabUtility.SaveAsPrefabAsset(template,Folder+"/Emplacement.prefab");
                }
                if(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)==null)
                {
                    root=new GameObject("Spring_Large");root.layer=original.layer;
                    var v=new GameObject("Visuel");v.transform.SetParent(root.transform,false);var filter=v.AddComponent<MeshFilter>();filter.sharedMesh=source;v.AddComponent<MeshRenderer>().sharedMaterials=new[]{body,star};
                    var c=new GameObject("Emplacements");c.transform.SetParent(root.transform,false);
                    var wide=root.AddComponent<SonicWideSpring>();wide.source=source;wide.leftPiece=left;wide.middlePiece=middle;wide.rightPiece=right;wide.singlePiece=single;wide.slotPrefab=slot;wide.visual=filter;wide.slotContainer=c.transform;
                    wide.springForce=prop.SpringForce;wide.isAdditive=prop.IsAdditive;wide.lockControl=prop.LockControl;wide.lockTime=prop.LockTime;wide.Refresh();filter.sharedMesh=source;
                    if(PrefabUtility.SaveAsPrefabAsset(root,PrefabPath)==null)throw new Exception("Spring large non enregistre.");
                }
                AssetDatabase.SaveAssets();
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Reports,"tests.txt"),"FAIL installation\n"+e);Debug.LogException(e);return;}
            finally{if(root!=null)Object.DestroyImmediate(root);if(template!=null)Object.DestroyImmediate(template);}
            SonicWideSpringVerification.Verify();
        }
        static Mesh ReadSource()
        {
            var positions=new List<Vector3>();var ns=new List<Vector3>();var tex=new List<Vector2>();var output=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var sub=new[]{new List<int>(),new List<int>()};int material=0;
            float F(string s)=>float.Parse(s,CultureInfo.InvariantCulture);
            Vector3 Rotate(Vector3 p)=>new Vector3(p.x,-p.z,p.y);
            foreach(string line in File.ReadLines(Folder+"/Model/cmn_obj_widespring.obj"))
            {
                var p=line.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);if(p.Length==0)continue;
                if(p[0]=="v")positions.Add((Rotate(new Vector3(F(p[1]),F(p[2]),F(p[3])))+Vector3.up*.131461f)*3);
                else if(p[0]=="vn")ns.Add(Rotate(new Vector3(F(p[1]),F(p[2]),F(p[3]))).normalized);
                else if(p[0]=="vt")tex.Add(new Vector2(F(p[1]),F(p[2])));
                else if(p[0]=="usemtl")material=p[1].Contains("star")?1:0;
                else if(p[0]=="f")for(int i=2;i<p.Length-1;i++)
                {
                    var triangle=new[]{p[1],p[i],p[i+1]};int start=output.Count;
                    foreach(string corner in triangle){var k=corner.Split('/');output.Add(positions[int.Parse(k[0])-1]);uv.Add(tex[int.Parse(k[1])-1]);normals.Add(ns[int.Parse(k[2])-1]);}
                    bool reverse=Vector3.Dot(Vector3.Cross(output[start+1]-output[start],output[start+2]-output[start]),normals[start]+normals[start+1]+normals[start+2])<0;
                    sub[material].Add(start);sub[material].Add(start+(reverse?2:1));sub[material].Add(start+(reverse?1:2));
                }
            }
            var mesh=new Mesh{name="Spring source"};mesh.SetVertices(output);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.subMeshCount=2;mesh.SetTriangles(sub[0],0);mesh.SetTriangles(sub[1],1);mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
        }
    }
}
