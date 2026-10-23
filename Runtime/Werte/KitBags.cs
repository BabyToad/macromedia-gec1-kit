using System;
using System.Collections.Generic;

namespace Kit
{
    /// <summary>
    /// The contents of one Inventar: how many of each item. Like <see cref="KitValue"/>, whoever holds the same
    /// bag sees the same contents; Spiel-scope bags survive scene loads.
    /// </summary>
    public sealed class KitBag
    {
        public readonly string Name;
        public readonly Scope Scope;
        readonly Dictionary<string, int> m_Counts = new Dictionary<string, int>();
        Dictionary<string, int> m_Start;   // what it held when it was made (Neues Spiel goes back to this)

        /// <summary>(item, new count) after every change.</summary>
        public event Action<string, int> Changed;

        internal KitBag(string name, Scope scope) { Name = name; Scope = scope; }

        public int Count(string item) => item != null && m_Counts.TryGetValue(item, out var n) ? n : 0;
        public IEnumerable<KeyValuePair<string, int>> All => m_Counts;

        public void Add(string item, int n)
        {
            Check(item, n, "Add");
            m_Counts[item] = Count(item) + n;
            Changed?.Invoke(item, m_Counts[item]);
        }

        /// <summary>Takes up to n; returns how many were really there.</summary>
        public int Remove(string item, int n)
        {
            Check(item, n, "Remove");
            int taken = Math.Min(n, Count(item));
            if (taken == 0) return 0;
            m_Counts[item] = Count(item) - taken;
            Changed?.Invoke(item, m_Counts[item]);
            return taken;
        }

        /// <summary>Replaces the contents (start contents, a restored snapshot).</summary>
        public void Fill(IEnumerable<KeyValuePair<string, int>> contents)
        {
            m_Counts.Clear();
            foreach (var kv in contents) if (!string.IsNullOrEmpty(kv.Key) && kv.Value > 0) m_Counts[kv.Key] = Count(kv.Key) + kv.Value;
        }

        /// <summary>The contents it starts with (made by an Inventar component with its start list).</summary>
        public void SetStart(IEnumerable<KeyValuePair<string, int>> contents)
        {
            Fill(contents);
            m_Start = Copy();
        }

        internal void ResetToStart()
        {
            if (m_Start != null) Fill(m_Start); else m_Counts.Clear();
        }

        internal Dictionary<string, int> Copy() => new Dictionary<string, int>(m_Counts);

        /// <summary>"Schlüssel ×1, Münze ×3" or "leer".</summary>
        public string Describe()
        {
            var parts = new List<string>();
            foreach (var kv in m_Counts) if (kv.Value > 0) parts.Add($"{kv.Key} ×{kv.Value}");
            return parts.Count > 0 ? string.Join(", ", parts) : "leer";
        }

        // Negative counts would create items on Remove and take them on Add.
        static void Check(string item, int n, string what)
        {
            if (string.IsNullOrEmpty(item)) throw new ArgumentException($"Inventar.{what}: kein Gegenstand angegeben", nameof(item));
            if (n < 1) throw new ArgumentOutOfRangeException(nameof(n), n, $"Inventar.{what}: die Anzahl muss mindestens 1 sein");
        }
    }

    /// <summary>Where Inventar contents live, by scope and name (the same rules as <see cref="KitValues"/>).</summary>
    public static class KitBags
    {
        static readonly Dictionary<Logic, Dictionary<string, KitBag>> s_Logik = new Dictionary<Logic, Dictionary<string, KitBag>>();
        static readonly Dictionary<int, Dictionary<string, KitBag>> s_Scene = new Dictionary<int, Dictionary<string, KitBag>>();
        static readonly Dictionary<string, KitBag> s_Game = new Dictionary<string, KitBag>();
        static Dictionary<string, Dictionary<string, int>> s_Entry;

        internal static void ResetOnPlay() { s_Logik.Clear(); s_Scene.Clear(); s_Game.Clear(); s_Entry = null; }
        internal static void SceneUnloaded(int handle) => s_Scene.Remove(handle);

        /// <summary>The bag; <paramref name="created"/> says whether it is new (and should get its start contents).</summary>
        public static KitBag Get(string name, Scope scope, Logic logik, UnityEngine.SceneManagement.Scene scene, out bool created)
        {
            created = false;
            Dictionary<string, KitBag> table;
            if (scope == Scope.Logik)
            {
                if (logik == null) throw new ArgumentNullException(nameof(logik));
                if (!s_Logik.TryGetValue(logik, out table)) s_Logik[logik] = table = new Dictionary<string, KitBag>();
            }
            else if (scope == Scope.Szene)
            {
                if (!s_Scene.TryGetValue(scene.handle, out table)) s_Scene[scene.handle] = table = new Dictionary<string, KitBag>();
            }
            else table = s_Game;
            if (table.TryGetValue(name, out var bag)) return bag;
            created = true;
            return table[name] = new KitBag(name, scope);
        }

        public static void ReleaseLogik(Logic logik) { if (logik != null) s_Logik.Remove(logik); }

        /// <summary>A bag owned by one object (Bereich Szene on the Inventar component): not shared by name.</summary>
        public static KitBag Own(string name) => new KitBag(name, Scope.Szene);

        internal static void MarkSceneEntry()
        {
            s_Entry = new Dictionary<string, Dictionary<string, int>>();
            foreach (var kv in s_Game) s_Entry[kv.Key] = kv.Value.Copy();
        }

        internal static void RestoreSceneEntry()
        {
            if (s_Entry == null) return;
            foreach (var kv in s_Game)
            {
                if (s_Entry.TryGetValue(kv.Key, out var old)) kv.Value.Fill(old);
                else kv.Value.ResetToStart();
            }
        }

        // Like Spiel values: never removed during a session, put back to their start, so every holder stays attached.
        internal static void ResetGame() { foreach (var b in s_Game.Values) b.ResetToStart(); s_Entry = null; }
    }
}
