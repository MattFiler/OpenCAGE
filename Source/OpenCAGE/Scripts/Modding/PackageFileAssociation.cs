using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OpenCAGE.Modding
{
    /* Makes .opencage files double-clickable: a per-user (no admin) file association pointing at
     * this executable with -modpackage=<file>, refreshed every launch so it survives the install
     * moving. Program.cs routes the argument into the Mod Manager's import flow. */
    public static class PackageFileAssociation
    {
        private const string ProgId = "OpenCAGE.ModPackage";

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);
        private const int SHCNE_ASSOCCHANGED = 0x08000000;

        /// <summary>
        /// Register (or refresh) the association. Silent on failure - a locked-down registry must
        /// never break startup.
        /// </summary>
        public static void Register()
        {
            try
            {
                string exePath = Application.ExecutablePath;
                string command = "\"" + exePath + "\" \"-modpackage=%1\"";
                bool changed = false;

                using (RegistryKey extension = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ModToolkit.PackageExtension))
                    changed |= SetIfDifferent(extension, "", ProgId);

                using (RegistryKey progId = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ProgId))
                {
                    changed |= SetIfDifferent(progId, "", "OpenCAGE Mod Package");
                    using (RegistryKey icon = progId.CreateSubKey("DefaultIcon"))
                        changed |= SetIfDifferent(icon, "", "\"" + exePath + "\",0");
                    using (RegistryKey open = progId.CreateSubKey(@"shell\open\command"))
                        changed |= SetIfDifferent(open, "", command);
                }

                if (changed)
                    SHChangeNotify(SHCNE_ASSOCCHANGED, 0, IntPtr.Zero, IntPtr.Zero);
            }
            catch { }
        }

        private static bool SetIfDifferent(RegistryKey key, string name, string value)
        {
            if ((key.GetValue(name) as string) == value)
                return false;
            key.SetValue(name, value);
            return true;
        }
    }
}
