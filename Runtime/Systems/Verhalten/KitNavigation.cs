using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Kit
{
    /// <summary>
    /// Bakes the NavMesh by itself, at runtime, from every collider in the scene, and rebakes it when the level
    /// changes (a door opens, a wall is switched off). Baking teaches nothing, so students never do it.
    /// Figures (<see cref="NpcBehaviour"/>) and <see cref="WalkTo"/> call <see cref="Ensure"/> before they walk.
    /// A scene that brings its own navigation gets the floor to itself: a NavMesh that was already there when the
    /// kit started (an old-style baked scene), or an active NavMeshSurface of the AI Navigation package, whenever
    /// it arrives (scene load, Start, a late bake). The kit then takes its own mesh out and stops baking.
    /// </summary>
    public static class KitNavigation
    {
        /// <summary>How often the watcher looks for changed geometry (seconds, real time).</summary>
        public const float WatchInterval = 0.5f;
        const float Margin = 4f;   // extra space around the level, so figures may walk to its edge
        const float ForeignCheckInterval = 2f;

        static NavMeshData s_Data;
        static NavMeshDataInstance s_Instance;
        static int s_BuiltSignature;
        static Bounds s_Bounds;
        static AsyncOperation s_Pending;
        static int s_PendingSignature;
        static int s_Generation;   // bumped by every build, cancel and reset: an old async completion is ignored
        static bool s_Foreign, s_ForeignChecked;
        static float s_NextForeignCheck;

        /// <summary>
        /// Is other navigation active in the world (besides the kit's)? Default: an AI Navigation NavMeshSurface
        /// with baked data. Tests replace it. Independent of triangle counts, so a rebuild in flight cannot hide it.
        /// </summary>
        public static Func<bool> ForeignProbe = SurfacePresent;
        static KitNavigationWatcher s_Watcher;
        static readonly List<NavMeshBuildSource> s_Sources = new List<NavMeshBuildSource>();
        static readonly List<NavMeshBuildSource> s_Collected = new List<NavMeshBuildSource>();
        static readonly List<NavMeshBuildMarkup> s_NoMarkups = new List<NavMeshBuildMarkup>();

        /// <summary>How many times the NavMesh was built since the start of Play (for tests and the Inspector).</summary>
        public static int BuildCount { get; private set; }
        /// <summary>True if the scene brought its own NavMesh, so the kit does not bake.</summary>
        public static bool UsesForeignNavMesh => s_Foreign;
        /// <summary>True while an asynchronous rebuild is running.</summary>
        public static bool RebuildPending => s_Pending != null;
        /// <summary>True while the kit's own NavMesh is in the world.</summary>
        public static bool HasKitNavMesh => s_Instance.valid;

        /// <summary>One line for the Inspector: what the navigation is doing.</summary>
        public static string Describe()
        {
            if (!Application.isPlaying) return "Wird beim Start automatisch gebacken.";
            if (s_Foreign) return "Die Szene hat ein eigenes NavMesh; das Kit backt nicht.";
            if (!s_Instance.valid) return "Noch nicht gebacken.";
            return s_Pending != null ? "Wird gerade neu gebacken …" : $"Automatisch gebacken ({BuildCount}×).";
        }

        /// <summary>
        /// Makes sure a NavMesh exists that matches the level right now. Builds synchronously if needed
        /// (one short hitch), so a figure placed this frame can walk at once.
        /// </summary>
        public static void Ensure()
        {
            if (!Application.isPlaying) return;
            StartWatcher();
            if (!s_ForeignChecked) CheckForeign();
            if (s_Foreign) return;
            int sig = Collect();
            if (s_Instance.valid && sig == s_BuiltSignature) return;
            if (s_Pending != null && sig == s_PendingSignature) return;   // the running rebuild already covers it
            BuildNow(sig);
        }

        /// <summary>
        /// Asks for a rebuild on the next watcher tick. Needed only for changes the kit cannot see: a mesh or
        /// terrain edited in place by a script. Moved, added, removed or switched objects are noticed by themselves.
        /// </summary>
        public static void Invalidate()
        {
            CancelPending();   // a rebuild already running may predate the edit: its result must not count as current
            s_BuiltSignature = 0;
        }

        /// <summary>Takes the kit NavMesh out of the world (tests; the next <see cref="Ensure"/> bakes again).</summary>
        public static void Clear()
        {
            CancelPending();
            if (s_Instance.valid) s_Instance.Remove();
            s_Instance = default;
            s_BuiltSignature = 0;
            s_ForeignChecked = false;
            s_Foreign = false;
        }

        /// <summary>
        /// Is there navigation in the world besides the kit's? Then the kit takes its mesh out and stops baking.
        /// Changes nothing otherwise (an empty additive scene leaves every walking figure alone). Runs on scene
        /// load and every few seconds from the watcher; public for tests.
        /// </summary>
        public static void Reconcile()
        {
            if (!Application.isPlaying) return;
            if (!s_Instance.valid) { s_ForeignChecked = false; return; }   // checked on the next Ensure/Watch
            if (!ForeignProbe()) return;
            CancelPending();
            s_Instance.Remove();
            s_Instance = default;
            s_ForeignChecked = true;
            s_Foreign = true;
            Debug.Log(KitBrand.Log + "Die Szene bringt ein eigenes NavMesh mit: das Kit backt ab jetzt nicht mehr selbst.");
            WarpAllAgents();   // onto the scene's own mesh
        }

        // AI Navigation is an optional package: found by name, read without allocating (its static list).
        static Type s_SurfaceType;
        static System.Reflection.PropertyInfo s_ActiveSurfaces, s_SurfaceData;
        static bool s_SurfaceLookedUp;

        static bool SurfacePresent()
        {
            if (!s_SurfaceLookedUp)
            {
                s_SurfaceLookedUp = true;
                s_SurfaceType = Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
                s_ActiveSurfaces = s_SurfaceType?.GetProperty("activeSurfaces", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                s_SurfaceData = s_SurfaceType?.GetProperty("navMeshData");
            }
            if (s_ActiveSurfaces == null || !(s_ActiveSurfaces.GetValue(null) is System.Collections.IList list)) return false;
            foreach (var surface in list)
                if (surface is Behaviour b && b && b.isActiveAndEnabled && s_SurfaceData?.GetValue(surface) is NavMeshData d && d != null) return true;
            return false;
        }

        // ---------------------------------------------------------------------------------- building
        static void CheckForeign()
        {
            s_ForeignChecked = true;
            if (s_Instance.valid) { s_Foreign = ForeignProbe(); return; }
            var tri = NavMesh.CalculateTriangulation();   // before the kit's own: anything here is someone else's
            s_Foreign = tri.indices != null && tri.indices.Length > 0 || ForeignProbe();
        }

        static void CancelPending()
        {
            s_Generation++;
            if (s_Pending != null && s_Data != null) NavMeshBuilder.Cancel(s_Data);
            s_Pending = null;
        }

        static void BuildNow(int sig)
        {
            CancelPending();
            var settings = NavMesh.GetSettingsByID(0);
            if (s_Data == null)
            {
                s_Data = NavMeshBuilder.BuildNavMeshData(settings, s_Sources, s_Bounds, Vector3.zero, Quaternion.identity);
                s_Data.name = "Kit-NavMesh";
            }
            else NavMeshBuilder.UpdateNavMeshData(s_Data, settings, s_Sources, s_Bounds);
            if (!s_Instance.valid) s_Instance = NavMesh.AddNavMeshData(s_Data);
            s_BuiltSignature = sig;
            BuildCount++;
            Warp();
        }

        static void BuildAsync(int sig)
        {
            if (s_Data == null) { BuildNow(sig); return; }
            int generation = ++s_Generation;
            s_PendingSignature = sig;
            var data = s_Data;
            var op = NavMeshBuilder.UpdateNavMeshDataAsync(data, NavMesh.GetSettingsByID(0), s_Sources, s_Bounds);
            s_Pending = op;
            op.completed += _ =>
            {
                // Cancelled, cleared, replaced or overtaken by a synchronous build meanwhile: not ours to publish.
                if (generation != s_Generation || op != s_Pending || data != s_Data) return;
                s_Pending = null;
                if (!s_Instance.valid) return;
                s_BuiltSignature = sig;
                BuildCount++;
                Warp();
            };
        }

        /// <summary>Figures that stood on no NavMesh (placed before the bake) are put onto it.</summary>
        static void Warp()
        {
            foreach (var b in Object.FindObjectsByType<NpcBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                b.SnapToNavMesh();
        }

        /// <summary>Every agent off the NavMesh (figures and plain agents of „Gehen zu“) is put onto the nearest one.</summary>
        static void WarpAllAgents()
        {
            foreach (var a in Object.FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (a.enabled && !a.isOnNavMesh && NavMesh.SamplePosition(a.transform.position, out var hit, 3f, NavMesh.AllAreas))
                    a.Warp(hit.position);
        }

        /// <summary>
        /// Collects what the figures can walk on: every solid collider, except moving things (figures, the player,
        /// bodies pushed by physics). Returns a signature of the result, so a change in the level can be detected:
        /// which colliders, where, what shape, which mesh or terrain.
        /// </summary>
        static int Collect()
        {
            s_Sources.Clear();
            Physics.SyncTransforms();   // moved walls count now, not at the next physics step
            var all = s_Collected; all.Clear();
            var huge = new Bounds(Vector3.zero, Vector3.one * 100000f);
            NavMeshBuilder.CollectSources(huge, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, s_NoMarkups, all);
            bool any = false;
            var bounds = new Bounds();
            unchecked
            {
                int sig = 17;
                foreach (var s in all)
                {
                    var c = s.component as Collider;
                    if (c == null || c.isTrigger || IsMover(c)) continue;
                    s_Sources.Add(s);
                    if (!any) { bounds = c.bounds; any = true; } else bounds.Encapsulate(c.bounds);
                    sig = sig * 31 + c.GetInstanceID();
                    sig = sig * 31 + s.transform.GetHashCode();
                    sig = sig * 31 + s.size.GetHashCode();
                    sig = sig * 31 + (int)s.shape;
                    sig = sig * 31 + s.area;
                    var shape = s.sourceObject;
                    if (shape != null)
                    {
                        sig = sig * 31 + shape.GetInstanceID();   // a swapped mesh or terrain
                        if (shape is Mesh m) sig = sig * 31 + m.vertexCount;
                    }
                }
                if (!any) bounds = new Bounds(Vector3.zero, Vector3.one);
                bounds.Expand(Margin * 2f);
                // Grow, never shrink, so a rebuild after a small change keeps the same tiles.
                if (s_Instance.valid) { s_Bounds.Encapsulate(bounds); }
                else s_Bounds = bounds;
                return sig == 0 ? 1 : sig;
            }
        }

        static bool IsMover(Collider c)
        {
            if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) return true;
            if (c is CharacterController) return true;
            if (c.GetComponentInParent<NavMeshAgent>() != null) return true;
            if (c.GetComponentInParent<NpcBehaviour>() != null) return true;
            if (c.GetComponentInParent<KitPlayer>() != null) return true;
            if (c.transform.root.CompareTag("Player")) return true;
            return false;
        }

        // ---------------------------------------------------------------------------------- watching
        static void StartWatcher()
        {
            if (s_Watcher != null) return;
            var go = new GameObject("Kit-Navigation") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            s_Watcher = go.AddComponent<KitNavigationWatcher>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Enter Play Mode without domain reload keeps statics; start every Play session clean.
            CancelPending();
            if (s_Instance.valid) s_Instance.Remove();
            s_Instance = default; s_Data = null; s_Watcher = null;
            s_BuiltSignature = 0; s_Foreign = false; s_ForeignChecked = false; BuildCount = 0;
            s_NextForeignCheck = 0f; ForeignProbe = SurfacePresent;
            SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
            SceneManager.sceneUnloaded -= Unloaded; SceneManager.sceneUnloaded += Unloaded;
        }

        static void Loaded(Scene s, LoadSceneMode m) => Reconcile();
        // A scene with its own NavMesh is gone: the next Ensure/Watch looks again and bakes if nothing is left.
        static void Unloaded(Scene s) { if (s_Foreign) s_ForeignChecked = false; }

        /// <summary>Every <see cref="WatchInterval"/>: rebuild if the level changed (called by the hidden watcher).</summary>
        internal static void Watch()
        {
            if (!s_ForeignChecked) CheckForeign();
            if (s_Foreign || s_Pending != null) return;
            if (s_Instance.valid && Time.unscaledTime >= s_NextForeignCheck)
            {
                s_NextForeignCheck = Time.unscaledTime + ForeignCheckInterval;
                Reconcile();
                if (s_Foreign) return;
            }
            // Nothing to keep current while no figure uses the NavMesh.
            if (!s_Instance.valid && Object.FindAnyObjectByType<NpcBehaviour>() == null) return;
            int sig = Collect();
            if (!s_Instance.valid) BuildNow(sig);
            else if (sig != s_BuiltSignature) BuildAsync(sig);
        }

        internal static void WatcherGone(KitNavigationWatcher w) { if (s_Watcher == w) s_Watcher = null; }
    }
}
