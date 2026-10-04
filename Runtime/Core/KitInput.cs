using UnityEngine;

namespace Kit
{
    /// <summary>Keyboard input that works with the old Input Manager and with the Input System package.</summary>
    public static class KitInput
    {
#if KIT_INPUTSYSTEM && ENABLE_INPUT_SYSTEM
        static UnityEngine.InputSystem.Controls.KeyControl Control(KeyCode code)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return null;
            string n = code.ToString();
            if (n.StartsWith("Alpha")) n = "Digit" + n.Substring(5);
            else if (n == "Return") n = "Enter";
            else if (n.EndsWith("Control")) n = n.Replace("Control", "Ctrl");
            return System.Enum.TryParse(n, out UnityEngine.InputSystem.Key key) && key != UnityEngine.InputSystem.Key.None ? kb[key] : null;
        }
        // Gamepad: E (Benutzen) = linke Gesichtstaste (X / Quadrat), Leertaste = untere (A / Kreuz).
        static UnityEngine.InputSystem.Controls.ButtonControl Pad(KeyCode code)
        {
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad == null) return null;
            return code == KeyCode.E ? pad.buttonWest : code == KeyCode.Space ? pad.buttonSouth : null;
        }
        // Keyboard and gamepad act as ONE button: it is held if either is held, and it is pressed/released
        // only when that combined state changes (holding Space and tapping A gives no second "pressed").
        static bool WasHeld(UnityEngine.InputSystem.Controls.ButtonControl b) =>
            b != null && (b.isPressed ? !b.wasPressedThisFrame : b.wasReleasedThisFrame);
        static bool IsHeld(UnityEngine.InputSystem.Controls.ButtonControl b) => b != null && b.isPressed;

        public static bool Held(KeyCode k) => IsHeld(Control(k)) || IsHeld(Pad(k));
        static bool HeldBefore(KeyCode k) => WasHeld(Control(k)) || WasHeld(Pad(k));
        public static bool Down(KeyCode k) => Held(k) && !HeldBefore(k);
        public static bool Up(KeyCode k) => !Held(k) && HeldBefore(k);
#elif ENABLE_LEGACY_INPUT_MANAGER
        public static bool Down(KeyCode k) => Input.GetKeyDown(k);
        public static bool Up(KeyCode k) => Input.GetKeyUp(k);
        public static bool Held(KeyCode k) => Input.GetKey(k);
#else
        public static bool Down(KeyCode k) => false;
        public static bool Up(KeyCode k) => false;
        public static bool Held(KeyCode k) => false;
#endif
    }
}
