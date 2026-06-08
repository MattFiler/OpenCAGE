using Assimp;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE;
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
            if (File.Exists("steam_api64.dll") && File.Exists("Assets/assets.manifest"))
            {
                try
                {
                    Steamworks.SteamAPI.Init();
                    if (Steamworks.SteamAPI.RestartAppIfNecessary((Steamworks.AppId_t)3367530))
                    {
                        Application.Exit();
                        Environment.Exit(0);
                        return;
                    }
                    Singleton.IsSteamworks = true;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Steamworks Exception: " + e.ToString());
                    Application.Exit();
                    Environment.Exit(0);
                    return;
                }
            }
#endif

#if DEBUG || RELEASE
            if (GetArgument("pathToAI") != null)
            {
                Singleton.PathToAI = GetArgument("pathToAI");
            }
            else
            {
#endif
                if (!File.Exists("OpenCAGE Settings.json"))
                {
                    if (Singleton.IsSteamworks && File.Exists(AppDomain.CurrentDomain.BaseDirectory + "/../Alien Isolation/AI.exe"))
                    {
                        Singleton.PathToAI = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory + "/../Alien Isolation/");
                        SettingsManager.SetString(Singleton.Settings.GameRoot, Singleton.PathToAI);
                    }
                    else if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "/AI.exe"))
                    {
                        Singleton.PathToAI = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
                        SettingsManager.SetString(Singleton.Settings.GameRoot, Singleton.PathToAI);
                    }
                    else
                    {
                        MessageBox.Show("Please locate your Alien: Isolation executable (AI.exe).", "OpenCAGE Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        using (OpenFileDialog dialog = new OpenFileDialog())
                        {
                            dialog.Filter = "Applications (*.exe)|AI.exe";
                            if (dialog.ShowDialog() == DialogResult.OK)
                            {
                                Singleton.PathToAI = Path.GetDirectoryName(dialog.FileName);
                                SettingsManager.SetString(Singleton.Settings.GameRoot, Singleton.PathToAI);
                            }
                            else
                            {
                                Application.Exit();
                                Environment.Exit(0);
                                return;
                            }
                        }
                    }
                }
                else
                {
                    Singleton.PathToAI = SettingsManager.GetString(Singleton.Settings.GameRoot);
                }
#if DEBUG
            }
#endif

