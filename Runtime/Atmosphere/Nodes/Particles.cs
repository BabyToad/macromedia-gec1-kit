using System;
using System.Collections;
using UnityEngine;

namespace Kit
{
    // Partikel: steuert ein Partikelsystem. Abspielen und Stoppen für Dauerndes (Rauch, Regen, Funkenflug),
    // Stoß für einen einzelnen Ausbruch (Staubwolke beim Landen, Funken an einer Stelle, Konfetti). Aussehen,
    // Farbe und Menge stellt das Particle System selbst ein; der Knoten sagt nur, wann.
    [Serializable]
    [NodeInfo("Partikel", KitGroups.Atmosphaere, "Partikelsystem abspielen, stoppen oder einen Stoß auslösen")]
    public class Particles : KitNode
    {
        [Ref("Partikel")]
        [Hint("Das Partikelsystem (Particle System). Fertige Vorlagen: GameObject › " + KitBrand.Name + " › Atmosphäre › Partikel.")]
        public ParticleSystem system;

        [Ref("Ort", Optional = true)]
        [Hint("Optional: hier entsteht der Stoß, z. B. ◆ Wer aus einer Zone.", edges: "Leer: am Partikelsystem selbst.")]
        public Transform at;

        [Setting("Teilchen pro Stoß")]
        [Hint("So viele Partikel erscheinen bei „Stoß“ auf einmal.", unit: "Stück", range: "10 bis 300", edges: "10: eine Prise. 50: eine Wolke. 300: eine Explosion von Konfetti.")]
        public int burst = 40;

        [Setting("Stoppen: sofort weg")]
        [Hint("An: „Stoppen“ löscht alle Partikel sofort. Aus: keine neuen mehr, die vorhandenen leben zu Ende (wirkt natürlicher).")]
        public bool clearOnStop = false;

        [Hint("Kommt, wenn kein Partikel mehr lebt. Bei einem laufenden Dauer-System erst nach „Stoppen“.")] [Output("Verklungen")] [NonSerialized] public Output finished;

        Coroutine m_Watch;

        [Hint("Startet das Partikelsystem (Dauerndes wie Rauch, Regen, Funkenflug).")] [Input("Abspielen")]
        public void Play(Signal s)
        {
            system.Play(true);
            Watch(s, "spielt");
        }

        [Hint("Keine neuen Partikel mehr; vorhandene leben zu Ende oder verschwinden sofort (Einstellung).")] [Input("Stoppen")]
        public void Stop(Signal s)
        {
            system.Stop(true, clearOnStop ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
            Watch(s, "klingt aus");
        }

        [Hint("Lässt auf einmal so viele Partikel entstehen, wie eingestellt: am ◆ Ort oder am System selbst.")] [Input("Stoß")]
        public void Burst(Signal s)
        {
            if (burst <= 0) { Fail("Teilchen pro Stoß muss größer als 0 sein"); return; }
            var p = new ParticleSystem.EmitParams();
            if (at)
            {
                var main = system.main;
                bool world = main.simulationSpace == ParticleSystemSimulationSpace.World;
                p.position = world ? at.position : system.transform.InverseTransformPoint(at.position);
                p.applyShapeToPosition = true;                       // die Form (Kugel, Kegel …) gilt um den Ort herum
            }
            system.Emit(p, burst);                                   // auch an einem gestoppten System: es startet dabei nicht
            Watch(s, $"Stoß: {burst} Teilchen", untilEmpty: true);
        }

        void Watch(Signal s, string what, bool untilEmpty = false)
        {
            if (m_Watch != null) StopWork(m_Watch);
            m_Watch = StartWork(UntilQuiet(s, what, untilEmpty));
        }

        // Nach einem Stoß zählt nur, ob seine Teilchen verflogen sind (auch die der Kind-Systeme, etwa Funken aus
        // Sub-Emittern): ein laufendes Dauer-System ist nie „still“.
        IEnumerator UntilQuiet(Signal s, string what, bool untilEmpty)
        {
            var all = system.GetComponentsInChildren<ParticleSystem>(true);
            yield return null;
            while (system && (untilEmpty ? Count(all) > 0 : system.IsAlive(true)))
            {
                bool emitting = system.isEmitting;
                Running($"{what} · {Count(all)} Teilchen" + (emitting ? "" : ", verklingt"));
                // Ein Dauer-System (Loop) wird nie still, solange es spielt: „Verklungen“ kommt erst nach „Stoppen“.
                yield return null;
            }
            m_Watch = null;
            if (!system) { Fail("◆ Partikel wurde gelöscht"); yield break; }
            Done("✓ verklungen");
            finished.Fire(s);
        }

        static int Count(ParticleSystem[] all)
        {
            int n = 0;
            foreach (var p in all) if (p) n += p.particleCount;
            return n;
        }

        public override void OnStop() { m_Watch = null; }

        public override string Readout() => system ? $"{system.particleCount} Teilchen" + (system.isPlaying ? ", spielt" : "") : null;

        public override void Validate(System.Collections.Generic.List<string> problems)
        {
            if (burst <= 0) problems.Add("Teilchen pro Stoß muss größer als 0 sein");
        }
    }
}
