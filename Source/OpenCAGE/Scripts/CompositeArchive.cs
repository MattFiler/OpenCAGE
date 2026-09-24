using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CathodeLib.CompositeFlowgraphTable;

namespace OpenCAGE
{
    /// <summary>
    /// Composites, with everything they use, as one file on disk: an OpenCAGE Composite Package
    /// (<c>.ocp</c>) that can be imported into a level on another install, or another machine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The porting itself is <see cref="CompositePorter"/>'s, unchanged. What this adds is a middle man:
    /// the composites are ported into a blank scratch level in a temp folder, that level is saved, and
    /// its files are packed into the package behind a manifest. Importing unpacks the level and ports
    /// out of it into the destination - exactly the path a level-to-level port takes, so every rule that
    /// port has (resource dedupe, Havok proxies, pin info, layouts) applies here too. The destination is
    /// the level open in the editor (in memory, kept by saving), or any number of levels on disk when
    /// nothing is open (each loaded, written and saved in turn).
    /// </para>
    /// <para>
    /// The scratch level is scaffolded from FRONTEND (the smallest Havok files the game ships) with
    /// <see cref="Level.AbsorbGlobalTextures"/> off: a level normally copies every global texture into
    /// its own pak on load and save, which is what keeps it self-contained but would put ~80MB nothing
    /// in the package references into every export. The source level was loaded normally, so its
    /// materials already point at level copies and the porter carries exactly the textures they use.
    /// </para>
    /// <para>
    /// Format: one gzip stream. First the 8-byte magic, then the manifest (int32 length + UTF-8 JSON),
    /// then each file as {int16 path length, path, int64 length, bytes}, then an int16 0 terminator.
    /// The manifest comes first so the header can be read without inflating the payload, and it lists
    /// every file with its length so a truncated package is refused rather than imported with holes.
    /// <see cref="FormatVersion"/> in the manifest is the container's version, bumped when a reader
    /// would need to change.
    /// </para>
    /// </remarks>
    public static class CompositeArchive
    {
        public const string Extension = PackageFiles.CompositeExtension;
        public const string FileFilter = "OpenCAGE Composite Packages (*" + Extension + ")|*" + Extension + "|All files (*.*)|*.*";

        /// <summary>Bumped when the container or manifest changes shape in a way an older reader cannot take.</summary>
        public const int FormatVersion = 1;

        private const string Magic = "CATHODE\u0001";
        private const string ScratchLevelName = "CATHODE_ARCHIVE";
        private const int MaxEntryPathLength = 512;
        private const int MaxManifestLength = 64 * 1024 * 1024;
        private static readonly string[] RequiredFiles = { "WORLD/COMMANDS.PAK", "WORLD/EXCLUSIVE_MASTER_RESOURCE_INDICES" };

        /* GLOBAL and PAUSEMENU: identical on every level and instanced by the engine on every level,
           and reachable from an ordinary composite's closure through the required-asset scripts. Neither
           direction descends into them - the destination has its own - unless one was ticked itself. */
        private static readonly HashSet<ShortGuid> LevelEntryPoints = new HashSet<ShortGuid>()
        {
            new ShortGuid("1D-2E-CE-E5"), //GLOBAL
            new ShortGuid("FE-7B-FE-B3"), //PAUSEMENU
        };

        public class Manifest
        {
            public int FormatVersion;
            public string OpenCAGEVersion;
            public string OpenCAGEBeta;
            public string Platform;
            public string SourceLevel;
            public DateTime ExportedUtc;

            //What the author called it - shown when the package is imported
            public string Name;
            public string Description;

            public List<CompositeEntry> Composites = new List<CompositeEntry>();
            public List<FileEntry> Files = new List<FileEntry>();

            /// <summary>The platform the package came from, or UNKNOWN if the manifest names one we don't have.</summary>
            public PatchManager.Platform ParsedPlatform =>
                Enum.TryParse(Platform ?? "", true, out PatchManager.Platform platform) ? platform : PatchManager.Platform.UNKNOWN;

