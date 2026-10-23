using System;
using UnityEngine;

namespace Kit
{
    // Nebel: blendet den Nebel der Szene (Lighting › Environment › Fog) auf eine neue Farbe und Sichtweite. Ein
    // Sumpf zieht zu, ein Morgen klart auf. „Zurück“ holt den Nebel vom Szenenstart, „Aus“ klart ganz auf.
    [Serializable]
    [NodeInfo("Nebel", KitGroups.Atmosphaere, "Nebel der Szene dichter, lichter oder farbig blenden")]
    public class FogChange : KitNode
    {
        [Setting("Farbe")]
        [Hint("Farbe des Nebels.", edges: "Nah an der Himmels- oder Hintergrundfarbe, sonst wirkt er wie eine Wand.")]
        public Color color = new Color(0.62f, 0.68f, 0.74f);

        [Setting("Sichtweite")]
        [Hint("Ab dieser Entfernung ist alles im Nebel verschwunden.", unit: "m", range: "10 bis 300", edges: "15: dichter Sumpf. 60: Dunst. 300: klare Luft mit Tiefe.")]
        public float visibility = 40f;

        [Setting("Dauer")]
        [Hint("So lange dauert das Blenden (Spielzeit).", unit: "s", range: "3 bis 8", edges: "Nebel wirkt langsam am glaubwürdigsten. 0: sofort.")]
        public float seconds = 4f;

        [Setting("Verlauf")]
        [Hint("Wie die Änderung über die Dauer läuft.", edges: "„Weich“ passt fast immer.")]
        public Ease ease = Ease.Smooth;

        [Hint("Kommt, wenn der Nebel angekommen ist.")] [Output("Fertig")] [NonSerialized] public Output done;

        [Hint("Blendet den Nebel zu Farbe und Sichtweite dieses Knotens.")] [Input("Ändern", UsesRefs = false)] public void Change(Signal s) => To(new FogLook { on = true, color = color, visibility = visibility }, "geändert", s);
        [Hint("Blendet zum Nebel vom Szenenstart zurück.")] [Input("Zurück", UsesRefs = false)] public void Restore(Signal s) => To(Blend.Original(Blend.Fog, FogLook.Current), "zurück", s);
        [Hint("Klart ganz auf: am Ende kein Nebel mehr.")] [Input("Aus", UsesRefs = false)] public void Off(Signal s) => To(new FogLook { on = false, color = RenderSettings.fogColor, visibility = FogLook.Clear }, "aus", s);

        void To(FogLook to, string what, Signal s)
        {
            Blend.Original(Blend.Fog, FogLook.Current);
            var from = FogLook.Current();
            int ticket = Blend.Claim(Blend.Fog, this);
            StartWork(Wrap(Blend.Run(seconds, ease, false, k => FogLook.Lerp(from, to, k).Apply(), Blend.Fog, ticket,
                                     t => Running($"{what}: {t * 100f:0} %", t)), ticket, what, s));
        }

        System.Collections.IEnumerator Wrap(System.Collections.IEnumerator run, int ticket, string what, Signal s)
        {
            while (run.MoveNext()) yield return run.Current;
            if (!Blend.Holds(Blend.Fog, ticket)) { Blocked("ein neueres Nebel-Blenden hat übernommen"); yield break; }
            Blend.Let(Blend.Fog, ticket);
            Done($"✓ {what}");
            done.Fire(s);
        }

        public override string Readout()
        {
            var f = FogLook.Current();
            return f.on ? $"Sichtweite {f.visibility:0} m" : "kein Nebel";
        }

        public override void Validate(System.Collections.Generic.List<string> problems)
        {
            if (visibility < 1f) problems.Add("Sichtweite muss mindestens 1 m sein");
            if (seconds < 0f) problems.Add("Dauer darf nicht negativ sein");
        }
    }
}
