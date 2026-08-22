using System;
using System.IO;
using System.Reflection;

namespace OpenCAGE
{
    /// <summary>
    /// The native binaries OpenCAGE carries inside itself - decoder libraries and the texture
    /// conversion tools - unpacked to disk on first use.
    ///
    /// They live in a "Native" folder beside the executable, so removing OpenCAGE is removing its
    /// folder and nothing is left behind anywhere else. The settings file is written there too, so
    /// somewhere unwritable is already a broken install; even so, a read-only folder falls back to
    /// local application data rather than leaving the feature dead.
    /// </summary>
    internal static class NativeAssets
    {
        private static readonly object _lock = new object();
        private static string _root;

        /// <summary>
        /// Unpack one embedded binary if it isn't already on disk, and hand back its full path.
        /// </summary>
        /// <param name="folder">
        /// The resource folder under Resources\Native, e.g. "tools" or "win-x64". The build turns
        /// the hyphen in a name like "win-x64" into an underscore when it makes the resource name,
        /// so that spelling differs from the folder's.
        /// </param>
        public static string Unpack(string folder, string fileName)
        {
            string directory = Path.Combine(Root(), folder);
            Directory.CreateDirectory(directory);

            string path = Path.Combine(directory, fileName);
            string resource = "OpenCAGE.Resources.Native." + folder.Replace('-', '_') + "." + fileName;

            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resource))
            {
                /* A copy already on disk stands in for a missing resource. That covers dropping a
                 * newer texconv in beside the old one, and lets the conversion code be exercised
                 * outside the editor, where there is no OpenCAGE assembly to read resources from. */
                if (stream == null)
                {
                    if (File.Exists(path)) return path;
                    throw new FileNotFoundException("This build is missing " + resource + ".");
                }

                /* Only rewrite when what's there isn't already the right size. The previous copy may
                 * be loaded by another instance of the editor, which makes it unwritable - and an
                 * identical file doesn't need writing anyway. */
                if (File.Exists(path) && new FileInfo(path).Length == stream.Length)
                    return path;

                try
                {
                    using (FileStream output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                        stream.CopyTo(output);
                }
                catch (IOException)
                {
                    if (!File.Exists(path)) throw;
                }
            }
            return path;
        }

        /* Beside the executable where it can be, local application data where it can't. Decided once
         * by actually writing something, because a folder's permissions don't tell the whole story -
         * a virtual store or a read-only mount both look fine until the write fails. */
        private static string Root()
        {
            lock (_lock)
            {
                if (_root != null) return _root;

                string beside = null;
                try
                {
                    string executable = Assembly.GetExecutingAssembly().Location;
                    if (!string.IsNullOrEmpty(executable))
                        beside = Path.Combine(Path.GetDirectoryName(executable), "Native");
                }
                catch { }

                if (beside != null && IsWritable(beside)) return _root = beside;

                return _root = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenCAGE", "Native");
            }
        }

        private static bool IsWritable(string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);
                string probe = Path.Combine(directory, "." + Guid.NewGuid().ToString("N") + ".tmp");
                using (FileStream stream = new FileStream(probe, FileMode.Create, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
                    stream.WriteByte(0);
                return true;
            }
            catch { return false; }
        }
    }
}
