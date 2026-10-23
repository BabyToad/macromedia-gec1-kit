using System;
using System.Collections;
using UnityEngine;

namespace Kit
{
    // Ton abspielen: spielt einen Clip auf einer AudioSource. Lautstärke, 3D usw. werden an der
    // AudioSource eingestellt – der Knoten ändert dort nichts.
    // "Ton fertig" heißt: die Quelle spielt nicht mehr. Das ist am Ende des Clips so, aber auch nach
    // Stopp oder Pause an der Quelle. Schneidet ein neuer Ton den alten ab, kommt für den alten kein
    // "Ton fertig", nur für den neuen. Bei "Loop" kommt es nie.
    [Serializable]
    [NodeInfo("Ton abspielen", KitGroups.Aktion, "Clip auf einer AudioSource abspielen")]
    public class PlaySound : KitNode
    {
        [Hint("Die AudioSource, die spielt. Lautstärke, 3D-Klang und Loop werden dort eingestellt.")]
        [Ref("Quelle")] public AudioSource source;
        [Hint("Welcher Ton gespielt wird. Ersetzt den Clip an der Quelle dauerhaft.", edges: "Leer: der Clip, der schon an der Quelle liegt.")]
        [Setting("Clip")] public AudioClip clip;

        [Hint("Feuert, sobald der Ton beginnt.")]
        [Output("Ton gestartet")] [NonSerialized] public Output started;
        [Hint("Feuert, sobald die Quelle nicht mehr spielt: am Ende des Clips, aber auch nach Stopp oder Pause an der Quelle. Spielt dieser Knoten neu, kommt es für den alten Ton nicht. Ein Loop endet nicht von selbst.")]
        [Output("Ton fertig")] [NonSerialized] public Output finished;

        Coroutine work;

        [Hint("Spielt den Clip von vorn. Läuft er noch, wird er abgeschnitten.")]
        [Input("Abspielen")]
        public void Play(Signal s)
        {
            if (clip != null) source.clip = clip;
            if (source.clip == null) { Fail("kein Clip: weder am Knoten noch an der Quelle"); return; }
            if (work != null) StopWork(work);         // der alte Ton wird gleich abgeschnitten
            source.Play();
            started.Fire(s);
            work = StartWork(WaitUntilSilent(s));
        }

        IEnumerator WaitUntilSilent(Signal s)
        {
            yield return null;                         // Play() startet erst im nächsten Audio-Update
            while (source != null && source.isPlaying)
            {
                Running($"spielt: {source.time:0.0} / {source.clip.length:0.0} s", source.time / source.clip.length);
                yield return null;
            }
            work = null;
            if (source == null) { Fail("◆ Quelle wurde gelöscht, während der Ton lief"); yield break; }
            Done("✓ Ton fertig");
            finished.Fire(s);
        }

        public override void Validate(System.Collections.Generic.List<string> problems)
        {
            if (source != null && source.loop) problems.Add($"„{source.name}“ steht auf Loop: „Ton fertig“ kommt nie");
            if (source != null && clip == null && source.clip == null) problems.Add($"„{source.name}“ hat keinen Clip");
        }

        public override void OnStop() { work = null; }
    }
}
