using System.Collections.Generic;
using UnityEngine;

public enum SonicTubeCameraMode { Proche = 0, FPS = 1 }

public static class SonicTubeCameraSettings
{
    const string PreferenceKey = "SONIC_FX_TUBE_CAMERA_MODE";
    public static SonicTubeCameraMode Mode
    {
        get { return PlayerPrefs.GetInt(PreferenceKey, 0) == 1 ? SonicTubeCameraMode.FPS : SonicTubeCameraMode.Proche; }
        set
        {
            PlayerPrefs.SetInt(PreferenceKey, value == SonicTubeCameraMode.FPS ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}

// Added automatically to the gameplay camera on the first ride. The normal
// camera keeps updating its internal state; this component overrides only the
// final rendered pose, then blends back to that live normal pose on exit.
[DefaultExecutionOrder(20000), DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public class SonicTubeCamera : MonoBehaviour
{
    [Header("Vue proche")]
    [Min(0.1f)] public float closeDistance = 1.8f;
    public float closeHeight = 0.45f;
    [Header("Vue FPS")]
    public float firstPersonHeight = 0.35f;
    [Header("Suivi")]
    [Min(0.1f)] public float lookAhead = 2.5f;
    [Min(0.01f)] public float nearClipInTube = 0.04f;
    [Min(0f)] public float returnDuration = 0.4f;

    Camera view;
    HedgeCamera normalCamera;
    SonicTube owner;
    PlayerBhysics rider;
    Animator skin;
    readonly Dictionary<Renderer, bool> hidden = new Dictionary<Renderer, bool>();
    bool controlling, returning;
    float originalNearClip, returnTime;
    Vector3 lastPosition;
    Quaternion lastRotation;

    public static SonicTubeCamera BeginRide(SonicTube tube, PlayerBhysics player, Animator animator)
    {
        var control = player.GetComponent<CameraControl>();
        var normal = control != null ? control.Cam : null;
        Camera camera = normal != null ? normal.GetComponent<Camera>() : null;
        if (camera == null) camera = Camera.main;
        if (camera == null)
        {
            Debug.LogWarning("SonicTube : camera de jeu introuvable, trajet sans changement de vue.", tube);
            return null;
        }
        if (normal == null) normal = camera.GetComponent<HedgeCamera>();
        // The inspected kit drives the Camera transform directly with HedgeCamera.
        if (normal == null || normal.transform != camera.transform || !normal.isActiveAndEnabled)
        {
            Debug.LogWarning("SonicTube : la camera doit avoir son composant HedgeCamera actif pour restituer la vue normale.", tube);
            return null;
        }
        var follower = camera.GetComponent<SonicTubeCamera>();
        if (follower == null) follower = camera.gameObject.AddComponent<SonicTubeCamera>();
        if (follower.controlling && follower.owner != tube)
        {
            Debug.LogWarning("SonicTube : cette camera suit deja un autre trajet.", tube);
            return null;
        }
        follower.enabled = true;
        follower.view = camera;
        follower.normalCamera = normal;
        follower.RestoreRenderers();
        // A second ride can begin while the previous exit is still blending.
        if (!follower.returning) follower.originalNearClip = camera.nearClipPlane;
        follower.returning = false;
        follower.returnTime = 0;
        follower.owner = tube;
        follower.rider = player;
        follower.skin = animator;
        follower.controlling = true;
        follower.ApplyTubeView();
        return follower;
    }

    void LateUpdate()
    {
        if (controlling)
        {
            if (owner == null || !owner.isActiveAndEnabled || rider == null ||
                !rider.gameObject.activeInHierarchy || normalCamera == null || !normalCamera.isActiveAndEnabled)
            {
                StopFollowing();
                return;
            }
            ApplyTubeView();
        }
        else if (returning && view != null)
        {
            // HedgeCamera has already computed the normal pose for this frame.
            returnTime += Time.deltaTime;
            float t = returnDuration <= 0f ? 1f : Mathf.Clamp01(returnTime / returnDuration);
            float blend = t * t * (3f - 2f * t);
            transform.SetPositionAndRotation(Vector3.Lerp(lastPosition, transform.position, blend),
                Quaternion.Slerp(lastRotation, transform.rotation, blend));
            view.nearClipPlane = Mathf.Lerp(Mathf.Min(originalNearClip, nearClipInTube), originalNearClip, blend);
            if (t >= 1f) { returning = false; view.nearClipPlane = originalNearClip; }
        }
    }

    void ApplyTubeView()
    {
        bool fps = SonicTubeCameraSettings.Mode == SonicTubeCameraMode.FPS;
        if (!owner.TryGetTubeCameraPose(fps ? 0f : closeDistance,
            fps ? firstPersonHeight : closeHeight, lookAhead, out Vector3 position, out Quaternion rotation))
        {
            StopFollowing();
            return;
        }
        if (fps)
        {
            HideRenderers(rider.transform);
            if (skin != null) HideRenderers(skin.transform);
            var actions = rider.GetComponent<ActionManager>();
            if (actions != null && actions.Action01 != null && actions.Action01.JumpBall != null)
                HideRenderers(actions.Action01.JumpBall.transform);
        }
        else RestoreRenderers();
        view.nearClipPlane = Mathf.Min(originalNearClip, Mathf.Max(0.01f, nearClipInTube));
        transform.SetPositionAndRotation(position, rotation);
        lastPosition = position;
        lastRotation = rotation;
    }

    void HideRenderers(Transform root)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!hidden.ContainsKey(renderer)) hidden.Add(renderer, renderer.forceRenderingOff);
            renderer.forceRenderingOff = true;
        }
    }

    void RestoreRenderers()
    {
        foreach (var pair in hidden) if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
        hidden.Clear();
    }

    public void EndRide(SonicTube tube)
    {
        if (controlling && owner == tube) StopFollowing();
    }

    void StopFollowing()
    {
        RestoreRenderers();
        returning = controlling && view != null && normalCamera != null && normalCamera.isActiveAndEnabled;
        controlling = false;
        returnTime = 0;
        owner = null; rider = null; skin = null;
        if (!returning && view != null) view.nearClipPlane = originalNearClip;
    }

    void OnDisable()
    {
        bool hadControl = controlling || returning;
        RestoreRenderers();
        controlling = false; returning = false;
        owner = null; rider = null; skin = null;
        if (hadControl && view != null) view.nearClipPlane = originalNearClip;
    }
}
