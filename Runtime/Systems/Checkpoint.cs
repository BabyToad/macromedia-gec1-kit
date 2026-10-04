using System;
using UnityEngine;

namespace Kit
{
    // Checkpoint: merkt sich einen Punkt als Neustart-Ort des Spielers ("Merken") und setzt den Spieler
    // dorthin zurück ("Zurücksetzen"), z.B. nach dem Tod. Arbeitet mit dem Kit-Spieler.
    [Serializable]
    [NodeInfo("Checkpoint", "Spielsysteme", "Neustart-Punkt merken und zurücksetzen")]
    public class Checkpoint : KitNode
    {
        [Ref("Spieler")] public KitPlayer player;
        [Ref("Punkt", Optional = true)] public Transform point;   // leer = wo der Spieler gerade steht

        [Output("Gemerkt")] [NonSerialized] public Output saved;
        [Output("Zurückgesetzt")] [NonSerialized] public Output respawned;

        [Input("Merken")]
        public void Save(Signal s)
        {
            var t = point != null ? point : player.transform;
            player.SetRespawnPoint(t.position, t.rotation);
            Done($"✓ gemerkt: {(point != null ? point.name : "aktuelle Stelle")}");
            saved.Fire(s);
        }

        [Input("Zurücksetzen")]
        public void Respawn(Signal s)
        {
            player.Respawn();
            Done("✓ zurückgesetzt");
            respawned.Fire(s);
        }
    }
}
