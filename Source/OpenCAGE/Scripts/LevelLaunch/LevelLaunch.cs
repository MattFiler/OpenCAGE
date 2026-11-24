using CathodeLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenCAGE
{
    /// <summary>
    /// Puts a level's launch settings (<see cref="LevelLaunchSettings"/>) into AI.exe as the game is launched into it (Launch Game,
    /// launch_game, Launch Mod, save-and-launch), and the game's own back once that game has closed, at any other launch, or as
    /// OpenCAGE starts. The spawn guard goes in at every launch and stays: it only changes what would crash.
    /// </summary>
    public static class LevelLaunch
    {
        /// <summary>The setting that turns the spawn guard off (on unless set).</summary>
        public const string SpawnGuardSetting = "LevelLaunch_SpawnGuard";

        public static bool SpawnGuardOn => SettingsManager.GetBool(SpawnGuardSetting, true);

        /* The level a launch set the binary up for, kept where the Mod Manager never looks */
        private static string RecordPath(string pathToAI) => Path.Combine(pathToAI ?? "", "DATA", "MODTOOLS", "LEVEL_LAUNCH.TXT");

        private static readonly object _lock = new object();
        private static int _generation;

        /// <summary>
        /// A launch into a level (null or the menu: none of a level's): AI.exe set up with its settings, the game's own for what it
        /// doesn't set. Returns what went wrong, or null; applied lists what went in, in words.
        /// </summary>
        public static string ApplyForLaunch(string pathToAI, string level, out List<string> applied)
        {
            applied = new List<string>();
            lock (_lock)
            {
                _generation++;
                LevelLaunchSettings settings = string.IsNullOrWhiteSpace(level) || EditorUtils.IsFrontend(level) ? new LevelLaunchSettings() : LevelLaunchSettings.Of(pathToAI, level);
                //The level's own permanent soundbanks in (an earlier launch's out): files, so any build of the game takes them
                string banksProblem = LevelPermanentSoundBanks.Apply(pathToAI, string.IsNullOrWhiteSpace(level) || EditorUtils.IsFrontend(level) ? null : level, applied);
                //Nothing of the level's to speak of: no settings, and its saves shared with the campaign's
                bool quiet = settings.IsEmpty && !LevelSaveSlots.Own(level, settings);
                //A build without them: only worth a word when the level has some
                if (!LevelLaunchPatches.Supports(Singleton.Platform))
                    return quiet ? banksProblem : "this version of the game (" + Singleton.Platform + ") can't take a level's launch settings" + (LevelSaveSlots.Own(level, settings) ? " - its saves go with the campaign's" : "");
                //The level's own loading movies in (an earlier launch's out): their playlist is the one the launch plays
                string ownPlaylist = LevelLoadingMovies.Apply(pathToAI, level, settings, out string moviesProblem);
                LevelLaunchSettings written = settings;
                if (settings.HasMovies)
                {
                    written = JsonConvert.DeserializeObject<LevelLaunchSettings>(JsonConvert.SerializeObject(settings));
                    written.LoadingPlaylist = ownPlaylist;
                }
                string problem = LevelLaunchPatches.Write(Singleton.Platform, pathToAI, level, written, SpawnGuardOn);
                if (moviesProblem != null)
                    problem = (problem != null ? problem + "; " : "") + moviesProblem;
                //Larger animation memory: on when the level asks (and the player's option doesn't already); otherwise left as the
                //launch's options wrote it
                bool anim = settings.LargerAnimationMemory == true && !AnimationHeapBudget.Enabled;
                if (anim && !PatchManager.PatchAnimHeapBudget(Singleton.Platform, pathToAI, true))
                    problem = (problem != null ? problem + "; " : "") + "the larger animation memory could not be written";
                bool any = LevelLaunchPatches.Read(Singleton.Platform, pathToAI).AnyLevelSetting || anim || LevelLoadingMovies.InUse(pathToAI) || LevelPermanentSoundBanks.InUse(pathToAI);
                if (any)
                    applied.AddRange(settings.Describe(level));
                Record(pathToAI, any ? level : null, anim);
                if (problem != null)
                    Console.WriteLine("LevelLaunch: " + problem);
                //Only the level's own settings are reported: a binary the guard can't go into launches as it is
                if (banksProblem != null)
                    Console.WriteLine("LevelLaunch: " + banksProblem);
                return quiet ? banksProblem : banksProblem != null ? (problem != null ? problem + "; " : "") + banksProblem : problem;
            }
        }

        /// <summary>The game's own heap, collisions, loading playlist and save file names back (the spawn guard stays). Returns what went wrong, or null.</summary>
        public static string PutBack(string pathToAI)
        {
            lock (_lock)
            {
                _generation++;
                return PutBackLocked(pathToAI);
            }
        }

        private static string PutBackLocked(string pathToAI)
        {
            string problem = LevelLaunchPatches.Write(Singleton.Platform, pathToAI, null, null, LevelLaunchPatches.Read(Singleton.Platform, pathToAI).SpawnGuard);
            if (RecordedAnim(pathToAI) && !AnimationHeapBudget.Enabled)
                PatchManager.PatchAnimHeapBudget(Singleton.Platform, pathToAI, false);
            string movies = LevelLoadingMovies.PutBack(pathToAI);
            if (movies != null)
                problem = (problem != null ? problem + "; " : "") + movies;
            string banks = LevelPermanentSoundBanks.PutBack(pathToAI);
            if (banks != null)
                problem = (problem != null ? problem + "; " : "") + banks;
            Record(pathToAI, null, false);
            return problem;
        }

        /* Whether AI.exe holds a level's settings: one of its own, or the larger animation memory a level's launch turned on */
        private static bool InUse(string pathToAI)
        {
            return LevelLaunchPatches.Read(Singleton.Platform, pathToAI).AnyLevelSetting || LevelLoadingMovies.InUse(pathToAI) || LevelPermanentSoundBanks.InUse(pathToAI) ||
                   (RecordedAnim(pathToAI) && PatchManager.IsAnimHeapBudgetPatched(Singleton.Platform, pathToAI) == true);
        }

        /// <summary>The level whose settings AI.exe holds now, or null when it holds none.</summary>
        public static string InUseFor(string pathToAI)
        {
            try
            {
                if (!InUse(pathToAI))
                    return null;
                string record = RecordPath(pathToAI);
                return File.Exists(record) ? File.ReadAllLines(record).FirstOrDefault()?.Trim() ?? "" : "";
            }
            catch
            {
                return null;
            }
        }

        /* The record: the level, and a line when its launch turned the larger animation memory on (which the player's own option can
           also have on, so the binary alone can't tell) */
        private const string AnimLine = "larger_animation_memory";

        private static void Record(string pathToAI, string level, bool anim)
        {
            try
            {
                string record = RecordPath(pathToAI);
                if (level == null)
                {
                    if (File.Exists(record)) File.Delete(record);
                    return;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(record));
                File.WriteAllLines(record, anim ? new[] { level, AnimLine } : new[] { level });
            }
            catch { } //says which level and what of the animation memory is the level's
        }

        private static bool RecordedAnim(string pathToAI)
        {
            try
            {
                string record = RecordPath(pathToAI);
                return File.Exists(record) && File.ReadAllLines(record).Skip(1).Any(o => o.Trim() == AnimLine);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// After a launch: once this install's game has come and gone (closed for 15 seconds: the Epic build restarts itself through its
        /// launcher), the game's own go back, so the game started any other way runs as it ships. A later launch makes this stand down.
        /// </summary>
        public static void PutBackWhenGameCloses(string pathToAI)
        {
            if (!InUse(pathToAI))
                return;
            int generation;
            lock (_lock) generation = _generation;
            Task.Run(async () =>
            {
                try
                {
                    Stopwatch waited = Stopwatch.StartNew();
                    while (waited.Elapsed < TimeSpan.FromMinutes(3) && !GameRunning())
                    {
                        if (Volatile.Read(ref _generation) != generation) return;
                        await Task.Delay(2000);
                    }
                    Stopwatch gone = null;
                    while (true)
                    {
                        if (Volatile.Read(ref _generation) != generation) return;
                        if (GameRunning())
                            gone = null;
                        else if (gone == null)
                            gone = Stopwatch.StartNew();
                        else if (gone.Elapsed > TimeSpan.FromSeconds(15) && Singleton.CurrentBackupState == Singleton.BackupState.NONE)
                            break;
                        await Task.Delay(2000);
                    }
                    //The level's UI movies come out of UI.PAK now too: theirs first, as both write it
                    if (LevelLoadingMovies.ScreenChanged(pathToAI))
                    {
                        Stopwatch ui = Stopwatch.StartNew();
                        while (ui.Elapsed < TimeSpan.FromSeconds(60) && LevelUiOverrides.InUse(pathToAI).Count != 0)
                        {
                            if (Volatile.Read(ref _generation) != generation) return;
                            await Task.Delay(1000);
                        }
                        await Task.Delay(3000);
                    }
                    //AI.exe can still be held for a moment after the game has gone: tried again for a minute while it stays in use
                    for (int attempt = 1; ; attempt++)
                    {
                        lock (_lock)
                        {
                            if (_generation != generation || GameRunning() || !string.Equals(Singleton.PathToAI, pathToAI, StringComparison.OrdinalIgnoreCase))
                                return;
                            string problem = PutBackLocked(pathToAI);
                            if (problem == null || attempt >= 12 || !InUse(pathToAI))
                            {
                                _generation++;
                                Console.WriteLine("LevelLaunch: game closed, the game's own launch settings back" + (problem != null ? " (" + problem + ")" : ""));
                                return;
                            }
                        }
                        await Task.Delay(5000);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("LevelLaunch::PutBackWhenGameCloses - " + e);
                }
            });
        }

        /// <summary>As OpenCAGE starts: a level's settings an earlier launch left in (OpenCAGE closed before the game did) come out, unless the game runs.</summary>
        public static bool PutBackAtStartup(string pathToAI)
        {
            try
            {
                if (!InUse(pathToAI) || EditorUtils.ThisInstallsGameRunning())
                    return false;
                PutBack(pathToAI);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool GameRunning()
        {
            try
            {
                return EditorUtils.ThisInstallsGameRunning();
            }
            catch
            {
                return true;
            }
        }
    }
}
