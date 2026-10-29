using OpenCAGE.Popups.Base;
using OpenCAGE.RuntimeUtilsConnection;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// The Runtime Error Log: the level's script errors as the running game finds them, like a game engine's console. It opens as the
    /// game is launched with Launch Game's Error log ticked (and from the Script Debugger menu). Each line is an error the game
    /// reported or a check of an entity it made that failed, with the entity it is about and the composite that entity is in;
    /// double-click one to go to its entity. Clear empties it; Clear on Play empties it as the game is launched from OpenCAGE
    /// (on unless switched off, and remembered); Group by Entity lists the lines under their entities. While it is open, the
    /// flowgraph nodes of entities with errors show a warning sign: clearing it, or closing it, takes them away. The lines come
    /// from ScriptErrorLog. Built in code: a toolbar, a list and the selected line's details.
    /// </summary>
    public class ErrorLog : BaseWindow
    {
        private static ErrorLog _open;

        /// <summary>Open the Error Log, or bring it to the front. UI thread.</summary>
        public static void Open()
        {
            if (_open != null && !_open.IsDisposed)
            {
                if (_open.WindowState == FormWindowState.Minimized)
                    _open.WindowState = FormWindowState.Normal;
                _open.Show();
                _open.BringToFront();
                return;
            }
            _open = new ErrorLog();
            _open.Show();
        }

        public static bool IsOpen => _open != null && !_open.IsDisposed;

        private readonly ToolStrip toolStrip = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
        //Clear acts on the log, on the left; the two settings sit on the right
        private readonly ToolStripButton clearBtn = new ToolStripButton("Clear") { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText };
        private readonly ToolStripButton clearOnPlayBtn = new ToolStripButton("Clear on Play") { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText, CheckOnClick = true, Alignment = ToolStripItemAlignment.Right };
        private readonly ToolStripButton groupBtn = new ToolStripButton("Group by Entity") { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText, CheckOnClick = true, Alignment = ToolStripItemAlignment.Right };
        private readonly ListView list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false, ShowItemToolTips = true };
        private readonly TextBox details = new TextBox { Dock = DockStyle.Bottom, Height = 96, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true };
        private readonly ImageList icons = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(16, 16) };
        private readonly Dictionary<(uint, uint), ListViewGroup> _groups = new Dictionary<(uint, uint), ListViewGroup>();
        private readonly HashSet<ScriptError> _shown = new HashSet<ScriptError>();

        private const int ErrorIcon = 0, WarningIcon = 1;

        public ErrorLog() : base(WindowClosesOn.NONE)
        {
            Text = "Runtime Error Log";
            Icon = SharedFormIcon.Icon;
            ClientSize = new Size(960, 440);
            MinimumSize = new Size(600, 280);
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
            OpenCAGE.Popups.Base.BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/live-link/#error-log");

            icons.Images.Add(ScriptErrorLog.SignImage(true, 16));
            icons.Images.Add(ScriptErrorLog.SignImage(false, 16));
            list.SmallImageList = icons;
            list.Columns.Add("Kind", 100);
            list.Columns.Add("Message", 380);
            list.Columns.Add("Entity", 190);
            list.Columns.Add("Composite", 170);
            list.Columns.Add("Script time", 75, HorizontalAlignment.Right);

            //Both settings as they were last left (Clear on Play is on until it's turned off)
            clearOnPlayBtn.Checked = SettingsManager.GetBool(Settings.ErrorLogClearOnPlay, true);
            groupBtn.Checked = SettingsManager.GetBool(Settings.ErrorLogGroupByEntity, false);
            clearBtn.ToolTipText = "Empty the log (the warning signs on flowgraph nodes go with it).";
            clearOnPlayBtn.ToolTipText = "Empty the log each time the game is launched from OpenCAGE.";
            groupBtn.ToolTipText = "List the errors under the entities they are about.";
            EditorIcons.Bind(clearBtn, EditorIcon.ClearLog);
            EditorIcons.Bind(clearOnPlayBtn, EditorIcon.ClearOnPlay);
            EditorIcons.Bind(groupBtn, EditorIcon.GroupByEntity);
            clearBtn.Click += (s, e) => ScriptErrorLog.Clear();
            clearOnPlayBtn.CheckedChanged += (s, e) => SettingsManager.SetBool(Settings.ErrorLogClearOnPlay, clearOnPlayBtn.Checked);
            groupBtn.CheckedChanged += (s, e) =>
            {
                SettingsManager.SetBool(Settings.ErrorLogGroupByEntity, groupBtn.Checked);
                Rebuild();
            };
            //Right-aligned items are laid out from the right: Group by Entity outermost, Clear on Play beside it
            toolStrip.Items.AddRange(new ToolStripItem[] { clearBtn, groupBtn, clearOnPlayBtn });

            list.DoubleClick += (s, e) => GoTo(Selected());
            list.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                e.Handled = true;
                GoTo(Selected());
            };
            list.SelectedIndexChanged += (s, e) => ShowDetails(Selected());

            //Docked from the edges in: the toolbar and details first, then the list in what is left
            Controls.Add(list);
            Controls.Add(details);
            Controls.Add(toolStrip);

            ScriptErrorLog.Added += OnAdded;
            ScriptErrorLog.Cleared += OnCleared;
            ScriptErrorLog.StatusChanged += OnStatusChanged;
            Load += (s, e) => Rebuild();
            FormClosed += (s, e) =>
            {
                ScriptErrorLog.Added -= OnAdded;
                ScriptErrorLog.Cleared -= OnCleared;
                ScriptErrorLog.StatusChanged -= OnStatusChanged;
                ScriptErrorLog.Stop();
                icons.Dispose();
                if (_open == this)
                    _open = null;
            };
            ScriptErrorLog.Start();
        }

        #region EVENTS (any thread)
        private void OnAdded(List<ScriptError> added)
        {
            OnUiThread(() => Add(added));
        }

        private void OnCleared()
        {
            OnUiThread(Rebuild);
        }

        //Said under an empty log, where nothing is selected (see ShowDetails)
        private void OnStatusChanged()
        {
            OnUiThread(() =>
            {
                if (!IsDisposed && Selected() == null)
                    ShowDetails(null);
            });
        }

        private void OnUiThread(Action action)
        {
            if (IsDisposed || !IsHandleCreated)
                return;
            try
            {
                BeginInvoke(action);
            }
            catch (InvalidOperationException)
            {
                //Closing
            }
        }
        #endregion

        #region THE LIST
        /// <summary>Everything the log holds, afresh. UI thread.</summary>
        private void Rebuild()
        {
            if (IsDisposed)
                return;
            list.BeginUpdate();
            try
            {
                list.Items.Clear();
                list.Groups.Clear();
                _groups.Clear();
                _shown.Clear();
                list.ShowGroups = groupBtn.Checked;
                AddRows(ScriptErrorLog.Snapshot());
            }
            finally
            {
                list.EndUpdate();
            }
            ShowDetails(Selected());
        }

        /// <summary>New errors at the end, the list kept scrolled to the bottom when it was there. UI thread.</summary>
        private void Add(List<ScriptError> added)
        {
            if (IsDisposed)
                return;
            bool atBottom = list.Items.Count == 0 || list.Items[list.Items.Count - 1].Bounds.Top <= list.ClientRectangle.Bottom;
            list.BeginUpdate();
            try
            {
                AddRows(added);
            }
            finally
            {
                list.EndUpdate();
            }
            if (atBottom && list.Items.Count != 0 && !groupBtn.Checked)
                list.Items[list.Items.Count - 1].EnsureVisible();
            if (Selected() == null)
                ShowDetails(null);
        }

        private void AddRows(List<ScriptError> errors)
        {
            foreach (ScriptError error in errors)
            {
                if (!_shown.Add(error))
                    continue;
                string entity = ScriptErrorLog.EntityText(error);
                string composite = ScriptErrorLog.CompositeText(error);
                ListViewItem item = new ListViewItem(error.FailedCheck ? "Check failed" : "Error", error.FailedCheck ? WarningIcon : ErrorIcon) { Tag = error };
                item.SubItems.Add(error.Message);
                item.SubItems.Add(entity);
                item.SubItems.Add(composite);
                item.SubItems.Add(error.ScriptTime.ToString("0.00", CultureInfo.InvariantCulture) + " s");
                item.ToolTipText = error.Message + "\n" + entity + (composite.Length == 0 ? "" : " in " + composite) + "\nDouble-click to go to it.";
                if (groupBtn.Checked)
                    item.Group = GroupOf(error, entity, composite);
                list.Items.Add(item);
            }
            if (groupBtn.Checked)
                foreach (KeyValuePair<(uint, uint), ListViewGroup> group in _groups)
                    group.Value.Header = ((string)group.Value.Tag) + "  (" + group.Value.Items.Count + ")";
        }

        private ListViewGroup GroupOf(ScriptError error, string entity, string composite)
        {
            (uint, uint) key = error.EntityRead ? (error.CompositeId, error.Entity) : (0u, 0u);
            if (_groups.TryGetValue(key, out ListViewGroup group))
                return group;
            string title = error.EntityRead ? entity + (composite.Length == 0 ? "" : " in " + composite) : "Entities the game could not read";
            group = new ListViewGroup(title) { Tag = title };
            _groups.Add(key, group);
            list.Groups.Add(group);
            return group;
        }

        private ScriptError Selected()
        {
            return list.SelectedItems.Count == 0 ? null : list.SelectedItems[0].Tag as ScriptError;
        }

        private void ShowDetails(ScriptError error)
        {
            if (error == null)
            {
                //Nothing selected: what to do with the list, or (while it is empty) where things stand with the game
                List<string> notes = new List<string>();
                if (list.Items.Count != 0)
                    notes.Add("Select an error to see it in full; double-click it (or press Enter) to go to its entity.");
                else
                {
                    string status = ScriptErrorLog.Status;
                    notes.Add(string.IsNullOrEmpty(status) || status == ScriptErrorLog.FollowingStatus ? "No script errors yet." : status.TrimEnd('.') + ".");
                }
                int lost = ScriptErrorLog.Lost;
                if (lost != 0)
                    notes.Add(lost + (lost == 1 ? " earlier error was" : " earlier errors were") + " missed: more came at once than the game keeps.");
                details.Text = string.Join(Environment.NewLine, notes);
                return;
            }
            string where = ScriptErrorLog.Locate(error, out _, out _, out _, out List<CATHODE.Scripting.ShortGuid> path);
            List<string> lines = new List<string>()
            {
                (error.FailedCheck ? "Check failed: " : "Error: ") + error.Message,
                "Entity: " + ScriptErrorLog.EntityText(error) + (error.EntityRead ? " in " + ScriptErrorLog.CompositeText(error) : ""),
                where == null ? "Double-click to go to it" + (path.Count == 0 ? " (in the level's root)." : " (in the instance it happened in).") : where + ".",
                "At " + error.ScriptTime.ToString("0.00", CultureInfo.InvariantCulture) + " s of the level's script time.",
            };
            details.Text = string.Join(Environment.NewLine, lines);
        }

        private void GoTo(ScriptError error)
        {
            if (error == null)
                return;
            string problem = ScriptErrorLog.Show(error);
            if (problem != null)
                MessageBox.Show(problem + ".", "Go to entity", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        #endregion
    }
}