#if SHIP_BUILD
            //If level viewer is disabled, clear it out
            if (!Singleton.IsSteamworks && !SettingsManager.GetBool(Singleton.Settings.LevelViewerEnabled))
            {
                string levelViewerPath = Singleton.PathToAI + "\\DATA\\MODTOOLS\\REMOTE_ASSETS\\levelviewer";
                if (Directory.Exists(levelViewerPath))
                {
                    try
                    {
                        Directory.Delete(levelViewerPath, true);
                    }
                    catch { }
                }
            }

            //If the user is using Steam, make sure REMOTE_ASSETS is up to date with our offline Assets folder
            if (Singleton.IsSteamworks)
            {
                string _gameAssetPath = Singleton.PathToAI + "\\DATA\\MODTOOLS\\REMOTE_ASSETS\\";
                string _offlineAssetPath = AppDomain.CurrentDomain.BaseDirectory + "\\Assets\\";

                if (!Directory.Exists(_offlineAssetPath) || !File.Exists(_offlineAssetPath + "assets.manifest"))
                {
                    MessageBox.Show("Please verify your Steam install, files are missing.", "File verification required", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                    Environment.Exit(0);
                    return;
                }

                if (!File.Exists(_gameAssetPath + "assets.manifest"))
                {
                    Directory.CreateDirectory(_gameAssetPath);

                    List<string> files = Directory.GetFiles(_offlineAssetPath, "*.archive", SearchOption.AllDirectories).ToList();
                    files.Add(_offlineAssetPath + "assets.manifest");
                    for (int i = 0; i < files.Count; i++)
                        File.Copy(files[i], _gameAssetPath + Path.GetFileName(files[i]));
                }
                else
                {
                    JObject offlineManifest = JObject.Parse(File.ReadAllText(_offlineAssetPath + "assets.manifest"));
                    JObject gameManifest = JObject.Parse(File.ReadAllText(_gameAssetPath + "assets.manifest"));
                    foreach (JObject offlineArchive in offlineManifest["archives"])
                    {
                        bool upToDate = false;
                        foreach (JObject gameArchive in gameManifest["archives"])
                        {
                            if (gameArchive["name"].Value<string>() != offlineArchive["name"].Value<string>())
                                continue;
                            if (!Directory.Exists(_gameAssetPath + offlineArchive["name"].Value<string>()))
                                continue;

                            if (gameArchive.ContainsKey("hash") && offlineArchive.ContainsKey("hash"))
                                upToDate = (gameArchive["hash"].Value<string>() == offlineArchive["hash"].Value<string>());
                            else
                                upToDate = (gameArchive["size"].Value<int>() == offlineArchive["size"].Value<int>());
                            break;
                        }
                        if (upToDate)
                            continue;

                        try
                        {
                            if (File.Exists(_gameAssetPath + offlineArchive["name"] + ".archive"))
                                File.Delete(_gameAssetPath + offlineArchive["name"] + ".archive");

                            File.Copy(_offlineAssetPath + offlineArchive["name"] + ".archive", _gameAssetPath + offlineArchive["name"] + ".archive");
                        }
                        catch { }
                    }
                    File.Copy(_offlineAssetPath + "assets.manifest", _gameAssetPath + "assets.manifest", true);
                }

                string[] archives = Directory.GetFiles(_gameAssetPath, "*.archive", SearchOption.TopDirectoryOnly);
                if (archives.Length != 0)
                {
                    List<Process> allProcesses = new List<Process>(Process.GetProcessesByName("CathodeEditorGodot"));
                    List<string> processNames = new List<string>(Directory.GetFiles(_gameAssetPath, "*.exe", SearchOption.AllDirectories));
                    for (int i = 0; i < processNames.Count; i++) allProcesses.AddRange(Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processNames[i])));
                    for (int i = 0; i < allProcesses.Count; i++) try { allProcesses[i].Kill(); } catch { }

                    foreach (string archive in archives)
                    {
                        //Try delete the base directory to clear out old assets (if it exists)
                        string directory = _gameAssetPath + "/" + Path.GetFileNameWithoutExtension(archive);
                        try { Directory.Delete(directory, true); } catch { }
                        try { Directory.Delete(directory, true); } catch { }

                        //Extract out the new assets
                        using (MemoryStream stream = new MemoryStream())
                        using (GZipStream gzipStream = new GZipStream(File.OpenRead(archive), CompressionMode.Decompress))
                        {
                            gzipStream.CopyTo(stream);
                            byte[] content = stream.ToArray();
                            using (BinaryReader reader = new BinaryReader(new MemoryStream(content)))
                            {
                                int file_count = reader.ReadInt32();
                                for (int i = 0; i < file_count; i++)
                                {
                                    string fileName = reader.ReadString();
                                    int fileLength = reader.ReadInt32();
                                    byte[] fileContent = reader.ReadBytes(fileLength);

                                    Directory.CreateDirectory((_gameAssetPath + fileName).Substring(0, (_gameAssetPath + fileName).Length - Path.GetFileName(_gameAssetPath + fileName).Length));
                                    if (File.Exists(_gameAssetPath + fileName)) File.Delete(_gameAssetPath + fileName);
                                    File.WriteAllBytes(_gameAssetPath + fileName, fileContent);
                                }
                            }
                        }
                        File.Delete(archive);
                    }
                }
            }
#endif

            Singleton.Platform = PatchManager.GetPlatform(Singleton.PathToAI);
            Singleton.Version = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;
#if SHIP_BUILD
            AnalyticsManager.LogAppStartup(Singleton.Version);
#endif

            //Check for update, and launch updater if one is available
#if SHIP_BUILD
            if (!Singleton.IsSteamworks && UpdateManager.IsUpdateAvailable(Singleton.Version))
            {
                MessageBox.Show("A new version of OpenCAGE is available!", "OpenCAGE Updater", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                UpdateManager.DoUpdate();
                return;
            }
            if (File.Exists("OpenCAGE Updater.exe"))
                File.Delete("OpenCAGE Updater.exe");
#endif

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
        static void HandleError(string error)
        {
            string logPath = Singleton.PathToAI + "/DATA/MODTOOLS/LOGS/CECrash_" + DateTime.Now.ToString("ddMMyy-HHmmss") + ".log";
            Directory.CreateDirectory(Singleton.PathToAI + "/DATA/MODTOOLS/LOGS");

            MessageBox.Show("A critical error occurred.\nPlease wait while a log is generated.", "OpenCAGE Error Handler", MessageBoxButtons.OK, MessageBoxIcon.Error);

            try
            {
                Task.Run(async () =>
                {
                    await UploadCrashLog(error, logPath);
                }).Wait();

                MessageBox.Show("Thanks, a log has been generated and auto-submitted.\nYou can find your logs within DATA/MODTOOLS/LOGS.", "OpenCAGE Error Handler", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch
            {
                MessageBox.Show("A log has been generated.\nYou can find it within DATA/MODTOOLS/LOGS, please submit it to GitHub!", "OpenCAGE Error Handler", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            
            Application.Exit();
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
                    version = "Standalone: " + Application.ProductVersion;
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
                error += "\n Current Level: " + level;
                content.Add(new StringContent(composite == null ? "Unknown/None" : composite.name), "current_composite");
                error += "\n Current Composite: " + composite.name;
                content.Add(new StringContent(entity == null ? "Unknown/None" : entity.shortGUID.ToByteString()), "current_entity");
                error += "\n Current Entity: " + entity.shortGUID.ToByteString();

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
