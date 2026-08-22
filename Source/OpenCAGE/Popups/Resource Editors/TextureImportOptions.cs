using CATHODE;
using OpenCAGE.TextureTools;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static CATHODE.Textures;

namespace OpenCAGE
{
    /// <summary>
    /// Asked after a file is chosen for import: what format to store it in, how much of a mip chain
    /// to build, and whether to keep a smaller copy in the persistent slot.
    ///
    /// The format defaults to whatever is being replaced, so bringing a texture back in leaves the
    /// slot as the game shipped it. A brand new texture has nothing to follow, and defaults to BC7 -
    /// the format seven of every ten textures in the game already use.
    /// </summary>
    public class TextureImportOptions : Form
    {
        /// <summary>The format the user settled on.</summary>
        public TextureFormat Format { get; private set; }

        /// <summary>Mip levels to build: 0 for as many as the image allows, 1 for the top alone.</summary>
        public int MipLevels { get { return _mips.Value == _mips.Maximum ? 0 : _mips.Value; } }

        /// <summary>How many levels down the persistent copy sits, or 0 for no persistent copy.</summary>
        public int PersistentDrop { get { return _persistent.Checked ? _drop.Value : 0; } }

        /// <summary>Whether the streamed slot should be left empty, the way volume textures are stored.</summary>
        public bool PersistentOnly { get; private set; }

        private readonly ComboBox _format = new ComboBox();
        private readonly TrackBar _mips = new TrackBar();
        private readonly Label _mipLabel = new Label();
        private readonly CheckBox _persistent = new CheckBox();
        private readonly TrackBar _drop = new TrackBar();
        private readonly Label _dropLabel = new Label();

        private readonly int _width, _height, _sourceMips;

        public TextureImportOptions(string file, TextureFormat? replacing, TEX4 slot)
        {
            Text = replacing == null ? "Import texture" : "Replace texture";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(540, 266);
            Font = SystemFonts.MessageBoxFont;

            Shape(file, slot, out _width, out _height, out _sourceMips, out bool cube, out bool volume);
            PersistentOnly = volume;

            Label source = new Label { AutoEllipsis = true, Location = new Point(12, 12), Size = new Size(516, 20) };
            source.Text = Path.GetFileName(file) + (_width > 0 ? "   —   " + _width + " x " + _height : "");
            source.Font = new Font(source.Font, FontStyle.Bold);

            Controls.Add(source);
            Controls.Add(new Label { AutoSize = true, Location = new Point(12, 42), Text = "Store as:" });

            BuildFormatPicker(replacing);
            BuildMipSlider();
            BuildPersistentSlider(replacing, slot, cube, volume);

            //just below the persistent slider, which ends at 219
            Button ok = new Button { Text = replacing == null ? "Import" : "Replace", DialogResult = DialogResult.OK, Location = new Point(372, 231), Size = new Size(75, 23) };
            Button cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(453, 231), Size = new Size(75, 23) };

            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        #region CONTROLS

        private void BuildFormatPicker(TextureFormat? replacing)
        {
            _format.DropDownStyle = ComboBoxStyle.DropDownList;
            _format.Location = new Point(12, 62);
            _format.Size = new Size(516, 21);

            TextureFormat[] formats = TextureConverter.ImportFormats(Singleton.Platform).ToArray();
            foreach (TextureFormat format in formats) _format.Items.Add(TextureConverter.Describe(format));

            /* The replaced texture's own format leads. AUTO isn't a real format - a texture carrying
             * it has never been given one - so that falls through to the same default as a new one. */
            TextureFormat initial = replacing != null && replacing.Value != TextureFormat.AUTO
                ? replacing.Value : TextureFormat.BC7;
            int at = Array.IndexOf(formats, initial);
            _format.SelectedIndex = at >= 0 ? at : Math.Max(0, Array.IndexOf(formats, TextureFormat.BC7));
            Format = formats[_format.SelectedIndex];

            _format.SelectedIndexChanged += (s, e) => { Format = formats[_format.SelectedIndex]; };
            Controls.Add(_format);
        }

        private void BuildMipSlider()
        {
            /* A full chain runs down to a single pixel. Without the image's size to go on - a TGA or
             * an HDR that System.Drawing won't open - allow enough levels for anything up to 8192
             * and let the converter cap it.
             *
             * A DDS that already has a chain caps it lower: texconv keeps the mips an input carries
             * rather than rebuilding them, so a 256x256 that stops at 4x4 gives seven levels however
             * many are asked for. Better to offer seven than to promise nine and hand back seven. */
            int full = _width > 0 ? TextureConverter.FullChain(_width, _height) : 14;
            if (_sourceMips > 1) full = Math.Min(full, _sourceMips);

            Controls.Add(new Label { AutoSize = true, Location = new Point(12, 96), Text = "Mipmaps:" });

            _mips.Location = new Point(70, 90);
            _mips.Size = new Size(300, 45);
            _mips.Minimum = 1;
            _mips.Maximum = full;
            _mips.Value = full;
            _mips.TickFrequency = 1;
            _mips.TickStyle = TickStyle.BottomRight;
            _mips.ValueChanged += (s, e) => { UpdateMipLabel(); UpdateDropRange(); };

            _mipLabel.Location = new Point(376, 96);
            _mipLabel.Size = new Size(152, 32);

            Controls.Add(_mips);
            Controls.Add(_mipLabel);
            UpdateMipLabel();
        }

