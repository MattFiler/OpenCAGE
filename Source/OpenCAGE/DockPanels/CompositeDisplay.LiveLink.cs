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

        private void SetupLiveLinkResync()
        {
            _liveLinkResync = new ToolStripButton("Resync")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Visible = LiveLink.Connected,
                ToolTipText = "Send this composite's scripting to the running game again, so every running instance of it matches the editor (live link).\n"
                    + "Edits are sent as they are made: this is for when the game has fallen out of step - edits made before the live link "
                    + "connected, or a level the game has reloaded from disk since.",
            };
            _liveLinkResync.Click += OnLiveLinkResyncClick;
            toolStrip1.Items.Add(_liveLinkResync);

            LiveLink.ConnectionChanged += OnLiveLinkResyncConnectionChanged;
            Disposed += (s, e) => LiveLink.ConnectionChanged -= OnLiveLinkResyncConnectionChanged;
        }

        private void OnLiveLinkResyncConnectionChanged()
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
                return;
            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (_liveLinkResync != null && !IsDisposed)
                        _liveLinkResync.Visible = LiveLink.Connected;
                }));
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
                    MessageBox.Show(reply.Message, "Live link", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                if (!IsDisposed)
                    _liveLinkResync.Enabled = true;
            }
        }
    }
}
