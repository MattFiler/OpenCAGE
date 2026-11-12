using CATHODE;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Texture Editor > Texture Streaming...: how many of the level's textures stream at once against the about 1,020 the game holds at
    /// full size (TextureStreaming), and two ways to fit: keep small textures loaded, or merge identical textures. Each is one undo step.
    /// </summary>
    public class TextureStreamingWindow : Form
    {
        private readonly Level _level;
        private readonly Label _summary = new Label();
        private readonly ComboBox _edge = new ComboBox();
        private readonly Label _keepCost = new Label();
        private readonly Button _keep = new Button();
        private readonly Button _merge = new Button();
        private List<TextureStreaming.DuplicateGroup> _groups = new List<TextureStreaming.DuplicateGroup>();

        public TextureStreamingWindow(Level level)
        {
            _level = level;
            Text = "Texture Streaming";
            Icon = SharedFormIcon.Icon;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = SystemFonts.MessageBoxFont;
            ClientSize = new Size(560, 330);

            _summary.Location = new Point(12, 12);
            _summary.Size = new Size(536, 170);
            Controls.Add(_summary);

            Controls.Add(new Label { AutoSize = true, Location = new Point(12, 196), Text = "Keep textures up to" });
            _edge.DropDownStyle = ComboBoxStyle.DropDownList;
            _edge.Location = new Point(130, 192);
            _edge.Size = new Size(70, 21);
            foreach (int edge in new[] { 64, 128, 256, 512 }) _edge.Items.Add(edge);
            _edge.SelectedItem = TextureStreaming.DefaultKeepLoadedEdge;
            _edge.SelectedIndexChanged += (s, e) => UpdateKeep();
            Controls.Add(_edge);
            Controls.Add(new Label { AutoSize = true, Location = new Point(206, 196), Text = "px loaded" });
            _keepCost.Location = new Point(12, 220);
            _keepCost.Size = new Size(400, 34);
            Controls.Add(_keepCost);
            _keep.Text = "Keep Loaded";
            _keep.Location = new Point(428, 191);
            _keep.Size = new Size(120, 24);
            _keep.Click += Keep_Click;
            Controls.Add(_keep);

            _merge.Location = new Point(12, 262);
            _merge.Size = new Size(536, 24);
            _merge.Click += Merge_Click;
            Controls.Add(_merge);

            Button close = new Button { Text = "Close", DialogResult = DialogResult.Cancel, Location = new Point(473, 296), Size = new Size(75, 23) };
            Controls.Add(close);
            CancelButton = close;

            Shown += (s, e) => Reload(true);
        }

        private int Edge => _edge.SelectedItem is int edge ? edge : TextureStreaming.DefaultKeepLoadedEdge;

        private void Reload(bool duplicates)
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                TextureStreaming.SlotEstimate e = TextureSlotsRule.EstimateWithZones(_level, false);
                if (duplicates) _groups = TextureStreaming.FindDuplicates(_level);
                int copies = _groups.Sum(o => o.Copies.Count(TextureStreaming.Streams));
                bool over = e.Peak > TextureStreaming.SafeStreamedAtOnce, close = e.Peak > TextureSlotsRule.WarnAt;
                _summary.Text =
                    "The game streams a texture's full-size image when it is drawn, and holds about " + (TextureStreaming.StreamerBlocks - 4) + " at once however much of its texture heap is free; " +
                    "past that, textures are drawn from their small copies - blurry" + (e.OneZone ? ", for the whole level in a level with one zone." : ".") + "\r\n\r\n" +
                    "This level: " + e.Streamed + " streamed textures, " + e.KeptLoaded + " kept loaded.\r\n" +
                    "At once: up to " + e.Peak + " " + e.PeakWhere + ".\r\n" +
                    (over ? "That is more than the game can hold: some textures will stay blurry." : close ? "That is close to what the game can hold (the player, weapons and effects take some too)." : "That fits.") + "\r\n\r\n" +
                    "Keeping a texture loaded moves its whole image into memory for good: it never streams and is never blurry. It suits small textures, and a sky. " +
                    (copies > 0 ? "Identical textures under different names each stream on their own." : "");
                _summary.ForeColor = over ? Color.Firebrick : SystemColors.ControlText;
                _merge.Text = copies > 0 ? "Merge " + _groups.Sum(o => o.Copies.Count) + " identical textures (" + copies + " streamed): point their materials at one copy" : "No identical textures to merge";
                _merge.Enabled = _groups.Count > 0;
                UpdateKeep();
            }
            finally { Cursor = Cursors.Default; }
        }

        private void UpdateKeep()
        {
            List<Textures.TEX4> chosen = TextureStreaming.Select(_level.Textures.Entries, null, null, Edge, true);
            long bytes = chosen.Sum(TextureStreaming.KeepLoadedCost);
            _keepCost.Text = chosen.Count == 0 ? "No streamed textures that small." : chosen.Count + " streamed textures; " + (bytes / 1048576.0).ToString("0.0") + " MB more kept in memory (the game is a 32-bit program: keep it to tens of MB).";
            _keep.Enabled = chosen.Count > 0;
        }

        private void Keep_Click(object sender, EventArgs e)
        {
            List<Textures.TEX4> chosen = TextureStreaming.Select(_level.Textures.Entries, null, null, Edge, true);
            if (chosen.Count == 0) return;
            int done = TextureStreaming.SetKeptLoaded(_level, chosen, true, "Keep " + chosen.Count + " textures up to " + Edge + " px loaded", out List<string> problems);
            if (problems.Count != 0)
                MessageBox.Show(done + " kept loaded; " + problems.Count + " could not be:\n\n" + string.Join("\n", problems.Take(15)), "Texture Streaming", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Reload(false);
        }

        private void Merge_Click(object sender, EventArgs e)
        {
            if (_groups.Count == 0) return;
            int moved = TextureStreaming.MergeDuplicates(_level, _groups, "Merge " + _groups.Sum(o => o.Copies.Count) + " identical textures");
            _groups = new List<TextureStreaming.DuplicateGroup>();
            Reload(true);
            MessageBox.Show(moved + " material texture slots now use one copy of each image. The copies stay in the level, unused (they no longer stream).", "Texture Streaming", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
