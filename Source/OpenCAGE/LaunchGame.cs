using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE
{
    public partial class LaunchGame : Form
    {
        private GameBuild gameVersion;

        /* On init, if we are trying to launch to a map, skip GUI */
        public LaunchGame(string MapToLaunchTo = "")
        {
            if (MapToLaunchTo != "")
            {
                LaunchToMap(MapToLaunchTo);
                return;
            }

            Enum.TryParse(SettingsManager.GetString("META_GameVersion"), out GameBuild gameVersion);

            InitializeComponent();

            enableCinematicTools.Checked = SettingsManager.GetBool("OPT_CinematicTools");
            enableCinematicTools.Enabled = gameVersion == GameBuild.STEAM;

            enableUIPerf.Checked = SettingsManager.GetBool("OPT_cUIEnabled_UIPerf");

            UIMOD_DebugCheckpoints.Checked = SettingsManager.GetBool("UIOPT_PAUSEMENU");
            UIMOD_MapName.Checked = SettingsManager.GetBool("UIOPT_LOADINGSCREEN");
            UIMOD_MapSelection.Checked = SettingsManager.GetBool("UIOPT_NEWFRONTENDMENU");
            UIMOD_ReturnFrontend.Checked = SettingsManager.GetBool("UIOPT_GAMEOVERMENU");

            if (SettingsManager.GetString("OPT_LoadToMap") == "") SettingsManager.SetString("OPT_LoadToMap", "Frontend");
            MapToLoad.Controls.OfType<RadioButton>().FirstOrDefault(r => r.Text == SettingsManager.GetString("OPT_LoadToMap")).Checked = true;
        }

        /* Load game with given map name */
        private void LaunchToMap(string MapName)
        {
            bool shouldPatch = true;

            //This is the level the benchmark function loads into - we can overwrite it to change
            byte[] mapStringByteArray = { 0x54, 0x45, 0x43, 0x48, 0x5F, 0x52, 0x4E, 0x44, 0x5F, 0x48, 0x5A, 0x44, 0x4C, 0x41, 0x42, 0x00, 0x00, 0x65, 0x6E, 0x67, 0x69, 0x6E, 0x65, 0x5F, 0x73, 0x65, 0x74, 0x74, 0x69, 0x6E, 0x67, 0x73 };

            //These are the original/edited setters in the benchmark function to enable benchmark mode - if we're just loading a level, we want to change them
            List<PatchBytes> benchmarkPatches = new List<PatchBytes>();
            switch (gameVersion)
            {
                case GameBuild.STEAM:
                    benchmarkPatches.Add(new PatchBytes(3842041, new byte[] { 0xe3, 0x48, 0x26 }, new byte[] { 0x13, 0x3c, 0x28 }));
                    benchmarkPatches.Add(new PatchBytes(3842068, new byte[] { 0xce, 0x0c, 0x6f }, new byte[] { 0x26, 0x0f, 0x64 }));
                    benchmarkPatches.Add(new PatchBytes(3842146, new byte[] { 0xcb, 0x0c, 0x6f }, new byte[] { 0x26, 0x0f, 0x64 }));
                    break;
                case GameBuild.EPIC_GAMES_STORE:
                    benchmarkPatches.Add(new PatchBytes(3911321, new byte[] { 0x13, 0x5f, 0x1a }, new byte[] { 0x23, 0x43, 0x1c }));
                    benchmarkPatches.Add(new PatchBytes(3911348, new byte[] { 0xee, 0xd1, 0x70 }, new byte[] { 0xe6, 0xce, 0x65 }));
                    benchmarkPatches.Add(new PatchBytes(3911426, new byte[] { 0xeb, 0xd1, 0x70 }, new byte[] { 0xe6, 0xce, 0x65 }));
                    break;
            }

            //Frontend acts as a reset
            if (MapName == "Frontend")
            {
                MapName = "Tech_RnD_HzdLab";
                shouldPatch = false;
            }

            //Update vanilla byte array with selection
            for (int i = 0; i < MapName.Length; i++)
            {
                mapStringByteArray[i] = (byte)MapName[i];
            }
            mapStringByteArray[MapName.Length] = 0x00;

            //Edit game EXE with selected option & hack out the benchmark mode
            try
            {
                BinaryWriter writer = new BinaryWriter(File.OpenWrite(SettingsManager.GetString("PATH_GameRoot") + "/AI.exe"));
                for (int i = 0; i < benchmarkPatches.Count; i++)
                {
                    writer.BaseStream.Position = benchmarkPatches[i].offset;
                    if (shouldPatch) writer.Write(benchmarkPatches[i].patched);
                    else writer.Write(benchmarkPatches[i].original);
                }
                switch (gameVersion)
                {
                    case GameBuild.STEAM:
                        writer.BaseStream.Position = 15676275;
                        break;
                    case GameBuild.EPIC_GAMES_STORE:
                        writer.BaseStream.Position = 15773411;
                        break;
                }
                writer.Write(mapStringByteArray);
                writer.Close();
            }
            catch (Exception e)
            {
                MessageBox.Show("Failed to set level loading values in AI.exe!\nIs the game already open?", "Failed to patch binary.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            //Start game process 
            ProcessStartInfo alienProcess = new ProcessStartInfo();
            alienProcess.WorkingDirectory = SettingsManager.GetString("PATH_GameRoot");
            alienProcess.FileName = SettingsManager.GetString("PATH_GameRoot") + "/AI.exe";
            Process.Start(alienProcess);
        }

        /* Load game from GUI map selection */
        Task cinematicToolInjectTask = null;
        private void LaunchGame_Click(object sender, EventArgs e)
        {
            //Work out what option was selected and launch to it
            string selectedMap = MapToLoad.Controls.OfType<RadioButton>().FirstOrDefault(r => r.Checked).Text;
            SettingsManager.SetString("OPT_LoadToMap", selectedMap);
            LaunchToMap(selectedMap);

            //Enable Cinematic Tools if requested
            if (SettingsManager.GetBool("OPT_CinematicTools"))
            {
                if (cinematicToolInjectTask != null) cinematicToolInjectTask.Dispose();
                cinematicToolInjectTask = Task.Factory.StartNew(() => InjectCinematicTools());
            }
            this.Close();
        }

        /* Show/hide appropriate GUI options on load */
        private void Landing_OpenGame_Load(object sender, EventArgs e)
        {
            /* -- Enable/Disable options based on DLC ownership -- */

            //LAST SURVIVOR
            EnableOptionIfHasDLC(radioButton30);

            //CREW EXPENDABLE
            EnableOptionIfHasDLC(radioButton29);

            //THE TRIGGER
            EnableOptionIfHasDLC(radioButton28);
            EnableOptionIfHasDLC(radioButton1);
            EnableOptionIfHasDLC(radioButton40);

            //CORPORATE LOCKDOWN
            EnableOptionIfHasDLC(radioButton26);
            EnableOptionIfHasDLC(radioButton25);
            EnableOptionIfHasDLC(radioButton22);

            //TRAUMA
            EnableOptionIfHasDLC(radioButton27);
            EnableOptionIfHasDLC(radioButton24);
            EnableOptionIfHasDLC(radioButton23);

            //SAFE HAVEN
            EnableOptionIfHasDLC(radioButton38);

            //LOST CONTACT
            EnableOptionIfHasDLC(radioButton37);
        }

        /* Enable/disable GUI inputs based on DLC ownership */
        private void EnableOptionIfHasDLC(RadioButton UiOption)
        {
            UiOption.Enabled = File.Exists(SettingsManager.GetString("PATH_GameRoot") + "/DATA/ENV/PRODUCTION/" + UiOption.Text + "/WORLD/COMMANDS.PAK");
        }

        /* Enable/disable the Cinematic Tools */
        private void enableCinematicTools_CheckedChanged(object sender, EventArgs e)
        {
            SettingsManager.SetBool("OPT_CinematicTools", enableCinematicTools.Checked);
        }

        /* Enable/disable cUI rendering for UI perf stats (Cathode debug render) */ 
        private void enableUIPerf_CheckedChanged(object sender, EventArgs e)
        {
            SettingsManager.SetBool("OPT_cUIEnabled_UIPerf", enableUIPerf.Checked);
            try
            {
                BinaryWriter writer = new BinaryWriter(File.OpenWrite(SettingsManager.GetString("PATH_GameRoot") + "/AI.exe"));
                switch (gameVersion)
                {
                    case GameBuild.STEAM:
                        writer.BaseStream.Position = 4430526;
                        break;
                    case GameBuild.EPIC_GAMES_STORE:
                        writer.BaseStream.Position = 4500590;
                        break;
                }
                writer.Write((enableUIPerf.Checked) ? (byte)0x01 : (byte)0x00);
                writer.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to set cUI UI perf enabled.\nIs Alien: Isolation open?", "Couldn't write!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /* UI Modifications */
        CathodeLib.PAK AlienPAK = new CathodeLib.PAK();
        private void UIMOD_DebugCheckpoints_CheckedChanged(object sender, EventArgs e)
        {
            UpdateUI("PAUSEMENU", UIMOD_DebugCheckpoints.Checked);
        }
        private void UIMOD_MapName_CheckedChanged(object sender, EventArgs e)
        {
            UpdateUI("LOADINGSCREEN", UIMOD_MapName.Checked);
        }
        private void UIMOD_MapSelection_CheckedChanged(object sender, EventArgs e)
        {
            UpdateUI("NEWFRONTENDMENU", UIMOD_MapSelection.Checked);
        }
        private void UIMOD_ReturnFrontend_CheckedChanged(object sender, EventArgs e)
        {
            UpdateUI("GAMEOVERMENU", UIMOD_ReturnFrontend.Checked);
        }
        private void UpdateUI(string file, bool modded)
        {
            if (AlienPAK.Format != CATHODE.PAKType.PAK2) AlienPAK.Open(SettingsManager.GetString("PATH_GameRoot") + "/DATA/UI.PAK");

            FileStream stream = File.Create(file);
            GetResourceStream((modded) ? "UI_Mods/" + file + "_MOD.GFX" : "UI_Mods/" + file + ".GFX").CopyTo(stream);
            stream.Close();

            AlienPAK.ImportFile("DATA/UI/" + file + ".GFX", file);
            File.Delete(file);

            SettingsManager.SetBool("UIOPT_" + file, modded);
        }
        protected static Stream GetResourceStream(string resourcePath)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            List<string> resourceNames = new List<string>(assembly.GetManifestResourceNames());

            resourcePath = resourcePath.Replace(@"/", ".");
            resourcePath = resourceNames.FirstOrDefault(r => r.Contains(resourcePath));

            if (resourcePath == null)
                throw new FileNotFoundException("Resource not found");

            return assembly.GetManifestResourceStream(resourcePath);
        }

        /* Inject the cinematic tools */
        private bool InjectCinematicTools()
        {
            Process[] processes = null;
            while (processes == null || processes.Length == 0)
            {
                Thread.Sleep(2500);
                processes = Process.GetProcessesByName("AI");
            }

            try
            {
                Thread.Sleep(2500);
                Process alienProcess = processes[0];
                string DllPath = SettingsManager.GetString("PATH_GameRoot") + "/DATA/MODTOOLS/REMOTE_ASSETS/cinematictools/CT_AlienIsolation.dll";
                IntPtr Size = (IntPtr)DllPath.Length;
                IntPtr DllSpace = VirtualAllocEx(alienProcess.Handle, IntPtr.Zero, Size, AllocationType.Reserve | AllocationType.Commit, MemoryProtection.ExecuteReadWrite);
                byte[] bytes = System.Text.Encoding.ASCII.GetBytes(DllPath);
                bool DllWrite = WriteProcessMemory(alienProcess.Handle, DllSpace, bytes, (int)bytes.Length, out var bytesread);
                IntPtr Kernel32Handle = GetModuleHandle("Kernel32.dll");
                IntPtr LoadLibraryAAddress = GetProcAddress(Kernel32Handle, "LoadLibraryA");
                Thread.Sleep(1000);
                IntPtr RemoteThreadHandle = CreateRemoteThread(alienProcess.Handle, IntPtr.Zero, 0, LoadLibraryAAddress, DllSpace, 0, IntPtr.Zero);
                Thread.Sleep(1000);
                bool FreeDllSpace = VirtualFreeEx(alienProcess.Handle, DllSpace, 0, AllocationType.Release);
                Thread.Sleep(1000);
                CloseHandle(RemoteThreadHandle);
                CloseHandle(alienProcess.Handle);
                return true;
            }
            catch (Exception e)
            {
                return false;
            }

            cinematicToolInjectTask.Dispose();
        }

        //Everything below is thanks to: https://github.com/ihack4falafel/DLL-Injection/blob/master/DllInjection/DllInjection/Program.cs

        // OpenProcess signture https://www.pinvoke.net/default.aspx/kernel32.openprocess
        [Flags]
        public enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VirtualMemoryOperation = 0x00000008,
            VirtualMemoryRead = 0x00000010,
            VirtualMemoryWrite = 0x00000020,
            DuplicateHandle = 0x00000040,
            CreateProcess = 0x000000080,
            SetQuota = 0x00000100,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            QueryLimitedInformation = 0x00001000,
            Synchronize = 0x00100000
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(
        ProcessAccessFlags processAccess,
        bool bInheritHandle,
        int processId);
        public static IntPtr OpenProcess(Process proc, ProcessAccessFlags flags)
        {
            return OpenProcess(flags, false, proc.Id);
        }

        // VirtualAllocEx signture https://www.pinvoke.net/default.aspx/kernel32.virtualallocex
        [Flags]
        public enum AllocationType
        {
            Commit = 0x1000,
            Reserve = 0x2000,
            Decommit = 0x4000,
            Release = 0x8000,
            Reset = 0x80000,
            Physical = 0x400000,
            TopDown = 0x100000,
            WriteWatch = 0x200000,
            LargePages = 0x20000000
        }

        // VirtualFreeEx signture  https://www.pinvoke.net/default.aspx/kernel32.virtualfreeex
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress,
        int dwSize, AllocationType dwFreeType);

        [Flags]
        public enum MemoryProtection
        {
            Execute = 0x10,
            ExecuteRead = 0x20,
            ExecuteReadWrite = 0x40,
            ExecuteWriteCopy = 0x80,
            NoAccess = 0x01,
            ReadOnly = 0x02,
            ReadWrite = 0x04,
            WriteCopy = 0x08,
            GuardModifierflag = 0x100,
            NoCacheModifierflag = 0x200,
            WriteCombineModifierflag = 0x400
        }

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern IntPtr VirtualAllocEx(
            IntPtr hProcess,
            IntPtr lpAddress,
            IntPtr dwSize,
            AllocationType flAllocationType,
            MemoryProtection flProtect);

        // WriteProcessMemory signture https://www.pinvoke.net/default.aspx/kernel32/WriteProcessMemory.html
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        [MarshalAs(UnmanagedType.AsAny)] object lpBuffer,
        int dwSize,
        out IntPtr lpNumberOfBytesWritten);

        // GetProcAddress signture https://www.pinvoke.net/default.aspx/kernel32.getprocaddress
        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        // GetModuleHandle signture http://pinvoke.net/default.aspx/kernel32.GetModuleHandle
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        // CreateRemoteThread signture https://www.pinvoke.net/default.aspx/kernel32.createremotethread
        [DllImport("kernel32.dll")]
        static extern IntPtr CreateRemoteThread(
        IntPtr hProcess,
        IntPtr lpThreadAttributes,
        uint dwStackSize,
        IntPtr lpStartAddress,
        IntPtr lpParameter,
        uint dwCreationFlags,
        IntPtr lpThreadId);

        // CloseHandle signture https://www.pinvoke.net/default.aspx/kernel32.closehandle
        [DllImport("kernel32.dll", SetLastError = true)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [SuppressUnmanagedCodeSecurity]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);

        struct PatchBytes
        {
            public PatchBytes(int _o, byte[] _orig, byte[] _patch)
            {
                offset = _o;
                original = _orig;
                patched = _patch;
            }
            public int offset;
            public byte[] original;
            public byte[] patched;
        }
    }
}
