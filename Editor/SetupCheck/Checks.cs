using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Gec1.SetupCheck
{
    /// <summary>
    /// The individual checks. Pure C#: no UnityEngine/UnityEditor calls here, so the logic
    /// can be tested outside the Editor. <see cref="SetupCheckRunner"/> feeds in Editor state.
    /// Student-facing text is German, impersonal (no du/Sie; studio rule).
    /// </summary>
    internal static class Checks
    {
        // ---------------------------------------------------------------- Unity

        public static CheckResult UnityVersion(string actual)
        {
            const string title = "Unity-Version";
            if (actual == StackConfig.UnityVersion)
                return new CheckResult(CheckStatus.Ok, title, actual + " (" + StackConfig.UnityLabel + ")");
            string install = "Diesen Link im Browser öffnen, dann installiert der Unity Hub die richtige Version: unityhub://"
                + StackConfig.UnityVersion + "/" + StackConfig.UnityChangeset + "\nDann im Hub beim Projekt die Version auf "
                + StackConfig.UnityVersion + " stellen und neu öffnen. Siehe Anleitung „Unity installieren“.";
            int patch = PatchNumber(actual, StackConfig.UnityStream);
            int maxPatch;
            if (patch >= 0 && int.TryParse(StackConfig.UnityMaxPatch, out maxPatch) && patch > maxPatch)
                return new CheckResult(CheckStatus.Fail, title,
                    "Installiert ist " + actual + ". Neuere 6.3-Versionen als " + StackConfig.UnityStream + "." + maxPatch
                    + " gehen in diesem Kurs NICHT: Ab " + StackConfig.UnityStream + "." + (maxPatch + 1)
                    + " funktioniert der Graph-Editor des Kurs-Kits nicht mehr (eine Änderung in Unity bricht das Graph Toolkit). "
                    + "Der Kurs nutzt genau " + StackConfig.UnityVersion + ".",
                    install);
            if (actual.StartsWith(StackConfig.UnityStream + ".", StringComparison.Ordinal))
                return new CheckResult(CheckStatus.Warn, title,
                    "Installiert ist " + actual + ", der Kurs nutzt " + StackConfig.UnityVersion + ". Gleiche Hauptversion, anderer Patch – sollte gehen.",
                    "Wenn etwas seltsam ist: im Unity Hub " + StackConfig.UnityVersion + " installieren und das Projekt damit öffnen.");
            return new CheckResult(CheckStatus.Fail, title,
                "Installiert ist " + actual + ". Der Kurs braucht " + StackConfig.UnityVersion + " (" + StackConfig.UnityLabel + ").",
                install);
        }

        /// <summary>"6000.3.15f1" with stream "6000.3" -> 15; -1 if another stream or unparsable.</summary>
        public static int PatchNumber(string version, string stream)
        {
            if (version == null || !version.StartsWith(stream + ".", StringComparison.Ordinal)) return -1;
            Match m = Regex.Match(version.Substring(stream.Length + 1), @"^(\d+)");
            return m.Success ? int.Parse(m.Groups[1].Value) : -1;
        }

        public static CheckResult RenderPipeline(string pipelineTypeName)
        {
            const string title = "Render-Pipeline (URP)";
            if (!string.IsNullOrEmpty(pipelineTypeName) && pipelineTypeName.Contains("Universal"))
                return new CheckResult(CheckStatus.Ok, title, "Universal Render Pipeline ist aktiv.");
            string what = string.IsNullOrEmpty(pipelineTypeName)
                ? "Es ist keine Render-Pipeline eingestellt (Built-in)."
                : "Aktiv ist " + pipelineTypeName + ", nicht URP.";
            return new CheckResult(CheckStatus.Fail, title, what,
                "Am einfachsten: Projekt neu anlegen mit der Vorlage \"" + StackConfig.Template + "\" (Anleitung „Projekt anlegen“). "
                + "Unter Edit > Project Settings > Graphics muss ein URP-Asset eingetragen sein.");
        }

        public static CheckResult Packages(string projectRoot, IEnumerable<string> required)
        {
            const string title = "Pakete";
            string manifest = ReadOrEmpty(Path.Combine(projectRoot, "Packages", "manifest.json"));
            string lockFile = ReadOrEmpty(Path.Combine(projectRoot, "Packages", "packages-lock.json"));
            var missing = required.Where(p => manifest.IndexOf("\"" + p + "\"", StringComparison.Ordinal) < 0
                                              && lockFile.IndexOf("\"" + p + "\"", StringComparison.Ordinal) < 0).ToList();
            if (missing.Count == 0)
                return new CheckResult(CheckStatus.Ok, title, "Alle Kurspakete sind installiert.");
            return new CheckResult(CheckStatus.Fail, title,
                "Es fehlt: " + string.Join(", ", missing.ToArray()),
                "Window > Package Manager > \"Unity Registry\" > Paket suchen > Install. "
                + "Fehlt URP, ist das Projekt wahrscheinlich mit der falschen Vorlage angelegt.");
        }

        public static CheckResult CodeEditor(string editorPath)
        {
            const string title = "Code-Editor";
            string name = string.IsNullOrEmpty(editorPath) ? "" : Path.GetFileNameWithoutExtension(editorPath.TrimEnd('/', '\\'));
            if (string.IsNullOrEmpty(editorPath) || editorPath.IndexOf("internal", StringComparison.OrdinalIgnoreCase) >= 0)
                return new CheckResult(CheckStatus.Warn, title, "Unity hat keinen externen Code-Editor eingestellt.",
                    "Edit > Preferences (Mac: Unity > Settings) > External Tools > External Script Editor: Visual Studio Code wählen.");
            if (editorPath.IndexOf("code", StringComparison.OrdinalIgnoreCase) >= 0
                && editorPath.IndexOf("Visual Studio\\", StringComparison.OrdinalIgnoreCase) < 0)
                return new CheckResult(CheckStatus.Ok, title, "Visual Studio Code ist eingestellt.");
            return new CheckResult(CheckStatus.Info, title,
                "Eingestellt ist \"" + name + "\". Das geht auch – der Kurs zeigt aber alles in Visual Studio Code.");
        }

        /// <summary>
        /// Unity loads the course package (de.macromedia.gec1) from a Git URL. The Package Manager needs a
        /// git executable on the PATH of the Unity process (GitHub Desktop's bundled git is not on PATH).
        /// </summary>
        /// <param name="versionOutput">stdout of "git --version", or null if git could not be started.</param>
        public static CheckResult GitOnPath(string versionOutput, bool isWindows)
        {
            const string title = "Git (für das Kurs-Paket)";
            string fix = isWindows
                ? "Git für Windows von https://git-scm.com/downloads/win installieren (Vorschläge im Installer übernehmen). Danach den Unity Hub ganz beenden (Symbol unten rechts in der Taskleiste > Quit) und neu starten, sonst sieht Unity Git nicht."
                : "Im Terminal \"xcode-select --install\" eingeben und die Command Line Tools installieren. Danach Unity Hub beenden (Cmd+Q) und neu starten.";
            if (string.IsNullOrEmpty(versionOutput))
                return new CheckResult(CheckStatus.Fail, title,
                    "Unity findet kein Git. Ohne Git kann Unity das Kurs-Paket nicht laden.", fix);
            Match m = Regex.Match(versionOutput, @"git version (\d+)\.(\d+)");
            if (!m.Success)
                return new CheckResult(CheckStatus.Fail, title,
                    "Git meldet sich nicht richtig: " + versionOutput.Trim(), fix);
            int major = int.Parse(m.Groups[1].Value), minor = int.Parse(m.Groups[2].Value);
            if (major < 2 || (major == 2 && minor < 14))
                return new CheckResult(CheckStatus.Warn, title,
                    versionOutput.Trim() + " ist älter als 2.14. Unity garantiert Git-Pakete erst ab 2.14.", fix);
            return new CheckResult(CheckStatus.Ok, title, versionOutput.Trim());
        }

        /// <summary>Reports which version of the course package is installed. No network call: the course's
        /// newest version is not known offline, so this stays informational.</summary>
        public static CheckResult KitPackage(string installedVersion, string source)
        {
            const string title = "Kurs-Paket";
            if (string.IsNullOrEmpty(installedVersion))
                return new CheckResult(CheckStatus.Info, title,
                    "Der Setup-Check läuft nicht aus dem Kurs-Paket " + StackConfig.KitPackage + " (z. B. als eingebettete Kopie).");
            return new CheckResult(CheckStatus.Info, title,
                StackConfig.KitPackage + " " + installedVersion + (string.IsNullOrEmpty(source) ? "" : " (" + source + ")")
                + ". Neuere Versionen kündigt Jonas an; aktualisieren: Anleitung „Kurs-Paket aktualisieren“.");
        }

        /// <summary>Mac students skip the Windows module in the room (bandwidth). Until 14 days before
        /// the hand-in a missing module is INFO, so Macs can reach "Alles bereit"; then WARNUNG.</summary>
        public static CheckResult WindowsBuild(bool supported, bool isWindows, DateTime today)
        {
            const string title = "Windows-Build möglich";
            if (supported)
                return new CheckResult(CheckStatus.Ok, title, "Windows-Builds sind möglich (für die Abgabe nötig).");
            CheckStatus status = isWindows || NearHandIn(today) ? CheckStatus.Warn : CheckStatus.Info;
            return new CheckResult(status, title,
                "Das Modul für Windows-Builds fehlt. Für den Anfang egal, für die Abgabe nötig.",
                isWindows
                    ? "Unity Hub > Installs > Zahnrad bei " + StackConfig.UnityVersion + " > Add modules > Windows Build Support."
                    : "Unity Hub > Installs > Zahnrad bei " + StackConfig.UnityVersion + " > Add modules > \"Windows Build Support (Mono)\" (ca. 400 MB). Am besten zu Hause."
                      + " Zu Hause nachholen, spätestens zwei Wochen vor der Abgabe (" + StackConfig.HandIn + ").");
        }

        // ---------------------------------------------------------------- Project folder

        static readonly string[] CloudMarkers = { "OneDrive", "iCloud", "Mobile Documents", "Dropbox", "Google Drive", "GoogleDrive", "My Drive", "Nextcloud" };

        public static CheckResult ProjectPath(string projectRoot, bool isWindows)
        {
            const string title = "Projektordner";
            var problems = new List<string>();
            string cloud = CloudMarkers.FirstOrDefault(m => projectRoot.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0);
            if (cloud != null)
                problems.Add("Der Ordner liegt in einem Cloud-Ordner (" + cloud + "). Synchronisieren und Git vertragen sich schlecht, und Unity wird langsam.");
            if (projectRoot.Any(c => c > 127))
                problems.Add("Der Pfad enthält Sonderzeichen (z. B. Umlaute). Manche Werkzeuge stolpern darüber.");
            if (projectRoot.IndexOf("Downloads", StringComparison.OrdinalIgnoreCase) >= 0)
                problems.Add("Der Ordner liegt in Downloads. Dieser Ordner wird oft aufgeräumt.");
            if (isWindows && projectRoot.Length > 120)
                problems.Add("Der Pfad ist sehr lang (" + projectRoot.Length + " Zeichen). Windows kann bei langen Pfaden scheitern.");

            if (problems.Count == 0)
                return new CheckResult(CheckStatus.Ok, title, projectRoot);
            return new CheckResult(CheckStatus.Warn, title, projectRoot + "\n" + string.Join("\n", problems.ToArray()),
                "Das Projekt in einen einfachen, lokalen Ordner legen, z. B. "
                + (isWindows ? "C:\\Unity\\" : "~/Unity/") + ". Wie das Projekt umzieht, steht in der Anleitung „Projekt anlegen“.");
        }

        public static CheckResult LargeFiles(string projectRoot)
        {
            const string title = "Große Dateien";
            const long warnBytes = 50L * 1024 * 1024, failBytes = 100L * 1024 * 1024;
            var big = new List<string>();
            bool tooBig = false;
            try
            {
                foreach (string f in Directory.EnumerateFiles(Path.Combine(projectRoot, "Assets"), "*", SearchOption.AllDirectories))
                {
                    long len = new FileInfo(f).Length;
                    if (len < warnBytes) continue;
                    tooBig |= len >= failBytes;
                    big.Add(RelativeTo(projectRoot, f) + " (" + (len / (1024 * 1024)) + " MB)");
                }
            }
            catch (Exception e)
            {
                return new CheckResult(CheckStatus.Info, title, "Assets lässt sich nicht durchsuchen: " + e.Message);
            }
            if (big.Count == 0)
                return new CheckResult(CheckStatus.Ok, title, "Keine Datei in Assets ist größer als 50 MB.");
            return new CheckResult(tooBig ? CheckStatus.Fail : CheckStatus.Warn, title,
                string.Join("\n", big.Take(8).ToArray()),
                tooBig
                    ? "GitHub nimmt keine Dateien über 100 MB an. Die Datei verkleinern (Audio als .ogg, Texturen kleiner) oder aus dem Projekt nehmen."
                    : "GitHub warnt ab 50 MB. Prüfen, ob die Datei so groß sein muss.");
        }

        public static CheckResult AgentInstructions(string projectRoot)
        {
            const string title = "Anweisungen für den KI-Agenten (AGENTS.md)";
            if (File.Exists(Path.Combine(projectRoot, "AGENTS.md")))
            {
                // Claude Code reads AGENTS.md only when there is no CLAUDE.md; a CLAUDE.md must import it.
                string claude = Path.Combine(projectRoot, "CLAUDE.md");
                if (File.Exists(claude) && ReadOrEmpty(claude).IndexOf("@AGENTS.md", StringComparison.Ordinal) < 0)
                    return new CheckResult(CheckStatus.Warn, title,
                        "Es gibt eine CLAUDE.md, die AGENTS.md nicht einbindet. Claude Code liest dann die Kursregeln nicht.",
                        "In die erste Zeile von CLAUDE.md schreiben: @AGENTS.md");
                return new CheckResult(CheckStatus.Ok, title, "AGENTS.md liegt im Projektordner.");
            }
            return new CheckResult(CheckStatus.Warn, title,
                "Im Projektordner fehlt AGENTS.md. Daraus liest der KI-Agent die Regeln für das Projekt.",
                "Die Kursvorlage bringt AGENTS.md mit. Ohne Vorlage: Datei aus dem Kursmaterial in den Projektordner legen (neben Assets).");
        }

        static bool NearHandIn(DateTime today)
        {
            DateTime handIn;
            return DateTime.TryParse(StackConfig.HandIn, System.Globalization.CultureInfo.InvariantCulture,
                       System.Globalization.DateTimeStyles.None, out handIn)
                   && today >= handIn.AddDays(-14);
        }

        /// <summary>
        /// The Macromedia AI guideline requires a complete AI register in every project, even without AI use.
        /// Never an error: missing or empty is a WARNUNG near the hand-in, otherwise INFO.
        /// </summary>
        public static CheckResult AiRegister(string projectRoot, DateTime today)
        {
            const string title = "KI-Verzeichnis";
            const int warnDaysBeforeHandIn = 14;
            DateTime handIn;
            bool nearHandIn = DateTime.TryParse(StackConfig.HandIn, System.Globalization.CultureInfo.InvariantCulture,
                                  System.Globalization.DateTimeStyles.None, out handIn)
                              && today >= handIn.AddDays(-warnDaysBeforeHandIn);
            CheckStatus soft = nearHandIn ? CheckStatus.Warn : CheckStatus.Info;

            string path = Path.Combine(projectRoot, "KI-VERZEICHNIS.md");
            if (!File.Exists(path))
                return new CheckResult(soft, title,
                    "Im Projektordner fehlt KI-VERZEICHNIS.md. Die KI-Richtlinie der Hochschule verlangt ein KI-Verzeichnis im Anhang, auch ohne KI-Nutzung.",
                    "Datei aus der Kursvorlage (oder den Kursdateien) in den Projektordner legen, neben AGENTS.md.");

            int rows = CountRegisterRows(ReadOrEmpty(path));
            if (rows == 0)
                return new CheckResult(soft, title,
                    nearHandIn
                        ? "Das KI-Verzeichnis ist noch leer. Bald ist Abgabe (" + StackConfig.HandIn + ")."
                        : "Das KI-Verzeichnis ist noch leer. Am Anfang ist das normal.",
                    nearHandIn
                        ? "Jede Aufgabe eintragen, bei der eine KI im Projekt etwas geändert oder erzeugt hat. Wurde wirklich keine KI benutzt, genau das als eine Zeile eintragen."
                        : "Der Agent trägt jede Aufgabe ein, bei der er im Projekt etwas geändert oder erzeugt hat. Ab und zu prüfen, ob das passiert.");
            return new CheckResult(CheckStatus.Ok, title, rows + (rows == 1 ? " Eintrag" : " Einträge") + " in KI-VERZEICHNIS.md.");
        }

        /// <summary>Documentation scaffold from the template. Never an error.</summary>
        public static CheckResult Documentation(string projectRoot)
        {
            const string title = "Dokumentation (DOKUMENTATION.md)";
            if (File.Exists(Path.Combine(projectRoot, "DOKUMENTATION.md")))
                return new CheckResult(CheckStatus.Ok, title, "DOKUMENTATION.md liegt im Projektordner.");
            return new CheckResult(CheckStatus.Info, title,
                "Im Projektordner fehlt DOKUMENTATION.md, das Gerüst für die Dokumentation.",
                "Datei aus der Kursvorlage (oder den Kursdateien) in den Projektordner legen, neben KI-VERZEICHNIS.md, dazu einen Ordner bilder/ für Screenshots.");
        }

        /// <summary>Counts table rows after the header and separator rows of the first Markdown table.</summary>
        public static int CountRegisterRows(string text)
        {
            int tableLine = 0, rows = 0;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (!line.StartsWith("|", StringComparison.Ordinal))
                {
                    if (tableLine > 0) break; // table ended
                    continue;
                }
                tableLine++;
                if (tableLine <= 2) continue; // header + separator
                string cells = line.Replace("|", "").Replace("-", "").Trim();
                if (cells.Length > 0) rows++;
            }
            return rows;
        }

        // ---------------------------------------------------------------- Git

        /// <summary>All Git-related checks. Later checks are skipped when there is no repository.</summary>
        public static List<CheckResult> Git(string projectRoot)
        {
            var results = new List<CheckResult>();
            string root = GitIndexReader.FindWorkTreeRoot(projectRoot);
            if (root == null)
            {
                results.Add(new CheckResult(CheckStatus.Fail, "Git-Repository",
                    "Der Projektordner ist kein Git-Repository.",
                    "Anleitung „Projekt ohne Vorlage anlegen“: In GitHub Desktop File > Add Local Repository > den Projektordner wählen > \"create a repository\"."));
                return results;
            }

            string prefix = "";
            if (!SamePath(root, projectRoot))
            {
                prefix = RelativeTo(root, projectRoot).Replace('\\', '/').TrimEnd('/') + "/";
                results.Add(new CheckResult(CheckStatus.Warn, "Git-Repository",
                    "Das Repository beginnt nicht im Projektordner, sondern weiter oben:\n" + root
                    + "\nDann landet leicht mehr im Repository als das Projekt (und mit einer falschen .gitignore auch Library/).",
                    "Im Kurs liegt das Repository genau im Projektordner (dem Ordner mit Assets, Packages, ProjectSettings). "
                    + "Zeigen die Prüfungen darunter keine Fehler, kann erst einmal weitergearbeitet werden. Sonst Jonas fragen."));
            }
            else
            {
                results.Add(new CheckResult(CheckStatus.Ok, "Git-Repository", "Der Projektordner ist ein Git-Repository."));
            }

            results.Add(GitIgnore(projectRoot));

            string gitDir = GitIndexReader.ResolveGitDir(root);
            if (gitDir == null)
            {
                results.Add(new CheckResult(CheckStatus.Info, "Git-Inhalt", "Der .git-Ordner lässt sich nicht lesen."));
                return results;
            }
            results.Add(Commits(gitDir));
            results.Add(Remote(gitDir));

            string error;
            List<string> tracked = GitIndexReader.ReadTrackedPaths(gitDir, out error);
            if (tracked == null)
            {
                results.Add(new CheckResult(CheckStatus.Info, "Library nicht im Repository", "Der Git-Index lässt sich nicht lesen: " + error));
                return results;
            }
            results.Add(GeneratedFoldersNotTracked(tracked, prefix));
            CheckResult metas = MetaFilesTracked(tracked, prefix);
            if (metas != null) results.Add(metas);
            return results;
        }

        static readonly string[] RequiredIgnores = { "library", "temp", "obj", "logs", "usersettings" };
        static readonly string[] ForbiddenIgnores = { "assets", "packages", "projectsettings", "*.meta", "*.cs", "*.unity", "*.prefab", "*.asset", "*.mat" };

        public static CheckResult GitIgnore(string projectRoot)
        {
            const string title = ".gitignore";
            string path = Path.Combine(projectRoot, ".gitignore");
            if (!File.Exists(path))
            {
                string near = new[] { "gitignore", ".gitignore.txt", "gitignore.txt", "Unity.gitignore" }
                    .FirstOrDefault(n => File.Exists(Path.Combine(projectRoot, n)));
                return new CheckResult(CheckStatus.Fail, title,
                    near != null
                        ? "Es gibt \"" + near + "\", aber keine Datei, die genau \".gitignore\" heißt."
                        : "Im Projektordner liegt keine .gitignore.",
                    near != null
                        ? "\"" + near + "\" in \".gitignore\" umbenennen (Punkt vorne, keine Endung). Am Mac zeigt der Finder Dateien mit Punkt vorne erst nach Cmd+Shift+Punkt."
                        : "Die Kursvorlage bringt sie mit. Sonst: GitHub Desktop > Repository > Repository Settings > Ignored Files – Inhalt aus dem Kursmaterial einfügen.");
            }

            var lines = File.ReadAllLines(path).Select(NormalizeIgnoreLine).Where(l => l != null).ToList();
            var missing = RequiredIgnores.Where(r => !lines.Contains(r)).ToList();
            var forbidden = ForbiddenIgnores.Where(lines.Contains).ToList();

            if (forbidden.Count > 0)
                return new CheckResult(CheckStatus.Fail, title,
                    "Die .gitignore schließt Dinge aus, die ins Repository gehören: " + string.Join(", ", forbidden.ToArray()),
                    "Diese Zeilen aus der .gitignore löschen. Ohne sie fehlen nach dem Klonen Dateien oder Verknüpfungen.");
            if (missing.Contains("library") || missing.Contains("temp"))
                return new CheckResult(CheckStatus.Fail, title,
                    "Die .gitignore schließt " + string.Join(", ", missing.ToArray()) + " nicht aus.",
                    "Den Inhalt durch die Unity-.gitignore aus dem Kursmaterial ersetzen.");
            if (missing.Count > 0)
                return new CheckResult(CheckStatus.Warn, title,
                    "Nicht ausgeschlossen: " + string.Join(", ", missing.ToArray()) + ". Library und Temp sind aber drin.",
                    "Den Inhalt durch die Unity-.gitignore aus dem Kursmaterial ersetzen.");
            return new CheckResult(CheckStatus.Ok, title, "Vorhanden und schließt Library, Temp, Obj, Logs und UserSettings aus.");
        }

        /// <summary>
        /// Reduces a .gitignore line to a comparable lowercase token: "/[Ll]ibrary/" -> "library".
        /// Returns null for blank lines, comments and negations.
        /// </summary>
        public static string NormalizeIgnoreLine(string line)
        {
            string l = line.Trim();
            if (l.Length == 0 || l[0] == '#' || l[0] == '!') return null;
            if (l.StartsWith("**/", StringComparison.Ordinal)) l = l.Substring(3);
            l = l.TrimStart('/');
            foreach (string suffix in new[] { "/**", "/*", "/" })
                if (l.EndsWith(suffix, StringComparison.Ordinal)) { l = l.Substring(0, l.Length - suffix.Length); break; }
            l = Regex.Replace(l, @"\[([A-Za-z])([A-Za-z])\]",
                m => char.ToLowerInvariant(m.Groups[1].Value[0]) == char.ToLowerInvariant(m.Groups[2].Value[0])
                    ? m.Groups[1].Value : m.Value);
            return l.ToLowerInvariant();
        }

        static readonly string[] GeneratedFolders = { "Library/", "Temp/", "Logs/", "obj/", "UserSettings/" };

        public static CheckResult GeneratedFoldersNotTracked(List<string> tracked, string prefix)
        {
            const string title = "Library nicht im Repository";
            if (tracked.Count == 0)
                return new CheckResult(CheckStatus.Info, title, "Im Repository sind noch keine Dateien erfasst (noch kein Commit).");
            var counts = new Dictionary<string, int>();
            foreach (string p in tracked)
            {
                if (!p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                string rel = p.Substring(prefix.Length);
                foreach (string g in GeneratedFolders)
                    if (rel.StartsWith(g, StringComparison.OrdinalIgnoreCase))
                    {
                        int n; counts.TryGetValue(g, out n); counts[g] = n + 1;
                    }
            }
            if (counts.Count == 0)
                return new CheckResult(CheckStatus.Ok, title, tracked.Count + " Dateien im Repository, keine davon aus Library, Temp, Logs, obj oder UserSettings.");
            string list = string.Join(", ", counts.Select(kv => kv.Key + " (" + kv.Value + (kv.Value == 1 ? " Datei)" : " Dateien)")).ToArray());
            bool severe = counts.ContainsKey("Library/") || counts.ContainsKey("Temp/");
            return new CheckResult(severe ? CheckStatus.Fail : CheckStatus.Warn, title,
                "Diese Ordner sind im Repository, gehören da aber nicht hin: " + list,
                "Erst die .gitignore reparieren. Dann in GitHub Desktop: Repository > Open in Command Prompt (Mac: Terminal) und eingeben:\n"
                + "git rm -r --cached Library Temp Logs obj UserSettings\n"
                + "Danach in GitHub Desktop committen. Bei Unsicherheit: diesen Bericht in den Chat mit dem KI-Agenten kopieren oder Jonas fragen.");
        }

        /// <summary>Every tracked file and folder under Assets/ needs its .meta tracked too, or references break after cloning.</summary>
        public static CheckResult MetaFilesTracked(List<string> tracked, string prefix)
        {
            const string title = ".meta-Dateien im Repository";
            var set = new HashSet<string>(tracked, StringComparer.Ordinal);
            string assets = prefix + "Assets/";
            var missing = new List<string>();
            var folders = new HashSet<string>(StringComparer.Ordinal);
            bool any = false;
            foreach (string p in tracked)
            {
                if (!p.StartsWith(assets, StringComparison.Ordinal) || p.EndsWith(".meta", StringComparison.Ordinal)) continue;
                any = true;
                if (!set.Contains(p + ".meta")) missing.Add(p);
                int slash = p.LastIndexOf('/');
                while (slash > assets.Length - 1)
                {
                    string folder = p.Substring(0, slash);
                    if (!folders.Add(folder)) break;
                    slash = folder.LastIndexOf('/');
                }
            }
            if (!any) return null;
            missing.AddRange(folders.Where(f => f.Length > assets.Length - 1 && f != prefix + "Assets" && !set.Contains(f + ".meta")).OrderBy(f => f));
            if (missing.Count == 0)
                return new CheckResult(CheckStatus.Ok, title, "Jede Datei in Assets hat ihre .meta-Datei im Repository.");
            return new CheckResult(CheckStatus.Warn, title,
                missing.Count + " Dateien/Ordner ohne .meta im Repository, z. B.:\n" + string.Join("\n", missing.Take(5).ToArray()),
                ".meta-Dateien immer mit committen. In GitHub Desktop die fehlenden .meta-Dateien anhaken und committen. "
                + "Steht \"*.meta\" in der .gitignore, diese Zeile löschen.");
        }

        static CheckResult Commits(string gitDir)
        {
            const string title = "Commits";
            string commonDir = CommonDir(gitDir);
            string head = ReadOrEmpty(Path.Combine(gitDir, "HEAD")).Trim();
            bool hasCommit;
            if (head.StartsWith("ref:", StringComparison.Ordinal))
            {
                string reference = head.Substring(4).Trim();
                hasCommit = File.Exists(Path.Combine(commonDir, reference.Replace('/', Path.DirectorySeparatorChar)))
                            || ReadOrEmpty(Path.Combine(commonDir, "packed-refs")).Contains(" " + reference);
            }
            else
            {
                hasCommit = head.Length >= 40; // detached HEAD
            }
            return hasCommit
                ? new CheckResult(CheckStatus.Ok, title, "Es gibt mindestens einen Commit.")
                : new CheckResult(CheckStatus.Warn, title, "Noch kein Commit.", "Anleitung „Erster Commit“.");
        }

        static CheckResult Remote(string gitDir)
        {
            const string title = "Mit GitHub verbunden";
            string config = ReadOrEmpty(Path.Combine(CommonDir(gitDir), "config"));
            Match m = Regex.Match(config, @"\[remote ""origin""\][^\[]*?url\s*=\s*(\S+)", RegexOptions.Singleline);
            if (!m.Success)
                return new CheckResult(CheckStatus.Warn, title, "Das Repository ist noch nicht auf GitHub veröffentlicht.",
                    "In GitHub Desktop oben \"Publish repository\" klicken, \"Keep this code private\" angehakt lassen. Anleitung „Erster Commit“.");
            string url = m.Groups[1].Value;
            if (url.IndexOf("github.com", StringComparison.OrdinalIgnoreCase) >= 0)
                return new CheckResult(CheckStatus.Ok, title, url);
            return new CheckResult(CheckStatus.Info, title, "Das Remote ist " + url + " (nicht GitHub). Das funktioniert, wird im Kurs aber nicht behandelt.");
        }

        // ---------------------------------------------------------------- helpers

        static string CommonDir(string gitDir)
        {
            string commonFile = Path.Combine(gitDir, "commondir");
            if (!File.Exists(commonFile)) return gitDir;
            string rel = ReadOrEmpty(commonFile).Trim();
            return Path.IsPathRooted(rel) ? rel : Path.GetFullPath(Path.Combine(gitDir, rel));
        }

        static string ReadOrEmpty(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : ""; }
            catch (IOException) { return ""; }
        }

        static bool SamePath(string a, string b)
        {
            return string.Equals(Path.GetFullPath(a).TrimEnd('/', '\\'), Path.GetFullPath(b).TrimEnd('/', '\\'), StringComparison.OrdinalIgnoreCase);
        }

        static string RelativeTo(string root, string path)
        {
            string r = Path.GetFullPath(root).TrimEnd('/', '\\') + Path.DirectorySeparatorChar;
            string p = Path.GetFullPath(path);
            return p.StartsWith(r, StringComparison.OrdinalIgnoreCase) ? p.Substring(r.Length) : p;
        }
    }
}
