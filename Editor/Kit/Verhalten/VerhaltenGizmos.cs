using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>
    /// Makes figures readable in the Scene view: the sight cone and hearing ring (coloured by what the figure
    /// knows), the route, wander circle, post, home and safe distance of each behaviour, and in Play the path
    /// it walks, the last clue and a label with behaviour and state above its head.
    /// Selected figures draw everything; the others draw a faint outline, so a busy level stays legible.
    /// </summary>
    static class VerhaltenGizmos
    {
        static readonly List<Vector3> s_Path = new List<Vector3>();
        static GUIStyle s_Label;

        static GUIStyle Label
        {
            get
            {
                if (s_Label != null) return s_Label;
                s_Label = new GUIStyle(EditorStyles.whiteMiniLabel) { alignment = TextAnchor.LowerCenter, richText = true };
                s_Label.normal.background = Texture2D.grayTexture;
                s_Label.padding = new RectOffset(4, 4, 1, 2);
                return s_Label;
            }
        }

        static float Ground(Transform t) => Senses.Feet(t) + 0.03f;

        // ---------------------------------------------------------------------------------- senses
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        static void DrawSenses(Senses s, GizmoType type)
        {
            if (!s.enabled) return;
            bool sel = (type & GizmoType.Selected) != 0 || (type & GizmoType.InSelectionHierarchy) != 0;
            bool play = Application.isPlaying;
            var state = play ? s.State : Senses.Awareness.Ruhig;
            var c = VerhaltenStyle.Of(state);
            float fill = sel ? 0.12f : play ? 0.07f : 0f;
            float line = sel ? 0.9f : play ? 0.5f : 0.25f;

            var eye = s.Eye;
            var fwd = s.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();

            if (s.sight && s.sightRange > 0f)
            {
                var ground = new Vector3(eye.x, Ground(s.transform), eye.z);
                float half = s.sightAngle * 0.5f;
                var from = Quaternion.Euler(0f, -half, 0f) * fwd;
                if (fill > 0f)
                {
                    Handles.color = VerhaltenStyle.Alpha(c, fill);
                    Handles.DrawSolidArc(ground, Vector3.up, from, s.sightAngle, s.sightRange);
                }
                Handles.color = VerhaltenStyle.Alpha(c, line);
                Handles.DrawWireArc(ground, Vector3.up, from, s.sightAngle, s.sightRange, sel ? 2f : 1f);
                if (s.sightAngle < 359.9f)
                {
                    Handles.DrawLine(ground, ground + from * s.sightRange);
                    Handles.DrawLine(ground, ground + Quaternion.Euler(0f, half, 0f) * fwd * s.sightRange);
                }
                if (sel)
                {
                    // Eye height: a short post from the ground to the eyes, and the cone's edge at eye level.
                    Handles.DrawDottedLine(ground, eye, 2f);
                    Handles.DrawWireArc(eye, Vector3.up, from, s.sightAngle, s.sightRange * 0.15f);
                }
                // Noticing: the cone fills from the figure outward as the meter rises.
                if (play && s.Alert > 0f && s.Alert < 1f)
                {
                    Handles.color = VerhaltenStyle.Alpha(VerhaltenStyle.Noticing, 0.35f);
                    Handles.DrawSolidArc(ground, Vector3.up, from, s.sightAngle, s.sightRange * s.Alert);
                }
            }

            if (s.hearing && s.hearingRange > 0f)
            {
                var ground = new Vector3(s.transform.position.x, Ground(s.transform), s.transform.position.z);
                // Darker blue than the cone: a dashed ring in sky blue disappears on a pale grey floor.
                Handles.color = VerhaltenStyle.Alpha(state == Senses.Awareness.Hört ? VerhaltenStyle.Hearing : VerhaltenStyle.Path, Mathf.Max(line, 0.6f));
                DashedCircle(ground, s.hearingRange, sel ? 3f : 2f);
                if (sel)
                {
                    Handles.color = VerhaltenStyle.Alpha(VerhaltenStyle.Hearing, 0.35f);
                    DashedCircle(ground, s.hearingRange * 2f, 1.5f);   // running carries twice as far
                    Handles.Label(ground + Vector3.right * s.hearingRange * 2f, " Rennen", EditorStyles.miniLabel);
                }
            }

            if (!play) return;
            var target = s.Target;
            if (target != null && (s.CanSee || s.LastRayBlocked) && s.sight)
            {
                Handles.color = s.LastRayBlocked ? VerhaltenStyle.Alpha(VerhaltenStyle.Calm, 0.8f) : VerhaltenStyle.Seeing;
                if (s.LastRayBlocked) { Handles.DrawDottedLine(eye, s.LastRayEnd, 3f); Handles.DrawWireDisc(s.LastRayEnd, -(s.LastRayEnd - eye).normalized, 0.12f); }
                else Handles.DrawLine(eye, s.LastRayEnd, 2f);
            }
            if (s.HasClue && (s.Noticed || Time.time - s.LastClueTime < 5f))
            {
                Handles.color = VerhaltenStyle.Searching;
                var p = s.LastClue;
                Handles.DrawWireDisc(p, Vector3.up, 0.35f, 2f);
                Handles.DrawLine(p + new Vector3(-0.25f, 0f, -0.25f), p + new Vector3(0.25f, 0f, 0.25f));
                Handles.DrawLine(p + new Vector3(-0.25f, 0f, 0.25f), p + new Vector3(0.25f, 0f, -0.25f));
                if (sel) Handles.Label(p + Vector3.up * 0.4f, "letzte Spur", EditorStyles.miniLabel);
            }

            // Above the head: behaviour · state, coloured like the cone.
            var head = new Vector3(s.transform.position.x, Senses.Feet(s.transform) + Mathf.Max(2.1f, s.eyeHeight + 0.5f), s.transform.position.z);
            var hex = ColorUtility.ToHtmlStringRGB(c);
            var behaviour = s.Current != null ? NpcBehaviour.NameOf(s.Current.Kind) : "Stehen";
            Handles.Label(head, $"<b>{behaviour}</b> · <color=#{hex}>{s.Describe()}</color>", Label);
        }

        static void DashedCircle(Vector3 centre, float r, float thickness = 1f)
        {
            const int n = 48;
            for (int i = 0; i < n; i += 2)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                Handles.DrawLine(centre + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * r, centre + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * r, thickness);
            }
        }

        // ---------------------------------------------------------------------------------- behaviours
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        static void DrawBehaviour(NpcBehaviour b, GizmoType type)
        {
            bool sel = (type & GizmoType.Selected) != 0 || (type & GizmoType.InSelectionHierarchy) != 0;
            bool play = Application.isPlaying;
            bool current = b.enabled;
            if (!sel && !current) return;                 // other behaviours of an unselected figure: hidden
            float a = (current ? 1f : 0.45f) * (sel ? 1f : 0.45f);
            bool words = sel && current;   // the others draw their shapes quietly; labels of all would pile up on one spot

            switch (b)
            {
                case Patrol p: DrawRoute(p, a, words); break;
                case Wander w:
                    Handles.color = VerhaltenStyle.Alpha(VerhaltenStyle.Route, a);
                    Handles.DrawWireDisc(w.Centre, Vector3.up, w.radius);
                    if (words) Handles.Label(w.Centre + Vector3.forward * w.radius, " Umherstreifen", EditorStyles.miniLabel);
                    break;
                case Guard g:
                    Marker(g.PostPosition, g.PostRotation, a, "Posten", words);
                    if (sel && g.lookAngle > 0f)
                    {
                        var fwd = g.PostRotation * Vector3.forward; fwd.y = 0f;
                        Handles.color = VerhaltenStyle.Alpha(VerhaltenStyle.Route, 0.6f * a);
                        Handles.DrawWireArc(g.PostPosition + Vector3.up * 0.05f, Vector3.up,
                            Quaternion.Euler(0f, -g.lookAngle * 0.5f, 0f) * fwd.normalized, g.lookAngle, 1.6f, 2f);
                    }
                    break;
                case ReturnHome r: Marker(r.HomePosition, r.HomeRotation, a, "Zuhause", words); break;
                case Follow f:
                    if (play && f.Target != null)
                    {
                        Handles.color = VerhaltenStyle.Alpha(VerhaltenStyle.Route, a);
                        Handles.DrawWireDisc(f.Target.position, Vector3.up, f.keepDistance);
                        Handles.color = VerhaltenStyle.Alpha(VerhaltenStyle.Route, 0.35f * a);
                        DashedCircle(f.Target.position, f.keepDistance + f.slack);
                    }
                    else if (words) Handles.Label(b.transform.position + Vector3.up * 2.4f, $"folgt {(f.target ? f.target.name : "dem Spieler")} mit {f.keepDistance:0.#} m Abstand", EditorStyles.miniLabel);
                    break;
                case Flee fl:
                    if (play && fl.Threat != null)
                    {
                        Handles.color = VerhaltenStyle.Alpha(VerhaltenStyle.Searching, 0.6f * a);
                        DashedCircle(fl.Threat.position, fl.safeDistance);
                    }
                    else if (sel)
                    {
                        Handles.color = VerhaltenStyle.Alpha(VerhaltenStyle.Searching, 0.4f);
                        DashedCircle(b.transform.position, fl.safeDistance);
                        if (words) Handles.Label(b.transform.position + Vector3.forward * fl.safeDistance, " sicher ab hier", EditorStyles.miniLabel);
                    }
                    break;
            }

            if (play && current)
            {
                b.GetPath(s_Path);
                if (s_Path.Count > 1)
                {
                    Handles.color = VerhaltenStyle.Path;
                    Handles.DrawAAPolyLine(3f, s_Path.ToArray());
                    Handles.DrawWireDisc(s_Path[s_Path.Count - 1], Vector3.up, 0.25f, 2f);
                }
            }
        }

        static void DrawRoute(Patrol p, float a, bool sel)
        {
            var pts = new List<Vector3>();
            foreach (var w in p.waypoints) if (w != null) pts.Add(w.position);
            if (pts.Count == 0)
            {
                if (sel) Handles.Label(p.transform.position + Vector3.up * 2.4f, "Patrouillieren: noch keine Wegpunkte", EditorStyles.miniLabel);
                return;
            }
            Handles.color = VerhaltenStyle.Alpha(VerhaltenStyle.Route, a);
            for (int i = 0; i + 1 < pts.Count; i++) Arrow(pts[i], pts[i + 1]);
            if (pts.Count > 1)
            {
                if (p.route == Patrol.RouteMode.Rundgang) Arrow(pts[pts.Count - 1], pts[0]);
                else if (p.route == Patrol.RouteMode.Zufällig)
                {
                    Handles.color = VerhaltenStyle.Alpha(VerhaltenStyle.Route, 0.3f * a);
                    for (int i = 0; i < pts.Count; i++) for (int j = i + 2; j < pts.Count; j++) Handles.DrawDottedLine(pts[i], pts[j], 4f);
                }
            }
            for (int i = 0, n = 0; i < p.waypoints.Count; i++)
            {
                var w = p.waypoints[i];
                if (w == null) continue;
                n++;
                bool at = Application.isPlaying && p.enabled && p.CurrentIndex == i;
                Handles.color = VerhaltenStyle.Alpha(at ? VerhaltenStyle.Path : VerhaltenStyle.Route, a);
                Handles.DrawWireDisc(w.position, Vector3.up, at ? 0.45f : 0.3f, at ? 3f : 1.5f);
                if (p.faceWaypoint) Handles.DrawLine(w.position, w.position + w.forward * 0.7f, 2f);
                if (sel || at) Handles.Label(w.position + Vector3.up * 0.5f, (i + 1).ToString(), Label);
            }
            if (sel && p.route == Patrol.RouteMode.HinUndZurück && pts.Count > 1)
                Handles.Label(pts[pts.Count - 1] + Vector3.up * 0.9f, "↩ zurück", EditorStyles.miniLabel);
        }

        static void Arrow(Vector3 from, Vector3 to)
        {
            Handles.DrawLine(from, to, 2f);
            var dir = to - from; dir.y = 0f;
            if (dir.sqrMagnitude < 0.25f) return;
            var mid = Vector3.Lerp(from, to, 0.55f);
            var back = -dir.normalized * 0.4f;
            Handles.DrawLine(mid, mid + Quaternion.Euler(0f, 30f, 0f) * back, 2f);
            Handles.DrawLine(mid, mid + Quaternion.Euler(0f, -30f, 0f) * back, 2f);
        }

        static void Marker(Vector3 pos, Quaternion rot, float a, string text, bool sel)
        {
            Handles.color = VerhaltenStyle.Alpha(VerhaltenStyle.Home, 0.8f * a);
            Handles.DrawWireDisc(pos, Vector3.up, 0.4f, 2f);
            var fwd = rot * Vector3.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude > 1e-4f) Handles.DrawLine(pos, pos + fwd.normalized * 0.8f, 2f);
            if (sel) Handles.Label(pos + Vector3.up * 0.5f, text, EditorStyles.miniLabel);
        }
    }
}
