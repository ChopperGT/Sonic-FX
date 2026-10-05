using UnityEngine;
using UnityEngine.SceneManagement;

namespace SonicFX.Score
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Sonic FX/Niveau/Medailles des records")]
    public sealed class SonicLevelMedals : MonoBehaviour
    {
        [Header("Temps maximum pour obtenir chaque medaille")]
        [Min(.01f), InspectorName("Arc-en-ciel (secondes)")]
        public float RainbowSeconds=60;
        [Min(.01f), InspectorName("Diamant (secondes)")]
        public float DiamondSeconds=75;
        [Min(.01f), InspectorName("Or (secondes)")]
        public float GoldSeconds=90;
        [Min(.01f), InspectorName("Argent (secondes)")]
        public float SilverSeconds=120;
        [Min(.01f), InspectorName("Bronze (secondes)")]
        public float BronzeSeconds=-1; // Missing in older scenes: derive it from their silver threshold.

        static float Valid(float value,float fallback)=>float.IsNaN(value) || float.IsInfinity(value)?fallback:Mathf.Max(.01f,value);
        public float[] GetTimes()
        {
            float rainbow=Valid(RainbowSeconds,60);
            float diamond=Mathf.Max(rainbow,Valid(DiamondSeconds,75));
            float gold=Mathf.Max(diamond,Valid(GoldSeconds,90));
            float silver=Mathf.Max(gold,Valid(SilverSeconds,120));
            float bronze=Mathf.Max(silver,Valid(BronzeSeconds>0?BronzeSeconds:float.NaN,silver*1.25f));
            return new[]{rainbow,diamond,gold,silver,bronze};
        }
        void OnValidate(){SetTimes(GetTimes());}
        public void SetTimes(float[] times)
        {
            if(times==null || (times.Length!=4 && times.Length!=5))return;
            RainbowSeconds=times[0];DiamondSeconds=times[1];GoldSeconds=times[2];SilverSeconds=times[3];
            BronzeSeconds=times.Length==5?times[4]:SilverSeconds*1.25f;
            var valid=GetTimes();RainbowSeconds=valid[0];DiamondSeconds=valid[1];GoldSeconds=valid[2];SilverSeconds=valid[3];BronzeSeconds=valid[4];
        }
        public static SonicLevelMedals FindInScene(Scene scene)
        {
            if(!scene.IsValid() || !scene.isLoaded)return null;
            foreach(var root in scene.GetRootGameObjects())
                foreach(var settings in root.GetComponentsInChildren<SonicLevelMedals>())
                    if(settings.isActiveAndEnabled)return settings;
            return null;
        }
    }
}
