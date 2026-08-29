#if ENABLE_MOD_PACKAGES
using CATHODE.Scripting.Internal;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace OpenCAGE.Modding
{
    /* Builds the FILE_HASHES table that ships inside CathodeLib's info.dat: point it at verified
     * clean installs and it hashes everything under DATA, folding each install in under its
     * CathodeLib.PatchManager.Platform identity. Identical bytes across builds share one stored
     * hash with the platforms OR'd into its bitmask (FileHashTable.Merge), so the table grows by
     * distinct content, not by platform count. MergeIntoInfoDat folds the result into an existing
     * info.dat without disturbing the other tables, ready to check in as the embedded resource. */
    public static class VanillaManifestGenerator
    {
        public class HashedFile
        {
            public string NormalisedPath;
            public long Size;
            public byte[] Sha256;
        }

        /// <summary>
        /// Hash every included file under gameRoot/DATA. The optional include filter sees the
        /// full on-disk path.
        /// </summary>
        public static List<HashedFile> HashInstall(string gameRoot, Action<int, int> progress = null, Func<string, bool> include = null)
        {
            string dataDir = Path.Combine(gameRoot, "DATA");
            if (!Directory.Exists(dataDir))
                throw new Exception("No DATA folder under " + gameRoot);

            List<string> files = new List<string>();
            foreach (string file in Directory.GetFiles(dataDir, "*", SearchOption.AllDirectories))
            {
                string normalised = ModToolkit.NormaliseFull(gameRoot, file);
                if (normalised != null && !ModToolkit.IsExcluded(normalised) && (include == null || include(file)))
                    files.Add(file);
            }
            files.Sort();

            List<HashedFile> hashed = new List<HashedFile>(files.Count);
            for (int i = 0; i < files.Count; i++)
            {
                hashed.Add(new HashedFile()
                {
                    NormalisedPath = ModToolkit.NormaliseFull(gameRoot, files[i]),
                    Size = new FileInfo(files[i]).Length,
                    Sha256 = ModToolkit.Sha256File(files[i]),
                });
                if (progress != null && ((i + 1) % 25 == 0 || i + 1 == files.Count))
                    progress(i + 1, files.Count);
            }
            return hashed;
        }

        /// <summary>
        /// Fold a hashed install into the table under the given platform mask (usually one
        /// FileHashTable.PlatformBit, but a mask covers verified-identical builds - e.g. Steam's
        /// bytes standing in for EGS and GOG). Reports how many files matched an existing
        /// variant (shared bytes, mask OR'd on) versus adding a new one.
        /// </summary>
        public static void MergeInstall(FileHashTable table, int platformMask, List<HashedFile> hashed, out int shared, out int added)
        {
            shared = 0;
            added = 0;
            foreach (HashedFile file in hashed)
            {
                string normalised = FileHashTable.NormalisePath(file.NormalisedPath);
                bool existed = false;
                List<FileHashTable.Entry> variants;
                if (table.files.TryGetValue(normalised, out variants))
                    foreach (FileHashTable.Entry variant in variants)
                        if (variant.SameContent(file.Size, file.Sha256)) { existed = true; break; }
                table.Merge(platformMask, file.NormalisedPath, file.Size, file.Sha256);
                if (existed) shared++; else added++;
            }
        }

        /// <summary>
        /// Fold the table into an info.dat (the gzipped standalone CustomTable blob), preserving
        /// every other table inside. Returns the new gzipped bytes.
        /// </summary>
        public static byte[] MergeIntoInfoDat(byte[] existingGzipped, FileHashTable table)
        {
            byte[] raw;
            using (MemoryStream decompressed = new MemoryStream())
            using (GZipStream gzip = new GZipStream(new MemoryStream(existingGzipped), CompressionMode.Decompress))
            {
                gzip.CopyTo(decompressed);
                raw = decompressed.ToArray();
            }

            /* WriteTable reads every other table out of the file before rewriting, so routing
             * through a temp file keeps them all */
            string temp = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(temp, raw);
                CustomTable.WriteTable(temp, CustomTableType.FILE_HASHES, table);
                raw = File.ReadAllBytes(temp);
            }
            finally
            {
                File.Delete(temp);
            }

            using (MemoryStream recompressed = new MemoryStream())
            {
                using (GZipStream gzip = new GZipStream(recompressed, CompressionMode.Compress, true))
                    gzip.Write(raw, 0, raw.Length);
                return recompressed.ToArray();
            }
        }

        /// <summary>
        /// The FILE_HASHES table currently inside an info.dat blob, or a fresh one.
        /// </summary>
        public static FileHashTable ReadFromInfoDat(byte[] existingGzipped)
        {
            byte[] raw;
            using (MemoryStream decompressed = new MemoryStream())
            using (GZipStream gzip = new GZipStream(new MemoryStream(existingGzipped), CompressionMode.Decompress))
            {
                gzip.CopyTo(decompressed);
                raw = decompressed.ToArray();
            }
            FileHashTable table = (FileHashTable)CustomTable.ReadTable(raw, CustomTableType.FILE_HASHES);
            return table ?? new FileHashTable();
        }
    }
}
#endif
