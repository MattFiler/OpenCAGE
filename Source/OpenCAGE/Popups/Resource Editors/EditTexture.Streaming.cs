using CATHODE;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// "Keep loaded (never streamed)" for the selected texture, and the level-wide Texture Streaming window (the game holds about 1,020
    /// streamed textures at full size at once; past that they draw blurry). Built in code, like the mip bars, to leave the designer layout alone.
    /// </summary>
    public partial class EditTexture
    {
        private CheckBox _keepLoaded;
        private Label _keepLoadedCost;
        private bool _suppressKeepLoaded;

        /// <summary>The check box under the state flags, and a Texture Streaming... button under Export All. Called once, on load.</summary>
        private void BuildStreamingControls()
        {
            if (_keepLoaded != null) return;

            Label caption = new Label { AutoSize = true, Text = "Streaming:", Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 6, 0, 2) };
            _keepLoaded = new CheckBox { AutoSize = true, Text = "Keep loaded (never streamed)", Margin = new Padding(0, 0, 8, 0), Enabled = false };
            _keepLoadedCost = new Label { AutoSize = true, Text = "", ForeColor = SystemColors.GrayText, Margin = new Padding(16, 0, 0, 2) };
            new ToolTip().SetToolTip(_keepLoaded, "The game streams a texture's full-size image in when it is drawn, and holds only about 1,020 at once (Texture Streaming says how many this level asks for). " +
                "A texture kept loaded has its whole image in its persistent part instead, as the game's own effect textures do: it never streams, is never drawn blurry and takes none of those slots - " +
                "but its memory stays taken for the whole level. Good for a sky, or many small textures.");
            _keepLoaded.CheckedChanged += KeepLoaded_CheckedChanged;
            stateFlagsPanel.Controls.Add(caption);
            stateFlagsPanel.Controls.Add(_keepLoaded);
            stateFlagsPanel.Controls.Add(_keepLoadedCost);

            Button window = new Button { Text = "Texture Streaming...", Dock = DockStyle.Fill, UseVisualStyleBackColor = true };
            new ToolTip().SetToolTip(window, "How many textures this level streams at once against what the game can hold, and keeping small textures loaded or merging identical ones to fit.");
            window.Click += (s, e) =>
            {
                if (Content?.Level == null) return;
                using (TextureStreamingWindow streaming = new TextureStreamingWindow(Content.Level))
                    streaming.ShowDialog(this);
                RefreshTexturePreviewFromSelection();
            };
            leftMainLayout.RowCount += 1;
            leftMainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            leftMainLayout.Controls.Add(window, 0, leftMainLayout.RowCount - 1);
        }

        /// <summary>Show the selected texture's streaming (null: nothing selected).</summary>
        private void UpdateStreamingControls(Textures.TEX4 texture)
        {
            if (_keepLoaded == null) return;
            _suppressKeepLoaded = true;
            try
            {
                bool kept = TextureStreaming.IsKeptLoaded(texture);
                string why = texture == null ? "" : kept ? null : TextureStreaming.CannotKeepLoaded(texture);
                bool volume = texture != null && (texture.StateFlags.HasFlag(Textures.TextureStateFlag.VOLUME) || (texture.TexturePersistent?.Depth ?? 1) > 1);
                _keepLoaded.Checked = kept;
                _keepLoaded.Enabled = texture != null && _activeTextures != null && (kept ? !volume : why == null);
                _keepLoadedCost.Text = texture == null ? ""
                    : kept ? (volume ? "volume textures are always loaded" : "never streams; " + Mb(texture.TexturePersistent.Content.LongLength) + " kept in memory")
                    : why != null ? "can't: " + why
                    : "streams; keeping it loaded holds " + Mb(TextureStreaming.KeepLoadedCost(texture)) + " more";
            }
            finally { _suppressKeepLoaded = false; }
        }

        private static string Mb(long bytes) => bytes >= 1048576 ? (bytes / 1048576.0).ToString("0.0") + " MB" : (bytes / 1024.0).ToString("0") + " KB";

        private void KeepLoaded_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressKeepLoaded || _selectedTexture == null || Content?.Level == null) return;
            Textures.TEX4 texture = _selectedTexture;
            bool keep = _keepLoaded.Checked;
            int done = TextureStreaming.SetKeptLoaded(Content.Level, new[] { texture }, keep, (keep ? "Keep texture loaded: " : "Stream texture again: ") + texture.Name, out List<string> problems);
            if (done == 0 && problems.Count != 0)
                MessageBox.Show(string.Join("\n", problems), "Texture streaming", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshTexturePreviewFromSelection();
        }
    }
}
