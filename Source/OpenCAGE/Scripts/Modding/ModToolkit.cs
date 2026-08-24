using CathodeLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace OpenCAGE.Modding
{
    /* Shared ground for the mod packaging system: where things live inside the game folder, which
     * files are ours to reason about, and the hashing every other class leans on.
     *
     * Everything in this namespace takes the game root as a constructor argument rather than
     * reaching for Singleton - the classes are exercised outside the editor by test harnesses. */
    public static class ModToolkit
    {
        public const string PackageExtension = ".opencage";

        public static byte[] Gzip(byte[] content)
        {
            using (MemoryStream result = new MemoryStream())
            {
                using (System.IO.Compression.GZipStream gzip = new System.IO.Compression.GZipStream(result, System.IO.Compression.CompressionLevel.Optimal, true))
                    gzip.Write(content, 0, content.Length);
                return result.ToArray();
            }
        }

        public static byte[] Gunzip(byte[] content)
        {
            using (MemoryStream result = new MemoryStream())
            using (System.IO.Compression.GZipStream gzip = new System.IO.Compression.GZipStream(new MemoryStream(content), System.IO.Compression.CompressionMode.Decompress))
            {
                gzip.CopyTo(result);
                return result.ToArray();
            }
        }

        /* Paths, all relative to the game root */
        public static string ModsDir(string gameRoot) { return Path.Combine(gameRoot, "DATA", "MODTOOLS", "MODS"); }
        public static string LibraryDir(string gameRoot) { return Path.Combine(ModsDir(gameRoot), "LIBRARY"); }
        public static string BaselineDir(string gameRoot) { return Path.Combine(ModsDir(gameRoot), "BASELINE"); }
        public static string SnapshotsDir(string gameRoot) { return Path.Combine(ModsDir(gameRoot), "SNAPSHOTS"); }
        public static string StateFile(string gameRoot) { return Path.Combine(ModsDir(gameRoot), "state.json"); }
        public static string HashCacheFile(string gameRoot) { return Path.Combine(ModsDir(gameRoot), "hashcache.json"); }
        public static string JournalFile(string gameRoot) { return Path.Combine(ModsDir(gameRoot), "journal.json"); }

        /// <summary>
        /// The canonical spelling for a game file in manifests, claims and state: relative to the
        /// game root, forward slashes, uppercase.
        /// </summary>
        public static string Normalise(string path)
        {
            return FileHashTable.NormalisePath(path);
        }

        /// <summary>
        /// Normalise an absolute path against the game root. Returns null if it isn't under the root.
        /// </summary>
        public static string NormaliseFull(string gameRoot, string fullPath)
        {
            string root = Path.GetFullPath(gameRoot).Replace('\\', '/').TrimEnd('/');
            string full = Path.GetFullPath(fullPath).Replace('\\', '/');
            if (!full.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
                return null;
            return Normalise(full.Substring(root.Length + 1));
        }

        public static string Denormalise(string gameRoot, string normalisedPath)
        {
            return Path.Combine(gameRoot, normalisedPath.Replace('/', Path.DirectorySeparatorChar));
        }

        /* Files under DATA that the game or tooling writes at runtime, which must never count as
         * "modified" or end up inside a package. Prefix match against normalised paths. */
        private static readonly string[] _excludedPrefixes =
        {
            "DATA/MODTOOLS/",
            "DATA/CACHE/",
            "DATA/DEV/",
            "DATA/BINARY_PIPES/",
            "DATA/LOGS/",
        };
        private static readonly string[] _excludedFiles =
        {
            "DATA/ENGINE_SETTINGS_SAVEFILE.BIN",
            "DATA/IP.DEV",
            "DATA/UIFILECACHELIST.TXT",
            "DATA/UPDATEDBUILTDATA.TXT",
        };
        private static readonly string[] _excludedSuffixes =
        {
            ".META", //OpenCAGE sidecars for custom tables
        };

        public static bool IsExcluded(string normalisedPath)
        {
            //Per-level DEV folders are runtime log output from the game - never vanilla data, never mod content
            if (normalisedPath.Contains("/DEV/"))
                return true;
            for (int i = 0; i < _excludedPrefixes.Length; i++)
                if (normalisedPath.StartsWith(_excludedPrefixes[i]))
                    return true;
            for (int i = 0; i < _excludedFiles.Length; i++)
                if (normalisedPath == _excludedFiles[i])
                    return true;
            for (int i = 0; i < _excludedSuffixes.Length; i++)
                if (normalisedPath.EndsWith(_excludedSuffixes[i]))
                    return true;
            return false;
        }

        /// <summary>
        /// The level a path belongs to ("BSP_TORRENS", "DLC/BSPNOSTROMO_RIPLEY"), or null for
        /// files outside the ENV level folders.
        /// </summary>
        public static string LevelOf(string normalisedPath)
        {
            const string prefix = "DATA/ENV/PRODUCTION/";
            if (!normalisedPath.StartsWith(prefix))
                return null;
            string rest = normalisedPath.Substring(prefix.Length);
            int slash = rest.IndexOf('/');
            if (slash < 0)
                return null;
            string level = rest.Substring(0, slash);
            if (level == "DLC")
            {
                int slash2 = rest.IndexOf('/', slash + 1);
                if (slash2 < 0)
                    return null;
                level = rest.Substring(0, slash2);
            }
            return level;
        }

        public static byte[] Sha256(byte[] content)
        {
            using (SHA256 sha = SHA256.Create())
                return sha.ComputeHash(content);
        }

        public static byte[] Sha256File(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return sha.ComputeHash(stream);
        }

        public static string ToHex(byte[] hash)
        {
            char[] result = new char[hash.Length * 2];
            for (int i = 0; i < hash.Length; i++)
            {
                result[i * 2] = "0123456789abcdef"[hash[i] >> 4];
                result[i * 2 + 1] = "0123456789abcdef"[hash[i] & 0xF];
            }
            return new string(result);
        }

        public static byte[] FromHex(string hex)
        {
            byte[] result = new byte[hex.Length / 2];
            for (int i = 0; i < result.Length; i++)
                result[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return result;
        }
    }
}
