using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenCAGE.CrashTriage
{
    /// <summary>
    /// Watches each game launched from OpenCAGE until it closes. If it crashed, its dump (<see cref="LocalDumps"/>) is triaged
    /// (<see cref="CrashAnalysis"/>) and kept as <see cref="Last"/> for the next launch_game result and in <see cref="History"/>
    /// for the Game Crash Log, and on disk in %LOCALAPPDATA%\OpenCAGE\crash_triage.json.
    /// </summary>
    internal static class CrashWatch
    {
        /// <summary>A launch crashed and was triaged (raised on a worker thread).</summary>
        public static event Action<CrashReport> Crashed;

        private const int HistoryLength = 20;

        private static readonly object _lock = new object();
        private static int _generation;
        private static CrashReport _last;
        private static bool _lastReported;
        private static readonly List<CrashReport> _history = new List<CrashReport>();
        private static bool _loaded;

        private static string StateFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenCAGE", "crash_triage.json");

        /// <summary>The last launch's crash (the newest one seen), or null.</summary>
        public static CrashReport Last
        {
            get
            {
                lock (_lock)
                {
                    Load();
                    return _last;
                }
            }
        }

        /// <summary>
        /// The crashes of launches from OpenCAGE, newest first. One read back from disk holds only its summary
        /// (<see cref="CrashReport.Summary"/>) until <see cref="Full"/> reads its dump again.
        /// </summary>
        public static List<CrashReport> History
        {
            get
            {
                lock (_lock)
                {
                    Load();
                    return _history.ToList();
                }
            }
        }

        /// <summary>Empties <see cref="History"/>. The last crash is still given to launch_game's next result if it hasn't been.</summary>
        public static void ClearHistory()
        {
            lock (_lock)
            {
                Load();
                _history.Clear();
                Save();
            }
        }

        /// <summary>A report with its dump read again, if it was read back from disk as its summary. Reads the dump: not on the UI thread.</summary>
        public static CrashReport Full(CrashReport report)
        {
            if (report == null || !report.Summary || report.DumpPath == null || !File.Exists(report.DumpPath))
                return report;
            CrashReport full = CrashAnalysis.Analyze(report.DumpPath, report.Install);
            CopyLaunch(report, full);
            lock (_lock)
            {
                int index = _history.IndexOf(report);
                if (index != -1)
                    _history[index] = full;
                if (_last == report)
                    _last = full;
            }
            return full;
        }

        private static void CopyLaunch(CrashReport from, CrashReport to)
        {
            to.Install = from.Install;
            to.Level = from.Level;
            to.LaunchedUtc = from.LaunchedUtc;
            to.ExitedUtc = from.ExitedUtc;
            to.ExitCode = from.ExitCode;
        }

        /// <summary>
        /// A game was just launched (call it straight after starting it): watch it until it closes. <paramref name="started"/> is
        /// the AI.exe started from OpenCAGE (GOG, Epic), or null when Steam starts it; the game is then found by its path.
        /// </summary>
        public static void Launched(string pathToAI, string level, Process started = null)
        {
            int generation = Interlocked.Increment(ref _generation);
            DateTime launchedUtc = DateTime.UtcNow;
            int startedPid = -1;
            try { if (started != null) startedPid = started.Id; } catch (Exception) { }
            Task.Run(() =>
            {
                try
                {
                    Watch(generation, pathToAI, level, startedPid, launchedUtc);
                }
                catch (Exception e)
                {
                    Debug.Log("CrashTriage", "watch failed: " + e);
                }
            });
        }

        /// <summary>
        /// For launch_game's result: the last launch's crash when no result has said so yet (then it has), or null.
        /// </summary>
        public static CrashReport TakeUnreported()
        {
            lock (_lock)
            {
                Load();
                if (_last == null || _lastReported)
                    return null;
                _lastReported = true;
                Save();
                return _last;
            }
        }

        /// <summary>
        /// For launch_game's error when the game crashed during its wait: the triage of its dump in a few sentences (the known
        /// crash, its cause and fix), which also counts as having told the caller (no previous_crash for it next launch). With
        /// no dump: whether crash dumps are off, else where Windows logs the crash.
        /// </summary>
        public static string DescribeForMcp(string dump)
        {
            if (dump == null)
            {
                LocalDumps.State dumps = LocalDumps.Read();
                if (!dumps.On)
                    return "Windows writes no crash dumps of the game on this PC (" + dumps.Describe() + "), so crash triage can't say why it crashed: turning them on is the user's choice, in OpenCAGE's Game Crash Log (Script Debugger menu). The Windows Application log (Application Error, event 1000) names the module it crashed in.";
                return "The Windows Application log (Application Error, event 1000) names the module it crashed in.";
            }
            lock (_lock)
                _told.Add(Path.GetFullPath(dump));
            CrashReport report = CrashAnalysis.Analyze(dump, Singleton.PathToAI);
            lock (_lock)
            {
                Load();
                if (_last != null && _last.DumpPath != null && _told.Contains(Path.GetFullPath(_last.DumpPath)) && !_lastReported)
                {
                    _lastReported = true;
                    Save();
                }
            }
            if (report.Known != null)
                return "Crash triage: " + report.Known.Title + " (" + report.Confidence + "). What happened: " + report.Known.Cause + " What to do: " + report.Known.Fix + " (crash_triage gives the details.)";
            return "Crash triage: not a crash OpenCAGE knows - " + (report.FaultModule != null ? report.Where() : report.Problem) + ". crash_triage gives the AI.exe return addresses on its stack.";
        }

        //Dumps launch_game's errors have already told the caller about
        private static readonly HashSet<string> _told = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The newest dump of this AI.exe process (or any AI.exe with pid -1) written since the time given, or null.</summary>
        public static string FindDump(int pid, DateTime sinceUtc)
        {
            List<FileInfo> dumps = new List<FileInfo>();
            foreach (string folder in LocalDumps.Folders())
            {
                try
                {
                    if (Directory.Exists(folder))
                        dumps.AddRange(new DirectoryInfo(folder).GetFiles("AI.exe*.dmp").Where(o => o.LastWriteTimeUtc >= sinceUtc));
                }
                catch (Exception) { }
            }
            string ours = "AI.exe." + pid + ".dmp";
            return dumps.Where(o => pid == -1 || string.Equals(o.Name, ours, StringComparison.OrdinalIgnoreCase)).OrderByDescending(o => o.LastWriteTimeUtc).FirstOrDefault()?.FullName;
        }

        /// <summary>The newest AI.exe crash dumps in the dump folders, newest first.</summary>
        public static List<FileInfo> RecentDumps(int count)
        {
            List<FileInfo> dumps = new List<FileInfo>();
            foreach (string folder in LocalDumps.Folders())
            {
                try
                {
                    if (Directory.Exists(folder))
                        dumps.AddRange(new DirectoryInfo(folder).GetFiles("AI.exe*.dmp"));
                }
                catch (Exception) { }
            }
            return dumps.OrderByDescending(o => o.LastWriteTimeUtc).Take(count).ToList();
        }

        /// <summary>Whether a process exit code is a Windows exception status (a crash), not an ordinary exit.</summary>
        public static bool IsCrashCode(int code)
        {
            uint value = (uint)code;
            //Ctrl+C / its console closing ends a program without it crashing
            if (value == 0xC000013A)
                return false;
            return (value & 0xF0000000) == 0xC0000000 || value == 0xE06D7363 || value == 0x80000003;
        }

        /* Wait for the game to start (Steam starts it itself; Epic's launcher restarts the one started here), hold it, and once
           it has closed triage its crash, if it crashed. A newer launch ends this watch. */
        private static void Watch(int generation, string pathToAI, string level, int startedPid, DateTime launchedUtc)
        {
            Process game = null;
            try
            {
                if (startedPid != -1)
                    game = Hold(Process.GetProcessById(startedPid));
            }
            catch (Exception) { }
            Stopwatch waiting = Stopwatch.StartNew();
            //Three minutes for the game to show up (Steam's own checks first), a minute after an Epic restart
            TimeSpan patience = TimeSpan.FromMinutes(3);
            while (true)
            {
                if (Volatile.Read(ref _generation) != generation)
                {
                    game?.Dispose();
                    return;
                }
                if (game == null)
                {
                    game = FindGame(pathToAI);
                    if (game == null)
                    {
                        if (waiting.Elapsed > patience)
                            return;
                        Thread.Sleep(1000);
                        continue;
                    }
                }
                bool exited;
                try { exited = game.WaitForExit(1000); }
                catch (Exception) { exited = true; }
                if (!exited)
                    continue;

                int pid = Read(() => game.Id, -1);
                int? exitCode = Read<int?>(() => game.ExitCode, null);
                DateTime exitedUtc = Read<DateTime?>(() => game.ExitTime.ToUniversalTime(), null) ?? DateTime.UtcNow;
                game.Dispose();
                game = null;

                //Windows Error Reporting writes the dump before the process ends, but give it a moment should it still be writing
                bool crashCode = exitCode.HasValue && IsCrashCode(exitCode.Value);
                string dump = FindDump(pid, launchedUtc.AddSeconds(-5));
                for (int tries = 0; dump == null && crashCode && tries < 10; tries++)
                {
                    Thread.Sleep(500);
                    dump = FindDump(pid, launchedUtc.AddSeconds(-5));
                }
                if (!crashCode && dump == null)
                {
                    //Epic: the AI.exe started here closes within a second to restart through the Epic Games launcher
                    if (exitedUtc - launchedUtc < TimeSpan.FromSeconds(15))
                    {
                        waiting.Restart();
                        patience = TimeSpan.FromMinutes(1);
                        continue;
                    }
                    return;
                }
                if (Volatile.Read(ref _generation) != generation)
                    return;

                CrashReport report;
                if (dump != null)
                    report = CrashAnalysis.Analyze(dump, pathToAI);
                else
                {
                    LocalDumps.State dumps = LocalDumps.Read();
                    string name = CrashAnalysis.ExceptionName((uint)exitCode.Value);
                    report = new CrashReport()
                    {
                        ExceptionCode = (uint)exitCode.Value,
                        ExceptionName = name,
                        Problem = "The game crashed (exit code 0x" + ((uint)exitCode.Value).ToString("X8") + (name != null ? ": " + name : "") +
                            "), but Windows wrote no crash dump of it" + (dumps.On ? " (looked in " + string.Join(", ", LocalDumps.Folders()) + ")." : ": crash dumps are off. Turn them on in the Game Crash Log to find out why next time."),
                    };
                }
                report.Install = pathToAI;
                report.Level = level;
                report.LaunchedUtc = launchedUtc;
                report.ExitedUtc = exitedUtc;
                report.ExitCode = exitCode;
                lock (_lock)
                {
                    Load();
                    _last = report;
                    _history.Insert(0, report);
                    if (_history.Count > HistoryLength)
                        _history.RemoveRange(HistoryLength, _history.Count - HistoryLength);
                    //Already in launch_game's error, if that call was waiting on this game
                    _lastReported = report.DumpPath != null && _told.Contains(Path.GetFullPath(report.DumpPath));
                    Save();
                }
                Debug.Log("CrashTriage", report.Headline());
                try { Crashed?.Invoke(report); }
                catch (Exception e) { Debug.Log("CrashTriage", "a crash listener failed: " + e); }
                return;
            }
        }

        /* This install's AI.exe that is running now, its handle held so its exit code can be read once it has gone. One that has
           exited is still listed while anything holds a handle to it (Windows Error Reporting, say): only live ones count */
        private static Process FindGame(string pathToAI)
        {
            if (!string.Equals(Singleton.PathToAI, pathToAI, StringComparison.OrdinalIgnoreCase))
                return null;
            Process found = null;
            foreach (Process process in EditorUtils.ThisInstallsGameProcesses())
            {
                if (found == null)
                    found = Hold(process);
                else
                    process.Dispose();
            }
            return found;
        }

        private static Process Hold(Process process)
        {
            try
            {
                if (process.Handle != IntPtr.Zero && !process.HasExited)
                    return process;
            }
            catch (Exception) { }
            process.Dispose();
            return null;
        }

        private static T Read<T>(Func<T> read, T otherwise)
        {
            try { return read(); }
            catch (Exception) { return otherwise; }
        }

        /* The crashes as kept on disk. The last one's dump is read again (it has all the detail); the history's are read when
           shown (Full). One whose dump was deleted since keeps what was said */
        private static void Load()
        {
            if (_loaded)
                return;
            _loaded = true;
            try
            {
                if (!File.Exists(StateFile))
                    return;
                JObject state = JObject.Parse(File.ReadAllText(StateFile));
                if (state["last"] is JObject last)
                {
                    _last = FromEntry(last, true);
                    _lastReported = (bool?)last["reported"] ?? true;
                }
                if (state["history"] is JArray history)
                {
                    foreach (JObject entry in history.OfType<JObject>().Take(HistoryLength))
                    {
                        bool isLast = _last != null && _last.DumpPath != null && string.Equals((string)entry["dump"], _last.DumpPath, StringComparison.OrdinalIgnoreCase) &&
                            (DateTime?)entry["exited_utc"] == _last.ExitedUtc;
                        _history.Add(isLast ? _last : FromEntry(entry, false));
                    }
                }
                else if (_last != null)
                    _history.Add(_last);
            }
            catch (Exception e)
            {
                Debug.Log("CrashTriage", "could not read " + StateFile + ": " + e.Message);
            }
        }

        private static CrashReport FromEntry(JObject entry, bool readDump)
        {
            string dump = (string)entry["dump"];
            string install = (string)entry["install"];
            bool present = dump != null && File.Exists(dump);
            const string deleted = " (its dump has since been deleted)";
            string summary = (string)entry["summary"] ?? "";
            if (dump != null && !present && !summary.EndsWith(deleted, StringComparison.Ordinal))
                summary += deleted;
            CrashReport report = readDump && present ? CrashAnalysis.Analyze(dump, install) : new CrashReport()
            {
                DumpPath = dump,
                Problem = summary,
                Summary = present,
            };
            report.Install = install;
            report.Level = (string)entry["level"];
            report.LaunchedUtc = (DateTime?)entry["launched_utc"];
            report.ExitedUtc = (DateTime?)entry["exited_utc"];
            report.ExitCode = (int?)entry["exit_code"];
            return report;
        }

        private static JObject ToEntry(CrashReport report)
        {
            return new JObject()
            {
                ["dump"] = report.DumpPath,
                ["install"] = report.Install ?? Singleton.PathToAI,
                ["level"] = report.Level,
                ["launched_utc"] = report.LaunchedUtc,
                ["exited_utc"] = report.ExitedUtc,
                ["exit_code"] = report.ExitCode,
                ["summary"] = report.Summary ? report.Problem : report.Headline(),
            };
        }

        private static void Save()
        {
            try
            {
                JObject state = new JObject();
                if (_last != null)
                {
                    JObject last = ToEntry(_last);
                    last["reported"] = _lastReported;
                    state["last"] = last;
                }
                state["history"] = new JArray(_history.Select(ToEntry));
                Directory.CreateDirectory(Path.GetDirectoryName(StateFile));
                File.WriteAllText(StateFile, state.ToString());
            }
            catch (Exception e)
            {
                Debug.Log("CrashTriage", "could not write " + StateFile + ": " + e.Message);
            }
        }
    }
}
