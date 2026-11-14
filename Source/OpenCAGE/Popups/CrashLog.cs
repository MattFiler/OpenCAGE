using OpenCAGE.CrashTriage;
using OpenCAGE.Popups.Base;
using OpenCAGE.RuntimeUtilsConnection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// The Game Crash Log: the crashes of games launched from OpenCAGE, newest first, each named by crash triage
    /// (<see cref="CrashWatch"/>) with what happened and what to do about it. It opens by itself when a launch crashes (Open on
    /// Crash, on unless switched off, and remembered), and from the Script Debugger menu. Open Dump... triages any crash dump of
    /// the game; Crash Dumps turns Windows' crash dumps of the game on or off (<see cref="LocalDumps"/>).
    /// </summary>
    public class CrashLog : BaseWindow
    {
        private static CrashLog _open;
        private static bool _watching;

        /// <summary>Open the Crash Log (or bring it to the front), with a crash selected, else the newest. UI thread.</summary>
        internal static void Open(CrashReport select = null)
        {
            if (_open == null || _open.IsDisposed)
            {
                _open = new CrashLog();
                _open.Show();
            }
            else
            {
                if (_open.WindowState == FormWindowState.Minimized)
                    _open.WindowState = FormWindowState.Normal;
                _open.Show();
                _open.BringToFront();
            }
            _open.Rebuild(select);
        }

        /// <summary>Open the Crash Log when a launch crashes (if Open on Crash is on), on <paramref name="ui"/>'s thread. Once, at startup.</summary>
        public static void WatchCrashes(Control ui)
        {
            if (_watching)
                return;
            _watching = true;
            CrashWatch.Crashed += report =>
            {
                try
                {
                    if (ui.IsDisposed || !ui.IsHandleCreated)
                        return;
                    ui.BeginInvoke((Action)(() =>
                    {
                        if (_open != null && !_open.IsDisposed)
                            _open.Rebuild(report);
                        else if (SettingsManager.GetBool(Settings.CrashLogOpenOnCrash, true))
                            Open(report);
                    }));
                }
                catch (InvalidOperationException)
                {
                    //Closing
                }
            };
        }

        private readonly ToolStrip toolStrip = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
        private readonly ToolStripButton clearBtn = new ToolStripButton("Clear") { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText };
        private readonly ToolStripButton openDumpBtn = new ToolStripButton("Open Dump...") { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText };
        private readonly ToolStripButton showDumpBtn = new ToolStripButton("Show Dump File") { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText };
        private readonly ToolStripButton copyBtn = new ToolStripButton("Copy") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        private readonly ToolStripButton openOnCrashBtn = new ToolStripButton("Open on Crash") { DisplayStyle = ToolStripItemDisplayStyle.Text, CheckOnClick = true, Alignment = ToolStripItemAlignment.Right };
        private readonly ToolStripButton dumpsBtn = new ToolStripButton("Crash Dumps") { DisplayStyle = ToolStripItemDisplayStyle.Text, Alignment = ToolStripItemAlignment.Right };
        private readonly ListView list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false, ShowItemToolTips = true };
        private readonly TextBox details = new TextBox { Dock = DockStyle.Bottom, Height = 190, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true };
        private readonly StatusStrip statusStrip = new StatusStrip { SizingGrip = false };
        private readonly ToolStripStatusLabel dumpsLabel = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        private readonly ImageList icons = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(16, 16) };

        //Dumps opened with Open Dump...: listed above the launches' crashes until the window closes
        private readonly List<CrashReport> _opened = new List<CrashReport>();
        private int _reading;

        public CrashLog() : base(WindowClosesOn.NONE)
        {
            Text = "Game Crash Log";
            Icon = SharedFormIcon.Icon;
            ClientSize = new Size(900, 480);
            MinimumSize = new Size(560, 320);
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
            OpenCAGE.Popups.Base.BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/launch-game/#crash-triage");

            icons.Images.Add(ScriptErrorLog.SignImage(true, 16));
            list.SmallImageList = icons;
            list.Columns.Add("When", 110);
            list.Columns.Add("Level", 170);
            list.Columns.Add("Crash", 420);
            list.Columns.Add("Ran for", 70, HorizontalAlignment.Right);
            list.Columns.Add("Dump", 110);

            openOnCrashBtn.Checked = SettingsManager.GetBool(Settings.CrashLogOpenOnCrash, true);
            clearBtn.ToolTipText = "Empty the list of crashes (the crash dumps themselves are kept).";
            openDumpBtn.ToolTipText = "Read any crash dump of the game (.dmp) and name the crash, if it is one OpenCAGE knows.";
            showDumpBtn.ToolTipText = "Show the selected crash's dump file in Explorer.";
            copyBtn.ToolTipText = "Copy the selected crash's report.";
            openOnCrashBtn.ToolTipText = "Open this window when a game launched from OpenCAGE crashes.";
            EditorIcons.Bind(clearBtn, EditorIcon.ClearLog);
            EditorIcons.Bind(openDumpBtn, EditorIcon.File);
            EditorIcons.Bind(showDumpBtn, EditorIcon.FolderOpen);
            clearBtn.Click += (s, e) =>
            {
                CrashWatch.ClearHistory();
                _opened.Clear();
                Rebuild(null);
            };
            openDumpBtn.Click += (s, e) => OpenDump();
            showDumpBtn.Click += (s, e) => ShowDumpFile(Selected());
            copyBtn.Click += (s, e) =>
            {
                try { if (details.Text.Length != 0) Clipboard.SetText(details.Text); }
                catch (Exception) { }
            };
            openOnCrashBtn.CheckedChanged += (s, e) => SettingsManager.SetBool(Settings.CrashLogOpenOnCrash, openOnCrashBtn.Checked);
            dumpsBtn.Click += (s, e) => SwitchCrashDumps();
            //Right-aligned items are laid out from the right: Crash Dumps outermost, Open on Crash beside it
            toolStrip.Items.AddRange(new ToolStripItem[] { clearBtn, new ToolStripSeparator(), openDumpBtn, showDumpBtn, copyBtn, dumpsBtn, openOnCrashBtn });
            statusStrip.Items.Add(dumpsLabel);

            list.SelectedIndexChanged += (s, e) => ShowDetails(Selected());
            list.DoubleClick += (s, e) => ShowDumpFile(Selected());

            //Docked from the edges in: the toolbar, status line and details first, then the list in what is left
            Controls.Add(list);
            Controls.Add(details);
            Controls.Add(statusStrip);
            Controls.Add(toolStrip);

            Activated += (s, e) => ShowDumpsState();
            FormClosed += (s, e) =>
            {
                icons.Dispose();
                if (_open == this)
                    _open = null;
            };
        }

        #region THE LIST
        /// <summary>The crashes afresh, with one selected (else the newest). UI thread.</summary>
        private void Rebuild(CrashReport select)
        {
            if (IsDisposed)
                return;
            List<CrashReport> reports = _opened.Concat(CrashWatch.History).ToList();
            list.BeginUpdate();
            try
            {
                list.Items.Clear();
                foreach (CrashReport report in reports)
                    list.Items.Add(Row(report));
            }
            finally
            {
                list.EndUpdate();
            }
            ListViewItem selected = list.Items.Cast<ListViewItem>().FirstOrDefault(o => o.Tag == select) ?? (list.Items.Count != 0 ? list.Items[0] : null);
            if (selected != null)
            {
                selected.Selected = true;
                selected.Focused = true;
                selected.EnsureVisible();
            }
            ShowDumpsState();
            ShowDetails(Selected());
        }

        private ListViewItem Row(CrashReport report)
        {
            ListViewItem item = new ListViewItem("", 0) { Tag = report };
            Fill(item, report);
            return item;
        }

        private void Fill(ListViewItem item, CrashReport report)
        {
            item.Tag = report;
            bool opened = _opened.Contains(report);
            DateTime? when = report.ExitedUtc ?? (report.DumpTimeUtc != default(DateTime) ? report.DumpTimeUtc : (DateTime?)null);
            string[] cells =
            {
                when.HasValue ? when.Value.ToLocalTime().ToString(when.Value.ToLocalTime().Date == DateTime.Today ? "HH:mm:ss" : "d MMM HH:mm", CultureInfo.InvariantCulture) : "",
                opened ? "(opened dump)" : report.Level ?? "",
                report.Headline(),
                report.LaunchedUtc.HasValue && report.ExitedUtc.HasValue ? Math.Round((report.ExitedUtc.Value - report.LaunchedUtc.Value).TotalSeconds).ToString(CultureInfo.InvariantCulture) + " s" : "",
                report.DumpPath != null ? Path.GetFileName(report.DumpPath) : "none",
            };
            item.Text = cells[0];
            while (item.SubItems.Count < cells.Length)
                item.SubItems.Add("");
            for (int i = 1; i < cells.Length; i++)
                item.SubItems[i].Text = cells[i];
            item.ToolTipText = report.Headline();
        }

        private CrashReport Selected()
        {
            return list.SelectedItems.Count == 0 ? null : list.SelectedItems[0].Tag as CrashReport;
        }

        private void ShowDetails(CrashReport report)
        {
            showDumpBtn.Enabled = report?.DumpPath != null && File.Exists(report.DumpPath);
            copyBtn.Enabled = report != null;
            if (report == null)
            {
                details.Text = list.Items.Count != 0 ? "Select a crash to see what happened and what to do about it." :
                    "No game launched from OpenCAGE has crashed." + Environment.NewLine + Environment.NewLine +
                    "When one does, OpenCAGE reads the crash dump Windows saves of it and names the cause, if it is a crash it knows. " +
                    "Open Dump... reads any other crash dump of the game.";
                return;
            }
            if (!report.Summary)
            {
                details.Text = report.Text().Replace("\r\n", "\n").Replace("\n", "\r\n");
                details.Select(0, 0);
                return;
            }
            //Read back from disk as a summary: read its dump again for the whole report
            details.Text = report.Headline() + Environment.NewLine + Environment.NewLine + "Reading the crash dump...";
            int reading = ++_reading;
            Task.Run(() => CrashWatch.Full(report)).ContinueWith(task =>
            {
                CrashReport full = task.Status == TaskStatus.RanToCompletion ? task.Result : null;
                OnUiThread(() =>
                {
                    //Not read again (the dump has gone, or could not be read): its summary is all there is
                    if (full == null || full == report)
                    {
                        report.Summary = false;
                        full = report;
                    }
                    ListViewItem item = list.Items.Cast<ListViewItem>().FirstOrDefault(o => o.Tag == report);
                    if (item != null)
                        Fill(item, full);
                    if (reading == _reading && Selected() == full)
                        ShowDetails(full);
                });
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

        #region DUMPS
        private void OpenDump()
        {
            string folder = LocalDumps.Folders().FirstOrDefault(o => Directory.Exists(o));
            using (OpenFileDialog dialog = new OpenFileDialog()
            {
                Title = "Open a crash dump of the game",
                Filter = "Crash dumps (*.dmp)|*.dmp|All files (*.*)|*.*",
                InitialDirectory = folder ?? "",
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                string path = dialog.FileName;
                UseWaitCursor = true;
                Task.Run(() => CrashAnalysis.Analyze(path, Singleton.PathToAI)).ContinueWith(task => OnUiThread(() =>
                {
                    UseWaitCursor = false;
                    if (task.Status != TaskStatus.RanToCompletion)
                    {
                        MessageBox.Show(this, "The crash dump could not be read: " + task.Exception?.GetBaseException().Message, "Open Dump", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    _opened.RemoveAll(o => string.Equals(o.DumpPath, path, StringComparison.OrdinalIgnoreCase));
                    _opened.Insert(0, task.Result);
                    Rebuild(task.Result);
                }));
            }
        }

        private void ShowDumpFile(CrashReport report)
        {
            if (report?.DumpPath == null || !File.Exists(report.DumpPath))
                return;
            try { Process.Start("explorer.exe", "/select,\"" + report.DumpPath + "\""); }
            catch (Exception) { }
        }

        /* Whether Windows saves crash dumps of the game, in the status line, and the button that turns them on or off */
        private void ShowDumpsState()
        {
            if (IsDisposed)
                return;
            LocalDumps.State dumps = LocalDumps.Read();
            dumpsBtn.Text = dumps.On ? "Turn Off Crash Dumps..." : "Turn On Crash Dumps...";
            //Only AI.exe's own key is ours to remove: dumps on for every program stay as they are
            dumpsBtn.Visible = !dumps.On || dumps.AppKey;
            dumpsBtn.ToolTipText = dumps.On ? "Stop Windows saving crash dumps of the game (removes the registry key OpenCAGE added)." : "Have Windows save a crash dump when the game crashes (asks first, saying what it changes).";
            dumpsLabel.Text = "Crash dumps: " + dumps.Describe() + (dumps.On ? "" : " - without them OpenCAGE can't say why the game crashed");
        }

        private void SwitchCrashDumps()
        {
            LocalDumps.State dumps = LocalDumps.Read();
            string problem;
            if (!dumps.On)
            {
                if (MessageBox.Show(this, LocalDumps.WhatTurningOnDoes(), "Turn on crash dumps", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                    return;
                problem = LocalDumps.TurnOn();
            }
            else
            {
                if (MessageBox.Show(this, "Stop Windows saving a crash dump when Alien: Isolation crashes?\n\nThis removes the registry key HKEY_LOCAL_MACHINE\\" + LocalDumps.AppKeyPath +
                    " (Windows will ask for administrator permission). Dumps already saved are kept.", "Turn off crash dumps", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                    return;
                problem = LocalDumps.TurnOff();
            }
            if (problem != null && problem != "cancelled")
                MessageBox.Show(this, "Crash dumps could not be turned " + (dumps.On ? "off" : "on") + ": " + problem + ".", "Crash dumps", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            ShowDumpsState();
        }
        #endregion
    }
}
