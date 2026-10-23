using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kit
{
    /// <summary>
    /// Bildstimmungs-Zone: inside, the picture takes on this zone's look (colour, contrast, vignette, glow …), blended
    /// in over a few seconds and out again on leaving. The look itself is a URP Volume on the same object; its
    /// profile is where the look is tuned, exactly as in any Unity project. The kit only adds the "when".
    /// </summary>
    [AddComponentMenu(KitBrand.ComponentMenu + "Atmosphäre/Bildstimmungs-Zone")]
    [RequireComponent(typeof(Volume))]
    public class MoodZone : AtmosphereZone
    {
        [Hint("Wie stark die Bildstimmung in der Zone wirkt. Feinarbeit am Profil der Volume-Komponente darunter.", range: "0 bis 1", edges: "1: ganz. 0,5: halb.", label: "Stärke")]
        [Range(0f, 1f)] public float strength = 1f;

        Volume m_Volume;
        int m_Ticket;
        static int s_Order = 100;   // later entered = higher priority = wins where two zones overlap

        protected override Color GizmoColor => new Color(1f, 0.75f, 0.3f);
        protected override string GizmoLabel => "Bildstimmung" + (Volume && Volume.sharedProfile ? ": " + Volume.sharedProfile.name : "");

        Volume Volume => m_Volume ? m_Volume : (m_Volume = GetComponent<Volume>());

        protected override void Reset()
        {
            base.Reset();
            seconds = 2f;
            var v = GetComponent<Volume>();
            // Global, weil die Zone selbst bestimmt, wann sie wirkt (nicht der Abstand der Kamera); Gewicht 0 bis man drin ist.
            v.isGlobal = true; v.weight = 0f; v.priority = 10f;
        }

        void Awake() { if (Volume) Volume.weight = 0f; }

        protected override void Entered()
        {
            PostFx.EnsureOnMainCamera();
            Volume.priority = ++s_Order;
            Go(strength);
        }

        protected override void Left() => Go(0f);

        void Go(float to)
        {
            var v = Volume;
            if (!v) return;
            float from = v.weight;
            m_Ticket = Blend.Claim(v, this);
            BlendRunner.Run(Blend.Run(seconds, ease, true, k => v.weight = Mathf.Clamp01(Mathf.LerpUnclamped(from, to, k)), v, m_Ticket));
        }
    }

    public static class PostFx
    {
        /// <summary>
        /// A camera only shows post-processing with "Post Processing" ticked (URP turns it off on new cameras).
        /// The kit ticks it for the main camera, so a Bildstimmung never silently does nothing.
        /// </summary>
        public static bool EnsureOnMainCamera()
        {
            var cam = Camera.main;
            if (!cam) return false;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data && !data.renderPostProcessing) data.renderPostProcessing = true;
            return data;
        }
    }
}
