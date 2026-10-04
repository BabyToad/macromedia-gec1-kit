using UnityEditor;
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

        /// <summary>True for paths the project may write (Assets/…); Packages/… is read-only.</summary>
        public static bool IsWritable(string assetPath) =>
            !string.IsNullOrEmpty(assetPath) && (assetPath == "Assets" || assetPath.StartsWith("Assets/"));

        /// <summary>Path of a shipped asset by its GUID, or null if it is not in this project.</summary>
        public static string ByGuid(string guid)
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(p) || AssetDatabase.LoadMainAssetAtPath(p) == null ? null : p;
        }

        /// <summary>Creates every missing level of an Assets/… folder.</summary>
        public static string EnsureFolder(string folder)
        {
            if (!IsWritable(folder)) throw new System.ArgumentException($"„{folder}“ liegt nicht unter Assets und ist schreibgeschützt", nameof(folder));
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
