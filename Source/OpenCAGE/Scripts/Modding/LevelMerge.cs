#if ENABLE_MOD_PACKAGES
using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using CATHODE.Scripting.Refactor;
using OpenCAGE.Modding.Merging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static CathodeLib.CompositeFlowgraphTable;

namespace OpenCAGE.Modding
{
    /// <summary>
    /// Several mods' versions of one level, combined into the game's level folder: CathodeLib's
    /// <see cref="LevelMerger"/> carries each mod's script and asset changes into a fresh copy of the level, which is
    /// then saved the way its authors saved it - plainly when no version was built (scripts, assets and the rest
    /// written as they are), or built the way Save &amp; Build builds it when one was (movers, collision, navigation and
    /// lighting regenerated from the combined scripts). The editor's own records kept beside the scripts - flowgraph
    /// pages, which parameters were edited, composite previews - are combined too, and so are the level's text files.
    /// </summary>
    public class ModLevelMerger : ILevelMerger
    {
        /// <summary>Folder the per-mod copies of the level are built in while combining. Deleted afterwards.</summary>
        public string WorkRoot;

        /// <summary>The pages OpenCAGE ships for a composite of a level (set by the editor; null outside it).</summary>
        public Func<Composite, string, List<FlowgraphMeta>> VanillaPages;

        /// <summary>
        /// Files only a build (Save &amp; Build) writes: the level's lighting, alpha lighting, and each state's navigation,
        /// cover and positions. A plain save leaves them as they are, so a version that changes none of them was never built.
        /// </summary>
        public static bool IsBuildOutput(string normalisedPath)
        {
            string file = normalisedPath.Substring(normalisedPath.LastIndexOf('/') + 1);
            return file.StartsWith("RADIOSITY_") || file == "ALPHALIGHT_LEVEL.BIN" || normalisedPath.Contains("/WORLD/STATE_");
        }

