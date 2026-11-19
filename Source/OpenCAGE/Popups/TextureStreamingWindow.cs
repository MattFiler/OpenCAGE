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
    public partial class TextureStreamingWindow : Form
    {
        private readonly Level _level;
        private List<TextureStreaming.DuplicateGroup> _groups = new List<TextureStreaming.DuplicateGroup>();

        public TextureStreamingWindow(Level level)
        {
            InitializeComponent();
            _level = level;
            Icon = SharedFormIcon.Icon;

            foreach (int edge in new[] { 64, 128, 256, 512 }) _edge.Items.Add(edge);
            _edge.SelectedItem = TextureStreaming.DefaultKeepLoadedEdge;
            //Wired after the first pick, which is not a change
            _edge.SelectedIndexChanged += Edge_SelectedIndexChanged;
        }

        private void TextureStreamingWindow_Shown(object sender, EventArgs e) => Reload(true);

        private void Edge_SelectedIndexChanged(object sender, EventArgs e) => UpdateKeep();

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
