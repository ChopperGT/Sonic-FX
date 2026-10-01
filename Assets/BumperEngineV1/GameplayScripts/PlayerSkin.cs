using UnityEngine;
using System.Collections;

public class PlayerSkin : MonoBehaviour {

    public Animator CharacterAnimator;
    public PlayerBhysics Player;

    public float skinRotationSpeed;

    void Update()
    {

        CharacterAnimator.SetInteger("Action", 0);
        CharacterAnimator.SetFloat("GroundSpeed", GetComponent<Rigidbody>().linearVelocity.magnitude);

        Quaternion CharRot = Quaternion.LookRotation(GetComponent<Rigidbody>().linearVelocity, transform.up);  
        CharacterAnimator.transform.rotation = Quaternion.Lerp(CharacterAnimator.transform.rotation, CharRot, Time.deltaTime * skinRotationSpeed);

    }

}
