using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kit
{
    /// <summary>
    /// A scene transition: covers the screen before the old scene goes and reveals the new one. The Atmosphäre
    /// lane's Blende implements it; without one the kit fades to black and back. Both run in real time
    /// (unscaled), so they also work while the game time stands still.
    /// </summary>
    public interface IKitSceneTransition
    {
        /// <summary>Ends when the screen is fully covered.</summary>
        IEnumerator Cover();
        /// <summary>Ends when the screen is visible again.</summary>
        IEnumerator Reveal();
        /// <summary>
        /// Something went wrong (Cover or Reveal threw, or the change was given up): make the game visible at once,
        /// no animation. Called at most once per scene change, and never instead of a successful Reveal.
        /// </summary>
        void Abort() { }
    }

    /// <summary>
    /// The whole game across scenes. Values with scope Spiel live as long as the game runs (also across scene
    /// loads); <see cref="Reset"/> puts them back to their start values, e.g. for "Neues Spiel".
    /// </summary>
    public static class KitGame
    {
        /// <summary>Called by <see cref="Reset"/>: everything with scope Spiel goes back to its start value.</summary>
        public static event Action Resetting;

        /// <summary>How often the game was reset in this Play session (for traces and tests).</summary>
        public static int ResetCount { get; private set; }

        public static void Reset()
        {
            ResetCount++;
            EachOf(Resetting, "Spiel zurücksetzen");
        }

        /// <summary>Called by <see cref="RestoreSceneEntry"/>: Spiel values go back to what they were when the scene was entered.</summary>
        public static event Action RestoringSceneEntry;

        /// <summary>Retry: Spiel values as when the current scene was entered (coins from the failed attempt are gone).</summary>
        public static void RestoreSceneEntry() => EachOf(RestoringSceneEntry, "Spiel-Werte zurückholen");

        // Every store gets its call, even if one before it throws: a half-reset game is worse than a logged error.
        static void EachOf(Action ev, string what)
        {
            if (ev == null) return;
            foreach (Action h in ev.GetInvocationList())
            {
                try { h(); }
                catch (Exception e) { Debug.LogError(KitBrand.Log + $"{what}: Fehler in {h.Method.DeclaringType?.Name}.{h.Method.Name}: {e.Message}"); Debug.LogException(e); }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void NewSession() => ResetCount = 0;   // subscribers stay: they belong to their assemblies, not to a Play session
    }

    /// <summary>
    /// Scene changes for the kit: load a scene (optionally arriving at a named Eingang), restart the current one,
    /// go to the next one in the Build Settings order. One change at a time; each one covers the screen, holds the
    /// game time while the new scene loads and reveals it again.
    /// </summary>
    public static class KitScenes
    {
        /// <summary>The transition in use. Null = the kit's own black fade.</summary>
        public static IKitSceneTransition Transition { get; set; }

        /// <summary>Seconds each way for the kit's own fade (when no <see cref="Transition"/> is set).</summary>
        public static float FallbackSeconds = 0.3f;

        /// <summary>True from the request until the new scene is visible again.</summary>
        public static bool IsLoading { get; private set; }

        /// <summary>Name of the scene the player came from with the last kit scene change ("" at the start).</summary>
        public static string CameFrom { get; private set; } = "";

        /// <summary>Eingang the last scene change asked for ("" = none named).</summary>
        public static string Entrance { get; private set; } = "";

        /// <summary>The scene the player is in (path).</summary>
        public static string CurrentPath => SceneManager.GetActiveScene().path;

        /// <summary>Old scene name, before it is unloaded (after the screen is covered).</summary>
        public static event Action<string> Leaving;

        /// <summary>New scene name, after the player is placed and before the screen is revealed.</summary>
        public static event Action<string> Arrived;

        /// <summary>
        /// Places the player after a load. Set by Kit.Systems (which knows the player and the Eingänge).
        /// Arguments: Eingang name ("" = none), name of the scene the player came from. Returns a German line for
        /// the trace, e.g. "am Eingang „Tor“".
        /// </summary>
        public static Func<string, string, string> PlacePlayer;

        /// <summary>True if the current scene was entered by a kit scene change (false for the scene Play started in).</summary>
        public static bool EnteredByKit { get; private set; }

        /// <summary>The line <see cref="PlacePlayer"/> returned for the last arrival.</summary>
        public static string LastPlacement { get; private set; } = "";

        // ---------------------------------------------------------------------------------- requests
        /// <summary>
        /// Loads a scene by path. Returns null when it starts, or the German reason why not.
        /// <paramref name="entrance"/>: Eingang to arrive at ("" = the default rule, see KitEntrance).
        /// </summary>
        public static string Load(string scenePath, string entrance = "", bool resetGame = false)
        {
            if (string.IsNullOrEmpty(scenePath)) return "keine Szene eingetragen";
            if (IsLoading) return "es wird schon eine Szene geladen";
            if (!InBuild(scenePath)) return $"Szene „{SceneRef.NameOf(scenePath)}“ steht nicht in den Build Settings";
            Begin(scenePath, entrance ?? "", SceneManager.GetActiveScene().name, resetGame, false, place: true);
            return null;
        }

        /// <summary>Loads the current scene again, arriving the way the player arrived last time.</summary>
        public static string Restart(bool resetGame = false, bool restoreEntry = true)
        {
            var path = CurrentPath;
            if (string.IsNullOrEmpty(path)) return "die aktuelle Szene ist nicht gespeichert";
            if (IsLoading) return "es wird schon eine Szene geladen";
            if (!InBuild(path)) return $"Szene „{SceneRef.NameOf(path)}“ steht nicht in den Build Settings";
            // Same arrival as before: same Eingang, same "came from"; a scene Play started in keeps its own start point.
            Begin(path, Entrance, CameFrom, resetGame, restoreEntry && !resetGame, place: EnteredByKit);
            return null;
        }

        /// <summary>Goes to the scene after the current one in the Build Settings order.</summary>
        public static string Next()
        {
            if (IsLoading) return "es wird schon eine Szene geladen";
            var path = NextPath(out var reason);
            if (path == null) return reason;
            Begin(path, "", SceneManager.GetActiveScene().name, false, false, place: true);
            return null;
        }

        /// <summary>Path of the scene after the current one, or null and the reason.</summary>
        public static string NextPath(out string reason)
        {
            reason = null;
            int i = SceneManager.GetActiveScene().buildIndex;
            if (i < 0) { reason = "die aktuelle Szene steht nicht in den Build Settings, also gibt es keine nächste"; return null; }
            if (i + 1 >= SceneManager.sceneCountInBuildSettings) { reason = "das ist die letzte Szene in den Build Settings"; return null; }
            return SceneUtility.GetScenePathByBuildIndex(i + 1);
        }

        /// <summary>True if the scene can be loaded (it is in the Build Settings and switched on).</summary>
        public static bool InBuild(string scenePath) => SceneUtility.GetBuildIndexByScenePath(scenePath) >= 0;

        // ---------------------------------------------------------------------------------- running
        static Host s_Host;
        static int s_Generation;            // one per scene change; an arrival only counts for its own change
        static AsyncOperation s_InFlight;   // a load Unity is still doing, even if its host is gone
        static bool s_Running;              // a Run coroutine is between its start and its finally

        static void Begin(string path, string entrance, string cameFrom, bool resetGame, bool restoreEntry, bool place)
        {
            IsLoading = true;
            s_Running = true;
            if (!s_Host)
            {
                var go = new GameObject(KitBrand.Log + "Szenenwechsel");
                UnityEngine.Object.DontDestroyOnLoad(go);
                s_Host = go.AddComponent<Host>();
            }
            var run = Run(++s_Generation, path, entrance, cameFrom, resetGame, restoreEntry, place);
            s_Host.Current = run;                              // so a destroyed host can still clean up (see Host.OnDestroy)
            s_Host.StartCoroutine(run);
        }

        sealed class Outcome { public bool Failed; }

        static IEnumerator Run(int generation, string path, string entrance, string cameFrom, bool resetGame, bool restoreEntry, bool place)
        {
            var host = s_Host;
            var transition = Alive(Transition);                // the one at the start; a new scene may bring another
            IKitSceneTransition reveal = null;
            IEnumerator step = null;                           // the transition step running now (disposed if we are cut off)
            bool done = false, arrived = false;

            // Placing happens when the new scene is loaded: after its Awake/OnEnable, before any Start or Update, so
            // nothing in the new scene ever sees the player at his old spot.
            void Arrive()
            {
                if (arrived) return;
                arrived = true;
                LastPlacement = "";
                EnteredByKit = place;
                if (place && PlacePlayer != null) Guard(() => LastPlacement = PlacePlayer(Entrance, CameFrom) ?? "", "Spieler platzieren");
                Notify(Arrived, SceneManager.GetActiveScene().name);
            }
            // Only the scene this change asked for, only while this change is the current one and its host lives.
            void OnLoaded(Scene scene, LoadSceneMode mode)
            {
                if (scene.path != path) return;
                SceneManager.sceneLoaded -= OnLoaded;
                if (host && generation == s_Generation) Arrive();
            }

            try
            {
                var cover = new Outcome();
                step = Safe(() => transition != null ? transition.Cover() : host.Fade(1f), "Blende (zudecken)", cover);
                while (step.MoveNext()) yield return step.Current;
                step = null;
                if (cover.Failed) Abort(transition, host);     // a broken Blende: go on without it, nothing stays half covered
                KitTime.Hold(host, 0f);                        // nothing happens behind the curtain
                Notify(Leaving, SceneManager.GetActiveScene().name);
                if (resetGame) KitGame.Reset();
                else if (restoreEntry) KitGame.RestoreSceneEntry();
                CameFrom = cameFrom ?? "";
                Entrance = entrance ?? "";
                SceneManager.sceneLoaded += OnLoaded;
                AsyncOperation op = null;
                Guard(() => op = SceneManager.LoadSceneAsync(path, LoadSceneMode.Single), "Laden");
                if (op == null) Debug.LogError(KitBrand.Log + $"Szene „{SceneRef.NameOf(path)}“ konnte nicht geladen werden.");
                else
                {
                    s_InFlight = op;
                    op.completed += Finished;
                    while (!op.isDone) yield return null;
                }
                SceneManager.sceneLoaded -= OnLoaded;
                if (op != null) Arrive();                      // in case the callback was missed
                KitTime.Release(host);

                // A Blende in the new scene may have taken over; reveal with whatever is there now.
                reveal = cover.Failed ? null : Alive(Transition) ?? Alive(transition);
                if (reveal == null && transition != null && !cover.Failed) host.Snap(1f);   // the Blende left with the old scene: go on from black
                if (reveal != null) host.Snap(0f);                                           // the kit's fade never stays over someone else's
                var shown = new Outcome();
                step = Safe(() => reveal != null ? reveal.Reveal() : host.Fade(0f), "Blende (aufdecken)", shown);
                while (step.MoveNext()) yield return step.Current;
                step = null;
                if (shown.Failed) Abort(reveal, host);
                done = true;
            }
            finally
            {
                // Also when something above threw, or the host was destroyed: the game must never stay covered, held or
                // "loading". A step cut off in the middle is disposed first, so the transition's own finally blocks run.
                if (step is IDisposable cut) { try { cut.Dispose(); } catch (Exception e) { Failed("Blende, Aufräumen", e); } }
                SceneManager.sceneLoaded -= OnLoaded;
                s_Running = false;
                if (!done) { Abort(reveal ?? transition, host); if (reveal != null && transition != reveal) Abort(transition, host); }
                if (host) KitTime.Release(host);
                if (s_InFlight == null || s_InFlight.isDone) { s_InFlight = null; IsLoading = false; }   // else Finished clears it
            }
        }

        // A load Unity finished. If its change was abandoned (host destroyed), nothing else will say "not loading".
        static void Finished(AsyncOperation op)
        {
            if (s_InFlight != op) return;
            s_InFlight = null;
            if (!s_Host || !s_Running) IsLoading = false;
        }

        // Make the game visible at once: the kit's own fade, and the transition's Abort.
        static void Abort(IKitSceneTransition t, Host host)
        {
            if (host) host.Snap(0f);
            t = Alive(t);
            if (t != null) Guard(t.Abort, "Blende (abbrechen)");
        }

        // A transition that is a destroyed component counts as none.
        static IKitSceneTransition Alive(IKitSceneTransition t) => t is UnityEngine.Object o && !o ? null : t;

        /// <summary>
        /// Runs a transition step so that no exception in it can stop the scene change: neither in creating the
        /// step, nor in its own code, nor in a step it yields (nested IEnumerators are run here, not by Unity). On a
        /// failure the steps still suspended are disposed, innermost first, so their finally blocks run.
        /// </summary>
        static IEnumerator Safe(Func<IEnumerator> make, string what, Outcome outcome)
        {
            IEnumerator step;
            try { step = make(); }
            catch (Exception e) { Failed(what, e); outcome.Failed = true; yield break; }
            var stack = new Stack<IEnumerator>();
            if (step != null) stack.Push(step);
            try
            {
                while (stack.Count > 0)
                {
                    object current;
                    try
                    {
                        if (!stack.Peek().MoveNext()) { Dispose(stack.Pop(), what); continue; }
                        current = stack.Peek().Current;
                    }
                    catch (Exception e) { Failed(what, e); outcome.Failed = true; yield break; }
                    if (current is IEnumerator nested) { stack.Push(nested); continue; }
                    yield return current;
                }
            }
            finally
            {
                while (stack.Count > 0) Dispose(stack.Pop(), what);
            }
        }

        static void Dispose(IEnumerator e, string what)
        {
            if (!(e is IDisposable d)) return;
            try { d.Dispose(); }
            catch (Exception ex) { Failed(what + ", Aufräumen", ex); }
        }

        static void Failed(string what, Exception e)
        {
            Debug.LogError(KitBrand.Log + $"Szenenwechsel: Fehler in {what}: {e.Message}");
            Debug.LogException(e);
        }

        static void Guard(Action a, string what)
        {
            try { a(); }
            catch (Exception e) { Debug.LogError(KitBrand.Log + $"Szenenwechsel: Fehler beim {what}: {e.Message}"); Debug.LogException(e); }
        }

        static void Notify(Action<string> ev, string scene)
        {
            if (ev == null) return;
            foreach (Action<string> h in ev.GetInvocationList()) Guard(() => h(scene), "Melden an " + h.Method.Name);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void NewSession()
        {
            IsLoading = false;
            CameFrom = Entrance = LastPlacement = "";
            EnteredByKit = false;
            s_Host = null;
            s_InFlight = null;
            s_Running = false;
            Transition = null;
        }

        /// <summary>Runs the scene changes across loads and draws the kit's own fade.</summary>
        [AddComponentMenu("")]
        sealed class Host : MonoBehaviour
        {
            float m_Alpha;
            static Texture2D s_Black;

            public IEnumerator Fade(float to)
            {
                float from = m_Alpha, t = 0f, d = Mathf.Max(0f, FallbackSeconds);
                while (t < d)
                {
                    t += Time.unscaledDeltaTime;
                    m_Alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / d));
                    yield return null;
                }
                m_Alpha = to;
            }

            public void Snap(float alpha) => m_Alpha = alpha;

            void OnGUI()
            {
                if (m_Alpha <= 0f) return;
                if (!s_Black) { s_Black = new Texture2D(1, 1); s_Black.SetPixel(0, 0, Color.black); s_Black.Apply(); }
                GUI.depth = -10000;
                var c = GUI.color; GUI.color = new Color(1, 1, 1, m_Alpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), s_Black);
                GUI.color = c;
            }

            // Destroyed mid-change (Play stopped, a script destroying everything): let go of the time. A load Unity has
            // already started still finishes; until then nothing else may start (Finished clears IsLoading).
            /// <summary>The scene change this host runs (null when idle).</summary>
            public IEnumerator Current;

            void OnDestroy()
            {
                // Unity stops the coroutine without running its finally blocks: dispose it, which runs them (abort the
                // transition, dispose its steps, unsubscribe, release the time).
                var run = Current;
                Current = null;
                if (run is IDisposable d) { try { d.Dispose(); } catch (Exception e) { Failed("Szenenwechsel, Aufräumen", e); } }
                KitTime.Release(this);
                if (s_Host != this) return;
                s_Host = null;
                s_Running = false;
                if (s_InFlight == null || s_InFlight.isDone) { s_InFlight = null; IsLoading = false; }
            }
        }
    }
}
