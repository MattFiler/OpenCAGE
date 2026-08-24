using CATHODE;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace OpenCAGE.Modding
{
    /* The vanilla bytes of the config files, from the same embedded archive ResetConfigs restores
     * from. This is the diff base for value-level config exports when the baseline store doesn't
     * hold a captured copy (it usually will, but the embedded set predates any install). */
    public static class VanillaConfigs
    {
        private static PAK2 _archive;
        private static readonly object _lock = new object();

        private static PAK2 Archive
        {
            get
            {
                lock (_lock)
                {
                    if (_archive == null)
                    {
                        using (MemoryStream stream = new MemoryStream())
                        using (GZipStream compressed = new GZipStream(new MemoryStream(Properties.Resources.config_backups), CompressionMode.Decompress))
                        {
                            compressed.CopyTo(stream);
                            _archive = new PAK2(stream.ToArray());
                        }
                    }
                    return _archive;
                }
            }
        }

        /// <summary>
        /// Vanilla bytes for a config file by its normalised game path ("DATA/GBL_ITEM.BML"), or
        /// null when the embedded set doesn't carry it.
        /// </summary>
        public static byte[] Get(string normalisedPath)
        {
            try
            {
                if (!normalisedPath.StartsWith("DATA/"))
                    return null;
                string relative = normalisedPath.Substring("DATA/".Length);
                PAK2.File entry = Archive.Entries.FirstOrDefault(o =>
                    ModToolkit.Normalise(o.Filename) == relative);
                return entry == null ? null : entry.Content;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Vanilla config bytes from wherever they can be had: the captured baseline first, the
        /// embedded archive second.
        /// </summary>
        public static byte[] GetBest(string normalisedPath)
        {
            byte[] captured = ModServices.Installer == null ? null : ModServices.Installer.VanillaBytes(normalisedPath);
            return captured ?? Get(normalisedPath);
        }
    }
}
