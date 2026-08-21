using System;
using System.Collections.Generic;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.Theming
{
    /// <summary>
    /// Owns the app's theme: what it currently is, applying it to windows as they appear, and switching
    /// it over live when the user changes the setting.
    ///
    /// The docking chrome and the forms inside it are kept on one palette deliberately - the DockPanel
    /// theme supplies both the tab strips and the renderer the menus and toolbars use, so the two halves
    /// of the window can't drift apart.
    /// </summary>
    public static class ThemeManager
    {
        private static readonly HashSet<Form> _themedForms = new HashSet<Form>();
        private static readonly List<DockPanel> _dockPanels = new List<DockPanel>();
        private static bool _initialised;

        private static ThemeBase _darkDockTheme;
        private static ThemeBase _lightDockTheme;

        /// <summary>Raised after the theme changes, for anything that has to recolour itself.</summary>
        public static event Action ThemeChanged;

        public static bool IsDark { get; private set; }

        /// <summary>
        /// The renderer menus, toolbars and status bars paint with. Comes from the docking theme so the
        /// command bars match the tab strips exactly rather than approximating them.
        /// </summary>
        public static ToolStripRenderer ToolStripRenderer { get; private set; }

        private static ThemeBase DarkDockTheme
        {
            get { return _darkDockTheme ?? (_darkDockTheme = new VS2015DarkTheme()); }
        }

        private static ThemeBase LightDockTheme
        {
            get { return _lightDockTheme ?? (_lightDockTheme = new VS2015BlueTheme()); }
        }

        public static ThemeBase DockTheme
        {
            get { return IsDark ? DarkDockTheme : LightDockTheme; }
        }

        /// <summary>
        /// Call once from Main, before any window exists. The process-wide dark mode opt-in only takes
        /// full effect for windows created after it, so this genuinely does need to be first.
        /// </summary>
        public static void Initialize()
        {
            if (_initialised)
                return;

            _initialised = true;
            IsDark = SettingsManager.GetBool(Settings.DarkMode);

            ThemeNative.SetAppDarkMode(IsDark);
            RebuildToolStripRenderer();

            //Nothing else tells us when a window opens, and the app has a hundred-odd of them that are
            //constructed directly rather than through a shared base. The message filter catches a window
            //on its first paint, which is early enough that it never shows light first; the idle sweep
            //stays as a backstop for anything that somehow gets past it.
            _paintFilter = new FirstPaintThemeFilter();
            Application.AddMessageFilter(_paintFilter);
            Application.Idle += OnApplicationIdle;
        }

        private static FirstPaintThemeFilter _paintFilter;

        /// <summary>
        /// Themes a window the first time it is asked to paint.
        ///
        /// Waiting for Application.Idle meant a panel had already been drawn light at least once, which
        /// is what made loading flash white. WM_PAINT arrives before the window puts anything on screen,
        /// so recolouring here lands in the same frame.
        /// </summary>
        private sealed class FirstPaintThemeFilter : IMessageFilter
        {
            private const int WM_PAINT = 0x000F;

            /// <summary>
            /// Handles whose owning window is confirmed themed, so the lookup is skipped from then on.
            /// Every control in the app paints through here, so this needs to stay cheap.
            /// </summary>
            private readonly HashSet<IntPtr> _settled = new HashSet<IntPtr>();

            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg != WM_PAINT || !IsDark)
                    return false;

                if (_settled.Contains(m.HWnd))
                    return false;

                Control control = Control.FromHandle(m.HWnd);
                if (control == null)
                    return false; //not one of ours; don't remember it, the handle may be reused

                //Must walk up to the window. A form whose client area is fully covered by child controls
                //may never receive a paint of its own, so only ever checking whether THIS handle is a
                //form meant such a window was never noticed here - and it stayed light until the idle
                //sweep eventually caught it, which is what made loading flash white.
                Form form = control.FindForm();
                if (form == null)
                    return false;

                if (!_themedForms.Contains(form))
                {
                    ApplyToForm(form);
                    return false;
                }

                _settled.Add(m.HWnd);

                //Never swallow the message - the window still has to paint
                return false;
            }

            /// <summary>
            /// Windows recycles handles, so a settled entry could later belong to something else.
            /// Clearing whenever a window closes bounds how long a stale entry can survive.
            /// </summary>
            public void Reset()
            {
                _settled.Clear();
            }
        }

        private static void RebuildToolStripRenderer()
        {
            if (!IsDark)
            {
                //Light mode has to be the app exactly as it was. The docking theme's renderer paints
                //command bars from its own palette, and in light mode that theme is VS2015 Blue - which
                //tinted every menu and tool strip blue even with dark mode switched off.
                ToolStripRenderer = null;
                ToolStripManager.RenderMode = ToolStripManagerRenderMode.Professional;
                return;
            }

            ToolStripRenderer = new ThemeToolStripRenderer(DockTheme.ColorPalette);
            ToolStripManager.Renderer = ToolStripRenderer;
        }

        private static void OnApplicationIdle(object sender, EventArgs e)
        {
            //Light mode is the app with no theming applied at all, so there is nothing to sweep for
            if (!IsDark)
                return;

            //A handful of set lookups per idle. Deliberately not short-circuited on the form count: one
            //window closing as another opens leaves the count unchanged and would skip the new one.
            SweepOpenForms();
        }

        private static void SweepOpenForms()
        {
            for (int i = 0; i < Application.OpenForms.Count; i++)
            {
                Form form = Application.OpenForms[i];
                if (form == null || form.IsDisposed || _themedForms.Contains(form))
                    continue;

                ApplyToForm(form);
            }
        }

        /// <summary>
        /// Theme a window now rather than waiting for the sweep. Worth calling from a form's constructor
        /// so it never appears in the wrong colours first.
        /// </summary>
        public static void ApplyToForm(Form form)
        {
            if (form == null || form.IsDisposed)
                return;

            //In light mode there is nothing to do and nothing to track: a window that was never themed
            //needs no restoring, and leaving it alone is what guarantees light mode is untouched
            if (!IsDark)
                return;

            if (_themedForms.Add(form))
                form.Disposed += OnFormDisposed;

            ThemeEngine.Apply(form, true);
        }

        private static void OnFormDisposed(object sender, EventArgs e)
        {
            Form form = sender as Form;
            if (form == null)
                return;

            form.Disposed -= OnFormDisposed;
            _themedForms.Remove(form);

            //A closing window frees handles that Windows will hand out again
            if (_paintFilter != null)
                _paintFilter.Reset();
        }

        /// <summary>
        /// True when a theme change couldn't reach the docking chrome because panels were already
        /// docked. Everything else switches live; only the dock chrome needs the app restarting.
        /// </summary>
        public static bool DockChromeNeedsRestart { get; private set; }

        /// <summary>
        /// Re-check whether any docking chrome is still on the old theme. Called after a host has torn
        /// its panels down and rebuilt them, which is what lets the chrome change without a restart.
        /// </summary>
        public static void RecheckDockChrome()
        {
            bool outstanding = false;
            for (int i = 0; i < _dockPanels.Count; i++)
            {
                DockPanel dockPanel = _dockPanels[i];
                if (dockPanel == null || dockPanel.IsDisposed)
                    continue;

                if (!ApplyToDockPanel(dockPanel))
                    outstanding = true;
            }

            DockChromeNeedsRestart = outstanding;
        }

        /// <summary>
        /// Register a DockPanel so its chrome follows the theme, now and on every change. Returns false
        /// if the panel already has content, which DockPanelSuite refuses to re-theme in place.
        /// </summary>
        public static bool ApplyToDockPanel(DockPanel dockPanel)
        {
            if (dockPanel == null)
                return true;

            if (!_dockPanels.Contains(dockPanel))
            {
                _dockPanels.Add(dockPanel);
                dockPanel.Disposed += OnDockPanelDisposed;
            }

            ThemeBase target = DockTheme;
            if (dockPanel.Theme != null && dockPanel.Theme.GetType() == target.GetType())
                return true;

            //Has to be checked before assigning, not caught afterwards. The setter tears the old theme
            //down and swaps the field over BEFORE the call that rejects a populated panel, so letting it
            //throw leaves the panel holding a theme whose factories never built any of its live panes -
            //and the next repaint of a splitter or caption dies on the mismatch.
            if (dockPanel.Panes.Count > 0 || dockPanel.FloatWindows.Count > 0 || dockPanel.Contents.Count > 0)
                return false;

            dockPanel.Theme = target;
            return true;
        }

        private static void OnDockPanelDisposed(object sender, EventArgs e)
        {
            DockPanel dockPanel = sender as DockPanel;
            if (dockPanel == null)
                return;

            dockPanel.Disposed -= OnDockPanelDisposed;
            _dockPanels.Remove(dockPanel);
        }

        /// <summary>Switch the whole app over, applying to everything already on screen.</summary>
        public static void SetDark(bool dark)
        {
            if (_initialised && IsDark == dark)
                return;

            IsDark = dark;
            SettingsManager.SetBool(Settings.DarkMode, dark);

            ThemeNative.SetAppDarkMode(dark);
            RebuildToolStripRenderer();

            DockChromeNeedsRestart = false;
            for (int i = 0; i < _dockPanels.Count; i++)
            {
                DockPanel dockPanel = _dockPanels[i];
                if (dockPanel == null || dockPanel.IsDisposed)
                    continue;

                if (!ApplyToDockPanel(dockPanel))
                    DockChromeNeedsRestart = true;
            }

            //Re-walk everything on screen. Forms hold their own colours, so nothing updates on its own.
            List<Form> open = new List<Form>();
            for (int i = 0; i < Application.OpenForms.Count; i++)
                open.Add(Application.OpenForms[i]);

            for (int i = 0; i < open.Count; i++)
            {
                if (open[i] == null || open[i].IsDisposed)
                    continue;

                ThemeEngine.Apply(open[i], dark);
                open[i].Invalidate(true);
            }

            Action handler = ThemeChanged;
            if (handler != null)
                handler();
        }
    }
}
