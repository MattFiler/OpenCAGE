#if ENABLE_MOD_PACKAGES
using CATHODE.Scripting.Internal;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace OpenCAGE.Modding
{
    /* Builds the FILE_HASHES table that ships inside CathodeLib's info.dat: point it at a verified
     * clean install, and it hashes everything under DATA into a named set (one per store build -
     * "STEAM_PC" today, EGS/GOG sets can join later). MergeIntoInfoDat folds the result into an
     * existing info.dat without disturbing the other tables, ready to check in as the embedded
     * resource. */
    public static class VanillaManifestGenerator
    {
        public static Dictionary<string, FileHashTable.Entry> HashInstall(string gameRoot, Action<int, int> progress = null, Func<string, bool> include = null)
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

            Dictionary<string, FileHashTable.Entry> entries = new Dictionary<string, FileHashTable.Entry>(files.Count);
            for (int i = 0; i < files.Count; i++)
            {
                string normalised = ModToolkit.NormaliseFull(gameRoot, files[i]);
                entries[normalised] = new FileHashTable.Entry()
                {
                    Path = normalised,
                    Size = new FileInfo(files[i]).Length,
                    Sha256 = ModToolkit.Sha256File(files[i]),
                };
                if (progress != null && ((i + 1) % 25 == 0 || i + 1 == files.Count))
                    progress(i + 1, files.Count);
            }
            return entries;
        }

        /// <summary>
        /// Fold a hash set into an info.dat (the gzipped standalone CustomTable blob), preserving
        /// every other table and any other hash sets already inside. Returns the new gzipped bytes.
        /// </summary>
        public static byte[] MergeIntoInfoDat(byte[] existingGzipped, string setName, Dictionary<string, FileHashTable.Entry> entries)
        {
            byte[] raw;
            using (MemoryStream decompressed = new MemoryStream())
            using (GZipStream gzip = new GZipStream(new MemoryStream(existingGzipped), CompressionMode.Decompress))
            {
                gzip.CopyTo(decompressed);
                raw = decompressed.ToArray();
            }

            FileHashTable table = (FileHashTable)CustomTable.ReadTable(raw, CustomTableType.FILE_HASHES);
            if (table == null)
                table = new FileHashTable();
            table.sets[setName] = entries;

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
    }
}
#endif
