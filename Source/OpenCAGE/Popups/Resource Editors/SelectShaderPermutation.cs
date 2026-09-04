using CATHODE;
using CATHODE.ShaderTypes;
using CathodeLib.Ubershaders;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Picks one of the feature combinations that already exist for a shader family.
    ///
    /// Shown for families CathodeLib carries no reconstructed master for. Those cannot have a new
    /// permutation compiled for them, so their features are not free checkboxes - the only masks
    /// obtainable are the ones the game already shipped, and this lists them.
    ///
    /// The feature list on the left filters rather than sets: each feature cycles Any -> Must be on
    /// -> Must be off, and the right hand list narrows to the combinations that answer. That is the
    /// closest thing to "tick the features you want" that a fixed pool of permutations can honestly
    /// offer.
    /// </summary>
    public class SelectShaderPermutation : Form
    {
        /// <summary>The feature mask the user settled on.</summary>
        public long Mask { get; private set; }

        private enum Want { Any, On, Off }

        private readonly SHADER_LIST _family;
        private readonly long _currentMask;
        private readonly Materials.Material _material;
        private readonly List<ShaderPermutationService.Permutation> _all;
        private readonly List<Tuple<string, int>> _features = new List<Tuple<string, int>>();
        private readonly Dictionary<int, Want> _filter = new Dictionary<int, Want>();
        private readonly Dictionary<int, CheckBox> _filterBoxes = new Dictionary<int, CheckBox>();
        private readonly List<ShaderPermutationService.Permutation> _shown = new List<ShaderPermutationService.Permutation>();

        private readonly ListView _list = new ListView();
        private readonly Label _status = new Label();
        private readonly Button _ok = new Button();
        private readonly Panel _filterPanel = new Panel();

        public SelectShaderPermutation(SHADER_LIST family, long currentMask, List<ShaderPermutationService.Permutation> permutations,
                                       Materials.Material material = null)
        {
            _family = family;
            _currentMask = currentMask;
            _material = material;
            Mask = currentMask;
            _all = permutations ?? new List<ShaderPermutationService.Permutation>();

            foreach (string feature in ShaderUtility.GetFeatures(family) ?? new List<string>())
            {
                int? bit = ShaderUtility.GetShaderFunctionalityIndex(family, ShaderIndexType.FEATURES, feature);
                if (bit.HasValue)
                {
                    _features.Add(new Tuple<string, int>(feature, bit.Value));
                    _filter[bit.Value] = Want.Any;
                }
            }
            MatchCurrent();

            Text = "Choose a feature combination";
            Icon = SharedFormIcon.Icon;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            Font = SystemFonts.MessageBoxFont;
            ClientSize = new Size(900, 580);
            MinimumSize = new Size(720, 420);

            Label intro = new Label
            {
                Text = EditMaterial.FixedFeatureReason(family)
                     + " so no new permutation can be compiled for " + family + "."
                     + " These are the " + _all.Count + " combination" + (_all.Count == 1 ? "" : "s")
                     + " your game data already ships - pick one.",
                Location = new Point(12, 10),
                Size = new Size(876, 32),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = SystemColors.GrayText
            };

            Label filterLabel = new Label
            {
                Text = "Features wanted  (right click = don't care)",
                Location = new Point(12, 48),
                Size = new Size(280, 18),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            _filterPanel.Location = new Point(12, 68);
            _filterPanel.Size = new Size(288, 452);
            _filterPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            _filterPanel.AutoScroll = true;
            _filterPanel.BorderStyle = BorderStyle.FixedSingle;
            BuildFilterBoxes();

            Button clear = new Button
            {
                Text = "Clear filters",
                Location = new Point(12, 526),
                Size = new Size(92, 26),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            clear.Click += (s, e) =>
            {
                foreach (Tuple<string, int> f in _features) _filter[f.Item2] = Want.Any;
                RepaintFilters();
                Refill();
            };

            Button matchCurrent = new Button
            {
                Text = "Match current",
                Location = new Point(110, 526),
                Size = new Size(100, 26),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            matchCurrent.Click += (s, e) => { MatchCurrent(); RepaintFilters(); Refill(); };

            _list.Location = new Point(308, 68);
            _list.Size = new Size(580, 452);
            _list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _list.View = View.Details;
            _list.FullRowSelect = true;
            _list.MultiSelect = false;
            _list.HideSelection = false;
            _list.Columns.Add("Features", 270);
            _list.Columns.Add("Mask", 90);
            _list.Columns.Add("Source", 80);
            _list.Columns.Add("Used by", 55, HorizontalAlignment.Right);
            _list.Columns.Add("Needs", 80);
            _list.SelectedIndexChanged += (s, e) => Revalidate();
            _list.DoubleClick += (s, e) => { if (_ok.Enabled) { DialogResult = DialogResult.OK; Close(); } };

            _status.Location = new Point(308, 526);
            _status.Size = new Size(400, 30);
            _status.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _status.ForeColor = SystemColors.GrayText;

            _ok.Text = "Use this";
            _ok.DialogResult = DialogResult.OK;
            _ok.Location = new Point(732, 526);
            _ok.Size = new Size(75, 26);
            _ok.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            Button cancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(813, 526),
                Size = new Size(75, 26),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            Controls.Add(intro);
            Controls.Add(filterLabel);
            Controls.Add(_filterPanel);
            Controls.Add(clear);
            Controls.Add(matchCurrent);
            Controls.Add(_list);
            Controls.Add(_status);
            Controls.Add(_ok);
            Controls.Add(cancel);
            AcceptButton = _ok;
            CancelButton = cancel;

            Refill();
        }

        /* Open on exactly what the material already is: the features it has ticked, the ones it hasn't
         * unticked. That means the list starts on the one combination it is already bound to, and the
         * window is used by changing the features you want and seeing what still answers - which is
         * how someone thinks about it, rather than "filter a pool of 600". The status line says when
         * only the current one matches, and Clear filters opens it all up. */
        private void MatchCurrent()
        {
            foreach (Tuple<string, int> feature in _features)
                _filter[feature.Item2] = (_currentMask & (1L << feature.Item2)) != 0 ? Want.On : Want.Off;
        }

        private void RepaintFilters()
        {
            foreach (KeyValuePair<int, CheckBox> box in _filterBoxes)
                PaintFilter(box.Value, _filter[box.Key]);
        }

        /* The boxes open showing the material's own features, so they have to behave like the ordinary
         * checkboxes they look like: a click ticks or unticks, and that is the permutation being asked
         * for. The third state - don't care - is still worth having when nothing shipped matches, so it
         * is on the right button rather than in a cycle that would make ticking take two clicks. */
        private readonly ToolTip toolTip = new ToolTip();

        private void SetFilter(int bit, CheckBox box, Want want)
        {
            _filter[bit] = want;
            PaintFilter(box, want);
            Refill();
        }

        private void BuildFilterBoxes()
        {
            int y = 6;
            foreach (Tuple<string, int> feature in _features)
            {
                int bit = feature.Item2;
                CheckBox box = new CheckBox
                {
                    Text = feature.Item1,
                    Location = new Point(8, y),
                    Size = new Size(258, 20),
                    ThreeState = true,
                    AutoCheck = false,
                    CheckState = CheckState.Indeterminate
                };
                PaintFilter(box, _filter[bit]);
                box.Click += (s, e) => SetFilter(bit, box, _filter[bit] == Want.On ? Want.Off : Want.On);
                box.MouseUp += (s, e) => { if (e.Button == MouseButtons.Right) SetFilter(bit, box, Want.Any); };
                toolTip.SetToolTip(box, "Click to tick or untick. Right click for \"don't care\", which lets a "
                                      + "combination match whether it has this feature or not.");
                _filterBoxes[bit] = box;
                _filterPanel.Controls.Add(box);
                y += 22;
            }

            if (_features.Count == 0)
                _filterPanel.Controls.Add(new Label
                {
                    Text = "This shader family declares no features.",
                    Location = new Point(8, 6),
                    Size = new Size(258, 32),
                    ForeColor = SystemColors.GrayText
                });
        }

        private static void PaintFilter(CheckBox box, Want want)
        {
            switch (want)
            {
                case Want.On:
                    box.CheckState = CheckState.Checked;
                    box.ForeColor = SystemColors.ControlText;
                    box.Font = new Font(box.Font, FontStyle.Bold);
                    break;
                case Want.Off:
                    box.CheckState = CheckState.Unchecked;
                    box.ForeColor = SystemColors.ControlText;
                    box.Font = new Font(box.Font, FontStyle.Strikeout);
                    break;
                default:
                    box.CheckState = CheckState.Indeterminate;
                    box.ForeColor = SystemColors.GrayText;
                    box.Font = new Font(box.Font, FontStyle.Regular);
                    break;
            }
        }

        private bool Matches(long mask)
        {
            foreach (KeyValuePair<int, Want> kv in _filter)
            {
                if (kv.Value == Want.Any) continue;
                bool set = (mask & (1L << kv.Key)) != 0;
                if (kv.Value == Want.On && !set) return false;
                if (kv.Value == Want.Off && set) return false;
            }
            return true;
        }

        private string DescribeFeatures(long mask)
        {
            List<string> on = new List<string>();
            foreach (Tuple<string, int> feature in _features)
                if ((mask & (1L << feature.Item2)) != 0)
                    on.Add(feature.Item1);
            return on.Count == 0 ? "(no features)" : string.Join(", ", on.ToArray());
        }

        private void Refill()
        {
            _shown.Clear();
            foreach (ShaderPermutationService.Permutation p in _all)
                if (Matches(p.Mask))
                    _shown.Add(p);

            _list.BeginUpdate();
            _list.Items.Clear();
            int selectIndex = -1;
            for (int i = 0; i < _shown.Count; i++)
            {
                ShaderPermutationService.Permutation p = _shown[i];
                bool current = p.Mask == _currentMask;
                ListViewItem item = new ListViewItem(DescribeFeatures(p.Mask) + (current ? "   (current)" : ""));
                item.SubItems.Add("0x" + p.Mask.ToString("X"));
                item.SubItems.Add(p.Source == PermutationSource.LevelPool ? "This level" : "Game data");
                item.SubItems.Add(p.MaterialUses == 0 ? "" : p.MaterialUses.ToString());

                /* A permutation samples maps this material may not have, and binding it anyway is what
                 * leaves a material declaring features it cannot feed - which renders wrongly rather
                 * than failing. Say the cost here, before it is picked. */
                int needed = MaterialConsistency.TexturesNeededFor(_material, p.Mask);
                item.SubItems.Add(needed == 0 ? "" : needed + (needed == 1 ? " texture" : " textures"));
                if (needed != 0) item.ForeColor = Color.Firebrick;

                if (current) { item.Font = new Font(_list.Font, FontStyle.Bold); selectIndex = i; }
                _list.Items.Add(item);
            }
            _list.EndUpdate();

            if (selectIndex >= 0)
            {
                _list.Items[selectIndex].Selected = true;
                _list.EnsureVisible(selectIndex);
            }
            else if (_list.Items.Count != 0)
            {
                _list.Items[0].Selected = true;
            }

            Revalidate();
        }

        private void Revalidate()
        {
            int index = _list.SelectedIndices.Count == 0 ? -1 : _list.SelectedIndices[0];
            if (index >= 0 && index < _shown.Count)
                Mask = _shown[index].Mask;

            _status.Text = _shown.Count == _all.Count
                ? _all.Count + " combination" + (_all.Count == 1 ? "" : "s") + " available."
                : "Showing " + _shown.Count + " of " + _all.Count + " combinations.";

            if (_shown.Count == 0)
                _status.Text += "  Nothing shipped matches that set of features.";
            else if (_shown.Count == 1 && _shown[0].Mask == _currentMask)
                _status.Text += "  Only what this material already uses. Loosen a feature, or Clear filters.";

            _ok.Enabled = index >= 0 && Mask != _currentMask;
        }
    }
}
