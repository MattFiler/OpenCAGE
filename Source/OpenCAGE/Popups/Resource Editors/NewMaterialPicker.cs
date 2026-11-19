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
    public partial class NewMaterialPicker : Form
    {
        /// <summary>The family the user settled on.</summary>
        public SHADER_LIST Family { get; private set; }

        /// <summary>The name to store the material under.</summary>
        public string MaterialName { get { return _name.Text.Trim(); } }

        private const string NoteText = "The material starts with no textures. Pick its features and samplers once it opens.";

        private readonly List<ShaderPermutationService.Creatable> _options;
        private readonly Func<string, string> _takenBy;

        /// <param name="takenBy">The name of the material already called this, or null when the name is free.</param>
        public NewMaterialPicker(List<ShaderPermutationService.Creatable> options, Func<string, string> takenBy = null)
        {
            InitializeComponent();

            _options = options ?? new List<ShaderPermutationService.Creatable>();
            _takenBy = takenBy;

            Icon = SharedFormIcon.Icon;
            //The system's message font, which the designer's Segoe UI 9 stands in for
            Font = SystemFonts.MessageBoxFont;

            foreach (ShaderPermutationService.Creatable option in _options)
                _family.Items.Add(Describe(option));

            if (_family.Items.Count != 0)
                _family.SelectedIndex = DefaultIndex();
            _name.SelectAll();
            Revalidate();
        }

        private void _family_SelectedIndexChanged(object sender, EventArgs e)
        {
            Revalidate();
        }

        private void _name_TextChanged(object sender, EventArgs e)
        {
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
            /* Two materials of one name can't be told apart by what finds a material by name (a material mapping's
             * target, an entity's material parameter), so a name already taken is refused here, as the asset name
             * boxes do */
            string takenBy = MaterialName.Length == 0 ? null : _takenBy?.Invoke(MaterialName);
            _note.Text = takenBy == null ? NoteText : "The level already has a material called '" + takenBy + "'. Pick another name.";
            _note.ForeColor = takenBy == null ? SystemColors.GrayText : Color.FromArgb(210, 90, 80);
            _ok.Enabled = index >= 0 && MaterialName.Length != 0 && takenBy == null;
        }
    }
}
