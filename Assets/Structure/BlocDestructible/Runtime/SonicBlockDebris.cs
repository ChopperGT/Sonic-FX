using UnityEngine;
namespace SonicFX.Structures
{
    public sealed class SonicBlockDebris : MonoBehaviour
    {
        public Vector3 velocity;
        public float lifetime=1.5f;
        float age;Vector3 scale;
        void Start(){scale=transform.localScale;}
        void Update()
        {
            age+=Time.deltaTime;velocity+=Vector3.down*18*Time.deltaTime;transform.position+=velocity*Time.deltaTime;
            transform.Rotate(new Vector3(100,150,70)*Time.deltaTime);transform.localScale=scale*Mathf.Clamp01((lifetime-age)/.4f);
            if(age>=lifetime)Destroy(gameObject);
        }
    }
}
