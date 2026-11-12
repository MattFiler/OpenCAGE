using CATHODE;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// The model import window's texture row: a Compact Textures box (colour maps as DXT1, or DXT5 where the diffuse alpha cuts out,
    /// instead of BC7) and what the import adds against the game's texture heap (<see cref="TextureBudget"/>). The choice is
    /// remembered for the session.
    /// </summary>
    public sealed class TextureBudgetImportRow : Panel
    {
        private static bool _lastChoice;

        private readonly Level _level;
        private readonly Func<IEnumerable<MaterialGenerator.Plan>> _plans;
        private readonly CheckBox _compact;
        private readonly Label _estimate;
        private TextureBudget.Estimate _levelNow;

        /// <summary>The row for an import into <paramref name="level"/> whose generated materials are <paramref name="plans"/>; null without a level.</summary>
        public static TextureBudgetImportRow For(Level level, Func<IEnumerable<MaterialGenerator.Plan>> plans)
        {
            if (level?.Textures == null || plans == null) return null;
            //Nothing to generate (the model brings its own materials): no textures to compact
            if (!(plans() ?? Enumerable.Empty<MaterialGenerator.Plan>()).Any(o => o != null && o.UsableTextures.Any(t => t.Existing == null))) return null;
            return new TextureBudgetImportRow(level, plans);
        }

        private TextureBudgetImportRow(Level level, Func<IEnumerable<MaterialGenerator.Plan>> plans)
        {
            _level = level;
            _plans = plans;
            Dock = DockStyle.Top;
            Height = 46;
            Padding = new Padding(0, 4, 0, 0);
            _compact = new CheckBox()
            {
                Text = "Compact textures (DXT1 instead of BC7: half the texture memory)",
                AutoSize = true,
                Location = new Point(0, 4),
                Checked = _lastChoice,
            };
            _compact.CheckedChanged += (s, e) => { _lastChoice = _compact.Checked; UpdateEstimate(); };
            new ToolTip().SetToolTip(_compact, "For big texture sets (a whole level): the colour, specular, dirt and AO maps are encoded DXT1 - DXT5 where the base colour's alpha cuts the surface out - as most of the game's own world textures are. Normal maps stay DXN.");
            _estimate = new Label() { AutoSize = false, Location = new Point(18, 24), Size = new Size(900, 18), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right };
            Controls.Add(_compact);
            Controls.Add(_estimate);
            HandleCreated += (s, e) => BeginInvoke(new Action(UpdateEstimate));
        }

        /// <summary>Set the box's choice on the plans about to be generated (the window calls this just before it generates).</summary>
        public void ApplyTo(IEnumerable<MaterialGenerator.Plan> plans)
        {
            foreach (MaterialGenerator.Plan plan in plans ?? Enumerable.Empty<MaterialGenerator.Plan>())
                if (plan != null) plan.CompactTextures = _compact.Checked;
        }

        public bool Compact => _compact.Checked;

        /* What the plans' new textures will stream: each image's pixels at the format it would be encoded in, with its mips, as the game
           counts it - an estimate (images already in the level, and ones that can't be read, add nothing) */
        private void UpdateEstimate()
        {
            try
            {
                if (_levelNow == null) _levelNow = TextureBudget.Of(_level);
                long bc7 = 0, compact = 0;
                int unknown = 0;
                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (MaterialGenerator.Plan plan in _plans() ?? Enumerable.Empty<MaterialGenerator.Plan>())
                {
                    if (plan == null) continue;
                    foreach (MaterialGenerator.PlannedTexture texture in plan.UsableTextures)
                    {
                        if (texture.Existing != null) continue;
                        if (texture.SourcePath != null && !seen.Add(texture.SourcePath + "|" + texture.Role.Format)) continue;
                        Size? size = SizeOf(texture);
                        if (size == null) { unknown++; continue; }
                        double pixels = (double)size.Value.Width * size.Value.Height * 4.0 / 3.0;
                        bool bc7Colour = texture.Role.Format == Textures.TextureFormat.BC7 && texture.Role.Sampler != "SEPARATE_ALPHA_MAP";
                        double full = pixels * BytesPerPixel(texture.Role.Format);
                        double small = bc7Colour ? pixels * 0.5 : full;
                        bc7 += (long)(full * TextureBudget.GameFactor);
                        compact += (long)(small * TextureBudget.GameFactor);
                    }
                }
                long adds = _compact.Checked ? compact : bc7;
                double after = (_levelNow.PeakBytes + adds) / 1048576.0;
                int heap = _levelNow.HeapMB;
                string text = "Adds about " + Mb(adds) + " of streamed textures" + (bc7 != compact ? " (" + Mb(bc7) + " as BC7, " + Mb(compact) + " compact)" : "") +
                              (unknown != 0 ? ", plus " + unknown + " image" + (unknown == 1 ? "" : "s") + " of unknown size" : "") +
                              ". The level would need about " + after.ToString("0") + " of the game's " + heap + " MB texture heap at once" +
                              (after > heap ? " - over it: textures will draw blocky (File > Level Check)." : after > heap * 0.85 ? " - close to full." : ".");
                _estimate.Text = text;
                _estimate.ForeColor = after > heap ? (Theming.ThemeManager.IsDark ? Color.FromArgb(255, 120, 110) : Color.Firebrick) : SystemColors.ControlText;
                if (Theming.ThemeManager.IsDark && after <= heap) _estimate.ForeColor = ForeColor;
            }
            catch (Exception e)
            {
                _estimate.Text = "";
                Debug.Log("LevelCheck", "Could not estimate the import's texture memory: " + e.Message);
            }
        }

        private static string Mb(long bytes) => (bytes / 1048576.0).ToString(bytes < 10 * 1048576 ? "0.0" : "0") + " MB";

        private static double BytesPerPixel(Textures.TextureFormat format)
        {
            string name = format.ToString();
            if (name.Contains("DXT1") || name.Contains("BC1") || name.Contains("BC4") || name.Contains("ATI1")) return 0.5;
            if (name.Contains("DXT") || name.Contains("BC") || name.Contains("DXN") || name.Contains("ATI2")) return 1.0;
            return 4.0;
        }

        /* An image's size from its header, without decoding it (GDI+ reads PNG, JPEG, BMP, GIF, TIFF); null when it can't */
        private static Size? SizeOf(MaterialGenerator.PlannedTexture texture)
        {
            try
            {
                if (texture.Embedded != null)
                {
                    if (!texture.Embedded.HasCompressedData) return new Size(texture.Embedded.Width, texture.Embedded.Height);
                    using (MemoryStream stream = new MemoryStream(texture.Embedded.CompressedData))
                    using (Image image = Image.FromStream(stream, false, false))
                        return image.Size;
                }
                if (texture.SourcePath == null || !File.Exists(texture.SourcePath)) return null;
                using (FileStream stream = File.OpenRead(texture.SourcePath))
                using (Image image = Image.FromStream(stream, false, false))
                    return image.Size;
            }
            catch
            {
                return null;
            }
        }
    }
}
