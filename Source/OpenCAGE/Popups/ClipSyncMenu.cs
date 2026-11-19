using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.ClipTools;
using OpenCAGE.MCP;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE.Popups
{
    /// <summary>
    /// "Sync to a CAGEAnimation..." on a CMD_PlayAnimation: links the command's ExternalTime to a CAGEAnimation in its composite, so the
    /// clip runs on the CAGEAnimation's clock and stays in step with the cutscene (<see cref="ClipSync"/>). One undo step.
    /// </summary>
    public partial class ClipSyncMenu : BaseWindow
    {
        /// <summary>
        /// Add "Sync to a CAGEAnimation..." to an entity context menu: shown when the entity under it (<paramref name="entity"/>, read as the menu
        /// opens) is a CMD_PlayAnimation; for a CHR_PlaySecondaryAnimation it says why it can't.
        /// </summary>
        public static void Attach(ContextMenuStrip menu, Func<Composite> composite, Func<Entity> entity)
        {
            if (menu == null) return;
            ToolStripMenuItem item = new ToolStripMenuItem("Sync to a CAGEAnimation...")
            {
                ToolTipText = "Run this clip on a CAGEAnimation's clock (its ExternalTime), so it stays in step with the cutscene's camera and props however the frame rate goes",
            };
            menu.Items.Add(item);
            Entity under = null;
            Composite at = null;
            menu.Opening += (s, e) =>
            {
                under = entity?.Invoke();
                at = composite?.Invoke();
                bool play = ClipSync.IsPlayAnimation(under), secondary = ClipSync.IsSecondaryAnimation(under);
                item.Visible = at != null && (play || secondary);
                item.Enabled = play;
                item.Text = secondary ? "Sync to a CAGEAnimation (CMD_PlayAnimation only)" : "Sync to a CAGEAnimation...";
            };
            item.Click += (s, e) =>
            {
                if (at == null || !ClipSync.IsPlayAnimation(under)) return;
                //not modal: the change runs as an editor step, which refuses to start under a modal window (McpEditor.ThrowIfUserModal)
                new ClipSyncMenu(at, under) { StartPosition = FormStartPosition.CenterScreen }.Show();
            };
        }

        private readonly Composite _composite;
        private readonly Entity _command;
        private readonly List<Entity> _cages;

        private ClipSyncMenu(Composite composite, Entity command) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            _composite = composite;
            _command = command;
            _cages = ClipSync.CageAnimations(composite);
            //docked throughout, so it lays out the same at any display scaling (fixed positions cut the buttons off at 150%)
            InitializeComponent();
            Commands commands = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
            string name = commands != null ? McpScript.EntityName(commands, composite, command) : "the command";

            Text = "Sync " + name + " to a CAGEAnimation";
            Icon = SharedFormIcon.Icon;
            introLabel.Text = "Links " + name + "'s ExternalTime to the CAGEAnimation: the clip's time is then the CAGEAnimation's time every frame, "
                + "so it can't drift behind on slow frames (a clip's own clock gains at most 1/24 s a frame).";

            Entity current = ClipSync.LinkedTo(composite, command, ClipSync.ExternalTime);
            foreach (Entity cage in _cages) _cageBox.Items.Add(commands != null ? McpScript.EntityName(commands, composite, cage) : cage.shortGUID.ToByteString());
            if (_cages.Count != 0) _cageBox.SelectedIndex = Math.Max(0, _cages.IndexOf(current));
            Entity start = ClipSync.LinkedTo(composite, command, ClipSync.ExternalStartTime);
            if (start != null && float.TryParse(McpGlobalAssetChecks.TextOf(start, "initial_value"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float was))
                _offset.Value = (decimal)Math.Max(0, Math.Min(36000, was));
            _unsync.Enabled = current != null;
            _sync.Enabled = _cages.Count != 0;
            Describe();
        }

        private void _cageBox_SelectedIndexChanged(object sender, EventArgs e) => Describe();
        private void _offset_ValueChanged(object sender, EventArgs e) => Describe();
        private void _sync_Click(object sender, EventArgs e) => Run(false);
        private void _unsync_Click(object sender, EventArgs e) => Run(true);
        private void _cancel_Click(object sender, EventArgs e) => Close();

        private Entity Cage { get { return _cageBox.SelectedIndex >= 0 && _cageBox.SelectedIndex < _cages.Count ? _cages[_cageBox.SelectedIndex] : null; } }

        private void Describe()
        {
            Commands commands = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
            if (_cages.Count == 0)
            {
                _notes.Text = "This composite has no CAGEAnimation. Links only join entities of one composite, so the CAGEAnimation has to be in here too.";
                return;
            }
            if (commands == null) { _notes.Text = ""; return; }
            Entity was = ClipSync.LinkedTo(_composite, _command, ClipSync.ExternalTime);
            List<string> notes = ClipSync.Notes(commands, _composite, _command, Cage, (float)_offset.Value);
            if (was != null) notes.Insert(0, "Now synced to " + McpScript.EntityName(commands, _composite, was) + ".");
            _notes.Text = string.Join("\r\n\r\n", notes);
        }

        private void Run(bool unsync)
        {
            try
            {
                ClipSync.Apply(_composite, _command, Cage, (float)_offset.Value, unsync, true, false);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (McpError ex)
            {
                MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
