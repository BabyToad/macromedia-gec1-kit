using System;
using System.Collections.Generic;

namespace Kit
{
    // Zeit anhalten: das Spiel steht still (Time.timeScale = 0). Alles, was mit Time.deltaTime rechnet, bleibt
    // stehen: Bewegung, Physik, Warten. UI-Knöpfe und Blenden laufen weiter (sie rechnen in echter Zeit).
    // „Zeit fortsetzen“ (auch aus einer anderen Logik, z.B. am Knopf Weiter) hebt jedes Anhalten wieder auf.
    // Wird die Szene verlassen, läuft die Zeit von selbst weiter.
    [Serializable]
    [NodeInfo("Zeit anhalten", KitGroups.Szene, "Spielzeit anhalten, bis „Zeit fortsetzen“")]
    [Hint("Hält die Spielzeit an (Time.timeScale = 0), bis „Zeit fortsetzen“ oder „Umschalten“ sie wieder laufen lässt.")]
    public class TimeStop : KitNode
    {
        [Output("Angehalten")]
        [Hint("Feuert, wenn die Zeit steht.")]
        [NonSerialized] public Output stopped;
        [Output("Läuft wieder")]
        [Hint("Feuert, wenn „Umschalten“ die Zeit wieder laufen lässt.")]
        [NonSerialized] public Output resumed;

        [DataOut("Angehalten?")]
        [Hint("Ja, solange irgendein „Zeit anhalten“ die Zeit anhält.")]
        public bool IsStopped => Open.Count > 0;

        internal static readonly List<TimeStop> Open = new List<TimeStop>();   // offene Halte (Knoten-Kopien)
        public static int OpenCount => Open.Count;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void NewSession() => Open.Clear();

        [Input("Anhalten")]
        [Hint("Hält die Zeit an. Bewegung, Physik und Warten stehen still; UI-Knöpfe und Blenden laufen weiter (echte Zeit).")]
        public void Stop(Signal s)
        {
            KitTime.Hold(this, 0f);
            if (!Open.Contains(this)) Open.Add(this);
            Done("✓ Zeit steht");
            stopped.Fire(s);
        }

        [Input("Umschalten")]
        [Hint("Läuft die Zeit, hält sie an; steht sie, läuft sie wieder. Gut für eine Pause-Taste.")]
        public void Toggle(Signal s)
        {
            if (Open.Count == 0) { Stop(s); return; }
            ReleaseAll();
            Done(AfterRelease());
            resumed.Fire(s);
        }

        internal void ReleaseOwn()
        {
            KitTime.Release(this);
            Open.Remove(this);
        }

        internal static int ReleaseAll()
        {
            var open = Open.ToArray();
            foreach (var t in open) t.ReleaseOwn();
            return open.Length;
        }

        // What is true after letting go: something else (a Pause with "Zeit anhalten", Anhalten) may still hold.
        internal static string AfterRelease() => KitTime.HoldCount > 0 ? "✓ aufgehoben; etwas anderes hält die Zeit noch an (Pause?)" : "✓ Zeit läuft";

        public override void OnStop() => ReleaseOwn();   // Logik aus oder Szene weg: nie im Stillstand hängen bleiben

        public override string Readout() => IsStopped ? "Zeit steht" : "Zeit läuft";
    }

    [Serializable]
    [NodeInfo("Zeit fortsetzen", KitGroups.Szene, "Angehaltene Spielzeit wieder laufen lassen")]
    [Hint("Hebt jedes „Zeit anhalten“ auf, auch aus einer anderen Logik (z.B. am Knopf „Weiter“).")]
    public class TimeResume : KitNode
    {
        [Output("Läuft wieder")]
        [Hint("Feuert danach. Hält noch etwas anderes die Zeit an (eine Pause mit „Zeit anhalten“), steht sie weiter.")]
        [NonSerialized] public Output resumed;

        [Input("Fortsetzen")]
        [Hint("Lässt die Zeit wieder laufen.")]
        public void Resume(Signal s)
        {
            int n = TimeStop.ReleaseAll();
            Done(n > 0 ? TimeStop.AfterRelease() : "✓ war nicht angehalten");
            resumed.Fire(s);
        }
    }
}
