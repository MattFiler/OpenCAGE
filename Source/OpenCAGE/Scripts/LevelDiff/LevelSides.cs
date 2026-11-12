using CATHODE;
using CathodeLib;
using OpenCAGE.Backups;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenCAGE.LevelDiff
{
    /// <summary>
    /// The sides Compare Levels offers in the editor, from one text form shared by the window and MCP:
    /// "saved" (the open level as saved), "open" (the open level now, unsaved edits included: script and assets only), "level:NAME"
    /// (another level of this install), "backup:ID" (a backup of the open level, unpacked to a temporary folder) or a level folder's
    /// path (another install, a copy). Editor thread for "open".
    /// </summary>
    public static class LevelSides
    {
        public static string OpenLevelName => Singleton.Editor?.CompositeBrowser?.Content?.Level?.Name;
        public static Level OpenLevel => Singleton.Editor?.CompositeBrowser?.Content?.Level;

        public static string LevelFolder(string levelName) => Path.GetFullPath(Path.Combine(Singleton.PathToAI, "DATA", "ENV", levelName.Replace('/', '\\')));

        /// <summary>The side a text names. Backups are unpacked here (slow for big levels): call off the UI thread.</summary>
        public static LevelSide Parse(string spec, Func<string, string> resolveLevel = null)
        {
            string s = (spec ?? "").Trim();
            if (s.Length == 0) throw new ArgumentException("A side is empty: give saved, open, level:NAME, backup:ID or a level folder.");
            string lower = s.ToLowerInvariant();
            if (lower == "saved")
            {
                string name = OpenLevelName ?? throw new ArgumentException("No level is open (\"saved\" is the open level as saved).");
                return LevelSide.OnDisk(LevelFolder(name), name + " as saved");
            }
            if (lower == "open" || lower == "now")
            {
                Level level = OpenLevel ?? throw new ArgumentException("No level is open.");
                return new LevelSide() { Live = level, Label = level.Name + " now (unsaved edits included)" };
            }
            if (lower.StartsWith("level:"))
            {
                string name = s.Substring(6).Trim();
                if (resolveLevel != null) name = resolveLevel(name);
                string folder = LevelFolder(name);
                if (!Directory.Exists(folder)) throw new ArgumentException("There is no level " + name + " in this install.");
                return LevelSide.OnDisk(folder, name + " as saved");
            }
            if (lower.StartsWith("backup:"))
            {
                //backup:ID is the open level's; backup:ID@LEVEL another level's (the Backup Manager's Compare With Level)
                string rest = s.Substring(7).Trim();
                int at = rest.IndexOf('@');
                string name = at >= 0 ? rest.Substring(at + 1).Trim() : OpenLevelName ?? throw new ArgumentException("No level is open (backup:ID is the open level's; backup:ID@LEVEL another's).");
                if (at >= 0) rest = rest.Substring(0, at).Trim();
                if (!long.TryParse(rest, out long id)) throw new ArgumentException("backup:ID takes a backup's id (list_backups / Manage Backups give them).");
                return Backup(name, id);
            }
            if (!Directory.Exists(s)) throw new ArgumentException("There is no folder " + s + " (a side is saved, open, level:NAME, backup:ID or a level folder).");
            return LevelSide.OnDisk(s);
        }

        /// <summary>A backup of a level of this install, unpacked into a temporary game-shaped folder (deleted after the compare).</summary>
        public static LevelSide Backup(string levelName, long id)
        {
            AlienLevel archive = new AlienLevel(levelName);
            AlienLevel.AlienBackup backup = archive.Backups.FirstOrDefault(o => o.ID == id)
                ?? throw new ArgumentException("No backup of " + levelName + " has id " + id + " (it has " + string.Join(", ", archive.Backups.Select(o => o.ID)) + ").");
            Dictionary<string, string> paths = archive.PathsOf(backup, out _);
            string root = Path.Combine(Path.GetTempPath(), "OpenCAGE_compare_" + Guid.NewGuid().ToString("N"));
            string folder = Path.Combine(root, "DATA", "ENV", levelName.Replace('/', '\\'));
            try
            {
                foreach (KeyValuePair<string, string> file in paths)
                {
                    string rel = file.Key.Replace('/', '\\');
                    string target = rel.StartsWith("_PATCH\\", StringComparison.OrdinalIgnoreCase) ? folder + "_PATCH\\" + rel.Substring(7) : Path.Combine(folder, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.WriteAllBytes(target, archive.ReadRevision(file.Value));
                }
            }
            catch
            {
                try { Directory.Delete(root, true); } catch { }
                throw;
            }
            return new LevelSide() { Folder = folder, Label = "backup " + id + " ('" + backup.Name + "', " + AlienLevel.DateUtc(backup).ToLocalTime().ToString("yyyy-MM-dd HH:mm") + ") of " + levelName, Temporary = root };
        }

        /// <summary>Compare two sides as the editor does: global data from the editor, the open level read on the UI thread.</summary>
        public static LevelDiffReport Compare(LevelSide before, LevelSide after, CompareOptions options, Func<Func<SideData>, SideData> onUiThread)
        {
            options.Global = options.Global ?? Singleton.Global;
            foreach (LevelSide side in new[] { before, after })
                if (side.Live != null && side.LiveData == null)
                    side.LiveData = onUiThread(() => LevelComparer.ReadLive(side.Live, options, side.Label));
            return LevelComparer.Compare(before, after, options);
        }
    }
}
