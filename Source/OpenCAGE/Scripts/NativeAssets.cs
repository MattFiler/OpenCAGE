using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace OpenCAGE
{
    /// <summary>
    /// The native binaries OpenCAGE ships alongside itself - decoder libraries and the texture
    /// conversion tools - which live in a "Native" folder beside the executable.
    ///
    /// They used to be carried inside the executable and written out on first use. Shipping them as
    /// ordinary files is cheaper in every direction: nothing is held in memory to write them, nothing
    /// is written to a folder that may not be writable, and a patch that changes one of them ships
    /// that one file rather than the whole executable.
    /// </summary>
    internal static class NativeAssets
    {
        private static readonly object _lock = new object();
        private static List<string> _roots;

        /// <summary>
        /// The full path to one of the shipped binaries.
        /// </summary>
        /// <param name="folder">
        /// The folder under Native, e.g. "tools" or "win-x64".
        /// </param>
        public static string Locate(string folder, string fileName)
        {
            foreach (string root in Roots())
            {
                string path = Path.Combine(root, folder, fileName);
                if (File.Exists(path)) return path;
            }
            throw new FileNotFoundException("This build is missing Native\\" + folder + "\\" + fileName + ".",
                                            Path.Combine(folder, fileName));
        }

        /* Beside the executable, which is where the build puts them and where an install has them.
         * The others are for code driven from outside the editor - a test harness that references
         * OpenCAGE runs from its own output folder, and only the assembly it borrowed the code from
         * knows where the binaries were shipped to. */
        private static List<string> Roots()
        {
            lock (_lock)
            {
                if (_roots != null) return _roots;

                _roots = new List<string>();
                Add(AppDomain.CurrentDomain.BaseDirectory);
                Add(Beside(Assembly.GetEntryAssembly()));
                Add(Beside(Assembly.GetExecutingAssembly()));
                Add(Environment.CurrentDirectory);
                return _roots;
            }
        }

        private static void Add(string directory)
        {
            if (string.IsNullOrEmpty(directory)) return;
            string root = Path.Combine(directory, "Native");
            if (!_roots.Contains(root)) _roots.Add(root);
        }

        private static string Beside(Assembly assembly)
        {
            try
            {
                string location = assembly?.Location;
                return string.IsNullOrEmpty(location) ? null : Path.GetDirectoryName(location);
            }
            catch { return null; }
        }
    }
}
