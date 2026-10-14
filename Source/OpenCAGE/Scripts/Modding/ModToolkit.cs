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
        public const string PackageExtension = PackageFiles.ModExtension;

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

        /* Files under DATA that the game or a tool writes as throwaway RUNTIME output, which must
         * never count as "modified" or end up inside a package. Prefix match against normalised
         * paths.
         *
         * Note the distinction: content OpenCAGE GENERATES is not runtime output and does belong
         * in a package. The .META sidecars are the case that makes the difference - they carry the
         * Commands custom tables and a level's radiosity ownership marker, which a mod depending
         * on them needs at the far end. They ship. */
        private static readonly string[] _excludedPrefixes =
        {
            "DATA/MODTOOLS/",
            "DATA/CACHE/",
            "DATA/DEV/",
            "DATA/BINARY_PIPES/",
            "DATA/LOGS/",
            //OpenCAGE copies its debug text fonts here on every start (Program.cs): every install has them, no mod carries them
            "DATA/DEBUG_FONT/",
        };
        private static readonly string[] _excludedFiles =
        {
            "DATA/ENGINE_SETTINGS_SAVEFILE.BIN",
            "DATA/IP.DEV",
            "DATA/UIFILECACHELIST.TXT",
            "DATA/UPDATEDBUILTDATA.TXT",
        };
        /* Nothing is excluded by suffix. .META used to be, on the reading that OpenCAGE sidecars
         * were tooling artifacts - but they are generated CONTENT (custom tables, the radiosity
         * ownership marker), a vanilla install ships none of them, and a mod that leaves them
         * behind arrives incomplete. Don't put it back without that in mind. */
        private static readonly string[] _excludedSuffixes = { };

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

        public const string LevelListPath = "DATA/PACKAGES/MAIN.PKG";

        /// <summary>
        /// Files a package never carries and the installer never takes from one, because they're worked out from
        /// the rest after every install: the custom level list in MAIN.PKG, and each level's behaviour tree list
        /// (from the character configs). Also COMMANDS.BIN, which OpenCAGE writes beside COMMANDS.PAK for the 2014
        /// development builds - the game itself only reads the PAK, and the BIN is 20 MB of every level mod.
        /// </summary>
        public static bool IsRegenerated(string normalisedPath)
        {
            return normalisedPath == LevelListPath
                || normalisedPath.EndsWith("/WORLD/BEHAVIOR_TREE.DB")
                || normalisedPath.EndsWith("/WORLD/COMMANDS.BIN");
        }

        /// <summary>The level folder of a level name: DATA/ENV/PRODUCTION/BSP_TORRENS/.</summary>
        public static string LevelFolder(string level)
        {
            return "DATA/ENV/PRODUCTION/" + level + "/";
        }

        /// <summary>
        /// Paths whose change can alter which behaviour trees each level needs: the character configs and the
        /// compiled trees themselves.
        /// </summary>
        public static bool AffectsBehaviourTrees(string normalisedPath)
        {
            return normalisedPath.StartsWith("DATA/CHR_INFO") || normalisedPath.StartsWith("DATA/BINARY_BEHAVIOR/");
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

        /// <summary>
        /// A SHA-256 hasher: Windows' own, which uses the processor's SHA instructions where it has them (several times
        /// faster than the managed one .NET Framework gives by default), or the default where Windows' can't be had - in
        /// .NET 5 and later, the default is Windows' own.
        /// </summary>
        public static SHA256 NewSha256()
        {
            try
            {
                if (_windowsSha256 != null && Activator.CreateInstance(_windowsSha256) is SHA256 sha)
                    return sha;
            }
            catch { }
            return SHA256.Create();
        }
        //In System.Core with LINQ in .NET Framework; elsewhere, not found there
        private static readonly Type _windowsSha256 = typeof(System.Linq.Enumerable).Assembly.GetType("System.Security.Cryptography.SHA256Cng", false);

        public static byte[] Sha256(byte[] content)
        {
            using (SHA256 sha = NewSha256())
                return sha.ComputeHash(content);
        }

        public static byte[] Sha256File(string path)
        {
            using (SHA256 sha = NewSha256())
                return Sha256File(sha, path, new byte[1 << 20]);
        }

        /// <summary>
        /// A file's hash, read through the given buffer, telling <paramref name="read"/> each run of bytes read. Nothing else is
        /// kept from writing, moving or deleting the file meanwhile (a check of the game files runs beside saves and applies):
        /// whoever records the hash makes sure the file didn't change while it was read.
        /// </summary>
        public static byte[] Sha256File(SHA256 sha, string path, byte[] buffer, Action<long> read = null)
        {
            sha.Initialize();
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan))
            {
                int count;
                while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    sha.TransformBlock(buffer, 0, count, null, 0);
                    read?.Invoke(count);
                }
            }
            sha.TransformFinalBlock(buffer, 0, 0);
            return sha.Hash;
        }

        /// <summary>
        /// How many files to read at once from the drive the game is on: one from a spinning disk, which slows down reading
        /// several (each read has to wait for the head to move), and one per processor core, up to 8, from anything else -
        /// hashing is what's slow there. Two when the drive can't say which it is.
        /// </summary>
        public static int ReadersFor(string gameRoot)
        {
            int cores = Math.Max(1, Math.Min(Environment.ProcessorCount, 8));
            switch (SeeksOnRead(gameRoot))
            {
                case true: return 1;
                case false: return cores;
                default: return Math.Min(2, cores);
            }
        }

        private static readonly Dictionary<string, bool?> _seeks = new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase);

        /* Whether the drive holding a folder is a spinning disk (Windows says whether reading from it "incurs a seek penalty"),
           or null when it can't say - a network share, a drive made of several disks */
        private static bool? SeeksOnRead(string folder)
        {
            string root;
            try { root = Path.GetPathRoot(Path.GetFullPath(folder)); }
            catch { return null; }
            if (string.IsNullOrEmpty(root) || root.Length < 2 || root[1] != ':')
                return null;
            lock (_seeks)
            {
                if (_seeks.TryGetValue(root, out bool? known))
                    return known;
                bool? seeks = null;
                try
                {
                    using (Microsoft.Win32.SafeHandles.SafeFileHandle volume = CreateFile(@"\\.\" + root.Substring(0, 2), 0, 3 /* share read, write */, IntPtr.Zero, 3 /* open existing */, 0, IntPtr.Zero))
                    {
                        //STORAGE_PROPERTY_QUERY { StorageDeviceSeekPenaltyProperty (7), PropertyStandardQuery (0) } -> DEVICE_SEEK_PENALTY_DESCRIPTOR
                        byte[] query = new byte[12];
                        query[0] = 7;
                        byte[] answer = new byte[12];
                        if (!volume.IsInvalid && DeviceIoControl(volume, 0x2D1400 /* IOCTL_STORAGE_QUERY_PROPERTY */, query, query.Length, answer, answer.Length, out int returned, IntPtr.Zero) && returned >= 9)
                            seeks = answer[8] != 0;
                    }
                }
                catch { }
                _seeks[root] = seeks;
                return seeks;
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(Microsoft.Win32.SafeHandles.SafeFileHandle device, uint code, byte[] input, int inputSize, byte[] output, int outputSize, out int returned, IntPtr overlapped);

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
