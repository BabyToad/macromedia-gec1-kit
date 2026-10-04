using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Kit
{
    // Gehen zu: schickt eine Figur mit NavMeshAgent zu einem Ziel. Der Weg wird um Hindernisse herum
    // berechnet (dafür muss die Navigation gebacken sein). "Kein Weg", wenn das Ziel nicht erreichbar ist.
    // Ein neues Ziel während des Gehens: "Unterbrochen" feuert mit dem alten Ereignis.
    [Serializable]
    [NodeInfo("Gehen zu", "Spielsysteme", "Figur per NavMesh zu einem Ziel schicken")]
    public class WalkTo : KitNode
    {
        [Ref("Läufer")] public NavMeshAgent agent;
        [Ref("Ziel")] public Transform goal;
        [Setting("Halteabstand (m)")] public float stopDistance = 0.5f;

        [Output("Angekommen")] [NonSerialized] public Output arrived;
        [Output("Kein Weg")] [NonSerialized] public Output noPath;
        [Output("Unterbrochen")] [NonSerialized] public Output interrupted;
        [DataOut("Restweg (m)")] public float Remaining => agent != null && agent.isOnNavMesh && agent.hasPath ? agent.remainingDistance : 0f;

        // Der laufende Gang: welcher Läufer, mit welchem Ereignis. Abbrechen räumt genau diesen Läufer auf.
        Coroutine work; NavMeshAgent walker; Signal walking;

        [Input("Los")]
        public void Go(Signal s)
        {
            // Erst den alten Gang beenden – auch wenn der neue gleich scheitert, sonst läuft der alte Läufer weiter.
            if (work != null) { var old = walking; Cancel(); interrupted.Fire(old); }
            if (!agent.isOnNavMesh) { Fail($"„{agent.name}“ steht auf keinem NavMesh (Navigation backen)"); return; }
            agent.stoppingDistance = stopDistance;
            var path = new NavMeshPath();
            if (!agent.CalculatePath(goal.position, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                Done("kein Weg zum Ziel");
                noPath.Fire(s);
                return;
            }
            walker = agent; walking = s;
            agent.SetPath(path);
            work = StartWork(Walk(agent, s));
        }

        [Input("Stopp")]
        public void Stop(Signal s)
        {
            if (work == null) { Done("ging nicht"); return; }
            var old = walking;
            Cancel();
            Done("gestoppt");
            interrupted.Fire(old);
        }

        /// <summary>Beendet den laufenden Gang: Coroutine aus, Weg des eigenen Läufers löschen.</summary>
        void Cancel()
        {
            if (work != null) StopWork(work);
            work = null;
            if (walker != null && walker.isOnNavMesh) walker.ResetPath();
            walker = null; walking = null;
        }

        IEnumerator Walk(NavMeshAgent a, Signal s)
        {
            yield return null;                                  // der Agent rechnet den Weg im nächsten Frame
            while (a != null && (a.pathPending || a.remainingDistance > stopDistance))
            {
                Running($"geht: noch {a.remainingDistance:0.0} m");
                yield return null;
            }
            work = null; walker = null; walking = null;
            if (a == null) { Fail("◆ Läufer wurde gelöscht"); yield break; }
            Done("✓ angekommen");
            arrived.Fire(s);
        }

        public override void OnStop()
        {
            // Die Coroutinen stoppt das Interaction-Objekt selbst; der Weg gehört aber dem Läufer und bliebe sonst stehen.
            work = null;
            if (walker != null && walker.isOnNavMesh) walker.ResetPath();
            walker = null; walking = null;
        }
    }
}
