using System;
using UnityEditor;
using UnityEngine;
using SonicFX.HUD;

namespace SonicFX.Monitors.Editor
{
    [InitializeOnLoad] internal static class LifeMonitorInstaller
    {
        internal const string Folder="Assets/Structure/MonitorVie";
        internal const string PrefabPath=Folder+"/Monitor_Vie.prefab";
        static LifeMonitorInstaller(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            EditorApplication.update-=Ready;
            try{Install();}catch(Exception e){Debug.LogException(e);}
        }
        [MenuItem("Tools/Sonic FX/Monitors/Installer le monitor 1 vie")]
        internal static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)!=null)return;
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BumperEngineV1/ObjectPrefabs/RingMonitor.prefab");
            var settings=AssetDatabase.LoadAssetAtPath<SonicHudSettings>("Assets/Menu/GameplayHUD/Resources/SonicHudSettings.asset");
            var shader=Shader.Find("Sonic FX/Monitor/Character Screen");
            if(source==null||settings==null||settings.sonicIcon==null||shader==null)throw new InvalidOperationException("Modele, icone Sonic ou shader du monitor manquant.");
            var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Ecran_Vie.mat");
            if(material==null)
            {
                material=new Material(shader){name="Ecran_Vie"};
                material.SetTexture("_IconTex",settings.sonicIcon.texture);
                material.SetFloat("_IconAspect",settings.sonicIcon.rect.width/settings.sonicIcon.rect.height);
                AssetDatabase.CreateAsset(material,Folder+"/Ecran_Vie.mat");
            }
            var instance=PrefabUtility.LoadPrefabContents("Assets/BumperEngineV1/ObjectPrefabs/RingMonitor.prefab");
            try
            {
                instance.name="Monitor_Vie";instance.transform.position=Vector3.zero;
                var data=instance.GetComponent<MonitorData>();data.Type=MonitorType.Life;data.RingAmount=0;data.ScoreOnDestroy=0;
                var renderer=instance.GetComponent<Renderer>();var materials=renderer.sharedMaterials;
                if(materials.Length<2)throw new InvalidOperationException("Ecran du monitor introuvable.");
                materials[1]=material;renderer.sharedMaterials=materials;
                var icon=instance.AddComponent<LifeMonitorIcon>();icon.screenRenderer=renderer;icon.fallbackIcon=settings.sonicIcon;
                icon.characterIcons=new[]{
                    new LifeMonitorIcon.CharacterIcon{character="sonic",icon=settings.sonicIcon},
                    new LifeMonitorIcon.CharacterIcon{character="tails"},
                    new LifeMonitorIcon.CharacterIcon{character="amy"},
                    new LifeMonitorIcon.CharacterIcon{character="shadow"}
                };
                icon.Refresh();
                if(PrefabUtility.SaveAsPrefabAsset(instance,PrefabPath)==null)throw new InvalidOperationException("Prefab du monitor non enregistre.");
                AssetDatabase.SaveAssets();
                Debug.Log("Monitor 1 vie installe : Assets/Structure/MonitorVie/Monitor_Vie.");
            }
            finally{PrefabUtility.UnloadPrefabContents(instance);}
        }
    }
}
