using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace OpenCAGE.Theming
{
    /// <summary>
    /// Walks a control tree and gives every control the treatment its type actually needs.
    ///
    /// The previous attempt set the same background and foreground on everything through reflection and
    /// then patched up a few types afterwards, which is why it read as one flat grey sheet: a text box
    /// looked like the panel behind it, and a button looked like both. Here each control is assigned a
    /// role - container, input, command - and takes the matching tier from <see cref="ThemeColours"/>,
    /// so the depth that makes a UI legible survives.
    ///
    /// Every value overwritten is recorded first, so switching back to light restores what the designer
    /// set rather than guessing at it. A control never themed at all is left completely untouched, which
    /// is what keeps light mode identical to having no theming in the app.
    /// </summary>
    internal static class ThemeEngine
    {
        private sealed class OriginalState
        {
            public Color BackColor;
            public Color ForeColor;

            /// <summary>
            /// Whether those colours were the control's own, or ones it was reading off its parent.
            /// A control that had none of its own has to be put back to having none of its own - see
            /// <see cref="HasOwn"/> for why assigning the remembered value instead is wrong.
            /// </summary>
            public bool OwnBackColor;
            public bool OwnForeColor;

            /// <summary>
            /// What we last painted on. If the control's colour has moved away from this since, the code
            /// that owns it meant it - a colour swatch, a status highlight - and we leave it alone.
            /// </summary>
            public Color? LastAppliedBack;

            /// <summary>
            /// A strip's own render mode. Not every strip is on the manager's - the status bar is
            /// deliberately on the system renderer - so putting them all back to the manager loses
            /// that, and the status bar comes back painted by something else than it started on.
            /// </summary>
            public ToolStripRenderMode? RenderMode;

            public FlatStyle? FlatStyle;
            public Color? FlatBorderColor;
            public Color? FlatMouseOverBackColor;
            public Color? FlatMouseDownBackColor;
            public bool? UseVisualStyleBackColor;
            public BorderStyle? BorderStyle;
        }

        private static readonly Dictionary<Control, OriginalState> _originals = new Dictionary<Control, OriginalState>();
        private static readonly HashSet<Control> _hooked = new HashSet<Control>();

        /// <summary>
        /// Controls that own their appearance. The docking chrome is painted by DockPanelSuite's own
        /// theme and the flowgraph canvas has its own configurable palette, so colouring either would
        /// fight the thing that already handles it. Their children are still walked - the panels docked
        /// inside a DockPanel are ordinary forms and do want theming.
        /// </summary>
        private static bool IsExcluded(Control control)
        {
            if (control == null)
                return true;

            //The control's OWN declaring namespace, deliberately not its base chain. Every docked panel
            //in this app derives from DockContent, which lives in the docking library's namespace - so
            //walking the base chain excluded the entire application from theming, leaving each panel's
            //background at the system default while its children themed normally. A control DEFINED by
            //one of these libraries is chrome; a control merely descended from one is ours.
            string ns = control.GetType().Namespace;
            if (string.IsNullOrEmpty(ns))
                return false;

            return ns.StartsWith("WeifenLuo.WinFormsUI", StringComparison.Ordinal)
                || ns.StartsWith("ST.Library.UI", StringComparison.Ordinal);
        }

        public static void Apply(Control control, bool dark)
        {
            if (control == null || control.IsDisposed)
                return;

            //Isolated per control, and deliberately so. A single control that objects to being themed
            //used to abort the whole recursion, which left a form with its own background recoloured and
            //every child below the failure still in light colours - a dark window full of white boxes.
            //One control looking wrong is a blemish; a whole window looking half-done is the bug.
            if (!IsExcluded(control))
            {
                try
                {
                    if (dark)
                        ApplyDark(control);
                    else
                        Restore(control);
                }
                catch (Exception ex)
                {
                    Debug.Log("Theme", "Failed to theme " + Describe(control) + ": " + ex);
                }
            }

            try
            {
                Hook(control);
            }
            catch (Exception ex)
            {
                Debug.Log("Theme", "Failed to hook " + Describe(control) + ": " + ex);
            }

            for (int i = 0; i < control.Controls.Count; i++)
                Apply(control.Controls[i], dark);

            if (control.ContextMenuStrip != null)
                Apply(control.ContextMenuStrip, dark);
        }

        private static string Describe(Control control)
        {
            if (control == null)
                return "<null>";

            string name = string.IsNullOrEmpty(control.Name) ? "<unnamed>" : control.Name;
            return control.GetType().Name + " '" + name + "'";
        }

        private static void Hook(Control control)
        {
            if (!_hooked.Add(control))
                return;

            control.ControlAdded += OnControlAdded;
            control.HandleCreated += OnHandleCreated;
            control.Disposed += OnControlDisposed;
        }

        private static void OnControlAdded(object sender, ControlEventArgs e)
        {
            //Controls built after the theme was applied still need it
            Apply(e.Control, ThemeManager.IsDark);
        }

        private static void OnHandleCreated(object sender, EventArgs e)
        {
            //Half of dark mode is native and needs a window handle, but forms are usually themed from
            //their constructor - before the handle exists - so this is where most of it actually lands.
            Control control = sender as Control;
            if (control == null || control.IsDisposed || !ThemeManager.IsDark || IsExcluded(control))
                return;

            try
            {
                ApplyDark(control);
            }
            catch (Exception ex)
            {
                //This runs mid handle-creation for every control in the app. Theming is cosmetic, so a
                //control that can't take it should look wrong, not bring the editor down with it.
                Debug.Log("Theme", "Failed to theme " + control.GetType().Name + ": " + ex.Message);
            }
        }

        private static void OnControlDisposed(object sender, EventArgs e)
        {
            Control control = sender as Control;
            if (control == null)
                return;

            control.ControlAdded -= OnControlAdded;
            control.HandleCreated -= OnHandleCreated;
            control.Disposed -= OnControlDisposed;
            _hooked.Remove(control);
            _originals.Remove(control);
        }

        private static OriginalState Remember(Control control)
        {
            OriginalState state;
            if (_originals.TryGetValue(control, out state))
                return state;

            state = new OriginalState
            {
                BackColor = control.BackColor,
                ForeColor = control.ForeColor,
                OwnBackColor = HasOwn(control, "BackColor"),
                OwnForeColor = HasOwn(control, "ForeColor"),
            };
            _originals.Add(control, state);
            return state;
        }

        /// <summary>
        /// Whether a control carries a colour of its own, rather than reading its parent's.
        ///
        /// This has to be recorded, because a control is always themed after the form above it: by the
        /// time a child is reached the window is already dark, and BackColor and ForeColor are ambient -
        /// so a control that has no colour of its own reports the dark one it is currently inheriting.
        /// Remembering that as the "original" and assigning it back on the way out is what left the
        /// command bars painting near-white text on a light bar. One that had none of its own is put
        /// back to having none of its own instead, so it inherits whatever light mode's parent is.
        ///
        /// ShouldSerializeValue asks the control the same question the designer does - has this
        /// property ever actually been assigned - which is exactly the distinction needed.
        /// </summary>
        private static bool HasOwn(Control control, string property)
        {
            try
            {
                PropertyDescriptor descriptor = TypeDescriptor.GetProperties(control)[property];
                return descriptor == null || descriptor.ShouldSerializeValue(control);
            }
            catch
            {
                //Assume it did, which is no worse than the behaviour this replaced
                return true;
            }
        }

        /// <summary>
        /// Set the background unless whoever owns this control has since chosen its colour for a reason.
        /// </summary>
        private static void SetBack(Control control, OriginalState state, Color colour)
        {
            if (state.LastAppliedBack.HasValue && control.BackColor != state.LastAppliedBack.Value)
            {
                //Changed out from under us since the last pass - that's a deliberate colour, keep it
                state.LastAppliedBack = control.BackColor;
                return;
            }

            control.BackColor = colour;
            state.LastAppliedBack = colour;
        }

        /// <summary>
        /// Drop a control off the sunken 3D border.
        ///
        /// Fixed3D is the default for lists and trees, and it is drawn by DefWindowProc from system
        /// metrics - no dark theme class touches it, so every list keeps a bright etched frame no matter
        /// what colour its client area is. That frame is most of what reads as "white surrounds".
        /// Dropping it entirely is what Visual Studio's own tool windows do - the panel's background
        /// against the surface behind it is enough separation, and it leaves nothing bright to draw.
        /// (FixedSingle was tried first and is worse: WS_BORDER paints a hard 1px line from a system
        /// colour that stays light, turning a soft grey edge into a bright one.)
        /// </summary>
        private static void FlattenBorder(Control control, OriginalState state)
        {
            //BorderStyle isn't declared on any shared base, so reflection is the only way to reach it
            //across ListView, TreeView and ListBox without a cast per type
            PropertyInfo property = control.GetType().GetProperty("BorderStyle");
            if (property == null || property.PropertyType != typeof(BorderStyle) || !property.CanWrite)
                return;

            BorderStyle current = (BorderStyle)property.GetValue(control, null);
            if (current != BorderStyle.Fixed3D)
                return;

            if (!state.BorderStyle.HasValue)
                state.BorderStyle = current;

            property.SetValue(control, BorderStyle.None, null);
        }

        private static void RestoreBorder(Control control, OriginalState state)
        {
            if (!state.BorderStyle.HasValue)
                return;

            PropertyInfo property = control.GetType().GetProperty("BorderStyle");
            if (property == null || property.PropertyType != typeof(BorderStyle) || !property.CanWrite)
                return;

            property.SetValue(control, state.BorderStyle.Value, null);
        }

        private static void ApplyDark(Control control)
        {
            OriginalState state = Remember(control);

            //The general case first; the type-specific pass below refines it
            SetBack(control, state, ThemeColours.Surface);
            control.ForeColor = ThemeColours.Text;

            Form form = control as Form;
            if (form != null)
            {
                ApplyForm(form, state);
                return;
            }

            ToolStrip strip = control as ToolStrip;
            if (strip != null)
            {
                ApplyToolStrip(strip, state);
                return;
            }

            if (control is Label || control is LinkLabel || control is CheckBox || control is RadioButton)
            {
                ApplyText(control, state);
                return;
            }

            Button button = control as Button;
            if (button != null)
            {
                ApplyButton(button, state);
                return;
            }

            ComboBox comboBox = control as ComboBox;
            if (comboBox != null)
            {
                //A DropDownList combo renders its face and button from the visual style and ignores
                //BackColor entirely - which is why they stayed white. FlatStyle.Flat hands the drawing
                //back to WinForms, where the colours below are actually used.
                if (!state.FlatStyle.HasValue)
                    state.FlatStyle = comboBox.FlatStyle;
                comboBox.FlatStyle = FlatStyle.Flat;

                SetBack(comboBox, state, ThemeColours.Input);
                comboBox.ForeColor = ThemeColours.Text;
                ThemeNative.SetControlTheme(comboBox, ThemeNative.ThemeClass.Edit, true);
                return;
            }

            TextBoxBase textBox = control as TextBoxBase;
            if (textBox != null)
            {
                //Measured at (155,155,155) before this - the 3D client edge is drawn by DefWindowProc
                //from system metrics, so DarkMode_CFD (which only reaches the client area) can't touch it
                FlattenBorder(textBox, state);
                SetBack(textBox, state, textBox.ReadOnly ? ThemeColours.InputDisabled : ThemeColours.Input);
                textBox.ForeColor = ThemeColours.Text;
                ThemeNative.SetControlTheme(textBox, ThemeNative.ThemeClass.Edit, true);
                return;
            }

            UpDownBase upDown = control as UpDownBase;
            if (upDown != null)
            {
                //Same edge, measured even brighter at (171,173,179), and there are hundreds of these
                FlattenBorder(upDown, state);
                SetBack(upDown, state, ThemeColours.Input);
                upDown.ForeColor = ThemeColours.Text;
                ThemeNative.SetControlTheme(upDown, ThemeNative.ThemeClass.Edit, true);
                ThemePainters.AttachSpinButtons(upDown);
                return;
            }

            ListView listView = control as ListView;
            if (listView != null)
            {
                //Before the colours: changing the border recreates the handle, which would drop the
                //native colour messages if it happened afterwards
                FlattenBorder(listView, state);
                ThemeListView.Apply(listView, true);
                return;
            }

            TreeView treeView = control as TreeView;
            if (treeView != null)
            {
                FlattenBorder(treeView, state);
                SetBack(treeView, state, ThemeColours.Input);
                treeView.ForeColor = ThemeColours.Text;
                treeView.LineColor = ThemeColours.BorderStrong;
                ThemeNative.SetControlTheme(treeView, ThemeNative.ThemeClass.Explorer, true);
                return;
            }

            ListBox listBox = control as ListBox;
            if (listBox != null)
            {
                FlattenBorder(listBox, state);
                SetBack(listBox, state, ThemeColours.Input);
                listBox.ForeColor = ThemeColours.Text;
                ThemeNative.SetControlTheme(listBox, ThemeNative.ThemeClass.Explorer, true);
                return;
            }

            GroupBox groupBox = control as GroupBox;
            if (groupBox != null)
            {
                SetBack(groupBox, state, groupBox.Parent != null ? groupBox.Parent.BackColor : ThemeColours.Surface);
                groupBox.ForeColor = ThemeColours.Text;
                ThemePainters.AttachGroupBox(groupBox);
                return;
            }

            TabControl tabControl = control as TabControl;
            if (tabControl != null)
            {
                SetBack(tabControl, state, ThemeColours.Surface);
                tabControl.ForeColor = ThemeColours.Text;
                ThemePainters.AttachTabControl(tabControl);
                return;
            }

            TabPage tabPage = control as TabPage;
            if (tabPage != null)
            {
                SetBack(tabPage, state, ThemeColours.Raised);
                tabPage.ForeColor = ThemeColours.Text;
                tabPage.UseVisualStyleBackColor = false;
                return;
            }

            SplitContainer splitContainer = control as SplitContainer;
            if (splitContainer != null)
            {
                //The splitter is just this control's own background showing between the two panels
                SetBack(splitContainer, state, ThemeColours.Border);
                return;
            }

            PropertyGrid propertyGrid = control as PropertyGrid;
            if (propertyGrid != null)
            {
                ApplyPropertyGrid(propertyGrid);
                return;
            }

            DataGridView grid = control as DataGridView;
            if (grid != null)
            {
                ApplyDataGridView(grid);
                return;
            }

            ProgressBar progressBar = control as ProgressBar;
            if (progressBar != null)
            {
                //Two separate steps, and both are needed. Stripping the visual style is what allows the
                //colours to apply at all, and the colours themselves have to go through PBM_SETBKCOLOR /
                //PBM_SETBARCOLOR - BackColor and ForeColor do nothing on a ProgressBar, which is why an
                //empty bar sat there as a white trough until it filled up and covered it.
                ThemeNative.SetControlTheme(progressBar, ThemeNative.ThemeClass.None, true);
                ThemeNative.SetProgressBarColours(progressBar, ThemeColours.Input, ThemeColours.Accent);
                SetBack(progressBar, state, ThemeColours.Input);
                progressBar.ForeColor = ThemeColours.Accent;
                return;
            }

            PictureBox pictureBox = control as PictureBox;
            if (pictureBox != null)
            {
                SetBack(pictureBox, state, pictureBox.Parent != null ? pictureBox.Parent.BackColor : ThemeColours.Surface);
                return;
            }

            if (control is Panel || control is TableLayoutPanel || control is FlowLayoutPanel
                || control is SplitterPanel || control is UserControl || control is ContainerControl)
            {
                //Containers take whatever they are sitting on, so nesting doesn't build up banding
                SetBack(control, state, control.Parent != null ? control.Parent.BackColor : ThemeColours.Surface);
                control.ForeColor = ThemeColours.Text;
                return;
            }

            //Anything else keeps the surface treatment above, and gets dark scrollbars
            ThemeNative.SetControlTheme(control, ThemeNative.ThemeClass.Explorer, true);
        }

        private static void ApplyForm(Form form, OriginalState state)
        {
            SetBack(form, state, ThemeColours.Surface);
            form.ForeColor = ThemeColours.Text;

            if (form.IsHandleCreated)
            {
                ThemeNative.AllowDarkModeForWindow(form.Handle, true);
                ThemeNative.SetTitleBarDarkMode(form.Handle, true);
            }
        }

        private static void ApplyText(Control control, OriginalState state)
        {
            //Deliberately a concrete colour rather than Color.Transparent. Under visual styles WinForms
            //renders a transparent child's background with DrawThemeParentBackground - it asks the system
            //theme to draw the parent, which skips our own painting entirely and hands back the LIGHT
            //theme's surface. Near-white text then lands on near-white background and disappears.
            SetBack(control, state, control.Parent != null ? control.Parent.BackColor : ThemeColours.Surface);
            control.ForeColor = control.Enabled ? ThemeColours.Text : ThemeColours.TextDisabled;

            LinkLabel link = control as LinkLabel;
            if (link != null)
            {
                link.LinkColor = ThemeColours.Link;
                link.ActiveLinkColor = ThemeColours.AccentHover;
                link.VisitedLinkColor = ThemeColours.Link;
                link.DisabledLinkColor = ThemeColours.TextDisabled;
            }

            //Check and radio glyphs follow the shell's dark button theme once the process opts in
            if (control is CheckBox || control is RadioButton)
                ThemeNative.SetControlTheme(control, ThemeNative.ThemeClass.Explorer, true);
        }

        private static void ApplyButton(Button button, OriginalState state)
        {
            if (!state.FlatStyle.HasValue)
            {
                state.FlatStyle = button.FlatStyle;
                state.FlatBorderColor = button.FlatAppearance.BorderColor;
                state.FlatMouseOverBackColor = button.FlatAppearance.MouseOverBackColor;
                state.FlatMouseDownBackColor = button.FlatAppearance.MouseDownBackColor;
                state.UseVisualStyleBackColor = button.UseVisualStyleBackColor;
            }

            //A themed 3D button face can't be recoloured, so flat is the only way to get a dark button
            //that still reads as a button - the border and the hover state are what carry it
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            SetBack(button, state, ThemeColours.Input);
            button.ForeColor = ThemeColours.Text;
            button.FlatAppearance.BorderColor = ThemeColours.Border;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.MouseOverBackColor = ThemeColours.Hover;
            button.FlatAppearance.MouseDownBackColor = ThemeColours.Accent;
        }

        private static void ApplyToolStrip(ToolStrip strip, OriginalState state)
        {
            //Has to be read before the renderer is handed over, since assigning one forces Custom
            if (!state.RenderMode.HasValue)
                state.RenderMode = strip.RenderMode;

            //The renderer comes from the docking theme, so menus and toolbars match the chrome exactly
            ToolStripRenderer renderer = ThemeManager.ToolStripRenderer;
            if (renderer != null)
                strip.Renderer = renderer;

            SetBack(strip, state, ThemeColours.Raised);
            strip.ForeColor = ThemeColours.Text;
        }

        private static void ApplyPropertyGrid(PropertyGrid grid)
        {
            grid.BackColor = ThemeColours.Surface;
            grid.ViewBackColor = ThemeColours.Input;
            grid.ViewForeColor = ThemeColours.Text;
            grid.ViewBorderColor = ThemeColours.Border;
            //LineColor is both the grid lines and the name column's background. At full border strength
            //that column renders as a light band down the middle of the grid; one tier up from the
            //surface separates the two columns without shouting.
            grid.LineColor = ThemeColours.Raised;
            grid.CategoryForeColor = ThemeColours.Text;
            grid.CategorySplitterColor = ThemeColours.Border;
            grid.HelpBackColor = ThemeColours.Raised;
            grid.HelpForeColor = ThemeColours.TextDim;
            grid.HelpBorderColor = ThemeColours.Border;
            grid.CommandsBackColor = ThemeColours.Raised;
            grid.CommandsForeColor = ThemeColours.Text;
            grid.CommandsBorderColor = ThemeColours.Border;
            grid.DisabledItemForeColor = ThemeColours.TextDisabled;
            grid.SelectedItemWithFocusBackColor = ThemeColours.Selection;
            grid.SelectedItemWithFocusForeColor = ThemeColours.Text;
        }

        private static void ApplyDataGridView(DataGridView grid)
        {
            grid.EnableHeadersVisualStyles = false;
            grid.BackgroundColor = ThemeColours.Input;
            grid.GridColor = ThemeColours.Border;
            grid.BorderStyle = BorderStyle.None;

            grid.DefaultCellStyle.BackColor = ThemeColours.Input;
            grid.DefaultCellStyle.ForeColor = ThemeColours.Text;
            grid.DefaultCellStyle.SelectionBackColor = ThemeColours.Selection;
            grid.DefaultCellStyle.SelectionForeColor = ThemeColours.Text;

            grid.ColumnHeadersDefaultCellStyle.BackColor = ThemeColours.Header;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = ThemeColours.Text;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = ThemeColours.Header;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = ThemeColours.Text;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;

            grid.RowHeadersDefaultCellStyle.BackColor = ThemeColours.Header;
            grid.RowHeadersDefaultCellStyle.ForeColor = ThemeColours.Text;
            grid.RowHeadersDefaultCellStyle.SelectionBackColor = ThemeColours.Header;
            grid.RowHeadersDefaultCellStyle.SelectionForeColor = ThemeColours.Text;
            grid.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        }

        private static void Restore(Control control)
        {
            OriginalState state;
            if (!_originals.TryGetValue(control, out state))
            {
                //Never themed, so there is nothing to undo. Important: light mode has to be exactly the
                //app with no theming applied, not the app run back through this in reverse.
                return;
            }

            //Detach anything we painted ourselves before the colours go back
            GroupBox groupBox = control as GroupBox;
            if (groupBox != null)
                ThemePainters.DetachGroupBox(groupBox);

            TabControl tabControl = control as TabControl;
            if (tabControl != null)
                ThemePainters.DetachTabControl(tabControl);

            UpDownBase upDown = control as UpDownBase;
            if (upDown != null)
                ThemePainters.DetachSpinButtons(upDown);

            ListView listView = control as ListView;
            if (listView != null)
                ThemeListView.Apply(listView, false);

            ToolStrip strip = control as ToolStrip;
            if (strip != null)
                strip.RenderMode = state.RenderMode ?? ToolStripRenderMode.ManagerRenderMode;

            Form form = control as Form;
            if (form != null && form.IsHandleCreated)
            {
                ThemeNative.AllowDarkModeForWindow(form.Handle, false);
                ThemeNative.SetTitleBarDarkMode(form.Handle, false);
            }

            ThemeNative.SetControlTheme(control, ThemeNative.ThemeClass.Explorer, false);

            //Only put the original back if nothing else has claimed the colour since
            if (!state.LastAppliedBack.HasValue || control.BackColor == state.LastAppliedBack.Value)
            {
                if (state.OwnBackColor)
                    control.BackColor = state.BackColor;
                else
                    control.ResetBackColor();
            }

            if (state.OwnForeColor)
                control.ForeColor = state.ForeColor;
            else
                control.ResetForeColor();

            Button button = control as Button;
            if (button != null && state.FlatStyle.HasValue)
            {
                button.FlatStyle = state.FlatStyle.Value;
                button.FlatAppearance.BorderColor = state.FlatBorderColor.Value;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.MouseOverBackColor = state.FlatMouseOverBackColor.Value;
                button.FlatAppearance.MouseDownBackColor = state.FlatMouseDownBackColor.Value;
                button.UseVisualStyleBackColor = state.UseVisualStyleBackColor.Value;
            }

            ComboBox comboBox = control as ComboBox;
            if (comboBox != null && state.FlatStyle.HasValue)
                comboBox.FlatStyle = state.FlatStyle.Value;

            TabPage tabPage = control as TabPage;
            if (tabPage != null)
                tabPage.UseVisualStyleBackColor = true;

            RestoreBorder(control, state);

            _originals.Remove(control);
        }
    }
}
