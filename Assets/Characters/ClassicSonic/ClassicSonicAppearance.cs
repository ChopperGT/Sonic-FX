using UnityEngine;

// Classic has several body meshes; show/hide them as one character and keep
// Mania's separate curled-up mesh for rolling, jumping and charging.
[DisallowMultipleComponent]
public sealed class ClassicSonicAppearance : MonoBehaviour
{
    public SkinnedMeshRenderer[] BodySkins;
    public SkinnedMeshRenderer ManiaBall;
    ActionManager actions;
    PlayerBhysics player;
    HurtControl hurt;

    void LateUpdate() { RefreshVisuals(); }

    public void RefreshVisuals()
    {
        RefreshVisuals(Time.time);
    }

    public void RefreshVisuals(float time)
    {
        if (!actions) actions = GetComponent<ActionManager>();
        if (!player) player = GetComponent<PlayerBhysics>();
        if (!hurt) hurt = GetComponent<HurtControl>();
        if (!actions || !player || !ManiaBall) return;
        bool damaged = actions.Action == 4 || (hurt && hurt.isDead);
        bool useBall = !damaged && !actions.BallBlocked &&
            ((actions.Action == 0 && player.isRolling) || actions.Action == 1 ||
             actions.Action == 3 || actions.Action == 6 || actions.Action == 8);
        // HurtControl and SpinDash both write renderer.enabled. Resolve their
        // competing writes after Update, with one blink phase for the whole model.
        bool visible = !hurt || !hurt.IsInvencible || hurt.isDead ||
            Mathf.Repeat(time * 10f, 2f) < 1f;
        foreach (var skin in BodySkins) if (skin) skin.enabled = visible && !useBall;
        ManiaBall.enabled = visible && useBall;
    }
}
