using UnityEngine;

// Root inspector for both entrances. SphereCollider remains the source of truth,
// so existing per-instance collider overrides are preserved.
[DisallowMultipleComponent, AddComponentMenu("Sonic FX/Tube/Zones d'entree")]
public sealed class SonicTubeEntryZones : MonoBehaviour
{
    public SonicTube FindEntrance(bool atEnd)
    {
        foreach (var entrance in GetComponentsInChildren<SonicTube>(true))
            if (entrance.entranceAtEnd == atEnd) return entrance;
        return null;
    }
}
