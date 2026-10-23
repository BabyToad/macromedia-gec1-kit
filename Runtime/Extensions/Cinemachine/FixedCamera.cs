using UnityEngine;

namespace Kit
{
    /// <summary>Kamera: Fest — steht still und schaut in eine feste Richtung. Wie eine Überwachungskamera, ein Bühnenbild.</summary>
    [AddComponentMenu(KitBrand.ComponentMenu + "Kamera/Fest")]
    public class FixedCamera : CameraPreset
    {
        public override string Kind => "Fest";
        public override void Configure() { Camera.Target.TrackingTarget = null; }   // folgt niemandem: Position und Drehung bestimmt das Objekt
    }
}
