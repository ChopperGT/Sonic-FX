using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace SonicFX.Menu.Editor
{
    [InitializeOnLoad] public static class SonicNewLevelInstaller
    {
        public const string ManiaPath="Assets/BumperEngineV1/PlayerPrefabs/PO_Mania.prefab";
        public const string FreePath="Assets/BumperEngineV1/PlayerPrefabs/SonicManiaFree.prefab";
        public const string CatalogPath="Assets/Menu/Frontend/Resources/SonicNewLevelCatalog.asset";
        const string Version="SonicFX.NewLevelCharacters.v1";
        public static readonly string Reports=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Codex/2026-09-06/referenced-chatgpt-conversation-this-is-an/outputs/NewLevelCharacters");
        static SonicNewLevelInstaller(){EditorApplication.update+=Ready;}
        static void Ready()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || PrefabStageUtility.GetCurrentPrefabStage()!=null)return;
            EditorApplication.update-=Ready;if(SessionState.GetBool(Version,false))return;SessionState.SetBool(Version,true);
            Install();
        }
        [MenuItem("Tools/Sonic FX/Menu/Installer et verifier New Level")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null)return;
            Directory.CreateDirectory(Reports);GameObject root=null;
            try{
                string sourceBefore=File.ReadAllText(ManiaPath);
                if(!File.Exists(FreePath) && !AssetDatabase.CopyAsset(ManiaPath,FreePath))throw new IOException("Copie Sonic Mania impossible.");
                root=PrefabUtility.LoadPrefabContents(FreePath);root.name="SonicManiaFree";
                var physics=root.GetComponentInChildren<PlayerBhysics>(true);if(physics==null)throw new Exception("Player Bhysics absent du prefab Mania.");
                physics.TopSpeed=65;physics.MaxSpeed=350;
                var actions=physics.GetComponent<ActionManager>();if(actions==null)throw new Exception("Action Manager absent du prefab Mania.");
                var serialized=new SerializedObject(actions);serialized.FindProperty("startingAbilities").intValue=(int)SonicAbility.All;serialized.ApplyModifiedPropertiesWithoutUndo();
                if(PrefabUtility.SaveAsPrefabAsset(root,FreePath)==null)throw new IOException("Prefab SonicManiaFree non enregistre.");
                PrefabUtility.UnloadPrefabContents(root);root=null;
                if(File.ReadAllText(ManiaPath)!=sourceBefore)throw new Exception("Le prefab Mania original a ete modifie.");
                var catalog=AssetDatabase.LoadAssetAtPath<SonicNewLevelCatalog>(CatalogPath);
                if(catalog==null){catalog=ScriptableObject.CreateInstance<SonicNewLevelCatalog>();AssetDatabase.CreateAsset(catalog,CatalogPath);}
                string folder="Assets/BumperEngineV1/PlayerPrefabs/";
                var entries=new[]{
                    new[]{"sonic","Sonic (jeune)",ManiaPath},new[]{SonicNewLevelSession.FreeCharacterId,"SonicManiaFree",FreePath},
                    new[]{"modernsonic","Sonic (moderne)",folder+"PO_ModernSonic.prefab"},new[]{"amy","Amy",folder+"PO_Amy.prefab"},
                    new[]{"shadow","Shadow",folder+"PO_Shadow.prefab"},new[]{"metal","Metal Sonic",folder+"PO_Metal.prefab"},
                    new[]{"mighty","Mighty",folder+"PO_MIghty.prefab"},new[]{"silver","Silver",folder+"PO_Silver.prefab"},
                    new[]{"blaze","Blaze",folder+"PO_Blaze.prefab"},new[]{"espio","Espio",folder+"PO_Espio.prefab"},new[]{"sally","Sally",folder+"PO_Sally.prefab"}
                };
                var pack=entries.Select(entry=>new SonicNewLevelCharacter{id=entry[0],displayName=entry[1],prefab=AssetDatabase.LoadAssetAtPath<GameObject>(entry[2])}).ToArray();
                if(pack.Any(entry=>entry.prefab==null))throw new Exception("Un prefab du pack est introuvable.");
                catalog.characters=MergeCharacters(catalog.characters,pack);
                if(catalog.characters.Any(entry=>entry.prefab==null))throw new Exception("Un prefab du pack est introuvable.");
                EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
                File.WriteAllText(Path.Combine(Reports,"installation.txt"),"INSTALLED\n"+DateTime.Now.ToString("s")+"\n11 personnages, SonicManiaFree 65/350, toutes capacites. Prefab original et musique du menu conserves.\n");
                SonicNewLevelVerification.Verify();
            }catch(Exception e){File.WriteAllText(Path.Combine(Reports,"installation.txt"),"FAIL\n"+e);Debug.LogException(e);}
            finally{if(root!=null)PrefabUtility.UnloadPrefabContents(root);}
        }
        public static SonicNewLevelCharacter[] MergeCharacters(SonicNewLevelCharacter[] previous,SonicNewLevelCharacter[] pack)
        {
            var characters=pack.Concat(previous??Array.Empty<SonicNewLevelCharacter>()).Where(c=>c!=null&&c.prefab!=null&&!string.IsNullOrEmpty(c.id)).ToList();
            foreach(var entry in new[]{
                new[]{"classicsonic","Sonic Classique","Assets/Characters/ClassicSonic/Resources/SonicClassique.prefab"},
                new[]{"classicsonicfree","Sonic Classique Free","Assets/Characters/ClassicSonic/SonicClassiqueFree.prefab"},
                new[]{"sonia","Sonia","Assets/Characters/Sonia/Sonia.prefab"}})
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(entry[2]);
                if(prefab&&!characters.Any(c=>c.id==entry[0]))characters.Add(new SonicNewLevelCharacter{id=entry[0],displayName=entry[1],prefab=prefab});
            }
            return characters.GroupBy(c=>c.id).Select(g=>g.First()).ToArray();
        }
    }
}
