using UnityEngine;

namespace Kit
{
    /// <summary>
    /// Nebel-Zone: inside, the scene's fog blends to this zone's colour and visibility; outside, back to what was
    /// there before (another zone around it, or the scene's own fog). A swamp, a cave mouth, a dusty hall.
    /// </summary>
    [AddComponentMenu(KitBrand.ComponentMenu + "Atmosphäre/Nebel-Zone")]
    public class FogZone : AtmosphereZone
    {
        [Hint("Farbe des Nebels in der Zone.", edges: "Nah an der Himmels- oder Wandfarbe wirkt er natürlich, kräftige Farben wirken magisch.", label: "Nebelfarbe")]
        public Color color = new Color(0.55f, 0.62f, 0.6f);

        [Hint("Ab dieser Entfernung ist in der Zone alles im Nebel verschwunden.", unit: "m", range: "8 bis 200", edges: "10: dichter Sumpf. 40: Dunst. 150: leichter Schleier.", label: "Sichtweite")]
        public float visibility = 18f;

        static Wishes<FogLook> s_Wishes;

        protected override Color GizmoColor => new Color(0.6f, 0.8f, 0.85f);
        protected override string GizmoLabel => $"Nebel: {visibility:0} m";

        protected override void Reset() { base.Reset(); seconds = 3f; }   // Nebel braucht mehr Zeit als ein Schnitt

        static Wishes<FogLook> Wishes => s_Wishes ??= new Wishes<FogLook>(FogLook.Current());

        protected override void Entered()
        {
            // Die erste Zone merkt sich den Nebel davor (auch einen, den ein Knoten „Nebel“ gesetzt hat).
            Wishes.PruneDead();
            // Läuft der Weg zurück zum Nebel davor noch (gerade eine Zone verlassen), bleibt dieser Nebel der Ausgangspunkt.
            if (Wishes.Count == 0 && !Returning) { Blend.Original(Blend.Fog, FogLook.Current); Wishes.Base = FogLook.Current(); }
            Wishes.Set(this, new FogLook { on = true, color = color, visibility = Mathf.Max(1f, visibility) });
            Go(seconds, ease);
        }

        protected override void Left()
        {
            if (s_Wishes != null && s_Wishes.Remove(this)) Go(seconds, ease);
        }

        static void Go(float seconds, Ease ease)
        {
            var from = FogLook.Current(); var to = Wishes.Current;
            int ticket = Blend.Claim(Blend.Fog, typeof(FogZone));
            s_Ticket = ticket;
            BlendRunner.Run(Release(Blend.Run(seconds, ease, true, k => FogLook.Lerp(from, to, k).Apply(), Blend.Fog, ticket), ticket));
        }

        static System.Collections.IEnumerator Release(System.Collections.IEnumerator run, int ticket)
        {
            while (run.MoveNext()) yield return run.Current;
            Blend.Let(Blend.Fog, ticket);                               // angekommen: der Nebel gehört wieder niemandem
        }

        static int s_Ticket;
        // A zone blend still owns the fog (no node took it over in between): the base it heads to is still the truth.
        static bool Returning => Blend.Holds(Blend.Fog, s_Ticket);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void NewSession()
        {
            s_Wishes = null;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= Loaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += Loaded;
        }

        static void Loaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
        {
            if (m == UnityEngine.SceneManagement.LoadSceneMode.Single) s_Wishes = null;   // neue Szene, eigener Nebel
        }

#if UNITY_EDITOR
        protected override void OnValidate() { base.OnValidate(); visibility = Mathf.Max(1f, visibility); }
#endif
    }
}
