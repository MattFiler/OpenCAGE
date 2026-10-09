using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static CathodeLib.CompositeFlowgraphTable;

namespace OpenCAGE
{
    /// <summary>
    /// What a user picked in the composite import dialog: composites per source level, plus how to port them.
    /// </summary>
    public class CompositeSelection
    {
        public class LevelPick
        {
            public string Level;
            /// <summary>What the user ticked - the roots of the port.</summary>
            public Dictionary<ShortGuid, string> Composites = new Dictionary<ShortGuid, string>();
            /// <summary>What those instance, all the way down, as the picker worked it out - they come along regardless.</summary>
            public Dictionary<ShortGuid, string> Implied = new Dictionary<ShortGuid, string>();

            public int TotalCount => Composites.Count + Implied.Count;

            public string Summary()
            {
                string text = TotalCount + " composite" + (TotalCount == 1 ? "" : "s");
                if (Implied.Count != 0)
                    text += " (" + Composites.Count + " ticked, " + Implied.Count + " instanced by those)";
                return text;
            }
        }

        public List<LevelPick> Levels = new List<LevelPick>();

        /// <summary>Follow composite instances and port the composites they refer to as well.</summary>
        public bool IncludeChildren = true;
        /// <summary>Replace composites the destination already holds under the same ID.</summary>
        public bool OverwriteComposites = false;
        /// <summary>Models, textures and materials replace same-named destination entries.</summary>
        public bool OverwriteAssets = false;

        /// <summary>The composites the user ticked, across every level.</summary>
        public int CompositeCount => Levels.Sum(o => o.Composites.Count);
        /// <summary>Ticked plus everything they instance.</summary>
        public int TotalCount => Levels.Sum(o => o.TotalCount);
        public bool IsEmpty => CompositeCount == 0;

        public LevelPick GetOrAdd(string level)
        {
            LevelPick pick = Levels.FirstOrDefault(o => string.Equals(o.Level, level, StringComparison.OrdinalIgnoreCase));
            if (pick == null)
            {
                pick = new LevelPick() { Level = level };
                Levels.Add(pick);
            }
            return pick;
        }

        public void Prune()
        {
            Levels.RemoveAll(o => o.Composites.Count == 0);
        }

        public string Summary()
        {
            Prune();
            if (IsEmpty) return "Nothing selected";
            int implied = TotalCount - CompositeCount;
            string text = TotalCount + " composite" + (TotalCount == 1 ? "" : "s") + " from " + Levels.Count + " level" + (Levels.Count == 1 ? "" : "s");
            if (implied != 0)
                text += " (" + CompositeCount + " ticked, " + implied + " instanced by those)";
            return text;
        }
    }

    /// <summary>Which composites a composite instances directly, for a loaded script.</summary>
    public static class CompositeNesting
    {
        public static Func<ShortGuid, IEnumerable<ShortGuid>> InstancesOf(Commands commands)
        {
            return id =>
            {
                Composite composite = commands?.GetComposite(id);
                if (composite == null)
                    return Enumerable.Empty<ShortGuid>();

                return composite.functions
                    .Where(o => o != null && !o.function.IsFunctionType)
                    .Select(o => o.function)
                    .Distinct();
            };
        }
    }

    /// <summary>
    /// The composites each level holds, read from the COMMANDS table alone and remembered for the
    /// session, so a picker can flick between levels without loading any of them.
    /// </summary>
    public static class CompositeIndexCache
    {
        private class Entry
        {
            public DateTime Written;
            public List<CompositeIndexEntry> Composites;
        }
        private static readonly Dictionary<string, Entry> _cache = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        public static List<CompositeIndexEntry> Get(string levelName)
        {
            Level level = new Level(Singleton.PathToAI + "/DATA/ENV/" + levelName, Singleton.Global, false);
            string commands = level.CommandsFilepath;
            DateTime written = File.Exists(commands) ? File.GetLastWriteTimeUtc(commands) : DateTime.MinValue;

            if (_cache.TryGetValue(levelName, out Entry cached) && cached.Written == written)
                return cached.Composites;

            List<CompositeIndexEntry> composites = Commands.ReadCompositeIndex(commands);
            if (composites == null)
            {
                //No cheap table for this build's script format: the only way to list it is to load it
                level.Load();
                composites = level.Commands.Entries.Select(o => new CompositeIndexEntry()
                {
                    ID = o.shortGUID,
                    Name = o.name,
                    Instances = o.functions.Where(f => f != null && !f.function.IsFunctionType).Select(f => f.function).Distinct().ToList(),
                    IsRoot = level.Commands.EntryPoints != null && level.Commands.EntryPoints.Length > 0 && level.Commands.EntryPoints[0] == o,
                }).ToList();
            }
            composites = composites.OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase).ToList();