            /// <summary>The name to show for the package: what the author called it, else its file name.</summary>
            public string DisplayName(string archivePath)
            {
                if (!string.IsNullOrWhiteSpace(Name))
                    return Name.Trim();
                return string.IsNullOrEmpty(archivePath) ? "package" : System.IO.Path.GetFileNameWithoutExtension(archivePath);
            }
        }

        /// <summary>What the author says about a package at export.</summary>
        public class PackageInfo
        {
            public string Name;
            public string Description;
        }

        public class CompositeEntry
        {
            public uint Guid;
            public string Name;
            /// <summary>Ticked by the user at export, as opposed to brought along because something ticked instances it.</summary>
            public bool IsRoot;
        }

        public class FileEntry
        {
            public string Path;
            public long Length;
        }

        public class ImportOptions
        {
            public bool OverwriteComposites = false;
            public bool OverwriteAssets = false;
        }

        public class Result
        {
            /// <summary>The composites that arrived. Emptied for a destination on disk, which is let go of afterwards.</summary>
            public List<Composite> Ported = new List<Composite>();
            public int PortedCount;
            public int Renderables, CollisionMappings, PhysicsSystems, AnimatedModels, EnvironmentMaps;
            /// <summary>The proxies among the ported composites that resolve to nothing in the destination.</summary>
            public DeadProxyReport DeadProxies = new DeadProxyReport();
        }

        /// <summary>One destination of an import into levels on disk.</summary>
        public class LevelResult
        {
            public string Level;
            /// <summary>What arrived, or null when the level failed (see <see cref="Error"/>).</summary>
            public Result Result;
            /// <summary>Why this level failed; the loop stops at the first failure.</summary>
            public string Error;
        }

