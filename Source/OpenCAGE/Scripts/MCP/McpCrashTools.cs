using Newtonsoft.Json.Linq;
using OpenCAGE.CrashTriage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// Crash triage (Scripts\CrashTriage): why the game crashed, read from the crash dump Windows wrote of AI.exe. The 'game'
    /// toolset. launch_game carries the same report: in its error when a wait_for ends in a crash, and as previous_crash on the
    /// next launch.
    /// </summary>
    internal static class McpCrashTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "crash_triage",
                Title = "Why the game crashed",
                Description = "Read a crash dump of Alien: Isolation (the .dmp Windows writes when AI.exe crashes; no symbols needed) and name the known crash it is, with its cause and fix and how sure that is: certain (the game stopped at the very instruction a known crash stops at), likely (in that function, by its usual route), possible (in that function, but not by its usual route). Known crashes: the 19th character, the alien with no vents to retreat into, a human NPC in combat with no cover data, an NPC despawned mid take-cover, a SimpleWater with no renderable, a HUD movie the game can't drive, a display model with no animated model, over 2048 lights drawn at once, a material with an empty texture slot, and crashes inside the runtime utils or the graphics driver (known: true lists the table). An unknown crash still gets its place (module + offset, the build of AI.exe) and the AI.exe return addresses on its stack. Without arguments: the last crash of a game launched from OpenCAGE (launch_game also says it: in its error when a wait_for ends in a crash, and as previous_crash in the next launch's result). crash_dumps says whether Windows writes dumps of the game at all: turning them on is the user's choice, in the Game Crash Log (Script Debugger menu; it asks for administrator permission).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("dump", "An absolute path to an AI.exe crash dump (.dmp) to read."),
                    McpSchema.Boolean("latest", "Read the newest AI.exe dump in Windows' dump folders instead of the last launch's crash."),
                    McpSchema.Integer("list", "List the newest N AI.exe dumps (1-20) in the dump folders, each with its one-line triage."),
                    McpSchema.Boolean("known", "List the table of known crashes (id, title, the builds it has addresses for).")),
                ReadOnly = true,
                Idempotent = true,
                Concurrent = true,
                Toolset = "game",
                Run = Triage,
            };
        }

        private static object Triage(McpCall call)
        {
            JObject result = new JObject();
            int given = new[] { call.Has("dump"), call.Bool("latest"), call.Has("list") }.Count(o => o);
            if (given > 1)
                throw McpError.Invalid("Give one of dump, latest or list.");
            if (call.Bool("known"))
            {
                CrashTable table = CrashTable.Current;
                result["known_crashes"] = new JArray(table.Crashes.Select(o => new JObject()
                {
                    ["id"] = o.Id,
                    ["title"] = o.Title,
                    ["where"] = o.Modules.Count != 0 ? "in " + string.Join(", ", o.Modules) : string.Join(", ", o.Sites.Select(s => s.Name).Where(s => s != null).Distinct()),
                    ["builds"] = new JArray(o.Sites.Select(s => s.Build).Distinct()),
                    ["source"] = o.Source,
                }));
                result["builds"] = new JArray(table.Builds.Select(o => o.Name + " (AI.exe stamp 0x" + o.TimeDateStamp.ToString("X8") + ")"));
                result["user_table"] = CrashTable.UserFile + (File.Exists(CrashTable.UserFile) ? "" : " (none: add crashes there in the same format)");
                if (table.Problems.Count != 0) result["table_problems"] = new JArray(table.Problems);
            }

            if (call.Has("dump"))
            {
                string path = call.Str("dump");
                if (!Path.IsPathRooted(path))
                    throw McpError.Invalid("dump must be an absolute path to a .dmp file.");
                if (!File.Exists(path))
                    throw new McpError(McpErrorCodes.NotFound, "There is no file '" + path + "'.");
                result["crash"] = CrashAnalysis.Analyze(path, Singleton.PathToAI).ToJson();
            }
            else if (call.Bool("latest"))
            {
                FileInfo newest = CrashWatch.RecentDumps(1).FirstOrDefault();
                if (newest == null)
                    call.Note("There is no AI.exe crash dump in " + string.Join(", ", LocalDumps.Folders()) + ".");
                else
                    result["crash"] = CrashAnalysis.Analyze(newest.FullName, Singleton.PathToAI).ToJson();
            }
            else if (call.Has("list"))
            {
                int count = Math.Max(1, Math.Min(20, call.Int("list", 5)));
                result["dumps"] = new JArray(CrashWatch.RecentDumps(count).Select(o =>
                {
                    CrashReport report = CrashAnalysis.Analyze(o.FullName, Singleton.PathToAI);
                    JObject line = new JObject()
                    {
                        ["dump"] = o.FullName,
                        ["written"] = o.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        ["summary"] = report.Headline(),
                    };
                    if (report.Known != null) line["id"] = report.Known.Id;
                    if (report.FaultModule != null) line["at"] = report.FaultModule + "+0x" + report.FaultOffset.ToString("X") + (report.BuildKey != null ? " (" + report.BuildKey + ")" : "");
                    return line;
                }));
            }
            else if (!call.Bool("known"))
            {
                CrashReport last = CrashWatch.Last;
                if (last == null)
                    call.Note("No game launched from OpenCAGE has crashed (since crash triage has kept a record). latest: true reads the newest AI.exe dump on this PC.");
                else
                    result["crash"] = last.ToJson();
            }

            LocalDumps.State dumps = LocalDumps.Read();
            result["crash_dumps"] = new JObject()
            {
                ["on"] = dumps.On,
                ["state"] = dumps.Describe(),
                ["folders"] = new JArray(LocalDumps.Folders()),
            };
            if (!dumps.On)
                call.Note("Windows writes no crash dumps of the game on this PC, so a crash can't be triaged. Turning them on is the user's choice: the Game Crash Log (Script Debugger menu, Crash Dumps) says what it changes and asks for administrator permission.");
            return result;
        }
    }
}
