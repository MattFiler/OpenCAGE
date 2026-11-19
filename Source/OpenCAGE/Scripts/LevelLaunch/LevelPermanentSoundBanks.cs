using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>
    /// A level's own versions of the game's permanent soundbanks (LIST_OF_PERMANENT_SOUND_BANKS.TXT: the motion tracker, the torch, the
    /// player's voice...), kept as DATA/ENV/&lt;level&gt;/WORLD/PERMANENT_SOUNDBANKS/&lt;BANK&gt;.BNK. The game loads those banks once, when it
    /// starts, so the level's own sound package can't replace them: a launch into the level puts each over DATA/SOUND/&lt;BANK&gt;.BNK (the
    /// game's file kept in DATA/MODTOOLS/LEVEL_SOUNDBANKS), and any other launch, the game closing or OpenCAGE starting puts the game's back.
    /// </summary>
    public static class LevelPermanentSoundBanks
    {
        /// <summary>The folder in a level's WORLD folder that holds its own permanent soundbanks.</summary>
        public const string FolderName = "PERMANENT_SOUNDBANKS";

        private static readonly object _lock = new object();

        /// <summary>A level's folder of permanent soundbanks: DATA/ENV/&lt;level&gt;/WORLD/PERMANENT_SOUNDBANKS.</summary>
        public static string Folder(string pathToAI, string level) =>
            Path.Combine(pathToAI ?? "", "DATA", "ENV", (level ?? "").Replace('/', Path.DirectorySeparatorChar).Trim(Path.DirectorySeparatorChar), "WORLD", FolderName);

        private static string SoundFolder(string pathToAI) => Path.Combine(pathToAI ?? "", "DATA", "SOUND");
        private static string KeptFolder(string pathToAI) => Path.Combine(pathToAI ?? "", "DATA", "MODTOOLS", "LEVEL_SOUNDBANKS");

        /// <summary>The level's banks the game loads permanently (others in the folder are left alone: the level's sound package takes those).</summary>
        public static List<string> BanksOf(string pathToAI, string level)
        {
            string folder = Folder(pathToAI, level);
            if (string.IsNullOrWhiteSpace(level) || !Directory.Exists(folder))
                return new List<string>();
            HashSet<string> permanent = new HashSet<string>(PermanentBanks(pathToAI).Select(o => o.ToUpperInvariant() + ".BNK"), StringComparer.OrdinalIgnoreCase);
            return Directory.GetFiles(folder, "*.BNK").Where(o => permanent.Contains(Path.GetFileName(o))).OrderBy(o => o, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static List<string> PermanentBanks(string pathToAI)
        {
            try
            {
                string file = Path.Combine(pathToAI ?? "", "DATA", "LIST_OF_PERMANENT_SOUND_BANKS.TXT");
                return File.Exists(file) ? File.ReadAllLines(file).Select(o => o.Trim()).Where(o => o.Length != 0).ToList() : new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// A launch into a level (null: none): an earlier launch's banks out, then this level's in. Returns what went wrong, or null; applied
        /// lists what went in, in words.
        /// </summary>
        public static string Apply(string pathToAI, string level, List<string> applied)
        {
            lock (_lock)
            {
                string problem = PutBackLocked(pathToAI);
                if (problem != null)
                    return problem;
                foreach (string bank in BanksOf(pathToAI, level))
                {
                    string name = Path.GetFileName(bank).ToUpperInvariant();
                    string game = Path.Combine(SoundFolder(pathToAI), name);
                    try
                    {
                        if (!File.Exists(game))
                        {
                            problem = (problem != null ? problem + "; " : "") + "the game has no " + name + " to stand in for";
                            continue;
                        }
                        Directory.CreateDirectory(KeptFolder(pathToAI));
                        File.Copy(game, Path.Combine(KeptFolder(pathToAI), name), true);
                        Modding.ModServices.CaptureBeforeWrite(game, usersEdit: false);
                        File.Copy(bank, game, true);
                        applied?.Add("its own " + Path.GetFileNameWithoutExtension(name) + " soundbank");
                    }
                    catch (Exception e)
                    {
                        problem = (problem != null ? problem + "; " : "") + name + " could not be put in (is the game open?): " + e.Message;
                    }
                }
                return problem;
            }
        }

        /// <summary>The game's own permanent soundbanks back. Returns what went wrong, or null.</summary>
        public static string PutBack(string pathToAI)
        {
            lock (_lock)
                return PutBackLocked(pathToAI);
        }

        private static string PutBackLocked(string pathToAI)
        {
            string kept = KeptFolder(pathToAI);
            if (!Directory.Exists(kept))
                return null;
            string problem = null;
            foreach (string file in Directory.GetFiles(kept, "*.BNK"))
            {
                try
                {
                    string game = Path.Combine(SoundFolder(pathToAI), Path.GetFileName(file));
                    Modding.ModServices.CaptureBeforeWrite(game, usersEdit: false);
                    File.Copy(file, game, true);
                    File.Delete(file);
                }
                catch (Exception e)
                {
                    problem = (problem != null ? problem + "; " : "") + Path.GetFileName(file) + " could not be put back (is the game open?): " + e.Message;
                }
            }
            try
            {
                if (Directory.GetFileSystemEntries(kept).Length == 0)
                    Directory.Delete(kept);
            }
            catch { }
            return problem;
        }

        /// <summary>Whether a launch's banks are in the game's files now.</summary>
        public static bool InUse(string pathToAI)
        {
            try
            {
                string kept = KeptFolder(pathToAI);
                return Directory.Exists(kept) && Directory.GetFiles(kept, "*.BNK").Length != 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
