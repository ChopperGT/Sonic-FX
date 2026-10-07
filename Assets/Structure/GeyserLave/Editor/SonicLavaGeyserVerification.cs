using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace SonicFX.Lava.Editor
{
    public static class SonicLavaGeyserVerification
    {
        static void Check(bool c,string text){if(!c)throw new Exception(text);}
        static void Private(object o,string field,object value){o.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,value);}
        static GameObject New(Scene scene,string name){var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,scene);return go;}
        [MenuItem("Sonic FX/Lave/Verifier le geyser")]
        public static void Verify()
        {
            var active=SceneManager.GetActiveScene();bool dirty=active.isDirty;var selection=Selection.objects;var random=UnityEngine.Random.state;
            int rings=Objects_Interaction.RingAmount;bool shield=Monitors_Interactions.HasShield;float shake=HedgeCamera.Shakeforce;
            var progress=typeof(SonicFX.Score.SonicLevelScore).GetField("<RingsTowardLife>k__BackingField",BindingFlags.NonPublic|BindingFlags.Static);object savedProgress=progress.GetValue(null);
            Scene scene=default;string temp=SonicLavaGeyserBuilder.Folder+"/Editor/Test-"+Guid.NewGuid().ToString("N")+".prefab";
            try
            {
                scene=EditorSceneManager.NewPreviewScene();Check(scene.GetPhysicsScene()!=Physics.defaultPhysicsScene,"Isolated physics");
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(SonicLavaGeyserBuilder.PrefabPath);Check(asset!=null,"Installed geyser prefab");
                var root=(GameObject)PrefabUtility.InstantiatePrefab(asset,scene);root.transform.position=Vector3.up*100;var g=root.GetComponent<SonicLavaGeyser>();
                Check(g.rockPrefabs.Length==6 && g.landingZones.Length==3,"Six supplied magma variants and three landing zones");
                var trigger=root.GetComponentInChildren<SonicGeyserTriggerZone>();Check(trigger.geyser==g && trigger.GetComponent<BoxCollider>().isTrigger,"Linked editable trigger block");
                Check(AssetDatabase.LoadAssetAtPath<GameObject>(SonicLavaGeyserBuilder.Folder+"/Zone_Geyser.prefab")!=null,"Standalone trigger prefab");
                foreach(var shader in new[]{"Sonic FX/Geyser Impact","Sonic FX/Geyser Jet Lave","Sonic FX/Geyser Goutte"})Check(!ShaderUtil.ShaderHasError(Shader.Find(shader)),"Shader compile "+shader);
                Check(!g.TryErupt() && g.Eruptions==0 && g.LastRefusal!=null,"Geyser refuses placement without lava");
                var lavaGo=New(scene,"Lave test");lavaGo.transform.position=Vector3.up*100;var lava=lavaGo.AddComponent<SonicLavaVolume>();lava.width=20;lava.length=20;g.lava=lava;Check(g.FindLava(),"Linked lava accepted");
                var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(ground,scene);ground.transform.position=new Vector3(0,99.5f,15);ground.transform.localScale=new Vector3(60,1,20);Physics.SyncTransforms();
                var z=g.landingZones[0];z.radius=0;z.rockCount=1;z.minimumScale=z.maximumScale=1.3f;z.rockPrefab=g.rockPrefabs[1];z.flightTime=2;z.arcHeight=15;
                Check(z.ResolveGround(z.transform.position+Vector3.up*2,out var point,out var normal) && Mathf.Abs(point.y-100)<.001f,"Ground projection at elevated map coordinates");
                var deco=New(scene,"Not Sonic");var decalCollider=deco.AddComponent<BoxCollider>();Check(!trigger.TryTrigger(decalCollider),"Non-player cannot trigger");
                var actor=New(scene,"Sonic test");var rb=actor.AddComponent<Rigidbody>();rb.useGravity=false;var player=actor.AddComponent<PlayerBhysics>();player.enabled=false;player.p_rigidbody=rb;var actorCollider=actor.AddComponent<CapsuleCollider>();
                g.landingZones=new[]{z};int eventCount=0;trigger.onTriggered.AddListener(()=>eventCount++);
                Check(trigger.TryTrigger(actorCollider) && g.PendingCount==1 && eventCount==1 && g.Eruptions==1,"Player triggers one eruption and UnityEvent");
                var markers=Find<SonicGeyserWarning>(scene);Check(markers.Length==1 && markers[0].GetComponent<Renderer>().enabled,"Warning visible before flight");
                Check(!trigger.TryTrigger(actorCollider) && !g.TryErupt() && g.Eruptions==1,"Once flag and concurrent eruption protection");
                g.Tick(g.warningDelay*.5f);Check(g.Rocks.Count==0,"Warning lead-in respected");g.Tick(g.warningDelay*.5f);Check(g.Rocks.Count==1,"Timed launch");
                var rock=g.Rocks[0];Check(rock.GetComponent<MeshFilter>().sharedMesh==z.rockPrefab.GetComponent<MeshFilter>().sharedMesh,"Original magma mesh used");
                Check(Mathf.Abs(rock.transform.localScale.x-1.3f)<.001f && !rock.GetComponent<Collider>().enabled && !rock.GetComponent<SonicFX.Magma.SonicMagmaRock>().contactDamage,"Configured scale and airborne contacts disabled");
                Check(g.jetVisual.enabled && g.jet!=null && g.sparks!=null,"Animated lava jet and droplets");
                var origin=rock.transform.position;g.Tick(1);Check(rock.Progress>.49f && rock.Progress<.51f && rock.transform.position.y>Mathf.Max(origin.y,point.y)+10,"Arcing ballistic flight above both endpoints");
                Check(Find<SonicGeyserWarning>(scene).Length==1,"Warning survives until landing");g.Tick(1);
                Check(rock.Landed && Vector3.Distance(rock.transform.position,point)<.001f && rock.GetComponent<Collider>().enabled && rock.GetComponent<SonicFX.Magma.SonicMagmaRock>().contactDamage,"Precise solid damaging landing");
                Check(Find<SonicGeyserWarning>(scene).Length==0 && !g.Erupting,"Warning removed at impact");
                g.Tick(60);Check(rock!=null && rock.gameObject.activeSelf,"Permanent rocks retained");
                Damage(actor,player,rock.GetComponent<SonicFX.Magma.SonicMagmaRock>());
                trigger.once=false;g.landingZones=new[]{z,g.GetComponentsInChildren<SonicGeyserLandingZone>()[1]};g.landingZones[1].rockCount=2;g.landingZones[1].radius=1.5f;g.rockLifetime=.8f;g.rockInterval=.3f;
                Check(trigger.TryTrigger(actorCollider) && g.PendingCount==3 && eventCount==2,"Repeat after cooldown with three planned projectiles");
                var marks=Find<SonicGeyserWarning>(scene);Check(marks.Length==3,"All impacts announced in advance");
                g.Tick(g.warningDelay);Check(g.PendingCount==2,"Configured staggered launches");g.Tick(15);g.Tick(1);Check(g.Rocks.Count==1 && g.Rocks[0]==rock,"Optional expiry removes only new timed rocks");
                var detached=New(scene,"Manual landing zone").AddComponent<SonicGeyserLandingZone>();detached.transform.position=new Vector3(0,120,40);detached.projectOnGround=false;Check(detached.ResolveGround(detached.transform.position,out var manual,out _) && manual==detached.transform.position,"Manual landing height");
                detached.projectOnGround=true;detached.groundSearchBelow=1;Check(!detached.ResolveGround(detached.transform.position,out _,out _),"No unsupported ground fallback");
                root.transform.rotation=Quaternion.Euler(0,40,0);root.transform.localScale=new Vector3(1.5f,1,1.5f);z.radius=2;
                var sample=z.SamplePoint(new Vector2(.5f,.25f));Check((sample-z.transform.TransformPoint(new Vector3(1,0,.5f))).magnitude<.001f,"Landing zone respects transform rotation and scale");
                PrefabUtility.RecordPrefabInstancePropertyModifications(g);PrefabUtility.RecordPrefabInstancePropertyModifications(trigger);PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform);
                foreach(var zone in g.GetComponentsInChildren<SonicGeyserLandingZone>()){PrefabUtility.RecordPrefabInstancePropertyModifications(zone);PrefabUtility.RecordPrefabInstancePropertyModifications(zone.transform);}
                PrefabUtility.RecordPrefabInstancePropertyModifications(z);Undo.IncrementCurrentGroup();Undo.RecordObject(z,"Test zone radius");z.radius=4;PrefabUtility.RecordPrefabInstancePropertyModifications(z);Undo.FlushUndoRecordObjects();Undo.PerformUndo();Check(z.radius==2,"Zone Undo: "+z.radius);Undo.PerformRedo();Check(z.radius==4,"Zone Redo");Undo.ClearUndo(z);
                Check(g.landingZones.Length==2,"Undo preserves the saved target list");
                Check(SonicGeyserProjectile.Trajectory(Vector3.zero,Vector3.up*60,15,.5f).y>=75,"High platform trajectory clears its landing point");
                g.lava=null;PrefabUtility.RecordPrefabInstancePropertyModifications(g);PrefabUtility.RecordPrefabInstancePropertyModifications(trigger);PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform);
                foreach(var zone in g.GetComponentsInChildren<SonicGeyserLandingZone>()){PrefabUtility.RecordPrefabInstancePropertyModifications(zone);PrefabUtility.RecordPrefabInstancePropertyModifications(zone.transform);}
                Check(PrefabUtility.SaveAsPrefabAsset(root,temp)!=null,"Save configured geyser");var saved=PrefabUtility.LoadPrefabContents(temp);try{var sg=saved.GetComponent<SonicLavaGeyser>();Check(sg.landingZones.Length==2 && sg.GetComponentInChildren<SonicGeyserTriggerZone>().geyser==sg && sg.landingZones[0].radius==4,"Reload landing settings and linked event: zones="+sg.landingZones.Length+" radius="+sg.landingZones[0].radius);}finally{PrefabUtility.UnloadPrefabContents(saved);}
                Capture(asset);
                Check(SceneManager.GetActiveScene()==active && active.isDirty==dirty,"User level unchanged");
                File.WriteAllText(Path.Combine(SonicLavaGeyserBuilder.Reports,"tests.txt"),"PASS\n"+DateTime.Now.ToString("s")+"\nPrefab and standalone trigger; six original magma models; lava validation; player-only trigger and UnityEvent; once/cooldown; terrain projection; warning before launch and until impact; scale, arc, staggered launches; solid permanent damaging rocks; existing shield/invincibility protections; optional lifetime; manual height; transform/Undo/Redo/save/reload; three compiled shaders and visual preview; user map unchanged.\n");
                Debug.Log("Geyser de lave installe et verifie : zones, avertissements, eruption et rochers magma avec degats.");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(SonicLavaGeyserBuilder.Reports,"tests.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally
            {
                if(AssetDatabase.LoadMainAssetAtPath(temp)!=null)AssetDatabase.DeleteAsset(temp);if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);Selection.objects=selection;
                Objects_Interaction.RingAmount=rings;Monitors_Interactions.HasShield=shield;HedgeCamera.Shakeforce=shake;progress.SetValue(null,savedProgress);UnityEngine.Random.state=random;
            }
        }
        static T[] Find<T>(Scene scene) where T:Component
        {
            var list=new System.Collections.Generic.List<T>();foreach(var go in scene.GetRootGameObjects())list.AddRange(go.GetComponentsInChildren<T>());return list.ToArray();
        }
        static void Damage(GameObject actor,PlayerBhysics player,SonicFX.Magma.SonicMagmaRock magma)
        {
            var actions=actor.AddComponent<ActionManager>();actions.enabled=false;actions.RestorePackAbilities();actions.Action00=actor.AddComponent<Action00_Regular>();actions.Action00.enabled=false;
            actions.Action01=actor.AddComponent<Action01_Jump>();actions.Action01.enabled=false;actions.Action01.JumpBall=new GameObject("Ball");actions.Action01.JumpBall.transform.SetParent(actor.transform,false);
            actions.Action04=actor.AddComponent<Action04_Hurt>();actions.Action04.enabled=false;Private(actions.Action04,"Player",player);
            var hurt=actor.AddComponent<HurtControl>();hurt.enabled=false;actions.Action04Control=hurt;
            var sounds=actor.AddComponent<SonicSoundsControl>();sounds.enabled=false;sounds.Source3=actor.AddComponent<AudioSource>();sounds.PainVoiceClips=new AudioClip[0];actions.Action04.sounds=sounds;
            var interaction=actor.AddComponent<Objects_Interaction>();interaction.enabled=false;interaction.Player=player;interaction.Actions=actions;interaction.Sounds=sounds;
            Objects_Interaction.RingAmount=50;Monitors_Interactions.HasShield=true;
            Check(magma.TryDamage(player,0) && !Monitors_Interactions.HasShield && !hurt.isDead && magma.DamageEvents==1,"Landed rock inflicts exactly one normal hit");
            Check(!magma.TryDamage(player,.01f) && magma.DamageEvents==1,"No double damage from repeated contact");actions.Action=0;hurt.IsInvencible=true;Check(!magma.TryDamage(player,5),"Invincibility respected");hurt.IsInvencible=false;
        }
        static void Capture(GameObject asset)
        {
            var preview=new PreviewRenderUtility();Texture2D image=null;
            try
            {
                var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);preview.AddSingleGO(ground);ground.transform.position=new Vector3(0,-.55f,8);ground.transform.localScale=new Vector3(34,1,32);
                var material=new Material(Shader.Find("Standard"));material.color=new Color(.16f,.105f,.085f);ground.GetComponent<Renderer>().sharedMaterial=material;
                var root=Object.Instantiate(asset);preview.AddSingleGO(root);var g=root.GetComponent<SonicLavaGeyser>();g.requireLava=false;g.jetHeight=10;
                foreach(var z in g.landingZones){z.projectOnGround=false;z.radius=0;z.flightTime=3;z.arcHeight=9;z.minimumScale=z.maximumScale=2;z.rockPrefab=g.rockPrefabs[1];}
                g.warningDelay=.5f;g.rockInterval=.18f;g.RefreshVisual();g.TryErupt();g.Tick(1.15f);g.SetJetStrength(1);
                if(g.jet!=null)g.jet.Simulate(.4f,true,false,true);if(g.sparks!=null)g.sparks.Simulate(.4f,true,false,true);
                foreach(var rock in g.Rocks)preview.AddSingleGO(rock.gameObject);foreach(var marker in Find<SonicGeyserWarning>(root.scene))preview.AddSingleGO(marker.gameObject);
                preview.camera.transform.position=new Vector3(30,28,-31);preview.camera.transform.LookAt(new Vector3(0,4,7));preview.camera.fieldOfView=44;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=150;preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.035f,.025f,.04f);
                preview.ambientColor=new Color(.28f,.25f,.23f);preview.lights[0].intensity=1.6f;preview.lights[0].transform.rotation=Quaternion.Euler(45,-30,0);preview.lights[1].intensity=.7f;
                preview.BeginStaticPreview(new Rect(0,0,1500,1000));preview.Render();image=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(SonicLavaGeyserBuilder.Reports,"geyser.png"),image.EncodeToPNG());Object.DestroyImmediate(material);
            }
            finally{if(image!=null)Object.DestroyImmediate(image);preview.Cleanup();}
        }
    }
}
