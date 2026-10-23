using UnityEngine;

namespace Kit
{
    /// <summary>One thing a figure noticed or reached, with the context fixed at the moment it happened.</summary>
    public struct FigureEvent
    {
        public enum Kind { Sieht, Hört, Geräusch, Verliert, ZielErreicht, Berührt }

        public Kind what;
        public Senses figure;
        /// <summary>The behaviour that was running when it happened (for „Nur während“).</summary>
        public BehaviourKind during;
        /// <summary>Whom it was about (the player), or null (Ziel erreicht, Geräusch).</summary>
        public Transform about;
        /// <summary>At „Ziel erreicht“ while patrolling: the waypoint number, 1-based; else 0.</summary>
        public int waypoint;
        /// <summary>Where a Geräusch was.</summary>
        public Vector3 where;
    }
}
