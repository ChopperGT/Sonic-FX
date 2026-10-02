using UnityEngine;
using UnityEngine.Audio;

namespace SonicFX.Audio
{
    [CreateAssetMenu(menuName="Sonic FX/Son du gain de vie")]
    public sealed class SonicExtraLifeSoundSettings : ScriptableObject
    {
        [Tooltip("Jingle joue lorsque le personnage gagne une ou plusieurs vies.")]
        public AudioClip clip;
        [Tooltip("Groupe des effets sonores, pour respecter le volume des parametres.")]
        public AudioMixerGroup mixerGroup;
        [Range(0,1)] public float volume=.8f;
        [Tooltip("Met la musique du niveau en pause pendant le jingle, puis la reprend au meme endroit.")]
        public bool pauseLevelMusic=true;
        public AudioMixer musicMixer;
    }
}
