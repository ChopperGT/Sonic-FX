using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using SonicFX.Menu;
using Object=UnityEngine.Object;

namespace SonicFX.Monitors.Editor
{
    [InitializeOnLoad] internal static class LifeMonitorVerification
    {
        static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/LifeMonitor");
        static LifeMonitorVerification(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            EditorApplication.update-=Ready;
            if(!File.Exists(Path.Combine(Folder,"unity-report.txt")))Verify();
        }
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        [MenuItem("Tools/Sonic FX/Monitors/Verifier le monitor 1 vie")]
        static void Verify()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            Directory.CreateDirectory(Folder);
            var lives=typeof(SonicXProgress).GetField("<Lives>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic);
            var session=typeof(SonicXProgress).GetField("storySession",BindingFlags.Static|BindingFlags.NonPublic);
            var character=typeof(SonicXProgress).GetField("<Character>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic);
            object oldLives=lives.GetValue(null),oldSession=session.GetValue(null),oldCharacter=character.GetValue(null);
            const string key="SonicFX.Story.Save.v1";
            bool hadSave=PlayerPrefs.HasKey(key);string oldSave=PlayerPrefs.GetString(key,"");
            PreviewRenderUtility preview=null;GameObject instance=null;
            try
            {
                LifeMonitorInstaller.Install();
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(LifeMonitorInstaller.PrefabPath);
                Check(prefab!=null,"Life monitor prefab exists");
                Check((int)MonitorType.Ring==0&&(int)MonitorType.Shield==1&&(int)MonitorType.Life==2,"Existing monitor enum values unchanged");
                var data=prefab.GetComponent<MonitorData>();
                Check(data!=null&&data.Type==MonitorType.Life&&data.ScoreOnDestroy==0,"Life monitor has its own reward and editable score");
                Check(data.MonitorExplosion!=null&&prefab.GetComponent<SphereCollider>().isTrigger&&prefab.GetComponentInChildren<BoxCollider>()!=null,"Existing break effect and colliders preserved");
                preview=new PreviewRenderUtility();instance=Object.Instantiate(prefab);preview.AddSingleGO(instance);
                foreach(var behaviour in instance.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
                var icon=instance.GetComponent<LifeMonitorIcon>();
                Check(icon.fallbackIcon!=null&&icon.IconFor("sonic")==icon.fallbackIcon,"Young Sonic icon configured");
                Check(icon.IconFor("tails")==icon.fallbackIcon,"Missing future icon has fallback");
                var other=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,Texture2D.whiteTexture.width,Texture2D.whiteTexture.height),new Vector2(.5f,.5f));
                try
                {
                    icon.characterIcons[1].icon=other;character.SetValue(null,"tails");icon.Refresh();
                    var properties=new MaterialPropertyBlock();icon.screenRenderer.GetPropertyBlock(properties,icon.screenMaterialIndex);
                    Check(properties.GetTexture("_IconTex")==other.texture,"Screen follows selected character");
                }
                finally{icon.characterIcons[1].icon=null;Object.DestroyImmediate(other);}
                character.SetValue(null,"sonic");icon.Refresh();
                var material=icon.screenRenderer.sharedMaterials[icon.screenMaterialIndex];
                Check(material.shader.name=="Sonic FX/Monitor/Character Screen","Screen uses character shader");
                Check(!ShaderUtil.ShaderHasError(material.shader),"Screen shader compiles");
                var claim=typeof(MonitorData).GetMethod("ClaimReward",BindingFlags.Instance|BindingFlags.NonPublic);
                data=instance.GetComponent<MonitorData>();lives.SetValue(null,3);session.SetValue(null,false);
                int rings=Objects_Interaction.RingAmount;bool shield=Monitors_Interactions.HasShield;
                Check((bool)claim.Invoke(data,null)&&SonicXProgress.Lives==4,"Breaking life monitor adds exactly one life immediately");
                Check(!(bool)claim.Invoke(data,null)&&SonicXProgress.Lives==4,"Repeated collisions cannot award duplicate lives");
                Check(Objects_Interaction.RingAmount==rings&&Monitors_Interactions.HasShield==shield,"Life reward does not grant rings or shield");
                var fresh=instance.AddComponent<MonitorData>();fresh.Type=MonitorType.Life;fresh.ScoreOnDestroy=0;
                lives.SetValue(null,3);session.SetValue(null,true);
                PlayerPrefs.SetString(key,JsonUtility.ToJson(new StorySave{scene=SonicXProgress.FirstLevel,lives=3}));
                Check((bool)claim.Invoke(fresh,null)&&SonicXProgress.TryRead(out var save)&&save.lives==4,"Life gain persisted to active adventure");
                Object.DestroyImmediate(fresh);
                session.SetValue(null,false);
                foreach(var type in new[]{MonitorType.Ring,MonitorType.Shield})
                {
                    fresh=instance.AddComponent<MonitorData>();fresh.Type=type;fresh.ScoreOnDestroy=0;
                    Check((bool)claim.Invoke(fresh,null)&&SonicXProgress.Lives==4,"Other monitors do not grant a life");Object.DestroyImmediate(fresh);
                }
                // Preview all four screens, including the monitor's rotated UV layout.
                Render(preview,instance);
                File.WriteAllText(Path.Combine(Folder,"unity-report.txt"),"PASS\nPrefab installed; existing monitor types, shell, colliders and break effect preserved; selected-character icon and fallback verified; screen shader compiled; exactly one immediate life per destruction, duplicates ignored, active adventure persists life; rings/shield unchanged. User save restored after isolated verification. Four actual model previews rendered.\n"+DateTime.Now.ToString("s"));
                Debug.Log("Monitor 1 vie : recompense, sauvegarde et icone verifies.");
            }
            catch(Exception e){File.WriteAllText(Path.Combine(Folder,"unity-report.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally
            {
                lives.SetValue(null,oldLives);session.SetValue(null,oldSession);character.SetValue(null,oldCharacter);
                if(hadSave)PlayerPrefs.SetString(key,oldSave);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();
                if(instance!=null)Object.DestroyImmediate(instance);if(preview!=null)preview.Cleanup();
            }
        }
        static void Render(PreviewRenderUtility preview,GameObject instance)
        {
            var renderer=instance.GetComponent<Renderer>();var bounds=renderer.bounds;
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.09f,.12f,.17f);
            preview.camera.fieldOfView=32;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=100;
            preview.lights[0].intensity=1.3f;preview.lights[0].transform.rotation=Quaternion.Euler(40,35,0);
            preview.lights[1].intensity=.8f;preview.ambientColor=new Color(.45f,.45f,.45f);
            for(int i=0;i<4;i++)
            {
                float angle=i*Mathf.PI/2;var direction=new Vector3(Mathf.Sin(angle),.18f,Mathf.Cos(angle)).normalized;
                preview.camera.transform.position=bounds.center+direction*bounds.extents.magnitude*4;
                preview.camera.transform.LookAt(bounds.center);
                preview.BeginStaticPreview(new Rect(0,0,512,512));preview.Render(true);
                var capture=preview.EndStaticPreview();
                File.WriteAllBytes(Path.Combine(Folder,"Monitor-Vie-"+i+".png"),capture.EncodeToPNG());Object.DestroyImmediate(capture);
            }
        }
    }
}
