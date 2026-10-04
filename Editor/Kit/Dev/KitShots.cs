using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Kit.Editor.Dev
{
    /// <summary>
    /// Development only: drives a GUI editor through the "Schlüssel und Tür" walkthrough and captures each
    /// Unity window (never the rest of the desktop) to Logs/KitShots. Launch:
    /// Unity.exe -projectPath kit -executeMethod Kit.Editor.Dev.KitShots.Run
    /// </summary>
    public static class KitShots
    {
        public static bool Verbose;
        static string Out => Path.GetFullPath("Logs/KitShots");
        static readonly List<(string name, Func<bool> step, double wait)> s_Steps = new List<(string, Func<bool>, double)>();
        static int s_Index, s_Ticks; static double s_At; static (bool, EnterPlayModeOptions) s_Prev;
        static EditorWindow s_Graph;

        static Interaction It => Object.FindAnyObjectByType<Interaction>();
        static KitPlayer Walker => Object.FindAnyObjectByType<KitPlayer>();
        static int Idx<T>() => It.Graph.nodes.FindIndex(n => n is T);

        public static void Run()
        {
            Verbose = true;
            Directory.CreateDirectory(Out);
            foreach (var f in Directory.GetFiles(Out, "*.png")) File.Delete(f);
            s_Prev = (EditorSettings.enterPlayModeOptionsEnabled, EditorSettings.enterPlayModeOptions);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            if (!File.Exists(KitDemo.ScenePath)) KitDemo.BuildAll();   // never rewrite the committed demo scene (Astra p2 MED 8)
            Steps();
            s_Index = 0; s_At = EditorApplication.timeSinceStartup + 8;   // let the editor layout come up first
            EditorApplication.update += Tick;
        }

        static void Steps()
        {
            s_Steps.Clear();
            void S(string n, Func<bool> f, double wait = 1.0) => s_Steps.Add((n, f, wait));
            S("scene", () => { EditorSceneManager.OpenScene(KitDemo.ScenePath); Selection.activeGameObject = It.gameObject; return true; }, 2);
            S("open", () =>
            {
                KitWindows.OpenGraph(It.Graph, It);
                s_Graph = KitGtkBridge.GraphWindows().FirstOrDefault();
                Debug.Log($"[KitShots] graph window: {(s_Graph ? s_Graph.titleContent.text : "none")}, windows: {Resources.FindObjectsOfTypeAll<EditorWindow>().Length}");
                if (!s_Graph) { s_Index--; return false; }      // try again next tick
                var main = EditorGUIUtility.GetMainWindowPosition();
                s_Graph.position = new Rect(main.x + 40, main.y + 80, 1500, 900);
                return true;
            }, 4);
            S("01-bearbeiten", Grab);
            S("doubleclick", () => DoubleClick("MoveNode"), 1);
            S("02-doppelklick-bewegen", Grab);
            S("select-key", () => { Selection.activeGameObject = GameObject.Find("Schlüssel"); return true; }, 1);
            S("03-auswahl-schluessel", Grab);
            S("code", () => { KitCodeView.Show(typeof(Move)); var w = EditorWindow.GetWindow<KitCodeView>(); w.position = new Rect(200, 120, 900, 820); return true; }, 1.5);
            S("04-code-ansehen", Grab);
            S("palette", () =>
            {
                var v = KitGtkBridge.ViewFor(s_Graph);
                var g = GraphOf();
                int before = g.GetNodes().Count();
                var n = v.CreateNode(typeof(HealthNode), KitGtkCommands.CanvasCentre(s_Graph) + new Vector2(-200, 250));
                var o = v.DropObject(GameObject.Find("Schild"), KitGtkCommands.CanvasCentre(s_Graph) + new Vector2(-450, 250));
                Debug.Log($"[KitShots] palette: {before} → {g.GetNodes().Count()} nodes, health={n != null}, drop={o != null}, available={KitGtkCommands.Available}");
                return true;
            }, 1.5);
            S("11-palette-und-hineinziehen", Grab);
            S("undo-create", () =>
            {
                Undo.PerformUndo(); Undo.PerformUndo(); Undo.PerformUndo();
                Debug.Log("[KitShots] undone 3 steps (see 12-nach-undo)");
                return true;
            }, 1.5);
            S("12-nach-undo", Grab);
            S("play", () => { EditorWindow.GetWindow<KitCodeView>().Close(); Selection.activeGameObject = null; EditorApplication.isPlaying = true; return true; }, 3);
            S("walk-in", () => { CloseTmpPrompt(); Walker.mouseSensitivity = 0; Walker.stickSensitivity = 0; return WalkTo(0.4f); }, 0.1);
            S("settle", () => true, 0.6);
            S("05-play-verschlossen", Grab);
            S("walk-out", () => WalkTo(-5f), 0.1);
            S("use-key", () => { It.FireOutput(Idx<Interact>(), "used"); return true; }, 0.6);
            S("06-play-schluessel-benutzt", Grab);
            S("walk-in-2", () => WalkTo(0.4f), 0.05);
            S("settle-2", () => true, 0.25);
            S("07-play-tuer-faehrt", Grab, 1.5);
            S("08-play-angekommen", Grab);
            S("exit", () => { EditorApplication.isPlaying = false; return true; }, 3);
            S("delete-door", () => { Undo.IncrementCurrentGroup(); Undo.DestroyObjectImmediate(GameObject.Find("Tür")); Selection.activeGameObject = It.gameObject; return true; }, 1.0);
            S("inspector", () => { EditorUtility.OpenPropertyEditor(It); return true; }, 0.5);
            S("inspector-pos", () => { var w = Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(x => x.GetType().Name.Contains("PropertyEditor")); if (w) w.position = new Rect(300, 150, 520, 760); return true; }, 1.5);
            S("09-tuer-geloescht-vor-play", Grab);
            S("play-2", () => { foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>().Where(x => x.GetType().Name.Contains("PropertyEditor"))) w.Close(); EditorApplication.isPlaying = true; return true; }, 3);
            S("use-key-2", () => { Walker.mouseSensitivity = 0; Walker.stickSensitivity = 0; It.FireOutput(Idx<Interact>(), "used"); return true; }, 0.3);
            S("walk-in-3", () => WalkTo(0.4f), 0.1);
            S("settle-3", () => true, 0.6);
            S("10-play-fehler", Grab);
            S("exit-2", () => { EditorApplication.isPlaying = false; return true; }, 3);
            S("undo", () => { Undo.PerformUndo(); return true; }, 1);   // never save here: the walkthrough deleted the Tür (Astra p2 MED 8)
            S("quit", () => { Finish(0); return true; });
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < s_At) return;
            if (EditorApplication.isPlaying != EditorApplication.isPlayingOrWillChangePlaymode) return;   // mid-transition
            if (s_Index >= s_Steps.Count) return;
            var (name, step, wait) = s_Steps[s_Index++];
            try { Debug.Log("[KitShots] " + name); step(); }
            catch (Exception e) { Debug.LogException(e); Finish(1); return; }
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            s_At = EditorApplication.timeSinceStartup + wait;
            if (++s_Ticks > 1500) Finish(2);   // safety: never hang the machine
        }

        static Unity.GraphToolkit.Editor.Graph GraphOf() =>
            Unity.GraphToolkit.Editor.GraphDatabase.LoadGraph<KitEditorGraph>(KitDemo.GraphPath);

        static bool DoubleClick(string nodeTypeName)
        {
            s_Graph = KitGtkBridge.GraphWindows().FirstOrDefault();
            VisualElement target = null;
            s_Graph.rootVisualElement.Query<VisualElement>().ForEach(ve =>
            {
                if (target == null && KitGtkBridge.Get(KitGtkBridge.ModelOf(ve), "Node")?.GetType().Name == nodeTypeName) target = ve;
            });
            if (target == null) { Debug.LogWarning("[KitShots] node view not found: " + nodeTypeName); return false; }
            var p = new Vector2(target.worldBound.center.x, target.worldBound.yMin + 12);
            var evt = new Event { type = EventType.MouseDown, button = 0, clickCount = 2, mousePosition = p };
            using (var e = MouseDownEvent.GetPooled(evt)) target.panel.visualTree.SendEvent(e);
            Debug.Log($"[KitShots] double-click → selection {Selection.activeGameObject?.name}");
            return true;
        }

        /// <summary>Walks the demo player along z until it reaches the target; repeats this step until then.</summary>
        static bool WalkTo(float z)
        {
            var w = Walker; float dz = z - w.transform.position.z;
            if (Mathf.Abs(dz) < 0.6f) { w.scripted = null; Debug.Log($"[KitShots] walker at {w.transform.position} t={Time.time}"); return true; }
            w.scripted = new KitPlayer.InputFrame { move = new Vector2(0, Mathf.Sign(dz)) };
            s_Index--;   // run this step again next tick
            return false;
        }

        static void CloseTmpPrompt()
        {
            foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>().Where(w => w && w.GetType().Name.Contains("TMP_PackageResourceImporter"))) w.Close();
        }

        static string s_Name => s_Steps[s_Index - 1].name;

        // ---- capture each Unity window with PrintWindow(PW_RENDERFULLCONTENT) --------------------
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
        delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int w, int h);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern int GetDIBits(IntPtr dc, IntPtr bmp, uint start, uint lines, byte[] bits, ref BITMAPINFOHEADER bi, uint usage);
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
        [StructLayout(LayoutKind.Sequential)] struct BITMAPINFOHEADER { public uint size; public int w, h; public ushort planes, bits; public uint compression, sizeImage; public int xppm, yppm; public uint clrUsed, clrImportant; }

        static bool Grab()
        {
            string name = s_Name;
            uint me = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            int n = 0;
            var handles = new List<IntPtr>();
            EnumWindows((h, l) => { handles.Add(h); return true; }, IntPtr.Zero);
            foreach (var h in handles)
            try
            {
                GetWindowThreadProcessId(h, out uint pid);
                if (pid != me || !IsWindowVisible(h)) continue;
                GetWindowRect(h, out var r);
                int w = r.R - r.L, hh = r.B - r.T;
                if (w < 300 || hh < 200) continue;
                var title = new StringBuilder(256); GetWindowText(h, title, 256);
                var file = Path.Combine(Out, $"{name}-{n++}.png");
                File.WriteAllBytes(file, Capture(h, w, hh));
                Debug.Log($"[KitShots] grabbed {Path.GetFileName(file)} '{title}' {w}x{hh}");
            }
            catch (Exception e) { Debug.LogWarning("[KitShots] grab failed: " + e.Message); }
            Debug.Log($"[KitShots] {name}: {n} window(s) of {handles.Count}");
            return true;
        }

        static byte[] Capture(IntPtr hwnd, int w, int h)
        {
            IntPtr screen = GetDC(IntPtr.Zero), dc = CreateCompatibleDC(screen), bmp = CreateCompatibleBitmap(screen, w, h);
            var old = SelectObject(dc, bmp);
            PrintWindow(hwnd, dc, 2);
            var bi = new BITMAPINFOHEADER { size = 40, w = w, h = h, planes = 1, bits = 32 };
            var bits = new byte[w * h * 4];
            GetDIBits(dc, bmp, 0, (uint)h, bits, ref bi, 0);
            for (int i = 3; i < bits.Length; i += 4) bits[i] = 255;
            SelectObject(dc, old); DeleteObject(bmp); DeleteDC(dc); ReleaseDC(IntPtr.Zero, screen);
            var tex = new Texture2D(w, h, TextureFormat.BGRA32, false);
            tex.LoadRawTextureData(bits); tex.Apply();
            var png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            return png;
        }

        static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            EditorSettings.enterPlayModeOptionsEnabled = s_Prev.Item1; EditorSettings.enterPlayModeOptions = s_Prev.Item2;
            AssetDatabase.SaveAssets();
            Debug.Log("[KitShots] done, exiting " + code);
            EditorApplication.Exit(code);
        }
    }
}
