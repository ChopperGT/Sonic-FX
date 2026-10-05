using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SonicFX.Menu
{
    public static class SonicNewLevelSession
    {
        public const string SecondAct="Assets/Level/Sonic 1/Act 1 GreenHiill/act 1-2/act 1-2.unity";
        public const string FreeCharacterId="sonicmaniafree";
        static SonicNewLevelCharacter selected;
        public static bool IsActive=>selected!=null;
        public static string CharacterId=>selected?.id;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){End();SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;}
        public static void End(){selected=null;}
        public static bool CanLaunch(SonicNewLevelCharacter character,string scene)
        {
            var catalog=SonicNewLevelCatalog.Load();
            return catalog!=null && catalog.Contains(character) && !string.IsNullOrEmpty(scene) && scene.StartsWith("Assets/Level/",StringComparison.Ordinal) && Application.CanStreamedLevelBeLoaded(scene);
        }
        public static AsyncOperation Begin(SonicNewLevelCharacter character,string scene)
        {
            if(!CanLaunch(character,scene))return null;
            selected=character;SonicXProgress.BeginNewLevel(character.id);
            Time.timeScale=1;Monitors_Interactions.HasShield=false;
            try{var operation=SceneManager.LoadSceneAsync(scene);if(operation==null)End();return operation;}
            catch{End();throw;}
        }
        static void Loaded(Scene scene,LoadSceneMode mode)
        {
            if(mode!=LoadSceneMode.Single)return;
            if(scene.name=="LogoScreen"){End();return;}
            if(!IsActive)return;
            // Results replay/continue must keep the same avatar for the next level.
            if(scene.name=="StageCompleteScreen" || scene.name=="LoadingScreen")return;
            if(!scene.path.StartsWith("Assets/Level/",StringComparison.Ordinal)){End();return;}
            InstallCharacter(scene,selected);
        }
        // sceneLoaded runs before Start: enemies and camera scripts then discover the chosen avatar.
        internal static PlayerBhysics InstallCharacter(Scene scene,SonicNewLevelCharacter character)
        {
            if(character==null || character.prefab==null)return null;
            var players=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<PlayerBhysics>()).Where(p=>p.gameObject.activeInHierarchy).ToArray();
            if(players.Length!=1){Debug.LogWarning("New Level : le niveau doit contenir un seul point de depart joueur.");return null;}
            var original=players[0];
            // Young Sonic uses the restricted Mania prefab in Story, but its
            // original pack settings in New Level. Never modify the source prefab.
            if(character.id=="sonic"){
                original.TopSpeed=55;original.MaxSpeed=343;
                original.GetComponent<ActionManager>()?.RestorePackAbilities();
                return original;
            }
            var root=original.transform;
            var camera=original.GetComponent<CameraControl>()?.Cam;
            if(camera!=null)while(root.parent!=null && !camera.transform.IsChildOf(root))root=root.parent;
            var position=original.transform.position;var rotation=original.transform.rotation;
            var config=original.GetComponent<LevelProgressControl>();
            GameObject replacement=null;
            try{
                root.gameObject.SetActive(false);
                replacement=UnityEngine.Object.Instantiate(character.prefab,root.position,root.rotation);
                replacement.name=character.displayName;SceneManager.MoveGameObjectToScene(replacement,scene);replacement.transform.localScale=root.lossyScale;
                var player=replacement.GetComponentInChildren<PlayerBhysics>(true);
                if(player==null)throw new InvalidOperationException("Le prefab choisi ne contient pas Player Bhysics.");
                player.transform.SetPositionAndRotation(position,rotation);
                player.GetComponent<ActionManager>()?.RestorePackAbilities();
                var target=player.GetComponent<LevelProgressControl>();
                if(config!=null && target!=null){
                    target.LevelToGoNext=config.LevelToGoNext;target.NextLevelScene=config.NextLevelScene;
                    target.NextLevelNameLeft=config.NextLevelNameLeft;target.NextLevelNameRight=config.NextLevelNameRight;
                    target.MaximumTimeBonus=config.MaximumTimeBonus;target.IdealTimeSeconds=config.IdealTimeSeconds;target.TimeBonusLimitSeconds=config.TimeBonusLimitSeconds;
                    target.MedalTimesSeconds=config.MedalTimesSeconds==null?Array.Empty<float>():(float[])config.MedalTimesSeconds.Clone();
                }
                RebindPlayerReferences(scene,original,player);
                if(Application.isPlaying)UnityEngine.Object.Destroy(root.gameObject);else UnityEngine.Object.DestroyImmediate(root.gameObject);
                return player;
            }catch{
                if(replacement!=null){replacement.SetActive(false);if(Application.isPlaying)UnityEngine.Object.Destroy(replacement);else UnityEngine.Object.DestroyImmediate(replacement);}
                root.gameObject.SetActive(true);throw;
            }
        }
        static void RebindPlayerReferences(Scene scene,PlayerBhysics previous,PlayerBhysics current)
        {
            // Legacy enemies (including Motobug) cache Sonic in Awake, before sceneLoaded.
            // Keep those references and explicit Inspector assignments on the selected avatar.
            foreach(var behaviour in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true))){
                if(behaviour==null || behaviour.transform.IsChildOf(previous.transform.root) || behaviour.transform.IsChildOf(current.transform.root))continue;
                foreach(var field in behaviour.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)){
                    if(field.IsInitOnly || !typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))continue;
                    var value=field.GetValue(behaviour) as UnityEngine.Object;UnityEngine.Object replacement=null;
                    if(value==null)continue;
                    if(value==previous.gameObject)replacement=current.gameObject;
                    else if(value is Component component && component.transform.IsChildOf(previous.transform)){
                        // Some pack prefabs also tag CharacterCapsule as Player; Motobug may cache that child.
                        if(component is Transform)replacement=current.transform;
                        else replacement=current.GetComponent(component.GetType())??current.GetComponentInChildren(component.GetType(),true);
                    }
                    if(replacement!=null && field.FieldType.IsInstanceOfType(replacement))field.SetValue(behaviour,replacement);
                }
            }
        }
    }
}
