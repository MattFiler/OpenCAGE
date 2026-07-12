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

                //Optionally disable the viewport
                if (arguments.Any(o => string.Equals(o, "-disable_viewport", StringComparison.OrdinalIgnoreCase)))
                    Singleton.ViewportEnabled = false;
                else
                    Singleton.ViewportEnabled = File.Exists(Singleton.ViewportExecutablePath);
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
                Singleton.PathToAI = Path.GetFullPath(GetArgument("pathToAI"));
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
                    else
                    {
                        MessageBox.Show("Please locate your Alien: Isolation executable (AI.exe).", "OpenCAGE Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        using (OpenFileDialog dialog = new OpenFileDialog())
                        {
                            dialog.Filter = "Applications (*.exe)|AI.exe";
                            DialogResult result = dialog.ShowDialog();
                            if (result == DialogResult.OK && Utilities.IsGameDirectoryValid(Path.GetDirectoryName(dialog.FileName)))
                            {
                                Singleton.PathToAI = Path.GetFullPath(Path.GetDirectoryName(dialog.FileName));
                            }
                            else
                            {
                                SettingsManager.Unset(Settings.GameDirectories);
                                MessageBox.Show("Failed to locate Alien: Isolation!\nOpenCAGE will now close.", "Failed to locate", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                Application.Exit();
                                Environment.Exit(0);
                                return;
                            }
                        }
                    }
                    SettingsManager.SetStringArray(Settings.GameDirectories, new string[1] { Singleton.PathToAI });
                }
                else
                {
                    Singleton.PathToAI = directories[0];
                }
            }

            //If the user has a custom CathodeLib file, use it!
            if (File.Exists(Singleton.PathToAI + "/" + Paths.CustomInfoDat))
                Paths.CustomInfoDat = Singleton.PathToAI + "/" + Paths.CustomInfoDat;
            if (File.Exists(Singleton.PathToAI + "/" + Paths.CustomSoundBin))
                Paths.CustomSoundBin = Singleton.PathToAI + "/" + Paths.CustomSoundBin;

            //Work out and verify version/platform
            Singleton.Version = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;
            Singleton.Platform = PatchManager.GetPlatform(Singleton.PathToAI);
#if SHIP_BUILD
            SteamApps.GetCurrentBetaName(out Singleton.BetaName, 100);
            if (Singleton.BetaName == null)
                Singleton.BetaName = "";
#else
            Singleton.BetaName = "LOCAL";
#endif

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

            //Run app
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new CommandsEditor(GetArgument("level")));
        }

        public static string GetArgument(string name)
        {
            if (_args.TryGetValue(name, out string arg))
                return arg;
            return null;
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
