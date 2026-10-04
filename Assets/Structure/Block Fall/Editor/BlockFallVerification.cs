using System;
using System.IO;
using System.Reflection;
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class BlockFallVerification
{
    const string Folder="Assets/Structure/Block Fall";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static BlockFallVerification(){EditorApplication.update+=Ready;}
    static void Ready()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        EditorApplication.update-=Ready;if(!File.Exists(Folder+"/Editor/ImpactV2Verified.txt"))Verify();
    }
    [MenuItem("Sonic FX/Structures/Verifier destruction BlockFall")]
    public static void Verify()
    {
        string report=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/BlockFall/tests.txt");
        GameObject block=null,ground=null,player=null;TerrainData terrainData=null;
        try {
            block=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/BlockFall.prefab"));block.transform.position=Vector3.one*35000;
            var script=block.GetComponent<FallingPlatform>();Check(script!=null,"Script conserve sur le prefab");var type=typeof(FallingPlatform);type.GetMethod("Awake",Private).Invoke(script,null);
            var rb=block.GetComponent<Rigidbody>();Check(rb.isKinematic&&!rb.useGravity,"Avant declenchement, plateforme immobile");
            ground=new GameObject("Impact_Test");var obstacle=ground.AddComponent<BoxCollider>();
            bool Should(Collider c)=>(bool)type.GetMethod("ShouldBreak",Private).Invoke(script,new object[]{c});
            Check(!Should(obstacle),"Contact avant la chute ne detruit pas");
            var countdown=(IEnumerator)type.GetMethod("FallAfterDelay",Private).Invoke(script,null);Check(countdown.MoveNext()&&countdown.Current is WaitForSeconds,"Delai conserve");Check(!Should(obstacle),"Pas de destruction pendant le delai");
            Check(!countdown.MoveNext(),"Fin du delai");Check(!rb.isKinematic&&rb.useGravity&&rb.collisionDetectionMode==CollisionDetectionMode.ContinuousDynamic,"Chute et detection continue");
            Check(!Should(obstacle),"Contact initial apres delai ne detruit pas");rb.position+=Vector3.down*.1f;Check(!Should(obstacle),"Petite correction physique ignoree");rb.position+=Vector3.down*.3f;
            Check(Should(obstacle),"Bloc solide detruit apres la chute");obstacle.isTrigger=true;Check(!Should(obstacle),"Trigger ignore");obstacle.isTrigger=false;
            player=new GameObject("Sonic_Test");player.tag="Player";var child=new GameObject("Collider_Enfant");child.transform.SetParent(player.transform);var pc=child.AddComponent<BoxCollider>();Check(!Should(pc),"Sonic enfant ignore");
            var isPlayer=type.GetMethod("IsPlayer",BindingFlags.Static|BindingFlags.NonPublic);Check((bool)isPlayer.Invoke(null,new object[]{pc}),"Sonic peut declencher le delai");Check(!(bool)isPlayer.Invoke(null,new object[]{obstacle}),"Terrain ne declenche pas le delai");
            player.tag="Untagged";player.AddComponent<PlayerBhysics>().enabled=false;Check(!Should(pc),"Sonic reconnu par controleur");Check(!Should(block.GetComponent<Collider>()),"Propre collision ignoree");
            type.GetField("impactLayers",Private).SetValue(script,(LayerMask)0);Check(!Should(obstacle),"Couches exclues ignorees");type.GetField("impactLayers",Private).SetValue(script,(LayerMask)(~0));
            Object.DestroyImmediate(obstacle);terrainData=new TerrainData();terrainData.heightmapResolution=33;terrainData.size=Vector3.one*10;var terrain=ground.AddComponent<TerrainCollider>();terrain.terrainData=terrainData;Check(Should(terrain),"Terrain detruit apres la chute");
            type.GetField("destroyed",Private).SetValue(script,true);Check(!Should(terrain),"Impact unique");
            VerifyPhysicalFall();
            File.WriteAllText(report,"PASS V2\nPrefab binding; Sonic-only trigger; initial contact after delay and 0.1-unit settling ignored; actual descent arms impact; players/triggers excluded; layers; duplicate impact; isolated physics fall beside ledge lands on solid floor without disabled collisions.\n"+DateTime.Now.ToString("s"));
            File.WriteAllText(Folder+"/Editor/ImpactV2Verified.txt","Contacts initiaux et chute reelle verifies");AssetDatabase.ImportAsset(Folder+"/Editor/ImpactV2Verified.txt");
        }catch(Exception e){File.WriteAllText(report,"FAIL\n"+e);Debug.LogException(e);}
        finally{if(block!=null)Object.DestroyImmediate(block);if(ground!=null)Object.DestroyImmediate(ground);if(player!=null)Object.DestroyImmediate(player);if(terrainData!=null)Object.DestroyImmediate(terrainData);}
    }
    static void VerifyPhysicalFall()
    {
        var scene=EditorSceneManager.NewPreviewScene();var physics=scene.GetPhysicsScene();GameObject block=null,floor=null,edge=null;
        try {
            Check(physics.IsValid()&&physics!=Physics.defaultPhysicsScene,"Physique isolee");
            block=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/BlockFall.prefab"));SceneManager.MoveGameObjectToScene(block,scene);block.transform.position=new Vector3(1.51f,5,0);
            var rb=block.GetComponent<Rigidbody>();rb.position=block.transform.position;var script=block.GetComponent<FallingPlatform>();var type=typeof(FallingPlatform);type.GetMethod("Awake",Private).Invoke(script,null);
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(floor,scene);floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(20,1,20);
            edge=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(edge,scene);edge.transform.position=new Vector3(0,2.5f,0);edge.transform.localScale=new Vector3(2,5,4);Physics.SyncTransforms();
            var fall=(IEnumerator)type.GetMethod("FallAfterDelay",Private).Invoke(script,null);fall.MoveNext();fall.MoveNext();
            var method=type.GetMethod("ShouldBreak",Private);Check(!(bool)method.Invoke(script,new object[]{edge.GetComponent<Collider>()}),"Bord initial ignore");
            physics.Simulate(.02f);Check(!(bool)method.Invoke(script,new object[]{edge.GetComponent<Collider>()}),"Pas de destruction au premier pas");
            for(int i=0;i<180;i++)physics.Simulate(.02f);
            Check(rb.position.y>.4f&&rb.position.y<.7f,"Atterrissage sur le sol, collisions conservees");Check((bool)method.Invoke(script,new object[]{floor.GetComponent<Collider>()}),"Impact arme apres vraie chute");
        }finally{if(block!=null)Object.DestroyImmediate(block);if(floor!=null)Object.DestroyImmediate(floor);if(edge!=null)Object.DestroyImmediate(edge);EditorSceneManager.ClosePreviewScene(scene);}
    }
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
}
