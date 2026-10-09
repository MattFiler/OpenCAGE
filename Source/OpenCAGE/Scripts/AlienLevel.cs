using OpenCAGE;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.Design;
using System.Xml.Linq;

namespace OpenCAGE.Backups
{
    // imported from backup tool

    public class AlienLevel
    {
        public List<AlienBackup> Backups = new List<AlienBackup>();
        private List<AlienFile> Files = new List<AlienFile>();

        /* Which revision each file of a backup is (backup ID -> level-relative path -> hash). Written after the version 1
           archive, where an older OpenCAGE stops reading, so archives stay readable both ways; backups made before it
           (or written by an older OpenCAGE since) are matched up from their hashes by PathsOf. */
        private Dictionary<long, Dictionary<string, string>> PathMaps = new Dictionary<long, Dictionary<string, string>>();
        private const int PathMapMagic = 0x50414D50; //"PMAP"

        /* Backups this object deleted: an archive re-read before writing must not bring them back */
        private HashSet<long> DeletedHere = new HashSet<long>();
        /* Backups the archive held when this object last read or wrote it: one missing from it later was deleted elsewhere */
        private HashSet<long> SeenOnDisk = new HashSet<long>();

        //TODO: change this to write the contents out to external files so we don't have to load everything every time 

        public string Name => _level;
        private string _level;

        private string LevelFolder;
        private string BackupFolder;
        private string BackupFile;

        private int Version = 1;

        public AlienLevel(string level)
        {
            _level = level;

            LevelFolder = Singleton.PathToAI + "/DATA/ENV/" + level;
            BackupFolder = Singleton.PathToAI + "/DATA/MODTOOLS/BACKUPS/" + level;

            // This is a bit of a hack - when this logic was used in the old external tool it always removed 'PRODUCTION' from the filepath.
            // As such, to support that old logic and avoid throwing away backups, lets try to trim it off.
            if (!Directory.Exists(BackupFolder))
            {
                if (level.ToUpper().StartsWith("PRODUCTION"))
                {
                    string altBackupFolder = Singleton.PathToAI + "/DATA/MODTOOLS/BACKUPS/" + level.Substring("PRODUCTION/".Length);
                    if (Directory.Exists(altBackupFolder))
                        BackupFolder = altBackupFolder;
                }
            }

            //TODO - i should compress saved backups to prevent bloating disk space. i'll need to add in support for old uncompressed backups too.

            BackupFile = BackupFolder + ".BAK";

            //The folder is made by the first backup, not here: only reading a level's backups must not write anything
            Load();
        }

        /* Create a new backup */
        public void CreateBackup(string name)
        {
            Directory.CreateDirectory(BackupFolder);
            //Another window (or tool) may have written the archive since this one read it
            MergeFromDisk();

            //IDs are seconds: a second backup within the same second would share the first's ID, and only one could ever be restored
            long id = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Backups.Any(o => o.ID == id))
                id = Backups.Max(o => o.ID) + 1;
            AlienBackup Backup = new AlienBackup() { Name = name, Date = DateTime.Now.ToString("dd-MM-yy HH:mm:ss"), ID = id, GUIDs = new List<string>() };
            Dictionary<string, string> paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string[] files = Directory.GetFiles(LevelFolder, "*.*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string fileName = files[i].Substring(LevelFolder.Length + 1);
                string fileHash = GenerateFileHash(files[i]);

                AlienFile file = Files.FirstOrDefault(o => o.Name == fileName);
                if (file == null)
                {
                    file = new AlienFile() { Name = fileName, Revisions = new List<string>() };
                    Files.Add(file);
                }

                string revisionFile = BackupFolder + "/" + fileHash + ".gz";
                if (!File.Exists(revisionFile))
                {
                    using (GZipStream gzipStream = new GZipStream(File.Create(revisionFile), CompressionMode.Compress))
                    {
                        byte[] content = File.ReadAllBytes(files[i]);
                        gzipStream.Write(content, 0, content.Length);
                    }
                }
                if (!file.Revisions.Contains(fileHash))
                    file.Revisions.Add(fileHash);

                Backup.GUIDs.Add(fileHash);
                paths[fileName] = fileHash;
            }

