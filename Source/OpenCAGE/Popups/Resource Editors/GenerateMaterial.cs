using Assimp;
using CATHODE;
using CATHODE.ShaderTypes;
using CathodeLib;
using CathodeLib.Ubershaders;
using OpenCAGE.Popups.Base;
using OpenCAGE.Popups.UserControls;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Shows what <see cref="MaterialGenerator"/> will build from an imported model's material -
    /// which shader family, which textures, which features - and lets it be adjusted.
    ///
    /// This window changes nothing. It hands back a <see cref="MaterialGenerator.Plan"/> and the
    /// import carries it out, because generating adds textures to the level and an entry to the
    /// shader pool, and an import the user then cancels must not leave either behind.
    /// </summary>
    public partial class GenerateMaterial : BaseWindow
    {
        private readonly Scene _scene;
        private readonly Assimp.Material _source;
        private readonly Mesh _mesh;
        private readonly string _modelPath;
        private readonly bool _hasMetadata;
        private readonly Level _level;
        private readonly List<SHADER_LIST> _families = new List<SHADER_LIST>();

        private MaterialGenerator.Plan _plan;
        private bool _populating;

        /// <summary>The plan as the user left it, or null if the window was cancelled.</summary>
        public MaterialGenerator.Plan Result { get; private set; }

        public GenerateMaterial(Scene scene, Assimp.Material source, Mesh mesh, string modelPath, bool hasMetadata, Level level, string suggestedName)
            : base()
        {
            _scene = scene;
            _source = source;
            _mesh = mesh;
            _modelPath = modelPath;
            _hasMetadata = hasMetadata;
            _level = level;

            InitializeComponent();
            StayAboveEditor = true;

            foreach (ShaderPermutationService.Creatable creatable in ShaderPermutationService.CreatableFamilies(_level?.Shaders, Singleton.PathToAI))
                _families.Add(creatable.Family);

            SHADER_LIST suggested = MaterialGenerator.SuggestFamily(_mesh, _families);
            if (!_families.Contains(suggested)) _families.Insert(0, suggested);

            _populating = true;
            foreach (SHADER_LIST family in _families)
                familyList.Items.Add(family.ToString());
            familyList.SelectedIndex = Math.Max(0, _families.IndexOf(suggested));
            nameBox.Bind(suggestedName, () => _level?.Materials?.Entries.Select(o => o.Name) ?? Enumerable.Empty<string>());
            _populating = false;

            nameBox.ValidityChanged += (s, e) => UpdateCreateButton();
            Rebuild();
        }

        private SHADER_LIST SelectedFamily
        {
            get { return _families[Math.Max(0, Math.Min(familyList.SelectedIndex, _families.Count - 1))]; }
        }

        private void familyList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_populating) return;
            Rebuild();
        }

        private void alwaysNewBox_CheckedChanged(object sender, EventArgs e)
        {
            if (_populating || _plan == null) return;
            _plan.AlwaysCreateNew = alwaysNewBox.Checked;
            ShowPlan();
        }

        private void Rebuild()
        {
            if (_families.Count == 0)
            {
                summaryLabel.Text = "This level has no shaders to build a material from.";
                createBtn.Enabled = false;
                return;
            }

            _plan = MaterialGenerator.Describe(_scene, _source, _mesh, _modelPath, _hasMetadata, _level,
                                               SelectedFamily, nameBox.Value, Singleton.PathToAI);
            _plan.AlwaysCreateNew = alwaysNewBox.Checked;
            ShowPlan();
        }

        private void ShowPlan()
        {
            textureList.BeginUpdate();
            textureList.Items.Clear();
            foreach (MaterialGenerator.PlannedTexture texture in _plan.Textures)
            {
                ListViewItem item = new ListViewItem(texture.Role.Description);
                item.SubItems.Add(texture.Role.Sampler);
                item.SubItems.Add(texture.Existing != null
                    ? "reuse " + texture.Existing.Name
                    : texture.Usable ? texture.Role.Format + (texture.Role.SRGB ? ", sRGB" : ", linear") : "skipped");
                item.SubItems.Add(texture.Usable ? texture.SourceLabel : texture.SourceLabel + "  -  " + texture.Problem);
                if (!texture.Usable) item.ForeColor = SystemColors.GrayText;
                textureList.Items.Add(item);
            }
            if (textureList.Items.Count == 0)
                textureList.Items.Add(new ListViewItem(new[] { "(none)", "", "", "This model material carries no texture slots we can read." }) { ForeColor = SystemColors.GrayText });
            textureList.EndUpdate();

            featureList.BeginUpdate();
            featureList.Items.Clear();
            foreach (string feature in _plan.Features)
                featureList.Items.Add(feature);
            featureList.EndUpdate();

            List<string> lines = new List<string>();
            if (_plan.Error != null)
            {
                lines.Add(_plan.Error);
            }
            else
            {
                int importing = _plan.UsableTextures.Count(o => o.Existing == null);
                int reusing = _plan.UsableTextures.Count(o => o.Existing != null);

                lines.Add(string.Format("Permutation 0x{0:X} ({1}){2}.", _plan.Mask, _plan.Source,
                    _plan.ExactMask ? "" : " - nearest available, not an exact match"));
                lines.Add(importing + (importing == 1 ? " texture" : " textures") + " will be imported into this level"
                        + (reusing == 0 ? "" : ", " + reusing + " reused") + ".");

                Materials.Material shared = MaterialGenerator.WouldReuse(_plan, _level);
                if (shared != null)
                    lines.Add("'" + shared.Name + "' already in this level is identical, so it will be shared. Tick the box to build a separate one instead.");
                foreach (string note in _plan.Notes)
                    lines.Add(note);
            }
            summaryLabel.Text = string.Join("\r\n", lines);

            UpdateCreateButton();
        }

        private void UpdateCreateButton()
        {
            createBtn.Enabled = _plan != null && _plan.CanGenerate && nameBox.IsValid;
        }

        private void createBtn_Click(object sender, EventArgs e)
        {
            if (_plan == null || !_plan.CanGenerate) return;

            _plan.Name = nameBox.Value;
            Result = _plan;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void cancelBtn_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
