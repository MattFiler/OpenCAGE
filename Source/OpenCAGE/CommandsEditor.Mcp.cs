using OpenCAGE.MCP;
using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// What the main window shows of an AI assistant at work (issue 723): the editor can look idle while an assistant
    /// is reading from it or part-way through an edit, so the status bar says so, and closing OpenCAGE asks first.
    /// </summary>
    public partial class CommandsEditor
    {
        //An assistant's calls come one after another with a pause between: the message stays up this long after one, so a run of them doesn't flicker it
        private const int AiAssistantStatusGraceMs = 3000;

        private ToolStripStatusLabel _aiAssistantStatus;
        private System.Windows.Forms.Timer _aiAssistantStatusTimer;
        private bool _askingToCloseMidTask;

        /// <summary>The status bar item that shows an assistant at work: not part of the status text an assistant reads back.</summary>
        internal ToolStripItem AiAssistantStatusItem => _aiAssistantStatus;

        private void SetupAiAssistantStatus()
        {
            //First on the bar, so it is where the eye goes; the bar is dark in both themes
            _aiAssistantStatus = new AiAssistantStatusLabel()
            {
                Name = "aiAssistantStatus",
                Visible = false,
                BackColor = Theming.ThemeColours.Accent,
                ForeColor = Color.White,
                Margin = new Padding(0, 2, 6, 1),
                Padding = new Padding(4, 0, 4, 0),
            };
            statusStrip.Items.Insert(0, _aiAssistantStatus);
            McpServer.StatusChanged += OnMcpStatusChanged;
        }

        //Raised on the server's threads: a tool starting or finishing, or a client coming or going
        private void OnMcpStatusChanged()
        {
            if (IsDisposed || !IsHandleCreated)
                return;
            try { BeginInvoke(new Action(RefreshAiAssistantStatus)); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        private void RefreshAiAssistantStatus()
        {
            if (IsDisposed || _aiAssistantStatus == null)
                return;

            string tool = McpServer.RunningTool;
            if (tool != null)
            {
                _aiAssistantStatusTimer?.Stop();
                _aiAssistantStatus.Text = "AI assistant working: " + AiAssistantToolTitle(tool) + "...";
                _aiAssistantStatus.Visible = true;
                //A tool's work can hold the UI thread for a while: paint now, not when it lets go
                statusStrip.Update();
                return;
            }

            if (!_aiAssistantStatus.Visible)
                return;
            if (_aiAssistantStatusTimer == null)
            {
                _aiAssistantStatusTimer = new System.Windows.Forms.Timer() { Interval = AiAssistantStatusGraceMs };
                _aiAssistantStatusTimer.Tick += (s, e) =>
                {
                    _aiAssistantStatusTimer.Stop();
                    if (McpServer.RunningTool == null)
                        _aiAssistantStatus.Visible = false;
                };
            }
            _aiAssistantStatusTimer.Stop();
            _aiAssistantStatusTimer.Start();
        }

        /* Closing while an assistant's tool call runs cuts it off part-way. The box is the user's own: the dialog watchdog must
           not answer it for the call, and the call waits for the answer (McpDialogs.AskUser). Owned by this window, so it is
           disabled meanwhile even when OpenCAGE was closed from the taskbar, with another program active */
        private bool ConfirmCloseWhileAiAssistantWorks()
        {
            if (Program.ExitingAfterCriticalError)
                return true;

            //Closed again while the box is up (a WM_CLOSE reaches a disabled window, from taskkill say): the box already up has it
            if (_askingToCloseMidTask)
                return false;

            string tool = McpServer.RunningTool;
            if (tool == null)
                return true;

            _askingToCloseMidTask = true;
            try
            {
                return McpDialogs.AskUser(
                    "An AI assistant is in the middle of a task in OpenCAGE (" + AiAssistantToolTitle(tool) + "). Closing OpenCAGE now stops it part-way through.\n\nClose OpenCAGE anyway?",
                    "Close OpenCAGE?",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2,
                    this) == DialogResult.Yes;
            }
            finally
            {
                _askingToCloseMidTask = false;
            }
        }

        /* Work a level load queues for this thread (building its panels takes seconds) would otherwise run inside the box of
           a question asked meanwhile (McpDialogs.AskUser), and hold up the answer until it was done - a Yes to closing
           OpenCAGE did nothing until then. It waits for the answer instead: true when it has been put off */
        private bool DeferWhileUserIsAsked(Action work)
        {
            if (Volatile.Read(ref McpDialogs.UserQuestions) == 0)
                return false;
            System.Windows.Forms.Timer later = new System.Windows.Forms.Timer() { Interval = 250 };
            later.Tick += (s, e) =>
            {
                later.Dispose();
                if (!IsDisposed)
                    work();
            };
            later.Start();
            return true;
        }

        private static string AiAssistantToolTitle(string tool) => McpTools.Find(tool)?.Title ?? tool;

        /* Painted here rather than by the strip's renderer: the dark theme's renderer leaves a label's BackColor unfilled,
           which left the message looking like any other status text */
        private sealed class AiAssistantStatusLabel : ToolStripStatusLabel
        {
            protected override void OnPaint(PaintEventArgs e)
            {
                Rectangle bounds = new Rectangle(Point.Empty, Size);
                using (SolidBrush back = new SolidBrush(BackColor))
                    e.Graphics.FillRectangle(back, bounds);
                TextRenderer.DrawText(e.Graphics, Text, Font, bounds, ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
        }
    }
}
