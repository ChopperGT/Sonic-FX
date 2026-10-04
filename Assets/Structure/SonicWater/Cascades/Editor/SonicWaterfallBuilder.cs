using System;
using System.IO;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace SonicFX.Water.Editor
{
    [InitializeOnLoad]
    public static class SonicWaterfallBuilder
    {
        public const string Folder="Assets/Structure/SonicWater/Cascades";
        public const string PrefabPath=Folder+"/Cascade_GreenHill.prefab";
        public static readonly string Reports=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/SonicWaterfall");
        static SonicWaterfallBuilder(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)return;
            EditorApplication.update-=Ready;
            if(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)==null || !File.Exists(Folder+"/Editor/LandingHandlesVerified.txt"))Build();
        }
        [MenuItem("Tools/Sonic FX/Eau/Creer et verifier la cascade")]
        public static void Build()
        {
            Directory.CreateDirectory(Reports);GameObject go=null;
            try
            {
                Material curtain=Material("Cascade.mat","Sonic FX/Cascade"),foam=Material("Ecume.mat","Sonic FX/Ecume Cascade");
                var splash=AssetDatabase.LoadAssetAtPath<Material>("Assets/Structure/SonicWater/Eclaboussure.mat");
                if(splash==null)throw new Exception("Le materiau Eclaboussure.mat de l'eau est introuvable.");
                var strip=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Rideau.asset");
                if(strip==null)
                {
                    strip=new Mesh{name="Rideau de cascade"};const int columns=16,rows=40;
                    var vertices=new Vector3[(columns+1)*(rows+1)];var uv=new Vector2[vertices.Length];var tris=new int[columns*rows*6];int k=0;
                    for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
                    {
                        int i=y*(columns+1)+x;float u=(float)x/columns,v=(float)y/rows;
                        vertices[i]=new Vector3(u-.5f,-v,Mathf.Sin(v*Mathf.PI));uv[i]=new Vector2(u,v);
                        if(y<rows && x<columns){tris[k++]=i;tris[k++]=i+columns+1;tris[k++]=i+1;tris[k++]=i+1;tris[k++]=i+columns+1;tris[k++]=i+columns+2;}
                    }
                    strip.vertices=vertices;strip.uv=uv;strip.triangles=tris;strip.RecalculateNormals();strip.RecalculateBounds();
                    strip.bounds=new Bounds(new Vector3(0,-.5f,.5f),new Vector3(1,1,1.2f));AssetDatabase.CreateAsset(strip,Folder+"/Rideau.asset");
                }
                var plane=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Structure/SonicWater/Surface.asset");
                if(plane==null)throw new Exception("La surface d'eau existante est introuvable.");
                // Existing prefab remains intact when the verification menu is used again.
                if(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)==null)
                {
                    go=new GameObject("Cascade_GreenHill");var fall=go.AddComponent<SonicWaterfall>();
                    fall.curtain=Surface(go.transform,"Rideau d'eau",strip,curtain);
                    fall.foam=Surface(go.transform,"Ecume a l'arrivee",plane,foam);
                    var spray=new GameObject("Brume et gouttelettes");spray.transform.SetParent(go.transform,false);
                    var ps=spray.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main=ps.main;main.loop=true;main.playOnAwake=true;main.startLifetime=new ParticleSystem.MinMaxCurve(.4f,1.2f);main.startSpeed=new ParticleSystem.MinMaxCurve(1.5f,4);
                    main.startSize=new ParticleSystem.MinMaxCurve(.15f,.6f);main.startColor=new Color(.7f,.95f,1,.55f);main.maxParticles=500;main.gravityModifier=.4f;main.simulationSpace=ParticleSystemSimulationSpace.World;
                    var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.rotation=new Vector3(-90,0,0);shape.randomDirectionAmount=.35f;
                    var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.65f,.15f),new GradientAlphaKey(0,1)});color.color=gradient;
                    var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=splash;renderer.renderMode=ParticleSystemRenderMode.Billboard;
                    fall.spray=ps;fall.splashMaterial=splash;fall.Refresh();fall.curtain.GetComponent<MeshFilter>().sharedMesh=strip;PrefabUtility.SaveAsPrefabAsset(go,PrefabPath);
                }
                AssetDatabase.SaveAssets();Verify();Preview();
                File.WriteAllText(Folder+"/Editor/CascadeInstalled.txt","Cascade installee et verifiee.");AssetDatabase.ImportAsset(Folder+"/Editor/CascadeInstalled.txt");
                File.WriteAllText(Folder+"/Editor/LandingHandlesVerified.txt","Point de chute et axe Z verifies.");AssetDatabase.ImportAsset(Folder+"/Editor/LandingHandlesVerified.txt");
                Debug.Log("Cascade prete : Assets/Structure/SonicWater/Cascades/Cascade_GreenHill.prefab");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Reports,"unity-tests.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally{if(go!=null)UnityEngine.Object.DestroyImmediate(go);}
        }
        static Material Material(string name,string shaderName)
        {
            Shader shader=Shader.Find(shaderName);if(shader==null || ShaderUtil.ShaderHasError(shader))throw new Exception("Shader invalide : "+shaderName);
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+name);if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,Folder+"/"+name);}return mat;
        }
        static Transform Surface(Transform parent,string name,Mesh mesh,Material mat)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=mat;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return go.transform;
        }
        static void Check(bool result,string message){if(!result)throw new Exception(message);}
        static void Verify()
        {
            GameObject water=null,go=null;
            try
            {
                water=new GameObject("Cascade water check"){hideFlags=HideFlags.HideAndDontSave};water.transform.position=new Vector3(10000,10,10000);
                var basin=water.AddComponent<SonicWaterVolume>();basin.width=40;basin.length=40;
                go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));go.hideFlags=HideFlags.HideAndDontSave;
                go.transform.position=water.transform.position+Vector3.up*20;var fall=go.GetComponent<SonicWaterfall>();fall.receivingWater=basin;fall.Refresh();
                Check(Mathf.Abs(fall.EffectiveHeight-20)<.001f && fall.HasWaterLanding,"Automatic landing height");
                Check(fall.foam.gameObject.activeSelf && fall.spray.gameObject.activeSelf,"Landing foam and spray enabled");
                water.transform.position+=Vector3.up*3;fall.Refresh();Check(Mathf.Abs(fall.EffectiveHeight-17)<.001f,"Follow basin surface changes");
                go.transform.localScale=new Vector3(1,2,1);fall.Refresh();Check(Mathf.Abs(fall.EffectiveHeight-8.5f)<.001f,"Scaled cascade reaches basin");
                go.transform.localScale=Vector3.one;go.transform.rotation=Quaternion.Euler(0,75,0);fall.Refresh();
                Vector3 center=go.transform.TransformPoint(new Vector3(0,-8,fall.bulge));
                Check(fall.ContainsCurtain(center),"Rotated curtain volume");
                Check(fall.IntersectsCurtain(center-go.transform.forward*20,center+go.transform.forward*20,out _),"Fast swept traversal");
                Check(!fall.IntersectsCurtain(center+go.transform.right*100,center+go.transform.right*101,out _),"No remote crossing");
                var velocity=fall.CurrentVelocity(new Vector3(12,4,8),.1f);Check(velocity.y<4 && velocity.x==12 && velocity.z==8,"Downward force preserves horizontal movement");
                var fast=new Vector3(0,-60,0);Check(fall.CurrentVelocity(fast,.2f)==fast,"Current never slows a faster fall");
                Check(fall.CurrentVelocity(Vector3.zero,100).y==-fall.currentFallSpeed,"Current speed cap");
                Check(fall.CurrentVelocity(Vector3.up,0)==Vector3.up,"Zero elapsed time applies no force");
                fall.pushDown=false;Check(fall.CurrentVelocity(Vector3.up,1)==Vector3.up,"Current can be disabled");
                fall.landingOffset=new Vector2(3,7);fall.bulge=0;fall.Refresh();
                Check((fall.LandingLocalPosition-new Vector3(3,-17,7)).sqrMagnitude<.001f,"Editable landing position");
                Check((fall.CurveCenter(.5f)-fall.LandingLocalPosition*.5f).sqrMagnitude<.001f,"Zero curvature is a straight line");
                Check((fall.foam.localPosition-fall.LandingLocalPosition-Vector3.up*.04f).sqrMagnitude<.001f,"Foam follows landing");
                Vector3 shifted=go.transform.TransformPoint(fall.CurveCenter(.5f));
                Check(fall.ContainsCurtain(shifted),"Current volume follows relocated curtain");
                Check(fall.IntersectsCurtain(shifted-go.transform.forward*20,shifted+go.transform.forward*20,out _),"Fast crossing of shifted curtain");
                Check(!fall.ContainsCurtain(go.transform.TransformPoint(new Vector3(0,-8.5f,0))),"No current at the abandoned old position");
                var generated=fall.curtain.GetComponent<MeshFilter>().sharedMesh;
                Check(generated.bounds.Contains(fall.LandingLocalPosition),"Render bounds include relocated end");
                Vector3 fixedLanding=fall.LandingPosition;fall.bulge=4;fall.Refresh();Check((fall.LandingPosition-fixedLanding).sqrMagnitude<.001f,"Curvature does not move landing");
                fall.landingOffset=Vector2.zero;fall.bulge=.6f;fall.Refresh();
                go.transform.position+=Vector3.right*100;fall.Refresh();Check(!fall.HasWaterLanding && !fall.foam.gameObject.activeSelf,"No foam outside assigned basin");
                fall.receivingWater=null;fall.Refresh();Check(fall.EffectiveHeight==fall.height,"Manual height without a basin");
                Check(go.GetComponentsInChildren<Collider>(true).Length==0,"Cascade never blocks Sonic with a solid collider");
                File.WriteAllText(Path.Combine(Reports,"unity-tests.txt"),"PASS\nBasin link, scale/rotation, automatic height, moved landing, zero-curvature straight fall, foam and current following the new position, swept crossing, render bounds, curvature independent of landing, downward current, manual height and non-solid curtain.\n");
            }
            finally{if(go!=null)UnityEngine.Object.DestroyImmediate(go);if(water!=null)UnityEngine.Object.DestroyImmediate(water);}
        }
        static void Preview()
        {
            var preview=new PreviewRenderUtility();GameObject root=null;Texture2D image=null;
            try
            {
                root=new GameObject("Cascade preview");
                var basin=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Structure/SonicWater/Eau_GreenHill.prefab"),root.transform);
                var water=basin.GetComponent<SonicWaterVolume>();water.width=40;water.length=28;water.depth=10;
                water.surface.localScale=new Vector3(40,1,28);
                var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),root.transform);go.transform.position=new Vector3(0,17,0);
                var fall=go.GetComponent<SonicWaterfall>();fall.receivingWater=water;fall.width=10;fall.landingOffset=new Vector2(0,7);fall.bulge=0;fall.Refresh();
                preview.AddSingleGO(root);preview.camera.fieldOfView=40;preview.camera.transform.position=new Vector3(24,17,29);preview.camera.transform.LookAt(new Vector3(0,7,0));preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=150;
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.12f,.19f,.24f);
                preview.lights[0].intensity=1.2f;preview.lights[0].transform.rotation=Quaternion.Euler(40,-30,0);preview.lights[1].intensity=.5f;preview.ambientColor=Color.gray;
                preview.BeginStaticPreview(new Rect(0,0,1000,760));preview.Render();image=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(Reports,"Cascade_PointChute.png"),image.EncodeToPNG());
            }
            finally{if(image!=null)UnityEngine.Object.DestroyImmediate(image);preview.Cleanup();if(root!=null)UnityEngine.Object.DestroyImmediate(root);}
        }
        [MenuItem("GameObject/Sonic FX/Cascade",false,11)]
        static void Add()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);if(prefab==null){Build();prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);}if(prefab==null)return;
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);Undo.RegisterCreatedObjectUndo(instance,"Ajouter cascade");
            if(SceneView.lastActiveSceneView!=null)instance.transform.position=SceneView.lastActiveSceneView.pivot;Selection.activeGameObject=instance;
        }
    }
    [CustomEditor(typeof(SonicWaterfall))]
    public class SonicWaterfallEditor:UnityEditor.Editor
    {
        readonly BoxBoundsHandle boundsHandle=new BoxBoundsHandle
        {
            axes=PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y
        };
        void OnSceneGUI()
        {
            var fall=(SonicWaterfall)target;
            fall.Refresh();
            using(new Handles.DrawingScope(new Color(.15f,.9f,1),fall.transform.localToWorldMatrix))
            {
                boundsHandle.center=new Vector3(0,-fall.EffectiveHeight*.5f,0);
                boundsHandle.size=new Vector3(fall.width,fall.EffectiveHeight,.1f);
                EditorGUI.BeginChangeCheck();boundsHandle.DrawHandle();
                if(EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObjects(new UnityEngine.Object[]{fall,fall.transform},"Etirer la cascade");
                    ApplySceneBounds(fall,boundsHandle.center,boundsHandle.size);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(fall);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(fall.transform);
                    EditorUtility.SetDirty(fall);Repaint();
                }
                Vector3 curveHandle=fall.CurveCenter(.5f)+Vector3.right*(fall.width*.5f+1);
                Handles.color=new Color(.25f,.55f,1);Handles.Label(curveHandle+Vector3.up*.5f,"Profondeur / courbure Z");
                EditorGUI.BeginChangeCheck();Vector3 depth=Handles.Slider(curveHandle,Vector3.forward,HandleUtility.GetHandleSize(fall.transform.TransformPoint(curveHandle))*.12f,Handles.CubeHandleCap,0);
                if(EditorGUI.EndChangeCheck()){Undo.RecordObject(fall,"Courbure Z de la cascade");fall.bulge=Mathf.Max(0,fall.bulge+depth.z-curveHandle.z);Changed(fall);}
                Handles.color=Color.green;Handles.Label(fall.LandingLocalPosition+Vector3.up*.7f,"Point de chute (X / Y / Z)");
                EditorGUI.BeginChangeCheck();Vector3 landing=Handles.PositionHandle(fall.LandingLocalPosition,Quaternion.identity);
                if(EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(fall,"Deplacer le point de chute");
                    if(Mathf.Abs(landing.y-fall.LandingLocalPosition.y)>.001f){fall.matchWaterHeight=false;fall.height=Mathf.Max(.5f,-landing.y);}
                    fall.landingOffset=new Vector2(landing.x,landing.z);Changed(fall);
                }
            }
        }
        static void Changed(SonicWaterfall fall){fall.Refresh();EditorUtility.SetDirty(fall);PrefabUtility.RecordPrefabInstancePropertyModifications(fall);SceneView.RepaintAll();}
        internal static void ApplySceneBounds(SonicWaterfall fall,Vector3 center,Vector3 size)
        {
            float previousBottom=-fall.EffectiveHeight;
            float top=center.y+size.y*.5f,bottom=center.y-size.y*.5f;
            // Enforce minimum size while keeping the edge opposite the dragged face fixed.
            float left=center.x-size.x*.5f,right=center.x+size.x*.5f;
            if(size.x<.2f){if(Mathf.Abs(left+fall.width*.5f)>Mathf.Abs(right-fall.width*.5f))left=right-.2f;else right=left+.2f;}
            if(size.y<.5f){if(Mathf.Abs(top)>Mathf.Abs(bottom-previousBottom))top=bottom+.5f;else bottom=top-.5f;}
            // A freely dragged bottom intentionally overrides the water-height constraint.
            // Moving the top or sides keeps the basin link and automatic height enabled.
            if(Mathf.Abs(bottom-previousBottom)>.001f)fall.matchWaterHeight=false;
            fall.transform.position=fall.transform.TransformPoint(new Vector3((left+right)*.5f,top,0));
            fall.width=right-left;fall.height=top-bottom;fall.Refresh();
        }
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();var fall=(SonicWaterfall)target;
            EditorGUILayout.HelpBox("Place le pivot au SOMMET. Glisse l'objet Eau_GreenHill de ta scene dans Eau a l'arrivee : la hauteur s'adapte au bassin. Largeur et Force du courant sont reglables. Aucun collider a ajouter.",MessageType.Info);
            EditorGUILayout.HelpBox("Dans Scene, tire les petites poignees cyan a gauche, a droite, en haut ou en bas. Le bord oppose reste en place. Tirer le bas passe en hauteur manuelle ; recoche Ajuster la hauteur a l'eau pour rejoindre le bassin. Ctrl + Z annule.",MessageType.Info);
            EditorGUILayout.HelpBox("La poignee bleue sur le cote regle la profondeur/courbure Z. Au pied, Point de chute dispose des trois axes : la fleche bleue deplace l'arrivee sur Z. Courbure = 0 donne une chute droite entre le sommet et l'arrivee. Deplacer l'arrivee sur Y passe en hauteur manuelle.",MessageType.Info);
            if(GUILayout.Button("Chute droite (courbure a zero)")){Undo.RecordObject(fall,"Redresser la cascade");fall.bulge=0;Changed(fall);}
            if(GUILayout.Button("Relier le bassin sous le point de chute"))
            {
                SonicWaterVolume best=null;float highest=float.NegativeInfinity;
                foreach(var water in SonicWaterVolume.Active)
                {
                    if(water==null || water.transform.position.y>=fall.transform.position.y)continue;
                    Vector3 p=water.transform.InverseTransformPoint(fall.LandingPosition);
                    if(Mathf.Abs(p.x)<=water.width*.5f && Mathf.Abs(p.z)<=water.length*.5f && water.transform.position.y>highest){best=water;highest=water.transform.position.y;}
                }
                if(best!=null){Undo.RecordObject(fall,"Relier cascade");fall.receivingWater=best;fall.Refresh();EditorUtility.SetDirty(fall);PrefabUtility.RecordPrefabInstancePropertyModifications(fall);}
                else EditorUtility.DisplayDialog("Cascade","Aucun bassin trouve sous le pivot. Deplace la cascade au-dessus de l'eau ou attribue le bassin manuellement.","OK");
            }
            if(Vector3.Dot(fall.transform.up,Vector3.up)<.999f)EditorGUILayout.HelpBox("Garde Rotation X et Z a zero pour une chute verticale. Rotation Y change son orientation.",MessageType.Warning);
            if(fall.receivingWater!=null && !fall.HasWaterLanding)EditorGUILayout.HelpBox("Le bas de la cascade n'atteint pas le bassin choisi. Place son sommet au-dessus du bassin et active Ajuster la hauteur a l'eau.",MessageType.Warning);
            EditorGUILayout.LabelField("Hauteur actuelle",fall.EffectiveHeight.ToString("0.0"));
        }
    }
}
