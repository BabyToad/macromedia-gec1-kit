using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Kit.Editor
{
    /// <summary>
    /// Assets › Create › Kit › Eigener Knoten (C#): writes the two files of a custom node, as in the Day 3 handout
    /// ("Was ein Knoten braucht"): the runtime class (a KitNode) and its face for the graph editor (a KitEditorNode).
    /// Always into a place the template owns, whatever folder is selected:
    ///   Assets/Eigene Knoten/Name.cs               (assembly Eigene.Knoten → Kit.Runtime; runs in the game)
    ///   Assets/Eigene Knoten/Editor/NameNode.cs    (assembly Eigene.Knoten.Editor, Editor only → Kit.Editor)
    /// The two .asmdef files are created on first use. So the face never ends up in a game assembly and the
    /// node never ends up editor-only (Unity's assembly rules follow .asmdef files, not folder names).
    /// Never overwrites; rejects names that would not compile.
    /// </summary>
    public static class KitNodeTemplate
    {
        public const string Folder = "Assets/Eigene Knoten";
        public const string RuntimeAssembly = "Eigene.Knoten", EditorAssembly = "Eigene.Knoten.Editor";
        const string Placeholder = "KNOTENNAME";

        public static string RuntimeSource(string name) => Runtime.Replace(Placeholder, name);
        public static string FaceSource(string name) => Face.Replace(Placeholder, name);

        static readonly HashSet<string> k_Keywords = new HashSet<string>
        {
            "abstract","as","base","bool","break","byte","case","catch","char","checked","class","const","continue",
            "decimal","default","delegate","do","double","else","enum","event","explicit","extern","false","finally",
            "fixed","float","for","foreach","goto","if","implicit","in","int","interface","internal","is","lock",
            "long","namespace","new","null","object","operator","out","override","params","private","protected",
            "public","readonly","ref","return","sbyte","sealed","short","sizeof","stackalloc","static","string",
            "struct","switch","this","throw","true","try","typeof","uint","ulong","unchecked","unsafe","ushort",
            "using","virtual","void","volatile","while","var","dynamic","record","value","nameof","await","async",
        };
        // Members of the generated class: a class may not have a member with its own name (CS0542).
        static readonly string[] k_Members = { "target", "strength", "done", "Go" };
        // Namespaces the generated files import or the course code uses.
        static readonly string[] k_Namespaces = { null, "Kit", "Kit.Editor", "UnityEngine", "UnityEditor", "System" };

        static HashSet<string> s_Roots;
        // First parts of every namespace in the loaded assemblies (System, Kit, UnityEngine, UnityEditor, Unity, …):
        // a class named like one makes "using X;" in the generated files fail (CS0138).
        static HashSet<string> NamespaceRoots()
        {
            if (s_Roots != null) return s_Roots;
            var roots = new HashSet<string> { "System", "Kit", "UnityEngine", "UnityEditor", "Unity" };
            foreach (var t in TypeCache.GetTypesDerivedFrom<object>())
                if (!string.IsNullOrEmpty(t.Namespace)) { int dot = t.Namespace.IndexOf('.'); roots.Add(dot < 0 ? t.Namespace : t.Namespace.Substring(0, dot)); }
            return s_Roots = roots;
        }

        /// <summary>Null if the name works for both generated classes, else why not (German).</summary>
        public static string NameProblem(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Der Name fehlt.";
            if (!Regex.IsMatch(name, "^[A-Za-z][A-Za-z0-9_]*$"))
                return $"„{name}“ geht nicht als Klassenname: nur Buchstaben (ohne Umlaute), Ziffern und _, vorne ein Buchstabe.";
            if (k_Keywords.Contains(name))
                return $"„{name}“ ist ein C#-Schlüsselwort.";
            if (NamespaceRoots().Contains(name))
                return $"„{name}“ ist der Name eines Namensraums (z. B. using {name};). Bitte einen anderen Namen.";
            if (Array.IndexOf(k_Members, name) >= 0)
                return $"„{name}“ heißt schon ein Teil des Knotens selbst. Bitte einen anderen Namen.";
            foreach (var n in new[] { name, name + "Node" })
            {
                var clash = TypeCache.GetTypesDerivedFrom<object>().FirstOrDefault(t => t.Name == n && Array.IndexOf(k_Namespaces, t.Namespace) >= 0);
                if (clash != null) return $"„{n}“ gibt es schon ({clash.FullName} in {clash.Assembly.GetName().Name}). Bitte einen anderen Namen.";
                foreach (var g in AssetDatabase.FindAssets(n + " t:MonoScript"))
                    if (Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == n)
                        return $"Ein Skript „{n}.cs“ gibt es schon: {AssetDatabase.GUIDToAssetPath(g)}.";
            }
            return null;
        }

        /// <summary>
        /// Writes Assets/Eigene Knoten/Name.cs and …/Editor/NameNode.cs (and the two .asmdef on first use).
        /// Throws if the name does not work or a file is there. <paramref name="root"/> is for tests only.
        /// </summary>
        public static (string runtime, string face) Create(string name, string root = Folder, bool import = true)
        {
            if (NameProblem(name) is string why) throw new ArgumentException(why, nameof(name));
            string runtime = root + "/" + name + ".cs", face = root + "/Editor/" + name + "Node.cs";
            foreach (var p in new[] { runtime, face })
                if (File.Exists(p)) throw new IOException($"„{p}“ gibt es schon: nichts angelegt.");
            Directory.CreateDirectory(root + "/Editor");
            WriteIfMissing(root + "/" + RuntimeAssembly + ".asmdef", RuntimeAsmdef);
            WriteIfMissing(root + "/Editor/" + EditorAssembly + ".asmdef", EditorAsmdef);
            File.WriteAllText(runtime, RuntimeSource(name));
            File.WriteAllText(face, FaceSource(name));
            if (import) AssetDatabase.Refresh();
            return (runtime, face);
        }

        static void WriteIfMissing(string path, string text) { if (!File.Exists(path)) File.WriteAllText(path, text); }

        [MenuItem("Assets/Create/Kit/Eigener Knoten (C#)", priority = 81)]
        [MenuItem("Tools/Kit/Eigener Knoten (C#)")]
        static void CreateMenu()
        {
            KitPaths.EnsureFolder(Folder);
            var path = EditorUtility.SaveFilePanelInProject("Eigener Knoten", "MeinKnoten", "cs",
                "Name des Knotens (wird der Klassenname). Beide Dateien kommen nach „Assets/Eigene Knoten“.", Folder);
            if (string.IsNullOrEmpty(path)) return;
            string name = Path.GetFileNameWithoutExtension(path);   // nur der Name zählt, der Ort ist immer Assets/Eigene Knoten
            try
            {
                var (runtime, _) = Create(name);
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<MonoScript>(runtime));
            }
            catch (Exception e) when (e is ArgumentException || e is IOException)
            {
                EditorUtility.DisplayDialog("Eigener Knoten", e.Message, "OK");
            }
        }

        const string RuntimeAsmdef =
@"{
    ""name"": ""Eigene.Knoten"",
    ""references"": [""Kit.Runtime""],
    ""autoReferenced"": true
}
";

        const string EditorAsmdef =
