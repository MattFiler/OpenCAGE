using AlienPAK;
using CATHODE;
using CATHODE.Enums;
using OpenCAGE.Popups.Base;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static CATHODE.CustomCharacterAssetData;

namespace OpenCAGE.ConfigEditors
{
    public partial class CharacterAssetEditor : BaseWindow
    {
        CustomCharacterAssetData _assetData;
        CustomCharacterAssetData.AssetDefinition _assetDefinition = null;

        public CharacterAssetEditor() : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();

            _assetData = new CustomCharacterAssetData(Singleton.PathToAI + "/DATA/CHR_INFO/CUSTOMCHARACTERASSETDATA.BIN");

            assetSetList.BeginUpdate();
            assetSetList.Items.Clear();
            foreach (CUSTOM_CHARACTER_ASSETS assetSet in Enum.GetValues(typeof(CUSTOM_CHARACTER_ASSETS)))
            {
                assetSetList.Items.Add(assetSet.ToString());
            }
            assetSetList.Items.RemoveAt(assetSetList.Items.Count - 1);
            assetSetList.EndUpdate();

            this.Load += EditCharacterAssets_Load;
            this.FormClosing += EditCharacterAssets_FormClosing;
            Singleton.OnResetConfigs += () => { this.Close(); };
        }

        private void EditCharacterAssets_Load(object sender, EventArgs e)
        {
            assetSetList.SelectedIndex = 0;
        }

        private void EditCharacterAssets_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.Load -= EditCharacterAssets_Load;
            this.FormClosing -= EditCharacterAssets_FormClosing;

            if (_selectTexture != null)
            {
                _selectTexture.OnTextureSelected -= OnDecalSelected;
                _selectTexture.Close();
                _selectTexture = null;
            }
        }

        private void assetSetList_SelectedIndexChanged(object sender, EventArgs e)
        {
            _assetDefinition = _assetData.Entries[assetSetList.SelectedIndex];
            ReloadUI();
        }

        private void ReloadUI()
        {
            SetColourPreview(ColourType.PRIMARY, primaryColourList, primaryColourImageList);
            SetColourPreview(ColourType.SECONDARY, secondaryColourList, secondaryColourImageList);
            SetColourPreview(ColourType.TERTIARY, tertiaryColourList, tertiaryColourImageList);

            decalList.BeginUpdate();
            decalList.Items.Clear();
            decalImageList.Images.Clear();
            foreach (string decal in _assetDefinition.Decals)
            {
                Bitmap thumb = CreateDecalThumbnail(decal, Math.Max(16, decalImageList.ImageSize.Width));
                try
                {
                    decalImageList.Images.Add(thumb);
                    decalList.Items.Add(new ListViewItem(decal, decalImageList.Images.Count - 1) { Tag = decal });
                }
                finally
                {
                    thumb?.Dispose();
                }
            }
            decalList.EndUpdate();
        }

        private Bitmap CreateDecalThumbnail(string decal, int iconSize)
        {
            Bitmap thumb = new Bitmap(iconSize, iconSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Bitmap texture = Content.Level.Textures.Entries.FirstOrDefault(o => o.Name.ToUpper() == decal.ToUpper())?.ToBitmap();
            if (texture != null && texture.Width > 0 && texture.Height > 0)
            {
                try
                {
                    using (var g = Graphics.FromImage(thumb))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(texture, 0, 0, iconSize, iconSize);
                    }
                }
                finally
                {
                    texture.Dispose();
                }
            }
            else
            {
                texture?.Dispose();
                using (var g = Graphics.FromImage(thumb))
                    g.Clear(Color.LightGray);
            }
            return thumb;
        }

        private void SetColourPreview(ColourType colourType, ListView ui, ImageList imageList)
        {
            ui.BeginUpdate();
            ui.Items.Clear();
            imageList.Images.Clear();
            List<Vector3> colours = _assetDefinition.Tints[colourType];
            foreach (Vector3 colour in colours)
            {
                Color c = Color.FromArgb(255, (int)(colour.X * 255.0f), (int)(colour.Y * 255.0f), (int)(colour.Z * 255.0f));
                using (Bitmap bmp = new Bitmap(imageList.ImageSize.Width, imageList.ImageSize.Width, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(bmp))
                        g.Clear(c);
                    imageList.Images.Add(bmp);
                }
                int imageIndex = imageList.Images.Count - 1;
                ui.Items.Add(new ListViewItem(string.Empty, imageIndex) { BackColor = c, Tag = colour });
            }
            ui.EndUpdate();
        }

        private void addNewPrimary_Click(object sender, EventArgs e)
        {
            PickNewColour(ColourType.PRIMARY);
        }

        private void removeSelectedPrimary_Click(object sender, EventArgs e)
        {
            if (primaryColourList.SelectedItems.Count == 0)
                return;
            _assetDefinition.Tints[ColourType.PRIMARY].Remove((Vector3)primaryColourList.SelectedItems[0].Tag);
            ReloadUI();
        }

        private void addNewSecondary_Click(object sender, EventArgs e)
        {
            PickNewColour(ColourType.SECONDARY);
        }

        private void removeSelectedSecondary_Click(object sender, EventArgs e)
        {
            if (secondaryColourList.SelectedItems.Count == 0)
                return;
            _assetDefinition.Tints[ColourType.SECONDARY].Remove((Vector3)secondaryColourList.SelectedItems[0].Tag);
            ReloadUI();
        }

        private void addNewTertiary_Click(object sender, EventArgs e)
        {
            PickNewColour(ColourType.TERTIARY);
        }

        private void removeSelectedTertiary_Click(object sender, EventArgs e)
        {
            if (tertiaryColourList.SelectedItems.Count == 0)
                return;
            _assetDefinition.Tints[ColourType.TERTIARY].Remove((Vector3)tertiaryColourList.SelectedItems[0].Tag);
            ReloadUI();
        }

        private void PickNewColour(ColourType type)
        {
            ColorDialog colourPicker = new ColorDialog();
            colourPicker.CustomColors = SettingsManager.GetIntegerArray(Settings.CustomColours);
            if (colourPicker.ShowDialog() == DialogResult.OK)
            {
                _assetDefinition.Tints[type].Add(new Vector3(colourPicker.Color.R / 255.0f, colourPicker.Color.G / 255.0f, colourPicker.Color.B / 255.0f));
                ReloadUI();
            }
        }

        EditTexture _selectTexture = null;
        private void addNewDecal_Click(object sender, EventArgs e)
        {
            if (_selectTexture != null)
            {
                _selectTexture.OnTextureSelected -= OnDecalSelected;
                _selectTexture.Close();
                _selectTexture = null;
            }
            _selectTexture = new EditTexture();
            _selectTexture.Show();
            _selectTexture.OnTextureSelected += OnDecalSelected;
        }
        private void OnDecalSelected(Textures.TEX4 texture)
        {
            if (texture == null)
                return;

            _assetDefinition.Decals.Add(texture.Name);
            ReloadUI();
        }

        private void removeSelectedDecal_Click(object sender, EventArgs e)
        {
            if (decalList.SelectedItems.Count == 0)
                return;
            _assetDefinition.Decals.Remove((string)decalList.SelectedItems[0].Tag);
            ReloadUI();
        }

        private void saveBtn_Click(object sender, EventArgs e)
        {
            _assetData.Save();
            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
            this.Close();
        }
    }
}
