using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// The Level Check (Scripts\LevelCheck): check_level, fix_level_check (fix, dismiss, bring back), set_texture_heap. Registered from
    /// McpBrowseTools.Tools(); the 'core' toolset, beside save_level (whose level_warnings are this check's findings after a build).
    /// </summary>
    internal static class McpLevelCheckTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "check_level",
                Title = "Check the level for crashes and silent failures",
                Description = "The Level Check (File > Level Check): what in the open level will crash the game or silently not work in it - each a lesson found the hard way in game. " +
                    "Checks: " + string.Join("; ", LevelCheck.Checks.Select(o => o.Id + " (" + o.About + ")")) + ". " +
                    "save_level with build=true runs it too and lists what it finds (not dismissed) as level_warnings. Each finding has an id, severity (crash, broken, warning), " +
                    "where it comes from, and a fix when one is mechanical (fix_level_check applies it, or dismisses a finding for this level). figures carries what the checks " +
                    "worked out: characters (how many the level can have alive at once, of the game's pool of 18), textures (the streamed textures that must fit in the heap at once, " +
                    "per zone, with the biggest), sound_banks, planar_reflections. Reads the level as it is now, saved or not (models' zones and flags are as last built).",
                InputSchema = McpSchema.Object(
                    McpSchema.Strings("checks", "Only these checks (ids as listed above). Default: all."),
                    McpSchema.Boolean("include_dismissed", "List findings dismissed for this level too (default false: they are counted only)."),
                    McpSchema.Integer("max_sources", "At most this many sources per finding (default 10).")),
                ReadOnly = true,
                Idempotent = true,
                Toolset = "core",
                Run = Check,
            };

            yield return new McpTool()
            {
                Name = "fix_level_check",
                Title = "Fix or dismiss a Level Check finding",
                Description = "Act on a finding check_level listed (by its id, as check_level gives it): action 'fix' applies its mechanical fix - a change to the open level's script made as " +
                    "one undo step (Save & Build takes it into the game), or for the texture heap a GLOBAL change to the game's DATA\\ENGINE_SETTINGS.XML (every level; refused unless global: true) - " +
                    "'dismiss' stops save_level and the editor saying it for this level (kept on this PC), 'restore' brings a dismissed one back. The level is checked again first, so the id " +
                    "must still be found. dry_run says what the fix would do.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("finding", "The finding's id from check_level (check:key).", required: true),
                    McpSchema.String("action", "fix (default), dismiss or restore.", options: new[] { "fix", "dismiss", "restore" }),
                    McpSchema.Boolean("global", "Allow a fix that changes a file outside the level (the texture heap): it affects every level and every player of a mod carrying it."),
                    McpSchema.Boolean("dry_run", "Say what the fix would do, changing nothing.")),
                Destructive = true,
                Toolset = "core",
                Run = Fix,
            };

            yield return new McpTool()
            {
                Name = "set_texture_heap",
                Title = "Set the game's texture streaming heap (global)",
                Description = "Set Streaming Texture Heap Size (Default and Ultra) in the game's DATA\\ENGINE_SETTINGS.XML - how much memory the game streams full-size textures into (300 MB as " +
                    "shipped); textures that don't fit draw from their small 128 px copies (blocky). GLOBAL: every level uses it, and a mod exported with its config changes carries it to its " +
                    "players. 512 is the tested raise; past ~700 the 32-bit game runs out of its own memory (768 took it past 2 GiB). check_level's figures.textures says what a level needs. " +
                    "Only the two numbers change; Reset Configs (graphics) puts 300 back. Without mb, reports the current values.",
                InputSchema = McpSchema.Object(
                    McpSchema.Integer("mb", "The heap in MB (64-1024; 512 recommended). Omit to read the current values.")),
                Destructive = true,
                Toolset = "core",
                Run = SetHeap,
            };
        }

        private static JObject Describe(LevelCheckFinding finding, int maxSources)
        {
            JObject described = new JObject()
            {
                ["id"] = finding.Id,
                ["check"] = finding.Check,
                ["severity"] = finding.Severity == LevelCheckSeverity.Crash ? "crash" : finding.Severity == LevelCheckSeverity.Broken ? "broken" : "warning",
                ["title"] = finding.Title,
                ["text"] = finding.Text,
                ["sources"] = new JArray(finding.Sources.Take(maxSources).Select(o => o.Text)),
            };
            if (finding.Sources.Count > maxSources) described["sources_total"] = finding.Sources.Count;
            if (finding.Fix != null)
                described["fix"] = new JObject() { ["label"] = finding.Fix.Label, ["detail"] = finding.Fix.Detail, ["global"] = finding.Fix.Global };
            if (finding.Dismissed) described["dismissed"] = true;
            return described;
        }

        private static LevelCheckReport RunCheck(McpCall call, IEnumerable<string> only)
        {
            call.Progress("Checking the level");
            //On the UI thread: the script can't change under the check
            return McpEditor.UI(() => LevelCheck.Run(McpEditor.RequireLevel(false).Level, only));
        }

        private static object Check(McpCall call)
        {
            List<string> only = call.Has("checks") ? call.StrList("checks") : null;
            if (only != null)
            {
                List<string> known = LevelCheck.Checks.Select(o => o.Id).ToList();
                foreach (string id in only)
                    if (!known.Contains(id, StringComparer.OrdinalIgnoreCase))
                        throw McpError.NotFound("check", id, known);
            }
            int maxSources = Math.Max(1, call.Int("max_sources", 10));
            bool dismissed = call.Bool("include_dismissed");
            LevelCheckReport report = RunCheck(call, only);
            List<LevelCheckFinding> shown = report.Findings.Where(o => dismissed || !o.Dismissed).OrderBy(o => o.Severity).ToList();
            JObject result = new JObject()
            {
                ["level"] = report.Level,
                ["findings"] = new JArray(shown.Select(o => Describe(o, maxSources))),
                ["dismissed"] = report.Findings.Count(o => o.Dismissed),
                ["figures"] = report.Figures,
                ["seconds"] = report.Seconds,
            };
            if (report.Failed.Count != 0) result["failed_checks"] = JObject.FromObject(report.Failed);
            if (report.Truncated) call.Note("The level is bigger than one placement walk covers: placed content past its budget was not checked.");
            if (shown.Count == 0) call.Note(report.Findings.Count == 0 ? "Nothing found." : "Nothing found that isn't dismissed (include_dismissed lists those).");
            return result;
        }

        private static object Fix(McpCall call)
        {
            string id = call.Str("finding", required: true).Trim();
            string action = (call.Str("action") ?? "fix").Trim().ToLowerInvariant();
            string check = id.Contains(":") ? id.Substring(0, id.IndexOf(':')) : id;
            LevelCheckReport report = RunCheck(call, LevelCheck.Checks.Any(o => string.Equals(o.Id, check, StringComparison.OrdinalIgnoreCase)) ? new[] { check } : null);
            LevelCheckFinding finding = report.Findings.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.Ordinal));
            if (finding == null)
            {
                //Dismissing or restoring one no longer found is still allowed: the id is all that is kept
                if (action == "restore" || action == "dismiss")
                {
                    bool changed = LevelCheck.Dismissals.Set(report.Level, id, action == "dismiss");
                    return new JObject() { ["finding"] = id, ["action"] = action, ["changed"] = changed, ["note"] = "Not found by the check now; its dismissal was " + (action == "dismiss" ? "recorded" : "removed") + " anyway." };
                }
                throw McpError.NotFound("finding", id, report.Findings.Select(o => o.Id), "check_level lists the findings and their ids.");
            }
            switch (action)
            {
                case "dismiss":
                case "restore":
                    {
                        bool changed = LevelCheck.Dismissals.Set(report.Level, finding.Id, action == "dismiss");
                        return new JObject()
                        {
                            ["finding"] = finding.Id,
                            ["action"] = action,
                            ["changed"] = changed,
                            ["note"] = action == "dismiss" ? "save_level and the editor no longer say it for " + report.Level + " (kept on this PC)." : "Brought back: save_level says it again.",
                        };
                    }
                case "fix":
                    {
                        if (finding.Fix == null)
                            throw new McpError(McpErrorCodes.Refused, finding.Id + " has no mechanical fix: " + finding.Text);
                        if (call.Bool("dry_run"))
                            return new JObject() { ["finding"] = finding.Id, ["dry_run"] = true, ["fix"] = finding.Fix.Label, ["detail"] = finding.Fix.Detail, ["global"] = finding.Fix.Global };
                        if (finding.Fix.Global && !call.Bool("global"))
                            throw new McpError(McpErrorCodes.Refused, "This fix changes a file outside the level, which every level (and every player of a mod carrying it) sees: " + finding.Fix.Detail + " Pass global: true to make it.");
                        string done = finding.Fix.Global ? finding.Fix.Apply(true) : McpEditor.UI(() => finding.Fix.Apply(true));
                        LevelCheckReport after = RunCheck(call, new[] { finding.Check });
                        return new JObject()
                        {
                            ["finding"] = finding.Id,
                            ["fixed"] = done,
                            ["global"] = finding.Fix.Global,
                            ["still_found"] = after.Findings.Any(o => o.Id == finding.Id),
                        };
                    }
                default:
                    throw McpError.Invalid("'action' is fix, dismiss or restore.");
            }
        }

        private static object SetHeap(McpCall call)
        {
            string path = TextureBudget.SettingsPath(Singleton.PathToAI);
            if (!call.Has("mb"))
            {
                (int? def, int? ultra) = System.IO.File.Exists(path) ? TextureBudget.ReadHeap(System.IO.File.ReadAllText(path)) : (null, null);
                return new JObject() { ["file"] = path, ["default_mb"] = def, ["ultra_mb"] = ultra, ["shipped_mb"] = TextureBudget.RetailHeapMB };
            }
            int mb = call.Int("mb");
            if (mb < 64 || mb > 1024) throw McpError.Invalid("'mb' is 64 to 1024 (512 is the tested raise; 768 takes the 32-bit game past 2 GiB of memory).");
            if (EditorUtils.ThisInstallsGameRunning())
                call.Note("The game is running: it reads the heap size as it starts, so this takes effect from its next start.");
            return new JObject() { ["file"] = path, ["done"] = TextureBudget.SetHeap(Singleton.PathToAI, mb), ["global"] = true };
        }
    }
}
