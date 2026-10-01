using UnityEngine;

namespace SonicFX.Structures
{
    // Input is an edge (a new press), never the held state of the button.
    public sealed class SonicBreakChallenge
    {
        public float Progress {get;private set;}
        public float Elapsed {get;private set;}
        public bool Won {get;private set;}
        public bool Lost {get;private set;}
        readonly int presses;
        readonly float duration,drain;
        public SonicBreakChallenge(float initial,int requiredPresses,float seconds,float drainPerSecond)
        {
            Progress=Mathf.Clamp(initial,0,.95f);presses=Mathf.Max(1,requiredPresses);
            duration=Mathf.Max(.1f,seconds);drain=Mathf.Max(0,drainPerSecond);
        }
        public static float EntryBonus(float speed,float referenceSpeed,float maximum)
        {
            return Mathf.Clamp01(Mathf.Max(0,speed)/Mathf.Max(.1f,referenceSpeed))*Mathf.Clamp(maximum,0,.95f);
        }
        public void Step(float deltaTime,bool pressed)
        {
            if(Won || Lost || deltaTime<=0)return;
            float usable=Mathf.Min(deltaTime,duration-Elapsed);
            Progress=Mathf.Max(0,Progress-drain*usable);Elapsed+=deltaTime;
            // A press arriving after the deadline cannot complete the challenge.
            if(Elapsed<=duration && pressed)Progress=Mathf.Min(1,Progress+1f/presses);
            if(Progress>=.99999f){Progress=1;Won=true;}
            else if(Elapsed>=duration)Lost=true;
        }
    }
}
