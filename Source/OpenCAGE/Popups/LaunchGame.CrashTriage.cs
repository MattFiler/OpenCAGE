using OpenCAGE.CrashTriage;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Launch Game's "Crashes" box, above the launch button: why the last launch crashed (crash triage, <see cref="CrashWatch"/>)
    /// with its details, and whether Windows writes crash dumps of the game, with the offer to turn them on or off
    /// (<see cref="LocalDumps"/>).
    /// </summary>
    public partial class LaunchGame
    {
        private GroupBox _crashGroup;
        private Label _crashLine;
        private LinkLabel _crashDetails;
        private Label _dumpsLine;
        private LinkLabel _dumpsSwitch;
        private readonly ToolTip _crashTips = new ToolTip() { AutoPopDelay = 20000 };

        /* The box goes where the launch button is, and the button and the window move down by its height (measured from the
           rows the designer laid out, so it scales as they do) */
        private void AddCrashTriageBox()
        {
            int row = debugLoadingScreen.Top - debugCheckpoints.Top;
            int left = debugCheckpoints.Left;
            _crashGroup = new GroupBox()
            {
                Text = "Crashes",
                Location = new Point(uiTweaksGroup.Left, OpenGame.Top),
                Size = new Size(uiTweaksGroup.Width, debugCheckpoints.Top + row + debugCheckpoints.Height + debugCheckpoints.Top / 2),
            };
            int inside = _crashGroup.ClientSize.Width;
            _crashDetails = new LinkLabel() { AutoSize = true, Text = "Details...", Visible = false };
            _crashDetails.Location = new Point(inside - _crashDetails.PreferredWidth - left, debugCheckpoints.Top);
            _crashLine = new Label()
            {
                AutoSize = false,
                AutoEllipsis = true,
                Location = new Point(left, debugCheckpoints.Top),
                Size = new Size(_crashDetails.Left - left - 6, debugCheckpoints.Height),
            };
            _dumpsSwitch = new LinkLabel() { AutoSize = true, Text = "Turn on..." };
            _dumpsLine = new Label()
            {
                AutoSize = false,
                AutoEllipsis = true,
                Location = new Point(left, debugCheckpoints.Top + row),
                Size = new Size(inside - left * 2, debugCheckpoints.Height),
            };
            _crashGroup.Controls.Add(_crashLine);
            _crashGroup.Controls.Add(_crashDetails);
            _crashGroup.Controls.Add(_dumpsLine);
            _crashGroup.Controls.Add(_dumpsSwitch);
            int grow = _crashGroup.Height + (OpenGame.Top - uiTweaksGroup.Bottom);
            OpenGame.Top += grow;
            ClientSize = new Size(ClientSize.Width, ClientSize.Height + grow);
            Controls.Add(_crashGroup);
            Theming.ThemeManager.ApplyCurrent(_crashGroup);

            _crashDetails.LinkClicked += (s, e) => ShowCrashDetails();
            _dumpsSwitch.LinkClicked += (s, e) => SwitchCrashDumps();
            _crashTips.SetToolTip(_crashDetails, "What happened, what to do about it, and the crash dump it was read from.");

            Action<CrashReport> crashed = report =>
            {
                try
                {
                    if (!IsDisposed && IsHandleCreated)
                        BeginInvoke((Action)UpdateCrashBox);
                }
                catch (Exception) { }
            };
            CrashWatch.Crashed += crashed;
            FormClosed += (s, e) =>
            {
                CrashWatch.Crashed -= crashed;
                _crashTips.Dispose();
            };
            UpdateCrashBox();
        }

        private void UpdateCrashBox()
        {
            if (_crashGroup == null || IsDisposed)
                return;
            CrashReport last = CrashWatch.Last;
            if (last == null)
            {
                _crashLine.Text = "No launch from OpenCAGE has crashed.";
                _crashDetails.Visible = false;
            }
            else
            {
                string when = last.ExitedUtc.HasValue ? last.ExitedUtc.Value.ToLocalTime().ToString(last.ExitedUtc.Value.ToLocalTime().Date == DateTime.Today ? "HH:mm" : "d MMM HH:mm") : null;
                _crashLine.Text = "Last crash" + (when != null ? " (" + when + (last.Level != null ? ", " + last.Level : "") + ")" : "") + ": " + last.Headline();
                _crashDetails.Visible = true;
            }
            _crashTips.SetToolTip(_crashLine, last == null ? "When a game launched from here crashes, OpenCAGE reads the crash dump Windows writes and names the cause, if it is a crash it knows." : last.Text());

            LocalDumps.State dumps = LocalDumps.Read();
            _dumpsSwitch.Text = dumps.On ? "Turn off..." : "Turn on...";
            //Only AI.exe's own key is ours to remove: dumps on for every program stay as they are
            _dumpsSwitch.Visible = !dumps.On || dumps.AppKey;
            _dumpsSwitch.Location = new Point(_crashGroup.ClientSize.Width - _dumpsSwitch.PreferredWidth - debugCheckpoints.Left, _dumpsLine.Top);
            _dumpsLine.Width = (_dumpsSwitch.Visible ? _dumpsSwitch.Left - 6 : _crashGroup.ClientSize.Width - debugCheckpoints.Left) - _dumpsLine.Left;
            //The folder is what the user looks for; the dump size, count and scope are in the tooltip
            _dumpsLine.Text = "Crash dumps: " + (dumps.On ? "on, saved in " + dumps.Folder : dumps.Describe());
            _crashTips.SetToolTip(_dumpsLine, dumps.On
                ? "Windows saves a crash dump when the game crashes, which OpenCAGE reads to say why. " + dumps.Describe() + "."
                : "Windows does not save a crash dump when the game crashes, so OpenCAGE can't say why it did. Turn on... explains what turning them on changes.");
            _crashTips.SetToolTip(_dumpsSwitch, dumps.On ? "Stop Windows saving crash dumps of the game (removes the registry key OpenCAGE added)." : "Have Windows save a crash dump when the game crashes (asks first, saying what it changes).");
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
            UpdateCrashBox();
        }

        private void ShowCrashDetails()
        {
            CrashReport last = CrashWatch.Last;
            if (last == null)
                return;
            using (Form details = new Form())
            {
                details.Text = "Last crash";
                details.Icon = SharedFormIcon.Icon;
                details.StartPosition = FormStartPosition.CenterParent;
                details.ShowInTaskbar = false;
                details.MinimizeBox = false;
                details.MaximizeBox = false;
                details.ClientSize = new Size(640, 360);
                details.MinimumSize = new Size(420, 240);
                TextBox text = new TextBox()
                {
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical,
                    WordWrap = true,
                    Text = last.Text().Replace("\r\n", "\n").Replace("\n", "\r\n"),
                    Location = new Point(12, 12),
                    Size = new Size(616, 300),
                    Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                };
                Button folder = new Button() { Text = "Open dump folder", Size = new Size(130, 26), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
                folder.Location = new Point(12, 324);
                folder.Enabled = last.DumpPath != null && File.Exists(last.DumpPath);
                folder.Click += (s, e) =>
                {
                    try { Process.Start("explorer.exe", "/select,\"" + last.DumpPath + "\""); }
                    catch (Exception) { }
                };
                Button copy = new Button() { Text = "Copy", Size = new Size(80, 26), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
                copy.Location = new Point(folder.Right + 8, 324);
                copy.Click += (s, e) =>
                {
                    try { Clipboard.SetText(text.Text); }
                    catch (Exception) { }
                };
                Button close = new Button() { Text = "Close", Size = new Size(80, 26), DialogResult = DialogResult.OK, Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
                close.Location = new Point(640 - 12 - close.Width, 324);
                details.Controls.Add(text);
                details.Controls.Add(folder);
                details.Controls.Add(copy);
                details.Controls.Add(close);
                details.AcceptButton = close;
                details.CancelButton = close;
                Theming.ThemeManager.ApplyToForm(details);
                text.Select(0, 0);
                details.ShowDialog(this);
            }
        }
    }
}
