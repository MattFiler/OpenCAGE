using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using System;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>
    /// The editor's own tables inside a level's COMMANDS.PAK - flowgraph pages, page history, layout
    /// compatibility, the inspector's modified-parameter and applied-defaults records, and the composite
    /// previews - for a level that is NOT the one open in the editor. The static managers
    /// (FlowgraphLayoutManager, ParameterModificationTracker, CompositePreviewManager) only ever look
    /// after the loaded level's copies; a level being written on disk (a port into it, or an import from
    /// a package) has to carry its own across a save.
    /// </summary>
    /// <remarks>
    /// Commands.Save truncates the file and writes the script alone; CathodeLib's own tables come back
    /// through its OnSaveSuccess handlers, but these six do not - so read them before the save and
    /// write them back after it. Writing any one table through CustomTable keeps the others present in
    /// the file, so the order below does not matter beyond happening after the save.
    /// </remarks>
    public class LevelEditorTables
    {
        public CompositeFlowgraphTable Layouts;
        public CompositePageHistoryTable PageHistory;
        public CompositeFlowgraphCompatibilityTable Compatibility;
        public CompositeParameterModificationTable Modifications;
        public EntityAppliedDefaultsTable Defaults;
        public CompositePreviewTable Previews;

        /// <summary>
        /// Read the tables from <paramref name="commandsPath"/>. A missing or empty modification table
        /// is generated for <paramref name="commands"/> the way the tracker does on first load: a partial
        /// one written over nothing would leave every other composite looking untouched.
        /// </summary>
        public static LevelEditorTables Read(string commandsPath, Commands commands)
        {
            LevelEditorTables tables = new LevelEditorTables()
            {
                Layouts = CustomTable.ReadTable(commandsPath, CustomTableType.COMPOSITE_FLOWGRAPHS) as CompositeFlowgraphTable,
                PageHistory = CustomTable.ReadTable(commandsPath, CustomTableType.COMPOSITE_PAGE_HISTORY) as CompositePageHistoryTable,
                Compatibility = CustomTable.ReadTable(commandsPath, CustomTableType.COMPOSITE_FLOWGRAPH_COMPATIBILITY_INFO) as CompositeFlowgraphCompatibilityTable,
                Modifications = CustomTable.ReadTable(commandsPath, CustomTableType.COMPOSITE_PARAMETER_MODIFICATION) as CompositeParameterModificationTable,
                Defaults = CustomTable.ReadTable(commandsPath, CustomTableType.ENTITY_APPLIED_DEFAULTS) as EntityAppliedDefaultsTable,
                Previews = CustomTable.ReadTable(commandsPath, CustomTableType.COMPOSITE_PREVIEWS) as CompositePreviewTable,
            };
            if (tables.Layouts == null) tables.Layouts = new CompositeFlowgraphTable();
            if (tables.Modifications == null || tables.Modifications.modified_params.Count == 0)
                tables.Modifications = ParameterModificationTracker.GenerateModificationTable(commands);
            if (tables.Defaults == null) tables.Defaults = new EntityAppliedDefaultsTable();
            return tables;
        }

        /// <summary>Write every table back. Tables that were never in the file (null) are left out.</summary>
        public void Write(string commandsPath)
        {
            if (Layouts != null) CustomTable.WriteTable(commandsPath, CustomTableType.COMPOSITE_FLOWGRAPHS, Layouts);
            if (PageHistory != null) CustomTable.WriteTable(commandsPath, CustomTableType.COMPOSITE_PAGE_HISTORY, PageHistory);
            if (Compatibility != null) CustomTable.WriteTable(commandsPath, CustomTableType.COMPOSITE_FLOWGRAPH_COMPATIBILITY_INFO, Compatibility);
            if (Modifications != null) CustomTable.WriteTable(commandsPath, CustomTableType.COMPOSITE_PARAMETER_MODIFICATION, Modifications);
            if (Defaults != null) CustomTable.WriteTable(commandsPath, CustomTableType.ENTITY_APPLIED_DEFAULTS, Defaults);
            if (Previews != null) CustomTable.WriteTable(commandsPath, CustomTableType.COMPOSITE_PREVIEWS, Previews);
        }

        /// <summary>
        /// A ported composite's preview: the source level's own preview of it replaces whatever the
        /// destination held for that ID; none (null) removes the destination's, so it falls back to the
        /// shipped preview the way a composite that was never edited does.
        /// </summary>
        public void ReplacePreview(ShortGuid composite, CompositePreviewTable.Preview preview)
        {
            if (preview == null)
            {
                Previews?.RemovePreview(composite);
                return;
            }
            if (Previews == null)
                Previews = new CompositePreviewTable();
            Previews.SetPreview(composite, preview);
        }

        /// <summary>
        /// A ported composite's pages: the source's replace whatever the destination held for that ID.
        /// Its compatibility row follows, the way FlowgraphLayoutManager.ImportLayouts sets it for the
        /// loaded level: pages that came across are known good; none means the first open decides.
        /// </summary>
        public void ReplaceLayouts(ShortGuid composite, System.Collections.Generic.List<CompositeFlowgraphTable.FlowgraphMeta> layouts)
        {
            Layouts.flowgraphs.RemoveAll(o => o.CompositeGUID == composite);
            bool hasPages = layouts != null && layouts.Count > 0;
            if (hasPages)
                Layouts.flowgraphs.AddRange(layouts);

            /* No verdict either way: the destination judges the pages when the composite is first opened
               there, after its own purge of the links (see FlowgraphLayoutManager.ImportLayouts). A row
               left over from a copy being overwritten would stand in for that, so it goes. */
            if (Compatibility == null)
                return;
            Compatibility.compatibility_info.RemoveAll(o => o.composite_id == composite);
        }
    }
}
