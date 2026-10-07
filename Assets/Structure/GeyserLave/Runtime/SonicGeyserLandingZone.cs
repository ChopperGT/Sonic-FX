using UnityEngine;
namespace SonicFX.Lava
{
    [DisallowMultipleComponent, AddComponentMenu("Sonic FX/Lave/Zone de chute du geyser")]
    public sealed class SonicGeyserLandingZone : MonoBehaviour
    {
        [Min(0), InspectorName("Rayon de dispersion")] public float radius=1.5f;
        [Min(1), InspectorName("Nombre de rochers")] public int rockCount=1;
        [Tooltip("Vide : un des modeles de pierres magma du geyser est choisi."), InspectorName("Modele impose")] public GameObject rockPrefab;
        [Min(.1f), InspectorName("Taille minimale")] public float minimumScale=1;
        [Min(.1f), InspectorName("Taille maximale")] public float maximumScale=1.5f;
        [Min(.3f), InspectorName("Duree du vol (secondes)")] public float flightTime=2.5f;
        [Min(1), InspectorName("Hauteur supplementaire du saut")] public float arcHeight=18;
        [InspectorName("Projeter la cible sur le sol")] public bool projectOnGround=true;
        [Min(1), InspectorName("Recherche du sol au-dessus")] public float groundSearchAbove=30;
        [Min(1), InspectorName("Recherche du sol en dessous")] public float groundSearchBelow=100;
        [Tooltip("Exclure la couche des personnages si possible."), InspectorName("Couches de sol")] public LayerMask groundLayers=~0;
        public Vector3 SamplePoint(Vector2 sample)
        {
            Vector3 p=transform.TransformPoint(new Vector3(sample.x*radius,0,sample.y*radius));
            return p;
        }
        public bool ResolveGround(Vector3 point,out Vector3 landing,out Vector3 normal)
        {
            landing=point;normal=Vector3.up;if(!projectOnGround)return true;
            var hits=new RaycastHit[128];
            int count=gameObject.scene.GetPhysicsScene().Raycast(point+Vector3.up*groundSearchAbove,Vector3.down,hits,groundSearchAbove+groundSearchBelow,groundLayers,QueryTriggerInteraction.Ignore);
            float closest=float.PositiveInfinity;bool found=false;
            for(int i=0;i<count;i++)
            {
                var c=hits[i].collider;
                if(c==null || c.GetComponentInParent<PlayerBhysics>()!=null || c.GetComponentInParent<SonicFX.Magma.SonicMagmaRock>()!=null || c.GetComponentInParent<SonicLavaGeyser>()!=null)continue;
                if(hits[i].normal.y<.2f || hits[i].distance>=closest)continue;
                closest=hits[i].distance;landing=hits[i].point;normal=hits[i].normal;found=true;
            }
            return found;
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color=new Color(1,.15f,.04f,.8f);var previous=Gizmos.matrix;Gizmos.matrix=transform.localToWorldMatrix;
            Vector3 last=new Vector3(radius,0,0);for(int i=1;i<=64;i++){float a=i*Mathf.PI*2/64;var next=new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);Gizmos.DrawLine(last,next);last=next;}Gizmos.DrawLine(Vector3.zero,Vector3.up*2);Gizmos.matrix=previous;
        }
    }
}
