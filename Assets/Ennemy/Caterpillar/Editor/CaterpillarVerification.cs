using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace SonicFX.Caterpillar.Editor
{
    [InitializeOnLoad]
    static class CaterpillarVerification
    {
        static string Folder=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/Caterpillar");
        static CaterpillarVerification(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            string request=Path.Combine(Folder,"request.txt");
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request))return;
            File.Delete(request);Verify();
        }
        static void Check(bool c,string message){if(!c)throw new Exception(message);}
        static void Private(object obj,string field,object value){obj.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(obj,value);}
        static object Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(obj,args);
        static GameObject New(Scene scene,string name){var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,scene);return go;}
        static CaterpillarController Spawn(Scene scene)
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CaterpillarBuilder.Prefab));SceneManager.MoveGameObjectToScene(go,scene);go.transform.position=new Vector3(0,20.025f,0);
            var c=go.GetComponent<CaterpillarController>();c.enabled=false;c.Initialize();return c;
        }
        static void Verify()
        {
            var active=SceneManager.GetActiveScene();bool dirty=active.isDirty;var selection=Selection.objects;var rng=UnityEngine.Random.state;
            int rings=Objects_Interaction.RingAmount;bool shield=Monitors_Interactions.HasShield;float shake=HedgeCamera.Shakeforce;
            var progress=typeof(SonicFX.Score.SonicLevelScore).GetField("<RingsTowardLife>k__BackingField",BindingFlags.NonPublic|BindingFlags.Static);object savedProgress=progress.GetValue(null);
            Scene scene=default;var report=new StringBuilder();
            try
            {
                CaterpillarBuilder.Build();CaterpillarBuilder.UpgradeSegments();CaterpillarBuilder.UpgradeTeeth();scene=EditorSceneManager.NewPreviewScene();Check(scene.GetPhysicsScene()!=Physics.defaultPhysicsScene,"Isolated physics");
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(floor,scene);floor.transform.position=new Vector3(0,19.5f,0);floor.transform.localScale=new Vector3(80,1,80);
                var go=New(scene,"Sonic verification");go.layer=LayerMask.NameToLayer("Player_01");var rb=go.AddComponent<Rigidbody>();rb.useGravity=false;
                var player=go.AddComponent<PlayerBhysics>();player.enabled=false;player.p_rigidbody=rb;
                var actions=go.AddComponent<ActionManager>();actions.enabled=false;actions.RestorePackAbilities();
                actions.Action00=go.AddComponent<Action00_Regular>();actions.Action00.enabled=false;
                actions.Action01=go.AddComponent<Action01_Jump>();actions.Action01.enabled=false;actions.Action01.JumpBall=New(scene,"Ball");
                actions.Action04=go.AddComponent<Action04_Hurt>();actions.Action04.enabled=false;Private(actions.Action04,"Player",player);
                var hurt=go.AddComponent<HurtControl>();hurt.enabled=false;actions.Action04Control=hurt;
                var sounds=go.AddComponent<SonicSoundsControl>();sounds.enabled=false;sounds.Source3=go.AddComponent<AudioSource>();sounds.PainVoiceClips=new AudioClip[0];actions.Action04.sounds=sounds;
                var interaction=go.AddComponent<Objects_Interaction>();interaction.enabled=false;interaction.Player=player;interaction.Actions=actions;interaction.Sounds=sounds;
                var physical=go.AddComponent<SphereCollider>();physical.radius=.3f;
                var trigger=go.AddComponent<SphereCollider>();trigger.radius=6;trigger.isTrigger=true;
                var c=Spawn(scene);c.player=player;go.transform.position=new Vector3(0,20.3f,9);Physics.SyncTransforms();
                Check(c.parts.Length==4 && c.contactColliders.Length==4,"Four modelled segments");
                var head=c.parts[0];int teeth=0;
                foreach(var tooth in head.GetComponentsInChildren<MeshFilter>())if(tooth.name=="Dent")
                {teeth++;Check(Quaternion.Angle(tooth.transform.localRotation,Quaternion.identity)<.1f && tooth.transform.localScale==Vector3.one,"Fangs are straight and correctly oriented");Check(tooth.sharedMesh.bounds.size.y>tooth.sharedMesh.bounds.size.x && tooth.sharedMesh.bounds.max.y<.001f,"Fang tip points down");}
                Check(teeth==2,"Exactly two corrected fangs");
                c.bodySegmentCount=8;c.ApplyBodySize();Check(c.parts.Length==9 && c.visual.segments.Length==9 && c.contactColliders.Length==9 && c.parts[0]==head,"Growing retains head and adds eight body spheres");
                Physics.SyncTransforms();for(int i=1;i<c.parts.Length;i++){Check(c.parts[i].gameObject.activeSelf && c.contactColliders[i].enabled && c.contactColliders[i].isTrigger,"Added segment contact enabled");Check(c.parts[i].localPosition.z<c.parts[i-1].localPosition.z,"Added balls spaced down the body");}
                Check(c.Vulnerable(8,c.parts[8].position-Vector3.forward) && !c.Vulnerable(3,c.parts[3].position-Vector3.forward),"Weak rear follows new last segment");
                Check(Vector3.Distance(c.rearTarget.position,c.parts[8].position-Vector3.forward*.4f)<.001f,"Rear homing target follows size");
                float longLength=-c.parts[8].localPosition.z;c.visual.Pose(CaterpillarController.Behaviour.Retracting,1,0,0,c.compressedSpacing);Check(-c.parts[8].localPosition.z<longLength*.5f,"Retraction animates entire extended body");
                var extra=c.parts[8];c.bodySegmentCount=1;c.ApplyBodySize();Check(c.parts.Length==2 && c.contactColliders.Length==2 && c.parts[0]==head,"Shrinking retains head and one body sphere");
                foreach(var part in c.segmentPool)if(part!=c.parts[0] && part!=c.parts[1])Check(!part.gameObject.activeSelf && !part.GetComponent<SphereCollider>().enabled,"Removed balls invisible and noncolliding");
                c.bodySegmentCount=8;c.ApplyBodySize();Check(c.parts[8]==extra && c.segmentPool.Length==9,"Regrowing reuses stored balls without duplicates");
                c.bodySegmentCount=32;c.ApplyBodySize();Check(c.parts.Length==33 && c.contactColliders.Length==33,"Maximum length supported");
                c.bodySegmentCount=8;c.ApplyBodySize();string temporary=AssetDatabase.GenerateUniqueAssetPath(CaterpillarBuilder.Folder+"/Editor/Verification_Segments.prefab");
                try
                {
                    PrefabUtility.SaveAsPrefabAsset(c.gameObject,temporary);var saved=PrefabUtility.LoadPrefabContents(temporary);
                    try{var loaded=saved.GetComponent<CaterpillarController>();loaded.ApplyBodySize();Check(loaded.bodySegmentCount==8 && loaded.parts.Length==9 && loaded.segmentPool.Length==33,"Length and reusable segments survive prefab save/reload");}
                    finally{PrefabUtility.UnloadPrefabContents(saved);}
                }
                finally{AssetDatabase.DeleteAsset(temporary);}
                c.bodySegmentCount=3;c.ApplyBodySize();Check(c.parts.Length==4,"Original size restored");
                report.AppendLine("PASS 1/3/8/32 body balls, head retained, reusable hidden segments, active contacts and rear weak/homing target follow size, full retraction animation and two straight downward fangs.");
                foreach(var col in c.contactColliders)Check(col.isTrigger && !col.CompareTag("Enemy") && !col.CompareTag("Hazard"),"Armour cannot be bypassed by generic tags");
                Check(c.frontTarget.CompareTag("HomingTarget") && c.rearTarget.CompareTag("HomingTarget"),"Head and rear targets");
                Check(c.GetComponent<EnemyHealth>().ScoreOnDefeat==100,"Editable 100 defeat points");
                Check(AssetDatabase.Contains(c.parts[0].GetComponentInChildren<Renderer>().sharedMaterial.mainTexture),"Persistent carapace texture");
                Check(c.CanSee(go.transform.position+Vector3.up*.65f),"Sonic visible in front");Check(!c.CanSee(c.Eye-Vector3.forward*8),"Behind field of vision");Check(!c.CanSee(c.Eye+Vector3.forward*30),"Vision range");
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(wall,scene);wall.transform.position=new Vector3(0,22,4);wall.transform.localScale=new Vector3(10,4,.1f);Physics.SyncTransforms();
                Check(!c.CanSee(go.transform.position+Vector3.up*.65f),"Wall occludes vision");Object.DestroyImmediate(wall);Physics.SyncTransforms();
                report.AppendLine("PASS persistent model, textured materials, four contacts, editable score, two homing targets, range/angle and wall occlusion.");
                c.Tick(.25f);Check(c.State==CaterpillarController.Behaviour.Retracting,"Sees Sonic and retracts");
                c.Tick(.25f);Check(c.Retraction>.6f && c.ChargesStarted==0 && c.parts[3].localPosition.z>-2,"Visible compression before attack");
                c.Tick(.21f);Check(c.State==CaterpillarController.Behaviour.Charging && c.ChargesStarted==1,"Launch after preparation");
                Vector3 direction=c.ChargeDirection, start=c.transform.position;go.transform.position=new Vector3(12,20.3f,9);c.Tick(.1f);
                Check((c.transform.position-start).magnitude>2 && c.ChargeDirection==direction && c.Retraction<.5f,"Fast charge extends and keeps aimed direction");
                for(int i=0;i<12;i++)c.Tick(.1f);Check(c.State!=CaterpillarController.Behaviour.Charging && (c.transform.position-start).magnitude<=c.chargeDistance+.1f,"Limited charge length");
                Check(c.ChargesStarted==1,"Attack cooldown");Object.DestroyImmediate(c.gameObject);
                c=Spawn(scene);c.player=player;go.SetActive(false);c.patrolDistance=1;c.pauseDuration=0;
                bool turned=false;for(int i=0;i<240;i++){c.Tick(.05f);if(c.transform.forward.z<-.9f)turned=true;}
                Check(turned,"Patrol changes direction");Object.DestroyImmediate(c.gameObject);go.SetActive(true);
                c=Spawn(scene);wall=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(wall,scene);wall.transform.position=new Vector3(0,21,2);wall.transform.localScale=new Vector3(5,2,.1f);Physics.SyncTransforms();
                Check(!c.MoveSafely(Vector3.forward*5,true) && c.transform.position.z<1,"Thin wall stops charge");Object.DestroyImmediate(wall);
                floor.transform.localScale=new Vector3(80,1,4);Physics.SyncTransforms();Check(!c.MoveSafely(Vector3.forward*5,true),"Gap stops charge");floor.transform.localScale=new Vector3(80,1,80);Physics.SyncTransforms();
                report.AppendLine("PASS compression, telegraphed fast charge, aim locking, maximum distance, cooldown, alternating patrol, thin walls and cliff safety.");
                Check(c.Vulnerable(0,c.parts[0].position+Vector3.forward),"Front vulnerable");Check(c.Vulnerable(3,c.parts[3].position-Vector3.forward),"Rear vulnerable");
                foreach(var v in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.back})Check(!c.Vulnerable(0,c.parts[0].position+v),"Head armour "+v);
                Check(!c.Vulnerable(1,c.parts[1].position+Vector3.forward) && !c.Vulnerable(3,c.parts[3].position+Vector3.up),"Body and above protected");
                c.transform.rotation=Quaternion.Euler(0,90,0);Check(c.Vulnerable(0,c.parts[0].position+Vector3.right) && !c.Vulnerable(0,c.parts[0].position+Vector3.forward),"Rotated weak spot");c.transform.rotation=Quaternion.identity;
                player.isRolling=true;actions.Action=0;hurt.IsHurt=false;hurt.isDead=false;Monitors_Interactions.HasShield=false;Objects_Interaction.RingAmount=50;
                Check(c.ResolveContact(player,1,c.parts[1].position+Vector3.right,0)==CaterpillarController.ContactResult.HurtSonic && hurt.IsHurt && c.DefeatEvents==0,"Rolling onto side damages Sonic");
                Check(c.ResolveContact(player,1,c.parts[1].position+Vector3.right,1)==CaterpillarController.ContactResult.None,"No repeated damage");
                hurt.IsHurt=false;actions.Action=0;hurt.IsInvencible=true;Check(c.ResolveContact(player,1,c.parts[1].position+Vector3.up,2)==CaterpillarController.ContactResult.None,"Invincibility respected");hurt.IsInvencible=false;
                Check(c.ResolveContact(player,0,c.parts[0].position+Vector3.forward,2)==CaterpillarController.ContactResult.Defeated && c.DefeatEvents==1 && !c.frontTarget.gameObject.activeSelf,"Head ball defeat");
                Check(c.ResolveContact(player,0,c.parts[0].position+Vector3.forward,3)==CaterpillarController.ContactResult.None,"Defeat only once");Object.DestroyImmediate(c.gameObject);
                c=Spawn(scene);actions.Action=0;player.isRolling=true;Check(c.ResolveContact(player,3,c.parts[3].position-Vector3.forward,0)==CaterpillarController.ContactResult.Defeated,"Rear ball defeat");Object.DestroyImmediate(c.gameObject);
                c=Spawn(scene);player.isRolling=false;actions.Action=1;actions.Action01.JumpBall.SetActive(false);hurt.IsHurt=false;
                Check(!CaterpillarController.Attacking(player) && c.ResolveContact(player,0,c.parts[0].position+Vector3.forward,0)==CaterpillarController.ContactResult.HurtSonic,"Unballed jump cannot defeat");Object.DestroyImmediate(c.gameObject);
                c=Spawn(scene);hurt.IsHurt=false;actions.Action=0;player.isRolling=true;go.transform.position=c.parts[0].position+Vector3.forward*2;
                c.transform.position+=Vector3.forward*3;Physics.SyncTransforms();Call(c,"CheckContacts");
                Check(c.DefeatEvents==1,"Swept charge recognises frontal impact before crossing Sonic");Object.DestroyImmediate(c.gameObject);
                c=Spawn(scene);go.transform.position=new Vector3(5,20.91f,0);Physics.SyncTransforms();Call(c,"CheckContacts");Check(c.DefeatEvents==0 && c.DamageEvents==0,"Large player trigger ignored");Object.DestroyImmediate(c.gameObject);
                report.AppendLine("PASS head/rear vulnerability, protected sides/top/body, rotation, shield/rings damage path, invincibility, normal jump, duplicate guard and fast swept contact; large interaction trigger ignored.");
                Capture();Check(SceneManager.GetActiveScene()==active && active.isDirty==dirty,"User map unchanged");Check(Selection.objects.Length==selection.Length,"Selection preserved");
                File.WriteAllText(Path.Combine(Folder,"tests.txt"),"PASS\n"+report+DateTime.Now.ToString("s"));Debug.Log("Chenille : modele, animations et combat verifies dans une scene separee.");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Folder,"tests.txt"),"FAIL\n"+report+e);Debug.LogException(e);}
            finally
            {
                if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);Objects_Interaction.RingAmount=rings;Monitors_Interactions.HasShield=shield;HedgeCamera.Shakeforce=shake;progress.SetValue(null,savedProgress);UnityEngine.Random.state=rng;
            }
        }
        static void Capture()
        {
            var preview=new PreviewRenderUtility();Texture2D image=null;
            try
            {
                for(int i=0;i<3;i++)
                {
                    var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CaterpillarBuilder.Prefab));preview.AddSingleGO(go);go.GetComponent<CaterpillarController>().enabled=false;
                    var c=go.GetComponent<CaterpillarController>();c.bodySegmentCount=i==0?1:i==1?3:7;c.ApplyBodySize();
                    go.transform.position=new Vector3((i-1)*4.4f,0,0);go.transform.rotation=Quaternion.Euler(0,-22,0);
                    foreach(var t in go.GetComponentsInChildren<Transform>())t.gameObject.layer=0;
                    go.GetComponent<CaterpillarVisual>().Pose(CaterpillarController.Behaviour.Patrol,0,1.8f,1.4f,.4f);
                }
                preview.camera.transform.position=new Vector3(10,7,15);preview.camera.transform.LookAt(new Vector3(0,1,-3));preview.camera.fieldOfView=42;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=100;
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.055f,.065f,.085f);
                preview.lights[0].intensity=1.5f;preview.lights[0].transform.rotation=Quaternion.Euler(35,-25,0);preview.lights[1].intensity=1;preview.ambientColor=new Color(.3f,.3f,.32f);
                preview.BeginStaticPreview(new Rect(0,0,1500,850));preview.Render();image=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(Folder,"chenille.png"),image.EncodeToPNG());
            }
            finally{if(image!=null)Object.DestroyImmediate(image);preview.Cleanup();}
        }
    }
}
