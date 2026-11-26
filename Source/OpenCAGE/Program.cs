using Assimp;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE;
using OpenCAGE.DockPanels;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE
{
    static class Program
    {
        static Dictionary<string, string> _args;
        static Stopwatch _timer = null;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            /* Every relative path OpenCAGE uses - LOGS, the native DLLs, the viewer - is meant relative
             * to the executable. Steam launches it that way; a shell launch (a double-clicked package
             * file) does not, so it is made so here before anything is read. (SettingsManager resolves
             * its own file against the exe folder regardless.) */
            try { Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory; } catch { }

            //Mono (Wine's default .NET) can't create OpenCAGE's windows
            if (Type.GetType("Mono.Runtime") != null)
            {
                try
                {
                    MessageBox.Show("OpenCAGE needs Microsoft .NET Framework 4.8, but it is running on Mono.\n\n" +
                        "On Linux or Steam Deck, install .NET Framework 4.8 into OpenCAGE's Wine/Proton prefix (for example \"protontricks 3367530 dotnet48\"), then launch OpenCAGE again.",
                        "OpenCAGE", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch { }
                Environment.Exit(0);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            /* A package file to open (a double-click in Explorer - see FileAssociations). If an OpenCAGE
             * is already running it takes the file, and this process goes away before it has touched
             * anything - not even the settings file. */
            string openFile = FindOpenFileArgument();
            if (openFile != null)
            {
                bool handedOver = false;
                try { handedOver = PackageHandover.TryHandOver(openFile); }
                catch { } //no exception handler is installed yet: a failure here means start normally
                if (handedOver)
                    return;
            }

            OpenCAGE.Theming.ThemeManager.Initialize();

            _args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            {
                var arguments = Environment.GetCommandLineArgs();
                for (int i = 0; i < arguments.Length; i++)
                {
                    var match = Regex.Match(arguments[i], "-([^=]+)=(.*)");
                    if (!match.Success) continue;
                    var vName = match.Groups[1].Value;
                    var vValue = match.Groups[2].Value;
                    _args[vName] = vValue;

                    if (_args[vName].Substring(_args[vName].Length - 1) == "\"")
                        _args[vName] = _args[vName].Substring(0, _args[vName].Length - 1);
                }

                //Optionally disable the viewport: by argument (the Steam launch option, child instances), or
                //by the Options > Viewport > Enable Viewport switch, which is the same thing remembered
                if (arguments.Any(o => string.Equals(o, "-disable_viewport", StringComparison.OrdinalIgnoreCase)))
                    Singleton.ViewportEnabled = false;
                else
                    Singleton.ViewportEnabled = File.Exists(Singleton.ViewportExecutablePath) && SettingsManager.GetBool(Settings.ViewportEnabled, true);

                //Optionally allow selecting a game folder without requiring AI.exe
                if (arguments.Any(o => string.Equals(o, "-dont_require_exe", StringComparison.OrdinalIgnoreCase)))
                    Singleton.DontRequireAIexe = true;
            }

            //Make sure we're using the UK culture to format our numbers correctly
            CultureInfo newCulture = CultureInfo.CreateSpecificCulture("en-GB");
            Thread.CurrentThread.CurrentUICulture = newCulture;
            Thread.CurrentThread.CurrentCulture = newCulture;

#if SHIP_BUILD
            //Advanced error handlers for silent exceptions
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += Application_ThreadException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            _timer = Stopwatch.StartNew();
#endif

            //DLL needs to be copied out for Assimp to work
            string dllPath = "runtimes\\win-x64\\native\\assimp.dll";
            if (!File.Exists(dllPath))
            {
                using (MemoryStream stream = new MemoryStream())
                using (GZipStream compressedStream = new GZipStream(new MemoryStream(Properties.Resources.assimp), CompressionMode.Decompress))
                {
                    compressedStream.CopyTo(stream);
                    Directory.CreateDirectory(Path.GetDirectoryName(dllPath));
                    File.WriteAllBytes(dllPath, stream.ToArray());
                }
            }

#if DEBUG
            //Assimp logging
            LogStream logstream = new LogStream(delegate (String msg, String userData) {
                Console.WriteLine(msg);
            });
            logstream.Attach();
#endif

#if SHIP_BUILD
            //Steam relaunches this process through itself without our arguments: park the file for the relaunch
            if (openFile != null)
                PackageHandover.Stash(openFile);

            //Initialise Steamworks
            try
            {
                Steam.Initialised = Steamworks.SteamAPI.Init();
                if (Steamworks.SteamAPI.RestartAppIfNecessary((Steamworks.AppId_t)3367530))
                {
                    Application.Exit();
                    Environment.Exit(0);
                    return;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Steamworks Exception: " + e.ToString());
                //A double-clicked package with Steam closed would otherwise vanish without a word
                if (openFile != null)
                    MessageBox.Show("OpenCAGE runs through Steam. Start Steam, then open the package again.", "OpenCAGE", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Application.Exit();
                Environment.Exit(0);
                return;
            }

            if (!Steam.Initialised)
            {
                bool steamRunning = false;
                try { steamRunning = Steamworks.SteamAPI.IsSteamRunning(); } catch { }
                MessageBox.Show(steamRunning
                    ? "OpenCAGE couldn't connect to Steam, although Steam is running.\n\nThis can happen when OpenCAGE and Steam are run with different permissions, for example OpenCAGE set to \"Run as administrator\" and Steam not. Run them both the same way, then launch OpenCAGE again from your Steam library."
                    : "OpenCAGE needs Steam to be running.\n\nStart Steam and sign in, then launch OpenCAGE again from your Steam library.",
                    "OpenCAGE", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Application.Exit();
                Environment.Exit(0);
                return;
            }
#endif

            //Work out path to Alien: Isolation
            if (GetArgument("pathToAI") != null)
            {
                /* A path that can't be made absolute is handed on as it is: the validation below turns it
                 * down and asks for a real install, where it used to surface as a crash before any window. */
                try
                {
                    Singleton.PathToAI = Path.GetFullPath(GetArgument("pathToAI"));
                }
                catch (Exception)
                {
                    Singleton.PathToAI = GetArgument("pathToAI");
                }
#if SHIP_BUILD
                Singleton.IsPrimaryInstance = false;
#endif
            }
            else
            {
                string[] directories = SettingsManager.GetStringArray(Settings.GameDirectories);
                if (directories.Length == 0 || !Utilities.IsGameDirectoryValid(directories[0]))
                {
                    if (Utilities.IsGameDirectoryValid(AppDomain.CurrentDomain.BaseDirectory + "/../Alien Isolation/"))
                    {
                        Singleton.PathToAI = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory + "/../Alien Isolation/");
                    }
                    else if (Utilities.IsGameDirectoryValid(AppDomain.CurrentDomain.BaseDirectory))
                    {
                        Singleton.PathToAI = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
                    }
                    else if (TryPromptForValidGameDirectory(out string selectedPath))
                    {
                        Singleton.PathToAI = selectedPath;
                    }
                    else
                    {
                        ExitMissingGameDirectory();
                        return;
                    }

                    SettingsManager.SetStringArray(Settings.GameDirectories, new string[1] { Singleton.PathToAI });
                }
                else
                {
                    Singleton.PathToAI = directories[0];
                }
            }

            // Final validation (covers -pathToAI, stale settings, and installs missing DATA/ENV)
            if (!Utilities.IsGameDirectoryValid(Singleton.PathToAI))
            {
                MessageBox.Show(
                    "The Alien: Isolation install at:\n" + Singleton.PathToAI +
                    "\n\nis missing required data (including DATA/ENV).\nPlease locate a valid install.",
                    "Invalid game install",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                if (TryPromptForValidGameDirectory(out string selectedPath))
                {
                    Singleton.PathToAI = selectedPath;
                    if (Singleton.IsPrimaryInstance)
                    {
                        List<string> directories = SettingsManager.GetStringArray(Settings.GameDirectories)
                            .Where(o => Utilities.IsGameDirectoryValid(o))
                            .Select(Path.GetFullPath)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        directories.RemoveAll(o => string.Equals(o, Singleton.PathToAI, StringComparison.OrdinalIgnoreCase));
                        directories.Insert(0, Singleton.PathToAI);
                        SettingsManager.SetStringArray(Settings.GameDirectories, directories.ToArray());
                    }
                }
                else
                {
                    ExitMissingGameDirectory();
                    return;
                }
            }

            //OpenCAGE edits the game's files in place, so it can do nothing without write access to them
            if (!CanWriteToGameFolder(Singleton.PathToAI))
            {
                ShowGameFolderNotWritable();
                Application.Exit();
                Environment.Exit(0);
                return;
            }

            //If the user has a custom CathodeLib file, use it!
            if (File.Exists(Singleton.PathToAI + "/" + Paths.CustomInfoDat))
                Paths.CustomInfoDat = Singleton.PathToAI + "/" + Paths.CustomInfoDat;

            //Work out and verify version/platform
            Singleton.Version = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;
            Singleton.Platform = PatchManager.GetPlatform(Singleton.PathToAI);
#if SHIP_BUILD
            try
            {
                if (Steam.Initialised)
                    SteamApps.GetCurrentBetaName(out Singleton.BetaName, 100);
                if (Singleton.BetaName == null)
                    Singleton.BetaName = "";
            }
            catch (Exception e)
            {
                Debug.Log("Program", "Failed to get Steam beta name: " + e.Message);
                Singleton.BetaName = "";
            }
#else
            Singleton.BetaName = "LOCAL";
#endif

            //The primary holds the lock for as long as it runs; a child polls for it (see CommandsEditor)
            if (Singleton.IsPrimaryInstance)
                PrimaryInstanceLock.TryAcquire();

            if (Singleton.IsPrimaryInstance)
                AnalyticsManager.LogAppStartup();

            //If we haven't already, copy the debug_font into the game's directory
            string debugFontDirectory = Singleton.PathToAI + "/DATA/debug_font/";
            if (!Directory.Exists(debugFontDirectory))
            {
                Directory.CreateDirectory(debugFontDirectory);
                File.WriteAllBytes(debugFontDirectory + "mini_font.fnt", Properties.Resources.mini_font);
                File.WriteAllBytes(debugFontDirectory + "mini_font_outlined.fnt", Properties.Resources.mini_font_outlined);
                File.WriteAllBytes(debugFontDirectory + "new_font.fnt", Properties.Resources.new_font);
                File.WriteAllBytes(debugFontDirectory + "tiny_font.fnt", Properties.Resources.tiny_font);
            }

            //Tidy up old install bloat, if it exists
            if (Directory.Exists(Singleton.PathToAI + "/data/modtools/remote_assets"))
            {
                try
                {
                    Directory.Delete(Singleton.PathToAI + "/data/modtools/remote_assets", true);
                }
                catch { }
            }

            /* Double-clickable package files: keep the .ocp (and .omp) associations pointing at this exe.
             * Only the primary registers, and only a shipped build does so by default - a debug run would
             * point the association at itself (see FileAssociations.ShouldRegister). The file this process
             * was launched with, if any, is opened once the window is up. */
            if (PrimaryInstanceLock.IsHeld && FileAssociations.ShouldRegister())
                FileAssociations.Register();
            PackageFiles.PendingOpen = openFile;

            Modding.ShaderDatabaseCatalogue.Register();

            //Run app
            Application.Run(new CommandsEditor(GetArgument("level")));
        }

        public static string GetArgument(string name)
        {
            if (_args.TryGetValue(name, out string arg))
                return arg;
            return null;
        }

        /* The -openfile= argument alone, read before the general parse so a handover can happen before
           anything else starts. Same shape as the parser above, including the stray closing quote a
           shell command line can leave on the end. */
        static string FindOpenFileArgument()
        {
            string prefix = "-" + FileAssociations.OpenFileArgument + "=";
            foreach (string argument in Environment.GetCommandLineArgs())
            {
                if (argument == null || !argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                string value = argument.Substring(prefix.Length).Trim();
                if (value.EndsWith("\""))
                    value = value.Substring(0, value.Length - 1);
                return value.Length == 0 ? null : value;
            }
            return null;
        }

        /* A value quoted for a child process's command line, so it parses back exactly as given. Windows
           only treats a backslash specially when it sits in front of a quote, so a path with a trailing
           separator wrapped in plain quotes - "C:\Alien Isolation\" - ends in \" which reads as a literal
           quote: the closing quote never comes, and everything after it (a -disable_viewport, say) lands
           inside the path (issue 649). The rule is to double any run of backslashes that precedes a quote,
           including the closing one, and escape quotes themselves. */
        public static string QuoteArgument(string value)
        {
            StringBuilder builder = new StringBuilder("\"");
            int backslashes = 0;
            foreach (char c in value ?? "")
            {
                if (c == '\\')
                {
                    backslashes++;
                    continue;
                }
                if (c == '"')
                {
                    builder.Append('\\', backslashes * 2 + 1);
                    builder.Append('"');
                    backslashes = 0;
                    continue;
                }
                builder.Append('\\', backslashes);
                builder.Append(c);
                backslashes = 0;
            }
            builder.Append('\\', backslashes * 2);
            builder.Append('"');
            return builder.ToString();
        }

        static bool TryPromptForValidGameDirectory(out string path)
        {
            path = null;
            GameDirectorySelectResult selectResult = GameDirectorySelector.TryPromptForGameDirectory(out string selectedPath);
            if (selectResult != GameDirectorySelectResult.Success)
                return false;

            path = selectedPath;
            return true;
        }

        static void ExitMissingGameDirectory()
        {
            SettingsManager.Unset(Settings.GameDirectories);
            MessageBox.Show("Failed to locate Alien: Isolation!\nOpenCAGE will now close.", "Failed to locate", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
            Environment.Exit(0);
        }

        static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            //A setup problem is explained instead of reported, and the editor carries on
            if (TryExplainSetupProblem(e.Exception))
                return;
            HandleError("Application_ThreadException\n" + e.Exception.ToString());
        }
        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            //Off the UI thread the process is going down regardless: explained, not reported
            if (TryExplainSetupProblem(e.ExceptionObject as Exception))
            {
                ExitAfterCriticalError();
                return;
            }
            HandleError("CurrentDomain_UnhandledException\n" + ((Exception)e.ExceptionObject).ToString());
        }

        /* Problems with the user's setup rather than bugs: write access to the game folder, and Windows
           Application Control (Smart App Control, WDAC) refusing to run a file. True when the exception is one,
           after telling the user what to do. */
        private static bool _explainingSetupProblem = false;
        static bool TryExplainSetupProblem(Exception exception)
        {
            if (exception == null)
                return false;
            bool blocked = IsApplicationControlBlock(exception, out string blockedFile);
            if (!blocked && !IsGameFolderAccessDenied(exception))
                return false;
            if (_explainingSetupProblem)
                return true;
            _explainingSetupProblem = true;
            try
            {
                if (blocked)
                    ShowApplicationControlBlocked(blockedFile == null ? "part of OpenCAGE" : "part of OpenCAGE (" + blockedFile + ")");
                else
                    ShowGameFolderNotWritable();
            }
            catch { }
            finally
            {
                _explainingSetupProblem = false;
            }
            return true;
        }

        static IEnumerable<Exception> ExceptionChain(Exception exception)
        {
            Stack<Exception> pending = new Stack<Exception>();
            if (exception != null)
                pending.Push(exception);
            while (pending.Count > 0)
            {
                Exception current = pending.Pop();
                yield return current;
                if (current is AggregateException aggregate)
                {
                    foreach (Exception inner in aggregate.InnerExceptions)
                        if (inner != null) pending.Push(inner);
                }
                else if (current.InnerException != null)
                    pending.Push(current.InnerException);
            }
        }

        /* A write to the game folder that Windows refused: the game under Program Files without elevation,
           or Controlled folder access. Matched by the path in the message, which is there in every language. */
        public static bool IsGameFolderAccessDenied(Exception exception)
        {
            string game = NormalisePath(Singleton.PathToAI);
            if (string.IsNullOrEmpty(game))
                return false;
            foreach (Exception current in ExceptionChain(exception))
            {
                if (!(current is UnauthorizedAccessException))
                    continue;
                string message = NormalisePath(current.Message);
                if (message != null && message.IndexOf(game, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        static string NormalisePath(string path)
        {
            return path?.Replace('/', '\\').TrimEnd('\\');
        }

        /* Whether writing to the game folder works, tried with a file that deletes itself. Only a refusal
           counts: any other failure is left to whatever writes there next. */
        static bool CanWriteToGameFolder(string pathToAI)
        {
            try
            {
                string probe = Path.Combine(pathToAI, "DATA", "opencage_write_check_" + Process.GetCurrentProcess().Id + ".tmp");
                using (new FileStream(probe, FileMode.Create, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (System.Security.SecurityException)
            {
                return false;
            }
            catch
            {
                return true;
            }
        }

        public static void ShowGameFolderNotWritable()
        {
            MessageBox.Show(
                "OpenCAGE can't write to the Alien: Isolation folder:\n" + Singleton.PathToAI + "\n\n" +
                "OpenCAGE edits the game's files in place, so it needs permission to change them. This usually means the game is installed under Program Files, which Windows protects.\n\n" +
                "To fix it, either:\n" +
                "- move the game to a library folder outside Program Files (in Steam: Settings > Storage, add a drive or folder, then move Alien: Isolation to it), or\n" +
                "- run OpenCAGE as administrator (Steam then has to run as administrator too).\n\n" +
                "If the game is already outside Program Files, check that Windows Security's Controlled folder access isn't blocking OpenCAGE.",
                "Can't write to the game folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /* Windows Application Control refusing a file: the "system integrity" errors, which arrive as a
           Win32Exception from Process.Start or as an HRESULT (FileLoadException) from a blocked DLL. */
        public static bool IsApplicationControlBlock(Exception exception, out string file)
        {
            file = null;
            foreach (Exception current in ExceptionChain(exception))
            {
                int code;
                if (current is System.ComponentModel.Win32Exception win32)
                    code = win32.NativeErrorCode;
                else if ((current.HResult & 0xFFFF0000) == 0x80070000)
                    code = current.HResult & 0xFFFF;
                else
                    continue;
                bool blocked = code == 4551 || (code >= 4556 && code <= 4559) || (code >= 4580 && code <= 4582);
                if (!blocked)
                    continue;
                if (current is FileLoadException load && !string.IsNullOrEmpty(load.FileName))
                    file = load.FileName.Split(',')[0];
                return true;
            }
            return false;
        }

        public static void ShowApplicationControlBlocked(string what)
        {
            MessageBox.Show(
                "Windows blocked " + what + " from running.\n\n" +
                "This is Windows Application Control (on Windows 11, usually Smart App Control) refusing a file it can't verify.\n\n" +
                "To use OpenCAGE, turn Smart App Control off in Windows Security > App & browser control > Smart App Control settings. On a work or school PC, ask whoever manages it to allow OpenCAGE.",
                "Blocked by Windows", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        private static bool _handlingError = false;

        /// <summary>
        /// True once a critical error is taking the application down. The main window's save prompt stands
        /// aside for it: the level's state is not to be trusted with a save, and a question nobody answers
        /// would keep a half-torn-down process alive.
        /// </summary>
        public static bool ExitingAfterCriticalError { get; private set; }

        /// <summary>
        /// Where crash logs are written: LOGS beside OpenCAGE.exe. Absolute, as a file dialog can leave the
        /// current directory somewhere else by the time something crashes. Help > Open Logs Folder opens it.
        /// </summary>
        public static string LogsFolder => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LOGS");

        static void HandleError(string error)
        {
            if (_handlingError)
                return;
            _handlingError = true;

            try
            {
                string logPath = Path.Combine(LogsFolder, "CECrash_" + DateTime.Now.ToString("ddMMyy-HHmmss") + ".log");
                Directory.CreateDirectory(LogsFolder);

                MessageBox.Show("A critical error occurred.\nPlease wait while a log is generated.", "OpenCAGE Error Handler", MessageBoxButtons.OK, MessageBoxIcon.Error);

                try
                {
                    Task.Run(async () =>
                    {
                        await UploadCrashLog(error, logPath);
                    }).Wait();

                    MessageBox.Show("Thanks, a log has been generated and auto-submitted.\nYou can find your logs locally within the OpenCAGE LOGS folder.", "OpenCAGE Error Handler", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch
                {
                    MessageBox.Show("A log has been generated.\nYou can find it within the OpenCAGE LOGS folder, please submit it to GitHub!", "OpenCAGE Error Handler", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch { }

            ExitAfterCriticalError();
        }

        /* Application.Exit asks every open form and gives up at the first refusal, which the void overload
           does not report. With a level loaded, a dock panel that hid instead of closing always refused,
           so a Ship build carried on after its "critical error" dialogs, half torn down - the main window's
           own closing had already run - and never killed the viewer: that is how one report went from a
           null reference to a viewer crash 45 seconds later. The panels now let an application exit
           through (CloseReasons), the save prompt is skipped by the flag, and whatever still refuses is
           not argued with: the viewer is stopped by hand and the process ends. */
        private static void ExitAfterCriticalError()
        {
            ExitingAfterCriticalError = true;

            bool refused = true;
            try
            {
                System.ComponentModel.CancelEventArgs exit = new System.ComponentModel.CancelEventArgs();
                Application.Exit(exit);
                refused = exit.Cancel;
            }
            catch
            {
            }

            if (refused)
            {
                try
                {
                    Singleton.Editor?.LevelViewerPanel?.Stop();
                }
                catch
                {
                }
            }

            Environment.Exit(1);
        }

        /// <summary>
        /// The embedded level viewer died (non-zero exit code). Logged and submitted the same way an OpenCAGE
        /// crash is, but as its own entry - the first line is "LevelViewerProcessExited" - so the crash stats can
        /// count it separately. No dialogs and no shutdown: OpenCAGE itself is fine, the viewport just went.
        /// The tail of the viewer's own output is the useful part; for the crash class found in issue #628 it
        /// carries the engine's "ERROR: Element limit reached" lines and the C# stack.
        ///
        /// The second line names <see cref="ViewportCrashException"/> and the exit code because the dashboard
        /// reads a report's type and message off the first "Type.SomethingException: message" line. Without
        /// one these arrive as "Unknown" and group by their last log line, which is whichever packet happened
        /// to be in flight - so the same fault shows up as several unrelated-looking entries.
        /// </summary>
        public static void ReportViewportCrash(int exitCode, string viewerOutputTail, DateTime? diedAt = null)
        {
            try
            {
                ViewportCrashException crash = new ViewportCrashException(exitCode);
                string fault = FaultingModuleFromEventLog("CathodeEditorGodot.exe", diedAt ?? DateTime.Now);
                string error = "LevelViewerProcessExited\n"
                    + crash.GetType().FullName + ": " + crash.Message + "\n"
                    + "Exit code: " + ViewportCrashException.Format(exitCode) + " (" + exitCode + ")\n"
                    + (fault ?? "(no Application Error event was recorded for the viewer)") + "\n"
                    + (string.IsNullOrWhiteSpace(viewerOutputTail)
                        ? "(no viewer output captured)"
                        : "Viewer output, last lines:\n" + viewerOutputTail);
                string logPath = Path.Combine(LogsFolder, "ViewportCrash_" + DateTime.Now.ToString("ddMMyy-HHmmss") + ".log");
                Directory.CreateDirectory(LogsFolder);
                Task.Run(async () =>
                {
                    try
                    {
                        await UploadCrashLog(error, logPath);
                    }
                    catch
                    {
                    }
                });
            }
            catch
            {
            }
        }

        /* Windows writes an "Application Error" event for a process that dies on a fault, naming the
           module and offset it died in - the one line an access violation report otherwise lacks, and
           what tells the engine, the runtime and a graphics driver apart. The entry is looked for close
           to when the viewer went; none is found for an exit code the viewer chose itself. */
        private static string FaultingModuleFromEventLog(string exeName, DateTime diedAt)
        {
            try
            {
                using (EventLog log = new EventLog("Application"))
                {
                    int count = log.Entries.Count;
                    for (int i = count - 1; i >= 0 && i >= count - 300; i--)
                    {
                        EventLogEntry entry = log.Entries[i];
                        if (entry.TimeGenerated < diedAt.AddSeconds(-30))
                            break;
                        if (entry.Source != "Application Error")
                            continue;
                        string[] fields = entry.ReplacementStrings;
                        if (fields == null || fields.Length < 8 || fields[0].IndexOf(exeName, StringComparison.OrdinalIgnoreCase) < 0)
                            continue;
                        //app name, app version, app stamp, module name, module version, module stamp, exception code, fault offset...
                        return "Faulting module: " + fields[3] + " (version " + fields[4] + ", stamp " + fields[5] + "), exception " + fields[6] + " at offset " + fields[7]
                            + "; viewer " + fields[1] + " stamp " + fields[2] + "; recorded " + entry.TimeGenerated.ToString("HH:mm:ss");
                    }
                }
            }
            catch
            {
                //The event log is a nicety: no access to it, or no record yet, and the report goes as before
            }
            return null;
        }

        static async Task UploadCrashLog(string error, string logPath)
        {
            try
            {
                var client = new HttpClient();
                var content = new MultipartFormDataContent();

                content.Add(new StringContent(error), "error_log");

                error += "\n **** ";

                string version = Singleton.Version;
                if (version == "")
                    version = Application.ProductVersion;
                string platform = Singleton.Platform.ToString();
                string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string uptime = _timer == null ? "" : _timer.Elapsed.ToString(@"dd\.hh\:mm\:ss");
                content.Add(new StringContent(version), "application_version");
                error += "\n Application Version: " + version;
                content.Add(new StringContent(platform), "game_version");
                error += "\n Game Version: " + platform;
                content.Add(new StringContent(time), "datetime");
                error += "\n Crash Time: " + time;
                content.Add(new StringContent(uptime), "uptime");
                error += "\n Uptime: " + uptime;

                error += "\n **** ";

                string level = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Name;
                CATHODE.Scripting.Composite composite = Singleton.Editor?.CompositeDisplay?.Composite;
                CATHODE.Scripting.Internal.Entity entity = Singleton.Editor?.CompositeDisplay?.EntityDisplay?.Entity;
                content.Add(new StringContent(level == null ? "Unknown/None" : level), "current_level");
                error += "\n Current Level: " + (level == null ? "Unknown/None" : level);
                content.Add(new StringContent(composite == null ? "Unknown/None" : composite.name), "current_composite");
                error += "\n Current Composite: " + (composite == null ? " Unknown/None" : composite.name);
                content.Add(new StringContent(entity == null ? "Unknown/None" : entity.shortGUID.ToByteString()), "current_entity");
                error += "\n Current Entity: " + (entity == null ? "Unknown/None" : entity.shortGUID.ToByteString());

                error += "\n **** ";

                string os = SystemInfo.GetOsName();
                string cpu = SystemInfo.GetCpuName();
                string ram = SystemInfo.GetTotalPhysicalMemory();
                content.Add(new StringContent(os), "os_name");
                error += "\n OS: " + os;
                content.Add(new StringContent(cpu), "cpu_name");
                error += "\n CPU: " + cpu;
                content.Add(new StringContent(ram), "ram_total");
                error += "\n RAM: " + ram;

                var response = await client.PostAsync("https://opencage.mattfiler.co.uk/crashes/crash_handler.php", content);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Failed to upload crash log [" + response.StatusCode + "]: " + response.RequestMessage);
                }
                else
                {
                    Console.WriteLine("Uploaded crash log successfully.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to create crash log to send: " + ex.Message);
            }

            File.WriteAllText(logPath, error);
        }
    }
}
