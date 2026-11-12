using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenCAGE.CrashTriage
{
    /// <summary>What crash triage made of one crash of the game: where it crashed, and the known crash it is, if any.</summary>
    internal sealed class CrashReport
    {
        public string DumpPath;
        public DateTime DumpTimeUtc;
        /// <summary>Why the dump could not be read (or that there was none), else null.</summary>
        public string Problem;

        /// <summary>The build of AI.exe (<see cref="CrashTable.Build.Key"/>), or null when the table does not know it.</summary>
        public string BuildKey;
        public string BuildName;
        public uint ExeTimeDateStamp;
        public uint ExeSize;

        public uint ExceptionCode;
        public string ExceptionName;
        /// <summary>For an access violation: "read", "write" or "execute"; else null.</summary>
        public string Access;
        public ulong AccessAddress;

        /// <summary>The module the crash happened in (its file name), and how far into it.</summary>
        public string FaultModule;
        public string FaultModulePath;
        public ulong FaultOffset;
        public bool FaultInGame;
        /// <summary>Return addresses into AI.exe on the crashing thread's stack (RVAs), the crash's callers first.</summary>
        public List<uint> Frames = new List<uint>();
        /// <summary>Whether each of <see cref="Frames"/> was checked to follow a call (AI.exe's code was at hand).</summary>
        public bool FramesChecked;

        public CrashTable.Crash Known;
        /// <summary>certain, likely or possible (<see cref="Known"/> set), else null.</summary>
        public string Confidence;
        /// <summary>The function of AI.exe it crashed in, when the table names it.</summary>
        public string Function;
        public List<string> Callers = new List<string>();

        //Filled by the watch on a launch (CrashWatch), not from the dump
        public string Level;
        public DateTime? LaunchedUtc;
        public DateTime? ExitedUtc;
        public int? ExitCode;

        public bool IsKnown => Known != null;

        /// <summary>One line: the known crash, or where it crashed.</summary>
        public string Headline()
        {
            if (Known != null)
                return Known.Title + " (" + Confidence + ")";
            if (Problem != null && FaultModule == null)
                return Problem;
            return "Not a crash OpenCAGE knows: " + Where();
        }

        /// <summary>"access violation reading 0x00000036 at AI.exe+0x5B21B9 (GOG)".</summary>
        public string Where()
        {
            StringBuilder text = new StringBuilder(ExceptionName ?? ("exception 0x" + ExceptionCode.ToString("X8")));
            if (Access != null)
                text.Append(" " + (Access == "read" ? "reading" : Access == "write" ? "writing" : "executing") + " 0x" + AccessAddress.ToString(AccessAddress > uint.MaxValue ? "X16" : "X8"));
            if (FaultModule != null)
                text.Append(" at " + FaultModule + "+0x" + FaultOffset.ToString("X"));
            else
                text.Append(" outside every module");
            if (FaultInGame)
                text.Append(" (" + (BuildName ?? "an unknown build: stamp " + ExeTimeDateStamp.ToString("X8") + ", size " + ExeSize.ToString("X8")) + ")");
            return text.ToString();
        }

        /// <summary>The whole report, as the Launch Game window's Details show it.</summary>
        public string Text()
        {
            StringBuilder text = new StringBuilder();
            if (Known != null)
            {
                text.AppendLine(Known.Title + " - " + ConfidenceText() + ".");
                text.AppendLine();
                text.AppendLine("What happened: " + Known.Cause);
                text.AppendLine();
                text.AppendLine("What to do: " + Known.Fix);
                if (!string.IsNullOrEmpty(Known.Docs))
                {
                    text.AppendLine();
                    text.AppendLine("More: " + Known.Docs);
                }
            }
            else if (FaultModule != null)
            {
                text.AppendLine("This is not one of the crashes OpenCAGE knows the cause of.");
                text.AppendLine();
                text.AppendLine("Check what changed in the level since it last ran, and the Error Log (Live Link). The dump and the addresses below are what " +
                    "someone with the game's symbols needs to name the crash - add it to OpenCAGE's table of known crashes (" + CrashTable.UserFile + ") once it is.");
            }
            else if (Problem != null)
                text.AppendLine(Problem);
            text.AppendLine();
            text.AppendLine("The crash");
            if (LaunchedUtc.HasValue && ExitedUtc.HasValue)
                text.AppendLine("  " + (Level != null ? Level + ", " : "") + "launched " + LaunchedUtc.Value.ToLocalTime().ToString("d MMM HH:mm:ss", CultureInfo.InvariantCulture) +
                    ", crashed " + Math.Round((ExitedUtc.Value - LaunchedUtc.Value).TotalSeconds, 1).ToString("0.#", CultureInfo.InvariantCulture) + " s later");
            if (FaultModule != null)
                text.AppendLine("  " + Where() + (Function != null ? ", in " + Function : ""));
            if (Callers.Count != 0)
                text.AppendLine("  called from " + string.Join(", ", Callers));
            if (Frames.Count != 0)
                text.AppendLine("  AI.exe return addresses on the stack" + (FramesChecked ? "" : " (unchecked)") + ": " + string.Join(" ", Frames.Take(12).Select(o => "+0x" + o.ToString("X"))));
            if (DumpPath != null)
                text.AppendLine("  dump: " + DumpPath + (DumpTimeUtc != default(DateTime) ? " (written " + DumpTimeUtc.ToLocalTime().ToString("d MMM yyyy HH:mm:ss", CultureInfo.InvariantCulture) + ")" : ""));
            if (Problem != null && FaultModule != null)
                text.AppendLine("  " + Problem);
            return text.ToString().TrimEnd();
        }

        public string ConfidenceText()
        {
            switch (Confidence)
            {
                case "certain": return "certain: the game stopped at the very instruction this crash stops at";
                case "likely": return "likely: the game crashed in the code this crash happens in";
                default: return "possible: the game crashed in code this crash goes through, but that alone does not prove it";
            }
        }

        public JObject ToJson()
        {
            JObject json = new JObject() { ["known"] = Known != null };
            if (Known != null)
            {
                json["id"] = Known.Id;
                json["title"] = Known.Title;
                json["confidence"] = Confidence;
                json["cause"] = Known.Cause;
                json["fix"] = Known.Fix;
                if (!string.IsNullOrEmpty(Known.Docs)) json["docs"] = Known.Docs;
            }
            json["summary"] = Headline();
            if (Level != null) json["level"] = Level;
            if (LaunchedUtc.HasValue && ExitedUtc.HasValue) json["seconds_after_launch"] = Math.Round((ExitedUtc.Value - LaunchedUtc.Value).TotalSeconds, 1);
            if (ExitedUtc.HasValue) json["crashed_at"] = ExitedUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            if (ExitCode.HasValue) json["exit_code"] = "0x" + ((uint)ExitCode.Value).ToString("X8");
            if (FaultModule != null)
            {
                json["exception"] = ExceptionName ?? ("0x" + ExceptionCode.ToString("X8"));
                if (Access != null) json["access"] = Access + " 0x" + AccessAddress.ToString("X");
                json["module"] = FaultModule;
                json["offset"] = "0x" + FaultOffset.ToString("X");
                if (FaultInGame) json["build"] = BuildKey ?? ("unknown (stamp 0x" + ExeTimeDateStamp.ToString("X8") + ", size 0x" + ExeSize.ToString("X8") + ")");
                if (Function != null) json["function"] = Function;
                if (Callers.Count != 0) json["called_from"] = new JArray(Callers);
                if (Frames.Count != 0) json["stack"] = new JArray(Frames.Take(12).Select(o => "0x" + o.ToString("X")));
            }
            if (DumpPath != null) json["dump"] = DumpPath;
            if (Problem != null) json["problem"] = Problem;
            return json;
        }
    }

    /// <summary>
    /// Crash triage: read a crash dump of AI.exe (no symbols needed) and match where it crashed against the table of known
    /// crashes (<see cref="CrashTable"/>).
    /// </summary>
    internal static class CrashAnalysis
    {
        private static readonly string[] _confidences = new[] { "possible", "likely", "certain" };

        /// <summary>
        /// Read a dump and name its crash. <paramref name="gameFolder"/> (optional) is where AI.exe can be found when the path the
        /// dump names is gone: with its code at hand, return addresses on the stack are checked to follow a call.
        /// </summary>
        public static CrashReport Analyze(string dumpPath, string gameFolder = null, CrashTable table = null)
        {
            table = table ?? CrashTable.Current;
            CrashReport report = new CrashReport() { DumpPath = dumpPath };
            try { report.DumpTimeUtc = File.GetLastWriteTimeUtc(dumpPath); } catch (Exception) { }
            MiniDump dump;
            try
            {
                dump = new MiniDump(dumpPath);
            }
            catch (Exception e)
            {
                report.Problem = "The crash dump could not be read (" + e.Message + ").";
                return report;
            }
            using (dump)
            {
                if (!dump.HasException)
                {
                    report.Problem = "The crash dump holds no exception (it was not written for a crash).";
                    return report;
                }
                report.ExceptionCode = dump.ExceptionCode;
                report.ExceptionName = ExceptionName(dump.ExceptionCode);
                if ((dump.ExceptionCode == 0xC0000005 || dump.ExceptionCode == 0xC0000006) && dump.ExceptionParameters.Length >= 2)
                {
                    report.Access = dump.ExceptionParameters[0] == 1 ? "write" : dump.ExceptionParameters[0] == 8 ? "execute" : "read";
                    report.AccessAddress = dump.ExceptionParameters[1];
                }

                MiniDump.Module game = dump.FindModule("AI.exe") ?? (dump.Modules.Count != 0 ? dump.Modules[0] : null);
                if (game != null)
                {
                    report.ExeTimeDateStamp = game.TimeDateStamp;
                    report.ExeSize = game.Size;
                    CrashTable.Build build = table.BuildOf(game.TimeDateStamp, game.Size);
                    report.BuildKey = build?.Key;
                    report.BuildName = build?.Name;
                }
                MiniDump.Module fault = dump.ModuleAt(dump.ExceptionAddress);
                if (fault != null)
                {
                    report.FaultModule = fault.FileName;
                    report.FaultModulePath = fault.Path;
                    report.FaultOffset = dump.ExceptionAddress - fault.Base;
                    report.FaultInGame = fault == game;
                }
                else
                    report.FaultModule = null;

                if (game != null)
                {
                    using (ExeCode code = ExeCode.Open(game, gameFolder))
                        Frames(dump, game, code, report);
                }
                Match(table, report);
                if (fault == null && report.Known == null)
                    report.Problem = "It crashed at 0x" + dump.ExceptionAddress.ToString("X") + ", outside every module the dump lists.";
            }
            return report;
        }

        /* The return addresses into AI.exe on the crashing thread's stack, nearest first: each checked to follow a call
           instruction when AI.exe's code is at hand (a stack also holds stale addresses and data that look like code) */
        private static void Frames(MiniDump dump, MiniDump.Module game, ExeCode code, CrashReport report)
        {
            byte[] stack = dump.Stack;
            if (stack == null)
                return;
            report.FramesChecked = code != null;
            for (int i = 0; i + 4 <= stack.Length && report.Frames.Count < 64; i += 4)
            {
                ulong value = BitConverter.ToUInt32(stack, i);
                if (!game.Contains(value))
                    continue;
                uint rva = (uint)(value - game.Base);
                if (rva < 0x1000)
                    continue;
                if (code != null && !code.FollowsCall(rva))
                    continue;
                report.Frames.Add(rva);
            }
        }

        private static void Match(CrashTable table, CrashReport report)
        {
            int best = -1;
            foreach (CrashTable.Crash crash in table.Crashes)
            {
                //A crash inside another module (the runtime utils, a graphics driver)
                int most = Array.IndexOf(_confidences, crash.Most);
                if (crash.Modules.Count != 0 && report.FaultModule != null && !report.FaultInGame && crash.Modules.Any(o => Wildcard(o, report.FaultModule)))
                {
                    int moduleLevel = Math.Min(1, most);
                    if (moduleLevel > best)
                    {
                        best = moduleLevel;
                        report.Known = crash;
                        report.Confidence = _confidences[moduleLevel];
                        report.Function = null;
                        report.Callers.Clear();
                    }
                    continue;
                }
                if (!report.FaultInGame || report.BuildKey == null)
                    continue;
                uint rva = (uint)report.FaultOffset;
                List<CrashTable.Function> vias = crash.Vias.Where(o => o.Build == report.BuildKey).ToList();
                foreach (CrashTable.Function site in crash.Sites.Where(o => o.Build == report.BuildKey && o.Contains(rva)))
                {
                    bool exact = site.Exact.Contains(rva);
                    //Its usual callers: within the first frames, so a stale address deep in the stack doesn't count
                    List<CrashTable.Function> seen = vias.Where(via => report.Frames.Take(24).Any(f => via.Contains(f))).ToList();
                    //A function many things call counts only by the route this crash takes
                    if (site.NeedVia && seen.Count == 0)
                        continue;
                    int level;
                    if (site.Weak) level = 0;
                    else if (vias.Count == 0 || seen.Count != 0) level = exact ? 2 : 1;
                    else level = exact ? 1 : 0;
                    level = Math.Min(level, most);
                    if (level <= best)
                        continue;
                    best = level;
                    report.Known = crash;
                    report.Confidence = _confidences[level];
                    report.Function = NameOf(crash, site, report.BuildKey);
                    report.Callers = seen.Select(o => NameOf(crash, o, report.BuildKey) ?? ("+0x" + o.Start.ToString("X"))).Distinct().ToList();
                }
            }
        }

        /* A function's name: on its own line, else on the Steam line the same position among the crash's sites or vias */
        private static string NameOf(CrashTable.Crash crash, CrashTable.Function function, string build)
        {
            if (function.Name != null)
                return function.Name;
            List<CrashTable.Function> list = crash.Sites.Contains(function) ? crash.Sites : crash.Vias;
            int index = list.Where(o => o.Build == build).ToList().IndexOf(function);
            List<CrashTable.Function> named = list.Where(o => o.Name != null).ToList();
            return index >= 0 && index < named.Count ? named[index].Name : null;
        }

        private static bool Wildcard(string pattern, string name)
        {
            string regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return Regex.IsMatch(name, regex, RegexOptions.IgnoreCase);
        }

        /// <summary>What a Windows exception code means, or null when it is none of the usual ones.</summary>
        public static string ExceptionName(uint code)
        {
            switch (code)
            {
                case 0xC0000005: return "access violation";
                case 0xC0000006: return "in-page error (a file it had mapped could not be read)";
                case 0xC0000017: return "out of memory";
                case 0xC000001D: return "illegal instruction";
                case 0xC0000094: return "integer divide by zero";
                case 0xC0000096: return "privileged instruction";
                case 0xC00000FD: return "stack overflow";
                case 0xC0000374: return "heap corruption";
                case 0xC0000409: return "stack buffer overrun or fail fast";
                case 0xC0000420: return "assertion failure";
                case 0xE06D7363: return "unhandled C++ exception";
                case 0x80000003: return "breakpoint";
            }
            return null;
        }

        /// <summary>
        /// AI.exe's code, read from the file the dump says was loaded (or AI.exe in the game folder), when it is the same build
        /// (PE stamp and size): to check that a return address on the stack follows a call.
        /// </summary>
        private sealed class ExeCode : IDisposable
        {
            private readonly FileStream _file;
            private readonly List<(uint va, uint size, uint raw, uint rawSize)> _sections = new List<(uint, uint, uint, uint)>();

            private ExeCode(FileStream file)
            {
                _file = file;
            }

            public static ExeCode Open(MiniDump.Module module, string gameFolder)
            {
                List<string> paths = new List<string>();
                if (!string.IsNullOrEmpty(module.Path)) paths.Add(module.Path);
                if (!string.IsNullOrEmpty(gameFolder)) paths.Add(Path.Combine(gameFolder, "AI.exe"));
                foreach (string path in paths)
                {
                    FileStream file = null;
                    try
                    {
                        if (!File.Exists(path))
                            continue;
                        file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                        ExeCode code = new ExeCode(file);
                        if (code.Read(module))
                            return code;
                        file.Dispose();
                    }
                    catch (Exception)
                    {
                        file?.Dispose();
                    }
                }
                return null;
            }

            private bool Read(MiniDump.Module module)
            {
                BinaryReader reader = new BinaryReader(_file);
                _file.Position = 0x3C;
                int pe = reader.ReadInt32();
                _file.Position = pe;
                if (reader.ReadUInt32() != 0x4550)
                    return false;
                reader.ReadUInt16();
                int sections = reader.ReadUInt16();
                uint stamp = reader.ReadUInt32();
                _file.Position = pe + 20;
                int optional = reader.ReadUInt16();
                _file.Position = pe + 24 + 56;
                uint size = reader.ReadUInt32();
                if (stamp != module.TimeDateStamp || size != module.Size)
                    return false;
                for (int i = 0; i < sections; i++)
                {
                    _file.Position = pe + 24 + optional + 40 * i + 8;
                    uint vsize = reader.ReadUInt32(), va = reader.ReadUInt32(), rawSize = reader.ReadUInt32(), raw = reader.ReadUInt32();
                    _sections.Add((va, vsize, raw, rawSize));
                }
                return true;
            }

            private byte[] Bytes(uint rva, int count)
            {
                foreach (var section in _sections)
                {
                    if (rva < section.va || rva + count > section.va + Math.Min(section.size, section.rawSize))
                        continue;
                    _file.Position = section.raw + (rva - section.va);
                    byte[] bytes = new byte[count];
                    return _file.Read(bytes, 0, count) == count ? bytes : null;
                }
                return null;
            }

            /// <summary>Whether a call instruction ends at this RVA (E8 rel32, or one of the FF /2 forms).</summary>
            public bool FollowsCall(uint rva)
            {
                if (rva < 8)
                    return false;
                byte[] b = Bytes(rva - 7, 7);
                if (b == null)
                    return false;
                //b[7 - n] is the first byte of an n-byte instruction ending at rva
                if (b[2] == 0xE8)
                    return true;
                for (int n = 2; n <= 7; n++)
                {
                    if (b[7 - n] != 0xFF)
                        continue;
                    byte modrm = b[8 - n];
                    if ((modrm & 0x38) != 0x10)
                        continue;
                    int mod = modrm >> 6, rm = modrm & 7;
                    int length = 2;
                    if (mod != 3 && rm == 4)
                    {
                        if (n < 3) continue;
                        length++;
                        byte sib = b[9 - n];
                        if (mod == 0 && (sib & 7) == 5) length += 4;
                    }
                    if (mod == 1) length += 1;
                    else if (mod == 2) length += 4;
                    else if (mod == 0 && rm == 5) length += 4;
                    if (length == n)
                        return true;
                }
                return false;
            }

            public void Dispose()
            {
                _file.Dispose();
            }
        }
    }
}
