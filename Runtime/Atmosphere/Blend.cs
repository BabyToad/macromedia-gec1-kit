using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kit
{
    /// <summary>
    /// Shared plumbing for everything in Atmosphäre that changes a value over time.
    /// Two rules hold for every node and zone: <b>the newest change wins</b> (a light that two nodes fade at once
    /// follows the later one; the earlier one reports "abgelöst"), and <b>Zurück means "as at the start"</b> (the
    /// value from before the kit first touched it in this Play session).
    /// </summary>
    public static class Blend
    {
        // who is changing what right now: target → (owner, ticket)
        static readonly Dictionary<object, (object owner, int ticket)> s_Claims = new Dictionary<object, (object, int)>();
        static readonly Dictionary<object, object> s_Originals = new Dictionary<object, object>();
        static int s_Ticket;

        /// <summary>Starts a change of <paramref name="target"/> by <paramref name="owner"/>; an older change of it ends.</summary>
        public static int Claim(object target, object owner)
        {
            s_Claims[target] = (owner, ++s_Ticket);
            return s_Ticket;
        }

        /// <summary>Is this still the newest change of the target?</summary>
        public static bool Holds(object target, int ticket) =>
            s_Claims.TryGetValue(target, out var c) && c.ticket == ticket;

        /// <summary>Lets go of a target (only if this change still holds it).</summary>
        public static void Let(object target, int ticket)
        {
            if (Holds(target, ticket)) s_Claims.Remove(target);
        }

        /// <summary>The value before the kit first changed this target in this Play session; <paramref name="now"/> on first call.</summary>
        public static T Original<T>(object target, Func<T> now)
        {
            if (s_Originals.TryGetValue(target, out var v) && v is T t) return t;
            var first = now();
            s_Originals[target] = first;
            return first;
        }

        /// <summary>True if the kit already remembers how this target looked at the start.</summary>
        public static bool HasOriginal(object target) => s_Originals.ContainsKey(target);

        /// <summary>
        /// Runs <paramref name="apply"/> with eased progress 0..1 over <paramref name="seconds"/>, then once with 1.
        /// Stops early (without the final 1) when the claim is lost or a Unity target is destroyed.
        /// </summary>
        public static IEnumerator Run(float seconds, Ease ease, bool realTime, Action<float> apply,
                                      object target = null, int ticket = 0, Action<float> progress = null)
        {
            float d = Easing.Duration(ease, seconds);
            float start = realTime ? Time.unscaledTime : Time.time;
            while (d > 0f)
            {
                if (BlendRunner.FinishNow) break;                 // nothing will tick any more (Edit Mode, Play ending): jump to the end
                if (Lost(target, ticket)) yield break;
                float t = ((realTime ? Time.unscaledTime : Time.time) - start) / d;
                if (t >= 1f) break;
                apply(Easing.Evaluate(ease, t));
                progress?.Invoke(t);
                yield return null;
            }
            if (Lost(target, ticket)) yield break;
            apply(1f);
        }

        static bool Lost(object target, int ticket)
        {
            if (target == null) return false;
            if (target is Object o && !o) return true;
            return !Holds(target, ticket);
        }

        /// <summary>Colour lerp that may overshoot (for Ease.Overshoot) but never goes below 0.</summary>
        public static Color Lerp(Color a, Color b, float k)
        {
            var c = Color.LerpUnclamped(a, b, k);
            return new Color(Mathf.Max(0, c.r), Mathf.Max(0, c.g), Mathf.Max(0, c.b), Mathf.Clamp01(c.a));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void NewSession()
        {
            s_Claims.Clear(); s_Originals.Clear();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= SceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += SceneLoaded;
        }

        // A new scene brings its own fog: "as at the start" now means this scene's start.
        static void SceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (mode != UnityEngine.SceneManagement.LoadSceneMode.Single) return;
            s_Originals.Remove(Fog); s_Claims.Remove(Fog);
        }

        /// <summary>Key for scene-wide settings (fog, ambient), which have no Unity object of their own.</summary>
        public static readonly object Fog = new object();
    }
}
