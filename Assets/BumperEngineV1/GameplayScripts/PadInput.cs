using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// Drop-in for UnityEngine.Input: legacy keyboard/mouse + Gamepad.current (legacy Input Manager sees no joystick here).
public static class PadInput
{
    static ButtonControl Button(string n)
    {
        var g = Gamepad.current;
        if (g == null) return null;
        switch (n)
        {
            case "A": return g.buttonSouth;
            case "B": return g.buttonEast;
            case "X": return g.buttonWest;
            case "Y": return g.buttonNorth;
            case "R1": return g.rightShoulder;
            case "Start": return g.startButton;
            case "RightStickIn": return g.rightStickButton;
            default: return null;
        }
    }

    public static float GetAxis(string n)
    {
        float v = Input.GetAxis(n);
        var g = Gamepad.current;
        if (g == null) return v;
        float p;
        switch (n)
        {
            case "Horizontal": p = g.leftStick.x.ReadValue(); break;
            case "Vertical": p = g.leftStick.y.ReadValue(); break;
            case "Horizontal_right": p = g.rightStick.x.ReadValue(); break;
            case "Vertical_right": p = g.rightStick.y.ReadValue(); break;
            default: return v;
        }
        return Mathf.Abs(p) > Mathf.Abs(v) ? p : v;
    }

    public static float GetAxisRaw(string n) => GetAxis(n);
    public static bool GetButton(string n) { var b = Button(n); return Input.GetButton(n) || (b != null && b.isPressed); }
    public static bool GetButtonDown(string n) { var b = Button(n); return Input.GetButtonDown(n) || (b != null && b.wasPressedThisFrame); }
    public static bool GetButtonUp(string n) { var b = Button(n); return Input.GetButtonUp(n) || (b != null && b.wasReleasedThisFrame); }
}
