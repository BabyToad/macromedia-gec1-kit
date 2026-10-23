using Unity.Cinemachine;
using UnityEngine;

namespace Kit
{
    /// <summary>
    /// The Spieler hides its own body in first person (KitPlayerCamera, "hideInFirstPerson"), so the camera does not
    /// look at the inside of its head. Once another camera shows the game (a Kamera-Zone, a fixed shot, a cut-scene),
    /// that body must be visible again, or the player walks through the shot invisible. This sits on the
    /// Spieler-Sicht and shows the body whenever the picture is not purely the player's own first-person view.
    /// Runs after KitPlayerCamera (which hides) and after the Brain (execution order 100), so it sees the camera and
    /// the camera move of this very frame, before anything is drawn.
    /// </summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(CinemachineCamera))]
    public sealed class SpielerSichtBody : MonoBehaviour
    {
        KitPlayerCamera m_View;
        CinemachineCamera m_Cam;

        void Awake() { m_View = GetComponent<KitPlayerCamera>(); m_Cam = GetComponent<CinemachineCamera>(); }

        /// <summary>Is the picture right now only the player's own view (no other camera, no camera move)?</summary>
        public bool OwnViewOnly
        {
            get
            {
                var brain = CameraDirector.Brain();
                if (!brain) return true;
                return !brain.IsBlending && ReferenceEquals(brain.ActiveVirtualCamera, m_Cam);
            }
        }

        void LateUpdate()
        {
            if (!m_View || !m_View.enabled || m_View.hideInFirstPerson == null || !m_View.player) return;
            if (m_View.player.view != KitPlayer.View.ErstePerson || OwnViewOnly) return;   // KitPlayerCamera has it right
            foreach (var r in m_View.hideInFirstPerson) if (r) r.enabled = true;          // another camera shows the player
        }
    }
}
