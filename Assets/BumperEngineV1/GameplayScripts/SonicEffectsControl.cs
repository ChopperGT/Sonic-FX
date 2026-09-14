using UnityEngine;
using System.Collections;

public class SonicEffectsControl : MonoBehaviour {

    public PlayerBhysics Player;
    public ParticleSystem RunningDust;
	public ParticleSystem SpeedLines;
    public ParticleSystem SpinDashDust;
    public float RunningDustThreshold;
	public float SpeedLinesThreshold;

	void FixedUpdate () {
	
		if(Player.p_rigidbody.linearVelocity.sqrMagnitude > RunningDustThreshold && Player.Grounded && RunningDust != null)
        {
            RunningDust.Emit(Random.Range(0,20));
        }

		if (Player.p_rigidbody.linearVelocity.sqrMagnitude > SpeedLinesThreshold && Player.Grounded && SpeedLines != null && SpeedLines.isPlaying == false) 
		{
			SpeedLines.Play ();
		} 
		else if (Player.p_rigidbody.linearVelocity.sqrMagnitude < SpeedLinesThreshold && SpeedLines.isPlaying == true || (!Player.Grounded)) 
		{
			SpeedLines.Stop ();
		}

	}
    public void DoSpindashDust(int amm, float speed)
    {
        SpinDashDust.startSpeed = speed;
        SpinDashDust.Emit(amm);
    }

}
