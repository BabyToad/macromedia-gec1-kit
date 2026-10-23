using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Kit
{
    // Gehen zu: schickt eine Figur mit NavMeshAgent zu einem Ziel. Der Weg wird um Hindernisse herum
    // berechnet; das NavMesh backt das Kit selbst (KitNavigation). "Kein Weg", wenn das Ziel nicht erreichbar ist.
    // Ein neues Ziel während des Gehens: "Unterbrochen" feuert mit dem alten Ereignis.
    [Serializable]
    [NodeInfo("Gehen zu", KitGroups.Spielsysteme, "Figur per NavMesh zu einem Ziel schicken")]
    public class WalkTo : KitNode
    {
        [Hint("Die Figur, die geht. Braucht einen NavMeshAgent (dort stehen Tempo und Wendigkeit) und muss auf gebackener Navigation stehen.")]
        [Ref("Läufer")] public NavMeshAgent agent;
        [Hint("Wohin die Figur geht. Gelesen beim Losgehen: bewegt sich das Ziel danach, geht die Figur trotzdem zum alten Punkt.")]
        [Ref("Ziel")] public Transform goal;
        [Hint("Wie nah die Figur ans Ziel herangeht, bevor sie als angekommen gilt. Ersetzt den Wert am NavMeshAgent.", unit: "m", range: "0,2 bis 2", edges: "0: bis genau aufs Ziel.")]
        [Setting("Halteabstand")] public float stopDistance = 0.5f;

        [Hint("Feuert, wenn die Figur am Ziel ist, mit dem Ereignis, das sie losgeschickt hat.")]
        [Output("Angekommen")] [NonSerialized] public Output arrived;
        [Hint("Feuert sofort, wenn das Ziel über die Navigation nicht erreichbar ist. Die Figur bleibt stehen.")]
        [Output("Kein Weg")] [NonSerialized] public Output noPath;
        [Hint("Feuert, wenn ein neues „Los“ oder „Stopp“ den laufenden Gang abbricht, mit dem alten Ereignis.")]
        [Output("Unterbrochen")] [NonSerialized] public Output interrupted;
        [Hint("Wie weit die Figur noch gehen muss, entlang des Wegs gemessen.", unit: "m", edges: "0: geht gerade nicht.")]
        [DataOut("Restweg")] public float Remaining => agent != null && agent.isOnNavMesh && agent.hasPath ? agent.remainingDistance : 0f;

        // Der laufende Gang: welcher Läufer, mit welchem Ereignis. Abbrechen räumt genau diesen Läufer auf.
        Coroutine work; NavMeshAgent walker; Signal walking; Vector3 target;

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>Close to the goal, also in height (a goal on the floor above is not reached from below).</summary>
        bool Near(NavMeshAgent a, Vector3 goalPoint)
        {
            var feet = a.transform.position - Vector3.up * a.baseOffset;
            return Flat(feet - goalPoint).magnitude <= stopDistance + 0.3f && Mathf.Abs(feet.y - goalPoint.y) <= 1.25f;
        }

        void Lost(NavMeshAgent a, Signal s)
        {
            work = null; walker = null; walking = null;
            if (a.isOnNavMesh) a.ResetPath();
            Done("kein Weg zum Ziel");
            noPath.Fire(s);
        }

        [Hint("Schickt die Figur zum Ziel. Geht sie schon, wird der alte Gang abgebrochen.")]
        [Input("Los")]
        public void Go(Signal s)
        {
            // Erst den alten Gang beenden – auch wenn der neue gleich scheitert, sonst läuft der alte Läufer weiter.
            if (work != null) { var old = walking; Cancel(); interrupted.Fire(old); }
            KitNavigation.Ensure();   // das Kit backt das NavMesh selbst
            if (!agent.isOnNavMesh && NavMesh.SamplePosition(agent.transform.position, out var near, 3f, NavMesh.AllAreas)) agent.Warp(near.position);
            if (!agent.isOnNavMesh) { Fail($"„{agent.name}“ steht nicht auf begehbarem Boden (kein Collider unter den Füßen?)"); return; }
            agent.stoppingDistance = stopDistance;
            var path = new NavMeshPath();
            if (!agent.CalculatePath(goal.position, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                Done("kein Weg zum Ziel");
                noPath.Fire(s);
                return;
            }
            walker = agent; walking = s; target = path.corners[path.corners.Length - 1];
            agent.SetPath(path);
            work = StartWork(Walk(agent, s));
        }

        [Hint("Hält die Figur an. Darf auch von einem ganz anderen Ereignis kommen.")]
        [Input("Stopp", UsesRefs = false)]   // stoppt den eigenen Läufer: braucht keinen ◆ aus dem Ereignis
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
            bool asked = false;   // a new path was requested after the old one was lost; its answer decides
            while (a != null)
            {
                // The NavMesh under the walker was replaced (another scene brought its own): back on, same goal.
                if (!a.isOnNavMesh)
                {
                    if (NavMesh.SamplePosition(a.transform.position, out var near, 3f, NavMesh.AllAreas)) a.Warp(near.position);
                    if (a.isOnNavMesh) { if (!a.SetDestination(target)) { Lost(a, s); yield break; } asked = true; }
                    Running("sucht den Boden wieder");
                    yield return null;
                    continue;
                }
                if (!a.pathPending)
                {
                    // Recovered onto a mesh where the goal is out of reach: say so, do not pretend to arrive.
                    if (a.hasPath && a.pathStatus != NavMeshPathStatus.PathComplete) { Lost(a, s); yield break; }
                    if (a.hasPath ? a.remainingDistance <= stopDistance : Near(a, target)) break;
                    if (a.hasPath) asked = false;
                    else if (asked) { Lost(a, s); yield break; }   // the new request found no path either
                    else { if (!a.SetDestination(target)) { Lost(a, s); yield break; } asked = true; }   // path lost on the way
                }
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
            // Die Coroutinen stoppt das Logic-Objekt selbst; der Weg gehört aber dem Läufer und bliebe sonst stehen.
            work = null;
            if (walker != null && walker.isOnNavMesh) walker.ResetPath();
            walker = null; walking = null;
        }
    }
}
