using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// Xiph's own libvorbis, used to decode the rebuilt Ogg streams.
    ///
    /// The managed decoders were tried first and all of them get 5.1 audio wrong: NVorbis 0.7.6, 0.10.5
    /// and 1.0.0-rc.2 each produce full-scale noise on surround content the moment it gets loud, at the
    /// identical sample, while decoding the same bytes' mono and stereo perfectly. The reference decoder
    /// handles the same streams cleanly, and around a third of the game's sounds are 5.1, so the native
    /// library is the only option that actually plays the game's audio.
    ///
    /// The DLLs are embedded and unpacked on first use, so nothing has to sit beside the executable.
    /// </summary>
    internal static class VorbisNative
    {
        private const string VorbisFile = "vorbisfile.dll";

        private static readonly object _lock = new object();
        private static bool _loaded;
        private static string _failure;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string path);

        /// <summary>
        /// Unpack and load the native library, once. Throws with a readable message if it can't be
        /// loaded, since without it there is no preview at all.
        /// </summary>
        public static void EnsureLoaded()
        {
            lock (_lock)
            {
                if (_loaded)
                    return;

                if (_failure != null)
                    throw new InvalidOperationException(_failure);

                try
                {
                    string directory = Unpack();

                    //Load in dependency order - vorbisfile needs vorbis, which needs ogg. Loading them
                    //by full path up front means the later DllImports resolve to these copies rather
                    //than searching the system.
                    foreach (string name in new[] { "ogg.dll", "vorbis.dll", "vorbisfile.dll" })
                    {
                        string path = Path.Combine(directory, name);
                        if (LoadLibrary(path) == IntPtr.Zero)
                            throw new IOException("Could not load " + name + " (error " + Marshal.GetLastWin32Error() + ").");
                    }

                    _loaded = true;
                }
                catch (Exception e)
                {
                    _failure = "The audio decoder could not be loaded: " + e.Message;
                    throw new InvalidOperationException(_failure, e);
                }
            }
        }

        private static string Unpack()
        {
            //Named this way to match how the native model importer is laid out, and because a plain
            //"x64" folder is caught by the repository's build-output ignore rules. The build turns the
            //hyphen into an underscore when it makes the resource name, so the two differ.
            bool sixtyFour = IntPtr.Size == 8;
            string architecture = sixtyFour ? "win-x64" : "win-x86";
            string resourceArchitecture = sixtyFour ? "win_x64" : "win_x86";

            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenCAGE", "Native", architecture);

            Directory.CreateDirectory(directory);

            Assembly assembly = Assembly.GetExecutingAssembly();
            foreach (string name in new[] { "ogg.dll", "vorbis.dll", "vorbisfile.dll" })
            {
                string resource = "OpenCAGE.Resources.Native." + resourceArchitecture + "." + name;
                string path = Path.Combine(directory, name);

                using (Stream stream = assembly.GetManifestResourceStream(resource))
                {
                    if (stream == null)
                        throw new FileNotFoundException("This build is missing " + resource + ".");

                    //Only rewrite when the file isn't already the right size - the previous copy may be
                    //loaded by another instance of the editor, which would make it unwritable
                    if (File.Exists(path) && new FileInfo(path).Length == stream.Length)
                        continue;

                    try
                    {
                        using (FileStream output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                            stream.CopyTo(output);
                    }
                    catch (IOException)
                    {
                        if (!File.Exists(path))
                            throw;
                    }
                }
            }

            return directory;
        }

        #region VORBISFILE

        /// <summary>
        /// The callbacks vorbisfile uses to pull bytes. Using these rather than ov_fopen keeps the
        /// decode entirely in memory, so nothing is written to disk to play a sound.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct OvCallbacks
        {
            public ReadFunc Read;
            public SeekFunc Seek;
            public CloseFunc Close;
            public TellFunc Tell;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate UIntPtr ReadFunc(IntPtr buffer, UIntPtr size, UIntPtr count, IntPtr source);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int SeekFunc(IntPtr source, long offset, int whence);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int CloseFunc(IntPtr source);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int TellFunc(IntPtr source);

        [DllImport(VorbisFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ov_open_callbacks(IntPtr source, IntPtr file, IntPtr initial, IntPtr initialBytes, OvCallbacks callbacks);

        [DllImport(VorbisFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ov_clear(IntPtr file);

        [DllImport(VorbisFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ov_info(IntPtr file, int link);

        [DllImport(VorbisFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern long ov_pcm_total(IntPtr file, int link);

        [DllImport(VorbisFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ov_read_float(IntPtr file, out IntPtr channels, int samples, out int bitstream);

        /// <summary>
        /// vorbis_info as libvorbis lays it out. Only the channel count and sample rate are needed, and
        /// both sit at the front, ahead of anything whose size varies.
        /// </summary>
        public static void ReadInfo(IntPtr info, out int channels, out int sampleRate)
        {
            channels = Marshal.ReadInt32(info, 4);
            sampleRate = Marshal.ReadInt32(info, 8);
        }

        /// <summary>
        /// Room for an OggVorbis_File. The real structure is a few hundred bytes and its size depends on
        /// the build, so this deliberately over-allocates rather than trying to mirror it exactly.
        /// </summary>
        public const int FileHandleSize = 2048;

        #endregion
    }
}
