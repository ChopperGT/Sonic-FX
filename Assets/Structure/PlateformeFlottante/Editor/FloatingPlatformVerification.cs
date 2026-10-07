using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SonicFX.Lava;
using SonicFX.Water;
using Object=UnityEngine.Object;
namespace SonicFX.Structures.Editor
{
    [InitializeOnLoad]
    static class FloatingPlatformVerification
    {
        static string Folder=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/FloatingPlatform");
        static FloatingPlatformVerification(){EditorApplication.update+=Ready;}
        static void Ready(){string request=Path.Combine(Folder,"request.txt");if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request))return;File.Delete(request);Verify();}
        static void Check(bool c,string message){if(!c)throw new Exception(message);}
        static void Private(object obj,string field,object value){obj.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(obj,value);}
        static GameObject New(Scene scene,string name){var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,scene);return go;}
        static SonicFloatingPlatform Spawn(Scene scene,SonicLavaVolume lava,SonicWaterVolume water=null)
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(FloatingPlatformBuilder.Prefab));SceneManager.MoveGameObjectToScene(go,scene);go.transform.position=new Vector3(0,20,0);
            var p=go.GetComponent<SonicFloatingPlatform>();p.enabled=false;go.GetComponent<SonicFloatingPlatformEffects>().enabled=false;p.water=water;p.lava=lava;p.enabled=true;p.Initialize();return p;
        }
        static void Verify()
        {
            var active=SceneManager.GetActiveScene();bool dirty=active.isDirty;var selection=Selection.objects;var rng=UnityEngine.Random.state;
            int rings=Objects_Interaction.RingAmount;bool shield=Monitors_Interactions.HasShield;float shake=HedgeCamera.Shakeforce;
            var progress=typeof(SonicFX.Score.SonicLevelScore).GetField("<RingsTowardLife>k__BackingField",BindingFlags.NonPublic|BindingFlags.Static);object savedProgress=progress.GetValue(null);
            Scene scene=default;var report=new StringBuilder();
            try
            {
                FloatingPlatformBuilder.Build();FloatingPlatformBuilder.UpgradeFlames();scene=EditorSceneManager.NewPreviewScene();Check(scene.GetPhysicsScene()!=Physics.defaultPhysicsScene,"Isolated physics");
                var lavaGo=New(scene,"Test lava");lavaGo.transform.position=new Vector3(0,20,0);var lava=lavaGo.AddComponent<SonicLavaVolume>();lava.width=lava.length=40;lava.depth=15;lava.lethalVolume=lavaGo.AddComponent<BoxCollider>();lava.Refresh();
                var waterGo=New(scene,"Test water");waterGo.transform.position=new Vector3(0,20,0);var water=waterGo.AddComponent<SonicWaterVolume>();water.width=water.length=40;
                var p=Spawn(scene,lava);Check(p.PlacementValid && p.CurrentLiquid==SonicFloatingPlatform.Liquid.Lava,"Lava identified");
                Check(Mathf.Abs(p.transform.position.y-20.28f)<.01f,"Bottom immersed, top dry");Check(!p.TopTouchesLava(out _),"Immersed underside does not ignite top");
                Check(p.deck.SourceMesh!=null && AssetDatabase.Contains(p.deck.SourceMesh),"Persistent editable source");
                var counts=p.deck.PointCounts;Check(p.deck.InsertPoints(0,.5f,out _) && p.deck.PointCounts.x==counts.x+1,"Same Cube row insertion");
                int triangles=p.deck.TriangleCount;p.deck.meshSubdivisions=3;p.deck.Rebuild();Check(p.deck.TriangleCount>triangles,"Subdivisions refine polygons and collider");
                p.UpdateFireVisuals();Check(p.fireMesh.sharedMesh==p.deck.GetComponent<MeshFilter>().sharedMesh && p.deckCollider.sharedMesh==p.fireMesh.sharedMesh,"Fire and collider follow generated editable mesh");
                Check(!ShaderUtil.ShaderHasError(Shader.Find("Sonic FX/Flammes animees")),"Animated flame shader compilation");
                Check(!ShaderUtil.ShaderHasError(Shader.Find("Sonic FX/Braises")),"Ember shader compilation");
                var flameMaterial=p.flames.GetComponent<ParticleSystemRenderer>().sharedMaterial;var flipbook=flameMaterial.mainTexture;
                Check(flipbook!=null && flipbook.width%4==0 && flipbook.height%4==0,"Sixteen-frame flipbook assigned to prefab");
                Check(((TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(flipbook))).DoesSourceTextureHaveAlpha(),"Flame texture has real transparency");
                p.transform.position=new Vector3(30,20,0);Check(!p.RefreshPlacement(false),"Outside fluid rejected");p.transform.position=new Vector3(17,20,0);Check(!p.RefreshPlacement(false),"Overhanging liquid boundary rejected");p.transform.position=new Vector3(0,20,0);p.RefreshPlacement(true);
                report.AppendLine("PASS water/lava placement and alignment, rejected dry/partial placements, editable Cube cage/extra rows/subdivisions, persistent geometry, matching fire/collision mesh and shader.");
                var go=New(scene,"Sonic verification");go.layer=LayerMask.NameToLayer("Player_01");var rb=go.AddComponent<Rigidbody>();rb.useGravity=false;rb.constraints=RigidbodyConstraints.FreezeRotation;
                var player=go.AddComponent<PlayerBhysics>();player.enabled=false;player.p_rigidbody=rb;player.Grounded=true;player.GroundNormal=Vector3.up;player.Playermask=1;
                var actions=go.AddComponent<ActionManager>();actions.enabled=false;actions.RestorePackAbilities();actions.Action00=go.AddComponent<Action00_Regular>();actions.Action00.enabled=false;
                actions.Action01=go.AddComponent<Action01_Jump>();actions.Action01.enabled=false;actions.Action01.JumpBall=New(scene,"Ball");
                actions.Action04=go.AddComponent<Action04_Hurt>();actions.Action04.enabled=false;Private(actions.Action04,"Player",player);
                var hurt=go.AddComponent<HurtControl>();hurt.enabled=false;actions.Action04Control=hurt;
                var sounds=go.AddComponent<SonicSoundsControl>();sounds.enabled=false;sounds.Source3=go.AddComponent<AudioSource>();sounds.PainVoiceClips=new AudioClip[0];actions.Action04.sounds=sounds;
                var interaction=go.AddComponent<Objects_Interaction>();interaction.enabled=false;interaction.Player=player;interaction.Actions=actions;interaction.Sounds=sounds;
                var physical=go.AddComponent<SphereCollider>();physical.radius=.3f;var trigger=go.AddComponent<SphereCollider>();trigger.radius=8;trigger.isTrigger=true;
                Vector3 center=p.transform.TransformPoint(new Vector3(0,.71f,0));rb.position=center;go.transform.position=center;Physics.SyncTransforms();
                Check(p.Supported(player,out _),"Centered Sonic supported");p.PrepareStep(.1f,new[]{player});Check(Quaternion.Angle(p.transform.rotation,Quaternion.identity)<.001f,"Stable at center");
                Vector3 local=new Vector3(3,.71f,0);rb.position=p.transform.TransformPoint(local);go.transform.position=rb.position;rb.linearVelocity=Vector3.right*3;Physics.SyncTransforms();Vector3 oldLocal=p.transform.InverseTransformPoint(rb.position);
                p.PrepareStep(.1f,new[]{player});Check(Mathf.Abs(Quaternion.Angle(p.transform.rotation,Quaternion.identity)-1.5f)<.01f && p.transform.up.x>0,"Side under player lowers at configured speed");
                Check((p.transform.InverseTransformPoint(rb.position)-oldLocal).magnitude<.001f && Mathf.Abs(Vector3.Dot(rb.linearVelocity,p.transform.up))<.001f,"Rotation carries rider without upward impulse");
                Check(p.SlideVelocity(p.transform.up,.1f).x>0 && p.SlideVelocity(Vector3.up,.1f).sqrMagnitude<.0001f,"Downhill sliding only on slope");
                actions.Action=1;rb.linearVelocity=Vector3.up*8;Vector3 jumped=rb.position;p.PrepareStep(.1f,new[]{player});Check(rb.position==jumped,"Jump releases rider");
                actions.Action=0;rb.linearVelocity=Vector3.zero;p.transform.rotation=Quaternion.identity;p.GetComponent<Rigidbody>().rotation=Quaternion.identity;
                rb.position=p.transform.TransformPoint(local);go.transform.position=rb.position;Physics.SyncTransforms();
                for(int i=0;i<20;i++)p.PrepareStep(.1f,new[]{player});Check(Quaternion.Angle(p.transform.rotation,Quaternion.identity)<=p.maximumTilt+.1f && p.transform.up.x>.2f,"Limited tilt follows edge pressure");
                Check(p.TopTouchesLava(out _),"Tilted top touches lava");Check(SonicFloatingPlatform.IsSupportedOverLava(player,lava),"Carrier protects rider above dipping deck");
                Check(!lava.CheckPlayer(player) && !hurt.isDead,"Direct lava overlap on supported platform is not fatal");
                Vector3 before=rb.position;rb.position=new Vector3(12,19.7f,0);go.transform.position=rb.position;Physics.SyncTransforms();
                Check(!SonicFloatingPlatform.IsSupportedOverLava(player,lava) && lava.CheckPlayer(player) && hurt.isDead,"Falling off deck still dies in lava");
                hurt.isDead=false;actions.Action=0;rb.position=before;go.transform.position=before;rb.linearVelocity=Vector3.zero;Physics.SyncTransforms();
                report.AppendLine("PASS stable center, direction/rate/limit of tilt, supported rotation without vertical launch, downhill acceleration, jump release, lava protection on deck and lethal lava after falling off.");
                p.Initialize();p.transform.rotation=Quaternion.Euler(0,0,-20);p.GetComponent<Rigidbody>().rotation=p.transform.rotation;Physics.SyncTransforms();
                p.TickFire(.1f);Check(p.FireState==SonicFloatingPlatform.FirePhase.Normal,"Ignition delay");p.TickFire(.11f);Check(p.FireState==SonicFloatingPlatform.FirePhase.Spreading,"Lava ignites immersed top");
                p.TickFire(1.5f);Check(Mathf.Abs(p.FireCoverage-.5f)<.01f,"Progressive half coverage");
                p.UpdateFireVisuals();Check(!p.fireRenderer.enabled,"No fire texture layer on wood");
                p.flames.Clear();p.EmitFlames(.5f);var burningParticles=new ParticleSystem.Particle[2000];int halfCount=p.flames.GetParticles(burningParticles);
                Check(halfCount>10,"Visible flame tongues appear during propagation");
                for(int n=0;n<halfCount;n++)
                {
                    Vector3 basePoint=p.flames.transform.TransformPoint(burningParticles[n].position)-Vector3.up*(burningParticles[n].startSize3D.y*.44f+.025f);
                    Check(p.PointBurning(basePoint),"Flames originate only on burning surface");Check(burningParticles[n].startSize3D.y>burningParticles[n].startSize3D.x,"Tall tongues rather than round sparks");
                }
                Vector2 far=new Vector2(p.IgnitionPoint.x<.5f?1:0,p.IgnitionPoint.y<.5f?1:0);Vector3 farPoint=p.deck.transform.TransformPoint(new Vector3(Mathf.Lerp(p.DeckBounds.min.x,p.DeckBounds.max.x,far.x),.4f,Mathf.Lerp(p.DeckBounds.min.z,p.DeckBounds.max.z,far.y)));
                Check(!p.PointBurning(farPoint),"Unreached surface does not damage");p.TickFire(1.5f);Check(p.FireState==SonicFloatingPlatform.FirePhase.Burning && p.PointBurning(farPoint),"Entire deck eventually burns");
                p.flames.Clear();p.EmitFlames(.5f);Check(p.flames.particleCount>halfCount,"Flame density grows with burning area");
                p.EmitFlames(2);Check(p.embers!=null && p.embers.particleCount>0,"Rising embers emitted from reached fire");p.embersEnabled=false;p.UpdateFireVisuals();Check(p.embers.particleCount==0,"Embers checkbox disables and clears sparks");p.embersEnabled=true;
                report.AppendLine("PASS textured 16-frame alpha animation, animated tall flames, no surface overlay, reached-mesh emission, increasing density and toggleable rising embers.");
                Monitors_Interactions.HasShield=true;Objects_Interaction.RingAmount=50;hurt.IsHurt=false;actions.Action=0;
                Check(p.TryFireDamage(player,farPoint,0) && !Monitors_Interactions.HasShield && !hurt.isDead && Objects_Interaction.RingAmount==50,"Fire consumes shield as one normal damage");
                actions.Action=0;Check(!p.TryFireDamage(player,farPoint,.1f),"Fire damage cooldown");hurt.IsInvencible=true;Check(!p.TryFireDamage(player,farPoint,3),"Fire respects invincibility");hurt.IsInvencible=false;
                Check(p.TryFireDamage(player,farPoint,3) && hurt.IsHurt && !hurt.isDead && p.DamageEvents==2,"Rings absorb fire damage, not instant death");
                p.TickFire(3);Check(p.FireState==SonicFloatingPlatform.FirePhase.Cooling,"Configurable full burn duration");p.TickFire(.6f);Check(Mathf.Abs(p.FireStrength-.5f)<.01f,"Gradual extinction");p.TickFire(.61f);Check(p.FireState==SonicFloatingPlatform.FirePhase.Resting && !p.PointBurning(farPoint),"Normal harmless surface after extinguishing");p.TickFire(4);Check(p.FireState==SonicFloatingPlatform.FirePhase.Normal,"Rest before new ignition");
                p.lava=null;p.water=water;p.RefreshPlacement(true);p.TickFire(1);Check(p.CurrentLiquid==SonicFloatingPlatform.Liquid.Water && p.FireCoverage==0,"Water cannot burn");
                p.lava=lava;p.water=null;p.RefreshPlacement(true);p.fireEnabled=false;p.TickFire(1);Check(p.FireState==SonicFloatingPlatform.FirePhase.Normal,"Fire checkbox");
                report.AppendLine("PASS top-only ignition delay, progressive coverage, damage only on burning region, full burn/cooling/rest durations, shield/rings/cooldown/invincibility, water and fire-disabled protection.");
                lavaGo.SetActive(false);p.lava=null;p.water=water;p.RefreshPlacement(true);p.Initialize();p.transform.rotation=Quaternion.identity;p.GetComponent<Rigidbody>().rotation=Quaternion.identity;
                hurt.IsHurt=false;hurt.IsInvencible=false;hurt.isDead=false;actions.Action=0;player.Grounded=true;rb.linearVelocity=Vector3.zero;rb.position=p.transform.TransformPoint(new Vector3(0,.705f,0));go.transform.position=rb.position;Physics.SyncTransforms();
                Vector3 stable=rb.position;float maximumUp=0;
                for(int i=0;i<30;i++){p.PrepareStep(.02f,new[]{player});p.ApplyEffects(.02f);scene.GetPhysicsScene().Simulate(.02f);maximumUp=Mathf.Max(maximumUp,rb.linearVelocity.y);}
                Check((rb.position-stable).magnitude<.05f && maximumUp<.5f,"Simulated physics stays stable at center; drift="+(rb.position-stable)+" upward="+maximumUp+" colliders="+go.GetComponents<Collider>().Length+" velocity="+rb.linearVelocity);
                rb.position=p.transform.TransformPoint(new Vector3(2.5f,.705f,0));go.transform.position=rb.position;rb.linearVelocity=Vector3.zero;Physics.SyncTransforms();maximumUp=0;
                float startX=p.transform.InverseTransformPoint(rb.position).x;
                for(int i=0;i<40;i++){p.PrepareStep(.02f,new[]{player});p.ApplyEffects(.02f);scene.GetPhysicsScene().Simulate(.02f);maximumUp=Mathf.Max(maximumUp,rb.linearVelocity.y);}
                Check(maximumUp<.5f && p.transform.InverseTransformPoint(rb.position).x>startX+.1f && p.Supported(player,out _),"Simulated rider slides downhill without launching or losing support");
                actions.Action=1;rb.linearVelocity=Vector3.up*8;float jumpY=rb.position.y;p.PrepareStep(.02f,new[]{player});p.ApplyEffects(.02f);scene.GetPhysicsScene().Simulate(.02f);Check(rb.position.y>jumpY+.1f,"Simulated jump remains free");
                report.AppendLine("PASS physics solver: stable centered rider, downhill slide without upward projection, maintained tilted support and free jump.");
                Capture();Check(SceneManager.GetActiveScene()==active && active.isDirty==dirty,"User map unchanged");
                File.WriteAllText(Path.Combine(Folder,"tests.txt"),"PASS\n"+report+DateTime.Now.ToString("s"));Debug.Log("Plateforme flottante : edition, appui, eau/lave et cycle de feu verifies dans une scene separee.");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Folder,"tests.txt"),"FAIL\n"+report+e);Debug.LogException(e);}
            finally{if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);Objects_Interaction.RingAmount=rings;Monitors_Interactions.HasShield=shield;HedgeCamera.Shakeforce=shake;progress.SetValue(null,savedProgress);UnityEngine.Random.state=rng;Selection.objects=selection;}
        }
        static void Capture()
        {
            var preview=new PreviewRenderUtility();Texture2D image=null;
            try
            {
                for(int i=0;i<3;i++)
                {
                    var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(FloatingPlatformBuilder.Prefab));preview.AddSingleGO(go);
                    var p=go.GetComponent<SonicFloatingPlatform>();p.enabled=false;go.GetComponent<SonicFloatingPlatformEffects>().enabled=false;go.transform.position=new Vector3((i-1)*10,0,0);go.transform.rotation=Quaternion.Euler(0,0,i==0?0:-18);
                    p.deck.Rebuild();p.ConfigureFlames();typeof(SonicFloatingPlatform).GetProperty("FireCoverage").SetValue(p,i==0?0:i==1?.45f:1);typeof(SonicFloatingPlatform).GetProperty("FireStrength").SetValue(p,i==0?0:1);typeof(SonicFloatingPlatform).GetProperty("IgnitionPoint").SetValue(p,new Vector2(1,.5f));p.UpdateFireVisuals();Physics.SyncTransforms();
                    if(i>0){for(int n=0;n<20;n++){p.EmitFlames(.05f);p.flames.Simulate(.05f,true,false);p.embers.Simulate(.05f,true,false);}Check(p.flames.particleCount>0,"Preview flames are visible");}
                }
                preview.camera.transform.position=new Vector3(15,17,25);preview.camera.transform.LookAt(new Vector3(0,0,0));preview.camera.fieldOfView=48;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=100;preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.05f,.065f,.075f);
                preview.lights[0].intensity=1.5f;preview.lights[0].transform.rotation=Quaternion.Euler(35,-25,0);preview.lights[1].intensity=1;preview.ambientColor=new Color(.3f,.3f,.3f);
                var materials=AssetDatabase.LoadAssetAtPath<Material>(FloatingPlatformBuilder.Folder+"/Materials/Flamme.mat");
                materials.SetFloat("_PreviewTime",0);preview.BeginStaticPreview(new Rect(0,0,1600,900));preview.Render();image=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(Folder,"plateforme.png"),image.EncodeToPNG());var pixels=image.GetPixels32();Object.DestroyImmediate(image);image=null;
                materials.SetFloat("_PreviewTime",.4f);preview.BeginStaticPreview(new Rect(0,0,1600,900));preview.Render();image=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(Folder,"plateforme-animation.png"),image.EncodeToPNG());var nextPixels=image.GetPixels32();int changed=0;for(int i=0;i<pixels.Length;i++)if(!pixels[i].Equals(nextPixels[i]))changed++;
                materials.SetFloat("_PreviewTime",-1);Check(changed>100,"Flame animation changes silhouette and color over time");
            }
            finally{if(image!=null)Object.DestroyImmediate(image);preview.Cleanup();}
        }
    }
}
