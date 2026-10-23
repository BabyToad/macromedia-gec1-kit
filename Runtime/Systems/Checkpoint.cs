using System;
using UnityEngine;

namespace Kit
{
    // Checkpoint: merkt sich einen Punkt als Neustart-Ort des Spielers ("Merken") und setzt den Spieler
    // dorthin zurück ("Zurücksetzen"), z.B. nach dem Tod. Arbeitet mit dem Kit-Spieler.
    [Serializable]
    [NodeInfo("Checkpoint", KitGroups.Spielsysteme, "Neustart-Punkt merken und zurücksetzen")]
    public class Checkpoint : KitNode
    {
        [Hint("Der Spieler (Komponente „Spieler“), der zurückgesetzt wird.")]
        [Ref("Spieler")] public KitPlayer player;
        [Hint("Wohin der Spieler zurückkommt, samt Drehung. Gilt die Lage im Moment des Merkens.", edges: "Leer: die Stelle, an der der Spieler gerade steht.")]
        [Ref("Punkt", Optional = true)] public Transform point;   // leer = wo der Spieler gerade steht

        [Hint("Feuert, sobald der Punkt gemerkt ist.")]
        [Output("Gemerkt")] [NonSerialized] public Output saved;
        [Hint("Feuert direkt nach dem Zurücksetzen.")]
        [Output("Zurückgesetzt")] [NonSerialized] public Output respawned;

        [Hint("Merkt den Punkt als neuen Neustart-Ort. Der vorige ist damit vergessen; auch Herunterfallen führt jetzt hierher.")]
        [Input("Merken")]
        public void Save(Signal s)
        {
            var t = point != null ? point : player.transform;
            player.SetRespawnPoint(t.position, t.rotation);
            Done($"✓ gemerkt: {(point != null ? point.name : "aktuelle Stelle")}");
            saved.Fire(s);
        }

        [Hint("Setzt den Spieler sofort auf den gemerkten Punkt, sein Tempo auf null. Ohne Merken: zurück zum Ort beim Start.")]
        [Input("Zurücksetzen")]
        public void Respawn(Signal s)
        {
            player.Respawn();
            Done("✓ zurückgesetzt");
            respawned.Fire(s);
        }
    }
}
