using System;
using UnityEngine;
using SonicFX.Menu;

namespace SonicFX.Monitors
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class LifeMonitorIcon : MonoBehaviour
    {
        [Serializable] public sealed class CharacterIcon
        {
            [Tooltip("Identifiant du personnage : sonic, tails, amy ou shadow.")]
            public string character="sonic";
            public Sprite icon;
        }
        public Renderer screenRenderer;
        [Min(0)] public int screenMaterialIndex=1;
        [Tooltip("Icone utilisee tant qu'un personnage n'a pas encore la sienne.")]
        public Sprite fallbackIcon;
        public CharacterIcon[] characterIcons=new CharacterIcon[0];
        MaterialPropertyBlock properties;
        string displayedCharacter;

        public Sprite IconFor(string character)
        {
            foreach(var entry in characterIcons??new CharacterIcon[0])
                if(entry!=null&&entry.character==character&&entry.icon!=null)return entry.icon;
            return fallbackIcon;
        }
        void OnEnable(){Refresh();}
        void OnValidate(){Refresh();}
        void Update(){if(displayedCharacter!=SonicXProgress.Character)Refresh();}
        public void Refresh()
        {
            displayedCharacter=SonicXProgress.Character;
            if(screenRenderer==null||screenMaterialIndex<0||screenMaterialIndex>=screenRenderer.sharedMaterials.Length)return;
            var icon=IconFor(displayedCharacter);
            if(icon==null)return;
            if(properties==null)properties=new MaterialPropertyBlock();
            screenRenderer.GetPropertyBlock(properties,screenMaterialIndex);
            var rect=icon.rect;var texture=icon.texture;
            properties.SetTexture("_IconTex",texture);
            properties.SetVector("_IconUVRect",new Vector4(rect.x/texture.width,rect.y/texture.height,rect.width/texture.width,rect.height/texture.height));
            properties.SetFloat("_IconAspect",rect.width/Mathf.Max(1f,rect.height));
            screenRenderer.SetPropertyBlock(properties,screenMaterialIndex);
        }
    }
}
