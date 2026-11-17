using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// İki oyuncu aynı klavyede (ya da iki gamepad ile) oynar.
///   Oyuncu 1 (Mavi)   : WASD koş, Sol Shift sprint, Space şut (basılı tut), F kayma/plonjon, E pas
///   Oyuncu 2 (Kırmızı): Oklar koş, Sağ Shift sprint, Enter şut, Sağ Ctrl kayma/plonjon
///   Gamepad          : sol çubuk, RT/RB sprint, B şut, X kayma/plonjon, A pas
/// Tek oyunculu antrenmanda (Solo) oyuncu 1 hem WASD hem okları kullanabilir.
/// </summary>
public static class InputReader
{
    public static bool Solo;

#if ENABLE_INPUT_SYSTEM
    static Gamepad Pad(int p)
    {
        var all = Gamepad.all;
        return p < all.Count ? all[p] : null;
    }
#endif

    public static Vector2 Move(int p)
    {
        Vector2 v = Vector2.zero;
        bool wasd = p == 0;
        bool arrows = p == 1 || (Solo && p == 0);
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null)
        {
            if (wasd)
            {
                if (k.wKey.isPressed) v.y += 1f;
                if (k.sKey.isPressed) v.y -= 1f;
                if (k.dKey.isPressed) v.x += 1f;
                if (k.aKey.isPressed) v.x -= 1f;
            }
            if (arrows)
            {
                if (k.upArrowKey.isPressed) v.y += 1f;
                if (k.downArrowKey.isPressed) v.y -= 1f;
                if (k.rightArrowKey.isPressed) v.x += 1f;
                if (k.leftArrowKey.isPressed) v.x -= 1f;
            }
        }
        var g = Pad(p);
        if (g != null)
        {
            Vector2 s = g.leftStick.ReadValue();
            if (s.sqrMagnitude > v.sqrMagnitude) v = s;
        }
#else
        if (wasd)
        {
            if (Input.GetKey(KeyCode.W)) v.y += 1f;
            if (Input.GetKey(KeyCode.S)) v.y -= 1f;
            if (Input.GetKey(KeyCode.D)) v.x += 1f;
            if (Input.GetKey(KeyCode.A)) v.x -= 1f;
        }
        if (arrows)
        {
            if (Input.GetKey(KeyCode.UpArrow)) v.y += 1f;
            if (Input.GetKey(KeyCode.DownArrow)) v.y -= 1f;
            if (Input.GetKey(KeyCode.RightArrow)) v.x += 1f;
            if (Input.GetKey(KeyCode.LeftArrow)) v.x -= 1f;
        }
#endif
        if (v.sqrMagnitude < 0.04f) return Vector2.zero;
        return Vector2.ClampMagnitude(v, 1f);
    }

    public static bool Sprint(int p)
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        var g = Pad(p);
        bool key = k != null && (p == 0 ? k.leftShiftKey.isPressed || (Solo && k.rightShiftKey.isPressed) : k.rightShiftKey.isPressed);
        return key || (g != null && (g.rightTrigger.isPressed || g.rightShoulder.isPressed));
#else
        return p == 0 ? Input.GetKey(KeyCode.LeftShift) || (Solo && Input.GetKey(KeyCode.RightShift)) : Input.GetKey(KeyCode.RightShift);
#endif
    }

    public static bool ShootHeld(int p)
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        var g = Pad(p);
        bool key = k != null && (p == 0 ? k.spaceKey.isPressed : k.enterKey.isPressed || k.numpadEnterKey.isPressed);
        return key || (g != null && g.buttonEast.isPressed);
#else
        return p == 0 ? Input.GetKey(KeyCode.Space) : Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
#endif
    }

    public static bool DivePressed(int p)
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        var g = Pad(p);
        bool key = k != null && (p == 0 ? k.fKey.wasPressedThisFrame : k.rightCtrlKey.wasPressedThisFrame || k.numpad0Key.wasPressedThisFrame);
        return key || (g != null && g.buttonWest.wasPressedThisFrame);
#else
        return p == 0 ? Input.GetKeyDown(KeyCode.F) : Input.GetKeyDown(KeyCode.RightControl) || Input.GetKeyDown(KeyCode.Keypad0);
#endif
    }

    public static bool PassPressed(int p)
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        var g = Pad(p);
        return (p == 0 && k != null && k.eKey.wasPressedThisFrame) || (g != null && g.buttonSouth.wasPressedThisFrame);
#else
        return p == 0 && Input.GetKeyDown(KeyCode.E);
#endif
    }

    public static bool ResetPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        return k != null && k.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }

    public static bool MenuPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        bool pad = false;
        foreach (var g in Gamepad.all) pad |= g.startButton.wasPressedThisFrame;
        return pad || (k != null && k.escapeKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    /// <summary>Menüde 1/2/3 tuşları. Basılmadıysa 0.</summary>
    public static int DigitPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k == null) return 0;
        if (k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame) return 1;
        if (k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame) return 2;
        if (k.digit3Key.wasPressedThisFrame || k.numpad3Key.wasPressedThisFrame) return 3;
        return 0;
#else
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) return 1;
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) return 2;
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) return 3;
        return 0;
#endif
    }
}
