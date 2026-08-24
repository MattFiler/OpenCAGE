using System;
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
                string temp = path + ".tmp";
                using (FileStream file = File.Create(temp))
                using (GZipStream gzip = new GZipStream(file, CompressionMode.Compress))
                    gzip.Write(content, 0, content.Length);
                if (File.Exists(path)) File.Delete(temp);
                else File.Move(temp, path);
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
