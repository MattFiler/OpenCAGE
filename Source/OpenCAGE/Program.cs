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
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

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
            //Initialise Steamworks
            try
            {
                Steamworks.SteamAPI.Init();
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

            //If the user has a custom CathodeLib file, use it!
            if (File.Exists(Singleton.PathToAI + "/" + Paths.CustomInfoDat))
                Paths.CustomInfoDat = Singleton.PathToAI + "/" + Paths.CustomInfoDat;

            //Work out and verify version/platform
            Singleton.Version = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;
            Singleton.Platform = PatchManager.GetPlatform(Singleton.PathToAI);
#if SHIP_BUILD
            try
            {
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

#if ENABLE_MOD_PACKAGES
            //Double-clickable mod packages: keep the .opencage association pointing at this exe,
            //and pick up a package we were launched with
            Modding.PackageFileAssociation.Register();
            Modding.ModServices.PendingPackageImport = GetArgument("modpackage");
#endif

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
            HandleError("Application_ThreadException\n" + e.Exception.ToString());
        }
        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            HandleError("CurrentDomain_UnhandledException\n" + ((Exception)e.ExceptionObject).ToString());
        }
        private static bool _handlingError = false;
        static void HandleError(string error)
        {
            if (_handlingError)
                return;
            _handlingError = true;

            try
            {
                string logPath = "LOGS/CECrash_" + DateTime.Now.ToString("ddMMyy-HHmmss") + ".log";
                Directory.CreateDirectory("LOGS");

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

            try
            {
                Application.Exit();
            }
            catch
            {
                Environment.Exit(1);
            }
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
        public static void ReportViewportCrash(int exitCode, string viewerOutputTail)
        {
            try
            {
                ViewportCrashException crash = new ViewportCrashException(exitCode);
                string error = "LevelViewerProcessExited\n"
                    + crash.GetType().FullName + ": " + crash.Message + "\n"
                    + "Exit code: " + ViewportCrashException.Format(exitCode) + " (" + exitCode + ")\n"
                    + (string.IsNullOrWhiteSpace(viewerOutputTail)
                        ? "(no viewer output captured)"
                        : "Viewer output, last lines:\n" + viewerOutputTail);
                string logPath = "LOGS/ViewportCrash_" + DateTime.Now.ToString("ddMMyy-HHmmss") + ".log";
                Directory.CreateDirectory("LOGS");
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
                error += "\n Current Level: " + level == null ? "Unknown/None" : level;
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

                var response = await client.PostAsync("http://opencage.mattfiler.co.uk/crashes/crash_handler.php", content);
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
