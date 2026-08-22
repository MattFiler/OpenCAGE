using Microsoft.Win32;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OpenCAGE.Theming
{
    /// <summary>
    /// The Windows half of dark mode.
    ///
    /// Windows can draw its own controls dark - scrollbars, combo box drop-downs, edit borders, tree
    /// expanders, tooltips - but only for a process that has opted in, and the opt-in lives on
    /// undocumented uxtheme exports that are reachable by ordinal only. Without it, SetWindowTheme with
    /// "DarkMode_Explorer" or "DarkMode_CFD" silently does nothing, which is why colouring controls by
    /// hand still left light scrollbars and light drop-downs everywhere.
    ///
    /// Everything here is best-effort: an ordinal that isn't there (or a Windows too old to have it)
    /// just means we fall back to painting what we can ourselves.
    /// </summary>
    internal static class ThemeNative
    {
        //The ordinals moved once: 1809 exposed AllowDarkModeForApp(bool), 1903 replaced it in the same
        //slot with SetPreferredAppMode(int). Same ordinal, different signature - hence the build check.
        private const int BuildDarkModeSupported = 17763; //1809
        private const int BuildPreferredAppMode = 18334; //19H1

        private enum PreferredAppMode
        {
            Default = 0,
            AllowDark = 1,
            ForceDark = 2,
            ForceLight = 3,
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int SetPreferredAppModeDelegate(PreferredAppMode mode);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool AllowDarkModeForAppDelegate(bool allow);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool AllowDarkModeForWindowDelegate(IntPtr window, bool allow);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void FlushMenuThemesDelegate();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void RefreshImmersiveColorPolicyStateDelegate();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, IntPtr ordinal);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr window, string subAppName, string subIdList);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        //20 from Windows 11 and late Windows 10; 19 on the 20H1-era builds that shipped it as an experiment
        private const int DwmUseImmersiveDarkMode = 20;
        private const int DwmUseImmersiveDarkModeLegacy = 19;

        private static SetPreferredAppModeDelegate _setPreferredAppMode;
        private static AllowDarkModeForAppDelegate _allowDarkModeForApp;
        private static AllowDarkModeForWindowDelegate _allowDarkModeForWindow;
        private static FlushMenuThemesDelegate _flushMenuThemes;
        private static RefreshImmersiveColorPolicyStateDelegate _refreshImmersiveColorPolicyState;
        private static bool _resolved;
        private static int _buildNumber = -1;

        /// <summary>True when this Windows is new enough for the app-wide dark mode opt-in.</summary>
        public static bool IsSupported
        {
            get { return BuildNumber >= BuildDarkModeSupported; }
        }

        private static int BuildNumber
        {
            get
            {
                if (_buildNumber >= 0)
                    return _buildNumber;

                //Environment.OSVersion lies about anything past 6.2 without a compatibility manifest
                _buildNumber = 0;
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                    {
                        if (key != null)
                        {
                            int parsed;
                            if (int.TryParse(key.GetValue("CurrentBuildNumber") as string, out parsed))
                                _buildNumber = parsed;
                        }
                    }
                }
                catch { }

                return _buildNumber;
            }
        }

        private static void Resolve()
        {
            if (_resolved)
                return;

            _resolved = true;
            if (!IsSupported)
                return;

            try
            {
                IntPtr uxtheme = LoadLibrary("uxtheme.dll");
                if (uxtheme == IntPtr.Zero)
                    return;

                if (BuildNumber >= BuildPreferredAppMode)
                    _setPreferredAppMode = GetDelegate<SetPreferredAppModeDelegate>(uxtheme, 135);
                else
                    _allowDarkModeForApp = GetDelegate<AllowDarkModeForAppDelegate>(uxtheme, 135);

                _allowDarkModeForWindow = GetDelegate<AllowDarkModeForWindowDelegate>(uxtheme, 133);
                _flushMenuThemes = GetDelegate<FlushMenuThemesDelegate>(uxtheme, 136);
                _refreshImmersiveColorPolicyState = GetDelegate<RefreshImmersiveColorPolicyStateDelegate>(uxtheme, 104);
            }
            catch { }
        }

        private static T GetDelegate<T>(IntPtr module, int ordinal) where T : class
        {
            IntPtr address = GetProcAddress(module, new IntPtr(ordinal));
            if (address == IntPtr.Zero)
                return null;

            return Marshal.GetDelegateForFunctionPointer(address, typeof(T)) as T;
        }

        /// <summary>
        /// Put the process into (or out of) dark mode. Best called before any window exists; calling it
        /// again later works, but windows already up need their controls re-themed to notice.
        /// </summary>
        public static void SetAppDarkMode(bool dark)
        {
            Resolve();

            try
            {
                if (_setPreferredAppMode != null)
                    _setPreferredAppMode(dark ? PreferredAppMode.ForceDark : PreferredAppMode.ForceLight);
                else if (_allowDarkModeForApp != null)
                    _allowDarkModeForApp(dark);

                if (_refreshImmersiveColorPolicyState != null)
                    _refreshImmersiveColorPolicyState();
                if (_flushMenuThemes != null)
                    _flushMenuThemes();
            }
            catch { }
        }

        /// <summary>Opt a single window in, so its non-client bits and children can theme dark.</summary>
        public static void AllowDarkModeForWindow(IntPtr handle, bool dark)
        {
            if (handle == IntPtr.Zero)
                return;

            Resolve();
            try
            {
                if (_allowDarkModeForWindow != null)
                    _allowDarkModeForWindow(handle, dark);
            }
            catch { }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr window, int msg, IntPtr wParam, IntPtr lParam);

        //A progress bar takes its trough and bar colours from these, and from nowhere else. WinForms'
        //BackColor and ForeColor are hidden on ProgressBar precisely because they do nothing.
        private const int PBM_SETBARCOLOR = 0x0400 + 9;
        private const int PBM_SETBKCOLOR = 0x0400 + 14;

        /// <summary>
        /// Colour a progress bar's trough and bar. Only has any effect once the visual style has been
        /// removed with <see cref="ThemeClass.None"/> - a themed progress bar ignores both messages.
        /// </summary>
        public static void SetProgressBarColours(Control control, Color trough, Color bar)
        {
            if (control == null || !control.IsHandleCreated)
                return;

            try
            {
                SendMessage(control.Handle, PBM_SETBKCOLOR, IntPtr.Zero, ToCref(trough));
                SendMessage(control.Handle, PBM_SETBARCOLOR, IntPtr.Zero, ToCref(bar));
                control.Invalidate();
            }
            catch { }
        }

        private static IntPtr ToCref(Color colour)
        {
            return (IntPtr)(uint)ColorTranslator.ToWin32(colour);
        }

        /// <summary>Paint a top-level window's title bar and border dark.</summary>
        public static void SetTitleBarDarkMode(IntPtr handle, bool dark)
        {
            if (handle == IntPtr.Zero)
                return;

            int value = dark ? 1 : 0;
            try
            {
                if (DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref value, sizeof(int)) != 0)
                    DwmSetWindowAttribute(handle, DwmUseImmersiveDarkModeLegacy, ref value, sizeof(int));

                /* Setting the attribute doesn't repaint what's already on screen - DWM picks the new
                 * colour up the next time the frame changes. Switching a window to dark usually
                 * coincides with enough repainting to hide that, but switching back doesn't, and the
                 * title bar sits there dark until the window is moved or resized. Ask for the frame
                 * explicitly so it changes when it's told to. */
                SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
                RedrawWindow(handle, IntPtr.Zero, IntPtr.Zero, RDW_FRAME | RDW_INVALIDATE | RDW_UPDATENOW);
            }
            catch { }
        }

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;

        private const uint RDW_INVALIDATE = 0x0001;
        private const uint RDW_UPDATENOW = 0x0100;
        private const uint RDW_FRAME = 0x0400;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        private static extern bool RedrawWindow(IntPtr hWnd, IntPtr update, IntPtr region, uint flags);

        public enum ThemeClass
        {
            /// <summary>Lists, trees, scrollbars - the shell's own explorer look.</summary>
            Explorer,

            /// <summary>List views drawn as items rather than a report.</summary>
            ItemsView,

            /// <summary>Text boxes and combo boxes.</summary>
            Edit,

            /// <summary>No visual style at all, for controls we paint ourselves.</summary>
            None,
        }

        /// <summary>
        /// Hand a control over to one of the shell's themed control classes. This is the call that gets
        /// dark scrollbars, drop-downs and expanders, and it only does anything once the process is in
        /// dark mode - which is exactly what the old implementation was missing.
        /// </summary>
        public static void SetControlTheme(Control control, ThemeClass themeClass, bool dark)
        {
            if (control == null || !control.IsHandleCreated)
                return;

            string name;
            switch (themeClass)
            {
                case ThemeClass.ItemsView:
                    name = dark ? "DarkMode_ItemsView" : "ItemsView";
                    break;
                case ThemeClass.Edit:
                    //"CFD" is the common file dialog's control set - the one place Windows themes an
                    //edit box and a combo box dark, borders included
                    name = dark ? "DarkMode_CFD" : "CFD";
                    break;
                case ThemeClass.None:
                    //Strips the visual style entirely, so our own colours actually show through.
                    //Both arguments have to be " " - passing null as the id list leaves the theme in
                    //place, which is why the progress bar stayed light.
                    try
                    {
                        SetWindowTheme(control.Handle, dark ? " " : null, dark ? " " : null);
                    }
                    catch { }
                    return;
                default:
                    name = dark ? "DarkMode_Explorer" : "Explorer";
                    break;
            }

            try
            {
                AllowDarkModeForWindow(control.Handle, dark);
                SetWindowTheme(control.Handle, name, null);
            }
            catch { }
        }
    }
}