        #region EXPORT
        /// <summary>
        /// Write <paramref name="composites"/> from <paramref name="source"/> (the level open in the
        /// editor) to a package at <paramref name="archivePath"/>.
        /// </summary>
        public static Manifest Export(Level source, List<Composite> composites, string archivePath, PackageInfo info = null)
        {
            if (source?.Commands == null) throw new ArgumentException("The source level must be loaded.", nameof(source));
            if (composites == null || composites.Count == 0) throw new ArgumentException("Nothing to export.", nameof(composites));
            if (string.IsNullOrEmpty(archivePath)) throw new ArgumentException("No package path.", nameof(archivePath));

            string tempRoot = NewTempRoot();
            string levelPath = Path.Combine(tempRoot, "DATA", "ENV", ScratchLevelName);
            try
            {
                //FRONTEND holds the campaign variant of every table a level cannot generate and the smallest
                //Havok scaffolds the game ships - the base for every blank level (see CreateLevel). Only its
                //files are wanted, straight from its folder: nothing here needs it loaded.
                string frontend = Path.Combine(Singleton.PathToAI, "DATA", "ENV", EditorUtils.FrontendLevel.Replace('/', Path.DirectorySeparatorChar));
                Level scratch = Level.MakeBlankLevel(levelPath, frontend, Singleton.Global, absorbGlobalTextures: false);

                CompositeFlowgraphTable layouts = new CompositeFlowgraphTable();
                CompositeParameterModificationTable modifications = new CompositeParameterModificationTable();
                EntityAppliedDefaultsTable defaults = new EntityAppliedDefaultsTable();

                CompositePorter porter;
                using (ProgressUI progress = new ProgressUI())
                {
                    progress.ShowTransferring("Exporting composites...");
                    progress.KeepOnTop();

                    porter = new CompositePorter(source, scratch)
                    {
                        OverwriteComposites = true,
                        OverwriteAssets = true,
                        Recurse = true,
                        DoNotDescendInto = new HashSet<ShortGuid>(LevelEntryPoints),
                    };
                    porter.OnProgress = progress.DoRefresh;
                    porter.OnCompositePorted = (original, copy) =>
                    {
                        //What the porter cannot know about: the editor's own metadata for the composite. Pages
                        //come from the loaded level's layouts (with the bundled predefined pages as fallback).
                        layouts.flowgraphs.RemoveAll(o => o.CompositeGUID == original.shortGUID);
                        layouts.flowgraphs.AddRange(FlowgraphLayoutManager.GetLayoutsForPort(original));
                        ParameterModificationTracker.ExportCompositeRows(original.shortGUID, modifications, defaults);
                    };
                    foreach (Composite composite in composites)
                        porter.Port(composite);

                    progress.Close();
                }

                if (porter.PortedComposites.Count == 0)
                    throw new InvalidOperationException("None of the chosen composites could be ported.");

                /* Commands.Save gives a script with no entry points a blank GLOBAL and PAUSEMENU, found by
                   those exact names - and the real ones are named 'Global' and 'PauseMenu', so a ticked
                   Global would sit beside a blank twin with the same ID. Name the ported copies as the
                   entry points when they are here, so the save has nothing to invent. */
                scratch.Commands.SetEntryPoints(
                    porter.PortedComposites[0],
                    scratch.Commands.GetComposite(new ShortGuid("1D-2E-CE-E5")),
                    scratch.Commands.GetComposite(new ShortGuid("FE-7B-FE-B3")));

                /* Materials that came across still pointing into global would be written as raw global
                   indices - which only mean the same thing on another install as long as its global is
                   the retail one. Bring just the textures they name into the scratch instead, so the
                   package stands on its own; with absorption off, Save will not add the rest. */
                int absorbed = scratch.AbsorbReferencedGlobalTextures();
                Debug.Log("Composite Archive", "Absorbed " + absorbed + " global texture reference(s) into the package");

                using (ProgressUI progress = new ProgressUI())
                {
                    progress.ShowLevelSaving(scratch, false);
                    progress.KeepOnTop();
                    scratch.Save();
                    progress.Close();
                }
                string commandsPath = scratch.Commands.Filepath;
                CustomTable.WriteTable(commandsPath, CustomTableType.COMPOSITE_FLOWGRAPHS, layouts);
                CustomTable.WriteTable(commandsPath, CustomTableType.COMPOSITE_PARAMETER_MODIFICATION, modifications);
                CustomTable.WriteTable(commandsPath, CustomTableType.ENTITY_APPLIED_DEFAULTS, defaults);

                Manifest manifest = new Manifest()
                {
                    FormatVersion = FormatVersion,
                    OpenCAGEVersion = Singleton.Version,
                    OpenCAGEBeta = Singleton.BetaName,
                    Platform = Singleton.Platform.ToString(),
                    SourceLevel = source.Name,
                    ExportedUtc = DateTime.UtcNow,
                    Name = info?.Name?.Trim(),
                    Description = info?.Description?.Trim(),
                };
                HashSet<ShortGuid> roots = new HashSet<ShortGuid>(composites.Select(o => o.shortGUID));
                foreach (Composite ported in porter.PortedComposites)
                    manifest.Composites.Add(new CompositeEntry() { Guid = ported.shortGUID.AsUInt32, Name = ported.name, IsRoot = roots.Contains(ported.shortGUID) });

                //The level is on disk now; let go of it before the folder is packed and removed
                scratch = null;
                porter = null;
                Collect();

                WriteArchive(archivePath, levelPath, manifest);
                return manifest;
            }
            finally
            {
                TryDeleteTemp(tempRoot);
            }
        }

