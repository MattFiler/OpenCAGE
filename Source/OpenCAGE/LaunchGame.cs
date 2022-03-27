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
        /* On init, if we are trying to launch to a map, skip GUI */
        public LaunchGame(string MapToLaunchTo = "")
        {
            if (MapToLaunchTo != "")
            {
                LaunchToMap(MapToLaunchTo);
                return;
            }

            InitializeComponent();

            enableCinematicTools.Checked = SettingsManager.GetBool("OPT_CinematicTools");
            enableCinematicTools.Enabled = SettingsManager.GetString("META_GameVersion") == "STEAM";

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
            if (!PatchManager.PatchLaunchMode(MapName) || !PatchManager.PatchFileIntegrityCheck())
                MessageBox.Show("Failed to set level loading values in AI.exe!\nIs the game already open?", "Failed to patch binary.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            PatchManager.UpdateLevelListInPackages();

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
            if (!PatchManager.PatchUIPerfFlag(enableUIPerf.Checked))
                MessageBox.Show("Failed to set cUI UI perf enabled.\nIs Alien: Isolation open?", "Couldn't write!", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
    }
}
