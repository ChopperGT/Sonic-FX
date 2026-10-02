using UnityEngine;
namespace SonicFX.HUD
{
    public class SonicHudSettings : ScriptableObject
    {
        public Sprite sonicIcon,ringIcon;
        public Font numberFont;
        [Range(.5f,1.5f)] public float scale=1;
        public Vector2 margin=new Vector2(18,24);

        [Header("Vitesse - bas gauche")]
        public Vector2 speedMargin=new Vector2(18,24);
        [Tooltip("Vitesse a laquelle le chiffre devient completement rouge et atteint sa taille maximale.")]
        [Min(1f)] public float speedRedAt=60f;
        [Range(1f,1.5f)] public float speedMaximumScale=1.25f;
    }
}
