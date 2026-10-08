using System;
using UnityEngine;

namespace SonicFX.Menu
{
    [CreateAssetMenu(menuName="Sonic FX/Parcours histoire",fileName="SonicStoryRoute")]
    public sealed class SonicStoryRoute : ScriptableObject
    {
        public const string SecondLevel="Assets/Level/Sonic 1/Act 1 GreenHiill/act 1-2/act 1-2.unity";
        public const string ThirdLevel="Assets/Level/Sonic 1/Act 1 GreenHiill/act 1-3/act 1-3.unity";
        public const string MarbleFirstLevel="Assets/Level/Sonic 1/Act 2 Marble Zone/Act 2-1.unity";
        [Serializable] public sealed class StageLink
        {
            [Tooltip("sonic, tails, amy ou shadow.")] public string character="sonic";
            [Tooltip("Chemin complet du fichier .unity du niveau actuel.")] public string levelScene;
            [Tooltip("Chemin complet du fichier .unity suivant. Un niveau non cree reste en attente.")] public string nextScene;
        }
        public StageLink[] stages=new StageLink[0];

        public bool TryGetNextScene(string character,string currentScene,out string nextScene)
        {
            nextScene=null;
            // Classic uses the same adventure as the young Mania character.
            if(character=="classicsonic")character="sonic";
            if(stages==null)return false;
            foreach(var stage in stages)
                if(stage!=null&&stage.character==character&&stage.levelScene==currentScene)
                {
                    nextScene=stage.nextScene;
                    return true;
                }
            return false;
        }
        public static bool TryResolve(string character,string currentScene,out string nextScene)
        {
            var route=Resources.Load<SonicStoryRoute>("SonicStoryRoute");
            nextScene=null;
            return route!=null&&route.TryGetNextScene(character,currentScene,out nextScene);
        }
    }
}
