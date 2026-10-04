using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Put this on a NEW Button inside the existing OptionsMenu. It wires its own
// click event, supports mouse/controller Submit, and updates Text or TMP labels.
[DisallowMultipleComponent, RequireComponent(typeof(Button))]
public class SonicTubeCameraOption : MonoBehaviour
{
    Button button;
    Text label;
    TMP_Text tmpLabel;

    void OnEnable()
    {
        button = GetComponent<Button>();
        label = GetComponentInChildren<Text>(true);
        tmpLabel = GetComponentInChildren<TMP_Text>(true);
        button.onClick.AddListener(ToggleMode);
        RefreshLabel();
    }

    void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(ToggleMode);
    }

    public void ToggleMode()
    {
        SonicTubeCameraSettings.Mode = SonicTubeCameraSettings.Mode == SonicTubeCameraMode.Proche
            ? SonicTubeCameraMode.FPS : SonicTubeCameraMode.Proche;
        RefreshLabel();
    }

    public void SetMode(int mode)
    {
        SonicTubeCameraSettings.Mode = mode == 1 ? SonicTubeCameraMode.FPS : SonicTubeCameraMode.Proche;
        RefreshLabel();
    }

    void RefreshLabel()
    {
        string text = "Caméra du tube : " + (SonicTubeCameraSettings.Mode == SonicTubeCameraMode.FPS ? "FPS" : "Proche");
        if (label != null) label.text = text;
        if (tmpLabel != null) tmpLabel.text = text;
    }
}
