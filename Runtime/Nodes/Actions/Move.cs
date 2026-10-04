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
    [NodeInfo("Bewegen", "Aktion", "Verschieben, drehen oder skalieren über Zeit")]
    public class Move : KitNode
    {
        public enum What { Position, Drehung, Größe }

        [Ref("Objekt")] public Transform target;
        [Setting("Was")] public What what = What.Position;
        [Setting("Weg")] public Vector3 offset = new Vector3(0, 3, 0);   // relativ zum Start (lokal)
        [Setting("Dauer (s)")] public float seconds = 1f;
        [Setting("Weich")] public bool smooth = true;

        [Output("Angekommen")] [NonSerialized] public Output arrived;
        [Output("Unterbrochen")] [NonSerialized] public Output interrupted;
        [DataOut("Fortschritt")] [NonSerialized] public float progress;

        // Laufende Fahrt: welches Objekt, mit welchem Ereignis.
        Coroutine work; Transform moving; Signal movingSignal;
        // Startlage pro Objekt (ein Knoten kann nacheinander verschiedene Objekte bewegen).
        // Wird in OnStart neu angelegt: jede Interaktion in der Szene hat ihre eigene Liste.
        Dictionary<Transform, Vector3> origins;

        public override void OnStart()
        {
            origins = new Dictionary<Transform, Vector3>();
            if (target != null) origins[target] = Read(target);   // fest gebunden: Startlage bei Play
        }

        [Input("Hin")] public void Forward(Signal s) => Go(target, true, s);
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

        Vector3 Read(Transform t) => what == What.Position ? t.localPosition : what == What.Drehung ? t.localEulerAngles : t.localScale;

        void Write(Transform t, Vector3 v)
        {
            if (what == What.Position) t.localPosition = v;
            else if (what == What.Drehung) t.localRotation = Quaternion.Euler(v);
            else t.localScale = v;
        }

        public override void OnStop() { work = null; moving = null; }
    }
}
