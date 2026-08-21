using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenCAGE.Audio
{
    /// <summary>
    /// Keeps one index of the game's Wwise content alive for the editor to preview against.
    ///
    /// Building it walks every soundbank in the game, so it happens once, off the UI thread, and is
    /// thrown away only when the loaded level changes - a level can bring its own sound package with it,
    /// and that package has to win over the shipped audio for the level it belongs to.
    /// </summary>
    public static class SoundPreviewLibrary
    {
        private static readonly object _lock = new object();
        private static Task<WwiseSoundLibrary> _building;
        private static string _key;

        /// <summary>Raised when the index finishes building, on the thread that built it.</summary>
        public static event Action<WwiseSoundLibrary> Ready;

        public static string SoundDirectory
        {
            get
            {
                if (string.IsNullOrEmpty(Singleton.PathToAI))
                    return null;

                return Path.Combine(Singleton.PathToAI, "DATA", "SOUND");
            }
        }

        /// <summary>Is there anything to preview from - i.e. has the game been found?</summary>
        public static bool IsAvailable
        {
            get
            {
                string directory = SoundDirectory;
                return !string.IsNullOrEmpty(directory) && Directory.Exists(directory);
            }
        }

        /// <summary>True once the index is built and calls to <see cref="Get"/> will not block.</summary>
        public static bool IsReady
        {
            get
            {
                lock (_lock)
                    return _building != null && _building.IsCompleted && !_building.IsFaulted;
            }
        }

        /// <summary>
        /// The index, building it if this is the first ask. Never call this on the UI thread without
        /// going through <see cref="GetAsync"/> - the first build takes about a second.
        /// </summary>
        public static WwiseSoundLibrary Get()
        {
            return GetAsync().Result;
        }

        public static Task<WwiseSoundLibrary> GetAsync()
        {
            string directory = SoundDirectory;
            string overridePackage = LevelOverridePackage();
            string key = directory + "|" + overridePackage;

            lock (_lock)
            {
                //A different level may bring a different override package, so the index is keyed on it
                if (_building != null && _key == key && !_building.IsFaulted)
                    return _building;

                _key = key;
                _building = Task.Factory.StartNew(() =>
                {
                    WwiseSoundLibrary library = WwiseSoundLibrary.Build(directory, PreferredLanguage(directory), overridePackage);

                    Action<WwiseSoundLibrary> handler = Ready;
                    if (handler != null)
                        handler(library);

                    return library;
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

                return _building;
            }
        }

        /// <summary>Drop the index, so the next ask rebuilds it.</summary>
        public static void Invalidate()
        {
            lock (_lock)
            {
                _building = null;
                _key = null;
            }
        }

        /// <summary>
        /// A level's own sound package, if it ships one. Most of the challenge maps do, and they use it
        /// to replace audio the base game already has under the same id.
        /// </summary>
        private static string LevelOverridePackage()
        {
            try
            {
                LevelContent content = Singleton.Editor == null || Singleton.Editor.CompositeBrowser == null
                    ? null
                    : Singleton.Editor.CompositeBrowser.Content;

                if (content == null || content.Level == null || string.IsNullOrEmpty(content.Level.Filepath))
                    return null;

                string world = content.Level.Filepath + (content.Level.Patched ? "_PATCH" : "") + "/WORLD/";
                string package = world + "level_sound_override.pck";
                return File.Exists(package) ? package : null;
            }
            catch
            {
                return null;
            }
        }

        private static string PreferredLanguage(string directory)
        {
            try
            {
                string[] directories = Directory.GetDirectories(directory).Select(Path.GetFileName).ToArray();

                //The editor is English only, so prefer the matching voice set and fall back to whatever
                //this installation actually has
                string english = directories.FirstOrDefault(d => d.StartsWith("English", StringComparison.OrdinalIgnoreCase));
                if (english != null)
                    return english;

                return directories.FirstOrDefault(d => d.IndexOf('(') != -1) ?? "";
            }
            catch
            {
                return "English(US)";
            }
        }
    }
}
