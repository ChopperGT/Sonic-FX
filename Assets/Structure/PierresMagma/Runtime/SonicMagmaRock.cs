using System.Collections.Generic;
using UnityEngine;
namespace SonicFX.Magma
{
    [ExecuteAlways, DisallowMultipleComponent]
    public class SonicMagmaRock : MonoBehaviour
    {
        [InspectorName("Inflige des degats au contact")] public bool contactDamage = true;
        [Min(.1f), InspectorName("Delai entre deux degats (secondes)")] public float damageInterval = 1.5f;
        [Header("Aspect")]
        [InspectorName("Roche")] public Color rockColor = new Color(.055f,.045f,.05f);
        [InspectorName("Magma")] public Color magmaColor = new Color(1,.13f,.005f);
        [InspectorName("Fissures chaudes")] public Color hotColor = new Color(1,.68f,.02f);
        [Range(0,5), InspectorName("Incandescence")] public float glow = 1.8f;
        [Range(.01f,.3f), InspectorName("Largeur des fissures")] public float crackWidth = .06f;
        [Min(.1f), InspectorName("Echelle du motif")] public float patternScale = 2.5f;
        [Min(0), InspectorName("Vitesse de pulsation")] public float pulseSpeed = 1;
        [SerializeField, HideInInspector] Renderer rockRenderer;
        readonly Dictionary<PlayerBhysics,float> nextDamage = new Dictionary<PlayerBhysics,float>();
        MaterialPropertyBlock properties;
        public int DamageEvents { get; private set; }
        void OnEnable() { nextDamage.Clear(); Refresh(); }
        void Update() { Refresh(); }
        public void Refresh()
        {
            if(rockRenderer == null) rockRenderer = GetComponent<Renderer>();
            if(rockRenderer == null) return;
            if(properties == null) properties = new MaterialPropertyBlock();
            properties.SetColor("_RockColor",rockColor); properties.SetColor("_MagmaColor",magmaColor); properties.SetColor("_HotColor",hotColor);
            properties.SetFloat("_Glow",glow); properties.SetFloat("_CrackWidth",crackWidth);
            properties.SetFloat("_PatternScale",Mathf.Max(.1f,patternScale)); properties.SetFloat("_PulseSpeed",pulseSpeed);
            rockRenderer.SetPropertyBlock(properties);
        }
        void OnCollisionEnter(Collision collision) { Contact(collision.collider); }
        void OnCollisionStay(Collision collision) { Contact(collision.collider); }
        void Contact(Collider other)
        {
            if(!Application.isPlaying || other == null || other.isTrigger) return;
            TryDamage(other.GetComponentInParent<PlayerBhysics>(),Time.time);
        }
        public bool TryDamage(PlayerBhysics player, float now)
        {
            if(!isActiveAndEnabled || !contactDamage || player == null || !player.gameObject.activeInHierarchy) return false;
            var hurt = player.GetComponent<HurtControl>(); var interaction = player.GetComponent<Objects_Interaction>();
            if(hurt == null || interaction == null || interaction.Actions == null || hurt.isDead || hurt.IsHurt || hurt.IsInvencible || interaction.Actions.Action == 4) return false;
            if(nextDamage.TryGetValue(player,out float next) && now < next) return false;
            nextDamage[player] = now + Mathf.Max(.1f,damageInterval);
            if(interaction.Actions.Action01 != null && interaction.Actions.Action01.JumpBall != null) interaction.Actions.Action01.JumpBall.SetActive(false);
            interaction.DamagePlayer(); DamageEvents++;
            HedgeCamera.Shakeforce = interaction.EnemyDamageShakeAmmount;
            return true;
        }
    }
}
