using CATHODE.ShaderTypes;
using CathodeLib.Ubershaders;
using OpenCAGE.Modding;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Asked before a brand new material is made: which shader family it runs on, and what to call it.
    ///
    /// Only families we can actually hand a working shader to are offered - the ones this level
    /// already carries, plus anything in the harvested shader database. A family with no shipped
    /// shader anywhere has nothing to start a material from, so it isn't listed at all.
    ///
    /// Nothing else is asked here. The permutation starts at the family's typical one and everything
    /// after that - features, samplers, parameters - is set in the editor the material opens in.
    /// </summary>
    public class NewMaterialPicker : Form
    {
        /// <summary>The family the user settled on.</summary>
        public SHADER_LIST Family { get; private set; }

        /// <summary>The name to store the material under.</summary>
        public string MaterialName { get { return _name.Text.Trim(); } }

        private readonly ComboBox _family = new ComboBox();
        private readonly TextBox _name = new TextBox();
        private readonly Label _detail = new Label();
        private readonly Button _ok = new Button();

        private readonly List<ShaderPermutationService.Creatable> _options;

        public NewMaterialPicker(List<ShaderPermutationService.Creatable> options)
        {
            _options = options ?? new List<ShaderPermutationService.Creatable>();

            Text = "New material";
            Icon = SharedFormIcon.Icon;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = SystemFonts.MessageBoxFont;
            ClientSize = new Size(460, 208);

            Label familyLabel = new Label { Text = "Shader type", Location = new Point(12, 12), Size = new Size(436, 18) };
            _family.DropDownStyle = ComboBoxStyle.DropDownList;
            _family.Location = new Point(12, 32);
            _family.Size = new Size(436, 21);
            _family.SelectedIndexChanged += (s, e) => Revalidate();
            foreach (ShaderPermutationService.Creatable option in _options)
                _family.Items.Add(Describe(option));

            _detail.Location = new Point(12, 58);
            _detail.Size = new Size(436, 34);
            _detail.ForeColor = SystemColors.GrayText;

            Label nameLabel = new Label { Text = "Material name", Location = new Point(12, 98), Size = new Size(436, 18) };
            _name.Location = new Point(12, 118);
            _name.Size = new Size(436, 20);
            _name.TextChanged += (s, e) => Revalidate();

            Label note = new Label
            {
                Text = "The material starts with no textures. Pick its features and samplers once it opens.",
                Location = new Point(12, 144),
                Size = new Size(436, 18),
                ForeColor = SystemColors.GrayText
            };

            _ok.Text = "Create";
            _ok.DialogResult = DialogResult.OK;
            _ok.Location = new Point(292, 170);
            _ok.Size = new Size(75, 26);

            Button cancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(373, 170),
                Size = new Size(75, 26)
            };

            Controls.Add(familyLabel);
            Controls.Add(_family);
            Controls.Add(_detail);
            Controls.Add(nameLabel);
            Controls.Add(_name);
            Controls.Add(note);
            Controls.Add(_ok);
            Controls.Add(cancel);
            AcceptButton = _ok;
            CancelButton = cancel;

            if (_family.Items.Count != 0)
                _family.SelectedIndex = DefaultIndex();
            _name.Text = "new material";
            _name.SelectAll();
            Revalidate();
        }

        /* CA_ENVIRONMENT is what most of the world is built from, so it opens on that where the level
         * has it - otherwise on whatever comes first. */
        private int DefaultIndex()
        {
            for (int i = 0; i < _options.Count; i++)
                if (_options[i].Family == SHADER_LIST.CA_ENVIRONMENT && _options[i].InLevel)
                    return i;
            for (int i = 0; i < _options.Count; i++)
                if (_options[i].InLevel)
                    return i;
            return 0;
        }

        private static string Describe(ShaderPermutationService.Creatable option)
        {
            return option.Family + (option.InLevel ? "" : "   (not used in this level)");
        }

        private void Revalidate()
        {
            int index = _family.SelectedIndex;
            if (index >= 0 && index < _options.Count)
            {
                ShaderPermutationService.Creatable option = _options[index];
                Family = option.Family;
                _detail.Text = option.Permutations + (option.Permutations == 1 ? " permutation" : " permutations") + " available, "
                    + (option.InLevel ? "from this level." : "from the harvested shader database.")
                    /* Worth saying before they commit rather than after: on a family with no
                     * reconstructed source the editor's feature checkboxes are read-only, and
                     * changing features means picking one of the combinations listed above. */
                    + (ShaderPermutationService.CanBuildArbitraryPermutations(option.Family)
                        ? ""
                        : "\r\nFeatures are chosen from those combinations - this shader type can't have new ones built.");
            }
            else
            {
                _detail.Text = "";
            }
            _ok.Enabled = index >= 0 && MaterialName.Length != 0;
        }
    }
}
