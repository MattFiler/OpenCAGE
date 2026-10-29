#if ENABLE_MOD_PACKAGES
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace OpenCAGE.Modding
{
    /* Content-addressed byte store under DATA/MODTOOLS/MODS/BASELINE: files named by the sha256 of
     * their content, gzipped. Holds captured vanilla bytes, pre-verify snapshots and transaction
     * journal content - identical bytes are stored once no matter how many roles they play. */
    public class BaselineStore
    {
        private readonly string _dir;

        public BaselineStore(string gameRoot)
        {
            _dir = ModToolkit.BaselineDir(gameRoot);
        }

        private string PathOf(string shaHex)
        {
            return Path.Combine(_dir, shaHex + ".gz");
        }

        public bool Has(string shaHex)
        {
            return shaHex != null && File.Exists(PathOf(shaHex));
        }

        /// <summary>
        /// Store content, returning its sha256 hex. A second store of the same bytes is free.
        /// </summary>
        public string Store(byte[] content)
        {
            string shaHex = ModToolkit.ToHex(ModToolkit.Sha256(content));
            string path = PathOf(shaHex);
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(_dir);
                //A temp of its own: the same bytes can be being stored twice at once (a level's files are captured several at a
                //time), and whichever lands second finds the first's - the same bytes, the same name - and leaves it be
                string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                using (FileStream file = File.Create(temp))
                using (GZipStream gzip = new GZipStream(file, CompressionMode.Compress))
                    gzip.Write(content, 0, content.Length);
                if (File.Exists(path)) File.Delete(temp);
                else
                {
                    try { File.Move(temp, path); }
                    catch (IOException) when (File.Exists(path)) { File.Delete(temp); }
                }
            }
            return shaHex;
        }

        /// <summary>
        /// Stream a file into the store without holding it all in memory twice.
        /// </summary>
        public string StoreFile(string filePath)
        {
            return Store(File.ReadAllBytes(filePath));
        }

        /// <summary>
        /// Delete everything held but the given hashes (and any half-written leftovers). Returns the bytes freed.
        /// </summary>
        public long Prune(HashSet<string> keep)
        {
            long freed = 0;
            if (!Directory.Exists(_dir))
                return 0;
            foreach (string file in Directory.GetFiles(_dir))
            {
                string name = Path.GetFileName(file);
                if (name.EndsWith(".gz") && keep.Contains(name.Substring(0, name.Length - 3)))
                    continue;
                try
                {
                    long size = new FileInfo(file).Length;
                    File.Delete(file);
                    freed += size;
                }
                catch { }
            }
            return freed;
        }

        /// <summary>
        /// Content back out by hash, or null if the store doesn't hold it.
        /// </summary>
        public byte[] Retrieve(string shaHex)
        {
            string path = PathOf(shaHex);
            if (!File.Exists(path))
                return null;
            using (MemoryStream result = new MemoryStream())
            using (FileStream file = File.OpenRead(path))
            using (GZipStream gzip = new GZipStream(file, CompressionMode.Decompress))
            {
                gzip.CopyTo(result);
                byte[] content = result.ToArray();

                //The store is only trustworthy if the name still matches the bytes
                if (ModToolkit.ToHex(ModToolkit.Sha256(content)) != shaHex)
                    return null;
                return content;
            }
        }
    }
}
#endif
