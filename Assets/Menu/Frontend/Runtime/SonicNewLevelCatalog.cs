using System;
using UnityEngine;

namespace SonicFX.Menu
{
    [Serializable] public sealed class SonicNewLevelCharacter
    {
        public string id,displayName;
        public GameObject prefab;
    }
    [CreateAssetMenu(menuName="Sonic FX/New Level/Personnages")]
    public sealed class SonicNewLevelCatalog : ScriptableObject
    {
        public SonicNewLevelCharacter[] characters=Array.Empty<SonicNewLevelCharacter>();
        public static SonicNewLevelCatalog Load()=>Resources.Load<SonicNewLevelCatalog>("SonicNewLevelCatalog");
        public SonicNewLevelCharacter Find(string id)
        {
            foreach(var entry in characters)if(entry!=null && entry.id==id)return entry;
            return null;
        }
        public bool Contains(SonicNewLevelCharacter character)
        {
            if(character==null || character.prefab==null || string.IsNullOrEmpty(character.id))return false;
            foreach(var entry in characters)if(entry!=null && entry.id==character.id && entry.prefab==character.prefab)return true;
            return false;
        }
    }
}
