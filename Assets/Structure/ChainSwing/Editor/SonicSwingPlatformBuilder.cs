using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace SonicFX.Structures.Editor
{
    [InitializeOnLoad]
    public static class SonicSwingPlatformBuilder
    {
        const string Folder="Assets/Structure/ChainSwing";
        const string PrefabPath="Assets/Structure/chain.prefab";
        const string Report="C:/Users/Lecle/Documents/Git/Sonic-FX/outputs/ChainSwing";
        static SonicSwingPlatformBuilder(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null)return;
            EditorApplication.update-=Ready;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if(prefab!=null && (prefab.GetComponent<SonicSwingPlatform>()==null || !File.Exists(Folder+"/Editor/Verified.txt")))Install();
            if(prefab!=null && prefab.GetComponent<SonicSwingPlatform>()!=null && !SessionState.GetBool("ChainSwing.PlatformSizeVerified.v1",false)){
                SessionState.SetBool("ChainSwing.PlatformSizeVerified.v1",true);VerifyPlatformSize();
            }
        }
        [MenuItem("Tools/Sonic FX/Structures/Verifier la taille de la plateforme chain")]
        public static void VerifyPlatformSize()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null)return;
            var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);GameObject go=null;
            const string report=Folder+"/Editor/PlatformSize_Verified.txt";
            try {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                go=Object.Instantiate(prefab);SceneManager.MoveGameObjectToScene(go,scene);
                go.transform.SetPositionAndRotation(new Vector3(0,100,0),Quaternion.Euler(0,38,0));go.transform.localScale=Vector3.one*.1f;
                var p=go.GetComponent<SonicSwingPlatform>();p.Rebuild();
                var initial=p.WorldPlatformSize;var initialScale=p.Platform.localScale;var length=p.WorldLength;var anchor=p.AnchorPosition;
                var chain=p.Links.GetComponent<MeshFilter>().sharedMesh;var chainScale=p.Links.localScale;var rest=p.RestPosition();
                var physical=Array.Find(p.Platform.GetComponents<BoxCollider>(),c=>!c.isTrigger);
                var trigger=Array.Find(p.Platform.GetComponents<BoxCollider>(),c=>c.isTrigger);
                Physics.SyncTransforms();float thickness=physical.bounds.size.y;
                p.SetWorldPlatformSize(new Vector2(initial.x*2,initial.y*.5f));Physics.SyncTransforms();
                Check(Vector2.Distance(p.WorldPlatformSize,new Vector2(initial.x*2,initial.y*.5f))<.001f,"Independent X/Z dimensions with rotated root");
                Check(Mathf.Abs(p.Platform.localScale.y-initialScale.y)<.0001f && Mathf.Abs(physical.bounds.size.y-thickness)<.001f,"Deck thickness unchanged");
                Check(p.WorldLength==length && p.AnchorPosition==anchor && p.Links.localScale==chainScale && p.Links.GetComponent<MeshFilter>().sharedMesh==chain,"Chain and anchor unchanged");
                Check(Vector3.Distance(rest,p.RestPosition())<.001f,"Resizing preserves platform centre and pendulum trajectory");
                var probe=p.Platform.TransformPoint(new Vector3(p.PlatformLocalBounds.size.x*.4f,p.PlatformLocalBounds.max.y,p.PlatformLocalBounds.center.z));
                Check(physical.Raycast(new Ray(probe+Vector3.up*2,Vector3.down),out _,4),"Expanded edge supports Sonic");
                Check(trigger.bounds.Contains(probe+Vector3.up*.1f),"Carry trigger covers expanded deck");
                for(int angle=-80;angle<=80;angle+=20){p.PlaceAtAngle(angle,false);Check(Vector3.Dot(p.Platform.up,Vector3.up)>.99999f,"Resized deck remains horizontal");}
                p.PlaceAtAngle(0,false);
                Undo.IncrementCurrentGroup();Undo.SetCurrentGroupName("Verifier taille chain");
                var before=p.WorldPlatformSize;
                Undo.RecordObjects(new Object[]{p,p.Platform},"Verifier taille chain");p.SetWorldPlatformSize(initial);Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();p.Rebuild();Check(Vector2.Distance(p.WorldPlatformSize,before)<.001f,"Undo restores platform dimensions");
                Undo.PerformRedo();p.Rebuild();Check(Vector2.Distance(p.WorldPlatformSize,initial)<.001f,"Redo restores platform dimensions");
                Undo.ClearUndo(p);Undo.ClearUndo(p.Platform);Undo.IncrementCurrentGroup();
                p.SetWorldPlatformSize(new Vector2(-1,0));Check(p.WorldPlatformSize.x>0 && p.WorldPlatformSize.y>0,"Minimum dimensions stay positive");
                p.SetWorldPlatformSize(initial);p.Rebuild();p.Rebuild();Check(Vector2.Distance(p.WorldPlatformSize,initial)<.001f,"Repeated rebuild does not compound scaling");
                var serial=new SerializedObject(p);Check(Vector2.Distance(serial.FindProperty("platformSizeScale").vector2Value,Vector2.one)<.0001f,"Default dimensions serialize correctly");
                File.WriteAllText(report,"PASS\nIndependent width/depth; chain unchanged; thickness unchanged; expanded edge collider and carry trigger; flat swing; Undo/Redo; positive minimum; stable rebuild.\n"+DateTime.Now.ToString("s"));
                Debug.Log("ChainSwing : taille de la plateforme, collisions et annulation verifies.");
            }catch(Exception e){File.WriteAllText(report,"FAIL\n"+e);Debug.LogException(e);}
            finally{if(go!=null)Object.DestroyImmediate(go);if(previous.IsValid() && previous.isLoaded)SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);AssetDatabase.ImportAsset(report);}
        }
        [MenuItem("Tools/Sonic FX/Structures/Installer et verifier chain balancante")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null){Debug.LogWarning("Quitte Play et le mode Prefab pour installer chain.");return;}
            Directory.CreateDirectory(Report);GameObject root=null;
            try {
                root=PrefabUtility.LoadPrefabContents(PrefabPath);
                var swing=root.GetComponent<SonicSwingPlatform>();
                if(swing==null){
                    var filter=root.GetComponent<MeshFilter>();var renderer=root.GetComponent<MeshRenderer>();
                    var floor=root.transform.Find("floor");
                    if(filter==null || filter.sharedMesh==null || floor==null)throw new Exception("chain : modele ou planche introuvable.");
                    var source=ReadMesh(filter.sharedMesh);var groups=Components(source);Bounds ballBounds=default,linkBounds=default;List<int> ball=null,link=null;float lastY=0;
                    foreach(var group in groups){
                        var b=BoundsOf(source,group);
                        if(Mathf.Abs(b.center.y)<.1f){ball=group;ballBounds=b;}
                        else if(link==null || b.center.y>linkBounds.center.y){link=group;linkBounds=b;}
                        lastY=Mathf.Min(lastY,b.center.y);
                    }
                    if(ball==null || link==null)throw new Exception("chain : boule d'attache ou maillon introuvable.");
                    var ballMesh=SaveMesh(Slice(source,ball,Vector3.zero,"Chain_Attache"),"Chain_Attache.asset");
                    var linkMesh=SaveMesh(Slice(source,link,linkBounds.center,"Chain_Maillon"),"Chain_Maillon.asset");
                    var top=CreateVisual("Attache",root.transform,ballMesh,renderer.sharedMaterials);
                    var chain=CreateVisual("Maillons",root.transform,null,renderer.sharedMaterials);
                    renderer.enabled=false;foreach(var c in root.GetComponents<Collider>())c.enabled=false;
                    var floorMesh=floor.GetComponent<MeshFilter>().sharedMesh;
                    var body=floor.GetComponent<Rigidbody>();if(body==null)body=floor.gameObject.AddComponent<Rigidbody>();
                    body.isKinematic=true;body.useGravity=false;body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
                    foreach(var c in floor.GetComponents<Collider>())c.enabled=false;
                    var physical=floor.gameObject.AddComponent<BoxCollider>();physical.center=floorMesh.bounds.center;physical.size=floorMesh.bounds.size;
                    var trigger=floor.gameObject.AddComponent<BoxCollider>();trigger.isTrigger=true;
                    float height=Mathf.Max(1,floorMesh.bounds.size.y*.15f);
                    trigger.center=new Vector3(floorMesh.bounds.center.x,floorMesh.bounds.max.y+height*.5f,floorMesh.bounds.center.z);trigger.size=new Vector3(floorMesh.bounds.size.x,height,floorMesh.bounds.size.z);
                    floor.gameObject.tag="MovingPlatform";
                    var movement=floor.gameObject.AddComponent<MovingPlatformControl>();movement.enabled=false;
                    swing=root.AddComponent<SonicSwingPlatform>();
                    swing.Initialize(linkMesh,null,top.transform,chain.transform,floor,(linkBounds.center.y-lastY)/4,linkBounds.center.y);
                    var rest=Object.Instantiate(chain.GetComponent<MeshFilter>().sharedMesh);rest.hideFlags=HideFlags.None;rest.name="Chain_Repose";
                    var restAsset=SaveMesh(rest,"Chain_Repose.asset");
                    var serial=new SerializedObject(swing);serial.FindProperty("restChainMesh").objectReferenceValue=restAsset;serial.ApplyModifiedPropertiesWithoutUndo();
                    chain.GetComponent<MeshFilter>().sharedMesh=restAsset;Object.DestroyImmediate(source);
                }
                Verify(root);
                swing.PlaceAtAngle(0,false);
                var serialized=new SerializedObject(swing);
                swing.Links.GetComponent<MeshFilter>().sharedMesh=(Mesh)serialized.FindProperty("restChainMesh").objectReferenceValue;
                if(PrefabUtility.SaveAsPrefabAsset(root,PrefabPath)==null)throw new Exception("chain : enregistrement du prefab impossible.");
                AssetDatabase.SaveAssets();Render(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
                File.WriteAllText(Folder+"/Editor/Verified.txt","Balancement, planche horizontale, longueur par maillon, collisions et transport de Sonic verifies.");AssetDatabase.ImportAsset(Folder+"/Editor/Verified.txt");
                File.WriteAllText(Path.Combine(Report,"unity-tests.txt"),"PASS\nOriginal model reused; link count and Y length; source geometry unchanged; deck stays horizontal throughout swing and with yaw; pendulum length preserved; solid floor raycast; existing Sonic platform transport; independent instances; zero amplitude / disabled swing; editor preview returns to rest.\n"+DateTime.Now.ToString("s"));
                Debug.Log("chain est prete : longueur Y, maillons et balancement reglables sur Sonic Swing Platform.");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Report,"unity-tests.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally{if(root!=null)PrefabUtility.UnloadPrefabContents(root);}
        }
        static GameObject CreateVisual(string name,Transform parent,Mesh mesh,Material[] materials)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=materials;return go;
        }
        static Mesh SaveMesh(Mesh mesh,string name)
        {
            string path=Folder+"/"+name;var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing!=null){Object.DestroyImmediate(mesh);return existing;}
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        internal static Mesh ReadMesh(Mesh original)
        {
            using(var array=MeshUtility.AcquireReadOnlyMeshData(original)){
                var data=array[0];var mesh=new Mesh{name="Chain_Source"};
                using(var v=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp)){data.GetVertices(v);mesh.SetVertices(v);}
                if(data.HasVertexAttribute(VertexAttribute.Normal))using(var n=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp)){data.GetNormals(n);mesh.SetNormals(n);}
                if(data.HasVertexAttribute(VertexAttribute.TexCoord0))using(var u=new NativeArray<Vector2>(data.vertexCount,Allocator.Temp)){data.GetUVs(0,u);mesh.SetUVs(0,u);}
                var triangles=new List<int>();for(int s=0;s<data.subMeshCount;s++)using(var ids=new NativeArray<int>(data.GetSubMesh(s).indexCount,Allocator.Temp)){data.GetIndices(ids,s,true);triangles.AddRange(ids.ToArray());}
                mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
            }
        }
        internal static List<List<int>> Components(Mesh mesh)
        {
            var vertices=mesh.vertices;var triangles=mesh.triangles;var parents=new int[vertices.Length];var welded=new Dictionary<Vector3Int,int>();
            for(int i=0;i<vertices.Length;i++){parents[i]=i;var key=Vector3Int.RoundToInt(vertices[i]*1000);if(welded.TryGetValue(key,out int p))parents[i]=p;else welded[key]=i;}
            Func<int,int> find=null;find=i=>parents[i]==i?i:(parents[i]=find(parents[i]));
            for(int j=0;j<triangles.Length;j+=3){parents[find(triangles[j+1])]=find(triangles[j]);parents[find(triangles[j+2])]=find(triangles[j]);}
            var groups=new Dictionary<int,List<int>>();for(int j=0;j<triangles.Length;j+=3){int p=find(triangles[j]);if(!groups.TryGetValue(p,out var group)){group=new List<int>();groups[p]=group;}group.Add(triangles[j]);group.Add(triangles[j+1]);group.Add(triangles[j+2]);}
            return new List<List<int>>(groups.Values);
        }
        internal static Bounds BoundsOf(Mesh m,List<int> indices){var v=m.vertices;var b=new Bounds(v[indices[0]],Vector3.zero);foreach(int i in indices)b.Encapsulate(v[i]);return b;}
        static Mesh Slice(Mesh source,List<int> triangles,Vector3 origin,string name)
        {
            var v=source.vertices;var n=source.normals;var u=source.uv;var vv=new List<Vector3>();var nn=new List<Vector3>();var uu=new List<Vector2>();var tt=new List<int>();var map=new Dictionary<int,int>();
            foreach(int i in triangles){if(!map.TryGetValue(i,out int j)){j=vv.Count;map[i]=j;vv.Add(v[i]-origin);if(n.Length==v.Length)nn.Add(n[i]);if(u.Length==v.Length)uu.Add(u[i]);}tt.Add(j);}
            var mesh=new Mesh{name=name};mesh.SetVertices(vv);mesh.SetTriangles(tt,0);if(nn.Count==vv.Count)mesh.SetNormals(nn);else mesh.RecalculateNormals();if(uu.Count==vv.Count)mesh.SetUVs(0,uu);mesh.RecalculateBounds();return mesh;
        }
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        internal static void Verify(GameObject prefab)
        {
            var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);GameObject go=null,other=null,player=null;
            try {
                go=Object.Instantiate(prefab);SceneManager.MoveGameObjectToScene(go,scene);go.transform.SetPositionAndRotation(new Vector3(0,100,0),Quaternion.Euler(0,38,0));go.transform.localScale=Vector3.one*.1f;
                var p=go.GetComponent<SonicSwingPlatform>();p.Rebuild();var source=p.LinkSource.vertices;float length=p.WorldLength;var floorScale=p.Platform.localScale;
                for(int i=-80;i<=80;i+=10){p.PlaceAtAngle(i,false);Check(Vector3.Dot(p.Platform.up,Vector3.up)>.99999f,"Deck remains horizontal");var radial=p.Platform.position-p.RestPosition()+Vector3.down*p.WorldLength;Check(Mathf.Abs(radial.magnitude-p.WorldLength)<.001f,"Pendulum radius stays constant");}
                p.PlaceAtAngle(0,false);Physics.SyncTransforms();var box=Array.Find(p.Platform.GetComponents<BoxCollider>(),c=>!c.isTrigger);
                Check(box!=null && box.Raycast(new Ray(box.bounds.center+Vector3.up*(box.bounds.extents.y+2),Vector3.down),out _,10),"Deck supports Sonic physically");
                p.SetWorldLength(length+5.5f);p.Rebuild();Check(p.linkCount==7 && p.WorldLength>length+5,"Y edit adds whole links");
                Check(p.Platform.localScale==floorScale,"Adding links does not stretch deck");Check(p.Links.GetComponent<MeshFilter>().sharedMesh.vertexCount==p.LinkSource.vertexCount*7,"Mesh includes requested link count");
                Check(p.LinkSource.vertices[0]==source[0],"Shared link source unchanged");
                other=Object.Instantiate(prefab);SceneManager.MoveGameObjectToScene(other,scene);other.transform.position=new Vector3(1000,1000,1000);
                Check(other.GetComponent<SonicSwingPlatform>().linkCount==5,"Other instances retain length");
                p.amplitude=0;Check(p.AngleAt(1)==0,"Zero amplitude stays still");p.amplitude=25;p.swingEnabled=false;Check(p.AngleAt(1)==0,"Disabled swing stays still");p.swingEnabled=true;
                p.phase=0;Check(Mathf.Abs(p.AngleAt(p.period*.25f)-25)<.001f,"Period reaches full amplitude at quarter cycle");
                p.direction=90;Check(Vector3.Dot(p.SwingDirection,go.transform.forward)>.999f,"Direction follows local Z and object yaw");
                player=new GameObject("Sonic transport verification");SceneManager.MoveGameObjectToScene(player,scene);
                var body=player.AddComponent<Rigidbody>();body.useGravity=false;
                var physics=player.AddComponent<PlayerBhysics>();physics.Grounded=true;physics.Playermask=~0;var interaction=player.AddComponent<Objects_Interaction>();interaction.Player=physics;
                var trigger=Array.Find(p.Platform.GetComponents<BoxCollider>(),c=>c.isTrigger);Check(trigger!=null && p.Platform.CompareTag("MovingPlatform") && !p.Movement.enabled,"Existing platform transport adapter installed");
                body.position=box.bounds.center+Vector3.up*(box.bounds.extents.y+.5f);Physics.SyncTransforms();
                interaction.OnTriggerStay(trigger);var before=body.position;var old=p.Platform.position;p.PlaceAtAngle(10,true);var delta=p.Platform.position-old;
                typeof(Objects_Interaction).GetMethod("FixedUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(interaction,null);
                Check(Vector3.Distance(body.position-before,delta)<.001f,"Sonic follows deck displacement once, without legacy double transport");
                p.carryPlayer=false;p.Rebuild();Check(!trigger.enabled,"Player carry can be disabled");
                p.carryPlayer=true;p.Rebuild();p.PlaceAtAngle(0,false);Check(Vector3.Distance(p.Platform.position,p.RestPosition())<.001f,"Preview returns to rest");
            }
            finally{if(go!=null)Object.DestroyImmediate(go);if(other!=null)Object.DestroyImmediate(other);if(player!=null)Object.DestroyImmediate(player);if(previous.IsValid() && previous.isLoaded)SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}
        }
        internal static void Render(GameObject prefab)
        {
            var preview=new PreviewRenderUtility();Texture2D texture=null;
            try {
                var go=Object.Instantiate(prefab);go.transform.position=Vector3.zero;go.transform.localScale=Vector3.one*.1f;preview.AddSingleGO(go);
                var p=go.GetComponent<SonicSwingPlatform>();p.PlaceAtAngle(25,false);var bounds=new Bounds(p.AnchorPosition,Vector3.zero);
                // Renderer bounds can still reflect the FBX pose immediately after a preview move.
                foreach(var f in go.GetComponentsInChildren<MeshFilter>()){
                    if(!f.GetComponent<Renderer>().enabled || f.sharedMesh==null)continue;
                    var b=f.sharedMesh.bounds;
                    for(int i=0;i<8;i++)bounds.Encapsulate(f.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
                }
                float size=bounds.size.magnitude;preview.camera.orthographic=true;preview.camera.orthographicSize=size*.6f;
                preview.camera.transform.position=bounds.center+new Vector3(1,.3f,-1).normalized*size*2;preview.camera.transform.LookAt(bounds.center);
                preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=size*10+100;preview.camera.clearFlags=CameraClearFlags.Color;preview.camera.backgroundColor=new Color(.08f,.12f,.18f);preview.ambientColor=Color.gray;
                preview.lights[0].intensity=1.5f;preview.lights[0].transform.rotation=Quaternion.Euler(45,-30,0);preview.lights[1].intensity=.8f;
                preview.BeginStaticPreview(new Rect(0,0,900,900));preview.Render();texture=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(Report,"chain-preview.png"),texture.EncodeToPNG());
            }finally{if(texture!=null)Object.DestroyImmediate(texture);preview.Cleanup();}
        }
    }
}
