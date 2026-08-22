using AlienPAK;
using CATHODE;
using CathodeLib.ObjectExtensions;
using OpenCAGE.TextureTools;
using System;
using System.Drawing;
using System.Windows.Forms;
using static CATHODE.Textures;

namespace OpenCAGE
{
    /// <summary>
    /// The mip level slider under each preview.
    ///
    /// A texture is stored as a chain of progressively smaller copies, and until now only the first
    /// was ever visible - which is the one that tells you least about how the thing was authored.
    /// Half the reason to look at a texture in the first place is to check its lower mips.
    ///
    /// Built here rather than in the designer so the generated layout stays as the designer wrote it.
    /// </summary>
    public partial class EditTexture
    {
        private TrackBar _streamedMip, _persistentMip;
        private Label _streamedMipLabel, _persistentMipLabel;
        private bool _suppressMipChange;

        /// <summary>
        /// Put a freshly converted image into a texture's two slots.
        ///
        /// The persistent copy is the one that stays in memory, so it is meant to be the smaller of
        /// the two - and in the shipped data it is never a separately compressed image, just the
        /// streamed chain starting a few levels in. Slicing is checked against all 15,345 textures
        /// that carry both parts and reproduces every one of them byte for byte, so producing one is
        /// exact rather than approximate.
        /// </summary>
        /// <param name="drop">Levels down for the persistent copy, or 0 for no persistent copy.</param>
        /// <param name="persistentOnly">Leave the streamed slot empty, the way volume textures are stored.</param>
        private static void ApplyParts(Textures.TEX4 texture, Textures.TEX4.Texture part, int drop, bool persistentOnly)
        {
            if (persistentOnly)
            {
                texture.TextureStreamed = new Textures.TEX4.Texture();
                texture.TexturePersistent = part.Copy();
                return;
            }

            texture.TextureStreamed = part.Copy();

            if (drop <= 0)
            {
                texture.TexturePersistent = new Textures.TEX4.Texture();
                return;
            }

            /* If the chain is too short to slice - a single level, or an odd format whose layout
             * isn't known - fall back to the same image in both slots rather than losing it. */
            Textures.TEX4.Texture smaller = TextureConverter.Slice(part, texture.Format, drop);
            texture.TexturePersistent = smaller ?? part.Copy();
        }

        /// <summary>Put a slider between each preview and its metadata box. Called once, on load.</summary>
        private void BuildMipBars()
        {
            if (_streamedMip != null) return;

            AddMipBar(streamedTabLayout, groupStreamedMeta, out _streamedMip, out _streamedMipLabel);
            AddMipBar(persistentTabLayout, groupPersistentMeta, out _persistentMip, out _persistentMipLabel);

            _streamedMip.ValueChanged += (s, e) => ShowMip(pictureStreamed, _streamedMip, _streamedMipLabel, true);
            _persistentMip.ValueChanged += (s, e) => ShowMip(picturePersistent, _persistentMip, _persistentMipLabel, false);
        }

        private void AddMipBar(TableLayoutPanel layout, Control below, out TrackBar bar, out Label label)
        {
            /* The tab is a two row table - preview on top, metadata underneath. Push the metadata
             * down a row and slot the slider into the gap. */
            layout.RowCount = 3;
            layout.RowStyles.Insert(1, new RowStyle(SizeType.Absolute, 30F));
            layout.SetRow(below, 2);

            Panel host = new Panel { Dock = DockStyle.Fill, Margin = new Padding(3, 0, 3, 0) };

            label = new Label
            {
                Dock = DockStyle.Right,
                Width = 190,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "",
            };
            bar = new TrackBar
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 0,
                TickStyle = TickStyle.BottomRight,
                SmallChange = 1,
                LargeChange = 1,
            };

            host.Controls.Add(bar);
            host.Controls.Add(label);
            layout.Controls.Add(host, 0, 1);
        }

        /// <summary>
        /// Point the sliders at a texture's two parts. A cubemap is left out of it: its content is
        /// six chains one after another rather than one, so a level can't be picked out of it the
        /// same way - and the six-face strip is what the preview shows anyway.
        /// </summary>
        private void UpdateMipBars(Textures.TEX4 texture)
        {
            if (_streamedMip == null) return;

            bool cube = texture != null && texture.StateFlags.HasFlag(TextureStateFlag.CUBE);
            SetUpMipBar(_streamedMip, _streamedMipLabel, cube ? null : texture?.TextureStreamed, texture);
            SetUpMipBar(_persistentMip, _persistentMipLabel, cube ? null : texture?.TexturePersistent, texture);
        }

        private void SetUpMipBar(TrackBar bar, Label label, Textures.TEX4.Texture part, Textures.TEX4 texture)
        {
            _suppressMipChange = true;
            try
            {
                int levels = part?.Content == null || part.Content.Length == 0 ? 0 : Math.Max(1, (int)part.MipLevels);

                bar.Enabled = levels > 1;
                bar.Maximum = Math.Max(0, levels - 1);
                bar.Value = 0;
                bar.TickFrequency = 1;

                label.Text = levels == 0 ? ""
                    : levels == 1 ? "1 level, " + part.Width + " x " + part.Height
                    : "Mip 0 of " + (levels - 1) + "   —   " + part.Width + " x " + part.Height;
            }
            finally { _suppressMipChange = false; }
        }

        /* Decode the level the slider is on and put it in the preview. */
        private void ShowMip(PictureBox box, TrackBar bar, Label label, bool streamed)
        {
            if (_suppressMipChange || _selectedTexture == null) return;

            Textures.TEX4 texture = _selectedTexture;
            Textures.TEX4.Texture part = streamed ? texture.TextureStreamed : texture.TexturePersistent;
            if (part?.Content == null || part.Content.Length == 0) return;

            int levels = Math.Max(1, (int)part.MipLevels);
            int level = Math.Min(bar.Value, levels - 1);

            Cursor = Cursors.WaitCursor;
            try
            {
                Textures.TEX4.Texture one = TextureConverter.Level(part, texture.Format, level);
                if (one == null)
                {
                    label.Text = "Mip " + level + " could not be read";
                    return;
                }

                AssignPreviewImage(box, texture.ToBitmap(one));
                label.Text = "Mip " + level + " of " + (levels - 1) + "   —   " + one.Width + " x " + one.Height;
            }
            catch
            {
                AssignPreviewImage(box, null);
                label.Text = "Mip " + level + " could not be decoded";
            }
            finally { Cursor = Cursors.Default; }
        }
    }
}
