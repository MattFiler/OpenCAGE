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
        /* A Nostromo level's WORLD is read and written in the "_PATCH" folder beside it (by the game and the editor): part of
           the level, held in its backups as "_PATCH\..." files. Null for every other level. */
        private string PatchFolder;
        private const string PatchPrefix = "_PATCH\\";
        private string BackupFolder;
        private string BackupFile;

        private int Version = 1;

        public AlienLevel(string level)
        {
            _level = level;

            LevelFolder = Singleton.PathToAI + "/DATA/ENV/" + level;
            if (level.Replace('\\', '/').StartsWith("PRODUCTION/DLC/BSPNOSTROMO", StringComparison.OrdinalIgnoreCase))
                PatchFolder = LevelFolder + "_PATCH";
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
            try
            {
                Directory.CreateDirectory(BackupFolder);
                //Another window (or tool) may have written the archive since this one read it
                MergeFromDisk();
                RemovePartialRevisions();

                //IDs are seconds: a second backup within the same second would share the first's ID, and only one could ever be restored
                long id = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (Backups.Any(o => o.ID == id))
                    id = Backups.Max(o => o.ID) + 1;
                AlienBackup Backup = new AlienBackup() { Name = name, Date = DateTime.Now.ToString("dd-MM-yy HH:mm:ss"), ID = id, GUIDs = new List<string>() };
                Dictionary<string, string> paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                List<KeyValuePair<string, string>> files = LevelFiles();
                for (int i = 0; i < files.Count; i++)
                {
                    string fileName = files[i].Key;
                    //Read once: what is stored is exactly what its name (the MD5 of it) was worked out from
                    byte[] content = File.ReadAllBytes(files[i].Value);
                    string fileHash = GenerateHash(content);

                    AlienFile file = Files.FirstOrDefault(o => o.Name == fileName);
                    if (file == null)
                    {
                        file = new AlienFile() { Name = fileName, Revisions = new List<string>() };
                        Files.Add(file);
                    }

                    //A revision already stored is reused, unless an older OpenCAGE was cut off writing it: that one is written again
                    string revisionFile = BackupFolder + "/" + fileHash + ".gz";
                    if (!RevisionLooksWhole(revisionFile, content.LongLength))
                        StoreRevision(revisionFile, content);
                    if (!file.Revisions.Contains(fileHash))
                        file.Revisions.Add(fileHash);

                    Backup.GUIDs.Add(fileHash);
                    paths[fileName] = fileHash;
                }

                //Listed only once every file it holds is stored: a backup cut off before this point is not one at all
                Backups.Add(Backup);
                PathMaps[id] = paths;
                try
                {
                    Save();
                }
                catch
                {
                    Backups.Remove(Backup);
                    PathMaps.Remove(id);
                    throw;
                }
            }
            catch (IOException e) when (IsOutOfSpace(e))
            {
                throw new IOException("There is not enough free space on " + DriveOf(BackupFolder) + " to back up " + _level + ". Nothing was added to its backups (the ones made before are unaffected): free up some space, then try again.", e.HResult);
            }
        }

        /* The level's files, by their name in a backup and where they are: its folder's, then a Nostromo level's _PATCH folder's */
        private List<KeyValuePair<string, string>> LevelFiles()
        {
            List<KeyValuePair<string, string>> files = Directory.GetFiles(LevelFolder, "*.*", SearchOption.AllDirectories)
                .Select(o => new KeyValuePair<string, string>(o.Substring(LevelFolder.Length + 1), o)).ToList();
            if (PatchFolder != null && Directory.Exists(PatchFolder))
                files.AddRange(Directory.GetFiles(PatchFolder, "*.*", SearchOption.AllDirectories)
                    .Select(o => new KeyValuePair<string, string>(PatchPrefix + o.Substring(PatchFolder.Length + 1), o)));
            return files;
        }

        private bool IsPatchFile(string name) => PatchFolder != null && name.Replace('/', '\\').StartsWith(PatchPrefix, StringComparison.OrdinalIgnoreCase);

        /* Where a file a backup names (either separator) is on disk */
        private string PathOf(string name)
        {
            if (IsPatchFile(name))
                return PatchFolder + "/" + name.Replace('/', '\\').Substring(PatchPrefix.Length).Replace('\\', '/');
            return LevelFolder + "/" + name.Replace('\\', '/');
        }

        /* Whether a stored revision of content this long is whole. Gzip ends with the length of what it holds: a revision an
           older OpenCAGE was cut off writing (the disk filling up, OpenCAGE closed or killed part way) does not. */
        private static bool RevisionLooksWhole(string path, long length)
        {
            if (!File.Exists(path)) return false;
            using (FileStream stream = File.OpenRead(path))
            {
                if (stream.Length == 0) return length == 0;
                if (stream.Length < 18) return false;
                byte[] size = new byte[4];
                stream.Seek(-4, SeekOrigin.End);
                return stream.Read(size, 0, 4) == 4 && BitConverter.ToUInt32(size, 0) == (uint)length;
            }
        }

        /* Store a revision: written aside, flushed to the disk, and only then given its name. A write cut off part way never
           leaves a short revision under the name every later backup reuses and every restore reads. */
        private static void StoreRevision(string path, byte[] content)
        {
            string partial = path + ".tmp";
            try
            {
                using (FileStream stream = File.Create(partial))
                {
                    using (GZipStream gzipStream = new GZipStream(stream, CompressionMode.Compress, true))
                        gzipStream.Write(content, 0, content.Length);
                    stream.Flush(true);
                }
                MoveIntoPlace(partial, path);
            }
            catch
            {
                try { File.Delete(partial); } catch { }
                throw;
            }
        }

        /* Give a file written aside the name it was written for, replacing what had it */
        private static void MoveIntoPlace(string partial, string path)
        {
            if (!File.Exists(path))
            {
                File.Move(partial, path);
                return;
            }
            try
            {
                File.Replace(partial, path, null);
            }
            catch (IOException) when (File.Exists(partial) && File.Exists(path))
            {
                //A drive that can't swap files in one go (some network shares): the old one goes first
                File.Delete(path);
                File.Move(partial, path);
            }
        }

        /* Revisions a backup was cut off writing are left as ".tmp" files: their space is freed (one another OpenCAGE is
           writing at this moment is held open, and stays) */
        private void RemovePartialRevisions()
        {
            foreach (string partial in Directory.GetFiles(BackupFolder, "*.gz.tmp"))
            {
                try { File.Delete(partial); } catch { }
            }
        }

        /// <summary>Whether an error is the drive running out of space (or the user's quota on it running out).</summary>
        public static bool IsOutOfSpace(Exception error)
        {
            for (Exception e = error; e != null; e = e.InnerException)
            {
                //ERROR_DISK_FULL, ERROR_HANDLE_DISK_FULL, ERROR_DISK_QUOTA_EXCEEDED
                if (e is IOException && (e.HResult == unchecked((int)0x80070070) || e.HResult == unchecked((int)0x80070027) || e.HResult == unchecked((int)0x8007050F)))
                    return true;
            }
            return false;
        }

        /* The drive a path is on, for messages */
        private static string DriveOf(string path)
        {
            try { return Path.GetPathRoot(Path.GetFullPath(path)); }
            catch { return "the drive"; }
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
                //A backup with no path map was taken before backups held a _PATCH folder: matching one of its files by hash
                //(the same content as a file of the level folder) would have a restore clear that folder down to it
                if (IsPatchFile(file.Name)) continue;
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

        /// <summary>The files on disk now, by level-relative path ('\' separated; a Nostromo level's _PATCH folder's as
        /// "_PATCH\...") and hash.</summary>
        public Dictionary<string, string> CurrentPaths()
        {
            List<KeyValuePair<string, string>> files = LevelFiles();
            string[] hashes = new string[files.Count];
            Parallel.For(0, files.Count, (i) => { hashes[i] = GenerateFileHash(files[i].Value); });
            Dictionary<string, string> paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Count; i++)
                paths[files[i].Key] = hashes[i];
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
        /// <summary>What made the last <see cref="RestoreBackup"/> fail (null when it succeeded), e.g. the drive filling up.</summary>
        public Exception LastRestoreError { get; private set; } = null;
        public string SafetyCopyFolder => LevelFolder + "_COPY";

        /// <summary>
        /// Thrown by <see cref="RestoreBackup"/>, before anything is changed, when files the backup holds are missing from
        /// its folder or were not stored completely (the disk filled up, or OpenCAGE was closed, while it was being made).
        /// </summary>
        public class DamagedBackupException : Exception
        {
            public List<string> DamagedFiles { get; }
            public DamagedBackupException(string backupName, List<string> damagedFiles)
                : base("The backup '" + backupName + "' is damaged, so it can't be restored: " + damagedFiles.Count + " of the files it holds " + (damagedFiles.Count == 1 ? "is" : "are") + " missing or incomplete ("
                      + string.Join(", ", damagedFiles.Take(5)) + (damagedFiles.Count > 5 ? ", ..." : "") + "). It may have been cut off while it was made (the disk filling up, or OpenCAGE closing). Nothing in the level was changed.")
            {
                DamagedFiles = damagedFiles;
            }
        }

        /* Restore a backup. With onlyPaths (level-relative, either separator), only those files are put back as the backup
           holds them: written if it has them, deleted if it does not; the rest of the folder is left alone. Throws a
           DamagedBackupException, changing nothing, if any file it would put back is missing or incomplete in the backup. */
        public bool RestoreBackup(Int64 ID, ICollection<string> onlyPaths = null)
        {
            LastRestoreFailure = RestoreFailure.Untouched;
            LastRestoreError = null;
            AlienBackup backup = Backups.FirstOrDefault(o => o.ID == ID);
            if (backup == null) return false;
            Dictionary<string, string> paths = PathsOf(backup, out _);

            //Every file it puts back is checked first: one cut off while it was stored would otherwise read back short without
            //complaint, and the restore would write it into the level and report success
            List<string> damaged = DamagedFiles(paths, onlyPaths);
            if (damaged.Count != 0)
                throw new DamagedBackupException(backup.Name, damaged);

            if (onlyPaths != null)
                return RestoreSome(paths, onlyPaths);

            //The folders it replaces: the level's, and a Nostromo level's _PATCH folder when the backup holds it (a backup
            //taken before backups held it leaves it as it is)
            List<string> folders = new List<string>() { LevelFolder };
            if (paths.Keys.Any(IsPatchFile))
                folders.Add(PatchFolder);

            //First, try keep a version of the current folders
            try
            {
                foreach (string folder in folders)
                {
                    if (Directory.Exists(folder + "_COPY"))
                        Directory.Delete(folder + "_COPY", true);
                    if (folder == PatchFolder) Directory.CreateDirectory(folder); //One the backup holds may have been deleted since
                    CopyDirectory(folder, folder + "_COPY", true);
                }
            }
            catch (Exception e)
            {
                //Nothing is changed yet: a copy made part way (the disk filling up) is only taking space
                LastRestoreError = e;
                foreach (string folder in folders)
                {
                    try { if (Directory.Exists(folder + "_COPY")) Directory.Delete(folder + "_COPY", true); } catch { }
                }
                return false;
            }

            //Now, try clear the current folders
            try
            {
                foreach (string folder in folders)
                {
                    Directory.Delete(folder, true);
                    Directory.CreateDirectory(folder);
                }
            }
            catch (Exception e)
            {
                //Part of the folder may be gone already: put it back from the copy rather than throwing the copy away
                LastRestoreError = e;
                LastRestoreFailure = RollBackRestore(folders) ? RestoreFailure.RolledBack : RestoreFailure.Damaged;
                return false;
            }

            //Restore the backup: each file once, as the backup holds it (not every revision of it whose content the backup has)
            try
            {
                foreach (KeyValuePair<string, string> file in paths)
                    WriteRevision(file.Key, file.Value);
            }
            catch (Exception e)
            {
                LastRestoreError = e;
                LastRestoreFailure = RollBackRestore(folders) ? RestoreFailure.RolledBack : RestoreFailure.Damaged;
                return false;
            }

            //Clear the copies
            try
            {
                foreach (string folder in folders)
                    Directory.Delete(folder + "_COPY", true);
            }
            catch (Exception e)
            {
                LastRestoreError = e;
                LastRestoreFailure = RestoreFailure.CopyLeftBehind;
                return false;
            }

            LastRestoreFailure = RestoreFailure.None;
            return true;
        }

        /* The files of a backup (of those named, when only some are restored) whose stored copy is missing or does not read
           back whole. Each stored revision is read once, however many files share it. */
        private List<string> DamagedFiles(Dictionary<string, string> paths, ICollection<string> onlyPaths)
        {
            List<KeyValuePair<string, string>> checking = paths.ToList();
            if (onlyPaths != null)
            {
                HashSet<string> wanted = new HashSet<string>(onlyPaths.Select(o => o.Replace('/', '\\').Trim('\\')), StringComparer.OrdinalIgnoreCase);
                checking = checking.Where(o => wanted.Contains(o.Key.Replace('/', '\\'))).ToList();
            }
            string[] hashes = checking.Select(o => o.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            bool[] whole = new bool[hashes.Length];
            Parallel.For(0, hashes.Length, (i) => { whole[i] = RevisionIsWhole(hashes[i]); });
            HashSet<string> bad = new HashSet<string>(hashes.Where((hash, i) => !whole[i]), StringComparer.OrdinalIgnoreCase);
            return checking.Where(o => bad.Contains(o.Value)).Select(o => o.Key).OrderBy(o => o, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /* Whether a stored revision is there and reads back to what it was stored from: its name is that content's MD5 */
        private bool RevisionIsWhole(string hash)
        {
            try
            {
                using (Stream stored = OpenRevision(hash))
                using (MD5 md5 = MD5.Create())
                {
                    string read = BitConverter.ToString(md5.ComputeHash(stored)).Replace("-", "").ToLower();
                    return !IsContentHash(hash) || string.Equals(read, hash, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (IOException) { return false; } //Missing (FileNotFoundException), or unreadable
            catch (InvalidDataException) { return false; } //Not gzip data
            catch (UnauthorizedAccessException) { return false; }
        }

        /* A revision's name is the MD5 of its content, as 32 hex digits */
        private static bool IsContentHash(string hash) => hash != null && hash.Length == 32 && hash.All(Uri.IsHexDigit);

        /* One stored revision, to read (compressed, or a legacy uncompressed one) */
        private Stream OpenRevision(string hash)
        {
            if (File.Exists(BackupFolder + "/" + hash))
                return File.OpenRead(BackupFolder + "/" + hash);
            return new GZipStream(File.OpenRead(BackupFolder + "/" + hash + ".gz"), CompressionMode.Decompress);
        }

        /* One stored revision's bytes (compressed, or a legacy uncompressed one). Throws if they are not what was stored. */
        public byte[] ReadRevision(string hash)
        {
            byte[] content;
            using (Stream stored = OpenRevision(hash))
            using (MemoryStream stream = new MemoryStream())
            {
                stored.CopyTo(stream);
                content = stream.ToArray();
            }
            //A revision cut off while it was written reads back short without complaint: its name (the MD5 of it) shows it
            if (IsContentHash(hash) && !string.Equals(GenerateHash(content), hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The backup's copy of this file (" + hash + ") is damaged: it was not stored completely.");
            return content;
        }

        private void WriteRevision(string name, string hash)
        {
            string target = PathOf(name);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllBytes(target, ReadRevision(hash));
        }

        /* Put back only some files: copied aside first, and put back from the copy if any of it fails */
        private bool RestoreSome(Dictionary<string, string> paths, ICollection<string> onlyPaths)
        {
            string copy = LevelFolder + "_COPY";
            List<string> wanted = onlyPaths.Select(o => o.Replace('/', '\\').Trim('\\')).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            //A backup taken before backups held the _PATCH folder knows nothing of it: its files are left alone, never deleted
            if (!paths.Keys.Any(IsPatchFile))
                wanted.RemoveAll(IsPatchFile);
            try
            {
                if (Directory.Exists(copy))
                    Directory.Delete(copy, true);
                foreach (string name in wanted)
                {
                    string current = PathOf(name);
                    if (!File.Exists(current)) continue;
                    string aside = copy + "/" + name.Replace('\\', '/');
                    Directory.CreateDirectory(Path.GetDirectoryName(aside));
                    File.Copy(current, aside, true);
                }
            }
            catch (Exception e)
            {
                LastRestoreError = e;
                try { if (Directory.Exists(copy)) Directory.Delete(copy, true); } catch { }
                return false;
            }
            try
            {
                foreach (string name in wanted)
                {
                    string stored = paths.FirstOrDefault(o => string.Equals(o.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
                    if (stored != null)
                        WriteRevision(name, stored);
                    else if (File.Exists(PathOf(name)))
                        File.Delete(PathOf(name));
                }
            }
            catch (Exception e)
            {
                LastRestoreError = e;
                //Write back what was copied aside, and remove what the backup added
                try
                {
                    foreach (string name in wanted)
                    {
                        string current = PathOf(name);
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
            catch (Exception e)
            {
                LastRestoreError = e;
                LastRestoreFailure = RestoreFailure.CopyLeftBehind;
                return false;
            }
            LastRestoreFailure = RestoreFailure.None;
            return true;
        }

        /* Put each folder back from the copy a restore took before it began; a copy is kept if that fails */
        private bool RollBackRestore(List<string> folders)
        {
            bool all = true;
            foreach (string folder in folders)
                all &= RollBackRestore(folder);
            return all;
        }
        private bool RollBackRestore(string folder)
        {
            string copy = folder + "_COPY";
            if (!Directory.Exists(copy))
                return false;
            try
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
                Directory.Move(copy, folder);
                return true;
            }
            catch { }
            //Something in the folder is still held open: write the copy's files back over it instead
            try
            {
                foreach (string file in Directory.GetFiles(copy, "*.*", SearchOption.AllDirectories))
                {
                    string target = folder + file.Substring(copy.Length);
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
            HashSet<string> unused = new HashSet<string>();
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
                        unused.Add(Files[i].Revisions[x]);
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

            //Only once the archive no longer lists the backup: a save that failed would leave it listed with its files gone
            foreach (string revision in unused)
            {
                if (File.Exists(BackupFolder + "/" + revision))
                    File.Delete(BackupFolder + "/" + revision);
                if (File.Exists(BackupFolder + "/" + revision + ".gz"))
                    File.Delete(BackupFolder + "/" + revision + ".gz");
            }
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
            try { Read(BackupFile, Backups, Files, PathMaps); }
            catch (Exception e) { throw new InvalidDataException(Path.GetFullPath(BackupFile) + " could not be read: " + e.Message, e); }
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

        /* Save the backup archive out to disk: written aside and swapped in, as an archive cut off part way would not read (and
           its level's backups could not be opened) */
        private void Save()
        {
            string partial = BackupFile + ".tmp";
            try
            {
                WriteArchive(partial);
                MoveIntoPlace(partial, BackupFile);
            }
            catch
            {
                try { File.Delete(partial); } catch { }
                throw;
            }
            SeenOnDisk = new HashSet<long>(Backups.Select(o => o.ID));
        }
        private void WriteArchive(string archive)
        {
            using (BinaryWriter writer = new BinaryWriter(File.Create(archive)))
            {
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

                writer.Flush();
                ((FileStream)writer.BaseStream).Flush(true);
            }
        }

        /* Generate a hash for a given file */
        private string GenerateFileHash(string path)
        {
            return GenerateHash(File.ReadAllBytes(path));
        }
        private static string GenerateHash(byte[] content)
        {
            using (MD5 md5 = MD5.Create())
                return BitConverter.ToString(md5.ComputeHash(content)).Replace("-", "").ToLower();
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
