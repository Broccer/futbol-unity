using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Klavye + gamepad girdisi. Proje "Input System" ya da eski "Input Manager" ile
/// açılsa da derlenir.
///   WASD / Ok tuşları  : koş
///   Shift              : sprint
///   Space (basılı tut) : şut gücü doldur, bırakınca vur
///   E                  : pas
///   R                  : pozisyonları sıfırla
/// Gamepad: sol çubuk koş, RT/RB sprint, B (basılı tut) şut, A pas, Y sıfırla.
/// </summary>
public static class InputReader
{
    public static Vector2 Move()
    {
        Vector2 v = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null)
        {
            if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1f;
            if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1f;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1f;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1f;
        }
        var g = Gamepad.current;
        if (g != null)
        {
            Vector2 s = g.leftStick.ReadValue();
            if (s.sqrMagnitude > v.sqrMagnitude) v = s;
        }
#else
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v.y += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v.y -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) v.x += 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) v.x -= 1f;
#endif
        if (v.sqrMagnitude < 0.04f) return Vector2.zero; // çubuk ölü bölgesi
        return Vector2.ClampMagnitude(v, 1f);
    }

    public static bool Sprint()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        var g = Gamepad.current;
        return (k != null && (k.leftShiftKey.isPressed || k.rightShiftKey.isPressed))
            || (g != null && (g.rightTrigger.isPressed || g.rightShoulder.isPressed));
#else
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
    }

    public static bool ShootHeld()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        var g = Gamepad.current;
        return (k != null && k.spaceKey.isPressed) || (g != null && g.buttonEast.isPressed);
#else
        return Input.GetKey(KeyCode.Space);
#endif
    }

    public static bool PassPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        var g = Gamepad.current;
        return (k != null && k.eKey.wasPressedThisFrame) || (g != null && g.buttonSouth.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

    public static bool ResetPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        var g = Gamepad.current;
        return (k != null && k.rKey.wasPressedThisFrame) || (g != null && g.buttonNorth.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }
}
