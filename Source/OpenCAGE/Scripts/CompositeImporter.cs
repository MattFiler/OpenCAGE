using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
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
            public Dictionary<ShortGuid, string> Composites = new Dictionary<ShortGuid, string>();
        }

        public List<LevelPick> Levels = new List<LevelPick>();

        /// <summary>Follow composite instances and port the composites they refer to as well.</summary>
        public bool IncludeChildren = true;
        /// <summary>Replace composites the destination already holds under the same ID.</summary>
        public bool OverwriteComposites = false;
        /// <summary>Models, textures and materials replace same-named destination entries.</summary>
        public bool OverwriteAssets = false;

        public int CompositeCount => Levels.Sum(o => o.Composites.Count);
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
            return CompositeCount + " composite" + (CompositeCount == 1 ? "" : "s") + " from " + Levels.Count + " level" + (Levels.Count == 1 ? "" : "s");
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
                composites = level.Commands.Entries.Select(o => new CompositeIndexEntry() { ID = o.shortGUID, Name = o.name }).ToList();
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
        }

        public static Result Import(CompositeSelection selection, Level destination, Action<Composite, List<FlowgraphMeta>> onLayouts)
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
                    progress.BringToFront();

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
                        onLayouts?.Invoke(copy, FlowgraphLayoutManager.GetLayoutsForPort(original, sourceLayouts));
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
            return result;
        }

        public static Level LoadLevel(string levelName)
        {
            Level level = new Level(Singleton.PathToAI + "/DATA/ENV/" + levelName, Singleton.Global, false);
            using (ProgressUI progress = new ProgressUI())
            {
                progress.ShowLevelLoading(level);
                progress.BringToFront();
                level.Load();
                progress.Close();
            }
            return level;
        }
    }
}
