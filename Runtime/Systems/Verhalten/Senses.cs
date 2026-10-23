using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Kit
{
    /// <summary>
    /// Wahrnehmung: what a figure notices. Sight (range, cone, walls in the way), hearing (footsteps and
    /// Geräusch nodes), memory (how long it keeps searching) and touch. It reports to the graph through the
    /// Figur trigger: Sieht Spieler, Hört Spieler, Hört Geräusch, Verliert Spieler, Berührt. The behaviours
    /// (<see cref="NpcBehaviour"/>) on the same object only move; deciding what follows what stays in the graph.
    /// Added automatically with every behaviour; on its own it makes a watcher that never moves (a camera, a statue).
    /// </summary>
    [AddComponentMenu(KitBrand.ComponentMenu + "Verhalten/Wahrnehmung")]
    [DisallowMultipleComponent]
    [HelpURL("https://allknivesnobagel.com/teaching/gec1/")]
    public class Senses : MonoBehaviour
    {
        /// <summary>What the figure knows about the watched object right now.</summary>
        public enum Awareness { Ruhig, Bemerkt, Sieht, Hört, Sucht }

        [Header("Sehen")]
        [Hint("Die Figur kann sehen.", edges: "Aus: nur Hören und Berühren zählen, z. B. für eine Wache, die nur hört.", label: "Kann sehen")]
        public bool sight = true;
        [Hint("Wie weit die Figur sieht, von den Augen aus gemessen.", unit: "m", range: "6 bis 15",
              edges: "Klein: kurzsichtig, man kommt nah heran. Groß: sieht über den ganzen Platz.", label: "Sichtweite")]
        [Min(0f)] public float sightRange = 10f;
        [Hint("Breite des Sichtkegels.", unit: "°", range: "60 bis 140",
              edges: "Schmal: ein Tunnelblick, an der Seite vorbeischleichen geht. 360: Augen überall.", label: "Sichtwinkel")]
        [Range(1f, 360f)] public float sightAngle = 100f;
        [Hint("Höhe der Augen über den Füßen. Von hier aus wird gesehen und geprüft, ob etwas im Weg ist.", unit: "m", range: "0,3 bis 2",
              edges: "Niedrig (ein Hund): sieht nicht über Kisten. Hoch: sieht über Mauern.", label: "Augenhöhe")]
        [Min(0f)] public float eyeHeight = 1.6f;
        [Hint("Wände und andere Collider verdecken die Sicht.", edges: "Aus: sieht durch Wände, z. B. ein Geist oder ein Radar.", label: "Wände verdecken")]
        public bool wallsBlock = true;
        [Hint("Wie lange der Spieler sichtbar sein muss, bis die Figur ihn bemerkt. Gilt am Rand der Sichtweite; " +
              "nah dran geht es bis zu dreimal schneller.", unit: "s", range: "0 bis 2",
              edges: "0: bemerkt sofort. Groß: viel Zeit zum Wegducken, gut für Schleichspiele.", label: "Bemerken dauert")]
        [Min(0f)] public float noticeTime = 0.6f;

        [Header("Hören")]
        [Hint("Die Figur hört die Schritte des Spielers und Geräusch-Knoten.", edges: "Aus: taub.", label: "Kann hören")]
        public bool hearing = false;
        [Hint("Wie weit die Figur Schritte bei normalem Gehen (4 m/s) hört. Rennen hört sie weiter, langsames Gehen weniger weit. " +
              "Wände dämpfen nicht.", unit: "m", range: "2 bis 10", edges: "Stillstand hört sie nie. Rennen: bis doppelt so weit.", label: "Hörweite")]
        [Min(0f)] public float hearingRange = 5f;

        [Header("Gedächtnis")]
        [Hint("Wie lange die Figur ohne Sehen und Hören weitersucht, bis sie den Spieler aufgibt. Dann feuert „Verliert Spieler“.",
              unit: "s", range: "1 bis 10", edges: "0: vergisst sofort, wenn er aus dem Blick ist. Groß: hartnäckig.", label: "Vergessen nach")]
        [Min(0f)] public float forgetAfter = 3f;

        [Header("Berühren")]
        [Hint("Zusätzlicher Abstand zwischen den Rändern beider Körper, ab dem „Berührt“ feuert.", unit: "m", range: "0 bis 1",
              edges: "0: erst bei echter Berührung. 1: schon eine Armlänge vorher.", label: "Berühren ab")]
        [Min(0f)] public float touchDistance = 0.3f;

        [Header("Wen")]
        [Hint("Wen die Figur wahrnimmt.", edges: "Leer: den Spieler (Kit-Spieler oder Tag „Player“). " +
              "Ein anderes Objekt: z. B. eine Katze, die nur die Maus beachtet.", label: "Wen")]
        public Transform watched;

        // ---- events for the Figur trigger and the behaviours ------------------------------------------
        /// <summary>
        /// Everything the Figur trigger reports. The context (which behaviour ran, whom it was about) is fixed when
        /// the event happens, so a listener that switches the behaviour does not change what the next listener sees.
        /// </summary>
        public event Action<FigureEvent> Reported;
        public event Action<NpcBehaviour> Switched;

        // ---- live state (Inspector, gizmos, tests) -----------------------------------------------------
        /// <summary>0..1: how close the figure is to noticing by sight.</summary>
        public float Alert { get; private set; }
        public bool CanSee { get; private set; }
        public bool CanHear { get; private set; }
        /// <summary>The figure knows about the watched object (from the first sighting/hearing until it forgets).</summary>
        public bool Noticed { get; private set; }
        public Awareness State { get; private set; }
        /// <summary>Where the figure last saw or heard something (the player or a Geräusch).</summary>
        public Vector3 LastClue { get; private set; }
        public bool HasClue { get; private set; }
        public float LastClueTime { get; private set; } = -1f;
        /// <summary>Seconds left until „Verliert Spieler“, while searching; otherwise -1.</summary>
        public float ForgetIn => State == Awareness.Sucht ? Mathf.Max(0f, m_LastPerceived + forgetAfter - Time.time) : -1f;
        public bool IsTouching { get; private set; }
        /// <summary>Where the eye ray to the target stopped last frame (for the gizmo), and whether it got through.</summary>
        public Vector3 LastRayEnd { get; private set; }
        public bool LastRayBlocked { get; private set; }

        /// <summary>The behaviour that is running now (null = Stehen).</summary>
        public NpcBehaviour Current { get; internal set; }
        public BehaviourKind CurrentKind => Current != null ? Current.Kind : BehaviourKind.Stehen;
        public Vector3 StartPosition { get; private set; }
        public Quaternion StartRotation { get; private set; }

        float m_LastPerceived;
        bool m_SawThisEpisode, m_HeardThisEpisode, m_Init;
        Vector3 m_LastTargetPos; bool m_HaveTargetPos;
        float m_TargetSpeed;
        Transform m_Player; float m_NextPlayerLookup;
        Transform m_LastTarget;

        static readonly List<Senses> s_All = new List<Senses>();
        static readonly RaycastHit[] s_Hits = new RaycastHit[16];

        /// <summary>Normal walking speed of the kit player; hearing ranges are given for this speed.</summary>
        public const float ReferenceSpeed = 4f;

        // ---------------------------------------------------------------------------------- lifecycle
        void Awake() => Init();

        internal void Init()
        {
            if (m_Init) return;
            m_Init = true;
            StartPosition = transform.position;
            StartRotation = transform.rotation;
        }

        void OnEnable() { s_All.Add(this); m_HaveTargetPos = false; }

        void OnDisable()
        {
            s_All.Remove(this);
            CanSee = CanHear = false; Alert = 0f; IsTouching = false;
            // Switched off while it knew the player: it does not forget silently, the graph hears about it.
            if (Noticed) Forget();
            State = Awareness.Ruhig;
        }

        // ---------------------------------------------------------------------------------- target
        /// <summary>The watched object: <see cref="watched"/>, else the player.</summary>
        public Transform Target
        {
            get
            {
                if (watched != null) return watched;
                if (m_Player == null && Time.unscaledTime >= m_NextPlayerLookup)
                {
                    m_NextPlayerLookup = Time.unscaledTime + 1f;
                    m_Player = FindPlayer();
                }
                return m_Player;
            }
        }

        /// <summary>The kit player, or else the object tagged „Player“.</summary>
        public static Transform FindPlayer()
        {
            var kp = FindAnyObjectByType<KitPlayer>();
            if (kp != null) return kp.transform;
            try { var go = GameObject.FindWithTag("Player"); return go ? go.transform : null; }
            catch (UnityException) { return null; }   // the tag does not exist in this project
        }

        /// <summary>The figure's eyes in world space.</summary>
        public Vector3 Eye => new Vector3(transform.position.x, Feet(transform) + eyeHeight, transform.position.z);

        /// <summary>
        /// Centre, half height and radius of a body, from its CharacterController, capsule, box or agent. Read from
        /// the Transform, not from collider bounds: those only follow a moved object at the next physics step.
        /// </summary>
        public static void Body(Transform t, out Vector3 centre, out float halfHeight, out float radius)
        {
            var s = t.lossyScale;
            float sy = Mathf.Abs(s.y), sxz = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
            if (t.TryGetComponent(out CharacterController cc))
            { centre = t.TransformPoint(cc.center); halfHeight = cc.height * 0.5f * sy; radius = cc.radius * sxz; return; }
            if (t.TryGetComponent(out CapsuleCollider cap) && cap.enabled)
            { centre = t.TransformPoint(cap.center); halfHeight = Mathf.Max(cap.height * 0.5f, cap.radius) * sy; radius = cap.radius * sxz; return; }
            if (t.TryGetComponent(out SphereCollider sph) && sph.enabled)
            { centre = t.TransformPoint(sph.center); halfHeight = sph.radius * sxz; radius = halfHeight; return; }
            if (t.TryGetComponent(out BoxCollider box) && box.enabled)
            { centre = t.TransformPoint(box.center); halfHeight = box.size.y * 0.5f * sy; radius = Mathf.Min(box.size.x * Mathf.Abs(s.x), box.size.z * Mathf.Abs(s.z)) * 0.5f; return; }
            if (t.TryGetComponent(out NavMeshAgent a))
            { halfHeight = a.height * 0.5f; centre = t.position + Vector3.up * (halfHeight - a.baseOffset); radius = a.radius; return; }
            if (t.TryGetComponent(out Collider c) && c.enabled)
            { var b = c.bounds; centre = b.center; halfHeight = b.extents.y; radius = Mathf.Min(b.extents.x, b.extents.z); return; }
            centre = t.position + Vector3.up * 0.9f; halfHeight = 0.9f; radius = 0.3f;
        }

        /// <summary>Height of an object's feet: the bottom of its body.</summary>
        public static float Feet(Transform t) { Body(t, out var c, out var h, out _); return c.y - h; }

        /// <summary>Horizontal radius of a body: its agent, else its collider.</summary>
        public static float RadiusOf(Transform t)
        {
            if (t.TryGetComponent(out NavMeshAgent a)) return a.radius;
            Body(t, out _, out _, out var r);
            return r;
        }

        // ---------------------------------------------------------------------------------- sensing
        void Update()
        {
            var target = Target;
            if (target != m_LastTarget) { m_LastTarget = target; IsTouching = false; m_HaveTargetPos = false; m_TargetSpeed = 0f; }
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                CanSee = CanHear = false; m_HaveTargetPos = false; IsTouching = false;
                Decay(Time.deltaTime);
                UpdateMemory();
                return;
            }

            // Speed of the target from its movement, so any object works, not only the kit player.
            var pos = target.position;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            float measured = m_HaveTargetPos ? Vector3.Distance(Flat(pos), Flat(m_LastTargetPos)) / dt : 0f;
            m_TargetSpeed = Mathf.Lerp(m_TargetSpeed, measured, 1f - Mathf.Exp(-12f * dt));   // smoothed: a single frame's jump is no step
            m_LastTargetPos = pos; m_HaveTargetPos = true;

            CanSee = sight && Sees(target);
            CanHear = hearing && Hears(target);

            if (CanSee)
            {
                float d = Vector3.Distance(Eye, Centre(target));
                float closeness = sightRange > 0f ? Mathf.Clamp01(1f - d / sightRange) : 1f;
                float rate = noticeTime <= 0f ? float.PositiveInfinity : (1f + 2f * closeness) / noticeTime;
                Alert = Mathf.Min(1f, Alert + rate * Time.deltaTime);
            }
            else if (!Noticed) Decay(Time.deltaTime);

            bool seenNow = CanSee && (Alert >= 1f || Noticed);
            if (seenNow || CanHear)
            {
                m_LastPerceived = Time.time;
                SetClue(pos);
                if (!Noticed) { Noticed = true; m_SawThisEpisode = m_HeardThisEpisode = false; }
                if (seenNow && !m_SawThisEpisode) { m_SawThisEpisode = true; Alert = 1f; Raise(FigureEvent.Kind.Sieht, target); }
                if (CanHear && !m_HeardThisEpisode) { m_HeardThisEpisode = true; Raise(FigureEvent.Kind.Hört, target); }
            }
            UpdateMemory();
            UpdateTouch(target);
        }

        void Decay(float dt)
        {
            if (Noticed) return;
            Alert = noticeTime <= 0f ? 0f : Mathf.Max(0f, Alert - dt / noticeTime);
        }

        void UpdateMemory()
        {
            if (Noticed && !(CanSee && Alert >= 1f) && !CanHear && Time.time - m_LastPerceived >= forgetAfter) Forget();
            State = !Noticed ? (Alert > 0f ? Awareness.Bemerkt : Awareness.Ruhig)
                  : CanSee ? Awareness.Sieht
                  : CanHear ? Awareness.Hört
                  : Awareness.Sucht;
        }

        void Forget()
        {
            Noticed = false; Alert = 0f;
            m_SawThisEpisode = m_HeardThisEpisode = false;
            Raise(FigureEvent.Kind.Verliert, Target);
        }

        void UpdateTouch(Transform target)
        {
            float gap = Vector3.Distance(Flat(transform.position), Flat(target.position)) - RadiusOf(transform) - RadiusOf(target);
            float dy = Mathf.Abs(Feet(transform) - Feet(target));
            bool touching = dy < 2f && gap <= touchDistance;
            bool released = dy >= 2f || gap > touchDistance + 0.3f;   // a little slack, so standing at the edge fires once
            if (touching && !IsTouching) { IsTouching = true; Raise(FigureEvent.Kind.Berührt, target); }
            else if (released) IsTouching = false;
        }

        /// <summary>In range, in the cone and (if walls block) with a free line of sight to the body or head.</summary>
        public bool Sees(Transform target)
        {
            var eye = Eye;
            var centre = Centre(target);
            LastRayEnd = centre; LastRayBlocked = false;
            if ((centre - eye).sqrMagnitude > sightRange * sightRange) return false;
            if (!InCone(centre)) return false;
            if (!wallsBlock) return true;
            if (Clear(eye, centre, target)) return true;
            var head = Head(target);
            if (Clear(eye, head, target)) { LastRayEnd = head; LastRayBlocked = false; return true; }
            return false;
        }

        /// <summary>Is the point inside the horizontal sight cone?</summary>
        public bool InCone(Vector3 point)
        {
            if (sightAngle >= 359.9f) return true;
            var to = Flat(point - transform.position);
            if (to.sqrMagnitude < 1e-4f) return true;
            return Vector3.Angle(Flat(transform.forward), to) <= sightAngle * 0.5f;
        }

        bool Clear(Vector3 from, Vector3 to, Transform target)
        {
            var dir = to - from;
            float len = dir.magnitude;
            if (len < 1e-3f) return true;
            int n = Physics.RaycastNonAlloc(from, dir / len, s_Hits, len, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                var t = s_Hits[i].transform;
                // Own body and the target's body (or the body the target belongs to) do not hide the target.
                if (t.IsChildOf(transform) || t.IsChildOf(target) || target.IsChildOf(t)) continue;
                if (s_Hits[i].distance < nearest) nearest = s_Hits[i].distance;
            }
            if (float.IsPositiveInfinity(nearest)) return true;
            LastRayEnd = from + dir / len * nearest; LastRayBlocked = true;
            return false;
        }

        bool Hears(Transform target)
        {
            float range = HearingRangeFor(m_TargetSpeed);
            return range > 0f && Vector3.Distance(transform.position, target.position) <= range;
        }

        /// <summary>How far footsteps at this speed carry: none when standing, full range at walking pace, up to double.</summary>
        public float HearingRangeFor(float speed)
        {
            if (speed < 0.3f) return 0f;
            return hearingRange * Mathf.Clamp(speed / ReferenceSpeed, 0f, 2f);
        }

        /// <summary>Speed of the watched object as the ears measure it (m/s).</summary>
        public float TargetSpeed => m_TargetSpeed;

        static Vector3 Centre(Transform t) { Body(t, out var c, out _, out _); return c; }

        static Vector3 Head(Transform t)
        {
            if (t.TryGetComponent(out KitPlayer p) && p.head != null) return p.head.position;
            Body(t, out var c, out var h, out _);
            return c + Vector3.up * h * 0.8f;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        void SetClue(Vector3 p) { LastClue = p; HasClue = true; LastClueTime = Time.time; }

        // ---------------------------------------------------------------------------------- noise
        /// <summary>
        /// A noise at <paramref name="position"/> that carries <paramref name="range"/> metres. Every figure with
        /// hearing inside that range hears it: its clue moves there and „Hört Geräusch“ fires. The player is
        /// not noticed by it (a thrown stone is not the thrower). Returns how many figures heard it.
        /// </summary>
        public static int MakeNoise(Vector3 position, float range)
        {
            int n = 0;
            foreach (var s in s_All.ToArray())
            {
                if (s == null || !s.hearing || !s.isActiveAndEnabled) continue;
                if (Vector3.Distance(s.transform.position, position) > range) continue;
                s.SetClue(position);
                n++;
                s.Raise(FigureEvent.Kind.Geräusch, null, where: position);
            }
            return n;
        }

        // ---------------------------------------------------------------------------------- hub
        internal void RaiseArrived(NpcBehaviour b, int index) => Raise(FigureEvent.Kind.ZielErreicht, null, b, index);

        void Raise(FigureEvent.Kind what, Transform about, NpcBehaviour by = null, int waypoint = 0, Vector3 where = default)
        {
            var e = new FigureEvent
            {
                what = what, figure = this, about = about, waypoint = waypoint, where = where,
                during = by != null ? by.Kind : CurrentKind,
            };
            Reported?.Invoke(e);
        }
        internal void RaiseSwitched(NpcBehaviour b) => Switched?.Invoke(b);

        /// <summary>One line for the Inspector and the graph.</summary>
        public string Describe()
        {
            if (!isActiveAndEnabled) return "aus: nimmt nichts wahr";
            if (Target == null) return watched == null ? "kein Spieler in der Szene" : "nichts zu beobachten";
            switch (State)
            {
                case Awareness.Bemerkt: return $"bemerkt etwas … {Alert * 100f:0} %";
                case Awareness.Sieht: return "sieht " + Target.name;
                case Awareness.Hört: return "hört " + Target.name;
                case Awareness.Sucht: return $"sucht {Target.name}, gibt auf in {ForgetIn:0.0} s";
                default: return "ruhig";
            }
        }
    }
}
