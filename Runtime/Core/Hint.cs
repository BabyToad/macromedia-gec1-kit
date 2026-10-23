using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Kit
{
    /// <summary>
    /// The tooltip of anything a student can set, read or connect: a node setting, a ● or ◆ port, a ▶ event, or a
    /// serialized field of a kit component (there its drawer shows the German label, the unit and the tooltip). One format everywhere:
    /// <code>
    /// [Hint("Wie lange die Fahrt dauert.", unit: "s", range: "0,2 bis 3", edges: "0: springt sofort. Groß: sehr langsam.")]
    /// </code>
    /// becomes "Wie lange die Fahrt dauert. / Einheit: s · sinnvoll: 0,2 bis 3 / 0: springt sofort. Groß: sehr langsam."
    /// German, impersonal, plain. <paramref name="what"/> says what it does in the game, not how it is coded.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Class, Inherited = true)]
    public sealed class HintAttribute : PropertyAttribute
    {
        public readonly string What, Unit, Range, Edges;
        /// <summary>German Inspector label for a component field (C# names stay English); null = Unity's own label.</summary>
        public readonly string Label;

        /// <param name="what">What it does, one or two sentences.</param>
        /// <param name="unit">Unit, short ("s", "m", "m/s", "°", "Stück"); null if it has none.</param>
        /// <param name="range">Sensible range in plain words ("0,2 bis 3"); null if any value works.</param>
        /// <param name="edges">What happens at the extremes ("0: sofort. Groß: sehr langsam."); null if nothing special.</param>
        /// <param name="label">Only on component fields: the German label shown in the Inspector ("Gehtempo").</param>
        public HintAttribute(string what, string unit = null, string range = null, string edges = null, string label = null)
        {
            What = what; Unit = unit; Range = range; Edges = edges; Label = label;
            Text = Compose(what, unit, range, edges);
            // Not a TooltipAttribute on purpose: Unity treats those as a bare tooltip and never runs their drawer, so the
            // German label would not show. Our drawer (Kit.Editor.HintDrawer) sets the tooltip itself.
            order = -1000;   // Unity gives a field one drawer, chosen by order: ours, which also draws [Range]/[Min]
        }

        /// <summary>The finished tooltip text.</summary>
        public readonly string Text;
        /// <summary>The same text under its old name (from when Hint was a TooltipAttribute); kept so existing code compiles.</summary>
        public string tooltip => Text;

        public static string Compose(string what, string unit, string range, string edges)
        {
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(what)) lines.Add(what);
            var facts = new List<string>();
            if (!string.IsNullOrEmpty(unit)) facts.Add("Einheit: " + unit);
            if (!string.IsNullOrEmpty(range)) facts.Add("sinnvoll: " + range);
            if (facts.Count > 0) lines.Add(string.Join(" · ", facts));
            if (!string.IsNullOrEmpty(edges)) lines.Add(edges);
            return string.Join("\n", lines);
        }

        /// <summary>The hint on a member, or null.</summary>
        public static HintAttribute Of(MemberInfo m) => m?.GetCustomAttribute<HintAttribute>(true);
    }
}
