using Unity.Cinemachine;
using UnityEngine;

namespace Kit
{
    /// <summary>Kamera: Verfolgen — hängt dem Ziel in festem Abstand hinterher, gedämpft, und hält es im Bild.</summary>
    [AddComponentMenu(KitBrand.ComponentMenu + "Kamera/Verfolgen")]
    public class FollowCamera : CameraPreset
    {
        public override string Kind => "Verfolgen";
        public override void Configure()
        {
            var f = Ensure<CinemachineFollow>();
            f.FollowOffset = new Vector3(0f, 4f, -7f);              // schräg von hinten oben
            f.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;   // Blickwinkel bleibt fest, auch wenn das Ziel sich dreht
            f.TrackerSettings.PositionDamping = new Vector3(0.6f, 0.4f, 0.6f);
            var r = Ensure<CinemachineRotationComposer>();
            r.Damping = new Vector2(0.4f, 0.4f);
        }
    }
}
