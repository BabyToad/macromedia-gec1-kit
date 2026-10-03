using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Gec1.SetupCheck
{
    /// <summary>
    /// Finds the Git repository around a folder and lists the paths tracked in its index,
    /// without needing a git executable on PATH (GitHub Desktop ships its own git and
    /// does not put it on PATH). Supports index versions 2, 3 and 4.
    /// </summary>
    internal static class GitIndexReader
    {
        /// <summary>Walks up from <paramref name="start"/> to the first folder that contains ".git".</summary>
        public static string FindWorkTreeRoot(string start)
        {
            var dir = new DirectoryInfo(start);
            while (dir != null)
            {
                string dotGit = Path.Combine(dir.FullName, ".git");
                if (Directory.Exists(dotGit) || File.Exists(dotGit))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }

        /// <summary>Resolves the git directory (handles the "gitdir:" file used by worktrees and submodules).</summary>
        public static string ResolveGitDir(string workTreeRoot)
        {
            string dotGit = Path.Combine(workTreeRoot, ".git");
            if (Directory.Exists(dotGit))
                return dotGit;
            if (!File.Exists(dotGit))
                return null;
            foreach (string line in File.ReadAllLines(dotGit))
            {
                if (!line.StartsWith("gitdir:", StringComparison.Ordinal))
                    continue;
                string target = line.Substring("gitdir:".Length).Trim();
                if (!Path.IsPathRooted(target))
                    target = Path.GetFullPath(Path.Combine(workTreeRoot, target));
                return Directory.Exists(target) ? target : null;
            }
            return null;
        }

        /// <summary>
        /// Returns the tracked paths ('/'-separated, relative to the work tree root),
        /// an empty list if there is no index yet, or null if the index cannot be read.
        /// </summary>
        public static List<string> ReadTrackedPaths(string gitDir, out string error)
        {
            error = null;
            string indexPath = Path.Combine(gitDir, "index");
            if (!File.Exists(indexPath))
                return new List<string>();
            try
            {
                byte[] data = File.ReadAllBytes(indexPath);
                return Parse(data, HashLength(gitDir));
            }
            catch (Exception e)
            {
                error = e.Message;
                return null;
            }
        }

        static int HashLength(string gitDir)
        {
            try
            {
                string config = File.ReadAllText(Path.Combine(gitDir, "config"));
                if (config.IndexOf("objectformat = sha256", StringComparison.OrdinalIgnoreCase) >= 0)
                    return 32;
            }
            catch (IOException) { }
            return 20;
        }

        static List<string> Parse(byte[] d, int hashLength)
        {
            if (d.Length < 12 || d[0] != 'D' || d[1] != 'I' || d[2] != 'R' || d[3] != 'C')
                throw new InvalidDataException("Keine gültige Git-Indexdatei.");
            uint version = ReadU32(d, 4);
            uint count = ReadU32(d, 8);
            if (version < 2 || version > 4)
                throw new InvalidDataException("Unbekannte Git-Indexversion " + version + ".");

            var paths = new List<string>((int)Math.Min(count, 200000));
            int pos = 12;
            string previous = "";
            for (uint i = 0; i < count; i++)
            {
                int start = pos;
                pos += 40 + hashLength;           // stat data + object hash
                ushort flags = ReadU16(d, pos);
                pos += 2;
                if (version >= 3 && (flags & 0x4000) != 0)
                    pos += 2;                     // extended flags
                int fixedLength = pos - start;

                string path;
                if (version == 4)
                {
                    int strip = ReadOffsetVarint(d, ref pos);
                    int end = Array.IndexOf(d, (byte)0, pos);
                    if (end < 0) throw new InvalidDataException("Index abgeschnitten.");
                    string suffix = Encoding.UTF8.GetString(d, pos, end - pos);
                    string prefix = previous.Substring(0, Math.Max(0, previous.Length - strip));
                    path = prefix + suffix;
                    pos = end + 1;
                }
                else
                {
                    int end = Array.IndexOf(d, (byte)0, pos);
                    if (end < 0) throw new InvalidDataException("Index abgeschnitten.");
                    int nameLength = end - pos;
                    path = Encoding.UTF8.GetString(d, pos, nameLength);
                    pos = start + ((fixedLength + nameLength + 8) & ~7);
                }
                paths.Add(path);
                previous = path;
                if (pos > d.Length) throw new InvalidDataException("Index abgeschnitten.");
            }
            return paths;
        }

        // Git's "offset" varint (see varint.c): big-endian groups of 7 bits with an implicit +1 per continuation.
        static int ReadOffsetVarint(byte[] d, ref int pos)
        {
            byte c = d[pos++];
            int value = c & 0x7f;
            while ((c & 0x80) != 0)
            {
                value += 1;
                c = d[pos++];
                value = (value << 7) + (c & 0x7f);
            }
            return value;
        }

        static uint ReadU32(byte[] d, int p)
        {
            return (uint)(d[p] << 24 | d[p + 1] << 16 | d[p + 2] << 8 | d[p + 3]);
        }

        static ushort ReadU16(byte[] d, int p)
        {
            return (ushort)(d[p] << 8 | d[p + 1]);
        }
    }
}
