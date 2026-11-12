using OpenCAGE.Audio;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Launch Game's "Game audio" box: Record game audio (<see cref="GameAudioRecorder"/>: every game launched from OpenCAGE is recorded
    /// until it closes), the last recording, and Recordings... to open the Game Audio window.
    /// </summary>
    public partial class LaunchGame
    {
        private GroupBox _audioGroup;
        private CheckBox _recordAudio;
        private Label _audioLine;
        private LinkLabel _audioRecordings;
        private readonly ToolTip _audioTips = new ToolTip() { AutoPopDelay = 20000 };

        /* The box goes where the launch button is, and the button and the window move down by its height (measured from the rows the
           designer laid out, so it scales as they do) */
        private void AddGameAudioBox()
        {
            int row = debugLoadingScreen.Top - debugCheckpoints.Top;
            int left = debugCheckpoints.Left;
            _audioGroup = new GroupBox()
            {
                Text = "Game audio",
                Location = new Point(uiTweaksGroup.Left, OpenGame.Top),
                Size = new Size(uiTweaksGroup.Width, debugCheckpoints.Top + row + debugCheckpoints.Height + debugCheckpoints.Top / 2),
            };
            int inside = _audioGroup.ClientSize.Width;
            _recordAudio = new CheckBox()
            {
                Text = "Record game audio",
                AutoSize = true,
                Location = new Point(left, debugCheckpoints.Top),
                Checked = GameAudioRecorder.RecordLaunches,
            };
            _audioRecordings = new LinkLabel() { AutoSize = true, Text = "Recordings..." };
            _audioRecordings.Location = new Point(inside - _audioRecordings.PreferredWidth - left, debugCheckpoints.Top);
            _audioLine = new Label()
            {
                AutoSize = false,
                AutoEllipsis = true,
                Location = new Point(left, debugCheckpoints.Top + row),
                Size = new Size(inside - left * 2, debugCheckpoints.Height),
            };
            _audioGroup.Controls.Add(_recordAudio);
            _audioGroup.Controls.Add(_audioRecordings);
            _audioGroup.Controls.Add(_audioLine);
            //The gap the designer left above the button, under whatever box is now last above it (the Crashes box, say)
            int above = uiTweaksGroup.Bottom;
            foreach (Control control in Controls)
                if (control != OpenGame && control.Bottom <= OpenGame.Top)
                    above = Math.Max(above, control.Bottom);
            int grow = _audioGroup.Height + Math.Max(0, OpenGame.Top - above);
            OpenGame.Top += grow;
            ClientSize = new Size(ClientSize.Width, ClientSize.Height + grow);
            Controls.Add(_audioGroup);
            Theming.ThemeManager.ApplyCurrent(_audioGroup);

            _audioTips.SetToolTip(_recordAudio, "Records what the game itself plays - only the game: not other programs, system sounds or a microphone - from when it starts until it closes, to a WAV in " +
                GameAudioRecorder.Folder + ". Recordings... measures them: when each sound starts and ends, how loud, how it fades. Needs Windows 11 (or Windows 10 build 20348 or later).");
            _audioTips.SetToolTip(_audioRecordings, "The game audio recordings, each measured: its loudness over time and a list of its sounds (Game Audio window). It can record a game that is already running too.");
            _recordAudio.CheckedChanged += (s, e) =>
            {
                SettingsManager.SetBool(GameAudioRecorder.SettingRecord, _recordAudio.Checked);
                UpdateGameAudioBox();
            };
            _audioRecordings.LinkClicked += (s, e) => Popups.GameAudioWindow.Open(GameAudioRecorder.Last?.Path);
            Action changed = () =>
            {
                try
                {
                    if (!IsDisposed && IsHandleCreated)
                        BeginInvoke((Action)UpdateGameAudioBox);
                }
                catch (Exception) { }
            };
            GameAudioRecorder.Changed += changed;
            FormClosed += (s, e) =>
            {
                GameAudioRecorder.Changed -= changed;
                _audioTips.Dispose();
            };
            UpdateGameAudioBox();
        }

        private void UpdateGameAudioBox()
        {
            if (_audioGroup == null || IsDisposed)
                return;
            int build = GameAudioCapture.WindowsBuild();
            bool supported = build == 0 || build >= 20348;
            _recordAudio.Enabled = supported;
            GameAudioCapture capture = GameAudioRecorder.Current;
            GameAudioRecorder.Summary last = GameAudioRecorder.Last;
            string line, tip;
            if (!supported)
            {
                line = "Needs Windows 11 (or Windows 10 build 20348+): this PC runs build " + build + ".";
                tip = "Windows can record one program's audio on its own only from Windows 10 build 20348 (Windows 11).";
            }
            else if (capture != null)
            {
                line = "Recording " + Path.GetFileName(capture.Path) + " (" + Math.Floor(capture.Seconds) + " s)";
                tip = capture.Path;
            }
            else if (last != null && last.Path != null)
            {
                line = "Last: " + Path.GetFileName(last.Path) + " (" + Math.Round(last.Seconds) + " s" + (last.PeakDb > -150 ? ", peak " + Math.Round(last.PeakDb, 1) + " dBFS" : ", silent") + ")";
                tip = last.Path + (last.Warning != null ? "\n\n" + last.Warning : "") + (last.Problem != null ? "\n\n" + last.Problem : "");
                if (last.Warning != null) line += " - " + last.Warning;
            }
            else if (last != null && last.Problem != null)
            {
                line = "The last recording failed: " + last.Problem;
                tip = last.Problem;
            }
            else
            {
                line = _recordAudio.Checked ? "Recorded to " + GameAudioRecorder.Folder : "Off: launches are not recorded.";
                tip = "Recordings go to " + GameAudioRecorder.Folder + ".";
            }
            _audioLine.Text = line;
            _audioTips.SetToolTip(_audioLine, tip);
        }
    }
}