            Backups.Add(Backup);
            PathMaps[id] = paths;
            Save();
        }

        /* The ID of the backup CreateBackup made last (null before one) */
        public AlienBackup Newest => Backups.Count == 0 ? null : Backups[Backups.Count - 1];

        /* When a backup was made, in UTC: its ID is the Unix time in seconds */
        public static DateTime DateUtc(AlienBackup backup) => DateTimeOffset.FromUnixTimeSeconds(backup.ID).UtcDateTime;

        /// <summary>
        /// The file each level-relative path ('\' separated) held in a backup, by hash. A backup made since path maps
        /// were kept has its own; an older one is matched up from its hashes: a file takes the one revision of it the
        /// backup holds, revisions of it that another file in the backup claims are left to that file, and a path whose
        /// revisions still match more than one way (identical files, a file deleted and recreated) is listed in
        /// <paramref name="ambiguous"/>, taking its newest matching revision.
        /// </summary>
        public Dictionary<string, string> PathsOf(AlienBackup backup, out List<string> ambiguous)
        {
            ambiguous = new List<string>();
            if (backup == null) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (PathMaps.TryGetValue(backup.ID, out Dictionary<string, string> known))
                return new Dictionary<string, string>(known, StringComparer.OrdinalIgnoreCase);

            //How many files of the backup each hash stands for
            Dictionary<string, int> left = new Dictionary<string, int>();
            foreach (string guid in backup.GUIDs)
                left[guid] = left.TryGetValue(guid, out int count) ? count + 1 : 1;

            Dictionary<string, string> paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            List<KeyValuePair<AlienFile, List<string>>> several = new List<KeyValuePair<AlienFile, List<string>>>();
            foreach (AlienFile file in Files)
            {
                List<string> candidates = file.Revisions.Where(o => left.ContainsKey(o)).Distinct().ToList();
                if (candidates.Count == 1)
                {
                    paths[file.Name] = candidates[0];
                    left[candidates[0]]--;
                }
                else if (candidates.Count > 1)
                    several.Add(new KeyValuePair<AlienFile, List<string>>(file, candidates));
            }
            foreach (KeyValuePair<AlienFile, List<string>> entry in several)
            {
                List<string> free = entry.Value.Where(o => left[o] > 0).ToList();
                string chosen = free.Count == 1 ? free[0] : (free.Count > 1 ? free : entry.Value).Last();
                if (free.Count != 1) ambiguous.Add(entry.Key.Name);
                paths[entry.Key.Name] = chosen;
                left[chosen]--;
            }
            //A hash more files claim than the backup holds: one of them (a file deleted before the backup whose old content
            //matches another file) cannot be told apart
            foreach (KeyValuePair<string, string> pair in paths)
                if (left[pair.Value] < 0 && !ambiguous.Contains(pair.Key))
                    ambiguous.Add(pair.Key);
            return paths;
        }

