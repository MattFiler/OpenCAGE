using CATHODE;
using CATHODE.Scripting;
using OpenCAGE.RuntimeUtilsConnection;
using System;
using System.Windows.Forms;

namespace OpenCAGE.DockPanels
{
    /* Live link: while the game is connected, "Resync" sends this composite's scripting to the game again, so every
       running instance of it matches the editor. Edits already go to the game as they are made; this is for when the game
       has fallen out of step - edits made before the live link connected, a level the game reloaded from disk since, a
       send the game turned away. */
    public partial class CompositeDisplay
    {
        private ToolStripButton _liveLinkResync;
        //The dividers before Resync and before Show Activity (see UpdateLiveLinkSeparators)
        private ToolStripSeparator _liveLinkResyncSeparator;
        private ToolStripSeparator _liveLinkActivitySeparator;

        private void SetupLiveLinkResync()
        {
            _liveLinkResyncSeparator = new ToolStripSeparator();
            _liveLinkResync = new ToolStripButton("Resync", Properties.Resources.LiveLink_Resync)
            {
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                Visible = LiveLink.Connected,
                ToolTipText = "Send this composite's scripting to the running game again, so every running instance of it matches the editor (Live Link).\n"
                    + "Edits are sent as they are made: this is for when the game has fallen out of step - edits made before Live Link "
                    + "connected, or a level the game has reloaded from disk since.",
            };
            _liveLinkResync.Click += OnLiveLinkResyncClick;
            toolStrip1.Items.Add(_liveLinkResyncSeparator);
            toolStrip1.Items.Add(_liveLinkResync);
            UpdateLiveLinkSeparators();

            LiveLink.ConnectionChanged += OnLiveLinkResyncConnectionChanged;
            //A connection made before there was a handle to hear of it on
            HandleCreated += (s, e) => UpdateLiveLinkResyncButton();
            Disposed += (s, e) => LiveLink.ConnectionChanged -= OnLiveLinkResyncConnectionChanged;
        }

        /// <summary>Resync is there while the game is connected. UI thread.</summary>
        private void UpdateLiveLinkResyncButton()
        {
            if (IsDisposed || _liveLinkResync == null)
                return;
            _liveLinkResync.Visible = LiveLink.Connected;
            UpdateLiveLinkSeparators();
        }

        private void OnLiveLinkResyncConnectionChanged()
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
                return;
            try
            {
                BeginInvoke(new Action(UpdateLiveLinkResyncButton));
            }
            catch
            {
                //Closing
            }
        }

        /// <summary>
        /// The live link buttons sit in groups of their own after Create Flowgraph: a divider before Resync while any of them
        /// shows, and another before Show Activity while Resync shows too (once the game has gone, Show Activity and Clear
        /// Activity can stay without Resync, and then the first divider is enough). Neither shows in normal editing.
        /// </summary>
        private void UpdateLiveLinkSeparators()
        {
            if (_liveLinkResyncSeparator == null || _liveLinkResync == null)
                return;
            //Available is what each button was set to; Visible also asks whether the toolbar itself is on screen
            bool resync = _liveLinkResync.Available;
            bool activity = _liveLinkActivityButton != null && _liveLinkActivityButton.Available;
            _liveLinkResyncSeparator.Visible = resync || activity;
            if (_liveLinkActivitySeparator != null)
                _liveLinkActivitySeparator.Visible = resync && activity;
        }

        /* Live link "Show Activity": while it is on, this composite's flowgraph links light up as the running game uses them, and
           its pins count how often (see LiveLinkTrace) - in the instance navigated to from the level's root, or in every
           instance of the composite when it was opened from the browser. "Clear Activity" forgets what has lit up so far. */
        private ToolStripButton _liveLinkActivityButton;
        private ToolStripButton _liveLinkClearActivity;
        private LiveLinkActivity _liveLinkActivity;

        /// <summary>The script activity this display shows (live link). UI thread.</summary>
        public LiveLinkActivity LiveLinkActivity => _liveLinkActivity;

