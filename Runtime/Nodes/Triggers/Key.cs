using System;
using UnityEngine;

namespace Kit
{
    // Taste: ein Ereignis beim Drücken, eins beim Loslassen. "Gehalten" ist ein Wert, kein Ereignis.
    [Serializable]
    [NodeInfo("Taste", "Auslöser", "Taste gedrückt oder losgelassen")]
    public class Key : KitNode
    {
        [Setting("Taste")] public KeyCode key = KeyCode.Space;

        [Output("Gedrückt")] [NonSerialized] public Output pressed;
        [Output("Losgelassen")] [NonSerialized] public Output released;

        [DataOut("Gehalten")] public bool Held => KitInput.Held(key);

        public override void Tick()
        {
            if (KitInput.Down(key)) { Done("✓ Gedrückt"); pressed.Fire(new Signal()); }
            if (KitInput.Up(key)) { Done("✓ Losgelassen"); released.Fire(new Signal()); }
        }

        public override string Readout() => $"{key}: {(Held ? "gehalten" : "frei")}";
    }
}