        /// <summary>The files on disk now, by level-relative path ('\' separated) and hash.</summary>
        public Dictionary<string, string> CurrentPaths()
        {
            string[] files = Directory.GetFiles(LevelFolder, "*.*", SearchOption.AllDirectories);
            string[] hashes = new string[files.Length];
            Parallel.For(0, files.Length, (i) => { hashes[i] = GenerateFileHash(files[i]); });
            Dictionary<string, string> paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Length; i++)
                paths[files[i].Substring(LevelFolder.Length + 1)] = hashes[i];
            return paths;
        }

        /// <summary>Where a <see cref="RestoreBackup"/> that returned false left the level folder.</summary>
        public enum RestoreFailure
        {
            /// <summary>The last restore succeeded (or none was tried).</summary>
            None,
            /// <summary>It failed before anything in the level folder was changed.</summary>
            Untouched,
            /// <summary>It failed part way, and the folder was put back as it was before.</summary>
            RolledBack,
            /// <summary>The backup is in place; only removing the safety copy (the level folder + "_COPY") failed.</summary>
            CopyLeftBehind,
            /// <summary>It failed part way and the folder could not be put back; the safety copy is kept.</summary>
            Damaged,
        }
        public RestoreFailure LastRestoreFailure { get; private set; } = RestoreFailure.None;
        public string SafetyCopyFolder => LevelFolder + "_COPY";

        /* Restore a backup. With onlyPaths (level-relative, either separator), only those files are put back as the backup
           holds them: written if it has them, deleted if it does not; the rest of the folder is left alone. */
        public bool RestoreBackup(Int64 ID, ICollection<string> onlyPaths = null)
        {
            LastRestoreFailure = RestoreFailure.Untouched;
            AlienBackup backup = Backups.FirstOrDefault(o => o.ID == ID);
            if (backup == null) return false;
            Dictionary<string, string> paths = PathsOf(backup, out _);
            if (onlyPaths != null)
                return RestoreSome(paths, onlyPaths);

            //First, try keep a version of the current folder
            try
            {
                if (Directory.Exists(LevelFolder + "_COPY"))
                    Directory.Delete(LevelFolder + "_COPY", true);
                CopyDirectory(LevelFolder, LevelFolder + "_COPY", true);
            }
            catch
            {
                return false;
            }

            //Now, try clear the current folder
            try
            {
                Directory.Delete(LevelFolder, true);
                Directory.CreateDirectory(LevelFolder);
            }
            catch
            {
                //Part of the folder may be gone already: put it back from the copy rather than throwing the copy away
                LastRestoreFailure = RollBackRestore() ? RestoreFailure.RolledBack : RestoreFailure.Damaged;
                return false;
            }

            //Restore the backup: each file once, as the backup holds it (not every revision of it whose content the backup has)
            try
            {
                foreach (KeyValuePair<string, string> file in paths)
                    WriteRevision(file.Key, file.Value);
            }
            catch
            {
                LastRestoreFailure = RollBackRestore() ? RestoreFailure.RolledBack : RestoreFailure.Damaged;
                return false;
            }

            //Clear the copy
            try
            {
                Directory.Delete(LevelFolder + "_COPY", true);
            }
            catch
            {
                LastRestoreFailure = RestoreFailure.CopyLeftBehind;
                return false;
            }

            LastRestoreFailure = RestoreFailure.None;
            return true;
        }

        /* One stored revision's bytes (compressed, or a legacy uncompressed one) */
        public byte[] ReadRevision(string hash)
        {
            if (File.Exists(BackupFolder + "/" + hash))
                return File.ReadAllBytes(BackupFolder + "/" + hash);
            using (MemoryStream stream = new MemoryStream())
            using (GZipStream compressedStream = new GZipStream(File.OpenRead(BackupFolder + "/" + hash + ".gz"), CompressionMode.Decompress))
            {
                compressedStream.CopyTo(stream);
                return stream.ToArray();
            }
        }

        private void WriteRevision(string name, string hash)
        {
            string target = LevelFolder + "/" + name.Replace('\\', '/');
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllBytes(target, ReadRevision(hash));
        }

        /* Put back only some files: copied aside first, and put back from the copy if any of it fails */
        private bool RestoreSome(Dictionary<string, string> paths, ICollection<string> onlyPaths)
        {
            string copy = LevelFolder + "_COPY";
            List<string> wanted = onlyPaths.Select(o => o.Replace('/', '\\').Trim('\\')).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            try
            {
                if (Directory.Exists(copy))
                    Directory.Delete(copy, true);
                foreach (string name in wanted)
                {
                    string current = LevelFolder + "/" + name.Replace('\\', '/');
                    if (!File.Exists(current)) continue;
                    string aside = copy + "/" + name.Replace('\\', '/');
                    Directory.CreateDirectory(Path.GetDirectoryName(aside));
                    File.Copy(current, aside, true);
                }
            }
            catch
            {
                return false;
            }
            try
            {
                foreach (string name in wanted)
                {
                    string stored = paths.FirstOrDefault(o => string.Equals(o.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
                    if (stored != null)
                        WriteRevision(name, stored);
                    else if (File.Exists(LevelFolder + "/" + name.Replace('\\', '/')))
                        File.Delete(LevelFolder + "/" + name.Replace('\\', '/'));
                }
            }
            catch
            {
                //Write back what was copied aside, and remove what the backup added
                try
                {
                    foreach (string name in wanted)
                    {
                        string current = LevelFolder + "/" + name.Replace('\\', '/');
                        string aside = copy + "/" + name.Replace('\\', '/');
                        if (File.Exists(aside)) File.Copy(aside, current, true);
                        else if (File.Exists(current)) File.Delete(current);
                    }
                    Directory.Delete(copy, true);
                    LastRestoreFailure = RestoreFailure.RolledBack;
                }
                catch
                {
                    LastRestoreFailure = RestoreFailure.Damaged;
                }
                return false;
            }
            try
            {
                if (Directory.Exists(copy))
                    Directory.Delete(copy, true);
            }
            catch
            {
                LastRestoreFailure = RestoreFailure.CopyLeftBehind;
                return false;
            }
            LastRestoreFailure = RestoreFailure.None;
            return true;
        }

        /* Put the level folder back from the copy a restore took before it began; the copy is kept if that fails */
        private bool RollBackRestore()
        {
            string copy = LevelFolder + "_COPY";
            if (!Directory.Exists(copy))
                return false;
            try
            {
                if (Directory.Exists(LevelFolder))
                    Directory.Delete(LevelFolder, true);
                Directory.Move(copy, LevelFolder);
                return true;
            }
            catch { }
            //Something in the folder is still held open: write the copy's files back over it instead
            try
            {
                foreach (string file in Directory.GetFiles(copy, "*.*", SearchOption.AllDirectories))
                {
                    string target = LevelFolder + file.Substring(copy.Length);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(file, target, true);
                }
                Directory.Delete(copy, true);
                return true;
            }
            catch
            {
                return false;
            }
        }
        private void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }

        /* Delete an existing backup */
        public void DeleteBackup(Int64 ID)
        {
            //Revisions are only deleted when no backup uses them: that has to count backups written since this was read
            MergeFromDisk();
            Backups.Remove(Backups.FirstOrDefault(o => o.ID == ID));
            PathMaps.Remove(ID);
            DeletedHere.Add(ID);

            List<AlienFile> trimmedFiles = new List<AlienFile>();
            for (int i = 0; i < Files.Count; i++)
            {
                List<string> trimmedRevisions = new List<string>();
                for (int x = 0; x < Files[i].Revisions.Count; x++)
                {
                    bool used = false;
                    for (int y = 0; y < Backups.Count; y++)
                    {
                        if (Backups[y].GUIDs.Contains(Files[i].Revisions[x]))
                        {
                            used = true;
                            break;
                        }
                    }
                    if (!used)
                    {
                        if (File.Exists(BackupFolder + "/" + Files[i].Revisions[x]))
                            File.Delete(BackupFolder + "/" + Files[i].Revisions[x]);
                        if (File.Exists(BackupFolder + "/" + Files[i].Revisions[x] + ".gz"))
                            File.Delete(BackupFolder + "/" + Files[i].Revisions[x] + ".gz");
                        continue;
                    }
                    trimmedRevisions.Add(Files[i].Revisions[x]);
                }
                if (trimmedRevisions.Count == 0) continue;
                Files[i].Revisions = trimmedRevisions;
                trimmedFiles.Add(Files[i]);
            }

            Files = trimmedFiles;
            Save();
        }

        /* Get the number of files changed between backups - leave 2nd arg null to calculate current state */
        public int CalculateDiff(AlienBackup orig, AlienBackup mod = null)
        {
            Dictionary<string, string> after = mod == null ? CurrentPaths() : PathMaps.TryGetValue(mod.ID, out Dictionary<string, string> modPaths) ? modPaths : null;
            if (orig == null) return after?.Count ?? mod.GUIDs.Count;

            //By path when both sides know their paths: changed, added and removed files, each once
            if (after != null && PathMaps.TryGetValue(orig.ID, out Dictionary<string, string> before))
                return Differences(before, after).Count;

            //Older backups only list hashes: count what each side holds that the other does not, as files
            List<string> mine = after?.Values.ToList() ?? mod.GUIDs;
            int onlyAfter = MultisetMinus(mine, orig.GUIDs), onlyBefore = MultisetMinus(orig.GUIDs, mine);
            return Math.Max(onlyAfter, onlyBefore);
        }

        /* Paths whose file differs between two path maps: changed, added or removed */
        public static List<string> Differences(Dictionary<string, string> before, Dictionary<string, string> after)
        {
            List<string> differ = new List<string>();
            foreach (KeyValuePair<string, string> file in after)
                if (!before.TryGetValue(file.Key, out string old) || old != file.Value)
                    differ.Add(file.Key);
            foreach (string name in before.Keys)
                if (!after.ContainsKey(name))
                    differ.Add(name);
            return differ;
        }

        private static int MultisetMinus(List<string> a, List<string> b)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            foreach (string hash in b) counts[hash] = counts.TryGetValue(hash, out int n) ? n + 1 : 1;
            int extra = 0;
            foreach (string hash in a)
            {
                if (counts.TryGetValue(hash, out int n) && n > 0) counts[hash] = n - 1;
                else extra++;
            }
            return extra;
        }


        /* Load the backup archive into memory */
        private void Load()
        {
            if (!File.Exists(BackupFile)) return;
            Read(BackupFile, Backups, Files, PathMaps);
            SeenOnDisk = new HashSet<long>(Backups.Select(o => o.ID));
        }

        private void Read(string archive, List<AlienBackup> backups, List<AlienFile> files, Dictionary<long, Dictionary<string, string>> pathMaps)
        {
            using (BinaryReader reader = new BinaryReader(File.OpenRead(archive)))
            {
                if (reader.ReadInt32() != Version)
                {
                    throw new Exception("Backup version mismatch");
                }

                int backupCount = reader.ReadInt32();
                for (int i = 0; i < backupCount; i++)
                {
                    AlienBackup backup = new AlienBackup() { Name = reader.ReadString(), Date = reader.ReadString(), ID = reader.ReadInt64(), GUIDs = new List<string>() };
                    int guidCount = reader.ReadInt32();
                    for (int x = 0; x < guidCount; x++)
                    {
                        backup.GUIDs.Add(reader.ReadString());
                    }
                    backups.Add(backup);
                }

                int fileCount = reader.ReadInt32();
                for (int i = 0; i < fileCount; i++)
                {
                    AlienFile file = new AlienFile() { Name = reader.ReadString(), Revisions = new List<string>() };
                    int revisionCount = reader.ReadInt32();
                    for (int x = 0; x < revisionCount; x++)
                    {
                        file.Revisions.Add(reader.ReadString());
                    }
                    files.Add(file);
                }

                //The path maps, when this archive was last written by an OpenCAGE that keeps them
                if (reader.BaseStream.Length - reader.BaseStream.Position < 8 || reader.ReadInt32() != PathMapMagic)
                    return;
                reader.ReadInt32(); //its version
                int mapCount = reader.ReadInt32();
                for (int i = 0; i < mapCount; i++)
                {
                    long id = reader.ReadInt64();
                    int pathCount = reader.ReadInt32();
                    Dictionary<string, string> paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int x = 0; x < pathCount; x++)
                    {
                        string name = reader.ReadString();
                        paths[name] = reader.ReadString();
                    }
                    //Only for a backup the archive still lists with the same files (an older OpenCAGE may have rewritten it since)
                    AlienBackup backup = backups.FirstOrDefault(o => o.ID == id);
                    if (backup != null && backup.GUIDs.Count == paths.Count)
                        pathMaps[id] = paths;
                }
            }
        }

        /* Take in what another AlienLevel (the Manage Backups window, a tool) wrote to the archive since this one read it:
           its backups (not those this one deleted), file revisions and path maps. Without it, writing the archive from
           memory would drop backups made meanwhile, and deleting would remove revisions they use. */
        private void MergeFromDisk()
        {
            if (!File.Exists(BackupFile)) return;
            List<AlienBackup> backups = new List<AlienBackup>();
            List<AlienFile> files = new List<AlienFile>();
            Dictionary<long, Dictionary<string, string>> pathMaps = new Dictionary<long, Dictionary<string, string>>();
            try { Read(BackupFile, backups, files, pathMaps); }
            catch { return; }

            foreach (AlienBackup backup in backups)
            {
                if (DeletedHere.Contains(backup.ID) || Backups.Any(o => o.ID == backup.ID)) continue;
                Backups.Add(backup);
                if (pathMaps.TryGetValue(backup.ID, out Dictionary<string, string> paths))
                    PathMaps[backup.ID] = paths;
            }
            //Deleted elsewhere since this read them: their revisions may be gone, so they must not be written back
            HashSet<long> onDisk = new HashSet<long>(backups.Select(o => o.ID));
            Backups.RemoveAll(o => SeenOnDisk.Contains(o.ID) && !onDisk.Contains(o.ID));
            Backups = Backups.OrderBy(o => o.ID).ToList();
            foreach (AlienFile file in files)
            {
                AlienFile mine = Files.FirstOrDefault(o => o.Name == file.Name);
                if (mine == null)
                {
                    Files.Add(file);
                    continue;
                }
                foreach (string revision in file.Revisions)
                    if (!mine.Revisions.Contains(revision))
                        mine.Revisions.Add(revision);
            }
        }

        /* Save the backup archive out to disk */
        private void Save()
        {
            using (BinaryWriter writer = new BinaryWriter(File.OpenWrite(BackupFile)))
            {
                writer.BaseStream.SetLength(0);
                writer.Write(Version);

                writer.Write(Backups.Count);
                for (int i = 0; i < Backups.Count; i ++)
                {
                    writer.Write(Backups[i].Name);
                    writer.Write(Backups[i].Date);
                    writer.Write(Backups[i].ID);
                    writer.Write(Backups[i].GUIDs.Count);
                    for (int x = 0; x < Backups[i].GUIDs.Count; x++)
                    {
                        writer.Write(Backups[i].GUIDs[x]);
                    }
                }

                writer.Write(Files.Count);
                for (int i = 0; i < Files.Count; i++)
                {
                    writer.Write(Files[i].Name);
                    writer.Write(Files[i].Revisions.Count);
                    for (int x = 0; x < Files[i].Revisions.Count; x++)
                    {
                        writer.Write(Files[i].Revisions[x]);
                    }
                }

                //After everything a version 1 reader takes: which file each backup holds, by path
                List<AlienBackup> mapped = Backups.Where(o => PathMaps.ContainsKey(o.ID)).ToList();
                writer.Write(PathMapMagic);
                writer.Write(1);
                writer.Write(mapped.Count);
                foreach (AlienBackup backup in mapped)
                {
                    Dictionary<string, string> paths = PathMaps[backup.ID];
                    writer.Write(backup.ID);
                    writer.Write(paths.Count);
                    foreach (KeyValuePair<string, string> path in paths)
                    {
                        writer.Write(path.Key);
                        writer.Write(path.Value);
                    }
                }
            }
            SeenOnDisk = new HashSet<long>(Backups.Select(o => o.ID));
        }

        /* Generate a hash for a given file */
        private string GenerateFileHash(string path)
        {
            string hash = "";
            {
                byte[] content = File.ReadAllBytes(path);

                MD5 md5 = MD5.Create();
                md5.TransformBlock(content, 0, content.Length, content, 0);
                md5.TransformFinalBlock(new byte[0], 0, 0);
                hash = BitConverter.ToString(md5.Hash);
            }
            return hash.Replace("-", "").ToLower();
        }


        public class AlienBackup
        {
            public string Name;
            public string Date;
            public Int64 ID;
            public List<string> GUIDs;
        }

        private class AlienFile
        {
            public string Name;
            public List<string> Revisions;
        }
    }
}