        private void BuildPersistentSlider(TextureFormat? replacing, TEX4 slot, bool cube, bool volume)
        {
            _persistent.AutoSize = true;
            _persistent.Location = new Point(14, 150);

            /* Replacing follows the slot: whatever split the game shipped is the one to keep. A new
             * texture follows its own shape - measured over every texture in the game, all 755
             * cubemaps are streamed only and all 76 volume textures are persistent only.
             *
             * Those two have no choice to offer, so the tick box says what is happening to them
             * instead of pretending otherwise. */
            bool forced = cube || volume;
            bool wanted;
            if (forced)
                wanted = volume;
            else if (replacing != null && slot != null)
                wanted = HasContent(slot.TexturePersistent) && HasContent(slot.TextureStreamed);
            else
                wanted = true;

            _persistent.Text = cube ? "Cubemap — the streamed slot only, as cubemaps have no persistent copy"
                             : volume ? "Volume texture — the persistent slot only, as the game stores these"
                             : "Keep a smaller copy in the persistent slot";
            _persistent.Checked = wanted;
            _persistent.Enabled = !forced;
            _persistent.CheckedChanged += (s, e) => { _drop.Enabled = _persistent.Checked && !forced; UpdateDropLabel(); };

            _drop.Location = new Point(70, 174);
            _drop.Size = new Size(300, 45);
            _drop.Minimum = 1;
            _drop.Maximum = Math.Max(1, _mips.Maximum - 1);
            _drop.TickFrequency = 1;
            _drop.TickStyle = TickStyle.BottomRight;
            _drop.Enabled = _persistent.Checked && !forced;

            /* Half size, as one level down. The game itself is usually smaller than that - 61% of its
             * persistent copies stop at 128 pixels and 90% are no bigger - but this is the least
             * surprising default, and the slider is right there. */
            _drop.Value = 1;
            if (replacing != null && slot != null && HasContent(slot.TexturePersistent) && HasContent(slot.TextureStreamed))
            {
                int had = slot.TextureStreamed.MipLevels - slot.TexturePersistent.MipLevels;
                if (had >= _drop.Minimum && had <= _drop.Maximum) _drop.Value = had;
            }
            _drop.ValueChanged += (s, e) => UpdateDropLabel();

            _dropLabel.Location = new Point(376, 180);
            _dropLabel.Size = new Size(152, 32);

            Controls.Add(_persistent);
            Controls.Add(_drop);
            Controls.Add(_dropLabel);
            UpdateDropLabel();
        }

        #endregion

        #region LABELS

        private void UpdateMipLabel()
        {
            int levels = _mips.Value;
            if (_width <= 0) { _mipLabel.Text = levels + (levels == 1 ? " level" : " levels"); return; }

            _mipLabel.Text = levels == 1
                ? "1 level, no mipmaps"
                : levels + " levels, down to "
                    + DdsFile.MipSize(_width, levels - 1) + " x " + DdsFile.MipSize(_height, levels - 1);
        }

        private void UpdateDropRange()
        {
            int max = Math.Max(1, _mips.Value - 1);
            if (_drop.Value > max) _drop.Value = max;
            _drop.Maximum = max;
            UpdateDropLabel();
        }

        private void UpdateDropLabel()
        {
            if (!_drop.Enabled) { _dropLabel.Text = _persistent.Checked ? "Full size" : "Streamed copy only"; return; }

            int drop = _drop.Value;
            string size = _width > 0
                ? DdsFile.MipSize(_width, drop) + " x " + DdsFile.MipSize(_height, drop)
                : "1/" + (1 << drop) + " size";

            _dropLabel.Text = size + "\r\n" + drop + (drop == 1 ? " level down" : " levels down");
        }

        #endregion

        /* What we can tell about the incoming file without converting it. A DDS says so in its
         * header; anything System.Drawing opens gives its size; a TGA or HDR gives neither, and the
         * sliders fall back to working in ratios. */
        private static void Shape(string file, TEX4 slot, out int width, out int height, out int mips, out bool cube, out bool volume)
        {
            width = height = mips = 0;
            cube = volume = false;

            try
            {
                byte[] head = ReadHead(file, 256);
                if (DdsFile.Describe(head, out DirectXTex.DirectXTexUtility.DXGI_FORMAT _, out width, out height, out mips, out cube))
                {
                    volume = false;
                    return;
                }
            }
            catch { }

            try
            {
                using (Image image = Image.FromFile(file))
                {
                    width = image.Width;
                    height = image.Height;
                }
            }
            catch { }

            /* A flat image can't say what shape the slot wants, so the slot says instead. */
            if (slot != null)
            {
                cube = slot.StateFlags.HasFlag(TextureStateFlag.CUBE);
                volume = slot.StateFlags.HasFlag(TextureStateFlag.VOLUME)
                      || (slot.TexturePersistent?.Depth ?? 1) > 1 || (slot.TextureStreamed?.Depth ?? 1) > 1;
            }
        }

        private static byte[] ReadHead(string file, int count)
        {
            using (FileStream stream = File.OpenRead(file))
            {
                byte[] head = new byte[Math.Min(count, (int)Math.Min(int.MaxValue, stream.Length))];
                stream.Read(head, 0, head.Length);
                return head;
            }
        }

        private static bool HasContent(TEX4.Texture part)
        {
            return part?.Content != null && part.Content.Length != 0;
        }
    }
}