        public LevelMergeResult Merge(string gameRoot, string level, IList<LevelVersion> versions, Action<string> progress)
        {
            LevelMergeResult result = new LevelMergeResult();
            string work = WorkRoot ?? Path.Combine(ModToolkit.ModsDir(gameRoot), "WORK");
            work = Path.Combine(work, Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(work);
            try
            {
                string levelDir = LevelDir(gameRoot, level);
                Global global = new Global(Path.Combine(gameRoot, "DATA", "ENV", "GLOBAL") + Path.DirectorySeparatorChar);

                //The game's folder holds the level unmodified: one copy to diff against, one to build the result in - read
                //together, and the first mod's copy alongside them
                progress("Reading the original " + level + "...");
                Func<int, Level> readVersion = i =>
                {
                    string copyRoot = Path.Combine(work, "m" + i);
                    MaterialiseLevel(gameRoot, copyRoot, level, versions[i]);
                    return new Level(LevelDir(copyRoot, level), global);
                };
                Task<Level> vanillaRead = Task.Run(() => new Level(levelDir, global));
                Task<Level> destRead = Task.Run(() => new Level(levelDir, global));
                Task<Level> next = Task.Run(() => readVersion(0));
                Level vanilla = vanillaRead.GetAwaiter().GetResult();
                Level dest = destRead.GetAwaiter().GetResult();
                LevelMerger merger = new LevelMerger(vanilla, dest);
                List<(string Mod, string CommandsPath)> editorTables = new List<(string, string)>();

                for (int i = 0; i < versions.Count; i++)
                {
                    progress("Reading '" + versions[i].ModName + "''s " + level + " (" + (i + 1) + " of " + versions.Count + ")...");
                    Level modLevel = next.GetAwaiter().GetResult();
                    //The next copy is read while this one is combined - when there's the memory for two at once
                    int following = i + 1;
                    next = following < versions.Count ? (PlentyOfMemory() ? Task.Run(() => readVersion(following)) : null) : null;
                    progress("Combining '" + versions[i].ModName + "''s changes to " + level + "...");
                    merger.Apply(modLevel, versions[i].ModName);
                    editorTables.Add((versions[i].ModName, modLevel.Commands.Filepath));
                    modLevel = null;
                    if (following < versions.Count && next == null)
                    {
                        GC.Collect();
                        next = Task.FromResult(readVersion(following));
                    }
                }
                merger.Finish();

                //The editor's records, read before the save truncates COMMANDS.PAK
                EditorTables merged = EditorTables.Read(dest.Commands.Filepath);
                foreach ((string mod, string commandsPath) in editorTables)
                    merged.Absorb(EditorTables.Read(commandsPath), mod, merger.ChangedComposites, VanillaPages == null ? null : (Func<ShortGuid, List<FlowgraphMeta>>)(id => VanillaPages(vanilla.Commands.GetComposite(id), level)));
                merged.Finalise(dest.Commands, merger.ChangedComposites, result);

                //Built only when a version was: script and asset changes need no new lighting or navigation, and building
                //anyway would regenerate them for nothing
                if (versions.Any(v => v.Files.Keys.Any(IsBuildOutput)))
                {
                    progress("Building " + level + " (navigation, collision, lighting)...");
                    Instancing pass = dest.SaveInstanced();
                    if (pass?.BakeWarnings != null)
                        foreach (string warning in pass.BakeWarnings)
                            result.Warnings.Add(level + ": " + warning);
                }
                else
                {
                    progress("Saving " + level + "...");
                    dest.Save();
                }
                merged.Write(dest.Commands.Filepath);

                //Files of the level folder the level itself doesn't write (its text) - merged file by file
                MergeLooseFiles(gameRoot, level, versions, result);

                foreach (LevelMerger.Conflict conflict in merger.Conflicts)
                    result.Conflicts.Add(new MergeConflict() { Target = level, Where = conflict.Where, Kind = ConflictKind.Overridden, Kept = conflict.Kept, Lost = conflict.Lost, Detail = conflict.Detail });
                foreach (string note in merger.Notes)
                    result.Warnings.Add(level + ": " + note);
                return result;
            }
            finally
            {
                try { Directory.Delete(work, true); } catch { }
            }
        }

        private static string LevelDir(string root, string level)
        {
            return Path.Combine(root, "DATA", "ENV", "PRODUCTION", level.Replace('/', Path.DirectorySeparatorChar));
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateHardLink(string newFile, string existingFile, IntPtr security);

        [StructLayout(LayoutKind.Sequential)]
        private class MemoryStatus
        {
            public uint Length = (uint)Marshal.SizeOf(typeof(MemoryStatus));
            public uint MemoryLoad;
            public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatus status);

        /* Room for another level in memory alongside the ones being combined (a big level takes a few GB) */
        private static bool PlentyOfMemory()
        {
            try
            {
                MemoryStatus status = new MemoryStatus();
                return GlobalMemoryStatusEx(status) && status.AvailablePhysical > 6UL * 1024 * 1024 * 1024;
            }
            catch { return false; }
        }

        /* One mod's copy of the level: the unmodified level with that mod's files over it. Files the mod doesn't change are
           linked rather than copied (the same drive, read only), and every file it does change is unlinked first, so
           writing it can never reach the game's own copy. */
        private static void MaterialiseLevel(string gameRoot, string copyRoot, string level, LevelVersion version)
        {
            foreach (string folder in new[] { level, level + "_PATCH" })
            {
                string source = LevelDir(gameRoot, folder);
                if (!Directory.Exists(source)) continue;
                string target = LevelDir(copyRoot, folder);
                foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                {
                    string copy = target + file.Substring(source.Length);
                    Directory.CreateDirectory(Path.GetDirectoryName(copy));
                    if (!CreateHardLink(copy, file, IntPtr.Zero))
                        File.Copy(file, copy, true);
                }
            }
            foreach (KeyValuePair<string, byte[]> file in version.Files)
            {
                string path = ModToolkit.Denormalise(copyRoot, file.Key);
                if (File.Exists(path))
                    File.Delete(path);
                if (file.Value == null)
                    continue;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, file.Value);
            }
        }
        /* The level's text, and anything else in its folder that saving the level doesn't write */
        private static void MergeLooseFiles(string gameRoot, string level, IList<LevelVersion> versions, LevelMergeResult result)
        {
            HashSet<string> paths = new HashSet<string>(versions.SelectMany(o => o.Files.Keys));
            foreach (string path in paths)
            {
                bool loose = path.Contains("/TEXT/") || !File.Exists(ModToolkit.Denormalise(gameRoot, path));
                if (!loose) continue;
                string full = ModToolkit.Denormalise(gameRoot, path);
                List<Contribution> changed = versions.Where(o => o.Files.ContainsKey(path) && o.Files[path] != null)
                    .Select(o => new Contribution() { ModId = o.ModId, ModName = o.ModName, Bytes = o.Files[path] }).ToList();
                if (changed.Count == 0)
                {
                    if (File.Exists(full)) File.Delete(full);
                    continue;
                }
                byte[] original = File.Exists(full) ? File.ReadAllBytes(full) : null;
                MergeOutcome outcome = FileMerger.Merge(path, original, changed);
                result.Conflicts.AddRange(outcome.Conflicts);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllBytes(full, outcome.Bytes);
            }
        }

        /// <summary>
        /// The editor's records kept inside COMMANDS.PAK beside the scripts. Combined composite by composite: a
        /// composite one mod changed keeps that mod's pages and preview; one several mods changed gets pages
        /// combined from theirs when they still show exactly its links, else none (OpenCAGE then shows its links
        /// in the list view, and Arrange Page can lay them out).
        /// </summary>
        private class EditorTables
        {
            public CompositeFlowgraphTable Layouts;
            public CompositeFlowgraphCompatibilityTable Compatibility;
            public CompositeParameterModificationTable Modifications;
            public EntityAppliedDefaultsTable Defaults;
            public CompositePreviewTable Previews;
            public CompositePageHistoryTable PageHistory;

            private readonly Dictionary<ShortGuid, List<FlowgraphMeta>> _pagesSoFar = new Dictionary<ShortGuid, List<FlowgraphMeta>>();

            public static EditorTables Read(string commandsPath)
            {
                return new EditorTables()
                {
                    Layouts = CustomTable.ReadTable(commandsPath, CustomTableType.COMPOSITE_FLOWGRAPHS) as CompositeFlowgraphTable,
                    Compatibility = CustomTable.ReadTable(commandsPath, CustomTableType.COMPOSITE_FLOWGRAPH_COMPATIBILITY_INFO) as CompositeFlowgraphCompatibilityTable,
                    Modifications = CustomTable.ReadTable(commandsPath, CustomTableType.COMPOSITE_PARAMETER_MODIFICATION) as CompositeParameterModificationTable,
                    Defaults = CustomTable.ReadTable(commandsPath, CustomTableType.ENTITY_APPLIED_DEFAULTS) as EntityAppliedDefaultsTable,
                    Previews = CustomTable.ReadTable(commandsPath, CustomTableType.COMPOSITE_PREVIEWS) as CompositePreviewTable,
                    PageHistory = CustomTable.ReadTable(commandsPath, CustomTableType.COMPOSITE_PAGE_HISTORY) as CompositePageHistoryTable,
                };
            }

            public void Absorb(EditorTables mod, string modName, Dictionary<ShortGuid, List<string>> changed, Func<ShortGuid, List<FlowgraphMeta>> vanillaPages)
            {
                foreach (KeyValuePair<ShortGuid, List<string>> composite in changed)
                {
                    if (!composite.Value.Contains(modName)) continue;
                    ShortGuid id = composite.Key;

                    //Which parameters were edited: everything any mod edited
                    if (mod.Modifications != null && mod.Modifications.modified_params.TryGetValue(id, out var edits))
                    {
                        if (Modifications == null) Modifications = new CompositeParameterModificationTable();
                        if (!Modifications.modified_params.TryGetValue(id, out var mine))
                            Modifications.modified_params[id] = mine = new Dictionary<ShortGuid, HashSet<ShortGuid>>();
                        foreach (var entity in edits)
                        {
                            if (!mine.TryGetValue(entity.Key, out HashSet<ShortGuid> parameters))
                                mine[entity.Key] = parameters = new HashSet<ShortGuid>();
                            parameters.UnionWith(entity.Value);
                        }
                    }

                    //Its preview: the last mod's
                    CompositePreviewTable.Preview preview = null;
                    if (mod.Previews != null && mod.Previews.previews.TryGetValue(id, out preview))
                    {
                        if (Previews == null) Previews = new CompositePreviewTable();
                        Previews.SetPreview(id, preview);
                    }

                    //Its pages: this mod's own, or the shipped ones when it kept those
                    List<FlowgraphMeta> modPages = mod.Layouts?.flowgraphs.Where(o => o.CompositeGUID == id).ToList() ?? new List<FlowgraphMeta>();
                    if (modPages.Count == 0 && vanillaPages != null)
                        modPages = vanillaPages(id) ?? new List<FlowgraphMeta>();
                    if (!_pagesSoFar.TryGetValue(id, out List<FlowgraphMeta> earlier))
                        _pagesSoFar[id] = modPages.Select(o => RefactorPages.Copy(o, id)).ToList();
                    else
                        _pagesSoFar[id] = CombinePages(earlier, modPages);
                }
                //Defaults applied to new entities: everything any mod applied
                if (mod.Defaults != null)
                {
                    if (Defaults == null) Defaults = new EntityAppliedDefaultsTable();
                    foreach (var entity in mod.Defaults.applied_defaults)
                    {
                        if (!Defaults.applied_defaults.TryGetValue(entity.Key, out HashSet<ShortGuid> parameters))
                            Defaults.applied_defaults[entity.Key] = parameters = new HashSet<ShortGuid>();
                        parameters.UnionWith(entity.Value);
                    }
                }
            }

            /// <summary>
            /// Each changed composite's combined pages, made to draw exactly its combined links: what the pages
            /// already show stays where it is, and what they don't is drawn beside it or laid out on a page of its own.
            /// </summary>
            public void Finalise(Commands merged, Dictionary<ShortGuid, List<string>> changed, LevelMergeResult result)
            {
                if (Layouts == null) Layouts = new CompositeFlowgraphTable();
                foreach (KeyValuePair<ShortGuid, List<FlowgraphMeta>> composite in _pagesSoFar)
                {
                    Composite combined = merged.GetComposite(composite.Key);
                    Layouts.flowgraphs.RemoveAll(o => o.CompositeGUID == composite.Key);
                    Compatibility?.compatibility_info.RemoveAll(o => o.composite_id == composite.Key);
                    if (combined == null || composite.Value.Count == 0)
                        continue;
                    List<FlowgraphMeta> pages = composite.Value;
                    if (!RefactorPages.PagesMatchLinks(combined, pages))
                        pages = RefactorPages.DrawLinks(combined, pages, "Combined by Mod Manager", merged);
                    if (pages.Count != 0 && RefactorPages.PagesMatchLinks(combined, pages))
                        Layouts.flowgraphs.AddRange(pages);
                    else if (changed.TryGetValue(composite.Key, out List<string> mods) && mods.Count > 1)
                        result.Warnings.Add("The flowgraph pages of " + combined.name + " couldn't be combined, so it opens as a list (Arrange Page lays it out again)");
                }
            }
            /* The later mod's pages, plus whatever the earlier mods' pages showed that the later's don't: an earlier
               mod's new entities and links land on their own page of the same name (or a page of their own), with
               node numbers moved clear of the later mod's */
            private static List<FlowgraphMeta> CombinePages(List<FlowgraphMeta> earlier, List<FlowgraphMeta> later)
            {
                List<FlowgraphMeta> result = later.Select(o => RefactorPages.Copy(o, o.CompositeGUID)).ToList();
                HashSet<ShortGuid> shown = new HashSet<ShortGuid>(result.SelectMany(p => p.Nodes).Select(n => n.EntityGUID));
                HashSet<(ShortGuid, ShortGuid, ShortGuid, ShortGuid)> drawn = new HashSet<(ShortGuid, ShortGuid, ShortGuid, ShortGuid)>(
                    result.SelectMany(p => p.Nodes.SelectMany(n => n.ConnectionsOut.Select(c => (n.EntityGUID, c.ParameterGUID, c.ConnectedEntityGUID, c.ConnectedParameterGUID)))));
                foreach (FlowgraphMeta page in earlier)
                {
                    FlowgraphMeta target = result.FirstOrDefault(o => o.Name == page.Name);
                    if (target == null)
                    {
                        target = RefactorPages.Copy(page, page.CompositeGUID);
                        target.Nodes.Clear();
                        result.Add(target);
                    }
                    int nextId = target.Nodes.Count == 0 ? 0 : target.Nodes.Max(o => o.NodeID) + 1;
                    Dictionary<int, int> renumbered = new Dictionary<int, int>();
                    foreach (FlowgraphMeta.NodeMeta node in page.Nodes)
                    {
                        bool newEntity = !shown.Contains(node.EntityGUID);
                        bool newLinks = node.ConnectionsOut.Any(c => !drawn.Contains((node.EntityGUID, c.ParameterGUID, c.ConnectedEntityGUID, c.ConnectedParameterGUID)));
                        if (!newEntity && !newLinks) continue;
                        FlowgraphMeta.NodeMeta copy = RefactorPages.Copy(new FlowgraphMeta() { Nodes = new List<FlowgraphMeta.NodeMeta>() { node } }, page.CompositeGUID).Nodes[0];
                        renumbered[node.NodeID] = nextId;
                        copy.NodeID = nextId++;
                        copy.ConnectionsOut = copy.ConnectionsOut.Where(c => !drawn.Contains((node.EntityGUID, c.ParameterGUID, c.ConnectedEntityGUID, c.ConnectedParameterGUID))).ToList();
                        target.Nodes.Add(copy);
                    }
                    //Connections between copied nodes follow the renumbering; ones to nodes left behind find the node of that entity on this page
                    foreach (FlowgraphMeta.NodeMeta node in target.Nodes)
                        foreach (FlowgraphMeta.NodeMeta.ConnectionMeta connection in node.ConnectionsOut)
                        {
                            if (renumbered.TryGetValue(connection.ConnectedNodeID, out int moved) && target.Nodes.Any(o => o.NodeID == moved && o.EntityGUID == connection.ConnectedEntityGUID))
                                connection.ConnectedNodeID = moved;
                            else if (!target.Nodes.Any(o => o.NodeID == connection.ConnectedNodeID && o.EntityGUID == connection.ConnectedEntityGUID))
                            {
                                FlowgraphMeta.NodeMeta other = target.Nodes.FirstOrDefault(o => o.EntityGUID == connection.ConnectedEntityGUID);
                                if (other != null) connection.ConnectedNodeID = other.NodeID;
                            }
                        }
                    foreach (FlowgraphMeta.NodeMeta node in target.Nodes)
                    {
                        shown.Add(node.EntityGUID);
                        foreach (FlowgraphMeta.NodeMeta.ConnectionMeta c in node.ConnectionsOut)
                            drawn.Add((node.EntityGUID, c.ParameterGUID, c.ConnectedEntityGUID, c.ConnectedParameterGUID));
                    }
                }
                return result.Where(o => o.Nodes.Count != 0).ToList();
            }

            /* What the editor checks before it offers pages: drop what the composite no longer has, then the links the
               pages draw must be exactly the composite's */
            private static void TrimToComposite(List<FlowgraphMeta> pages, Composite composite)
            {
                foreach (FlowgraphMeta page in pages)
                {
                    page.Nodes = page.Nodes.Where(o => composite.GetEntityByID(o.EntityGUID) != null).ToList();
                    foreach (FlowgraphMeta.NodeMeta node in page.Nodes)
                    {
                        Entity entity = composite.GetEntityByID(node.EntityGUID);
                        node.ConnectionsOut = node.ConnectionsOut.Where(c =>
                        {
                            FlowgraphMeta.NodeMeta target = page.Nodes.FirstOrDefault(o => o.NodeID == c.ConnectedNodeID && o.EntityGUID == c.ConnectedEntityGUID);
                            return target != null && entity.childLinks.Any(o => o.thisParamID == c.ParameterGUID && o.linkedParamID == c.ConnectedParameterGUID && o.linkedEntityID == target.EntityGUID);
                        }).ToList();
                    }
                }
            }

            public void Write(string commandsPath)
            {
                if (Layouts != null) CustomTable.WriteTable(commandsPath, CustomTableType.COMPOSITE_FLOWGRAPHS, Layouts);
                if (Compatibility != null) CustomTable.WriteTable(commandsPath, CustomTableType.COMPOSITE_FLOWGRAPH_COMPATIBILITY_INFO, Compatibility);
                if (Modifications != null) CustomTable.WriteTable(commandsPath, CustomTableType.COMPOSITE_PARAMETER_MODIFICATION, Modifications);
                if (Defaults != null) CustomTable.WriteTable(commandsPath, CustomTableType.ENTITY_APPLIED_DEFAULTS, Defaults);
                if (Previews != null) CustomTable.WriteTable(commandsPath, CustomTableType.COMPOSITE_PREVIEWS, Previews);
                if (PageHistory != null) CustomTable.WriteTable(commandsPath, CustomTableType.COMPOSITE_PAGE_HISTORY, PageHistory);
            }
        }
    }
}
#endif
