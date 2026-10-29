using System;
using System.Collections.Generic;
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

                //The Switch build keeps its Wwise content in DATA/SOUND_SWITCH (and its banks
                //are a newer Wwise generation - v134 vs the PC builds' v88).
                if (CathodeLib.PatchManager.GetPlatform(Singleton.PathToAI) == CathodeLib.PatchManager.Platform.SWITCH)
                    return Path.Combine(Singleton.PathToAI, "DATA", "SOUND_SWITCH");

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
        /// Bring the index back in line with soundbanks that have just been written to, so the audio that
        /// moved inside them is read from where it is now. If they can't be read back, the index is dropped
        /// instead and the next ask builds it again from the files.
        /// </summary>
        public static void Refresh(IEnumerable<string> files)
        {
            Task<WwiseSoundLibrary> building;
            lock (_lock)
                building = _building;

            //Nothing indexed yet: the next ask reads the files as they are now anyway
            if (building == null)
                return;

            //Still building (it may have read a bank before the write), or the build failed
            if (!building.IsCompleted || building.IsFaulted || building.IsCanceled)
            {
                Invalidate();
                return;
            }

            try
            {
                foreach (string file in files.Where(f => !string.IsNullOrEmpty(f)).Distinct(StringComparer.OrdinalIgnoreCase))
                    building.Result.Refresh(file);
            }
            catch
            {
                Invalidate();
            }
        }

        /// <summary>
        /// A level's own sound package, if it ships one. Most of the challenge maps do, and they use it
        /// to replace audio the base game already has under the same id.
        /// </summary>
        private static string LevelOverridePackage()
        {
            string package = LevelSoundOverride.PathForOpenLevel();
            return package != null && File.Exists(package) ? package : null;
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
