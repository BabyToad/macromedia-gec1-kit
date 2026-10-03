using UnityEngine;
using UnityEngine.InputSystem;

namespace Kit
{
    // Die Eingaben des Spielers, direkt im Code angelegt (ohne Input-Actions-Datei), damit alles an
    // einer Stelle lesbar ist. Jede Aktion hat Bindungen für Tastatur/Maus UND Gamepad;
    // das Input System nimmt, was gerade benutzt wird.
    public class PlayerControls
    {
        public readonly InputAction Move = new InputAction("Bewegen", InputActionType.Value, expectedControlType: "Vector2");
        public readonly InputAction LookMouse = new InputAction("Umschauen (Maus)", InputActionType.Value, "<Mouse>/delta");
        public readonly InputAction LookStick = new InputAction("Umschauen (Stick)", InputActionType.Value, "<Gamepad>/rightStick");
        public readonly InputAction Jump = new InputAction("Springen", InputActionType.Button);
        public readonly InputAction Run = new InputAction("Rennen", InputActionType.Button);
        public readonly InputAction SwitchView = new InputAction("Ansicht wechseln", InputActionType.Button);

        public PlayerControls()
        {
            Move.AddCompositeBinding("2DVector")                      // WASD
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddCompositeBinding("2DVector")                      // Pfeiltasten
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            Move.AddBinding("<Gamepad>/leftStick");

            Jump.AddBinding("<Keyboard>/space");
            Jump.AddBinding("<Gamepad>/buttonSouth");                 // A (Xbox) / Kreuz (PlayStation)
            Run.AddBinding("<Keyboard>/leftShift");
            Run.AddBinding("<Gamepad>/leftTrigger");
            SwitchView.AddBinding("<Keyboard>/v");
            SwitchView.AddBinding("<Gamepad>/select");
        }

        public void Enable() { foreach (var a in All) a.Enable(); }
        public void Disable() { foreach (var a in All) a.Disable(); }

        InputAction[] All => new[] { Move, LookMouse, LookStick, Jump, Run, SwitchView };
    }
}