@"{
    ""name"": ""Eigene.Knoten.Editor"",
    ""references"": [""Eigene.Knoten"", ""Kit.Runtime"", ""Kit.Editor"", ""Unity.GraphToolkit.Editor"", ""Unity.GraphToolkit.Common.Editor""],
    ""includePlatforms"": [""Editor""],
    ""autoReferenced"": true
}
";

        const string Runtime =
@"using System;
using UnityEngine;
using Kit;

// KNOTENNAME: ein eigener Kit-Knoten. Der Vertrag steht im Handout von Tag 3, „Was ein Knoten braucht“.
// NodeInfo: Titel, Gruppe und Kurzbeschreibung unter „+ Knoten“.
[Serializable]
[NodeInfo(""KNOTENNAME"", ""Aktion"", ""Was der Knoten tut"")]
public class KNOTENNAME : KitNode
{
    // ◆ Platz für ein Objekt der Szene. Die Interaktion füllt ihn.
    [Ref(""Objekt"")] public GameObject target;

    // Einstellung, im Knoten einstellbar.
    [Setting(""Stärke"")] public float strength = 1f;

    // ▶ Ausgang: feuert, wenn der Knoten seine Arbeit getan hat.
    [Output(""Fertig"")] [NonSerialized] public Output done;

    // ▶ Eingang: läuft, wenn ein Event ankommt.
    [Input(""Los"")]
    public void Go(Signal s)
    {
        // Hier passiert die Arbeit. Bei einem Problem: Fail(""Grund""); return;
        Debug.Log($""{target.name}: Stärke {strength}"");
        done.Fire(s);
    }
}
";

        const string Face =
@"using System;
using Kit.Editor;

// Das Gesicht von KNOTENNAME im Graph-Editor. Liegt in „Assets/Eigene Knoten/Editor“ (Assembly nur für den Editor).
[Serializable]
public class KNOTENNAMENode : KitEditorNode
{
    public override Type RuntimeType => typeof(KNOTENNAME);
}
";
    }
}