        private void SetupLiveLinkActivity()
        {
            _liveLinkActivity = new LiveLinkActivity(this);
            _liveLinkActivitySeparator = new ToolStripSeparator();
            _liveLinkActivityButton = new ToolStripButton("Show Activity", Properties.Resources.LiveLink_ShowActivity)
            {
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                ToolTipText = "Light up this composite's links as the running game uses them (Live Link): a logic link (orange) when its entity fires the "
                    + "output, a data link (blue) when its value is read or sent through it. They glow for a moment each time, and stay dimly lit until Clear Activity.\n"
                    + "Beside the pins, how many times each output fired - \"name [n]\" - and each method was called - \"[n] name\".\n"
                    + "Shows the instance you navigated to from the level's root - or every instance of the composite when you opened it from the browser.",
            };
            _liveLinkActivityButton.Click += (s, e) => LiveLinkTrace.SetShowActivity(!LiveLinkTrace.ShowActivity);
            _liveLinkClearActivity = new ToolStripButton("Clear Activity", Properties.Resources.LiveLink_ClearActivity)
            {
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                ToolTipText = "Forget the links lit up and the counts so far in this composite (they light up again as the game uses them).",
            };
            _liveLinkClearActivity.Click += (s, e) => _liveLinkActivity.Clear();
            toolStrip1.Items.Add(_liveLinkActivitySeparator);
            toolStrip1.Items.Add(_liveLinkActivityButton);
            toolStrip1.Items.Add(_liveLinkClearActivity);
            UpdateLiveLinkActivityButtons();

            LiveLink.ConnectionChanged += OnLiveLinkActivityConnectionChanged;
            //A connection made before there was a handle to hear of it on
            HandleCreated += (s, e) => UpdateLiveLinkActivityButtons();
            LiveLinkTrace.ShowActivityChanged += UpdateLiveLinkActivityButtons;
            _liveLinkActivity.Changed += OnLiveLinkActivityChangedButtons;
            _liveLinkActivity.Cleared += UpdateLiveLinkActivityButtons;
            LiveLinkTrace.Register(_liveLinkActivity);
            Disposed += (s, e) =>
            {
                LiveLink.ConnectionChanged -= OnLiveLinkActivityConnectionChanged;
                LiveLinkTrace.ShowActivityChanged -= UpdateLiveLinkActivityButtons;
                LiveLinkTrace.Unregister(_liveLinkActivity);
            };
        }

        /// <summary>
        /// The display was closed (it is kept, hidden, for the next composite): what it showed is forgotten, and the game
        /// stops tracing for it, once the posted refresh finds it showing nothing. UI thread.
        /// </summary>
        private void ForgetLiveLinkActivityPlace()
        {
            LiveLinkTrace.RequestRefresh();
        }

        /// <summary>A new flowgraph page shows this display's activity. UI thread.</summary>
        private void AttachLiveLinkActivity(Flowgraph page)
        {
            if (page == null || _liveLinkActivity == null)
                return;
            //What it shows is worked out afresh first, so a page of another composite never paints what the last one had lit
            LiveLinkTrace.Refresh();
            page.AttachLiveLinkActivity(_liveLinkActivity);
        }

        /// <summary>
        /// Show Activity is there while the game is connected (and after it goes, while anything is still lit, so it can be
        /// cleared or switched off); Clear Activity while Show Activity is on.
        /// </summary>
        private void UpdateLiveLinkActivityButtons()
        {
            if (IsDisposed || _liveLinkActivityButton == null)
                return;
            bool there = LiveLink.Connected || !_liveLinkActivity.IsEmpty;
            _liveLinkActivityButton.Visible = there;
            _liveLinkActivityButton.Checked = LiveLinkTrace.ShowActivity;
            _liveLinkClearActivity.Visible = there && LiveLinkTrace.ShowActivity;
            UpdateLiveLinkSeparators();
        }

        private void OnLiveLinkActivityChangedButtons(LiveLinkActivity.Change change)
        {
            if (change.Repaint.Count != 0 || change.NewPins)
                UpdateLiveLinkActivityButtons();
        }

        private void OnLiveLinkActivityConnectionChanged()
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
                return;
            try
            {
                BeginInvoke(new Action(UpdateLiveLinkActivityButtons));
            }
            catch
            {
                //Closing
            }
        }

        private async void OnLiveLinkResyncClick(object sender, EventArgs e)
        {
            Commands commands = Content?.Level?.Commands;
            Composite composite = Composite;
            if (commands == null || composite == null)
                return;
            _liveLinkResync.Enabled = false;
            try
            {
                LiveLink.Reply reply = await LiveLink.PushNow(commands, composite);
                Singleton.Editor?.ShowLiveLinkActivity(reply.Message);
                if (!reply.Ok)
                    MessageBox.Show(reply.Message, "Live Link", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                if (!IsDisposed)
                    _liveLinkResync.Enabled = true;
            }
        }
    }
}
