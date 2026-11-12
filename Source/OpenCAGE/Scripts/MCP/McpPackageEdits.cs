using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// import_composite_package and the user's own edits: with overwrite_composites, a composite the destination already has
    /// and the user has changed there (its edit history - CompositeArchive.EditedCompositesItWouldReplace) is kept unless
    /// keep_edited is false, as the import window keeps it unless its row is ticked. dry_run lists them.
    /// </summary>
    internal static class McpPackageEdits
    {
        public const string KeepEditedArg = "keep_edited";

        public static McpSchema.Prop KeepEditedSchema() => McpSchema.Boolean(KeepEditedArg,
            "With overwrite_composites: keep the level's own copy of each composite you have changed there (its edit history: made, edited or brought in with OpenCAGE) instead of replacing it - default true. dry_run lists them (edited_kept / edited_replaced). false replaces them too, losing those changes.");

        public static bool KeepEdited(McpCall call) => call.Bool(KeepEditedArg, true);

        /// <summary>A dry_run target: the edited composites the import would keep, or replace with keep_edited false.</summary>
        public static JObject Plan(JObject target, List<CompositeArchive.EditedComposite> edited, bool overwrite, bool keepEdited)
        {
            if (!overwrite || edited == null || edited.Count == 0)
                return target;
            target[keepEdited ? "edited_kept" : "edited_replaced"] = new JArray(edited.Take(100).Select(o => new JObject()
            {
                ["name"] = o.Name,
                ["edited"] = o.Edited.ToString("yyyy-MM-dd HH:mm"),
            }));
            if (keepEdited && target["would_replace"] is JArray replace)
            {
                HashSet<string> kept = new HashSet<string>(edited.Select(o => o.Name));
                target["would_replace"] = new JArray(replace.Select(o => (string)o).Where(o => !kept.Contains(o)));
            }
            return target;
        }

        /// <summary>The names kept, on an import's summary (or a level's item of it), with a note on the call when there is one.</summary>
        public static void Report(McpCall call, JObject summary, CompositeArchive.Result result)
        {
            if (result?.KeptEdited == null || result.KeptEdited.Count == 0)
                return;
            summary["kept_edited"] = new JArray(result.KeptEdited.Take(200));
            call?.Note(result.KeptEdited.Count + " composite(s) you had changed in the level kept your version (kept_edited): pass keep_edited false to have the package replace them.");
        }
    }
}