            _cache[levelName] = new Entry() { Written = written, Composites = composites };
            return composites;
        }
    }

    /// <summary>
    /// Carries a <see cref="CompositeSelection"/> into a destination level: loads each source level in
    /// turn, ports the chosen composites (and, when asked, everything they instance) with the level
    /// data they use, and hands each ported composite's flowgraph pages to the caller, who knows where
    /// the destination keeps them.
    /// </summary>
    public static class CompositeImporter
    {
        public class Result
        {
            public List<Composite> Ported = new List<Composite>();
            public int Renderables, CollisionMappings, PhysicsSystems, AnimatedModels;
            /// <summary>The proxies among the ported composites that resolve to nothing in the destination.</summary>
            public DeadProxyReport DeadProxies = new DeadProxyReport();
            /// <summary>Composites <c>extraComposites</c> added to a level's pick (display models, say), by name.</summary>
            public List<string> Extra = new List<string>();
        }

        /// <param name="extraComposites">Given each source level as it is loaded, with its pick: more of its composites to port with the pick (null: none).</param>
        /// <param name="onPorted">Each composite as it is ported: the source level, the original and the copy.</param>
        public static Result Import(CompositeSelection selection, Level destination, Action<Composite, List<FlowgraphMeta>> onLayouts,
            Func<Level, CompositeSelection.LevelPick, IEnumerable<Composite>> extraComposites = null, Action<Level, Composite, Composite> onPorted = null)
        {
            Result result = new Result();
            if (selection == null) return result;
            selection.Prune();

            foreach (CompositeSelection.LevelPick pick in selection.Levels)
            {
                Level source = LoadLevel(pick.Level);
                CompositeFlowgraphTable sourceLayouts = (CompositeFlowgraphTable)CustomTable.ReadTable(source.Commands.Filepath, CustomTableType.COMPOSITE_FLOWGRAPHS);

                using (ProgressUI progress = new ProgressUI())
                {
                    progress.ShowTransferring("Importing from " + pick.Level + "...");
                    progress.KeepOnTop();

                    CompositePorter porter = new CompositePorter(source, destination)
                    {
                        OverwriteComposites = selection.OverwriteComposites,
                        OverwriteAssets = selection.OverwriteAssets,
                        Recurse = selection.IncludeChildren,
                    };
                    porter.OnProgress = progress.DoRefresh;
                    porter.OnCompositePorted = (original, copy) =>
                    {
                        result.Ported.Add(copy);
                        onLayouts?.Invoke(copy, FlowgraphLayoutManager.GetLayoutsForPort(original, sourceLayouts, FlowgraphLayoutManager.BundledLevelName(source.Commands, pick.Level), source.Commands));
                        onPorted?.Invoke(source, original, copy);
                    };

                    foreach (ShortGuid id in pick.Composites.Keys)
                    {
                        Composite composite = source.Commands.GetComposite(id);
                        if (composite == null)
                        {
                            Debug.Log("Import", "Composite " + id + " (" + pick.Composites[id] + ") is no longer in " + pick.Level);
                            continue;
                        }
                        porter.Port(composite);
                    }
                    if (extraComposites != null)
                    {
                        foreach (Composite extra in extraComposites(source, pick) ?? Enumerable.Empty<Composite>())
                        {
                            if (extra == null || pick.Composites.ContainsKey(extra.shortGUID)) continue;
                            porter.Port(extra);
                            result.Extra.Add(extra.name);
                        }
                    }

                    result.Renderables += porter.RenderablesPorted;
                    result.CollisionMappings += porter.CollisionMappingsPorted;
                    result.PhysicsSystems += porter.PhysicsSystemsPorted;
                    result.AnimatedModels += porter.AnimatedModelsPorted;

                    progress.Close();
                }

                source = null;
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
            }
            result.DeadProxies = DeadProxyReport.Of(destination.Commands, result.Ported);
            return result;
        }

        /// <summary>
        /// Open a composite the import just made, in the editor now and in the viewer once the viewer
        /// can show it. Opening it sends the viewer a selection straight away, but the composite's
        /// contents only go behind the next resource sync (<c>Send.CompositeAdded</c>: the renderables
        /// name model indexes the viewer holds only once the snapshot carrying them has landed) - so
        /// the viewer would populate it empty, and a later selection of the same composite is one it
        /// skips. The selection is kept from the viewer here, and a rebuild of the composite is queued
        /// behind the same sync as its contents, so the viewer builds it once, with everything in it.
        /// </summary>
        public static void OpenPortedComposite(Composite composite)
        {
            if (composite == null || Singleton.Editor?.CompositeBrowser == null)
                return;

            ViewerSelectionSync.SuppressSyncBroadcastDepth++;
            try
            {
                Singleton.Editor.CompositeBrowser.SelectCompositeAndReloadList(composite);
            }
            finally
            {
                ViewerSelectionSync.SuppressSyncBroadcastDepth--;
            }
            ViewerResourceSync.AfterNextSync(() => Send.RefreshCompositeInViewer(composite));
            //The import's changes are all in: the contents and the rebuild need not wait for the coalesce timer
            ViewerResourceSync.SyncImmediately();
        }

        /// <summary>
        /// Put the editor back where it was before an import closed it, for when nothing the import
        /// brought is opened in its place. An import that can replace composites closes the display
        /// first (a tab may hold a composite it is about to swap out), so without this the editor is
        /// left showing nothing - and opening the composite it had on its own lost the hierarchy the
        /// user had stepped down through, in the editor and in the viewer.
        /// </summary>
        /// <remarks>
        /// The place is walked back down by id (<see cref="CompositePath.Resolve"/>): a composite the
        /// import replaced, on the path or at the end of it, comes back as its replacement, and a step
        /// the import took away ends the walk at the deepest composite still there. The selection comes
        /// back when the walk got all the way. It goes to the viewer as an imported composite does: kept
        /// from it until the resource sync carrying the import's contents, then one rebuild of the
        /// composite the path starts from - the scene - with the place to land in the same packet.
        /// Call it inside the import's scene batch, so that rebuild is the batch's only one.
        /// </remarks>
        public static void ReopenClosedPlace(CompositePath.Place place)
        {
            DockPanels.CompositeBrowser browser = Singleton.Editor?.CompositeBrowser;
            if (place == null || browser?.Content?.Level?.Commands == null)
                return;
            CompositePath.Snapshot path = CompositePath.Resolve(browser.Content.Level.Commands, place, out Composite landed);
            if (landed == null)
                return;

            ViewerSelectionSync.SuppressSyncBroadcastDepth++;
            try
            {
                browser.SelectCompositeAndReloadList(landed, path);
                Singleton.Editor.CompositeDisplay?.RestoreSelection(place.SelectionIn(landed));
            }
            finally
            {
                ViewerSelectionSync.SuppressSyncBroadcastDepth--;
            }
            Composite scene = path.EntryComposite ?? landed;
            ViewerResourceSync.AfterNextSync(() => Send.RefreshCompositeInViewer(scene));
            //The import's changes are all in: the contents and the rebuild need not wait for the coalesce timer
            ViewerResourceSync.SyncImmediately();
        }

        public static Level LoadLevel(string levelName)
        {
            Level level = new Level(Singleton.PathToAI + "/DATA/ENV/" + levelName, Singleton.Global, false);
            using (ProgressUI progress = new ProgressUI())
            {
                progress.ShowLevelLoading(level);
                progress.KeepOnTop();
                level.Load();
                progress.Close();
            }
            return level;
        }
    }
}
