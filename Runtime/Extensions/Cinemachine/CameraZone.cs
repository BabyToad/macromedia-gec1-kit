using Unity.Cinemachine;
using UnityEngine;

namespace Kit
{
    /// <summary>
    /// Kamera-Zone: while the player is inside, another camera shows the game, with a camera move in and out.
    /// The room seen from the corner, the bridge from the side, the hall from above. Leaving gives the picture back
    /// to the camera before (another zone around this one, or the player's own view).
    /// </summary>
    [AddComponentMenu(KitBrand.ComponentMenu + "Kamera/Kamera-Zone")]
    public class CameraZone : AtmosphereZone
    {
        [Hint("Die Kamera, die in der Zone zeigt: ein Objekt mit CinemachineCamera, z. B. mit „Kamera: Fest“ oder „Kamera: Blick auf“.", label: "Kamera")]
        public CinemachineCamera view;

        protected override Color GizmoColor => new Color(0.35f, 0.75f, 1f);
        protected override string GizmoLabel => view ? $"Kamera: {view.name}" : "Kamera-Zone (keine Kamera)";

        protected override void Reset()
        {
            base.Reset();
            seconds = 1.2f;
        }

        // Cinemachine is ready before anyone walks in: the first entry is a camera move, not a cut.
        protected override void OnEnable()
        {
            base.OnEnable();
            if (Application.isPlaying) PlayerCameraBridge.Ensure();
        }

        protected override void Entered()
        {
            if (!view) { Debug.LogWarning($"Kamera-Zone „{name}“: keine Kamera eingetragen.", this); return; }
            CameraDirector.Switch(view, this, seconds, ease);
        }

        protected override void Left() => CameraDirector.Release(this, seconds, ease);

#if UNITY_EDITOR
        protected override void DrawSelected()
        {
            var c = GetComponent<Collider>();
            if (!view || !c) return;
            Gizmos.color = GizmoColor;
            Gizmos.DrawLine(c.bounds.center, view.transform.position);
        }
#endif
    }
}
