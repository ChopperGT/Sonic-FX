using UnityEngine;
using System.Collections;

public class Action01_Jump : MonoBehaviour {

    public Animator CharacterAnimator;
    PlayerBhysics Player;
    ActionManager Actions;
    public SonicSoundsControl sounds;

    public float skinRotationSpeed;
	public GameObject JumpBall;

    public Vector3 InitialNormal { get; set; }
    public float Counter { get; set; }
    public float JumpDuration;
    public float SlopedJumpDuration;
    public float JumpSpeed;
    public float JumpSlopeConversion;
    public float StopYSpeedOnRelease;
    public float RollingLandingBoost;

    float jumpSlopeSpeed;

    void Awake()
    {
        Player = GetComponent<PlayerBhysics>();
        Actions = GetComponent<ActionManager>();
    }

    public void InitialEvents()
    {
        //Set Initial Variables
		JumpBall.SetActive(true);
        Counter = 0;
        jumpSlopeSpeed = 0;
        InitialNormal = Player.GroundNormal;
        Player.TimeOnGround = 0;
        //Debug.Log(Player.GroundNormal);
        
        //SnapOutOfGround to make sure you do jump
        transform.position += (InitialNormal * 0.3f);

        //Jump higher depending on the speed and the slope you're in
        if(Player.p_rigidbody.linearVelocity.y > 0 && Player.GroundNormal.y > 0)
        {
            jumpSlopeSpeed = Player.p_rigidbody.linearVelocity.y * JumpSlopeConversion;
        }
        //Sound
        sounds.JumpSound();
    }

    void Update()
	{

		//Set Animator Parameters
		if (Actions.Action == 1) {
			CharacterAnimator.SetInteger ("Action", 1);
		}
		CharacterAnimator.SetFloat ("YSpeed", Player.p_rigidbody.linearVelocity.y);
		CharacterAnimator.SetFloat ("GroundSpeed", Player.p_rigidbody.linearVelocity.magnitude);
		CharacterAnimator.SetBool ("Grounded", Player.Grounded);
		CharacterAnimator.SetBool ("isRolling", false);

		//Set Animation Angle
		Vector3 VelocityMod = new Vector3 (Player.p_rigidbody.linearVelocity.x, 0, Player.p_rigidbody.linearVelocity.z);
		Quaternion CharRot = Quaternion.LookRotation (VelocityMod, transform.up);
		CharacterAnimator.transform.rotation = Quaternion.Lerp (CharacterAnimator.transform.rotation, CharRot, Time.deltaTime * skinRotationSpeed);

		if (Actions.Action02 != null) {
			Actions.Action02.HomingAvailable = true;
		}
		if (Actions.Action06 != null) {
			Actions.Action06.BounceAvailable = true;
		}
		if (Actions.Action08 != null) {
			Actions.Action08.DropDashAvailable = true;
		}

		//Do a homing attack
		if (Actions.Action02 != null) {
			
			if (Counter > 0.08f && Input.GetButtonDown ("A") && Actions.Action02Control.HasTarget && Actions.Action02.HomingAvailable) {
				if (Actions.Action02Control.HomingAvailable) {
					sounds.HomingAttackSound ();
					Actions.Action02.IsAirDash = false;
					Actions.ChangeAction (2);
					Actions.Action02.InitialEvents ();
				}
			}
			//If no tgt, do air dash;
			if (Counter > 0.08f && Input.GetButtonDown ("A") && !Actions.Action02Control.HasTarget && Actions.Action02.HomingAvailable && Actions.Action08 == null) {
				if (Actions.Action02Control.HomingAvailable) {
					sounds.AirDashSound ();
					Actions.Action02.IsAirDash = true;
					Actions.ChangeAction (2);
					Actions.Action02.InitialEvents ();
				}
			}
		}

		//Do a Bounce Attack
		if (Input.GetButtonDown("X") && Actions.Action06.BounceAvailable)
		{
			Actions.ChangeAction (6);
		//	Actions.Action06.ShouldStomp = false;
			Actions.Action06.InitialEvents ();
		}
		//Do a LightDash Attack
		if (Input.GetButtonDown("Y") && Actions.Action07Control.HasTarget)
		{
			Actions.ChangeAction (7);
			Actions.Action07.InitialEvents ();
		}
		//Do a DropDash Attack
		if (Actions.Action08 != null) 
		{
			if (Counter > 0.08f && Input.GetButtonDown ("A") && Actions.Action08.DropDashAvailable && Actions.Action08 != null && !Actions.Action02Control.HasTarget) {
				Actions.ChangeAction (8);

				Actions.Action08.InitialEvents ();
			}
		}



    }

    void FixedUpdate () {

        //Jump action
        Counter += Time.deltaTime;

        if(!Input.GetButton("A") && Counter < JumpDuration)
        {
            Counter = JumpDuration;
        }

        //Keep Colliders Rotation to avoid collision Issues
        if (Counter < 0.2f)
        {
            //transform.rotation = Quaternion.FromToRotation(transform.up, InitialNormal) * transform.rotation;
        }

        //Add Jump Speed
        if (Counter < JumpDuration)
        {
            Player.isRolling = false;
            if(Counter < SlopedJumpDuration)
            {
                Player.AddVelocity(InitialNormal * (JumpSpeed));
            }
            else
            {
                Player.AddVelocity(new Vector3(0,1,0) * (JumpSpeed));
            }
            //Extra speed
            Player.AddVelocity(new Vector3(0, 1, 0) * (jumpSlopeSpeed));
        }

        //Cancel Jump
        if (Player.p_rigidbody.linearVelocity.y > 0 && !Input.GetButton("A"))
        {
            Vector3 Velocity = new Vector3(Player.p_rigidbody.linearVelocity.x, Player.p_rigidbody.linearVelocity.y, Player.p_rigidbody.linearVelocity.z);
            Velocity.y = Velocity.y - StopYSpeedOnRelease;
            Player.p_rigidbody.linearVelocity = Velocity;
        }


        //End Action
        if (Player.Grounded && Counter > SlopedJumpDuration)
        {
            Actions.ChangeAction(0);
            
			Actions.Action06.BounceCount = 0;
			JumpBall.SetActive(false);
        }

    }
}
