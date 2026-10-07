using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace SonicFX.Magma.Editor
{
    [InitializeOnLoad]
    static class MagmaRockVerification
    {
        static string Folder=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/MagmaRocks");
        static MagmaRockVerification(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            string request=Path.Combine(Folder,"request.txt");
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request))return;
            File.Delete(request);Verify();
        }
        static void Check(bool c,string message){if(!c)throw new Exception(message);}
        static void Private(object obj,string field,object value){obj.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(obj,value);}
        static GameObject New(Scene scene,string name){var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,scene);return go;}
        static void Verify()
        {
            var active=SceneManager.GetActiveScene();bool dirty=active.isDirty;var selection=Selection.objects;var rng=UnityEngine.Random.state;
            int rings=Objects_Interaction.RingAmount;bool shield=Monitors_Interactions.HasShield;float shake=HedgeCamera.Shakeforce;
            var progress=typeof(SonicFX.Score.SonicLevelScore).GetField("<RingsTowardLife>k__BackingField",BindingFlags.NonPublic|BindingFlags.Static);object savedProgress=progress.GetValue(null);
            Scene scene=default;var report=new StringBuilder();
            try
            {
                MagmaRockBuilder.Build();scene=EditorSceneManager.NewPreviewScene();
                Check(scene.GetPhysicsScene()!=Physics.defaultPhysicsScene,"Isolated physics");
                foreach(string name in MagmaRockBuilder.Names)
                {
                    var asset=AssetDatabase.LoadAssetAtPath<GameObject>(MagmaRockBuilder.Folder+"/"+name+".prefab");Check(asset!=null,"Prefab "+name);
                    var rock=Object.Instantiate(asset);SceneManager.MoveGameObjectToScene(rock,scene);
                    var mesh=rock.GetComponent<MeshFilter>().sharedMesh;var col=rock.GetComponent<MeshCollider>();
                    Check(mesh!=null && mesh.vertexCount>=200 && AssetDatabase.Contains(mesh),"Persistent detailed mesh "+name);
                    Check(col.convex && !col.isTrigger && col.sharedMesh==mesh,"Solid collider "+name);
                    Physics.SyncTransforms();Check(scene.GetPhysicsScene().Raycast(new Vector3(0,8,0),Vector3.down,out var hit,12) && hit.collider!=null,"Collider cooked "+name);
                    Object.DestroyImmediate(rock);
                }
                Check(!ShaderUtil.ShaderHasError(Shader.Find("Sonic FX/Pierre magma")),"Shader compilation");
                report.AppendLine("PASS six distinct persistent meshes/prefabs, solid cooked colliders and magma shader.");
                var go=New(scene,"Sonic verification");var rb=go.AddComponent<Rigidbody>();rb.useGravity=false;
                var player=go.AddComponent<PlayerBhysics>();player.enabled=false;player.p_rigidbody=rb;
                var actions=go.AddComponent<ActionManager>();actions.enabled=false;actions.RestorePackAbilities();
                actions.Action00=go.AddComponent<Action00_Regular>();actions.Action00.enabled=false;
                actions.Action01=go.AddComponent<Action01_Jump>();actions.Action01.enabled=false;actions.Action01.JumpBall=New(scene,"Ball");
                actions.Action04=go.AddComponent<Action04_Hurt>();actions.Action04.enabled=false;Private(actions.Action04,"Player",player);
                var hurt=go.AddComponent<HurtControl>();hurt.enabled=false;actions.Action04Control=hurt;
                var sounds=go.AddComponent<SonicSoundsControl>();sounds.enabled=false;sounds.Source3=go.AddComponent<AudioSource>();sounds.PainVoiceClips=new AudioClip[0];actions.Action04.sounds=sounds;
                var interaction=go.AddComponent<Objects_Interaction>();interaction.enabled=false;interaction.Player=player;interaction.Actions=actions;interaction.Sounds=sounds;
                var obj=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MagmaRockBuilder.Folder+"/"+MagmaRockBuilder.Names[1]+".prefab"));SceneManager.MoveGameObjectToScene(obj,scene);var magma=obj.GetComponent<SonicMagmaRock>();
                Objects_Interaction.RingAmount=50;Monitors_Interactions.HasShield=true;
                Check(magma.TryDamage(player,0) && !Monitors_Interactions.HasShield && !hurt.isDead && Objects_Interaction.RingAmount==50,"Shield absorbed one damage");
                Check(!magma.TryDamage(player,.02f) && magma.DamageEvents==1,"Repeated collider callback cannot double damage");
                actions.Action=0;Check(!magma.TryDamage(player,1),"Damage interval respected");
                Check(magma.TryDamage(player,2) && hurt.IsHurt && !hurt.isDead && !actions.Action01.JumpBall.activeSelf,"Rings absorb damage and ball removed");
                Private(hurt,"RingsToRelease",0);typeof(HurtControl).GetMethod("RingLoss",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(hurt,null);
                Check(Objects_Interaction.RingAmount==0,"Existing ring loss path clears rings");
                actions.Action=0;Check(!magma.TryDamage(player,5),"Already hurt protected");hurt.IsHurt=false;hurt.IsInvencible=true;
                Check(!magma.TryDamage(player,5),"Invincibility protected");hurt.IsInvencible=false;magma.contactDamage=false;
                Check(!magma.TryDamage(player,5),"Decorative checkbox protected");magma.contactDamage=true;
                Check(magma.TryDamage(player,5) && hurt.isDead && magma.DamageEvents==3,"Without rings damage invokes normal death");
                Check(!magma.TryDamage(player,10) && magma.DamageEvents==3,"Dead cannot be damaged again");
                report.AppendLine("PASS shield loss, one damage per callback sequence, configurable cooldown, rings loss, ball cancellation, hurt/invincibility protection, decor switch, normal unprotected death.");
                Capture();
                Check(SceneManager.GetActiveScene()==active && active.isDirty==dirty,"User map unchanged");Check(Selection.objects.Length==selection.Length,"Selection preserved");
                File.WriteAllText(Path.Combine(Folder,"tests.txt"),"PASS\n"+report+DateTime.Now.ToString("s"));Debug.Log("Pierres de magma : six modeles verifies, aucun niveau modifie.");
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
                for(int i=0;i<MagmaRockBuilder.Names.Length;i++)
                {
                    var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MagmaRockBuilder.Folder+"/"+MagmaRockBuilder.Names[i]+".prefab"));preview.AddSingleGO(go);
                    Vector3 size=go.GetComponent<MeshFilter>().sharedMesh.bounds.size;go.transform.localScale=Vector3.one*(2.5f/Mathf.Max(size.x,Mathf.Max(size.y,size.z)));
                    go.transform.position=new Vector3((i%3-1)*3.6f,0,(i/3==0?-1:1)*2.1f);go.GetComponent<SonicMagmaRock>().Refresh();
                }
                preview.camera.transform.position=new Vector3(8,9,13);preview.camera.transform.LookAt(new Vector3(0,.5f,0));preview.camera.fieldOfView=42;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=100;
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.045f,.045f,.06f);
                preview.lights[0].intensity=1.7f;preview.lights[0].transform.rotation=Quaternion.Euler(35,-25,0);preview.lights[1].intensity=1;preview.ambientColor=new Color(.28f,.28f,.3f);
                preview.BeginStaticPreview(new Rect(0,0,1200,850));preview.Render();image=preview.EndStaticPreview();File.WriteAllBytes(Path.Combine(Folder,"pierres_magma.png"),image.EncodeToPNG());
            }
            finally{if(image!=null)Object.DestroyImmediate(image);preview.Cleanup();}
        }
    }
}
