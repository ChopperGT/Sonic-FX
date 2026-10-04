using System;
using UnityEngine;

public static class CrabBallistics
{
    // Apex is above BOTH endpoints, so even an elevated target gets an upward launch.
    public static Vector3 LaunchVelocity(Vector3 start, Vector3 target, float gravity, float arcHeight, out float flightTime)
    {
        if (gravity <= 0f || arcHeight <= 0f || float.IsNaN(gravity) || float.IsNaN(arcHeight))
            throw new ArgumentOutOfRangeException("Gravity and arc height must be positive.");
        float apex = Math.Max(start.y, target.y) + arcHeight;
        float upSpeed = (float)Math.Sqrt(2.0 * gravity * (apex - start.y));
        flightTime = upSpeed / gravity + (float)Math.Sqrt(2.0 * (apex - target.y) / gravity);
        return new Vector3((target.x - start.x) / flightTime, upSpeed, (target.z - start.z) / flightTime);
    }

    public static Vector3 Position(Vector3 start, Vector3 velocity, float gravity, float time)
    {
        return start + velocity * time + Vector3.down * (0.5f * gravity * time * time);
    }
}
