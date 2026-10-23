using UnityEngine;

namespace Kit.Editor
{
    /// <summary>
    /// Colours for figures in the Scene view and Inspector. Okabe–Ito, so they stay apart for colour-blind
    /// eyes: calm is sky blue, noticing yellow, seeing vermillion, hearing orange, searching purple.
    /// </summary>
    static class VerhaltenStyle
    {
        public static readonly Color Calm = new Color(0.34f, 0.71f, 0.91f);      // #56B4E9
        public static readonly Color Noticing = new Color(0.94f, 0.89f, 0.26f);  // #F0E442
        public static readonly Color Seeing = new Color(0.84f, 0.37f, 0.00f);    // #D55E00
        public static readonly Color Hearing = new Color(0.90f, 0.62f, 0.00f);   // #E69F00
        public static readonly Color Searching = new Color(0.80f, 0.47f, 0.65f); // #CC79A7
        public static readonly Color Route = new Color(0.00f, 0.62f, 0.45f);     // #009E73
        public static readonly Color Path = new Color(0.00f, 0.45f, 0.70f);      // #0072B2
        public static readonly Color Home = new Color(0.95f, 0.95f, 0.95f);

        public static Color Of(Senses.Awareness a)
        {
            switch (a)
            {
                case Senses.Awareness.Bemerkt: return Noticing;
                case Senses.Awareness.Sieht: return Seeing;
                case Senses.Awareness.Hört: return Hearing;
                case Senses.Awareness.Sucht: return Searching;
                default: return Calm;
            }
        }

        public static Color Alpha(Color c, float a) { c.a = a; return c; }
    }
}
