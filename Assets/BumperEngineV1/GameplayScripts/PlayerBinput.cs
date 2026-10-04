using UnityEngine;
using System.Collections;

public class PlayerBinput : MonoBehaviour {

    private PlayerBhysics Player; // Reference to the ball controller.
    CameraControl Cam;
    ActionManager Actions;

	public Vector3 moveAcc { get; set; }
    private Vector3 move;
    // the world-relative desired move direction, calculated from the camForward and user input.

    private Transform cam; // A reference to the main camera in the scenes transform
    private Vector3 camForward; // The current forward direction of the camera

	private bool PreviousInputWasNull;

    [Header("Direction a grande vitesse")]
    [Tooltip("Empeche les courbes de vitesse de rendre les commandes presque inactives.")]
    public bool responsiveSteering = true;
    [Min(0), Tooltip("Reactivite minimale des commandes par seconde. 10 garde une direction reactive meme a pleine vitesse.")]
    public float minimumInputResponse = 10;

    public AnimationCurve InputLerpingRateOverSpeed;
    public bool UtopiaTurning;
    public AnimationCurve UtopiaInputLerpingRateOverSpeed;
    public float InputLerpSpeed { get; set; }
    public Vector3 UtopiaInput { get; set; }
    public float UtopiaIntensity;
    public float UtopiaInitialInputLerpSpeed;
    public float UtopiaLerpingSpeed { get; set; }
    float InitialInputMag;
    float InitialLerpedInput;

    bool LockInput { get; set; }
    float LockedTime;
    Vector3 LockedInput;
    float LockedCounter = 0;
    bool LockCam { get; set; }
    public float prevDecel { get; set; }
	private bool HittingWall;

    private void Awake()
    {
        // Set up the reference.
        Player = GetComponent<PlayerBhysics>();
        Actions = GetComponent<ActionManager>();
        Cam = GetComponent<CameraControl>();
        prevDecel = Player.MoveDecell;

        // get the transform of the main camera
        if (Camera.main != null)
        {
            cam = Camera.main.transform;
        }
    }

    private void Update()
    {
        // Get curve position

        float speedRatio = Player.p_rigidbody.linearVelocity.sqrMagnitude / Mathf.Max(.01f, Player.MaxSpeed * Player.MaxSpeed);
        InputLerpSpeed = InputLerpingRateOverSpeed.Evaluate(speedRatio);
        UtopiaLerpingSpeed = UtopiaInputLerpingRateOverSpeed.Evaluate(speedRatio);

        // Get the axis and jump input.

        float h = PadInput.GetAxis("Horizontal");
        float v = PadInput.GetAxis("Vertical");

		// calculate move direction
		if (cam != null)
		{

			//Vector3 moveInp = new Vector3(h, 0, v);
			Vector3 moveInp = new Vector3(h, 0, v);

			InitialInputMag = moveInp.sqrMagnitude;
			InitialLerpedInput = Mathf.Lerp(InitialLerpedInput, InitialInputMag, Time.deltaTime * UtopiaInitialInputLerpSpeed);

			float currentInputSpeed = (!UtopiaTurning) ? InputLerpSpeed : UtopiaLerpingSpeed;

				if (moveInp != Vector3.zero)
				{
					Vector3 transformedInput;
					transformedInput = Quaternion.FromToRotation(cam.up, Player.GroundNormal) * (cam.rotation * moveInp);    
					transformedInput = transform.InverseTransformDirection (transformedInput);
					transformedInput.y = 0.0f;
					

					Player.RawInput = transformedInput;
					moveInp = SmoothMovementInput(move, transformedInput, currentInputSpeed, Time.deltaTime);
				}
				else
				{
					//Debug.Log ("InputNull");
					Vector3 transformedInput = Quaternion.FromToRotation(cam.up, Player.GroundNormal) * (cam.rotation * moveInp);
					transformedInput = transform.InverseTransformDirection(transformedInput);
					transformedInput.y = 0.0f;
					Player.RawInput = transformedInput;
					moveInp = SmoothMovementInput(move, transformedInput, UtopiaLerpingSpeed * UtopiaIntensity, Time.deltaTime);
				}
				
			if (moveInp.x < 0.01 && moveInp.z < 0.01 && moveInp.x > -0.01 && moveInp.z > -0.01) 
			{
				moveInp = Vector3.zero;
			}

			move = moveInp;



		}

        //Lock Input Funcion
        if (LockInput)
        {
            LockedInputFunction();
        }

    }



    // Exponential smoothing keeps response identical across rendering frame rates.
    // The configured curves still apply; only their excessively slow values are floored.
    Vector3 SmoothMovementInput(Vector3 current, Vector3 desired, float curveRate, float seconds)
    {
        float rate = Mathf.Max(0, curveRate);
        if (responsiveSteering) rate = Mathf.Max(rate, Mathf.Max(0, minimumInputResponse));
        float blend = 1 - Mathf.Exp(-rate * Mathf.Max(0, seconds));
        return Vector3.Lerp(current, desired, blend);
    }

    void FixedUpdate()
    {

        Debug.DrawRay(transform.position, move, Color.cyan);
        Player.MoveInput = (move);

    }

    void LockedInputFunction()
    {
        move = Vector3.zero;
        LockedCounter += 1;
        Player.MoveDecell = 1;
        Player.b_normalSpeed = 0;

        if (LockCam)
        {
            Cam.Cam.FollowDirection(3, 14, -10,0);
        }

        if (Actions.Action != 0)
        {
            LockedCounter = LockedTime;
        }

        if (LockedCounter > LockedTime)
        {
            Player.MoveDecell = prevDecel;
            LockInput = false;
        }
    }

    public void LockInputForAWhile(float duration, bool lockCam)
    {
        LockedTime = duration;
        LockedCounter = 0;
        LockInput = true;
        LockCam = lockCam;
    }

}
