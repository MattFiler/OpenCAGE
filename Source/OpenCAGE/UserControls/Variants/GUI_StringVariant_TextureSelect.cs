using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using System;
using System.Windows.Forms;

namespace OpenCAGE.UserControls
{
    public partial class GUI_StringVariant_TextureSelect : ParameterUserControl
    {
        cString _textureVal = null;
        EditTexture _popup = null;

        public GUI_StringVariant_TextureSelect() : base()
        {
            InitializeComponent();
            this.ContextMenuStrip = contextMenuStrip1;
            this.deleteToolStripMenuItem.Click += new EventHandler(deleteToolStripMenuItem_Click);
            Disposed += (s, e) =>
            {
                if (_popup != null)
                {
                    _popup.OnTextureSelected -= OnTextureSelected;
                    _popup.Close();
                    _popup = null;
                }
            };
        }

        public void PopulateUI(cString textureVal, string paramName)
        {
            _textureVal = textureVal;
            textBox1.Text = textureVal?.value ?? "";
            label1.Text = paramName;
            this.deleteToolStripMenuItem.Text = "Delete '" + paramName + "'";
            _hasDoneSetup = true;
        }

        private void SelectStr_Click(object sender, EventArgs e)
        {
            if (_popup != null)
            {
                _popup.OnTextureSelected -= OnTextureSelected;
                _popup.Close();
                _popup = null;
            }

            string path = _textureVal?.value;
            Textures.TEX4 current = null;
            int sourceIndex = 0;
            if (!string.IsNullOrEmpty(path) && Content?.Level?.Textures != null)
            {
                current = Content.Level.Textures.GetEnvironmentMapByPath(path);
                if (current == null && Singleton.Global?.Textures != null)
                {
                    current = Singleton.Global.Textures.GetEnvironmentMapByPath(path);
                    if (current != null)
                        sourceIndex = 1;
                }
            }

            _popup = new EditTexture(current, showSelectBtn: true, initialTextureSourceIndex: sourceIndex, environmentMapsOnly: true);
            _popup.OnTextureSelected += OnTextureSelected;
            _popup.Show();
        }

        private void OnTextureSelected(Textures.TEX4 texture)
        {
            if (_textureVal == null || texture == null)
                return;

            _textureVal.value = "n:\\content\\build\\textures\\" + texture.Name;
            textBox1.Text = _textureVal.value;

            HighlightAsModified();
        }

        public override void HighlightAsModified(bool updateDatabase = true, Control fontToUpdate = null)
        {
            base.HighlightAsModified(updateDatabase, label1);
        }
    }
}
