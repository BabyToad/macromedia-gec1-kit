using System;
using UnityEngine;

namespace Kit
{
    // Animator-Wert: setzt einen Parameter im Animator (Trigger, Ja/Nein, Zahl oder Ganzzahl). Die Übergänge im
    // Animator Controller entscheiden dann, welche Animation läuft.
    // "Gesetzt" heißt: der Parameter ist gesetzt – nicht, dass die Animation schon fertig ist.
    [Serializable]
    [NodeInfo("Animator-Wert", "Erweiterung", "Parameter im Animator setzen")]
    public class AnimatorValue : KitNode
    {
        public enum Kind { Trigger, JaNein, Zahl, Ganzzahl }   // Reihenfolge nicht ändern: gespeicherte Graphen nutzen die Nummer

        [Ref("Animator")] public Animator animator;
        [Setting("Parameter")] public string parameter = "Open";
        [Setting("Art")] public Kind kind = Kind.Trigger;
        [Setting("Ja/Nein")] public bool boolValue = true;
        [DataIn("Zahl")] public float number;

        [Output("Gesetzt")] [NonSerialized] public Output set;

        [Input("Setzen")]
        public void Set(Signal s)
        {
            if (animator.runtimeAnimatorController == null) { Fail($"„{animator.name}“ hat keinen Animator Controller"); return; }
            var p = Array.Find(animator.parameters, x => x.name == parameter);
            if (p == null) { Fail($"Parameter „{parameter}“ gibt es im Animator nicht"); return; }
            if (p.type != TypeOf(kind)) { Fail(Mismatch(p.type)); return; }   // vor dem Setzen prüfen: Unity würde still nichts tun
            switch (kind)
            {
                case Kind.Trigger: animator.SetTrigger(parameter); break;
                case Kind.JaNein: animator.SetBool(parameter, boolValue); break;
                case Kind.Ganzzahl: animator.SetInteger(parameter, Mathf.RoundToInt(number)); break;
                default: animator.SetFloat(parameter, number); break;
            }
            Done($"✓ {parameter} gesetzt");
            set.Fire(s);
        }

        static AnimatorControllerParameterType TypeOf(Kind k) => k switch
        {
            Kind.Trigger => AnimatorControllerParameterType.Trigger,
            Kind.JaNein => AnimatorControllerParameterType.Bool,
            Kind.Ganzzahl => AnimatorControllerParameterType.Int,
            _ => AnimatorControllerParameterType.Float,
        };

        static string Name(AnimatorControllerParameterType t) => t switch
        {
            AnimatorControllerParameterType.Trigger => "Trigger",
            AnimatorControllerParameterType.Bool => "Ja/Nein",
            AnimatorControllerParameterType.Int => "Ganzzahl",
            _ => "Zahl",
        };

        string Mismatch(AnimatorControllerParameterType actual) =>
            $"„{parameter}“ ist im Animator ein {Name(actual)}-Parameter, hier ist aber „{Name(TypeOf(kind))}“ eingestellt (Art ändern)";

        public override void Validate(System.Collections.Generic.List<string> problems)
        {
            if (animator == null) return;
            if (animator.runtimeAnimatorController == null) { problems.Add($"„{animator.name}“ hat keinen Animator Controller"); return; }
            if (!animator.isActiveAndEnabled) return;                  // Parameter sind nur an aktiven Animatoren lesbar
            var p = Array.Find(animator.parameters, x => x.name == parameter);
            if (p == null) problems.Add($"Parameter „{parameter}“ gibt es im Animator nicht");
            else if (p.type != TypeOf(kind)) problems.Add(Mismatch(p.type));
        }
    }
}
