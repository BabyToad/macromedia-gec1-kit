using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kit
{
    /// <summary>
    /// Verweis auf eine Szene: im Editor zieht man die Szene-Datei hinein (wie jedes andere Asset), im fertigen
    /// Spiel bleibt ihr Pfad. Umbenennen oder Verschieben der Szene bricht den Verweis nicht: im Editor gilt immer
    /// die Datei selbst, vor jedem Build schreibt das Kit die Pfade neu.
    /// </summary>
    [Serializable]
    public class SceneRef
    {
        // The SceneAsset (editor-only type) is kept as a plain Object: in a player build the reference is empty,
        // the path below stays. Same pattern as Unity's own samples; no #if, so editor and player share one layout.
        [SerializeField, Tooltip("Szene-Datei aus dem Project-Fenster hier hineinziehen")] Object asset;
        [SerializeField, HideInInspector] string path = "";

        public SceneRef() { }
        public SceneRef(string path) { this.path = path ?? ""; }

        /// <summary>Pfad wie „Assets/Szenen/Keller.unity“. Im Editor frisch aus der Datei, im Spiel der gespeicherte.</summary>
        public string Path
        {
            get
            {
#if UNITY_EDITOR
                if (asset) { var p = UnityEditor.AssetDatabase.GetAssetPath(asset); if (!string.IsNullOrEmpty(p)) return p; }
#endif
                return path ?? "";
            }
        }

        /// <summary>Name der Szene ohne Ordner und Endung („Keller“).</summary>
        public string Name => NameOf(Path);

        /// <summary>True, wenn keine Szene eingetragen ist.</summary>
        public bool IsEmpty => string.IsNullOrEmpty(Path);

        public override string ToString() => IsEmpty ? "(keine Szene)" : Name;

        public static string NameOf(string scenePath) =>
            string.IsNullOrEmpty(scenePath) ? "" : System.IO.Path.GetFileNameWithoutExtension(scenePath);

#if UNITY_EDITOR
        /// <summary>Editor: the scene file (a SceneAsset) or null.</summary>
        public Object Asset => asset;

        /// <summary>Editor: the path as stored (what a build would use), without asking the file.</summary>
        public string StoredPath => path ?? "";

        /// <summary>Editor: sets the scene file.</summary>
        public static SceneRef To(UnityEditor.SceneAsset scene)
        {
            var r = new SceneRef { asset = scene };
            r.path = scene ? UnityEditor.AssetDatabase.GetAssetPath(scene) : "";
            return r;
        }
#endif
    }
}
