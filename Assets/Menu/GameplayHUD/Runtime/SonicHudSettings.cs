using UnityEngine;
namespace SonicFX.HUD
{
    public class SonicHudSettings : ScriptableObject
    {
        public Sprite sonicIcon,ringIcon;
        public Font numberFont;
        [Range(.5f,1.5f)] public float scale=1;
        public Vector2 margin=new Vector2(18,24);
    }
}
