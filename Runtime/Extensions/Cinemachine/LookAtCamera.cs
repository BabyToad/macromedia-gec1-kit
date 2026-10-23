using Unity.Cinemachine;
using UnityEngine;

namespace Kit
{
    /// <summary>Kamera: Blick auf — steht still, dreht sich aber mit dem Ziel mit. Wie ein Kameramann auf einem Stativ.</summary>
    [AddComponentMenu(KitBrand.ComponentMenu + "Kamera/Blick auf")]
    public class LookAtCamera : CameraPreset
    {
        public override string Kind => "Blick auf";
        public override void Configure()
        {
            var r = Ensure<CinemachineRotationComposer>();
            r.Damping = new Vector2(0.6f, 0.6f);
        }
    }
}
