using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Kit.Editor
{
    /// <summary>
    /// Where the kit lives and where its menus may write. The kit arrives either as a package
    /// (Packages/…, read-only) in student projects or under Assets/Kit in the studio project.
    /// Shipped assets are found by GUID (the package keeps the .meta files); anything built goes into
    /// the project's Assets, never into Packages.
    /// </summary>
    public static class KitPaths
    {
        /// <summary>Folder for everything the kit menus build in a student project.</summary>
        public const string StudentFolder = "Assets/Kit Beispiel";

        /// <summary>The package this kit was installed as, or null when it sits in Assets (studio project).</summary>
        public static PackageInfo Package => PackageInfo.FindForAssembly(typeof(KitPaths).Assembly);

        public static bool FromPackage => Package != null;

        /// <summary>
        /// True only for paths that really end up inside the project's Assets folder (Packages/… is read-only).
        /// Resolved to a full path first, so "Assets/../Packages/…" does not count as Assets.
        /// </summary>
        public static bool IsWritable(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            string root, full;
            try
            {
                root = Path.GetFullPath("Assets").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                full = Path.GetFullPath(assetPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception) { return false; }
            return string.Equals(full, root, StringComparison.OrdinalIgnoreCase)
                || full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        public enum Existing { NewCopy, Overwrite, Cancel }

        /// <summary>
        /// Something the menu would build is already there (possibly changed by the student): ask. Without a
        /// window to ask in (batch mode) the answer is always "new copy" – nothing is overwritten unasked.
        /// </summary>
        public static Existing AskExisting(string what, string path)
        {
            if (Application.isBatchMode) return Existing.NewCopy;
            int r = EditorUtility.DisplayDialogComplex("Schon vorhanden",
                $"{what} gibt es schon:\n{path}\n\nBeim Überschreiben gehen Änderungen daran verloren.",
                "Neue Kopie anlegen", "Abbrechen", "Überschreiben");
            return r == 0 ? Existing.NewCopy : r == 2 ? Existing.Overwrite : Existing.Cancel;
        }

        /// <summary>
        /// Before a menu replaces the open scene: offer to save changes. False = stop (cancelled, or batch mode
        /// with unsaved changes, where nobody can be asked).
        /// </summary>
        public static bool OpenScenesSafe()
        {
            if (!Application.isBatchMode) return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogWarning("[Kit] Eine geöffnete Szene hat ungespeicherte Änderungen: nichts gebaut.");
                    return false;
                }
            return true;
        }

        /// <summary>Path of a shipped asset by its GUID, or null if it is not in this project.</summary>
        public static string ByGuid(string guid)
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(p) || AssetDatabase.LoadMainAssetAtPath(p) == null ? null : p;
        }

        /// <summary>Creates every missing level of an Assets/… folder.</summary>
        public static string EnsureFolder(string folder)
        {
            if (!IsWritable(folder)) throw new ArgumentException($"„{folder}“ liegt nicht unter Assets und ist schreibgeschützt", nameof(folder));
            if (AssetDatabase.IsValidFolder(folder)) return folder;
            var parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
            return folder;
        }
    }
}
