using Microsoft.Win32;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Makes OpenCAGE's package files double-clickable: per-user (no admin) file associations that
    /// point at this executable with -openfile=&lt;file&gt;, refreshed every launch of the primary
    /// instance so they follow the install wherever Steam puts it. Program.cs routes the argument
    /// into <see cref="PackageFiles"/>.
    /// </summary>
    /// <remarks>
    /// Steam cannot register file types for a tool, so the tool does it for itself, the way portable
    /// apps do: everything lives under HKEY_CURRENT_USER\Software\Classes, which needs no elevation
    /// and never touches another user's settings.
    /// </remarks>
    public static class FileAssociations
    {
        private const string CompositeProgId = "OpenCAGE.CompositePackage";
        private const string ModProgId = "OpenCAGE.ModPackage";

        //The extension the mod association used before it had a name of its own
        private const string LegacyModExtension = ".opencage";

        /// <summary>The argument a shell launch carries the file in: -openfile=&lt;path&gt;.</summary>
        public const string OpenFileArgument = "openfile";

        /// <summary>Launch flag that leaves the registry alone - for builds run from a scratch folder.</summary>
        public const string SkipFlag = "-no_file_associations";
        /// <summary>Launch flag that registers from a build that would not otherwise (a debug build).</summary>
        public const string OptInFlag = "-register_file_associations";

        /// <summary>
        /// Whether this launch should touch the registry. A shipped build always does, unless told not
        /// to; any other build only when asked, since a debug run from a build folder would otherwise
        /// point the association at itself and the next Steam launch would point it back.
        /// </summary>
        public static bool ShouldRegister()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            if (arguments.Any(o => string.Equals(o, SkipFlag, StringComparison.OrdinalIgnoreCase)))
                return false;
#if SHIP_BUILD
            return true;
#else
            return arguments.Any(o => string.Equals(o, OptInFlag, StringComparison.OrdinalIgnoreCase));
#endif
        }

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);
        private const int SHCNE_ASSOCCHANGED = 0x08000000;

        /// <summary>
        /// Register (or refresh) every association. Silent on failure - a locked-down registry must
        /// never break startup.
        /// </summary>
        public static void Register()
        {
            try
            {
                string exePath = Application.ExecutablePath;
                string command = "\"" + exePath + "\" \"-" + OpenFileArgument + "=%1\"";
                bool changed = false;

                changed |= RegisterExtension(PackageFiles.CompositeExtension, CompositeProgId, "OpenCAGE Composite Package", exePath, command);
#if ENABLE_MOD_PACKAGES
                changed |= RegisterExtension(PackageFiles.ModExtension, ModProgId, "OpenCAGE Mod Package", exePath, command);
#endif
                changed |= RemoveLegacyExtension(LegacyModExtension, ModProgId);

                if (changed)
                    SHChangeNotify(SHCNE_ASSOCCHANGED, 0, IntPtr.Zero, IntPtr.Zero);
            }
            catch { }
        }

        private static bool RegisterExtension(string extension, string progId, string friendlyName, string exePath, string command)
        {
            bool changed = false;
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + extension))
                changed |= SetIfDifferent(key, "", progId);

            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + progId))
            {
                changed |= SetIfDifferent(key, "", friendlyName);
                using (RegistryKey icon = key.CreateSubKey("DefaultIcon"))
                    changed |= SetIfDifferent(icon, "", "\"" + exePath + "\",0");
                using (RegistryKey open = key.CreateSubKey(@"shell\open\command"))
                    changed |= SetIfDifferent(open, "", command);
            }
            return changed;
        }

        /* An extension we used to claim, dropped only if it still points at us */
        private static bool RemoveLegacyExtension(string extension, string progId)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + extension))
            {
                if (key == null || (key.GetValue("") as string) != progId)
                    return false;
            }
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\" + extension, false);
            return true;
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
