using System.Collections.Generic;
using UnityEngine;

namespace Kit
{
    /// <summary>
    /// Patrouillieren: walks a route of waypoints. „Ziel erreicht“ fires at every waypoint, with its number.
    /// Switched on again (after a chase), the figure resumes at the nearest waypoint instead of starting over.
    /// Waypoints deleted during Play are skipped.
    /// </summary>
    [AddComponentMenu(KitBrand.ComponentMenu + "Verhalten/Patrouillieren")]
    public class Patrol : NpcBehaviour
    {
        public enum RouteMode { Rundgang, HinUndZurück, Einmal, Zufällig }

        [Hint("Die Wegpunkte der Route, in dieser Reihenfolge: leere Objekte in der Szene. Mit dem Knopf „Wegpunkt hinzufügen“ anlegen und im Scene View verschieben.", edges: "Keine Wegpunkte: die Figur bleibt stehen.", label: "Wegpunkte")]
        public List<Transform> waypoints = new List<Transform>();

        [Hint("In welcher Reihenfolge die Wegpunkte drankommen. Rundgang: 1, 2, 3, 1 … · Hin und zurück: 1, 2, 3, 2, 1 … · Einmal: 1, 2, 3, dann stehen · Zufällig: jedes Mal ein anderer.", label: "Reihenfolge")]
        public RouteMode route = RouteMode.Rundgang;

        [Hint("Wie lange die Figur an jedem Wegpunkt stehen bleibt.", unit: "s", range: "0 bis 5", edges: "0: geht ohne Halt weiter.", label: "Warten")]
        [Min(0f)] public float waitTime = 1f;

        [Hint("An jedem Wegpunkt in dessen Blickrichtung schauen (blauer Pfeil des Wegpunkts): eine Wache blickt aus dem Fenster, ein Besucher betrachtet ein Bild.", label: "In Wegpunkt-Richtung schauen")]
        public bool faceWaypoint = false;

        public Patrol() { speed = 2f; }

        public override BehaviourKind Kind => BehaviourKind.Patrouillieren;

        int m_Index, m_Dir = 1;
        float m_WaitUntil;
        bool m_Waiting, m_Finished, m_Walking;

        /// <summary>Index (0-based) of the waypoint the figure walks to or waits at.</summary>
        public int CurrentIndex => m_Index;

        bool Valid(int i) => i >= 0 && i < waypoints.Count && waypoints[i] != null;

        int Count
        {
            get { int n = 0; for (int i = 0; i < waypoints.Count; i++) if (waypoints[i] != null) n++; return n; }
        }

        protected override void Begin()
        {
            m_Waiting = false; m_Finished = false; m_Walking = false;
            if (Count == 0) { Say("keine Wegpunkte: steht"); Halt(); return; }
            m_Index = WasActiveBefore ? Nearest() : Following(-1, 1);
            Walk();
        }

        protected override void Tick()
        {
            if (Count == 0)
            {
                if (m_Walking || m_Waiting) { m_Walking = m_Waiting = false; Halt(); ReleaseRotation(); }   // the last one was deleted
                Say("keine Wegpunkte: steht");
                return;
            }
            if (m_Finished) { Say("Route zu Ende"); return; }
            if (!Valid(m_Index))   // deleted during Play: no waiting there any more, on to the next one
            {
                m_Waiting = false;
                Advance();
                if (m_Finished) Halt(); else Walk();
                return;
            }
            var wp = waypoints[m_Index];

            if (m_Waiting)
            {
                if (faceWaypoint) TurnTo(wp.forward);
                float left = m_WaitUntil - Time.time;
                Say("wartet an Wegpunkt {0:0} ({1:0.0} s)", m_Index + 1, Mathf.Max(0f, left));
                if (left > 0f) return;
                m_Waiting = false;
                Advance();
                if (m_Finished) { Say("Route zu Ende"); return; }
                Walk();
                return;
            }

            if (m_Walking && AtDestination)
            {
                m_Walking = false;
                if (!ReachedGoal(m_Index + 1)) return;   // the graph switched to something else
                if (route == RouteMode.Einmal && NextIndex(out _) < 0) { m_Finished = true; Say("Route zu Ende"); return; }
                m_Waiting = true;
                m_WaitUntil = Time.time + waitTime;
                return;
            }
            if (!m_Walking) { Walk(); return; }   // was unreachable: try again (a door may have opened)
            if (PathBlocked) Say("Weg zu Wegpunkt {0:0} versperrt: wartet", m_Index + 1);
            else Say("geht zu Wegpunkt {0:0} ({1:0.0} m)", m_Index + 1, Remaining);
        }

        void Walk()
        {
            m_Walking = Valid(m_Index) && GoTo(waypoints[m_Index].position);
            if (!m_Walking) Say("Wegpunkt {0:0} ist nicht erreichbar", m_Index + 1);
        }

        void Advance()
        {
            int next = NextIndex(out int dir);
            if (next < 0) { m_Finished = true; return; }
            m_Index = next; m_Dir = dir;
        }

        /// <summary>The waypoint after the current one, by route mode; -1 = route over (Einmal) or none left.</summary>
        int NextIndex(out int dir)
        {
            dir = m_Dir;
            if (Count == 0) return -1;
            switch (route)
            {
                case RouteMode.Rundgang: return Following(m_Index, 1, wrap: true);
                case RouteMode.Einmal: return Following(m_Index, 1);
                case RouteMode.HinUndZurück:
                {
                    int i = Following(m_Index, m_Dir);
                    if (i < 0) { dir = -m_Dir; i = Following(m_Index, dir); }
                    return i >= 0 ? i : Following(-1, 1);   // only the current one is left (or it was deleted)
                }
                default:
                {
                    if (Count == 1) return Following(-1, 1);
                    int pick;
                    do pick = Random.Range(0, waypoints.Count); while (pick == m_Index || !Valid(pick));
                    return pick;
                }
            }
        }

        /// <summary>The next valid waypoint after <paramref name="from"/> in direction <paramref name="dir"/>; -1 if none.</summary>
        int Following(int from, int dir, bool wrap = false)
        {
            int n = waypoints.Count;
            for (int k = 1; k <= n; k++)
            {
                int i = from + dir * k;
                if (wrap) i = ((i % n) + n) % n;
                else if (i < 0 || i >= n) return -1;
                if (Valid(i)) return i;
            }
            return -1;
        }

        int Nearest()
        {
            int best = 0; float bd = float.PositiveInfinity;
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (!Valid(i)) continue;
                float d = (waypoints[i].position - transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }
    }
}