        private static void WriteArchive(string archivePath, string levelPath, Manifest manifest)
        {
            //Enumerate against the path we created: Level upper-cases its own copy of it
            string root = Path.GetFullPath(levelPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            List<string> files = new List<string>();
            foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(root.Length).Replace('\\', '/');
                //Commands.Save writes the BIN beside the PAK, and Load takes the PAK when it is there
                if (string.Equals(relative, "WORLD/COMMANDS.BIN", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (Encoding.UTF8.GetByteCount(relative) > MaxEntryPathLength)
                    throw new IOException("A file in the scratch level has a path too long to package: " + relative);
                files.Add(relative);
            }
            files.Sort(StringComparer.OrdinalIgnoreCase);

            manifest.Files.Clear();
            foreach (string relative in files)
                manifest.Files.Add(new FileEntry() { Path = relative, Length = new FileInfo(Path.Combine(root, relative)).Length });

            string tempArchive = archivePath + ".tmp";
            try
            {
                using (FileStream file = File.Create(tempArchive))
                using (GZipStream gzip = new GZipStream(file, CompressionLevel.Optimal))
                using (BinaryWriter writer = new BinaryWriter(gzip, Encoding.UTF8, true))
                {
                    writer.Write(Encoding.ASCII.GetBytes(Magic));

                    byte[] manifestBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(manifest, Formatting.Indented));
                    writer.Write(manifestBytes.Length);
                    writer.Write(manifestBytes);

                    byte[] buffer = new byte[1 << 20];
                    foreach (FileEntry entry in manifest.Files)
                    {
                        byte[] pathBytes = Encoding.UTF8.GetBytes(entry.Path);
                        writer.Write((short)pathBytes.Length);
                        writer.Write(pathBytes);
                        writer.Write(entry.Length);

                        using (FileStream source = File.OpenRead(Path.Combine(root, entry.Path)))
                        {
                            long remaining = entry.Length;
                            while (remaining > 0)
                            {
                                int read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                                if (read <= 0)
                                    throw new IOException("File changed while it was being packaged: " + entry.Path);
                                writer.Write(buffer, 0, read);
                                remaining -= read;
                            }
                        }
                    }

                    writer.Write((short)0);
                }

                if (File.Exists(archivePath))
                    File.Delete(archivePath);
                File.Move(tempArchive, archivePath);
            }
            finally
            {
                if (File.Exists(tempArchive))
                {
                    try { File.Delete(tempArchive); } catch { }
                }
            }
        }
        #endregion

        #region READ
        /// <summary>
        /// The manifest alone. The package is only inflated as far as the header, so this is cheap
        /// enough to run on a file the user has just picked.
        /// </summary>
        public static Manifest ReadHeader(string archivePath)
        {
            using (FileStream file = File.OpenRead(archivePath))
            using (GZipStream gzip = new GZipStream(file, CompressionMode.Decompress))
            using (BinaryReader reader = new BinaryReader(gzip, Encoding.UTF8, true))
            {
                return ReadManifest(reader);
            }
        }

        private static Manifest ReadManifest(BinaryReader reader)
        {
            byte[] magic;
            try
            {
                magic = reader.ReadBytes(Magic.Length);
            }
            catch (InvalidDataException)
            {
                throw new InvalidDataException("This is not an OpenCAGE composite package.");
            }
            if (magic.Length != Magic.Length || Encoding.ASCII.GetString(magic) != Magic)
                throw new InvalidDataException("This is not an OpenCAGE composite package.");

            int length = reader.ReadInt32();
            if (length <= 0 || length > MaxManifestLength)
                throw new InvalidDataException("The package's header is damaged.");

            byte[] json = reader.ReadBytes(length);
            if (json.Length != length)
                throw new InvalidDataException("The package is truncated.");

            Manifest manifest = JsonConvert.DeserializeObject<Manifest>(Encoding.UTF8.GetString(json));
            if (manifest == null)
                throw new InvalidDataException("The package's header is damaged.");
            if (manifest.FormatVersion > FormatVersion)
                throw new InvalidDataException("This package was written by a newer version of OpenCAGE (format " + manifest.FormatVersion + ", this build reads up to " + FormatVersion + ").");
            if (manifest.Composites == null) manifest.Composites = new List<CompositeEntry>();
            if (manifest.Files == null) manifest.Files = new List<FileEntry>();
            return manifest;
        }

        /* Unpack the level into levelPath, refusing anything that is not exactly what the manifest
           promised: an entry the manifest does not list, a length that differs, a file missing at the end,
           or a path that would land outside the folder. */
        private static Manifest Extract(string archivePath, string levelPath)
        {
            string root = Path.GetFullPath(levelPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(root);

            Manifest manifest;
            Dictionary<string, long> received = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (FileStream file = File.OpenRead(archivePath))
                using (GZipStream gzip = new GZipStream(file, CompressionMode.Decompress))
                using (BinaryReader reader = new BinaryReader(gzip, Encoding.UTF8, true))
                {
                    manifest = ReadManifest(reader);
                    Dictionary<string, long> expected = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                    foreach (FileEntry entry in manifest.Files)
                        expected[entry.Path] = entry.Length;

                    byte[] buffer = new byte[1 << 20];
                    while (true)
                    {
                        short pathLength = reader.ReadInt16();
                        if (pathLength == 0)
                            break;
                        if (pathLength < 0 || pathLength > MaxEntryPathLength)
                            throw new InvalidDataException("The package is damaged (bad entry).");

                        string relative = Encoding.UTF8.GetString(reader.ReadBytes(pathLength));
                        long length = reader.ReadInt64();
                        if (length < 0)
                            throw new InvalidDataException("The package is damaged (bad entry length).");
                        if (!expected.TryGetValue(relative, out long promised) || promised != length)
                            throw new InvalidDataException("The package is damaged: '" + relative + "' does not match its manifest.");
                        if (received.ContainsKey(relative))
                            throw new InvalidDataException("The package is damaged: '" + relative + "' appears twice.");

                        string destination = SafeEntryPath(root, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        using (FileStream output = File.Create(destination))
                        {
                            long remaining = length;
                            while (remaining > 0)
                            {
                                int read = reader.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                                if (read <= 0)
                                    throw new EndOfStreamException();
                                output.Write(buffer, 0, read);
                                remaining -= read;
                            }
                        }
                        received[relative] = length;
                    }
                }
            }
            catch (EndOfStreamException)
            {
                throw new InvalidDataException("The package is truncated - it ends before all of its files do.");
            }

            foreach (FileEntry entry in manifest.Files)
            {
                if (!received.TryGetValue(entry.Path, out long length) || length != entry.Length)
                    throw new InvalidDataException("The package is incomplete: '" + entry.Path + "' is missing.");
            }
            foreach (string required in RequiredFiles)
            {
                if (!received.ContainsKey(required))
                    throw new InvalidDataException("The package holds no level to import from (no " + required + ").");
            }
            return manifest;
        }

        /* An entry path is only ever a relative path with forward slashes below the level root.
           Path.Combine hands back a rooted second argument untouched, so rooted and drive-relative
           forms are refused outright, and the resolved path is checked against the root as well. */
        private static string SafeEntryPath(string root, string relative)
        {
            if (string.IsNullOrEmpty(relative) || relative.Length > MaxEntryPathLength)
                throw new InvalidDataException("The package is damaged (bad entry path).");
            if (Path.IsPathRooted(relative) || relative.IndexOf(':') >= 0 || relative[0] == '/' || relative[0] == '\\')
                throw new InvalidDataException("The package is damaged (entry path is not relative): " + relative);
            foreach (string segment in relative.Split('/', '\\'))
            {
                if (segment.Length == 0 || segment == "." || segment == "..")
                    throw new InvalidDataException("The package is damaged (entry path is not relative): " + relative);
                if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    throw new InvalidDataException("The package is damaged (bad characters in entry path): " + relative);
            }

            string full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The package is damaged (entry path escapes the package): " + relative);
            return full;
        }
        #endregion

        #region IMPORT
        /* A package unpacked and loaded as a level, with the editor tables it carries, for as long as
           something is being ported out of it. Disposing lets the level go and removes the folder. */
        private sealed class OpenedArchive : IDisposable
        {
            public string TempRoot;
            public Manifest Manifest;
            public Level Scratch;
            public CompositeFlowgraphTable Layouts;
            public CompositeParameterModificationTable Modifications;
            public EntityAppliedDefaultsTable Defaults;

            public void Dispose()
            {
                Scratch = null;
                Layouts = null;
                Modifications = null;
                Defaults = null;
                TryDeleteTemp(TempRoot);
            }
        }

        /* The import into levels on disk runs on a worker thread while the editor pumps messages, so its
           progress windows are made and closed on the UI thread and only ticked from the worker (which
           ProgressUI already marshals). On the UI thread these are plain calls. */
        internal static T OnUiThread<T>(Func<T> action)
        {
            CommandsEditor editor = Singleton.Editor;
            if (editor != null && !editor.IsDisposed && editor.IsHandleCreated && editor.InvokeRequired)
                return (T)editor.Invoke(action);
            return action();
        }

        internal static void OnUiThread(Action action)
        {
            OnUiThread(() => { action(); return 0; });
        }

        internal static ProgressUI OpenProgress(Action<ProgressUI> show)
        {
            return OnUiThread(() =>
            {
                ProgressUI progress = new ProgressUI();
                show(progress);
                progress.KeepOnTop();
                return progress;
            });
        }

        internal static void CloseProgress(ProgressUI progress)
        {
            if (progress == null)
                return;
            OnUiThread(() =>
            {
                try { progress.Close(); progress.Dispose(); } catch { }
            });
        }

        /* A repaint per porter tick, coalesced when the ticks come from a worker thread */
        internal static Action RefreshAction(ProgressUI progress)
        {
            int pending = 0;
            return () =>
            {
                if (!progress.InvokeRequired)
                {
                    progress.DoRefresh();
                    return;
                }
                if (System.Threading.Interlocked.CompareExchange(ref pending, 1, 0) != 0)
                    return;
                try
                {
                    progress.BeginInvoke(new Action(() =>
                    {
                        System.Threading.Interlocked.Exchange(ref pending, 0);
                        progress.DoRefresh();
                    }));
                }
                catch
                {
                    System.Threading.Interlocked.Exchange(ref pending, 0);
                }
            };
        }

        private static OpenedArchive OpenArchive(string archivePath)
        {
            string tempRoot = NewTempRoot();
            string levelPath = Path.Combine(tempRoot, "DATA", "ENV", ScratchLevelName);
            try
            {
                Stopwatch timer = Stopwatch.StartNew();
                Manifest manifest = Extract(archivePath, levelPath);
                long extracted = timer.ElapsedMilliseconds;

                //The scratch's materials point only at its own textures, so there is nothing to absorb -
                //and absorbing would read this install's whole global set into memory for nothing
                Level scratch = new Level(levelPath, Singleton.Global, false) { AbsorbGlobalTextures = false };
                ProgressUI progress = OpenProgress(p => p.ShowLevelLoading(scratch));
                try { scratch.Load(); }
                finally { CloseProgress(progress); }
                Debug.Log("Composite Archive", "Opened " + Path.GetFileName(archivePath) + ": unpacked in " + extracted + " ms, scratch level loaded in " + (timer.ElapsedMilliseconds - extracted) + " ms");

                string commandsPath = scratch.Commands.Filepath;
                return new OpenedArchive()
                {
                    TempRoot = tempRoot,
                    Manifest = manifest,
                    Scratch = scratch,
                    Layouts = CustomTable.ReadTable(commandsPath, CustomTableType.COMPOSITE_FLOWGRAPHS) as CompositeFlowgraphTable,
                    Modifications = CustomTable.ReadTable(commandsPath, CustomTableType.COMPOSITE_PARAMETER_MODIFICATION) as CompositeParameterModificationTable,
                    Defaults = CustomTable.ReadTable(commandsPath, CustomTableType.ENTITY_APPLIED_DEFAULTS) as EntityAppliedDefaultsTable,
                };
            }
            catch
            {
                TryDeleteTemp(tempRoot);
                throw;
            }
        }

        /* The port itself, into whichever level: what the caller adds in onPorted is the editor
           metadata, which lives somewhere different for a loaded level and for one on disk. */
        private static Result PortInto(OpenedArchive archive, ICollection<uint> compositeIds, ImportOptions options, Level destination, string progressLabel, Action<Composite, Composite> onPorted)
        {
            Result result = new Result();
            ProgressUI progress = OpenProgress(p => p.ShowTransferring(progressLabel));
            try
            {
                CompositePorter porter = new CompositePorter(archive.Scratch, destination)
                {
                    OverwriteComposites = options.OverwriteComposites,
                    OverwriteAssets = options.OverwriteAssets,
                    Recurse = true,
                    DoNotDescendInto = new HashSet<ShortGuid>(LevelEntryPoints),
                };
                porter.OnProgress = RefreshAction(progress);
                porter.OnCompositePorted = (original, copy) =>
                {
                    result.Ported.Add(copy);
                    onPorted?.Invoke(original, copy);
                };

                foreach (uint id in compositeIds)
                {
                    Composite composite = archive.Scratch.Commands.GetComposite(new ShortGuid(id));
                    if (composite == null)
                    {
                        Debug.Log("Import", "Composite " + id + " is not in the package");
                        continue;
                    }
                    porter.Port(composite);
                }

                result.PortedCount = result.Ported.Count;
                result.Renderables = porter.RenderablesPorted;
                result.CollisionMappings = porter.CollisionMappingsPorted;
                result.PhysicsSystems = porter.PhysicsSystemsPorted;
                result.AnimatedModels = porter.AnimatedModelsPorted;
                result.EnvironmentMaps = porter.EnvironmentMapsPorted;
                //Judged here, against the destination, while the ported objects are still to hand
                result.DeadProxies = DeadProxyReport.Of(destination.Commands, result.Ported);
            }
            finally
            {
                CloseProgress(progress);
            }
            return result;
        }

        /// <summary>
        /// Port the composites with the given IDs out of the package into <paramref name="destination"/>
        /// (the level open in the editor - nothing touches disk until the user saves), handing each
        /// ported composite's flowgraph pages to the caller, who knows where the destination keeps them.
        /// </summary>
        public static Result Import(string archivePath, ICollection<uint> compositeIds, ImportOptions options, Level destination, Action<Composite, List<FlowgraphMeta>> onLayouts)
        {
            if (destination?.Commands == null) throw new ArgumentException("The destination level must be loaded.", nameof(destination));
            if (options == null) options = new ImportOptions();
            if (compositeIds == null || compositeIds.Count == 0)
                return new Result();

            Stopwatch timer = Stopwatch.StartNew();
            using (OpenedArchive archive = OpenArchive(archivePath))
            {
                long opened = timer.ElapsedMilliseconds;
                Result result = PortInto(archive, compositeIds, options, destination, "Importing from " + Path.GetFileName(archivePath) + "...", (original, copy) =>
                {
                    ParameterModificationTracker.ImportCompositeRows(copy.shortGUID, archive.Modifications, archive.Defaults);
                    //Pages the package carries for it; failing that, the bundled predefined pages for the
                    //level it was exported from (what CreateLevel does for composites with no pages)
                    onLayouts?.Invoke(copy, FlowgraphLayoutManager.GetLayoutsForPort(original, archive.Layouts, archive.Manifest.SourceLevel));
                });
                Debug.Log("Composite Archive", "Ported " + result.PortedCount + " composites in " + (timer.ElapsedMilliseconds - opened) + " ms (package opened in " + opened + " ms)");
                return result;
            }
        }

        /// <summary>
        /// Port the composites with the given IDs out of the package into levels on disk, none of which
        /// is open in the editor: each is loaded, receives the composites, and is saved (built, if
        /// <paramref name="buildAfterImport"/>). The package is unpacked once for all of them. The
        /// editor's tables in each level - pages, modified-parameter marks - are carried across the save
        /// and given the package's rows for what arrived. Meant to run on a worker thread while the
        /// editor pumps messages (see ImportCompositeArchive); the progress windows are put up on the UI
        /// thread either way.
        /// </summary>
        public static List<LevelResult> ImportIntoLevelsOnDisk(string archivePath, ICollection<uint> compositeIds, ImportOptions options, IList<string> levelNames, bool buildAfterImport)
        {
            if (levelNames == null || levelNames.Count == 0) throw new ArgumentException("No destination levels.", nameof(levelNames));
            if (options == null) options = new ImportOptions();

            List<LevelResult> results = new List<LevelResult>();
            if (compositeIds == null || compositeIds.Count == 0)
                return results;

            using (OpenedArchive archive = OpenArchive(archivePath))
            {
                foreach (string levelName in levelNames)
                {
                    LevelResult entry = new LevelResult() { Level = levelName };
                    results.Add(entry);
                    try
                    {
                        entry.Result = ImportIntoLevelOnDisk(archive, compositeIds, options, levelName, buildAfterImport);
                    }
                    catch (Exception e)
                    {
                        //Levels before this one are saved; this one may be part-written. Say so and stop.
                        entry.Error = e.Message;
                        break;
                    }
                    finally
                    {
                        Collect();
                    }
                }
            }
            return results;
        }
        private static Result ImportIntoLevelOnDisk(OpenedArchive archive, ICollection<uint> compositeIds, ImportOptions options, string levelName, bool buildAfterImport)
        {
            Level level = new Level(Singleton.PathToAI + "/DATA/ENV/" + levelName, Singleton.Global, false);
            ProgressUI loadProgress = OpenProgress(p => p.ShowLevelLoading(level));
            try { level.Load(); }
            finally { CloseProgress(loadProgress); }

            LevelEditorTables tables = LevelEditorTables.Read(level.Commands.Filepath, level.Commands);
            Result result = PortInto(archive, compositeIds, options, level, "Importing into " + levelName + "...", (original, copy) =>
            {
                //Composite previews are not carried in a package: the destination keeps what it has for the ID, else the shipped picture
                tables.ReplaceLayouts(copy.shortGUID, FlowgraphLayoutManager.GetLayoutsForPort(original, archive.Layouts, archive.Manifest.SourceLevel));
                ParameterModificationTracker.CopyCompositeRows(copy.shortGUID, archive.Modifications, archive.Defaults, tables.Modifications, tables.Defaults);
            });
            //Counts are all the caller needs; the objects would keep this level's meshes and textures alive
            result.Ported.Clear();

            //What the editor's own save does before writing a level: keep pristine copies for the mod tools
            Modding.ModServices.CaptureLevelBeforeSave(levelName);

            //Close alien down if it's open, it conflicts with our write locks!
            EditorUtils.CloseAI();

            /* Commands.Save truncates the script file to the script alone, so once the save has begun the
               editor's tables MUST go back in whatever happens after - even if a later part of the save
               throws, or the save reports failure with the script already rewritten. Otherwise the level is
               left saved and every page in it silently gone. Writing them when nothing was truncated is
               harmless: WriteTable keeps the script and rewrites only the table block. */
            ProgressUI saveProgress = OpenProgress(p => p.ShowLevelSaving(level, buildAfterImport));
            try
            {
                if (buildAfterImport)
                    level.SaveInstanced();
                else
                    level.Save();
            }
            finally
            {
                CloseProgress(saveProgress);
                //Re-resolved: a level that had only a BIN has a PAK now, and that is what loads next
                try { tables.Write(level.CommandsFilepath); }
                catch (Exception e) { Debug.Log("Composite Archive", "Could not write the editor tables to " + levelName + ": " + e.Message); }
            }
            return result;
        }
        #endregion

        #region HELPERS
        /* A folder of this process's own: another OpenCAGE exporting at the same moment gets a
           different one, and nothing here ever sweeps anyone else's. */
        private static string NewTempRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "OpenCAGE", "cathode_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        /* A full collection with finalisers run: the export lets its scratch level go before packing, and the
           on-disk import (on its worker) between levels, so thirty levels do not pile up */
        private static void Collect()
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        }

        /* The scratch has been let go of by the time this runs. Its files are normally free to go at
           once; one still held by something awaiting finalisation is chased on a worker - a forced
           collection of a level-sized heap costs seconds, and on the UI thread it sat squarely between
           an import finishing and the composite appearing. A handle that outlives even that costs a
           stray temp folder, not a failed import or export - so this never throws. */
        private static void TryDeleteTemp(string tempRoot)
        {
            if (string.IsNullOrEmpty(tempRoot) || !Directory.Exists(tempRoot))
                return;
            try
            {
                Directory.Delete(tempRoot, true);
                return;
            }
            catch (Exception e)
            {
                Debug.Log("Composite Archive", "Temporary folder still in use, will retry in the background: " + e.Message);
            }

            /* Whatever holds a file is on its way out (a reader awaiting finalisation, the indexer having a
               look at new files): plain retries first, and one collection only if a good while of those
               got nowhere - a forced collection pauses every thread, the UI's included */
            Task.Run(() =>
            {
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    Thread.Sleep(500);
                    if (!Directory.Exists(tempRoot))
                        return;
                    if (attempt == 20)
                    {
                        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                        GC.WaitForPendingFinalizers();
                    }
                    try
                    {
                        Directory.Delete(tempRoot, true);
                        Debug.Log("Composite Archive", "Removed the temporary folder on retry " + (attempt + 1));
                        return;
                    }
                    catch { }
                }
                Debug.Log("Composite Archive", "Could not remove the temporary folder " + tempRoot);
            });
        }
        #endregion
    }
}
