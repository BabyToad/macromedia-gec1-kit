using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kit
{
    // Bewegen: verschiebt, dreht oder skaliert ein Objekt in einer festen Zeit.
    // "Hin" fährt zum Start + Weg, "Zurück" zum Start. "Start" ist die Lage, in der das Objekt zum
    // ersten Mal gesehen wurde (bei Play, oder beim ersten Ereignis, wenn das Objekt mit dem Ereignis
    // kommt, z.B. über "Wer"). Angekommen feuert erst am Ende.
    // Ein neuer Auftrag während der Fahrt: "Unterbrochen" feuert (mit dem alten Ereignis),
    // die neue Fahrt beginnt von der aktuellen Lage.
    [Serializable]
    [Hint("„Start“ ist die Lage beim ersten Mal: bei Play, oder beim ersten Ereignis, wenn das Objekt mit dem Ereignis kommt. Ein neuer Auftrag während der Fahrt unterbricht sie und fährt von der aktuellen Lage aus weiter.")]
    [NodeInfo("Bewegen", KitGroups.Aktion, "Verschieben, drehen oder skalieren über Zeit")]
    public class Move : KitNode
    {
        public enum What { Position, Drehung, Größe }

        [Hint("Was bewegt wird. Fest zuweisen oder mit dem Ereignis mitgeben (z.B. ◆ Wer). Ein Rigidbody daran sollte „Is Kinematic“ haben.")]
        [Ref("Objekt")] public Transform target;
        [Hint("Was sich ändert. Position: Weg in Metern. Drehung: Weg in Grad. Größe: der Weg wird zur Skalierung addiert.")]
        [Setting("Was")] public What what = What.Position;
        [Hint("Wohin „Hin“ vom Start aus führt, je Achse X, Y, Z, in den Achsen des Elternobjekts (ohne Elternobjekt: Welt).", edges: "0, 0, 0: bleibt am Start. Einheit je nach „Was“: m, ° oder Skalierung.")]
        [Setting("Weg")] public Vector3 offset = new Vector3(0, 3, 0);   // relativ zum Start (lokal)
        [Hint("Wie lange eine Fahrt dauert. Steht die Spielzeit still, steht auch die Fahrt.", unit: "s", range: "0,2 bis 3", edges: "0 oder negativ: springt sofort ans Ziel.")]
        [Setting("Dauer")] public float seconds = 1f;
        [Hint("An: fährt sanft an und bremst sanft ab. Aus: gleiches Tempo von Anfang bis Ende.")]
        [Setting("Weich")] public bool smooth = true;

        [Hint("Feuert, wenn die Fahrt am Ziel ist, mit dem Ereignis, das sie gestartet hat.")]
        [Output("Angekommen")] [NonSerialized] public Output arrived;
        [Hint("Feuert, wenn ein neuer Auftrag die laufende Fahrt abbricht, mit dem alten Ereignis.")]
        [Output("Unterbrochen")] [NonSerialized] public Output interrupted;
        [Hint("Wie weit die laufende Fahrt ist.", range: "0 bis 1", edges: "0: gerade gestartet. 1: angekommen.")]
        [DataOut("Fortschritt")] [NonSerialized] public float progress;

        // Laufende Fahrt: welches Objekt, mit welchem Ereignis.
        Coroutine work; Transform moving; Signal movingSignal;
        // Startlage pro Objekt (ein Knoten kann nacheinander verschiedene Objekte bewegen).
        // Wird in OnStart neu angelegt: jede Logik in der Szene hat ihre eigene Liste.
        Dictionary<Transform, Vector3> origins;
        // Drehung: Unity gibt Winkel immer als 0–360 zurück (−90 kommt als 270 zurück). Damit "Zurück" den kurzen
        // Weg dreht und eine Drehung um 720° zwei Runden bleibt, merkt sich der Knoten die Winkel, die er selbst schrieb.
        Dictionary<Transform, Vector3> written;

        public override void OnStart()
        {
            origins = new Dictionary<Transform, Vector3>();
            written = new Dictionary<Transform, Vector3>();
            if (target != null) origins[target] = Read(target);   // fest gebunden: Startlage bei Play
        }

        [Hint("Fährt von der aktuellen Lage zu Start + Weg.")]
        [Input("Hin")] public void Forward(Signal s) => Go(target, true, s);
        [Hint("Fährt von der aktuellen Lage zurück zum Start.")]
        [Input("Zurück")] public void Back(Signal s) => Go(target, false, s);

        void Go(Transform t, bool forward, Signal s)
        {
            if (!origins.TryGetValue(t, out var origin)) origins[t] = origin = Read(t);   // zum ersten Mal gesehen
            if (work != null)
            {
                StopWork(work);
                interrupted.Fire(movingSignal);       // die alte Fahrt endet ohne "Angekommen"
            }
            moving = t; movingSignal = s;
            work = StartWork(Run(t, Read(t), forward ? origin + offset : origin, s));
        }

        IEnumerator Run(Transform t, Vector3 from, Vector3 to, Signal s)
        {
            for (float time = 0; time < seconds; time += Time.deltaTime)   // deltaTime: Zeit seit dem letzten Frame
            {
                if (t == null) { Lost(); yield break; }
                progress = time / seconds;
                Running($"läuft: {time:0.0} / {seconds:0.0} s", progress);
                float k = smooth ? Mathf.SmoothStep(0, 1, progress) : progress;
                Write(t, Vector3.LerpUnclamped(from, to, k));
                yield return null;                                          // weiter im nächsten Frame
            }
            if (t == null) { Lost(); yield break; }
            Write(t, to); progress = 1; work = null; moving = null;
            Done("✓ Angekommen");
            arrived.Fire(s);                                                // erst jetzt, nicht beim Start
        }

        void Lost() { work = null; moving = null; Fail("◆ Objekt wurde während der Bewegung gelöscht"); }

        Vector3 Read(Transform t)
        {
            if (what == What.Position) return t.localPosition;
            if (what != What.Drehung) return t.localScale;
            var actual = t.localEulerAngles;                 // Unity: immer 0–360
            if (!written.TryGetValue(t, out var e)) return actual;
            if (Quaternion.Angle(Quaternion.Euler(e), t.localRotation) < 0.01f) return e;   // unverändert: eigene Winkel (auch 720°)
            // Hat etwas anderes gedreht: dieselbe Drehung, aber so nah wie möglich an den gemerkten Winkeln (−89 statt 271).
            return new Vector3(e.x + Mathf.DeltaAngle(e.x, actual.x), e.y + Mathf.DeltaAngle(e.y, actual.y), e.z + Mathf.DeltaAngle(e.z, actual.z));
        }

        void Write(Transform t, Vector3 v)
        {
            if (what == What.Position) t.localPosition = v;
            else if (what == What.Drehung) { t.localRotation = Quaternion.Euler(v); written[t] = v; }
            else t.localScale = v;
        }

        public override void OnStop() { work = null; moving = null; }
    }
}
