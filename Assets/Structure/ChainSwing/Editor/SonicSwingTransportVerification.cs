using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace SonicFX.Structures.Editor
{
    [InitializeOnLoad]
    public static class SonicSwingTransportVerification
    {
        const string Version="ChainSwing.Transport.v3";
        const string Report="C:/Users/Lecle/Documents/Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/ChainSwingFix/unity-transport-report.txt";
        static SonicSwingTransportVerification(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null)return;
            EditorApplication.update-=Ready;
            if(SessionState.GetBool(Version,false))return;
            SessionState.SetBool(Version,true);Verify();
        }
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        [MenuItem("Tools/Sonic FX/Structures/Verifier le transport de Sonic sur chain")]
        public static void Verify()
        {
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewPreviewScene();
            GameObject root=null,sonic=null,obstacle=null;
            try{
                root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Structure/chain.prefab"));
                SceneManager.MoveGameObjectToScene(root,scene);
                root.transform.SetPositionAndRotation(new Vector3(0,100,0),Quaternion.Euler(0,38,0));root.transform.localScale=Vector3.one*.1f;
                var swing=root.GetComponent<SonicSwingPlatform>();swing.Rebuild();swing.PlaceAtAngle(0,false);
                Physics.SyncTransforms();
                var deck=Array.Find(swing.Platform.GetComponents<BoxCollider>(),c=>c.enabled && !c.isTrigger);
                var trigger=Array.Find(swing.Platform.GetComponents<BoxCollider>(),c=>c.isTrigger);
                var physics=scene.GetPhysicsScene();
                Check(physics!=Physics.defaultPhysicsScene,"Verification must use an isolated physics scene");
                sonic=new GameObject("Sonic physics probe");SceneManager.MoveGameObjectToScene(sonic,scene);sonic.layer=2;
                var body=sonic.AddComponent<Rigidbody>();body.constraints=RigidbodyConstraints.FreezeRotation;body.useGravity=true;
                var capsule=sonic.AddComponent<CapsuleCollider>();capsule.radius=.5f;capsule.height=1;
                var player=sonic.AddComponent<PlayerBhysics>();player.p_rigidbody=body;player.Playermask=1<<deck.gameObject.layer;player.Grounded=true;
                var actions=sonic.AddComponent<ActionManager>();actions.Action=0;
                var interaction=sonic.AddComponent<Objects_Interaction>();interaction.Player=player;
                var legacyStep=typeof(Objects_Interaction).GetMethod("FixedUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
                var origin=deck.bounds.center+Vector3.up*(deck.bounds.extents.y+.5f);
                body.position=origin;Physics.SyncTransforms();
                const float dt=1f/60;
                float maxYSpeed=0,maxGap=0,maxDrift=0;
                var offset=origin-swing.PlatformBody.position;
                for(int frame=1;frame<=1440;frame++){
                    interaction.OnTriggerStay(trigger);
                    swing.PlaceAtAngle(swing.AngleAt(frame*dt),true);
                    legacyStep.Invoke(interaction,null);
                    physics.Simulate(dt);
                    var error=body.position-swing.PlatformBody.position-offset;
                    maxYSpeed=Mathf.Max(maxYSpeed,body.linearVelocity.y);maxGap=Mathf.Max(maxGap,Mathf.Abs(error.y));
                    maxDrift=Mathf.Max(maxDrift,new Vector2(error.x,error.z).magnitude);
                    player.Grounded=physics.Raycast(body.position+Vector3.up*2,Vector3.down,out var hit,2.55f,player.Playermask,QueryTriggerInteraction.Ignore) && hit.collider==deck;
                    Check(player.Grounded,"Ground contact lost at frame "+frame+"; error="+error+"; velocity="+body.linearVelocity);
                }
                Check(maxGap<.025f && maxDrift<.025f && maxYSpeed<.1f,"Pendulum must not bounce/slide Sonic: gap="+maxGap+", drift="+maxDrift+", upward velocity="+maxYSpeed);
                Check(swing.Movement.TranslateVector==Vector3.zero,"Legacy transport must not publish a duplicate displacement");
                Check(Vector3.Dot(swing.Platform.up,Vector3.up)>.9999f,"Board must stay horizontal");

                // Preserve the character's input velocity while the deck changes direction.
                var before=body.position;var deckBefore=swing.PlatformBody.position;
                body.linearVelocity=Vector3.forward*2;
                for(int frame=1;frame<=30;frame++){
                    interaction.OnTriggerStay(trigger);swing.PlaceAtAngle(swing.AngleAt(24+frame*dt),true);
                    legacyStep.Invoke(interaction,null);physics.Simulate(dt);
                    Check(Mathf.Abs(body.linearVelocity.z-2)<.01f,"Deck steals walking velocity");
                }
                var walked=(body.position-before)-(swing.PlatformBody.position-deckBefore);
                Check(Vector3.Distance(walked,Vector3.forward)<.025f,"Walking must add its own displacement: "+walked);

                // Jump starts in Update while Grounded can still be true: Action=1 must release it immediately.
                before=body.position;actions.Action=1;player.Grounded=true;body.linearVelocity=Vector3.up*8;
                interaction.OnTriggerStay(trigger);swing.PlaceAtAngle(-10,true);legacyStep.Invoke(interaction,null);
                Check(Vector3.Distance(body.position,before)<.001f && body.linearVelocity.y==8,"Jump is dragged by deck");
                actions.Action=0;player.Grounded=false;before=body.position;
                interaction.OnTriggerStay(trigger);swing.PlaceAtAngle(10,true);
                Check(body.position==before,"Airborne Sonic is transported");

                // Overlapping the trigger at an edge/underside is not enough to become a passenger.
                actions.Action=0;player.Grounded=true;body.linearVelocity=Vector3.zero;
                body.position=deck.bounds.center+Vector3.right*(deck.bounds.extents.x+2);Physics.SyncTransforms();before=body.position;
                interaction.OnTriggerStay(trigger);swing.PlaceAtAngle(15,true);
                Check(body.position==before,"Sonic beside the platform is transported");
                body.position=deck.bounds.center-Vector3.up*(deck.bounds.extents.y+1);Physics.SyncTransforms();before=body.position;
                interaction.OnTriggerStay(trigger);swing.PlaceAtAngle(20,true);
                Check(body.position==before,"Sonic below the platform is transported");

                obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(obstacle,scene);
                obstacle.transform.localScale=new Vector3(2,.1f,2);obstacle.transform.position=deck.bounds.center+Vector3.up*(deck.bounds.extents.y+.15f);
                body.position=obstacle.transform.position+Vector3.up*.55f;Physics.SyncTransforms();before=body.position;
                interaction.OnTriggerStay(trigger);swing.PlaceAtAngle(25,true);
                Check(body.position==before,"Sonic standing on another object is transported");
                Object.DestroyImmediate(obstacle);obstacle=null;

                body.position=deck.bounds.center+Vector3.up*(deck.bounds.extents.y+.5f);Physics.SyncTransforms();
                player.Grounded=true;actions.Action=0;body.linearVelocity=Vector3.zero;
                interaction.OnTriggerStay(trigger);swing.PlaceAtAngle(0,true);
                Check(Vector3.Distance(body.position-swing.PlatformBody.position,offset)<.025f,"Landing does not reacquire platform");
                swing.carryPlayer=false;swing.Rebuild();before=body.position;
                swing.PlaceAtAngle(-10,true);Check(body.position==before && !trigger.enabled,"Carry toggle ignored");
                swing.carryPlayer=true;swing.Rebuild();player.Grounded=true;
                body.position=deck.bounds.center+Vector3.up*(deck.bounds.extents.y+.5f);Physics.SyncTransforms();
                interaction.OnTriggerStay(trigger);swing.enabled=false;before=body.position;
                legacyStep.Invoke(interaction,null);Check(body.position==before,"Disabled platform leaves a stale legacy carry");

                Directory.CreateDirectory(Path.GetDirectoryName(Report));
                File.WriteAllText(Report,"PASS\n"+DateTime.Now.ToString("s")+"\n24 seconds / 6 full swings in isolated PhysicsScene.\nMax vertical gap: "+maxGap+"; max lateral drift: "+maxDrift+"; max upward speed: "+maxYSpeed+".\nWalking velocity/displacement; immediate jump release; airborne/side/underside exclusion; nearer obstacle; landing reacquisition; carry toggle; disabled cleanup; flat board; legacy double transport removed.\n");
                Debug.Log("ChainSwing : transport, appui, marche et saut verifies sans modifier le niveau.");
            }catch(Exception e){Directory.CreateDirectory(Path.GetDirectoryName(Report));File.WriteAllText(Report,"FAIL\n"+e);Debug.LogException(e);}
            finally{
                if(root!=null)Object.DestroyImmediate(root);if(sonic!=null)Object.DestroyImmediate(sonic);if(obstacle!=null)Object.DestroyImmediate(obstacle);
                if(previous.IsValid() && previous.isLoaded)SceneManager.SetActiveScene(previous);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
