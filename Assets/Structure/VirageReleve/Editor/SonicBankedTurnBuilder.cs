using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SonicFX.Structures.Editor
{
    [InitializeOnLoad]
    public static class SonicBankedTurnBuilder
    {
        public const string Folder="Assets/Structure/VirageReleve";
        public const string PrefabPath=Folder+"/Virage_Releve_GreenHill.prefab";
        public static readonly string Reports=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/SonicBankedTurn");
        static SonicBankedTurnBuilder(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)return;
            EditorApplication.update-=Ready;if(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)==null || !File.Exists(Folder+"/Editor/ExtensionsVerified.txt"))Build();
        }
        [MenuItem("Tools/Sonic FX/Structures/Creer et verifier le virage releve")]
        public static void Build()
        {
            Directory.CreateDirectory(Reports);GameObject go=null;
            try
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/BumperEngineV1/Models/Materials/GreenHillTile.mat");
                if(material==null)throw new Exception("Materiau GreenHillTile introuvable.");
                if(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)==null)
                {
                    go=new GameObject("Virage_Releve_GreenHill");var turn=go.AddComponent<SonicBankedTurn>();
                    var mesh=turn.CreateMesh();AssetDatabase.CreateAsset(mesh,Folder+"/Virage_Defaut.asset");
                    go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshCollider>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;
                    PrefabUtility.SaveAsPrefabAsset(go,PrefabPath);AssetDatabase.SaveAssets();
                }
                Verify();VerifyExtensions();Preview();
                File.WriteAllText(Folder+"/Editor/InteriorFillVerified.txt","Remplissage interieur verifie.");AssetDatabase.ImportAsset(Folder+"/Editor/InteriorFillVerified.txt");
                File.WriteAllText(Folder+"/Editor/SmoothSidesVerified.txt","Lissage lateral verifie.");AssetDatabase.ImportAsset(Folder+"/Editor/SmoothSidesVerified.txt");
                File.WriteAllText(Folder+"/Editor/ExtensionsVerified.txt","Prolongements entree et sortie verifies.");AssetDatabase.ImportAsset(Folder+"/Editor/ExtensionsVerified.txt");
                Debug.Log("Virage pret : "+PrefabPath);
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Reports,"unity-tests.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally{if(go!=null)UnityEngine.Object.DestroyImmediate(go);}
        }
        static void Check(bool condition,string label){if(!condition)throw new Exception(label);}
        static void Verify()
        {
            GameObject go=null;
            try
            {
                go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));go.hideFlags=HideFlags.HideAndDontSave;
                go.transform.position=new Vector3(10000,1000,10000);var turn=go.GetComponent<SonicBankedTurn>();
                foreach(bool smooth in new[]{false,true})
                foreach(bool fill in new[]{false,true})
                foreach(var direction in new[]{SonicBankedTurn.TurnDirection.Droite,SonicBankedTurn.TurnDirection.Gauche})
                {
                    turn.smoothSides=smooth;turn.fillInterior=fill;turn.direction=direction;turn.Rebuild();Physics.SyncTransforms();
                    var mesh=go.GetComponent<MeshFilter>().sharedMesh;var collider=go.GetComponent<MeshCollider>();
                    Check(mesh==collider.sharedMesh && !collider.convex && !collider.isTrigger,"Continuous non-convex solid collider");
                    Vector3[] vertices=mesh.vertices,normals=mesh.normals;int[] triangles=mesh.triangles;
                    Check(vertices.Length>1000 && mesh.uv.Length==vertices.Length,"Detailed mesh and complete UVs");
                    for(int i=0;i<triangles.Length;i+=3)
                    {
                        int a=triangles[i],b=triangles[i+1],c=triangles[i+2];Vector3 cross=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);
                        Check(cross.sqrMagnitude>1e-12f,"No degenerate collision triangle");
                        Check(Vector3.Dot(cross,normals[a]+normals[b]+normals[c])>0,"Consistent surface winding and normals");
                    }
                    float theta=turn.angle*Mathf.Deg2Rad*.5f;
                    foreach(float radius in new[]{turn.innerRadius*.25f,turn.innerRadius-.05f,turn.innerRadius+.05f})
                    {
                        Vector3 p=go.transform.TransformPoint(turn.Point(theta,radius,0));
                        bool found=collider.Raycast(new Ray(p+Vector3.up,Vector3.down),out var filledHit,1.5f);
                        bool expect=fill || radius>turn.innerRadius;
                        Check(found==expect,"Interior floor exists only when requested");
                        if(found)Check(Mathf.Abs(filledHit.point.y-p.y)<.002f && filledHit.normal.y>.999f,"No height step or slope across old inner edge");
                    }
                    Vector3 inner=go.transform.TransformPoint(turn.Point(theta,turn.innerRadius-1,-turn.baseDepth*.5f));
                    Vector3 outward=new Vector3(-turn.Sign*Mathf.Cos(theta),0,Mathf.Sin(theta));
                    bool innerWall=collider.Raycast(new Ray(inner,outward),out _,2);
                    Check(innerWall==!fill,"Filled interior removes the old vertical collision wall");
                    foreach(float q in new[]{0f,.2f,.5f,.8f,.97f})
                    {
                        float phi=q*Mathf.PI*.5f;
                        float r=turn.innerRadius+turn.flatWidth+turn.rampWidth*Mathf.Sin(phi),y=turn.wallHeight*(1-Mathf.Cos(phi));
                        Vector3 p=go.transform.TransformPoint(turn.Point(theta,r,y));
                        Vector3 n=new Vector3(turn.Sign*Mathf.Cos(theta)*turn.wallHeight*Mathf.Sin(phi),turn.rampWidth*Mathf.Cos(phi),-Mathf.Sin(theta)*turn.wallHeight*Mathf.Sin(phi)).normalized;
                        Check(collider.Raycast(new Ray(p+n*2,-n),out var hit,3),"Raycast reaches curved riding surface");
                        Check(Vector3.Dot(hit.normal,n)>.98f,"Physical slope follows smooth profile");
                    }
                    Vector3 flat=go.transform.TransformPoint(turn.EntryPosition+Vector3.forward*.1f);
                    Check(collider.Raycast(new Ray(flat+Vector3.up*2,Vector3.down),out var ground,3) && ground.normal.y>.999f,"Flat entrance collider");
                    if(smooth)
                    {
                        foreach(float end in new[]{0f,1f})foreach(float u in new[]{.2f,.7f,1f})
                        {
                            turn.EvaluateRamp(end*turn.angle*Mathf.Deg2Rad,u,out var edge,out var edgeNormal);
                            Check(Mathf.Abs(edge.y)<.00001f && edgeNormal.y>.99999f,"Both openings are flat with horizontal tangent");
                        }
                        foreach(float along in new[]{.002f,.15f,.85f,.998f})foreach(float u in new[]{.3f,.8f})
                        {
                            turn.EvaluateRamp(along*turn.angle*Mathf.Deg2Rad,u,out var sample,out var expectedNormal);
                            Vector3 p=go.transform.TransformPoint(sample);
                            Check(collider.Raycast(new Ray(p+expectedNormal*2,-expectedNormal),out var hit,3),"Continuous collision through eased side transitions");
                            Check(Vector3.Dot(hit.normal,expectedNormal)>.97f,"Eased collision agrees with analytic normals");
                        }
                        turn.EvaluateRamp(turn.angle*Mathf.Deg2Rad*.5f,1,out var peak,out _);Check(Mathf.Abs(peak.y-turn.wallHeight)<.001f,"Central wall height retained");
                    }
                }
                turn.angle=180;turn.wallHeight=15;turn.rampWidth=12;turn.Rebuild();Check(go.GetComponent<MeshFilter>().sharedMesh.bounds.max.y>=14.99f,"Edited dimensions rebuild mesh");
                var previous=go.GetComponent<MeshFilter>().sharedMesh;turn.enabled=false;turn.enabled=true;turn.Rebuild();Check(go.GetComponent<MeshFilter>().sharedMesh!=null && go.GetComponent<MeshFilter>().sharedMesh!=previous,"Mesh regenerated on re-enable");
                File.WriteAllText(Path.Combine(Reports,"unity-tests.txt"),"PASS\nSmooth sides on/off, left/right, filled/open: valid triangles, UVs and normals; continuous collider; flat openings and horizontal endpoint tangents when smoothed; raycast normals through both side transitions; central wall height retained; filled interior and removal of its old wall; dimensions and lifecycle.\n");
            }
            finally{if(go!=null)UnityEngine.Object.DestroyImmediate(go);}
        }
        static void Preview()
        {
            var preview=new PreviewRenderUtility();GameObject go=null;Texture2D image=null;
            try
            {
                go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));var turn=go.GetComponent<SonicBankedTurn>();turn.fillInterior=true;turn.smoothSides=true;turn.entryLength=12;turn.exitLength=18;turn.Rebuild();preview.AddSingleGO(go);
                Vector3 center=go.GetComponent<MeshFilter>().sharedMesh.bounds.center;
                preview.camera.fieldOfView=42;preview.camera.transform.position=center+new Vector3(65,55,-75);preview.camera.transform.LookAt(center);preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=250;
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.12f,.19f,.25f);
                preview.lights[0].intensity=1.3f;preview.lights[0].transform.rotation=Quaternion.Euler(48,-30,0);preview.lights[1].intensity=.8f;preview.ambientColor=new Color(.65f,.65f,.65f);
                preview.BeginStaticPreview(new Rect(0,0,1100,800));preview.Render();image=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(Reports,"Virage_Prolongements.png"),image.EncodeToPNG());
            }
            finally{if(image!=null)UnityEngine.Object.DestroyImmediate(image);preview.Cleanup();if(go!=null)UnityEngine.Object.DestroyImmediate(go);}
        }
        static void VerifyExtensions()
        {
            GameObject go=null;
            try {
                go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));go.transform.position=new Vector3(10000,1000,10000);
                var turn=go.GetComponent<SonicBankedTurn>();var collider=go.GetComponent<MeshCollider>();
                foreach(var direction in new[]{SonicBankedTurn.TurnDirection.Droite,SonicBankedTurn.TurnDirection.Gauche})
                foreach(bool fill in new[]{false,true})foreach(bool smooth in new[]{false,true})foreach(float angle in new[]{45f,90f,180f}) {
                    turn.direction=direction;turn.fillInterior=fill;turn.smoothSides=smooth;turn.angle=angle;turn.entryLength=7;turn.exitLength=11;turn.Rebuild();Physics.SyncTransforms();
                    var mesh=go.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;var normals=mesh.normals;var indices=mesh.triangles;
                    Check(mesh==collider.sharedMesh,"Extended collision matches visual mesh");
                    for(int i=0;i<indices.Length;i+=3) {
                        int a=indices[i],b=indices[i+1],c=indices[i+2];Vector3 cross=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);
                        Check(cross.sqrMagnitude>1e-12f,"No degenerate extension triangle");Check(Vector3.Dot(cross,normals[a]+normals[b]+normals[c])>0,"Extension normals and winding");
                    }
                    foreach(bool exit in new[]{false,true}) {
                        float theta=exit?angle*Mathf.Deg2Rad:0;Vector3 forward=exit?turn.ExitForward:Vector3.forward;float length=exit?turn.exitLength:turn.entryLength,sign=exit?1:-1;
                        foreach(float fraction in new[]{.02f,.5f,.98f})foreach(float u in new[]{0f,.4f,.85f}) {
                            turn.EvaluateRamp(theta,u,out var local,out var normal);local+=forward*(sign*length*fraction);
                            Vector3 world=go.transform.TransformPoint(local);Check(collider.Raycast(new Ray(world+normal*2,-normal),out var hit,3),"Riding collision on straight extension");Check(Vector3.Dot(normal,hit.normal)>.98f,"Extension keeps smooth profile");
                        }
                        Vector3 join=turn.Point(theta,turn.innerRadius+turn.flatWidth*.5f,0);
                        foreach(float offset in new[]{-.05f,.05f}) {Vector3 p=go.transform.TransformPoint(join+forward*offset);Check(collider.Raycast(new Ray(p+Vector3.up,Vector3.down),out var h,2)&&Mathf.Abs(h.point.y-p.y)<.002f,"No height step at join");}
                        Check(!collider.Raycast(new Ray(go.transform.TransformPoint(join-forward*.1f+Vector3.down*.5f),forward),out _, .2f),"No hidden end cap across old join");
                        Vector3 interior=go.transform.TransformPoint(turn.Point(theta,turn.innerRadius*.5f,0)+forward*(sign*length*.5f));
                        Check(collider.Raycast(new Ray(interior+Vector3.up,Vector3.down),out _,2)==fill,"Interior fill extends when enabled");
                    }
                    var entry=turn.EntryPosition;var oldExit=turn.ExitPosition;turn.exitLength+=3;turn.Rebuild();Check(Vector3.Distance(entry,turn.EntryPosition)<.0001f,"Exit length does not move entry");Check(Vector3.Distance(turn.ExitPosition,oldExit+turn.ExitForward*3)<.0001f,"Exit grows along tangent");
                }
                turn.entryLength=turn.exitLength=0;turn.Rebuild();Check(turn.EntryPosition==turn.Point(0,turn.innerRadius+turn.flatWidth*.5f,0),"Zero length preserves old entry");
                turn.entryLength=5;turn.exitLength=0;turn.Rebuild();Check(go.GetComponent<MeshFilter>().sharedMesh!=null,"Entry-only extension");turn.entryLength=0;turn.exitLength=5;turn.Rebuild();Check(go.GetComponent<MeshFilter>().sharedMesh!=null,"Exit-only extension");
                File.AppendAllText(Path.Combine(Reports,"unity-tests.txt"),"PASS EXTENSIONS\nIndependent entry/exit lengths, left/right, 45/90/180 degrees, fill and smoothing on/off: valid geometry and normals; solid riding collider; continuous join heights; no internal end caps; interior fill follows extensions; zero and one-sided lengths.\n"+DateTime.Now.ToString("s"));
            }finally{if(go!=null)UnityEngine.Object.DestroyImmediate(go);}
        }
        [MenuItem("GameObject/Sonic FX/Virage releve",false,12)]
        static void Add()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);if(prefab==null){Build();prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);}if(prefab==null)return;
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);Undo.RegisterCreatedObjectUndo(go,"Ajouter virage releve");
            if(SceneView.lastActiveSceneView!=null)go.transform.position=SceneView.lastActiveSceneView.pivot;Selection.activeGameObject=go;
        }
    }
    [CustomEditor(typeof(SonicBankedTurn))]
    public class SonicBankedTurnEditor:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();DrawDefaultInspector();var turn=(SonicBankedTurn)target;
            if(EditorGUI.EndChangeCheck())turn.Rebuild();
            if(turn.smoothSides)EditorGUILayout.HelpBox("L'entree et la sortie redescendent au niveau du sol, sans cassure de pente. Etendue du lissage = portion de chaque cote utilisee pour la transition (0.3 = 30 %). Le mur conserve sa hauteur au centre. La collision suit cette forme.",MessageType.Info);
            if(!turn.fillInterior && GUILayout.Button("Combler l'interieur sans rebord"))
            {
                Undo.RecordObject(turn,"Combler le trou interieur");turn.fillInterior=true;Changed(turn);
            }
            if(turn.fillInterior)EditorGUILayout.HelpBox("Le sol interieur fait maintenant partie du virage, a la meme hauteur et avec la meme collision. Retire les petits blocs de remplissage que tu avais ajoutes s'ils se superposent a ce sol.",MessageType.Info);
            EditorGUILayout.HelpBox("Entree dans la direction de la fleche verte (+Z). Le sol reste plat a l'interieur ; la pente rejoint progressivement le mur exterieur. Utilise les dimensions ci-dessus plutot que Scale. Aucun Rigidbody a ajouter. Le Mesh Collider est deja actif.",MessageType.Info);
            EditorGUILayout.HelpBox("Longueur de l'entree / de la sortie prolongent la piste separement. Dans Scene : cubes vert et jaune = longueurs ; poignee bleue = hauteur du mur ; cyan = largeur de la pente. Ctrl + Z annule.",MessageType.Info);
        }
        void OnSceneGUI()
        {
            var turn=(SonicBankedTurn)target;float mid=turn.angle*Mathf.Deg2Rad*.5f;
            using(new Handles.DrawingScope(turn.transform.localToWorldMatrix))
            {
                Handles.color=Color.green;Handles.ArrowHandleCap(0,turn.EntryPosition,Quaternion.identity,3,EventType.Repaint);Handles.Label(turn.EntryPosition+Vector3.up,"Entree");
                Handles.color=Color.yellow;Handles.ArrowHandleCap(0,turn.ExitPosition,Quaternion.LookRotation(turn.ExitForward),3,EventType.Repaint);Handles.Label(turn.ExitPosition+Vector3.up,"Sortie");
                ExtensionHandle(turn,false);ExtensionHandle(turn,true);
                Vector3 top=turn.Point(mid,turn.OuterRadius,turn.wallHeight);Handles.color=new Color(.2f,.55f,1);
                EditorGUI.BeginChangeCheck();Vector3 raised=Handles.Slider(top,Vector3.up);
                if(EditorGUI.EndChangeCheck()){Undo.RecordObject(turn,"Hauteur du mur");turn.wallHeight=Mathf.Max(1,raised.y);Changed(turn);}
                Vector3 foot=turn.Point(mid,turn.OuterRadius,0),outward=new Vector3(-turn.Sign*Mathf.Cos(mid),0,Mathf.Sin(mid));Handles.color=Color.cyan;
                EditorGUI.BeginChangeCheck();Vector3 widened=Handles.Slider(foot,outward);
                if(EditorGUI.EndChangeCheck()){Undo.RecordObject(turn,"Largeur de la pente");turn.rampWidth=Mathf.Max(1,turn.rampWidth+Vector3.Dot(widened-foot,outward));Changed(turn);}
            }
        }
        static void ExtensionHandle(SonicBankedTurn turn,bool exit)
        {
            Vector3 point=(exit?turn.ExitPosition:turn.EntryPosition)+Vector3.up*.6f,forward=exit?turn.ExitForward:Vector3.back;
            Handles.color=exit?Color.yellow:Color.green;
            float size=HandleUtility.GetHandleSize(turn.transform.TransformPoint(point))*.1f;
            EditorGUI.BeginChangeCheck();Vector3 moved=Handles.Slider(point,forward,size,Handles.CubeHandleCap,0);
            if(EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(turn,exit?"Etendre la sortie":"Etendre l'entree");float delta=Vector3.Dot(moved-point,forward);
                if(exit)turn.exitLength=Mathf.Max(0,turn.exitLength+delta);else turn.entryLength=Mathf.Max(0,turn.entryLength+delta);Changed(turn);
            }
        }
        static void Changed(SonicBankedTurn turn){turn.Rebuild();EditorUtility.SetDirty(turn);PrefabUtility.RecordPrefabInstancePropertyModifications(turn);}
    }
}
